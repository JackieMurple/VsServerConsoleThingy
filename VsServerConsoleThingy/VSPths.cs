using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace VsServerConsoleThingy
{
    public class VSPths
    {
        public string? InstPth { get; private set; }
        public string? ExecPth { get; private set; }
        public string? AnnConfPth { get; private set; }

        private const string ConfigFileName = "vspaths.json";

        public VSPths()
        {
            LdPth();
            if (string.IsNullOrEmpty(InstPth) || string.IsNullOrEmpty(ExecPth))
            {
                DetPth();
            }
            ValPth();
        }

        private class PathsConfig
        {
            public string? InstallationPath { get; set; }
            public string? ServerExecutablePath { get; set; }
            public string? AnnouncerConfigPath { get; set; }
        }


        private void DetPth()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DetWinPth();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                DetLinPth();
            }
        }

        private void DetWinPth()
        {
            string[] potentialPaths =
            [
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vintagestory"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Vintagestory"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Vintagestory"),
            ];


            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "VintagestoryServer.exe")))
                {
                    InstPth = path;
                    ExecPth = Path.Combine(path, "VintagestoryServer.exe");
                    return;
                }
            }
        }

        private void DetLinPth()
        {
            string[] potentialPaths =
                    [
                     "/opt/vintagestory",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "vintagestory"),
                     "/usr/local/games/vintagestory",
                    ];


            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "VintagestoryServer")))
                {
                    InstPth = path;
                    ExecPth = Path.Combine(path, "VintagestoryServer");
                    return;
                }
            }
        }

        private void ValPth()
        {
            if (string.IsNullOrEmpty(InstPth) || !Directory.Exists(InstPth) ||
                string.IsNullOrEmpty(ExecPth) || !File.Exists(ExecPth))
            {
                InstPth = null;
                ExecPth = null;
            }
        }

        public async Task ManPth()
        {
            var window = new Window();
            var folderResult = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Vintage Story Server Installation Folder",
                AllowMultiple = false
            });

            if (folderResult.Count > 0)
            {
                string selectedPath = folderResult[0].Path.LocalPath;
                if (ValSelPth(selectedPath))
                {
                    InstPth = selectedPath;
                    ExecPth = Path.Combine(InstPth, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VintagestoryServer.exe" : "VintagestoryServer");
                    SvPth();
                }
                else
                {
                    throw new Exception("Selected folder does not contain VintagestoryServer executable.");
                }
            }
            else
            {
                throw new Exception("Vintage Story Server folder not selected");
            }
        }


        private static bool ValSelPth(string path)
        {
            return Directory.Exists(path) &&
                   File.Exists(Path.Combine(path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VintagestoryServer.exe" : "VintagestoryServer"));
        }

        private void SvPth()
        {
            var paths = new PathsConfig
            {
                InstallationPath = InstPth,
                ServerExecutablePath = ExecPth,
                AnnouncerConfigPath = AnnConfPth
            };

            string json = JsonSerializer.Serialize(paths);
            File.WriteAllText(ConfigFileName, json);
        }

        public void StPth(string installPath, string execPath, string annConfPath)
        {
            InstPth = installPath;
            ExecPth = execPath;
            AnnConfPth = annConfPath;
            MainWindow.ConfigPath = annConfPath;
            SvPth();
        }

        private void LdPth()
        {
            if (File.Exists(ConfigFileName))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFileName);
                    var paths = JsonSerializer.Deserialize<PathsConfig>(json);

                    if (paths != null)
                    {
                        InstPth = paths.InstallationPath;
                        ExecPth = paths.ServerExecutablePath;
                        AnnConfPth = paths.AnnouncerConfigPath;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error loading paths: {ex.Message}");
                }
            }
        }


    }
}
