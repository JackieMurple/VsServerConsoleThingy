using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvRichTextBox;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using Avalonia.Platform.Storage;


namespace VsServerConsoleThingy
{

    public partial class MainWindow : Window
    {
        private VSPths? vsPaths;
        private readonly List<Announcement> announcements = [];
        private string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "AnnouncerConfig.json"); private readonly RestartSettings restartSettings = new();
        private readonly string restartSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vintagestorydata", "ModConfig", "RestartSettings.json");
        private readonly DispatcherTimer restartTimer = new();
        private readonly ObservableCollection<string> currentPlayers = [];
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        private Control? txtConsole;
        private Process? serverProcess;


        private bool AutoSaveEnabled => AutoSave?.IsChecked == true;

        public MainWindow()
        {
            InitializeComponent();
            _ = InitVSPath();
            InitAsyc();
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
            AdminCheck.Click += TxtBx;
            Consoleswap(restartSettings.UseRichTextBox);
            _ = InitSrvSt();
            StartServer();
        }

        private async void InitAsyc()
        {
            await InitVSPath();
            await LdAnn();
            await InitSrvSt();
            await LdAnn();
        }
        private async Task InitSrvSt()
        {
            await InitVSPath();
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
                if (string.IsNullOrEmpty(vsPaths.InstPth))
                {
                    await SelDialog();
                }
            }
            catch (Exception)
            {
                vsPaths = null;
                await SelDialog();
            }
        }

        private async Task<string?> AskDir()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Vintage Story Data Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    return folders[0].Path.LocalPath;
                }
            }
            return null;
        }


        private async Task SelDialog()
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                new MessageBoxStandardParams
                {
                    ContentTitle = "Vintage Story Paths Not Found",
                    ContentMessage = "The Vintage Story installation couldn't be found automatically. Would you like to select it manually?",
                    ButtonDefinitions = ButtonEnum.YesNo
                });

            var result = await messageBox.ShowAsync();

            if (result == ButtonResult.Yes)
            {
                try
                {
                    if (vsPaths != null)
                    {
                        await vsPaths.ManPth();
                    }
                    else
                    {
                        TxtXChng("Error: VSPths is not initialized." + Environment.NewLine, Colors.Red);
                    }
                }
                catch (Exception ex)
                {
                    await MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ContentTitle = "Error",
                            ContentMessage = ex.Message,
                            ButtonDefinitions = ButtonEnum.Ok
                        }).ShowAsync();
                    Close();
                }
            }
            else
            {
                Close();
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

                    var paragraph = new Paragraph
                    {
                        Margin = new Thickness(0),
                        LineHeight = 0
                    };
                    var run = new EditableRun(text)
                    {
                        Foreground = new SolidColorBrush(color)
                    };
                    paragraph.Inlines.Add(run);

                    var scrollViewer = richTextBox.FindDescendantOfType<ScrollViewer>();

                    richTextBox.FlowDoc.Blocks.Add(paragraph);

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
                    Focusable = false
                };
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



        //private void AppendPrompt()
        //{
        //    TxtXChng(PROMPT, Colors.White);
        //     lastInputPosition = txtConsole.FlowDoc.Text.Length;
        //}

        // private string GetUserInput()
        //  {
        //      return txtConsole?.FlowDoc?.Text[lastInputPosition..].Trim() ?? string.Empty;
        //   }


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
                    //AppendPrompt();
                    currentPlayers.Clear();
                }
            }
        }

        private void ChckExst(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }


        private async void MainCls(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                e.Cancel = true;
                await StpSrvAsync();
                Close();
            }
        }

        private void StartServer()
        {
            if (restartSettings.StartAutomatically && (serverProcess == null || serverProcess.HasExited) && vsPaths != null && !string.IsNullOrEmpty(vsPaths.ExecPth))
            {
                BtnStrtSrvClk(null, null);
            }
        }

        private async Task LdAnn()
        {
            try
            {
                string? configDir = Path.GetDirectoryName(configPath);
                if (configDir == null)
                {
                    TxtXChng("Invalid configuration path." + Environment.NewLine, Colors.Red);
                    return;
                }

                if (!Directory.Exists(configDir))
                {
                    var messageBox = MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ContentTitle = "Directory Not Found",
                            ContentMessage = "The default configuration directory was not found. Would you like to select it manually?",
                            ButtonDefinitions = ButtonEnum.YesNo
                        });

                    var result = await messageBox.ShowAsync();

                    if (result == ButtonResult.Yes)
                    {
                        string? selectedDir = await AskDir();
                        if (selectedDir != null)
                        {
                            configPath = Path.Combine(selectedDir, "ModConfig", "AnnouncerConfig.json");
                        }
                        else
                        {
                            TxtXChng("No directory selected. Using default path." + Environment.NewLine, Colors.Yellow);
                        }
                    }
                }

                ChckExst(configPath);
                if (File.Exists(configPath))
                {
                    string json = await File.ReadAllTextAsync(configPath);
                    var loadedAnnouncements = JsonSerializer.Deserialize<List<Announcement>>(json, jsonOptions);
                    if (loadedAnnouncements != null)
                    {
                        announcements.Clear();
                        announcements.AddRange(loadedAnnouncements);
                    }
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
                ChckExst(configPath);
                string json = JsonSerializer.Serialize(announcements, jsonOptions);
                await File.WriteAllTextAsync(configPath, json);
            }
            catch (Exception ex)
            {
                TxtXChng($"Error saving announcements: {ex.Message}" + Environment.NewLine, Colors.Red);
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
                        Debug.WriteLine($"Player added: {playerName}");
                    }
                }
                else
                {
                    if (currentPlayers.Remove(playerName))
                    {
                        Debug.WriteLine($"Player removed: {playerName}");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to remove player: {playerName}");
                    }
                }
            });
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
                //AppendPrompt();
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
                    //AppendPrompt();
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
                var loadedSettings = JsonSerializer.Deserialize<RestartSettings>(json, jsonOptions);
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
            lstAnnouncements.ItemsSource = new ObservableCollection<string>(
                 announcements.Select(a => $"{a.Hour:D2}:{a.Minute:D2} - {a.Message}")
            );

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

    public class RestartSettings
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
}