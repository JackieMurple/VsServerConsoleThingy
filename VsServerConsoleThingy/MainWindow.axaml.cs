using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    public partial class MainWindow : Window
    {
        private VSPths? vsPaths;
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
        private static string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "AnnouncerConfig.json");
        //private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "AnnouncerConfig.json");
        private readonly ResSet restartSettings = new();
        private readonly string restartSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "ResSet.json");
        private readonly DispatcherTimer restartTimer = new();
        private readonly ObservableCollection<string> currentPlayers = [];
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        private Control? txtConsole;
        private Process? serverProcess;
        private static readonly FilePickerFileType JsonFileType = new("JSON Files") { Patterns = ["*.json"] };
        private int playerCount = 0;
        private static void SaveConfigPath()
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VsServerConsoleThingy", "config.json");
            string? directory = Path.GetDirectoryName(configFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(configFile, JsonSerializer.Serialize(new { ConfigPath = _configPath }));
        }

        private static void LoadConfigPath()
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VsServerConsoleThingy", "config.json");
            if (File.Exists(configFile))
            {
                string jsonContent = File.ReadAllText(configFile);
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty(nameof(ConfigPath), out JsonElement configPathElement))
                {
                    _configPath = configPathElement.GetString() ?? _configPath;
                }
            }
        }
        private HashSet<string> totalUniquePlayers = [];
        private HashSet<string> weeklyUniquePlayers = [];
        private DateTime lastWeekReset = DateTime.MinValue;

        private bool AutoSaveEnabled => AutoSave?.IsChecked == true;

        public MainWindow()
        {
            LoadConfigPath();
            InitializeComponent();
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
            else
            {
                Debug.WriteLine("_txtConsole not found or is not a supported control type!");
            }

            DataContext = this;

            DayWeek.ItemsSource = new ObservableCollection<string>(Enum.GetNames(typeof(DayOfWeek)));
            LdResSet();
            StartResTime();
            Consoleswap(true);

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
            Consoleswap(restartSettings.UseRichTextBox);
            ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "AnnouncerConfig.json");
            LdAnn2();
            LoadPlayerCounts();
        }

        private async void PthSetClk(object _sender, RoutedEventArgs _e)
        {
            var dialog = new PathSettingsWindow(this);
            await dialog.ShowDialog(this);
        }

        public void ResAnnClk(object sender, RoutedEventArgs e)
        {
            var ResAnnWin = new ResAnnWin();
            ResAnnWin.Show();
        }

        public void UpdatePlayerCounts(int totalPlayers, int weeklyPlayers)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotPlay.Text = totalPlayers.ToString();
                TotPlayWk.Text = weeklyPlayers.ToString();
            });
        }
        public void ConfPathabcdefg(string newPath)
        {
            ConfigPath = newPath;
            LdAnn2();
        }

        private void SavePlayerCounts()
        {
            string countsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VsServerConsoleThingy", "player_counts.json");
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
            string countsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VsServerConsoleThingy", "player_counts.json");

            if (File.Exists(countsPath))
            {
                string jsonContent = File.ReadAllText(countsPath);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(jsonContent, jsonOptions);

                        if (data.TryGetProperty("TotalUniquePlayers", out JsonElement totalUniquePlayers))
                        {
                            this.totalUniquePlayers = new HashSet<string>(totalUniquePlayers.EnumerateArray()
                                .Select(x => x.GetString() ?? string.Empty));
                        }
                        else
                        {
                            this.totalUniquePlayers = [];
                        }

                        if (data.TryGetProperty("LastWeekReset", out JsonElement lastWeekReset))
                        {
                            this.lastWeekReset = lastWeekReset.GetDateTime();
                        }
                        else
                        {
                            this.lastWeekReset = DateTime.MinValue;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                        this.totalUniquePlayers = [];
                        this.lastWeekReset = DateTime.MinValue;
                    }
                }
                else
                {
                    this.totalUniquePlayers = [];
                    this.lastWeekReset = DateTime.MinValue;
                }
            }
            else
            {
                this.totalUniquePlayers = [];
                this.lastWeekReset = DateTime.MinValue;
            }
        }


        private void LdAnn2()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var loadedAnnouncements = JsonSerializer.Deserialize<List<Announcement>>(json);
                    announcements.Clear();
                    if (loadedAnnouncements != null)
                    {
                        announcements.AddRange(loadedAnnouncements);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading announcements: {ex.Message}");
                }
            }
            else
            {
                announcements.Clear();
            }
        }

        public class ResAnnWin : Window
        {
            public ResAnnWin()
            {
                Title = "Restart Announcements";
                Width = 400;
                Height = 300;
                // add stuff later
            }
        }
        private async Task InitAsyc()
        {
            await InitVSPath();
            await LdAnn();
            StartServer();
        }
        private void TxtBx(object? sender, RoutedEventArgs e)
        {
            bool useRichTextBox = AdminCheck.IsChecked.GetValueOrDefault();
            Consoleswap(useRichTextBox);
            restartSettings.UseRichTextBox = useRichTextBox;
            SvResSet();
        }


        private async Task InitVSPath()
        {
            try
            {
                vsPaths = new VSPths();
                if (string.IsNullOrEmpty(vsPaths.InstPth) || string.IsNullOrEmpty(vsPaths.ExecPth))
                {
                    await SelDialog();
                }
            }
            catch (Exception ex)
            {
                TxtXChng($"Error initializing VSPths: {ex.Message}" + Environment.NewLine, Colors.Red);
                vsPaths = null;
            }
        }

        public enum PthSelTyp
        {
            VSInst,
            AnnConf
        }
        private async Task SelDialog()
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
                    vsPaths ??= new VSPths();

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

        private void TxtXChng(string text, Color color)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
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

                    var Par = new Paragraph
                    {
                        Margin = new Thickness(0),
                        LineHeight = 0
                    };
                    var run = new EditableRun(text)
                    {
                        Foreground = new SolidColorBrush(color)
                    };
                    Par.Inlines.Add(run);

                    var Scrl = richTextBox.FindDescendantOfType<ScrollViewer>();

                    richTextBox.FlowDoc.Blocks.Add(Par);

                }
                else if (txtConsole is TextBox textBox)
                {
                    textBox.Text += text;
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




        private void SrvInputDat(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && txtServerInput != null)
            {
                string input = txtServerInput.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(input) && serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcess.StandardInput.WriteLine(input);
                    TxtXChng($"Admin Input: {input}" + Environment.NewLine, Colors.MediumPurple);
                    txtServerInput.Text = string.Empty;
                }
            }
        }

        private async Task StpSrvAsync()
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                btnStopServer.IsEnabled = false;
                try
                {
                    if (AutoSaveEnabled)
                    {
                        serverProcess.StandardInput.WriteLine("/autosavenow");
                        TxtXChng("saving..." + Environment.NewLine, Colors.Green);
                        await Task.Delay(2000);
                    }
                    serverProcess.StandardInput.WriteLine("/stop");
                    TxtXChng("stopping..." + Environment.NewLine, Colors.White);
                    bool exited = await Task.Run(() => serverProcess.WaitForExit(10000));
                    if (exited)
                    {
                        TxtXChng("Server has stopped" + Environment.NewLine, Colors.White);
                    }
                    else
                    {
                        TxtXChng("Server did not stop within the expected time. Forcing shutdown..." + Environment.NewLine, Colors.Red);
                        serverProcess.Kill();
                    }
                }
                catch (Exception ex)
                {
                    TxtXChng($"Error while stopping the server: {ex.Message}" + Environment.NewLine, Colors.Red);
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

        //private static void ChckExst(string path)
        //{
        //   string? directory = Path.GetDirectoryName(path);
        //    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        //    {
        //        Directory.CreateDirectory(directory);
        //    }
        //}


        private async void MainCls(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                e.Cancel = true;
                await StpSrvAsync();
                SavePlayerCounts();
                Close();
            }
            else
            {
                SavePlayerCounts();
            }
        }

        private void StartServer()
        {
            if (restartSettings.StartAutomatically && (serverProcess == null || serverProcess.HasExited) && vsPaths != null && !string.IsNullOrEmpty(vsPaths.ExecPth))
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
                if (!File.Exists(ConfigPath))
                {
                    string? newPath = await AnnConfDir();
                    if (newPath != null)
                    {
                        ConfigPath = newPath;
                    }
                    else
                    {
                        announcements.Add(new Announcement { Hour = 6, Minute = 0, Message = "placeholder announcement" });
                        await SvAnn();
                        return;
                    }
                }

                string json = await File.ReadAllTextAsync(ConfigPath);
                var loadedAnnouncements = JsonSerializer.Deserialize<List<Announcement>>(json, jsonOptions);
                if (loadedAnnouncements != null)
                {
                    announcements.Clear();
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
                TxtXChng($"Error loading announcements: {ex.Message}" + Environment.NewLine, Colors.Red);
            }
        }

        private async Task SvAnn()
        {
            try
            {
                string? configDir = Path.GetDirectoryName(ConfigPath);
                if (configDir != null)
                {
                    Directory.CreateDirectory(configDir);
                }

                string json = JsonSerializer.Serialize(announcements, jsonOptions);
                await File.WriteAllTextAsync(ConfigPath, json);
            }
            catch (Exception ex)
            {
                TxtXChng($"Error saving announcements: {ex.Message}" + Environment.NewLine, Colors.Red);
            }
        }

        private void ResetWeeklyCounts()
        {
            if ((DateTime.Now - lastWeekReset).TotalDays >= 7)
            {
                weeklyUniquePlayers.Clear();
                lastWeekReset = DateTime.Now;
            }
        }

        private void UpPlayLst(string playerName, bool isJoining)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (isJoining)
                {
                    if (!currentPlayers.Contains(playerName))
                    {
                        currentPlayers.Add(playerName);
                        playerCount++;
                        totalUniquePlayers.Add(playerName);
                        weeklyUniquePlayers.Add(playerName);
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
                UpdatePlayerCounts(totalUniquePlayers.Count, weeklyUniquePlayers.Count);
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
                UpdatePlayerCounts(totalUniquePlayers.Count, weeklyUniquePlayers.Count);
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


        private void BtnStrtSrvClk(object? sender, RoutedEventArgs? e)
        {
            if (vsPaths == null || string.IsNullOrEmpty(vsPaths.ExecPth))
            {
                TxtXChng("Vintage Story paths are not initialized." + Environment.NewLine, Colors.Red);
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
                    }
                };
                serverProcess.OutputDataReceived += SrvPrcOutDat;
                serverProcess.ErrorDataReceived += SrvPrcErrDat;
                serverProcess.Start();
                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();
                TxtXChng("Server started." + Environment.NewLine, Colors.White);
            }
        }

        private void SrvPrcOutDat(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
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
                        UpPlayLst(playerName, true);
                    }
                    else if (e.Data.Contains("[Server Event] Player", StringComparison.OrdinalIgnoreCase) && e.Data.Contains("left.", StringComparison.OrdinalIgnoreCase))
                    {
                        string playerName = PlayerName(e.Data);
                        UpPlayLst(playerName, false);
                    }

                    TxtXChng(e.Data + Environment.NewLine, textColor);
                });
            }
        }



        private void SrvPrcErrDat(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TxtXChng(e.Data + Environment.NewLine, Colors.Red);
                });
            }
        }

        private async void BtnStpSrv(object? sender, RoutedEventArgs e)
        {
            await StpSrvAsync();
        }

        private void Whitelister(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && whitelist != null && serverProcess != null && !serverProcess.HasExited)
            {
                string playerName = whitelist.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(playerName))
                {
                    serverProcess.StandardInput.WriteLine($"/whitelist {playerName} add");
                    TxtXChng($"Whitelisting {playerName}..." + Environment.NewLine, Colors.White);
                    whitelist.Text = string.Empty;
                }
            }
        }

        private void Blacklister(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && blacklist != null && serverProcess != null && !serverProcess.HasExited)
            {
                string playerName = blacklist.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(playerName))
                {
                    serverProcess.StandardInput.WriteLine($"/ban {playerName}");
                    TxtXChng($"Banning {playerName}..." + Environment.NewLine, Colors.Red);
                    blacklist.Text = string.Empty;
                }
            }
        }

        private void SvResSet()
        {
            restartSettings.UseRichTextBox = AdminCheck.IsChecked ?? false;
            string json = JsonSerializer.Serialize(restartSettings, jsonOptions);
            File.WriteAllText(restartSettingsPath, json);
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
                    restartSettings.StartAutomatically = loadedSettings.StartAutomatically;
                    restartSettings.LastRestartDate = loadedSettings.LastRestartDate;
                    restartSettings.UseRichTextBox = loadedSettings.UseRichTextBox;
                }
            }
            else
            {
                restartSettings.WeekDay = DayOfWeek.Sunday;
                restartSettings.Hour = 6;
                restartSettings.Minute = 0;
                restartSettings.LastRestartDate = DateTime.MinValue;
                restartSettings.UseRichTextBox = false;
            }
            ResCtrlChng();
        }

        private void ResCtrlChng()
        {
            EnableRestart.IsChecked = restartSettings.Enabled;
            DailyRestart.IsChecked = restartSettings.IsDaily;
            WeeklyRestart.IsChecked = !restartSettings.IsDaily;
            AdminCheck.IsChecked = restartSettings.UseRichTextBox;

            int validHour = Math.Min(Math.Max(restartSettings.Hour, 0), 23);
            TimeHour.Value = validHour;

            TimeMinute.Value = Math.Min(Math.Max(restartSettings.Minute, 0), 59);

            if (DayWeek.Items.Count > 0)
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
                bool shouldRestart = restartSettings.IsDaily
                    ? now.Hour == restartSettings.Hour && now.Minute == restartSettings.Minute
                    : now.DayOfWeek == restartSettings.WeekDay &&
                      now.Hour == restartSettings.Hour &&
                      now.Minute == restartSettings.Minute;

                if (shouldRestart && now.Date != restartSettings.LastRestartDate.Date)
                {
                    await RestartServer();
                }
            }
        }

        private async Task RestartServer()
        {
            await StpSrvAsync();
            BtnStrtSrvClk(null, null);
            restartSettings.LastRestartDate = DateTime.Now;
            SvResSet();
        }

        private void UpAnnLst()
        {
            if (lstAnnouncements != null)
            {
                lstAnnouncements.ItemsSource = new ObservableCollection<string>(
                    announcements.Select(a => $"{a.Hour:D2}:{a.Minute:D2} - {a.Message}")
                );
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
        public bool StartAutomatically { get; set; } = true;
        public DateTime LastRestartDate { get; set; }
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

        public PathSettingsWindow(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            Title = "Path Settings";
            Width = 800;
            Height = 250;
            vsPaths = new VSPths();

            InstPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.InstPth ?? "" };
            ExecPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.ExecPth ?? "" };
            AnnConfPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.AnnConfPth ?? MainWindow.ConfigPath ?? "" };

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

            InstPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.InstPth ?? "" };
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

            ExecPath = new TextBox { Margin = new Thickness(5), Text = vsPaths.ExecPth ?? "" };
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

            AnnConfPath = new TextBox { Margin = new Thickness(5), Text = MainWindow.ConfigPath ?? "" };
            grid.Children.Add(AnnConfPath);
            Grid.SetRow(grid.Children[^1], 2);
            Grid.SetColumn(grid.Children[^1], 1);

            BtnAnnConfig = new Button { Content = "Browse", Margin = new Thickness(5) };
            BtnAnnConfig.Click += BrwsAnnConf;
            grid.Children.Add(BtnAnnConfig);
            Grid.SetRow(grid.Children[^1], 2);
            Grid.SetColumn(grid.Children[^1], 2);

            btnSave = new Button { Content = "Save", Margin = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right };
            btnSave.Click += BtnSv;
            grid.Children.Add(btnSave);
            Grid.SetRow(grid.Children[^1], 3);
            Grid.SetColumn(grid.Children[^1], 1);
            Grid.SetColumnSpan(grid.Children[^1], 2);

            Content = grid;
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
            if (string.IsNullOrEmpty(InstPath.Text) || string.IsNullOrEmpty(ExecPath.Text) || string.IsNullOrEmpty(AnnConfPath.Text))
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
            UpdateConfigPath(AnnConfPath.Text);
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
}