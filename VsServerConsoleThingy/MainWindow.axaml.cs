using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvRichTextBox;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace VsServerConsoleThingy
{

    public class ServerManagerConfig
    {
        public string ConfigPath { get; set; } = string.Empty;
        public ResSet RestartSettings { get; set; } = new();
        public List<Announcement> Announcements { get; set; } = [];
        public string InstallationPath { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public HashSet<string> TotalUniquePlayers { get; set; } = [];
        public HashSet<string> WeeklyUniquePlayers { get; set; } = [];
        public DateTime LastWeekReset { get; set; } = DateTime.MinValue;
        public bool UseRichTextBox { get; set; } = false;
        public string BackupFolderPath { get; set; } = Path.Combine(MainWindow.GetAppRootDirectory(), "Backups");
        public int MaxBackups { get; set; } = 5;


    }

    public partial class MainWindow : Window
    {
        private VSPths? vsPaths;
        private ServerManagerConfig config = new();
        public ServerManagerConfig Config => config;
        public static string GetAppRootDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static readonly string ServerManagerConfigPath = Path.Combine(
            GetAppRootDirectory(),
            "ServerManagerConfig.json");

        private readonly List<Announcement> announcements = [];

        public static string ConfigPath
        {
            get => _configPath;
            set
            {
                _configPath = value;
                SaveConfigPath();
            }
        }


        private static string _configPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "vintagestorydata",
                                "ModConfig",
                                "AnnouncerConfig.json");

        private readonly ResSet restartSettings = new();
        private readonly string restartSettingsPath = Path.Combine(
            GetAppRootDirectory(),
            "ResSet.json");
        private readonly ObservableCollection<string> currentPlayers = [];
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        private static readonly FilePickerFileType JsonFileType = new("JSON Files") { Patterns = ["*.json"] };
        private int playerCount;
        private readonly DispatcherTimer restartTimer = new();
        private Control? txtConsole;
        private Process? serverProcess;
        private const int MAX_CONSOLE_LINES = 1000;


        private readonly HashSet<string> totalUniquePlayers = [];
        private readonly HashSet<string> weeklyUniquePlayers = [];
        private readonly DateTime lastWeekReset = DateTime.MinValue;
        private DateTime lastAnnouncementTime = DateTime.MinValue;

        private bool AutoSaveEnabled => AutoSave?.IsChecked == true;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ConfigPath = config.ConfigPath;
            LdAnn2();
            LdResSet();
            ConfigPath = restartSettings.ConfigPath;
            
            StrtParam();
            DataContext = this;
            _ = InitAsyc();
            txtConsole = this.FindControl<Control>("_txtConsole");

            if (txtConsole is TextBox textBox)
            {
                textBox.Padding = new Thickness(0);
            }
            else if (txtConsole is AvRichTextBox.RichTextBox richTextBox)
            {
                richTextBox.Padding = new Thickness(0);
            }

            DataContext = this;

            DayWeek.ItemsSource = new ObservableCollection<string>(Enum.GetNames(typeof(DayOfWeek)));
            StartResTime();
            Consoleswap(true);
            txtStrtParam.TextChanged += TxtStrtParam_TextChanged;
            btnStartServer.Click += BtnStrtSrvClk;
            btnStopServer.Click += BtnStpSrv;
            btnAddAnnouncement.Click += BtnAnnClick;
            btnRemoveAnnouncement.Click += BtnRemClick;
            whitelist.KeyDown += Whitelister;
            blacklist.KeyDown += Blacklister;
            Closing += MainCls;
            txtServerInput.KeyDown += SrvInputDat;
            EnableRestart.Click += EnRes;
            DailyRestart.Click += DlyRes;
            TimeHour.ValueChanged += ChngHr;
            TimeMinute.ValueChanged += ChngMin;
            DayWeek.SelectionChanged += DyWkSel;
            lstPlayers.ItemsSource = currentPlayers;
            PlayerUpdate();
            AdminCheck.Click += TxtBx;
            Consoleswap(restartSettings.RchTxt);
            ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "AnnouncerConfig.json");
            LoadPlayerCounts();
            BackupNum.Text = config.MaxBackups.ToString();
            BackupNum.LostFocus += BackupNum_LostFocus;
        }

        public async void ConfPathabcdefg(string newPath)
        {
            ConfigPath = newPath;
            config.ConfigPath = newPath;
            await SaveConfig();
            await LdAnn();
        }

        private async void BackupNum_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(BackupNum.Text, out int maxBackups))
            {
                config.MaxBackups = maxBackups;
                await SaveConfig();
            }
            else
            {
                BackupNum.Text = config.MaxBackups.ToString();
            }
        }

        private void StrtParam()
        {
            txtStrtParam = this.FindControl<TextBox>("txtStrtParam");
            if (txtStrtParam != null)
            {
                txtStrtParam.Watermark = "Enter startup parameters";
                txtStrtParam.Margin = new Thickness(5);
                txtStrtParam.Text = restartSettings.StartupParameters;
                txtStrtParam.LostFocus += TxtStrtParam_Focus;
                txtStrtParam.TextChanged += TxtStrtParam_TextChanged;
            }
            else
            {
                Debug.WriteLine("txtStrtParam not found");
            }
        }

        private void TxtStrtParam_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (txtStrtParam != null)
            {
                restartSettings.StartupParameters = txtStrtParam.Text ?? string.Empty;
                SvResSet();
            }
        }

        private void TxtStrtParam_Focus(object? sender, RoutedEventArgs e)
        {
            if (txtStrtParam != null)
            {
                restartSettings.StartupParameters = txtStrtParam.Text ?? string.Empty;
                SvResSet();
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(ServerManagerConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ServerManagerConfigPath);
                    config = JsonSerializer.Deserialize<ServerManagerConfig>(json, jsonOptions) ?? new ServerManagerConfig();
                    if (!string.IsNullOrEmpty(config.ConfigPath))
                    {
                        ConfigPath = config.ConfigPath;
                    }
                    else
                    {
                        Debug.WriteLine("ConfigPath is empty in loaded config");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading config: {ex.Message}");
                    config = new ServerManagerConfig();
                }
            }
            else
            {
                Debug.WriteLine("Config file not found, creating new config");
                config = new ServerManagerConfig();
            }
        }

        public async Task SaveConfig()
        {
            config.ConfigPath = ConfigPath;
            string json = JsonSerializer.Serialize(config, jsonOptions);
            await File.WriteAllTextAsync(ServerManagerConfigPath, json);
            Debug.WriteLine($"Saved ConfigPath: {config.ConfigPath}");
        }

        private async void PthSetClk(object _sender, RoutedEventArgs _e)
        {
            var dialog = new PathSettingsWindow(this);
            await dialog.ShowDialog(this);
        }
        private void BackupNum_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (!char.IsDigit((char)e.Key) && e.Key != Avalonia.Input.Key.Back)
            {
                e.Handled = true;
            }
        }


        public void ResAnnClk(object sender, RoutedEventArgs e)
        {
            var resAnnWin = new ResAnnWin(restartSettings);
            resAnnWin.SetOwner(this);
            resAnnWin.Closed += (s, args) =>
            {
                resAnnWin.SaveSettings();
                SaveRestartSettings();
            };
            resAnnWin.Show();
        }

        public void UpdatePlayerCounts(int totalPlayers, int weeklyPlayers)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotPlay.Text = totalPlayers.ToString();
                TotPlayWk.Text = weeklyPlayers.ToString();
            });
        }
        private static void SaveConfigPath()
        {
            File.WriteAllText(ServerManagerConfigPath, ConfigPath);
        }

        private static void LoadConfigPath()
        {
            if (File.Exists(ServerManagerConfigPath))
            {
                ConfigPath = File.ReadAllText(ServerManagerConfigPath);
            }
        }

        private void SavePlayerCounts()
        {
            string countsPath = Path.Combine(GetAppRootDirectory(), "player_counts.json");
            var data = new
            {
                TotalUniquePlayers = totalUniquePlayers.ToList(),
                WeeklyUniquePlayers = weeklyUniquePlayers.ToList(),
                LastWeekReset = lastWeekReset
            };
            File.WriteAllText(countsPath, JsonSerializer.Serialize(data, jsonOptions));
        }

        private void LoadPlayerCounts()
        {
            restartSettings.TotalUniquePlayers = restartSettings.TotalUniquePlayers ?? [];
            restartSettings.WeeklyUniquePlayers = restartSettings.WeeklyUniquePlayers ?? [];
            restartSettings.LastWeekReset = restartSettings.LastWeekReset;

            UpdatePlayerCounts(restartSettings.TotalUniquePlayers.Count, restartSettings.WeeklyUniquePlayers.Count);
        }


        private void LdAnn2()
        {
            announcements.Clear();
            announcements.AddRange(config.Announcements);
        }

        private async Task InitAsyc()
        {
            LoadConfig();
            ConfigPath = config.ConfigPath;
            vsPaths = new VSPths(config);
            await InitVSPath();
            if (!string.IsNullOrEmpty(ConfigPath))
            {
                await LdAnn();
            }
            else
            {
                await SelDialog(PthSelTyp.AnnConf);
            }
            StartServer();
        }
        private void TxtBx(object? sender, RoutedEventArgs e)
        {
            bool useRichTextBox = AdminCheck.IsChecked.GetValueOrDefault();
            Consoleswap(useRichTextBox);
            restartSettings.RchTxt = useRichTextBox;
            SvResSet();
        }


        private async Task InitVSPath()
        {
            try
            {
                await TxtXChng("Checking VS Paths...\n", Colors.White);

                await LdAnn();

                await TxtXChng($"Config InstallationPath: {config.InstallationPath}\n", Colors.Yellow);
                await TxtXChng($"Config ExecutablePath: {config.ExecutablePath}\n", Colors.Yellow);

                vsPaths = new VSPths(config);

                await TxtXChng($"VSPths InstPth: {vsPaths.InstPth}\n", Colors.Yellow);
                await TxtXChng($"VSPths ExecPth: {vsPaths.ExecPth}\n", Colors.Yellow);

                if (!string.IsNullOrEmpty(config.InstallationPath) && !string.IsNullOrEmpty(config.ExecutablePath))
                {
                    await TxtXChng("Paths found in config, setting them...\n", Colors.Green);
                    vsPaths.StPth(config.InstallationPath, config.ExecutablePath, config.ConfigPath);
                }
                else if (string.IsNullOrEmpty(vsPaths.InstPth) || string.IsNullOrEmpty(vsPaths.ExecPth))
                {
                    await TxtXChng("Paths missing, prompting for selection...\n", Colors.Orange);
                    await SelDialog(PthSelTyp.VSInst);

                    if (!string.IsNullOrEmpty(vsPaths.InstPth) && !string.IsNullOrEmpty(vsPaths.ExecPth))
                    {
                        await TxtXChng("New paths selected, updating config...\n", Colors.Green);
                        config.InstallationPath = vsPaths.InstPth;
                        config.ExecutablePath = vsPaths.ExecPth;
                        await SaveConfig();
                    }
                }

                await TxtXChng($"Final VSPths InstPth: {vsPaths.InstPth}\n", Colors.Yellow);
                await TxtXChng($"Final VSPths ExecPth: {vsPaths.ExecPth}\n", Colors.Yellow);

                if (string.IsNullOrEmpty(vsPaths.InstPth) || string.IsNullOrEmpty(vsPaths.ExecPth))
                {
                    throw new Exception("Vintage Story paths are not set.");
                }

                await TxtXChng("Paths verified.\n", Colors.White);
            }
            catch (Exception ex)
            {
                await TxtXChng($"Error initializing VSPths: {ex.Message}\n", Colors.Red);
                vsPaths = null;
            }
        }

        public enum PthSelTyp
        {
            VSInst,
            AnnConf
        }
        private async Task SelDialog(PthSelTyp type)
        {
            if (type == PthSelTyp.AnnConf && !string.IsNullOrEmpty(ConfigPath))
            {
                return;
            }
            else
            {
                var MsgBx = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ContentTitle = "Vintage Story Server Executable Not Found",
                        ContentMessage = "The Vintage Story Server executable couldn't be found automatically. Would you like to select the installation folder manually?",
                        ButtonDefinitions = ButtonEnum.YesNo
                    });
                var Res = await MsgBx.ShowAsync();
                if (Res == ButtonResult.Yes)
                {
                    try
                    {
                        vsPaths = new VSPths(config);

                        await vsPaths.ManPth();
                        if (string.IsNullOrEmpty(vsPaths.InstPth) || string.IsNullOrEmpty(vsPaths.ExecPth))
                        {
                            throw new Exception("Invalid Vintage Story Server installation folder selected.");
                        }
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard(
                            new MessageBoxStandardParams
                            {
                                ContentTitle = "Error - Vintage Story Server Executable",
                                ContentMessage = ex.Message,
                                ButtonDefinitions = ButtonEnum.Ok
                            }).ShowAsync();
                        vsPaths = null;
                    }
                }
                else
                {
                    vsPaths = null;
                }
            }
        }

        private async Task TxtXChng(string text, Color color)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (txtConsole == null)
                {
                    Debug.WriteLine("txtConsole is null in TxtXChng");
                    return;
                }

                if (txtConsole is AvRichTextBox.RichTextBox richTextBox)
                {
                    if (richTextBox.FlowDoc == null)
                    {
                        Debug.WriteLine("FlowDoc is null in TxtXChng");
                        return;
                    }

                    var paragraph = new Paragraph
                    {
                        Margin = new Thickness(0),
                        LineHeight = 0,
                        Inlines = { new EditableRun(text) { Foreground = new SolidColorBrush(color) } }
                    };

                    richTextBox.FlowDoc.Blocks.Add(paragraph);

                    while (richTextBox.FlowDoc.Blocks.Count > MAX_CONSOLE_LINES)
                    {
                        richTextBox.FlowDoc.Blocks.RemoveAt(0);
                    }
                }
                else if (txtConsole is TextBox textBox)
                {
                    textBox.Text += text;

                    var lines = textBox.Text.Split('\n');
                    if (lines.Length > MAX_CONSOLE_LINES)
                    {
                        textBox.Text = string.Join("\n", lines.Skip(lines.Length - MAX_CONSOLE_LINES));
                    }

                    textBox.CaretIndex = textBox.Text.Length;
                }
            });
        }



        private void Consoleswap(bool useRichTextBox)
        {
            var grid = this.FindControl<Grid>("ConsoleGrid");
            if (grid == null) return;

            grid.Children.Clear();

            if (useRichTextBox)
            {
                var richTextBox = new AvRichTextBox.RichTextBox
                {
                    Name = "_txtConsole",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.Parse("#E0F0FF")),
                    FontFamily = "Consolas",
                    Margin = new Thickness(10),
                    Padding = new Thickness(10),
                    BorderThickness = new Thickness(0),
                    Focusable = false
                };
                grid.Children.Add(richTextBox);
                txtConsole = richTextBox;
            }
            else
            {
                var textBox = new TextBox
                {
                    Name = "_txtConsole",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.Parse("#E0F0FF")),
                    FontFamily = "Consolas",
                    Margin = new Thickness(10),
                    Padding = new Thickness(10),
                    BorderThickness = new Thickness(0),
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                };
                textBox.Classes.Add("NoFoc");

                grid.Children.Add(textBox);
                txtConsole = textBox;

            }

            if (AdminCheck != null)
            {
                AdminCheck.IsChecked = useRichTextBox;
            }
        }




        private async void SrvInputDat(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && txtServerInput != null)
            {
                string input = txtServerInput.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(input) && serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcess.StandardInput.WriteLine(input);
                    await TxtXChng($"Admin Input: {input}" + Environment.NewLine, Colors.MediumPurple);
                    txtServerInput.Text = string.Empty;
                }
            }
        }

        private async Task StpSrv()
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                btnStopServer.IsEnabled = false;
                try
                {
                    if (AutoSaveEnabled)
                    {
                        serverProcess.StandardInput.WriteLine("/autosavenow");
                        await TxtXChng("saving..." + Environment.NewLine, Colors.Green);
                        await Task.Delay(2000);
                    }

                    if (Backup.IsChecked == true)
                    {
                        serverProcess.StandardInput.WriteLine("/genbackup");
                        await TxtXChng("Generating backup..." + Environment.NewLine, Colors.Green);
                        await WtBack();
                    }

                    serverProcess.StandardInput.WriteLine("/stop");
                    await TxtXChng("stopping..." + Environment.NewLine, Colors.White);
                    await serverProcess.WaitForExitAsync();
                    await TxtXChng("Server has stopped" + Environment.NewLine, Colors.White);
                    if (Backup.IsChecked == true)
                    {
                        await ManBack();
                    }
                }
                catch (Exception ex)
                {
                    await TxtXChng($"Error while stopping the server: {ex.Message}" + Environment.NewLine, Colors.Red);
                }
                finally
                {
                    serverProcess = null;
                    btnStopServer.IsEnabled = true;
                    currentPlayers.Clear();
                    playerCount = 0;
                    PlayerUpdate();
                }
            }
        }

        private async Task WtBack()
        {
            var backupCompletionTask = Task.Run(async () =>
            {
                while (true)
                {
                    string? output = serverProcess != null ? await serverProcess.StandardOutput.ReadLineAsync() : null;
                    if (output != null && output.Contains("[Server Notification] Backup Complete!"))
                    {
                        return;
                    }
                    await Task.Delay(100);
                }
            });

            await Task.WhenAny(backupCompletionTask, Task.Delay(TimeSpan.FromMinutes(5)));
        }

        private async Task ManBack()
        {
            int maxBackups = config.MaxBackups;
            if (maxBackups > 0)
            {
                string backupFolder = config.BackupFolderPath;
                if (Directory.Exists(backupFolder))
                {
                    var backupFiles = new DirectoryInfo(backupFolder)
                        .GetFiles("*.vcdbs")
                        .OrderByDescending(f => f.CreationTime)
                        .ToList();

                    while (backupFiles.Count > maxBackups)
                    {
                        var oldestBackup = backupFiles.Last();
                        try
                        {
                            await Task.Run(() => File.Delete(oldestBackup.FullName));
                            await TxtXChng($"Deleted old backup: {oldestBackup.Name}" + Environment.NewLine, Colors.Orange);
                        }
                        catch (Exception ex)
                        {
                            await TxtXChng($"Error deleting backup {oldestBackup.Name}: {ex.Message}" + Environment.NewLine, Colors.Red);
                        }
                        backupFiles.RemoveAt(backupFiles.Count - 1);
                    }
                }
                else
                {
                    await TxtXChng($"Backup folder not found: {backupFolder}" + Environment.NewLine, Colors.Red);
                }
            }
            else
            {
                await TxtXChng("Invalid backup number. Please enter a positive integer." + Environment.NewLine, Colors.Red);
            }
        }

        private async void MainCls(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                e.Cancel = true;
                await StpSrv();
            }
            SavePlayerCounts();
            await SaveConfig();
            Close();
        }

        private void StartServer()
        {
            if (restartSettings.Auto && (serverProcess == null || serverProcess.HasExited) && vsPaths != null && !string.IsNullOrEmpty(vsPaths.ExecPth))
            {
                playerCount = 0;
                PlayerUpdate();
                BtnStrtSrvClk(null, null);
            }
        }

        private async Task<string?> AnnConfDir()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var FsRes = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Announcer Config Path",
                    AllowMultiple = false,
                    FileTypeFilter = [JsonFileType]
                });

                if (FsRes.Count > 0)
                {
                    return FsRes[0].Path.LocalPath;
                }
            }
            return null;
        }

        private async Task LdAnn()
        {
            try
            {
                if (string.IsNullOrEmpty(ConfigPath))
                {
                    Debug.WriteLine("ConfigPath is empty");
                    string? newPath = await AnnConfDir();
                    if (newPath != null)
                    {
                        ConfigPath = newPath;
                        config.ConfigPath = newPath;
                        await SaveConfig();
                    }
                    else
                    {
                        throw new Exception("No config path selected");
                    }
                }

                if (!File.Exists(ConfigPath))
                {
                    Debug.WriteLine($"Config file not found at {ConfigPath}, creating default");
                    announcements.Clear();
                    announcements.Add(new Announcement { Hour = 6, Minute = 0, Message = "placeholder announcement" });
                    await SvAnn();
                    return;
                }

                string json = await File.ReadAllTextAsync(ConfigPath);
                var loadedAnnouncements = JsonSerializer.Deserialize<List<Announcement>>(json, jsonOptions);
                announcements.Clear();
                if (loadedAnnouncements != null)
                {
                    announcements.AddRange(loadedAnnouncements);
                }
                else
                {
                    announcements.Add(new Announcement { Hour = 6, Minute = 0, Message = "placeholder announcement" });
                    await SvAnn();
                }

                UpAnnLst();
            }
            catch (Exception ex)
            {
                await TxtXChng($"Error loading announcements: {ex.Message}" + Environment.NewLine, Colors.Red);
            }
        }

        private async Task SvAnn()
        {
            string json = JsonSerializer.Serialize(announcements, jsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json).ConfigureAwait(false);
        }

        private async void ResetWeeklyCounts()
        {
            if ((DateTime.Now - config.LastWeekReset).TotalDays >= 7)
            {
                config.WeeklyUniquePlayers.Clear();
                config.LastWeekReset = DateTime.Now;
                await SaveConfig();
            }
        }

        private async Task UpPlayLst(string playerName, bool isJoining)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (isJoining)
                {
                    if (!currentPlayers.Contains(playerName))
                    {
                        currentPlayers.Add(playerName);
                        playerCount++;
                        config.TotalUniquePlayers.Add(playerName);
                        config.WeeklyUniquePlayers.Add(playerName);
                        Debug.WriteLine($"Player added: {playerName}");
                    }
                }
                else
                {
                    if (currentPlayers.Remove(playerName))
                    {
                        playerCount--;
                        Debug.WriteLine($"Player removed: {playerName}");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to remove player: {playerName}");
                    }
                }
                PlayerUpdate();
                UpdatePlayerCounts(config.TotalUniquePlayers.Count, config.WeeklyUniquePlayers.Count);
                await SaveConfig();
            });
        }

        private void PlayerUpdate()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResetWeeklyCounts();
                var oldCountItem = currentPlayers.FirstOrDefault(item => item.StartsWith("Players Online:"));
                if (oldCountItem != null)
                {
                    currentPlayers.Remove(oldCountItem);
                }
                currentPlayers.Insert(0, $"Players Online: {playerCount}");
                UpdatePlayerCounts(config.TotalUniquePlayers.Count, config.WeeklyUniquePlayers.Count);
            });
        }


        private void PnBtnClk(object sender, RoutedEventArgs e)
        {
            var sidebar = this.FindControl<SplitView>("Sidebar");
            if (sidebar != null)
            {
                sidebar.IsPaneOpen = !sidebar.IsPaneOpen;
            }
        }




        private static string PlayerName(string serverOutput)
        {
            if (serverOutput.Contains("[Server Event] Player", StringComparison.OrdinalIgnoreCase))
            {
                int startIndex = serverOutput.IndexOf("Player ") + 7;
                int endIndex = serverOutput.IndexOf(" left.", startIndex);
                return (startIndex != -1 && endIndex != -1) ? serverOutput[startIndex..endIndex].Trim() : string.Empty;
            }
            else
            {
                int startIndex = serverOutput.IndexOf(']') + 1;
                int endIndex = serverOutput.IndexOf('[', startIndex);
                return (startIndex != -1 && endIndex != -1) ? serverOutput[startIndex..endIndex].Trim() : string.Empty;
            }
        }


        private async void BtnStrtSrvClk(object? sender, RoutedEventArgs? e)
        {
            if (vsPaths == null || string.IsNullOrEmpty(vsPaths.ExecPth))
            {
                await TxtXChng("Vintage Story paths are not initialized." + Environment.NewLine, Colors.Red);
                return;
            }

            if (serverProcess == null || serverProcess.HasExited)
            {
                serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = vsPaths.ExecPth,
                        WorkingDirectory = vsPaths.InstPth,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        Arguments = txtStrtParam.Text
                    }
                };
                serverProcess.OutputDataReceived += SrvPrcOutDat;
                serverProcess.ErrorDataReceived += SrvPrcErrDat;
                serverProcess.Start();
                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();
                await TxtXChng($"Server started with arguments: {restartSettings.StartupParameters}" + Environment.NewLine, Colors.White);
            }
        }

        private void SrvPrcOutDat(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Task.Run(async () =>
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        Color textColor = Colors.White;
                        if (e.Data.Contains("Server Error", StringComparison.OrdinalIgnoreCase))
                        {
                            textColor = Colors.Red;
                        }
                        else if (e.Data.Contains("Server Warning", StringComparison.OrdinalIgnoreCase))
                        {
                            textColor = Colors.Orange;
                        }

                        if (e.Data.Contains("joins.", StringComparison.OrdinalIgnoreCase))
                        {
                            string playerName = PlayerName(e.Data);
                            await UpPlayLst(playerName, true);
                        }
                        else if (e.Data.Contains("[Server Event] Player", StringComparison.OrdinalIgnoreCase) && e.Data.Contains("left.", StringComparison.OrdinalIgnoreCase))
                        {
                            string playerName = PlayerName(e.Data);
                            await UpPlayLst(playerName, false);
                        }

                        await TxtXChng(e.Data + Environment.NewLine, textColor);
                    });
                });
            }
        }



        private void SrvPrcErrDat(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await TxtXChng(e.Data + Environment.NewLine, Colors.Red);
                });
            }
        }


        private async void BtnStpSrv(object? sender, RoutedEventArgs e)
        {
            await StpSrv();
        }

        private async void Whitelister(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && whitelist != null && serverProcess != null && !serverProcess.HasExited)
            {
                string playerName = whitelist.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(playerName))
                {
                    serverProcess.StandardInput.WriteLine($"/whitelist {playerName} add");
                    await TxtXChng($"Whitelisting {playerName}..." + Environment.NewLine, Colors.White);
                    whitelist.Text = string.Empty;
                }
            }
        }
        public void SaveRestartSettings()
        {
            SvResSet();
        }
        private async void Blacklister(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && blacklist != null && serverProcess != null && !serverProcess.HasExited)
            {
                string playerName = blacklist.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(playerName))
                {
                    serverProcess.StandardInput.WriteLine($"/ban {playerName}");
                    await TxtXChng($"Banning {playerName}..." + Environment.NewLine, Colors.Red);
                    blacklist.Text = string.Empty;
                }
            }
        }

        private void SvResSet()
        {
            restartSettings.RchTxt = AdminCheck.IsChecked ?? false;
            restartSettings.StartupParameters = txtStrtParam.Text ?? string.Empty;

            string json = JsonSerializer.Serialize(restartSettings, jsonOptions);
            File.WriteAllText(restartSettingsPath, json);

            restartSettings.EnableAnn = restartSettings.EnableAnn;
            restartSettings.AnnSrtMin = restartSettings.AnnSrtMin;
            restartSettings.AnnInt = restartSettings.AnnInt;
        }

        private void LdResSet()
        {
            if (File.Exists(restartSettingsPath))
            {
                string json = File.ReadAllText(restartSettingsPath);
                var loadedSettings = JsonSerializer.Deserialize<ResSet>(json, jsonOptions);
                if (loadedSettings != null)
                {
                    restartSettings.Enabled = loadedSettings.Enabled;
                    restartSettings.IsDaily = loadedSettings.IsDaily;
                    restartSettings.Hour = Math.Min(Math.Max(loadedSettings.Hour, 0), 23);
                    restartSettings.Minute = loadedSettings.Minute;
                    restartSettings.WeekDay = loadedSettings.WeekDay;
                    restartSettings.Auto = loadedSettings.Auto;
                    restartSettings.LastResDt = loadedSettings.LastResDt;
                    restartSettings.RchTxt = loadedSettings.RchTxt;
                    restartSettings.EnableAnn = loadedSettings.EnableAnn;
                    restartSettings.AnnSrtMin = loadedSettings.AnnSrtMin;
                    restartSettings.AnnInt = loadedSettings.AnnInt;
                    restartSettings.StartupParameters = loadedSettings.StartupParameters;
                    restartSettings.ConfigPath = loadedSettings.ConfigPath;
                    restartSettings.Announcements = loadedSettings.Announcements;
                    restartSettings.TotalUniquePlayers = loadedSettings.TotalUniquePlayers;
                    restartSettings.WeeklyUniquePlayers = loadedSettings.WeeklyUniquePlayers;
                    restartSettings.LastWeekReset = loadedSettings.LastWeekReset;
                    restartSettings.BackupFolderPath = loadedSettings.BackupFolderPath;
                    restartSettings.MaxBackups = loadedSettings.MaxBackups;
                    restartSettings.UseRichTextBox = loadedSettings.UseRichTextBox;
                }
            }
            else
            {
                restartSettings.WeekDay = DayOfWeek.Sunday;
                restartSettings.Hour = 6;
                restartSettings.Minute = 0;
                restartSettings.LastResDt = DateTime.MinValue;
                restartSettings.RchTxt = false;
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResCtrlChng();
            });
        }



        private void ResCtrlChng()
        {
            if (EnableRestart != null)
                EnableRestart.IsChecked = restartSettings.Enabled;

            if (DailyRestart != null)
                DailyRestart.IsChecked = restartSettings.IsDaily;

            if (WeeklyRestart != null)
                WeeklyRestart.IsChecked = !restartSettings.IsDaily;

            if (AdminCheck != null)
                AdminCheck.IsChecked = restartSettings.RchTxt;

            if (TimeHour != null)
            {
                int validHour = Math.Min(Math.Max(restartSettings.Hour, 0), 23);
                TimeHour.Value = validHour;
            }

            if (TimeMinute != null)
            {
                TimeMinute.Value = Math.Min(Math.Max(restartSettings.Minute, 0), 59);
            }

            if (DayWeek != null && DayWeek.Items.Count > 0)
            {
                DayWeek.SelectedIndex = (int)restartSettings.WeekDay;
            }
        }


        private void EnRes(object? sender, RoutedEventArgs e)
        {
            restartSettings.Enabled = EnableRestart.IsChecked == true;
            SvResSet();
        }

        private void DlyRes(object? sender, RoutedEventArgs e)
        {
            restartSettings.IsDaily = DailyRestart.IsChecked == true;
            DayWeek.IsEnabled = !restartSettings.IsDaily;
            SvResSet();
        }

        private void ChngHr(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            restartSettings.Hour = (int)Math.Min(Math.Max(e.NewValue ?? 0, 0), 23);
            SvResSet();
        }

        private void ChngMin(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            restartSettings.Minute = (int)Math.Min(Math.Max(e.NewValue ?? 0, 0), 59);
            SvResSet();
        }

        private void DyWkSel(object? sender, SelectionChangedEventArgs e)
        {
            if (DayWeek.SelectedIndex >= 0)
            {
                restartSettings.WeekDay = (DayOfWeek)DayWeek.SelectedIndex;
                SvResSet();
            }
        }

        private void StartResTime()
        {
            restartTimer.Interval = TimeSpan.FromSeconds(10);
            restartTimer.Tick += ResTime;
            restartTimer.Start();
        }

        private async void ResTime(object? sender, EventArgs e)
        {
            if (restartSettings.Enabled && serverProcess != null && !serverProcess.HasExited)
            {
                DateTime now = DateTime.Now;
                DateTime nextRestartTime = Nxres(now);
                TimeSpan timeUntilRestart = nextRestartTime - now;

                await ChkAnn(timeUntilRestart);

                bool shouldRestart = restartSettings.IsDaily
                    ? now.Hour == restartSettings.Hour && now.Minute == restartSettings.Minute
                    : now.DayOfWeek == restartSettings.WeekDay &&
                      now.Hour == restartSettings.Hour &&
                      now.Minute == restartSettings.Minute;

                if (shouldRestart && now.Date != restartSettings.LastResDt.Date)
                {
                    await RestartServer();
                }
            }
        }

        private DateTime Nxres(DateTime now)
        {
            if (restartSettings.IsDaily)
            {
                return new DateTime(now.Year, now.Month, now.Day, restartSettings.Hour, restartSettings.Minute, 0)
                    .AddDays(now.TimeOfDay >= new TimeSpan(restartSettings.Hour, restartSettings.Minute, 0) ? 1 : 0);
            }
            else
            {
                int daysUntilNextRestart = ((int)restartSettings.WeekDay - (int)now.DayOfWeek + 7) % 7;
                return new DateTime(now.Year, now.Month, now.Day, restartSettings.Hour, restartSettings.Minute, 0)
                    .AddDays(daysUntilNextRestart);
            }
        }

        private async Task ChkAnn(TimeSpan timeUntilRestart)
        {
            int minutesUntilRestart = (int)Math.Ceiling(timeUntilRestart.TotalMinutes);

            if (restartSettings.EnableAnn &&
                minutesUntilRestart <= restartSettings.AnnSrtMin &&
                restartSettings.AnnInt.Contains(minutesUntilRestart) &&
                (DateTime.Now - lastAnnouncementTime).TotalMinutes >= 1)
            {
                await SResAnn(minutesUntilRestart);
                lastAnnouncementTime = DateTime.Now;
            }
        }


        private async Task SResAnn(int minutesUntilRestart)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                string message = $"Server will restart in {minutesUntilRestart} minute(s).";
                await Task.Run(() => serverProcess.StandardInput.WriteLine($"/announce {message}"));
            }
        }

        private async Task RestartServer()
        {
            await StpSrv();
            BtnStrtSrvClk(null, null);
            restartSettings.LastResDt = DateTime.Now;
            lastAnnouncementTime = DateTime.MinValue;
            SvResSet();
        }

        private void UpAnnLst()
        {
            if (lstAnnouncements != null)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    lstAnnouncements.ItemsSource = new ObservableCollection<string>(
                        announcements.Select(a => $"{a.Hour:D2}:{a.Minute:D2} - {a.Message}")
                    );
                });
            }
        }


        private async void BtnAnnClick(object? sender, RoutedEventArgs e)
        {
            int hour = (int)(numHour.Value ?? 0);
            int minute = (int)(numMinute.Value ?? 0);
            string message = txtMessage.Text ?? string.Empty;

            announcements.Add(new Announcement { Hour = hour, Minute = minute, Message = message });
            await SvAnn();
            UpAnnLst();
        }

        private async void BtnRemClick(object? sender, RoutedEventArgs e)
        {
            if (lstAnnouncements.SelectedIndex >= 0)
            {
                announcements.RemoveAt(lstAnnouncements.SelectedIndex);
                await SvAnn();
                UpAnnLst();
            }
        }
    }

    public class ResSet
    {
        public bool Enabled { get; set; }
        public bool IsDaily { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public DayOfWeek WeekDay { get; set; }
        public bool Auto { get; set; } = true;
        public DateTime LastResDt { get; set; }
        public bool RchTxt { get; set; } = false;
        public bool EnableAnn { get; set; } = true;
        public int AnnSrtMin { get; set; } = 15;
        public List<int> AnnInt { get; set; } = [15, 10, 5, 1];
        public string StartupParameters { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public List<Announcement> Announcements { get; set; } = [];
        public HashSet<string> TotalUniquePlayers { get; set; } = [];
        public HashSet<string> WeeklyUniquePlayers { get; set; } = [];
        public DateTime LastWeekReset { get; set; } = DateTime.MinValue;
        public string BackupFolderPath { get; set; } = Path.Combine(MainWindow.GetAppRootDirectory(), "Backups");
        public int MaxBackups { get; set; } = 5;
        public bool UseRichTextBox { get; set; } = false;

    }

    public class Announcement
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    public class PathSettingsWindow : Window
    {
        private readonly TextBox InstPath;
        private readonly TextBox ExecPath;
        private readonly TextBox AnnConfPath;
        private readonly Button BtnInst;
        private readonly Button BtnExec;
        private readonly Button BtnAnnConfig;
        private readonly Button btnSave;
        private readonly VSPths vsPaths;
        private static readonly string[] ExecutableFileTypes = ["exe"];
        private static readonly string[] JsonFileTypes = ["json"];
        private readonly MainWindow mainWindow;
        private readonly TextBox BackupFolderPath;
        private readonly Button BtnBackupFolder;

        public PathSettingsWindow(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            Title = "Path Settings";
            Width = 800;
            Height = 250;
            vsPaths = new VSPths(new ServerManagerConfig());

            InstPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.InstPth ?? string.Empty };
            ExecPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.ExecPth ?? string.Empty };
            AnnConfPath = new TextBox { Margin = new Thickness(5), Text = MainWindow.ConfigPath ?? string.Empty };

            var grid = new Grid
            {
                RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
                ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
            };

            grid.Children.Add(new TextBlock { Text = "Installation Path:", Margin = new Thickness(5) });
            Grid.SetRow(grid.Children[^1], 0);
            Grid.SetColumn(grid.Children[^1], 0);

            InstPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.InstPth ?? string.Empty };
            grid.Children.Add(InstPath);
            Grid.SetRow(grid.Children[^1], 0);
            Grid.SetColumn(grid.Children[^1], 1);

            BtnInst = new Button { Content = "Browse", Margin = new Thickness(5) };
            BtnInst.Click += BrwsInst;
            grid.Children.Add(BtnInst);
            Grid.SetRow(grid.Children[^1], 0);
            Grid.SetColumn(grid.Children[^1], 2);

            grid.Children.Add(new TextBlock { Text = "Executable Path:", Margin = new Thickness(5) });
            Grid.SetRow(grid.Children[^1], 1);
            Grid.SetColumn(grid.Children[^1], 0);

            ExecPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.ExecPth ?? string.Empty };
            grid.Children.Add(ExecPath);
            Grid.SetRow(grid.Children[^1], 1);
            Grid.SetColumn(grid.Children[^1], 1);

            BtnExec = new Button { Content = "Browse", Margin = new Thickness(5) };
            BtnExec.Click += BrwsExec;
            grid.Children.Add(BtnExec);
            Grid.SetRow(grid.Children[^1], 1);
            Grid.SetColumn(grid.Children[^1], 2);

            grid.Children.Add(new TextBlock { Text = "Announcer Config Path:", Margin = new Thickness(5) });
            Grid.SetRow(grid.Children[^1], 2);
            Grid.SetColumn(grid.Children[^1], 0);

            AnnConfPath = new TextBox { Margin = new Thickness(5), Text = MainWindow.ConfigPath ?? string.Empty };
            grid.Children.Add(AnnConfPath);
            Grid.SetRow(grid.Children[^1], 2);
            Grid.SetColumn(grid.Children[^1], 1);

            BtnAnnConfig = new Button { Content = "Browse", Margin = new Thickness(5) };
            BtnAnnConfig.Click += BrwsAnnConf;
            grid.Children.Add(BtnAnnConfig);
            Grid.SetRow(grid.Children[^1], 2);
            Grid.SetColumn(grid.Children[^1], 2);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = "Backup Folder Path:", Margin = new Thickness(5) });
            Grid.SetRow(grid.Children[^1], 3);
            Grid.SetColumn(grid.Children[^1], 0);

            BackupFolderPath = new TextBox { Margin = new Thickness(5), Text = mainWindow.Config.BackupFolderPath ?? string.Empty };
            grid.Children.Add(BackupFolderPath);
            Grid.SetRow(grid.Children[^1], 3);
            Grid.SetColumn(grid.Children[^1], 1);

            BtnBackupFolder = new Button { Content = "Browse", Margin = new Thickness(5) };
            BtnBackupFolder.Click += BrwsBackupFolder;
            grid.Children.Add(BtnBackupFolder);
            Grid.SetRow(grid.Children[^1], 3);
            Grid.SetColumn(grid.Children[^1], 2);

            btnSave = new Button { Content = "Save", Margin = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right };
            btnSave.Click += BtnSv;
            grid.Children.Add(btnSave);
            Grid.SetRow(grid.Children[^1], 3);
            Grid.SetColumn(grid.Children[^1], 1);
            Grid.SetColumnSpan(grid.Children[^1], 2);

            Content = grid;
        }

        private async void BrwsBackupFolder(object? _sender, RoutedEventArgs _e)
        {
            var path = await SelFol("Select Backup Folder");
            if (path != null)
            {
                BackupFolderPath.Text = path;
            }
        }

        public void UpdateConfigPath(string newPath)
        {
            mainWindow.ConfPathabcdefg(newPath);
        }
        private async void BrwsInst(object? _sender, RoutedEventArgs _e)
        {
            var path = await SelFol("Select Vintage Story/Server Installation Folder");
            if (path != null)
            {
                InstPath.Text = path;
            }
        }

        private async void BrwsExec(object? _sender, RoutedEventArgs _e)
        {
            var path = await SelFil("Select Vintage Story Server Executable", ExecutableFileTypes);
            if (path != null)
            {
                ExecPath.Text = path;
            }
        }

        private async void BrwsAnnConf(object? _sender, RoutedEventArgs _e)
        {
            var path = await SelFil("Select Announcer Config File", JsonFileTypes);
            if (path != null)
            {
                AnnConfPath.Text = path;
            }
        }

        private async void BtnSv(object? _sender, RoutedEventArgs _e)
        {
            if (string.IsNullOrEmpty(InstPath.Text) || string.IsNullOrEmpty(ExecPath.Text) ||
                string.IsNullOrEmpty(AnnConfPath.Text) || string.IsNullOrEmpty(BackupFolderPath.Text))
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ContentTitle = "Error",
                        ContentMessage = "All paths must be filled.",
                        ButtonDefinitions = ButtonEnum.Ok
                    }).ShowAsync();
                return;
            }

            vsPaths.StPth(InstPath.Text, ExecPath.Text, AnnConfPath.Text);
            MainWindow.ConfigPath = AnnConfPath.Text;
            mainWindow.Config.ConfigPath = AnnConfPath.Text;
            mainWindow.Config.BackupFolderPath = BackupFolderPath.Text;
            await mainWindow.SaveConfig();
            Close();
        }

        private async Task<string?> SelFol(string title)
        {
            var FolRes = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            return FolRes.Count > 0 ? FolRes[0].Path.LocalPath : null;
        }

        private async Task<string?> SelFil(string title, string[] fileTypes)
        {
            var FilRes = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Files")
                    {
                         Patterns = fileTypes.Select(ft => $"*.{ft}").ToArray()
                        }
                ]
            });

            return FilRes.Count > 0 ? FilRes[0].Path.LocalPath : null;
        }
    }
    public class ResAnnWin : Window
    {
        private readonly CheckBox EnableAnn;
        private readonly NumericUpDown AnnMinSel;
        private readonly TextBox AnnMinTxt;
        private readonly Button SaveButton;
        private readonly ResSet restartSettings;

        public ResAnnWin(ResSet restartSettings)
        {
            this.restartSettings = restartSettings;
            Title = "Restart Announcements";
            Width = 500;
            Height = 380;

            var grid = new Grid
            {
                Margin = new Thickness(20),
                RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
            };

            EnableAnn = new CheckBox
            {
                Content = "Enable Announcements",
                IsChecked = restartSettings.EnableAnn,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 20)
            };
            grid.Children.Add(EnableAnn);
            Grid.SetRow(EnableAnn, 0);

            grid.Children.Add(new TextBlock
            {
                Text = "Minutes before restart to start announcements:",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });
            Grid.SetRow(grid.Children[^1], 1);

            AnnMinSel = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 60,
                Value = restartSettings.AnnSrtMin,
                Width = 150,
                Height = 30,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            grid.Children.Add(AnnMinSel);
            Grid.SetRow(AnnMinSel, 2);

            grid.Children.Add(new TextBlock
            {
                Text = "Announcement intervals (comma-separated minutes):",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });
            Grid.SetRow(grid.Children[^1], 3);

            AnnMinTxt = new TextBox
            {
                Text = string.Join(", ", restartSettings.AnnInt),
                FontSize = 14,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 20)
            };
            grid.Children.Add(AnnMinTxt);
            Grid.SetRow(AnnMinTxt, 4);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            SaveButton = new Button
            {
                Content = "Save",
                FontSize = 16,
                Padding = new Thickness(20, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 20, 0, 0)
            };
            SaveButton.Click += SvBtnClk;
            grid.Children.Add(SaveButton);
            Grid.SetRow(SaveButton, 6);

            Content = grid;
        }

        public void SetOwner(Window owner)
        {
            Owner = owner;
        }

        public void SaveSettings()
        {
            restartSettings.EnableAnn = EnableAnn.IsChecked ?? false;
            restartSettings.AnnSrtMin = (int)(AnnMinSel.Value ?? 0);
            restartSettings.AnnInt = AnnMinTxt.Text?
                .Split(',')
                .Select(s => int.TryParse(s.Trim(), out int result) ? result : -1)
                .Where(i => i > 0)
                .OrderByDescending(i => i)
                .ToList() ?? [];
        }

        private void SvBtnClk(object? sender, RoutedEventArgs e)
        {
            SaveSettings();
            Close();
        }
    }

}