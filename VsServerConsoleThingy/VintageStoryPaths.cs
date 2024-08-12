using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Runtime.InteropServices;

namespace VsServerConsoleThingy
{
    public class VintageStoryPaths
    {
        public string? InstallationPath { get; private set; }
        public string? ServerExecutablePath { get; private set; }

        private const string ConfigFileName = "vspaths.json";

        public VintageStoryPaths()
        {
            LoadSavedPaths();
            if (string.IsNullOrEmpty(InstallationPath))
            {
                DetectPaths();
            }
            ValidatePaths();
        }

        private void DetectPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DetectWindowsPaths();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                DetectLinuxPaths();
            }
        }

        private void DetectWindowsPaths()
        {
            string[] potentialPaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vintagestory"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Vintagestory"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Vintagestory"),
            };

            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "VintagestoryServer.exe")))
                {
                    InstallationPath = path;
                    ServerExecutablePath = Path.Combine(path, "VintagestoryServer.exe");
                    return;
                }
            }
        }

        private void DetectLinuxPaths()
        {
            string[] potentialPaths =
            {
                "/opt/vintagestory",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "vintagestory"),
                "/usr/local/games/vintagestory",
            };

            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "VintagestoryServer")))
                {
                    InstallationPath = path;
                    ServerExecutablePath = Path.Combine(path, "VintagestoryServer");
                    return;
                }
            }
        }

        private void ValidatePaths()
        {
            if (string.IsNullOrEmpty(InstallationPath) || !Directory.Exists(InstallationPath) ||
                string.IsNullOrEmpty(ServerExecutablePath) || !File.Exists(ServerExecutablePath))
            {
                InstallationPath = null;
                ServerExecutablePath = null;
            }
        }

        public async Task ManuallySelectPaths()
        {
            var window = new Window();
            var folderResult = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Vintage Story Installation Folder",
                AllowMultiple = false
            });

            if (folderResult.Count > 0)
            {
                string selectedPath = folderResult[0].Path.LocalPath;
                if (ValidateSelectedPath(selectedPath))
                {
                    InstallationPath = selectedPath;
                    ServerExecutablePath = Path.Combine(InstallationPath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VintagestoryServer.exe" : "VintagestoryServer");
                    SavePaths();
                }
                else
                {
                    throw new Exception("Selected folder is not a valid Vintage Story installation.");
                }
            }
            else
            {
                throw new Exception("Vintage Story installation folder not selected.");
            }
        }

        private static bool ValidateSelectedPath(string path)
        {
            return Directory.Exists(path) &&
                   File.Exists(Path.Combine(path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VintagestoryServer.exe" : "VintagestoryServer"));
        }

        private void SavePaths()
        {
            var paths = new
            {
                InstallationPath,
                ServerExecutablePath
            };

            string json = JsonSerializer.Serialize(paths);
            File.WriteAllText(ConfigFileName, json);
        }

        private void LoadSavedPaths()
        {
            if (File.Exists(ConfigFileName))
            {
                string json = File.ReadAllText(ConfigFileName);
                var paths = JsonSerializer.Deserialize<dynamic>(json);

                if (paths != null)
                {
                    InstallationPath = paths.InstallationPath?.ToString();
                    ServerExecutablePath = paths.ServerExecutablePath?.ToString();
                }
            }
        }
    }
}
