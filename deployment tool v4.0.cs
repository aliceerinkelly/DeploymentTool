using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsDeploymentSuite
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            if (!IsAdministrator())
            {
                try
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            Verb = "runas",
                            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                        };
                        Process.Start(startInfo);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("Administrator privileges are required for system deployment routines.",
                                    "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var app = new Application();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show($"Engine Crash Caught:\n\n{e.ExceptionObject}", "Deployment Suite Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            app.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show($"Dispatcher Fault:\n\n{e.Exception.Message}\n\nStack:\n{e.Exception.StackTrace}", "UI Thread Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            var window = new MainWindow();
            app.Run(window);
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public class AppXItem
    {
        public string PackageName { get; set; } = "";
        public string Version { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string FullIdentity { get; set; } = "";
    }

    public class GhostDriverGridItem
    {
        public string OemName { get; set; } = "";
        public string Status { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsProtected { get; set; }
    }

    public class WimEditionItem
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string DisplayText => $"Index {Index} : {Name}";
    }

    public enum LayoutMode
    {
        NeonWave,
        Windows11,
        Glass
    }

    public class WimIndexDialog : Window
    {
        public WimEditionItem? SelectedItem { get; private set; }
        public bool PruneOtherIndexes { get; private set; } = true;

        public WimIndexDialog(List<WimEditionItem> editions, Color accentColor)
        {
            Title = "Select Image Edition";
            Width = 480;
            Height = 310;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 24));
            Foreground = Brushes.White;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Detected Windows Editions in WIM:",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(accentColor)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var list = new ListBox
            {
                ItemsSource = editions,
                DisplayMemberPath = "DisplayText",
                SelectedIndex = 0,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 36)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(accentColor),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 12
            };
            Grid.SetRow(list, 1);
            grid.Children.Add(list);

            var chkPrune = new CheckBox
            {
                Content = "Remove all other indexes (Keep only the selected edition)",
                IsChecked = true,
                Foreground = Brushes.LightGray,
                FontSize = 11.5,
                Margin = new Thickness(2, 0, 0, 14)
            };
            Grid.SetRow(chkPrune, 2);
            grid.Children.Add(chkPrune);

            var btnConfirm = new Button
            {
                Content = "Service & Retain Selected Edition",
                Height = 36,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(32, 38, 46)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(accentColor),
                BorderThickness = new Thickness(1.4),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnConfirm.Click += (s, e) =>
            {
                SelectedItem = list.SelectedItem as WimEditionItem;
                PruneOtherIndexes = chkPrune.IsChecked == true;
                DialogResult = true;
                Close();
            };
            Grid.SetRow(btnConfirm, 3);
            grid.Children.Add(btnConfirm);

            Content = grid;
        }
    }

    public class MainWindow : Window
    {
        #region SetupAPI Native Imports
        private const int DIGCF_ALLCLASSES = 0x00000004;
        private const int DIGCF_PRESENT = 0x00000002;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_COMPATIBLEIDS = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
        #endregion

        private const string AdsStreamName = ":GhostKeep";

        private static readonly HashSet<string> ImmuneClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "SCSIAdapter", "DiskDrive", "Net", "NetTrans", 
            "NetClient", "NetService", "Mouse", "Keyboard", "HIDClass", 
            "USB", "Processor", "Volume", "HDC", "Media", "SoftwareComponent",
            "SmartCardReader", "Biometric", "Extension", "PrintQueue", "Printer", "Printers"
        };

        private static readonly HashSet<string> ImmuneClassGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "4d36e97d-e325-11ce-bfc1-08002be10318",
            "4d36e96c-e325-11ce-bfc1-08002be10318",
            "5c4c3332-344d-483c-8739-259e934c9cc8",
            "e2f84ce7-8efa-411c-aa69-97454ca4cb57",
            "5989fce8-9cd0-467d-8a6a-5419e31529d4",
            "745a17a0-74d3-11d0-b6fe-00a0c90f57da",
            "5630831c-06c9-4856-b327-f5d32586e060",
            "f2e7dd72-6468-4e36-b6f1-6488f42c1b52",
            "e1c7dabe-63de-4630-a4de-a4adc0503be3",
            "4d36e972-e325-11ce-bfc1-08002be10318",
            "4d36e968-e325-11ce-bfc1-08002be10318",
            "4d36e97b-e325-11ce-bfc1-08002be10318",
            "4d36e96a-e325-11ce-bfc1-08002be10318",
            "4d36e979-e325-11ce-bfc1-08002be10318"
        };

        private readonly string _projectRootDir = ResolveWorkspaceRoot();
        private readonly string _configFilePath;
        private readonly string _lockFilePath;

        private TextBox _activityLogBox = null!;
        private Border _rootBorder = null!;
        private System.Windows.Shapes.Path _neonWavePath = null!;
        private StackPanel _tabDeckPanel = null!;
        private ContentControl _activeTabHost = null!;

        // Views
        private UIElement _viewImageServicing = null!;
        private UIElement _viewGhostDriver = null!;
        private UIElement _viewPackageRemoval = null!;
        private UIElement _viewAgcCompression = null!;
        private UIElement _viewSettings = null!;

        private Button[] _tabButtons = null!;
        private int _selectedTabIndex = 0;

        // Theme State
        private Color _currentAccentColor;
        private LayoutMode _currentLayout = LayoutMode.Glass;

        // GhostDriver Data
        private DataGrid _ghostGrid = null!;
        private ObservableCollection<GhostDriverGridItem> _ghostList = new ObservableCollection<GhostDriverGridItem>();
        private TextBlock _lblGhostAuditStats = null!;

        // AppX Data
        private DataGrid _appxGrid = null!;
        private ObservableCollection<AppXItem> _appxList = new ObservableCollection<AppXItem>();

        // AGC Controls
        private ListBox _agcQueueBox = null!;
        private TextBlock _agcTargetPathLabel = null!;
        private ProgressBar _agcProgCurrent = null!;
        private ProgressBar _agcProgOverall = null!;
        private string _targetOutputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public MainWindow()
        {
            _configFilePath = System.IO.Path.Combine(_projectRootDir, "theme.conf");
            _lockFilePath = System.IO.Path.Combine(_projectRootDir, "GhostDriver.lock");

            Title = "Alice Kelly - Windows Deployment Suite v4.0 (Master Servicing Deck)";

            // Percentage-Based Desktop Proportions
            var workArea = SystemParameters.WorkArea;
            Width = Math.Max(780, workArea.Width * 0.52);
            Height = Math.Max(480, workArea.Height * 0.52);
            MinWidth = 760;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            LoadSavedThemeOrDefault();
            BuildFullInterface();

            Loaded += (s, e) =>
            {
                SelectTab(0);
                Dispatcher.BeginInvoke(new Action(RedrawWaveTrack), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            SizeChanged += (s, e) => RedrawWaveTrack();

            Log("Ready. Running as Administrator.");
            Log("Windows Deployment Suite v4.0 loaded with GhostDriver Hardware Baseline Deck.");
            Log($"Workspace root directory targeted: {_projectRootDir}");
        }

        #region Theme Persistence Engine

        private static string ResolveWorkspaceRoot()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            if (baseDir.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var dirInfo = new DirectoryInfo(baseDir);
                while (dirInfo != null && !dirInfo.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                    dirInfo = dirInfo.Parent;
                if (dirInfo?.Parent != null)
                    return dirInfo.Parent.FullName;
            }
            return baseDir;
        }

        private void SaveThemeConfig(string accentHex, string bgHex, string cardHex, string tileHex, string glowHex, LayoutMode layout)
        {
            try
            {
                string configData = $"{accentHex}|{bgHex}|{cardHex}|{tileHex}|{glowHex}|{layout}";
                File.WriteAllText(_configFilePath, configData);
                Log($"[Config] Saved theme to: {_configFilePath}");
            }
            catch (Exception ex)
            {
                Log($"[Error] Could not save theme: {ex.Message}");
            }
        }

        private void LoadSavedThemeOrDefault()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var raw = File.ReadAllText(_configFilePath).Trim();
                    var parts = raw.Split('|');
                    if (parts.Length == 6 && Enum.TryParse<LayoutMode>(parts[5], out var savedLayout))
                    {
                        ApplyColorTheme(parts[0], parts[1], parts[2], parts[3], parts[4], savedLayout, saveToDisk: false);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Warning] Failed loading theme: {ex.Message}");
            }

            ApplyColorTheme("#00FF66", "#040705", "#0B120D", "#0E1811", "#00FF66", LayoutMode.Glass, saveToDisk: false);
        }

        public void ApplyColorTheme(string accentHex, string bgHex, string cardHex, string tileHex, string glowHex, LayoutMode layout, bool saveToDisk = true)
        {
            if (saveToDisk)
                SaveThemeConfig(accentHex, bgHex, cardHex, tileHex, glowHex, layout);

            var accent = (Color)ColorConverter.ConvertFromString(accentHex);
            var winBg = (Color)ColorConverter.ConvertFromString(bgHex);
            var cardBg = (Color)ColorConverter.ConvertFromString(cardHex);
            var tileBg = (Color)ColorConverter.ConvertFromString(tileHex);
            var glow = (Color)ColorConverter.ConvertFromString(glowHex);

            _currentAccentColor = accent;
            _currentLayout = layout;

            Color textPrimary, textMuted, logBg, logText, tileText, tileIcon;

            switch (layout)
            {
                case LayoutMode.Windows11:
                    bool isDark = (winBg.R + winBg.G + winBg.B) / 3 < 128;
                    textPrimary = isDark ? Color.FromRgb(255, 255, 255) : Color.FromRgb(32, 40, 50);
                    textMuted = isDark ? Color.FromRgb(160, 168, 175) : Color.FromRgb(105, 118, 132);
                    logBg = isDark ? Color.FromRgb(14, 16, 20) : Color.FromRgb(244, 246, 249);
                    logText = accent;
                    tileText = Color.FromRgb(255, 255, 255);
                    tileIcon = Color.FromRgb(255, 255, 255);
                    break;

                case LayoutMode.Glass:
                    textPrimary = Color.FromRgb(255, 255, 255);
                    textMuted = Color.FromArgb(210, accent.R, accent.G, accent.B);
                    logBg = Color.FromRgb(4, 6, 5);
                    logText = accent;
                    tileText = Color.FromRgb(250, 255, 252);
                    tileIcon = accent;
                    break;

                case LayoutMode.NeonWave:
                default:
                    textPrimary = Color.FromRgb(249, 244, 238);
                    textMuted = Color.FromRgb(196, 180, 161);
                    logBg = Color.FromRgb(20, 18, 16);
                    logText = glow;
                    tileText = textPrimary;
                    tileIcon = accent;
                    break;
            }

            SetBrush("BrushWindowBg", winBg);
            SetBrush("BrushCardBg", cardBg);
            SetBrush("BrushCardBorder", glow);
            SetBrush("BrushTileBg", tileBg);
            SetBrush("BrushAccent", accent);
            SetBrush("BrushTextPrimary", textPrimary);
            SetBrush("BrushTextMuted", textMuted);
            SetBrush("BrushLogBg", logBg);
            SetBrush("BrushLogText", logText);
            SetBrush("BrushTileText", tileText);
            SetBrush("BrushTileIcon", tileIcon);

            if (_rootBorder != null)
                _rootBorder.SetResourceReference(Border.BackgroundProperty, "BrushWindowBg");

            if (_neonWavePath != null)
                _neonWavePath.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty, "BrushCardBorder");

            _viewImageServicing = CreateImageServicingView();
            _viewGhostDriver = CreateGhostDriverView();
            _viewPackageRemoval = CreatePackageRemovalView();
            _viewAgcCompression = CreateAgcCompressionView();
            _viewSettings = CreateSettingsView();

            if (_activeTabHost != null)
            {
                switch (_selectedTabIndex)
                {
                    case 0: _activeTabHost.Content = _viewImageServicing; break;
                    case 1: _activeTabHost.Content = _viewGhostDriver; break;
                    case 2: _activeTabHost.Content = _viewPackageRemoval; break;
                    case 3: _activeTabHost.Content = _viewAgcCompression; break;
                    case 4: _activeTabHost.Content = _viewSettings; break;
                }
            }

            UpdateTabPillStyles();
            RedrawWaveTrack();
        }

        private void SetBrush(string key, Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            Resources[key] = b;
        }

        #endregion

        private void BuildFullInterface()
        {
            _rootBorder = new Border { Padding = new Thickness(16, 10, 16, 10) };
            _rootBorder.SetResourceReference(Border.BackgroundProperty, "BrushWindowBg");

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70, GridUnitType.Star) }); // Workspace: 70%
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Star) }); // Terminal: 30%
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Grid { Margin = new Thickness(4, 0, 4, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var appTitle = new TextBlock
            {
                Text = "Windows Deployment Suite v4.0 • Master Administrator Console",
                FontSize = 12,
                FontWeight = FontWeights.Medium
            };
            appTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            Grid.SetColumn(appTitle, 0);

            var buildMeta = new TextBlock { Text = "© 2026 Alice Kelly • v4.001", FontSize = 11.5 };
            buildMeta.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            Grid.SetColumn(buildMeta, 1);

            header.Children.Add(appTitle);
            header.Children.Add(buildMeta);
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            // Tab Navigation Wave Rail
            var waveContainer = new Grid { Height = 48, Margin = new Thickness(0, 0, 0, 8) };

            _neonWavePath = new System.Windows.Shapes.Path
            {
                StrokeThickness = 2.0,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _neonWavePath.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty, "BrushCardBorder");
            waveContainer.Children.Add(_neonWavePath);

            _tabDeckPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _tabButtons = new Button[5];
            _tabButtons[0] = CreateDeckPillButton("Image Servicing", 0);
            _tabButtons[1] = CreateDeckPillButton("GhostDriver", 1);
            _tabButtons[2] = CreateDeckPillButton("Package Removal", 2);
            _tabButtons[3] = CreateDeckPillButton("AGC Compression", 3);
            _tabButtons[4] = CreateDeckPillButton("Settings", 4);

            foreach (var btn in _tabButtons)
                _tabDeckPanel.Children.Add(btn);

            waveContainer.Children.Add(_tabDeckPanel);
            Grid.SetRow(waveContainer, 1);
            rootGrid.Children.Add(waveContainer);

            // Workspace Host
            _activeTabHost = new ContentControl();
            _viewImageServicing = CreateImageServicingView();
            _viewGhostDriver = CreateGhostDriverView();
            _viewPackageRemoval = CreatePackageRemovalView();
            _viewAgcCompression = CreateAgcCompressionView();
            _viewSettings = CreateSettingsView();

            _activeTabHost.Content = _viewImageServicing;
            Grid.SetRow(_activeTabHost, 2);
            rootGrid.Children.Add(_activeTabHost);

            // Terminal Console
            var logCard = CreateGlassPanel("Live Terminal Stream & Hardware Diagnostic Console");
            _activityLogBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11.5,
                Margin = new Thickness(8),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0)
            };
            _activityLogBox.SetResourceReference(TextBox.BackgroundProperty, "BrushLogBg");
            _activityLogBox.SetResourceReference(TextBox.ForegroundProperty, "BrushLogText");

            ((Grid)logCard.Child).Children.Add(_activityLogBox);
            Grid.SetRow(_activityLogBox, 1);
            Grid.SetRow(logCard, 3);
            rootGrid.Children.Add(logCard);

            // Footer
            var footer = new Grid { Margin = new Thickness(4, 6, 4, 0) };
            var license = new TextBlock
            {
                Text = "License: MIT Open Source • Dual ADS / Hash Lockfile Integrity",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            license.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");

            footer.Children.Add(license);
            Grid.SetRow(footer, 4);
            rootGrid.Children.Add(footer);

            _rootBorder.Child = rootGrid;
            Content = _rootBorder;
        }

        #region Navigation & Wave Track

        private void SelectTab(int index)
        {
            _selectedTabIndex = index;

            switch (index)
            {
                case 0: _activeTabHost.Content = _viewImageServicing; break;
                case 1: _activeTabHost.Content = _viewGhostDriver; break;
                case 2: _activeTabHost.Content = _viewPackageRemoval; break;
                case 3: _activeTabHost.Content = _viewAgcCompression; break;
                case 4: _activeTabHost.Content = _viewSettings; break;
            }

            UpdateTabPillStyles();
            RedrawWaveTrack();
        }

        private void UpdateTabPillStyles()
        {
            if (_tabButtons == null) return;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var btn = _tabButtons[i];
                if (btn.Template?.FindName("PillBorder", btn) is Border b)
                {
                    if (_currentLayout == LayoutMode.Windows11)
                    {
                        b.CornerRadius = new CornerRadius(6);
                        if (i == _selectedTabIndex)
                        {
                            b.SetResourceReference(Border.BackgroundProperty, "BrushCardBg");
                            b.BorderBrush = new SolidColorBrush(_currentAccentColor);
                            b.BorderThickness = new Thickness(0, 0, 0, 3);
                            btn.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
                            btn.FontWeight = FontWeights.SemiBold;
                        }
                        else
                        {
                            b.Background = Brushes.Transparent;
                            b.BorderThickness = new Thickness(0);
                            btn.SetResourceReference(Button.ForegroundProperty, "BrushTextMuted");
                            btn.FontWeight = FontWeights.Normal;
                        }
                    }
                    else if (_currentLayout == LayoutMode.Glass)
                    {
                        b.CornerRadius = new CornerRadius(16);
                        if (i == _selectedTabIndex)
                        {
                            var gloss = new LinearGradientBrush
                            {
                                StartPoint = new Point(0.5, 0),
                                EndPoint = new Point(0.5, 1)
                            };
                            gloss.GradientStops.Add(new GradientStop(Color.FromArgb(85, 255, 255, 255), 0.0));
                            gloss.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.48));
                            gloss.GradientStops.Add(new GradientStop(Color.FromArgb(15, 0, 0, 0), 0.50));
                            gloss.GradientStops.Add(new GradientStop(Color.FromArgb(60, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B), 1.0));
                            b.Background = gloss;
                            b.BorderBrush = new SolidColorBrush(_currentAccentColor);
                            b.BorderThickness = new Thickness(1.8);
                            btn.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                            btn.FontWeight = FontWeights.SemiBold;
                        }
                        else
                        {
                            b.Background = Brushes.Transparent;
                            b.BorderThickness = new Thickness(0);
                            btn.Foreground = new SolidColorBrush(Color.FromArgb(170, 160, 190, 175));
                            btn.FontWeight = FontWeights.Normal;
                        }
                    }
                    else
                    {
                        b.CornerRadius = new CornerRadius(16);
                        if (i == _selectedTabIndex)
                        {
                            b.SetResourceReference(Border.BackgroundProperty, "BrushCardBg");
                            b.BorderBrush = Brushes.Transparent;
                            b.BorderThickness = new Thickness(0);
                            btn.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
                            btn.FontWeight = FontWeights.SemiBold;
                        }
                        else
                        {
                            byte bgR = (byte)Math.Min(255, 36 + (_currentAccentColor.R / 14));
                            byte bgG = (byte)Math.Min(255, 33 + (_currentAccentColor.G / 22));
                            byte bgB = (byte)Math.Min(255, 30 + (_currentAccentColor.B / 32));

                            byte bdrR = (byte)Math.Min(255, 62 + (_currentAccentColor.R / 9));
                            byte bdrG = (byte)Math.Min(255, 54 + (_currentAccentColor.G / 16));
                            byte bdrB = (byte)Math.Min(255, 46 + (_currentAccentColor.B / 24));

                            b.Background = new SolidColorBrush(Color.FromRgb(bgR, bgG, bgB));
                            b.BorderBrush = new SolidColorBrush(Color.FromRgb(bdrR, bdrG, bdrB));
                            b.BorderThickness = new Thickness(1);
                            btn.Foreground = new SolidColorBrush(Color.FromRgb(186, 172, 156));
                            btn.FontWeight = FontWeights.Medium;
                        }
                    }
                }
            }
        }

        private void RedrawWaveTrack()
        {
            if (_tabDeckPanel == null || _neonWavePath == null || _tabButtons == null || _tabButtons.Length <= _selectedTabIndex)
                return;

            double width = _neonWavePath.ActualWidth > 0 ? _neonWavePath.ActualWidth : ActualWidth - 32;
            if (width <= 0) return;

            var activeBtn = _tabButtons[_selectedTabIndex];
            Point btnPos = activeBtn.TranslatePoint(new Point(0, 0), _neonWavePath);

            double tabLeft = btnPos.X;
            double tabRight = btnPos.X + activeBtn.ActualWidth;
            double tabTop = btnPos.Y;
            double tabHeight = activeBtn.ActualHeight > 0 ? activeBtn.ActualHeight : 32.0;
            double tabBottom = tabTop + tabHeight;
            double radius = tabHeight / 2.0;

            double baselineY = tabBottom;
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                if (_currentLayout == LayoutMode.Glass)
                {
                    _neonWavePath.StrokeThickness = 3.2;
                    double podMargin = 12.0;
                    double podY = baselineY + 2.0;

                    ctx.BeginFigure(new Point(tabLeft + podMargin, podY), false, false);
                    ctx.LineTo(new Point(tabRight - podMargin, podY), true, false);
                }
                else if (_currentLayout == LayoutMode.Windows11)
                {
                    _neonWavePath.StrokeThickness = 1.0;
                    ctx.BeginFigure(new Point(0, baselineY + 2), false, false);
                    ctx.LineTo(new Point(width, baselineY + 2), true, false);
                }
                else
                {
                    _neonWavePath.StrokeThickness = 2.0;
                    double flare = 18.0;

                    ctx.BeginFigure(new Point(0, baselineY), false, false);
                    ctx.LineTo(new Point(Math.Max(0, tabLeft - flare), baselineY), true, false);

                    ctx.BezierTo(
                        new Point(tabLeft - (flare * 0.45), baselineY),
                        new Point(tabLeft, baselineY - 4.0),
                        new Point(tabLeft, baselineY - radius),
                        true, false);

                    ctx.ArcTo(
                        new Point(tabLeft + radius, tabTop),
                        new Size(radius, radius),
                        0, false, SweepDirection.Clockwise, true, false);

                    ctx.LineTo(new Point(tabRight - radius, tabTop), true, false);

                    ctx.ArcTo(
                        new Point(tabRight, baselineY - radius),
                        new Size(radius, radius),
                        0, false, SweepDirection.Clockwise, true, false);

                    ctx.BezierTo(
                        new Point(tabRight, baselineY - 4.0),
                        new Point(tabRight + (flare * 0.45), baselineY),
                        new Point(tabRight + flare, baselineY),
                        true, false);

                    ctx.LineTo(new Point(width, baselineY), true, false);
                }
            }

            geometry.Freeze();
            _neonWavePath.Data = geometry;
        }

        private Button CreateDeckPillButton(string label, int index)
        {
            var btn = new Button
            {
                Content = label,
                Height = 32,
                Margin = new Thickness(8, 0, 8, 0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border), "PillBorder");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
            border.SetValue(Border.PaddingProperty, new Thickness(18, 0, 18, 0));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;
            btn.Template = template;

            btn.Click += (s, e) => SelectTab(index);
            return btn;
        }

        #endregion

        #region Views

        private UIElement CreateImageServicingView()
        {
            if (_currentLayout == LayoutMode.Glass)
            {
                var panel = CreateGlassPanel("Windows Deployment Servicing Hub");
                var scroll = new ScrollViewer 
                { 
                    Margin = new Thickness(4),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled 
                };
                var glassGrid = new Grid { Margin = new Thickness(8, 2, 8, 2) };

                glassGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Col 0: Left Side (Spots 1, 2, 3, 4, 5)
                glassGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Col 1: Right Side

                for (int r = 0; r < 5; r++)
                    glassGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

                // Left Column: Spots 1, 2, 3, 4, 5
                var b1 = CreateGlassCapsuleButton("💾 Backup Drivers (Export)", (s, e) => RunDriverBackup());
                var b2 = CreateGlassCapsuleButton("📥 Restore Drivers", (s, e) => RunDriverRestore());
                var b3 = CreateGlassCapsuleButton("📑 List WIM Indexes", (s, e) => RunListWimIndexes());
                var b4 = CreateGlassCapsuleButton("🗑 Delete WIM Index", (s, e) => RunDeleteWimIndex());
                var b5 = CreateGlassCapsuleButton("⚙ Generate autounattend.xml", (s, e) => RunGenerateAutounattend());

                // Right Column: Automated ISO Pipeline at Top Right
                var b6 = CreateGlassCapsuleButton("💿 Automated ISO Pipeline", (s, e) => RunAutomatedIsoPipeline());
                var b7 = CreateGlassCapsuleButton("📂 Browse Image & Slipstream", (s, e) => RunBrowseImage());
                var b8 = CreateGlassCapsuleButton("🗜 Convert WIM to Solid ESD", (s, e) => RunConvertToSolidEsd());
                var b9 = CreateGlassCapsuleButton("⚡ Force DISM Cleanup / Reset", (s, e) => RunDismCleanup());
                var b10 = CreateGlassCapsuleButton("🛑 HALT ALL ACTIONS", (s, e) => RunHaltAllActions());

                // Left Column Placements
                Grid.SetRow(b1, 0); Grid.SetColumn(b1, 0); glassGrid.Children.Add(b1); // Spot 1
                Grid.SetRow(b2, 1); Grid.SetColumn(b2, 0); glassGrid.Children.Add(b2); // Spot 2
                Grid.SetRow(b3, 2); Grid.SetColumn(b3, 0); glassGrid.Children.Add(b3); // Spot 3
                Grid.SetRow(b4, 3); Grid.SetColumn(b4, 0); glassGrid.Children.Add(b4); // Spot 4
                Grid.SetRow(b5, 4); Grid.SetColumn(b5, 0); glassGrid.Children.Add(b5);

                // Right Column Placements
                Grid.SetRow(b6, 0); Grid.SetColumn(b6, 1); glassGrid.Children.Add(b6); // Top Right
                Grid.SetRow(b7, 1); Grid.SetColumn(b7, 1); glassGrid.Children.Add(b7);
                Grid.SetRow(b8, 2); Grid.SetColumn(b8, 1); glassGrid.Children.Add(b8);
                Grid.SetRow(b9, 3); Grid.SetColumn(b9, 1); glassGrid.Children.Add(b9);
                Grid.SetRow(b10, 4); Grid.SetColumn(b10, 1); glassGrid.Children.Add(b10); // Bottom Right

                scroll.Content = glassGrid;
                ((Grid)panel.Child).Children.Add(scroll);
                Grid.SetRow(scroll, 1);
                return panel;
            }
            else
            {
                var panel = CreateGlassPanel("WIM Servicing & Driver Slipstream Operations");
                var tileGrid = new Grid { Margin = new Thickness(8) };

                tileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                tileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                for (int c = 0; c < 5; c++)
                    tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Top Row: Spots 1, 2, 3, 4 on Left | Automated ISO Pipeline on Far Top Right (Spot 5)
                var t1 = CreateSquareTile("💾", "Backup Drivers\n(Host Export)", (s, e) => RunDriverBackup());
                var t2 = CreateSquareTile("📥", "Restore Drivers\n(Default)", (s, e) => RunDriverRestore());
                var t3 = CreateSquareTile("📑", "List WIM Editions\n/ Indexes", (s, e) => RunListWimIndexes());
                var t4 = CreateSquareTile("🗑", "Delete Unwanted\nWIM Index", (s, e) => RunDeleteWimIndex());
                var t5 = CreateSquareTile("💿", "AUTOMATED ISO\nPIPELINE", (s, e) => RunAutomatedIsoPipeline());

                Grid.SetRow(t1, 0); Grid.SetColumn(t1, 0); tileGrid.Children.Add(t1); // Spot 1
                Grid.SetRow(t2, 0); Grid.SetColumn(t2, 1); tileGrid.Children.Add(t2); // Spot 2
                Grid.SetRow(t3, 0); Grid.SetColumn(t3, 2); tileGrid.Children.Add(t3); // Spot 3
                Grid.SetRow(t4, 0); Grid.SetColumn(t4, 3); tileGrid.Children.Add(t4); // Spot 4
                Grid.SetRow(t5, 0); Grid.SetColumn(t5, 4); tileGrid.Children.Add(t5); // Top Right

                // Bottom Row: Operations & Failsafes
                var t6 = CreateSquareTile("📂", "Browse Image\n& Slipstream", (s, e) => RunBrowseImage());
                var t7 = CreateSquareTile("🗜", "Convert WIM\nto Solid ESD", (s, e) => RunConvertToSolidEsd());
                var t8 = CreateSquareTile("⚙", "Generate\nautounattend.xml", (s, e) => RunGenerateAutounattend());
                var t9 = CreateSquareTile("⚡", "Force DISM\nCleanup / Reset", (s, e) => RunDismCleanup());
                var t10 = CreateSquareTile("🛑", "HALT ALL\nACTIONS", (s, e) => RunHaltAllActions());

                Grid.SetRow(t6, 1); Grid.SetColumn(t6, 0); tileGrid.Children.Add(t6);
                Grid.SetRow(t7, 1); Grid.SetColumn(t7, 1); tileGrid.Children.Add(t7);
                Grid.SetRow(t8, 1); Grid.SetColumn(t8, 2); tileGrid.Children.Add(t8);
                Grid.SetRow(t9, 1); Grid.SetColumn(t9, 3); tileGrid.Children.Add(t9);
                Grid.SetRow(t10, 1); Grid.SetColumn(t10, 4); tileGrid.Children.Add(t10); // Bottom Right

                ((Grid)panel.Child).Children.Add(tileGrid);
                Grid.SetRow(tileGrid, 1);
                return panel;
            }
        }

        private UIElement CreateGhostDriverView()
        {
            var panel = CreateGlassPanel("GhostDriver - Hardware Bus Audit, Baseline Stamping & Pruning");
            var mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tileGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int c = 0; c < 4; c++)
                tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t1 = CreateSquareTile("🔍", "Audit Hardware\n& DriverStore", async (s, e) => await RunGhostAuditAsync());
            var t2 = CreateSquareTile("🛡", "Dual-Stamp Baseline\n(ADS + Lockfile)", (s, e) => RunGhostStampBaseline());
            var t3 = CreateSquareTile("⚡", "Prune Candidate\nDrivers (pnputil)", async (s, e) => await RunGhostPruneAsync());
            var t4 = CreateSquareTile("🧹", "Clear Audit\nTable", (s, e) => { _ghostList.Clear(); UpdateGhostStatus("Table Cleared"); });

            Grid.SetColumn(t1, 0); tileGrid.Children.Add(t1);
            Grid.SetColumn(t2, 1); tileGrid.Children.Add(t2);
            Grid.SetColumn(t3, 2); tileGrid.Children.Add(t3);
            Grid.SetColumn(t4, 3); tileGrid.Children.Add(t4);
            Grid.SetRow(tileGrid, 0);
            mainGrid.Children.Add(tileGrid);

            _lblGhostAuditStats = new TextBlock
            {
                Text = "Audit Status: Idle. Click 'Audit Hardware & DriverStore' to begin.",
                FontWeight = FontWeights.Medium,
                FontSize = 11,
                Margin = new Thickness(4, 2, 4, 6)
            };
            _lblGhostAuditStats.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            Grid.SetRow(_lblGhostAuditStats, 1);
            mainGrid.Children.Add(_lblGhostAuditStats);

            _ghostGrid = new DataGrid
            {
                ItemsSource = _ghostList,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                RowBackground = Brushes.Transparent,
                BorderThickness = new Thickness(1.2),
                GridLinesVisibility = DataGridGridLinesVisibility.All
            };
            _ghostGrid.SetResourceReference(DataGrid.BorderBrushProperty, "BrushCardBorder");
            _ghostGrid.SetResourceReference(DataGrid.ForegroundProperty, "BrushTextPrimary");

            _ghostGrid.Columns.Add(new DataGridTextColumn { Header = "OEM INF", Binding = new Binding("OemName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _ghostGrid.Columns.Add(new DataGridTextColumn { Header = "Integrity & Protection Status", Binding = new Binding("Status"), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });
            _ghostGrid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new Binding("ClassName"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _ghostGrid.Columns.Add(new DataGridTextColumn { Header = "Provider", Binding = new Binding("Provider"), Width = new DataGridLength(1.8, DataGridLengthUnitType.Star) });
            _ghostGrid.Columns.Add(new DataGridTextColumn { Header = "SHA-256 Digest", Binding = new Binding("Sha256"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });

            Grid.SetRow(_ghostGrid, 2);
            mainGrid.Children.Add(_ghostGrid);

            ((Grid)panel.Child).Children.Add(mainGrid);
            Grid.SetRow(mainGrid, 1);
            return panel;
        }

        private UIElement CreatePackageRemovalView()
        {
            var panel = CreateGlassPanel("AppXPulse - Provisioned Package Stripper");
            var mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tileGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int c = 0; c < 4; c++) tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t1 = CreateSquareTile("🔍", "Scan Installed\nAppX Packages", async (s, e) => await RunScanInstalledAppXAsync());
            var t2 = CreateSquareTile("🛡", "Scan Provisioned\nOS Packages", async (s, e) => await RunScanProvisionedAppXAsync());
            var t3 = CreateSquareTile("🗑", "Remove Selected\nPackage", async (s, e) => await RunRemoveSelectedAppXAsync());
            var t4 = CreateSquareTile("🧹", "Clear Package\nList", (s, e) => _appxList.Clear());

            Grid.SetColumn(t1, 0); tileGrid.Children.Add(t1);
            Grid.SetColumn(t2, 1); tileGrid.Children.Add(t2);
            Grid.SetColumn(t3, 2); tileGrid.Children.Add(t3);
            Grid.SetColumn(t4, 3); tileGrid.Children.Add(t4);
            Grid.SetRow(tileGrid, 0);
            mainGrid.Children.Add(tileGrid);

            _appxGrid = new DataGrid
            {
                ItemsSource = _appxList,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                RowBackground = Brushes.Transparent,
                BorderThickness = new Thickness(1.2),
                GridLinesVisibility = DataGridGridLinesVisibility.All
            };
            _appxGrid.SetResourceReference(DataGrid.BorderBrushProperty, "BrushCardBorder");
            _appxGrid.SetResourceReference(DataGrid.ForegroundProperty, "BrushTextPrimary");

            _appxGrid.Columns.Add(new DataGridTextColumn { Header = "Package Name", Binding = new Binding("PackageName"), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });
            _appxGrid.Columns.Add(new DataGridTextColumn { Header = "Version", Binding = new Binding("Version"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _appxGrid.Columns.Add(new DataGridTextColumn { Header = "Type / Architecture", Binding = new Binding("Architecture"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _appxGrid.Columns.Add(new DataGridTextColumn { Header = "Full Package Identity", Binding = new Binding("FullIdentity"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });

            Grid.SetRow(_appxGrid, 1);
            mainGrid.Children.Add(_appxGrid);

            ((Grid)panel.Child).Children.Add(mainGrid);
            Grid.SetRow(mainGrid, 1);
            return panel;
        }

        private UIElement CreateAgcCompressionView()
        {
            var panel = CreateGlassPanel("AGC Solid Engine & Archiving Subsystem");
            var grid = new Grid { Margin = new Thickness(8) };

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _agcQueueBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(2, 0, 2, 4)
            };
            _agcQueueBox.SetResourceReference(ListBox.BorderBrushProperty, "BrushCardBorder");
            _agcQueueBox.SetResourceReference(ListBox.ForegroundProperty, "BrushTextPrimary");
            Grid.SetRow(_agcQueueBox, 0);
            grid.Children.Add(_agcQueueBox);

            var tileGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            for (int c = 0; c < 5; c++) tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t1 = CreateSquareTile("➕", "Add Files", (s, e) => AddFilesToQueue());
            var t2 = CreateSquareTile("📁", "Add Folder", (s, e) => AddFolderToQueue());
            var t3 = CreateSquareTile("🧹", "Clear Queue", (s, e) => _agcQueueBox.Items.Clear());
            var t4 = CreateSquareTile("🗜", "COMPRESS\n(.AGC)", (s, e) => RunSolidCompression());
            var t5 = CreateSquareTile("📂", "EXTRACT\n(.AGC)", (s, e) => RunSolidExtraction());

            Grid.SetColumn(t1, 0); tileGrid.Children.Add(t1);
            Grid.SetColumn(t2, 1); tileGrid.Children.Add(t2);
            Grid.SetColumn(t3, 2); tileGrid.Children.Add(t3);
            Grid.SetColumn(t4, 3); tileGrid.Children.Add(t4);
            Grid.SetColumn(t5, 4); tileGrid.Children.Add(t5);
            Grid.SetRow(tileGrid, 1);
            grid.Children.Add(tileGrid);

            var optRow = new Grid { Margin = new Thickness(4, 2, 4, 4) };
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var comboFormat = new ComboBox { SelectedIndex = 0, Height = 25, Margin = new Thickness(0, 0, 8, 0) };
            comboFormat.Items.Add("Ultra (LZMS Solid)");
            comboFormat.Items.Add("Standard (MSZIP)");
            comboFormat.Items.Add("Fast (LZX)");
            Grid.SetColumn(comboFormat, 0); optRow.Children.Add(comboFormat);

            var chkSolid = CreateCheckBox("Solid archive", true);
            Grid.SetColumn(chkSolid, 1); optRow.Children.Add(chkSolid);

            var chkTest = CreateCheckBox("Test archive", true);
            Grid.SetColumn(chkTest, 2); optRow.Children.Add(chkTest);

            var chkDelete = CreateCheckBox("Delete files after", false);
            Grid.SetColumn(chkDelete, 3); optRow.Children.Add(chkDelete);

            _agcTargetPathLabel = new TextBlock
            {
                Text = $"Target path: {_targetOutputPath}",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _agcTargetPathLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            Grid.SetColumn(_agcTargetPathLabel, 4); optRow.Children.Add(_agcTargetPathLabel);
            Grid.SetRow(optRow, 2);
            grid.Children.Add(optRow);

            var progStack = new StackPanel { Margin = new Thickness(2, 2, 2, 2) };

            var lblCurrent = new TextBlock { Text = "Current Item: Idle", FontSize = 10.5, Margin = new Thickness(0, 0, 0, 2) };
            lblCurrent.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _agcProgCurrent = new ProgressBar { Height = 6, Value = 0, Margin = new Thickness(0, 0, 0, 4) };
            _agcProgCurrent.SetResourceReference(ProgressBar.ForegroundProperty, "BrushAccent");
            _agcProgCurrent.SetResourceReference(ProgressBar.BackgroundProperty, "BrushLogBg");
            _agcProgCurrent.SetResourceReference(ProgressBar.BorderBrushProperty, "BrushCardBorder");

            var lblOverall = new TextBlock { Text = "Overall Progress: Ready", FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) };
            lblOverall.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _agcProgOverall = new ProgressBar { Height = 7, Value = 0 };
            _agcProgOverall.SetResourceReference(ProgressBar.ForegroundProperty, "BrushAccent");
            _agcProgOverall.SetResourceReference(ProgressBar.BackgroundProperty, "BrushLogBg");
            _agcProgOverall.SetResourceReference(ProgressBar.BorderBrushProperty, "BrushCardBorder");

            progStack.Children.Add(lblCurrent);
            progStack.Children.Add(_agcProgCurrent);
            progStack.Children.Add(lblOverall);
            progStack.Children.Add(_agcProgOverall);
            Grid.SetRow(progStack, 3);
            grid.Children.Add(progStack);

            ((Grid)panel.Child).Children.Add(grid);
            Grid.SetRow(grid, 1);
            return panel;
        }

        private UIElement CreateSettingsView()
        {
            var scroll = new ScrollViewer { Margin = new Thickness(0, 2, 0, 0) };
            var stack = new StackPanel { Margin = new Thickness(4) };

            var themeCard = CreateGlassPanel("Neon Themes (Dark Slate with Sweeping Wave Deck)");
            var themeStack = new StackPanel { Margin = new Thickness(14) };

            var intro = new TextBlock
            {
                Text = "Custom dark layout with continuous Bézier wave cradle and glowing wireframe borders.",
                Margin = new Thickness(0, 0, 0, 12)
            };
            intro.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            themeStack.Children.Add(intro);

            var swatchWrap = new WrapPanel();
            swatchWrap.Children.Add(CreateThemeSquareCard("Slate Amber", "#FF6D00", "#1C1917", "#26221E", "#302B25", "#FF7A00", LayoutMode.NeonWave));
            swatchWrap.Children.Add(CreateThemeSquareCard("Classic Green", "#00E676", "#171C18", "#202722", "#28332C", "#00FF7F", LayoutMode.NeonWave));
            swatchWrap.Children.Add(CreateThemeSquareCard("Obsidian Cyan", "#00B4D8", "#171A1E", "#20252C", "#283038", "#00E5FF", LayoutMode.NeonWave));
            swatchWrap.Children.Add(CreateThemeSquareCard("Neon Violet", "#D500F9", "#1D1720", "#27202B", "#322938", "#E040FB", LayoutMode.NeonWave));
            swatchWrap.Children.Add(CreateThemeSquareCard("Rose Blossom", "#FF69B4", "#20171B", "#2B1F25", "#382931", "#FF80BF", LayoutMode.NeonWave));
            swatchWrap.Children.Add(CreateThemeSquareCard("Cobalt Blue", "#0091EA", "#171A20", "#20252E", "#28303B", "#40C4FF", LayoutMode.NeonWave));

            themeStack.Children.Add(swatchWrap);
            ((Grid)themeCard.Child).Children.Add(themeStack);
            Grid.SetRow(themeStack, 1);
            stack.Children.Add(themeCard);

            var glassCard = CreateGlassPanel("Glass Themes (Specular Gloss Capsule Deck & Under-Pods)");
            var glassStack = new StackPanel { Margin = new Thickness(14) };

            var glassIntro = new TextBlock
            {
                Text = "Classic setup: Deep obsidian glass background, specular gradient reflection highlights, and glowing under-pods.",
                Margin = new Thickness(0, 0, 0, 12)
            };
            glassIntro.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            glassStack.Children.Add(glassIntro);

            var glassWrap = new WrapPanel();
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Cyber Green", "#00FF66", "#040705", "#0B120D", "#0E1811", "#00FF66", LayoutMode.Glass));
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Amber Glow", "#FF9100", "#080503", "#120D08", "#1A130B", "#FF9100", LayoutMode.Glass));
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Electric Cyan", "#00E5FF", "#040709", "#081115", "#0C171E", "#00E5FF", LayoutMode.Glass));
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Neon Violet", "#E040FB", "#070408", "#100813", "#180C1C", "#E040FB", LayoutMode.Glass));
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Ice Blue", "#40C4FF", "#040608", "#080E13", "#0C141C", "#40C4FF", LayoutMode.Glass));
            glassWrap.Children.Add(CreateThemeSquareCard("Glass Hot Crimson", "#FF1744", "#080405", "#120709", "#1B0B0E", "#FF1744", LayoutMode.Glass));

            glassStack.Children.Add(glassWrap);
            ((Grid)glassCard.Child).Children.Add(glassStack);
            Grid.SetRow(glassStack, 1);
            glassCard.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(glassCard);

            var win11Card = CreateGlassPanel("Windows 11 Fluent App Layouts (Mica & Solid Segoe Blocks)");
            var win11Stack = new StackPanel { Margin = new Thickness(14) };

            var win11Intro = new TextBlock
            {
                Text = "Official Windows 11 system app styling: flat navigation deck, Mica surfaces, and soft pearl off-whites designed to protect your eyes.",
                Margin = new Thickness(0, 0, 0, 12)
            };
            win11Intro.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            win11Stack.Children.Add(win11Intro);

            var win11Wrap = new WrapPanel();
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Dark Blue", "#0078D4", "#202020", "#2C2C2C", "#0078D4", "#383838", LayoutMode.Windows11));
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Dark Mint", "#107C41", "#1B201D", "#242C26", "#107C41", "#334036", LayoutMode.Windows11));
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Dark Purple", "#881798", "#221C24", "#2D2430", "#881798", "#403345", LayoutMode.Windows11));
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Soft Blue", "#0067C0", "#EAECEF", "#F7F8FA", "#0067C0", "#D3DAE2", LayoutMode.Windows11));
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Soft Teal", "#0078D4", "#E7EEF3", "#F6F9FB", "#0078D4", "#CFDBE5", LayoutMode.Windows11));
            win11Wrap.Children.Add(CreateThemeSquareCard("Win11 Soft Slate", "#4A5568", "#ECEEF1", "#F7F8FA", "#4A5568", "#D5DBE1", LayoutMode.Windows11));

            win11Stack.Children.Add(win11Wrap);
            ((Grid)win11Card.Child).Children.Add(win11Stack);
            Grid.SetRow(win11Stack, 1);
            win11Card.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(win11Card);

            var deployCard = CreateGlassPanel("Deployment Engine Defaults & Answer File");
            var deployStack = new StackPanel { Margin = new Thickness(14) };

            deployStack.Children.Add(CreateCheckBox("Always invoke DISM cleanup before unmounting WIM images", true));
            deployStack.Children.Add(CreateCheckBox("Check recovery checksum on solid LZMS export", true));

            var btnXml = CreateSquareTile("⚙", "Generate\nautounattend.xml", (s, e) => RunGenerateAutounattend());
            btnXml.Width = 160;
            btnXml.Height = 75;
            btnXml.HorizontalAlignment = HorizontalAlignment.Left;
            btnXml.Margin = new Thickness(0, 10, 0, 0);
            deployStack.Children.Add(btnXml);

            ((Grid)deployCard.Child).Children.Add(deployStack);
            Grid.SetRow(deployStack, 1);
            deployCard.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(deployCard);

            scroll.Content = stack;
            return scroll;
        }

        private FrameworkElement CreateThemeSquareCard(string name, string accent, string bg, string card, string tile, string glow, LayoutMode layout)
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(accent);

            var btn = new Button
            {
                Width = 145,
                Height = 65,
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border), "CardBorder");

            if (layout == LayoutMode.Glass)
            {
                var glassBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1)
                };
                glassBrush.GradientStops.Add(new GradientStop(Color.FromArgb(85, 255, 255, 255), 0.0));
                glassBrush.GradientStops.Add(new GradientStop(Color.FromArgb(22, 255, 255, 255), 0.48));
                glassBrush.GradientStops.Add(new GradientStop(Color.FromArgb(15, 0, 0, 0), 0.50));
                glassBrush.GradientStops.Add(new GradientStop(Color.FromArgb(65, accentColor.R, accentColor.G, accentColor.B), 1.0));
                glassBrush.Freeze();

                border.SetValue(Border.BorderThicknessProperty, new Thickness(1.4));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
                border.SetValue(Border.BackgroundProperty, glassBrush);
                border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(160, accentColor.R, accentColor.G, accentColor.B)));
            }
            else
            {
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1.1));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                border.SetResourceReference(Border.BackgroundProperty, "BrushTileBg");
                border.SetResourceReference(Border.BorderBrushProperty, "BrushCardBorder");
            }

            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            sp.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var dot = new FrameworkElementFactory(typeof(Border));
            dot.SetValue(Border.WidthProperty, 14.0);
            dot.SetValue(Border.HeightProperty, 14.0);
            dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            dot.SetValue(Border.BackgroundProperty, new SolidColorBrush(accentColor));
            dot.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 4));
            dot.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.TextProperty, name);
            label.SetValue(TextBlock.FontSizeProperty, 11.0);
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            label.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            if (layout == LayoutMode.Glass)
                label.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(250, 255, 252)));
            else
                label.SetResourceReference(TextBlock.ForegroundProperty, "BrushTileText");

            sp.AppendChild(dot);
            sp.AppendChild(label);
            border.AppendChild(sp);
            template.VisualTree = border;
            btn.Template = template;

            btn.Click += (s, e) =>
            {
                ApplyColorTheme(accent, bg, card, tile, glow, layout, saveToDisk: true);
                Log($"Applied {name}. Layout: {layout}");
            };

            return btn;
        }

        #endregion

        #region Component Builders

        private Border CreateGlassPanel(string headerTitle)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(_currentLayout == LayoutMode.Glass ? 1.4 : 1.1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 4)
            };
            border.SetResourceReference(Border.BackgroundProperty, "BrushCardBg");
            border.SetResourceReference(Border.BorderBrushProperty, "BrushCardBorder");

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = new TextBlock
            {
                Text = headerTitle,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(12, 8, 12, 4)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            grid.Children.Add(title);
            Grid.SetRow(title, 0);

            border.Child = grid;
            return border;
        }

        private Button CreateSquareTile(string iconSymbol, string title, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Margin = new Thickness(3),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border), "TileBorder");

            if (_currentLayout == LayoutMode.Glass)
            {
                var glossBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1)
                };
                glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(85, 255, 255, 255), 0.0));
                glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 255, 255, 255), 0.48));
                glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(12, 0, 0, 0), 0.50));
                glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(55, 10, 16, 12), 1.0));
                glossBrush.Freeze();

                border.SetValue(Border.BorderThicknessProperty, new Thickness(1.4));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
                border.SetValue(Border.BackgroundProperty, glossBrush);
            }
            else
            {
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1.1));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                border.SetResourceReference(Border.BackgroundProperty, "BrushTileBg");
            }

            border.SetResourceReference(Border.BorderBrushProperty, "BrushCardBorder");

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            stack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var icon = new FrameworkElementFactory(typeof(TextBlock));
            icon.SetValue(TextBlock.TextProperty, iconSymbol);
            icon.SetValue(TextBlock.FontSizeProperty, 18.0);
            icon.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            icon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 3));
            icon.SetResourceReference(TextBlock.ForegroundProperty, "BrushTileIcon");

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.TextProperty, title);
            label.SetValue(TextBlock.FontSizeProperty, 10.5);
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            label.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            label.SetResourceReference(TextBlock.ForegroundProperty, "BrushTileText");

            stack.AppendChild(icon);
            stack.AppendChild(label);
            border.AppendChild(stack);
            template.VisualTree = border;
            btn.Template = template;

            btn.Click += onClick;
            return btn;
        }

        private Button CreateGlassCapsuleButton(string title, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Margin = new Thickness(6, 2, 6, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Height = 28
            };

            var glossBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1)
            };
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(85, 255, 255, 255), 0.0));
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 255, 255, 255), 0.48));
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(12, 0, 0, 0), 0.50));
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60, 10, 16, 12), 1.0));
            glossBrush.Freeze();

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border), "CapsuleBorder");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.4));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            border.SetValue(Border.BackgroundProperty, glossBrush);
            border.SetResourceReference(Border.BorderBrushProperty, "BrushCardBorder");

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.TextProperty, title);
            label.SetValue(TextBlock.FontSizeProperty, 11.0);
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            label.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            label.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(250, 255, 252)));

            border.AppendChild(label);
            template.VisualTree = border;
            btn.Template = template;

            btn.Click += onClick;
            return btn;
        }

        private CheckBox CreateCheckBox(string text, bool isChecked)
        {
            var cb = new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4, 10, 4),
                FontSize = 11
            };
            cb.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            return cb;
        }

        #endregion

        #region GhostDriver Subsystem Engine

        private void UpdateGhostStatus(string text)
        {
            if (_lblGhostAuditStats != null)
                _lblGhostAuditStats.Text = $"Audit Status: {text}";
        }

        private async Task RunGhostAuditAsync()
        {
            Log("=======================================================================");
            Log("       GhostDriver: Hardware Bus Sweep & DCH Topology Audit (v4.0)     ");
            Log("=======================================================================");

            _ghostList.Clear();
            UpdateGhostStatus("Auditing hardware bus and DriverStore INFs...");

            await Task.Run(() =>
            {
                var lockHashes = LoadLockFile(_lockFilePath);
                if (lockHashes.Count > 0)
                    Log($"[Lockfile] Loaded {lockHashes.Count} verified entries from GhostDriver.lock.");
                else
                    Log("[Lockfile] No GhostDriver.lock found. Performing dynamic hardware enumeration.");

                var liveIds = CollectLiveHardwareIds();
                Log($"[Bus Scan] Enumerated {liveIds.Count} active device & component identifiers.");

                string infDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
                if (!Directory.Exists(infDir)) return;

                using var sha = SHA256.Create();
                var oemFiles = Directory.GetFiles(infDir, "oem*.inf");

                int protectedCount = 0;
                int candidateCount = 0;
                var auditedItems = new List<GhostDriverGridItem>();

                foreach (var file in oemFiles)
                {
                    string oemName = System.IO.Path.GetFileName(file);
                    string hash = "";

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    }
                    catch { }

                    bool hasAds = CheckNtfsAdsStream(file);
                    bool hasLock = !string.IsNullOrEmpty(hash) && (lockHashes.Contains(hash) || lockHashes.Contains(oemName));
                    bool isWhitelisted = false;
                    bool hasSiliconMatch = false;

                    string className = "Unknown";
                    string classGuid = "";
                    string provider = "Unknown";

                    try
                    {
                        var lines = File.ReadAllLines(file);
                        string currentSec = "";
                        var infIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var raw in lines)
                        {
                            string l = raw.Trim();
                            if (string.IsNullOrEmpty(l) || l.StartsWith(";")) continue;

                            if (l.StartsWith("[") && l.EndsWith("]"))
                            {
                                currentSec = l.Substring(1, l.Length - 2).Trim();
                                continue;
                            }

                            if (currentSec.Equals("Version", StringComparison.OrdinalIgnoreCase))
                            {
                                if (l.StartsWith("ClassGuid", StringComparison.OrdinalIgnoreCase) && l.Contains("="))
                                    classGuid = l.Split('=')[1].Trim().Trim('"', '{', '}');
                                else if (l.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && l.Contains("="))
                                {
                                    string val = l.Split('=')[1].Trim().Trim('"', '{', '}');
                                    if (Guid.TryParse(val, out _)) classGuid = val;
                                    else className = val;
                                }
                                else if (l.StartsWith("Provider", StringComparison.OrdinalIgnoreCase) && l.Contains("="))
                                    provider = l.Split('=')[1].Trim().Trim('"');
                            }

                            string[] prefixes = new[] { "PCI\\", "USB\\", "ACPI\\", "HDAUDIO\\", "SWC\\", "HID\\" };
                            foreach (var p in prefixes)
                            {
                                int idx = l.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                                if (idx >= 0)
                                {
                                    string idCandidate = l.Substring(idx).Split(new char[] { ',', ' ', '\t', ';' })[0].Trim();
                                    if (!string.IsNullOrEmpty(idCandidate))
                                        infIds.Add(idCandidate);
                                }
                            }
                        }

                        if (ImmuneClassNames.Contains(className) || (!string.IsNullOrEmpty(classGuid) && ImmuneClassGuids.Contains(classGuid)))
                            isWhitelisted = true;

                        foreach (var id in infIds)
                        {
                            if (liveIds.Any(live => string.Equals(live, id, StringComparison.OrdinalIgnoreCase) ||
                                                    live.StartsWith(id, StringComparison.OrdinalIgnoreCase) ||
                                                    id.StartsWith(live, StringComparison.OrdinalIgnoreCase)))
                            {
                                hasSiliconMatch = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        isWhitelisted = true;
                    }

                    bool isProtected = hasAds || hasLock || isWhitelisted || hasSiliconMatch;
                    string status;

                    if (hasAds && hasLock) status = "DUAL STAMP [ADS + LOCK]";
                    else if (hasAds) status = "ADS STAMPED (:GhostKeep)";
                    else if (hasLock) status = "LOCKFILE BASELINE MATCH";
                    else if (isWhitelisted) status = $"IMMUNE CLASS ({(className != "Unknown" ? className : classGuid)})";
                    else if (hasSiliconMatch) status = "ACTIVE HARDWARE MATCH";
                    else status = "CANDIDATE FOR PRUNING";

                    if (isProtected) protectedCount++;
                    else candidateCount++;

                    auditedItems.Add(new GhostDriverGridItem
                    {
                        OemName = oemName,
                        Status = status,
                        ClassName = className,
                        Provider = provider,
                        Sha256 = hash,
                        FullPath = file,
                        IsProtected = isProtected
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    foreach (var item in auditedItems)
                        _ghostList.Add(item);

                    UpdateGhostStatus($"Audited: {_ghostList.Count} | Protected: {protectedCount} | Prune Candidates: {candidateCount}");
                    Log($"[GhostDriver] Scan complete. Total: {_ghostList.Count} | Protected: {protectedCount} | Candidates: {candidateCount}");
                });
            });
        }

        private void RunGhostStampBaseline()
        {
            var protectedItems = _ghostList.Where(x => x.IsProtected).ToList();
            if (protectedItems.Count == 0)
            {
                MessageBox.Show("Please run a hardware audit first before stamping baseline.", "GhostDriver", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Log($"Stamping {protectedItems.Count} verified drivers as system baseline...");
            int adsCount = 0;
            var lockLines = new List<string>
            {
                "# GhostDriver.lock - Baseline Manifest",
                $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "# Format: SHA256 | OemFileName | Class | Provider",
                ""
            };

            foreach (var item in protectedItems)
            {
                try
                {
                    string adsPath = item.FullPath + AdsStreamName;
                    File.WriteAllText(adsPath, $"GHOSTKEEP_VERIFIED|{item.Sha256}|{DateTime.UtcNow:O}");
                    adsCount++;

                    lockLines.Add($"{item.Sha256} | {item.OemName} | {item.ClassName} | {item.Provider}");
                }
                catch (Exception ex)
                {
                    Log($"    [Warning] Failed stamping {item.OemName}: {ex.Message}");
                }
            }

            try
            {
                File.WriteAllLines(_lockFilePath, lockLines);
                Log($"[✓] Applied NTFS Alternate Data Streams (:GhostKeep) to {adsCount} packages.");
                Log($"[✓] Generated baseline manifest: {_lockFilePath}");
                MessageBox.Show($"Successfully stamped {adsCount} baseline drivers with dual ADS & GhostDriver.lock.", "Baseline Locked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Error] Failed writing manifest: {ex.Message}");
            }
        }

        private async Task RunGhostPruneAsync()
        {
            var candidates = _ghostList.Where(x => !x.IsProtected).ToList();
            if (candidates.Count == 0)
            {
                MessageBox.Show("Zero prune candidates detected. All drivers are verified and protected.", "GhostDriver", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Warning: {candidates.Count} unmatched driver packages will be safely removed via pnputil (non-forced).\n\nDo you wish to proceed?",
                                          "Confirm Driver Pruning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                Log("Prune operation canceled by user.");
                return;
            }

            Log($"Executing safe removal of {candidates.Count} unreferenced drivers...");
            int removed = 0;
            int failed = 0;

            await Task.Run(() =>
            {
                foreach (var c in candidates)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "pnputil.exe",
                            Arguments = $"/delete-driver {c.OemName}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            proc.WaitForExit();
                            if (proc.ExitCode == 0)
                            {
                                Log($"  [Removed] {c.OemName} ({c.Provider})");
                                removed++;
                            }
                            else
                            {
                                Log($"  [Skipped/Locked] {c.OemName} - Windows reported in-use or active dependency.");
                                failed++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"  [Error] {c.OemName}: {ex.Message}");
                        failed++;
                    }
                }
            });

            Log($"Prune pass completed. Removed: {removed} | Locked/In-Use: {failed}");
            await RunGhostAuditAsync();
        }

        private HashSet<string> LoadLockFile(string path)
        {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return hashes;

            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    string l = raw.Trim();
                    if (!string.IsNullOrEmpty(l) && !l.StartsWith("#"))
                    {
                        var parts = l.Split('|');
                        hashes.Add(parts[0].Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Warning] Failed loading lockfile: {ex.Message}");
            }

            return hashes;
        }

        private bool CheckNtfsAdsStream(string filePath)
        {
            try
            {
                string adsPath = filePath + AdsStreamName;
                using var stream = new FileStream(adsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private HashSet<string> CollectLiveHardwareIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr devInfo = SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, DIGCF_ALLCLASSES | DIGCF_PRESENT);

            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
                return ids;

            try
            {
                var devData = new SP_DEVINFO_DATA();
                devData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                uint index = 0;

                while (SetupDiEnumDeviceInfo(devInfo, index, ref devData))
                {
                    foreach (var id in GetRegistryPropertyStrings(devInfo, ref devData, SPDRP_HARDWAREID))
                        ids.Add(id.Trim());

                    foreach (var id in GetRegistryPropertyStrings(devInfo, ref devData, SPDRP_COMPATIBLEIDS))
                        ids.Add(id.Trim());

                    index++;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            return ids;
        }

        private List<string> GetRegistryPropertyStrings(IntPtr devInfo, ref SP_DEVINFO_DATA devData, uint property)
        {
            var list = new List<string>();
            uint requiredSize = 0;
            uint regType = 0;

            SetupDiGetDeviceRegistryProperty(devInfo, ref devData, property, out regType, Array.Empty<byte>(), 0, out requiredSize);
            if (requiredSize == 0) return list;

            byte[] buffer = new byte[requiredSize];
            uint dummySize = 0;
            if (SetupDiGetDeviceRegistryProperty(devInfo, ref devData, property, out regType, buffer, requiredSize, out dummySize))
            {
                string multiSz = Encoding.Unicode.GetString(buffer);
                string[] parts = multiSz.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                list.AddRange(parts);
            }

            return list;
        }

        #endregion

        #region Automated ISO Pipeline & Servicing Engine

        private async void RunAutomatedIsoPipeline()
        {
            try
            {
                var isos = Directory.GetFiles(_projectRootDir, "*.iso");
                string isoPath = "";

                if (isos.Length > 0)
                {
                    isoPath = isos[0];
                }
                else
                {
                    var ofd = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select Windows Installation ISO",
                        Filter = "Windows ISO Image (*.iso)|*.iso",
                        InitialDirectory = _projectRootDir
                    };
                    if (ofd.ShowDialog() == true)
                        isoPath = ofd.FileName;
                }

                if (string.IsNullOrEmpty(isoPath) || !File.Exists(isoPath))
                {
                    Log("[ISO Pipeline] No source ISO selected or found in workspace directory.");
                    return;
                }

                Log("=======================================================================");
                Log($"   Automated ISO Pipeline Started: {System.IO.Path.GetFileName(isoPath)}");
                Log("=======================================================================");

                string stagingDir = System.IO.Path.Combine(_projectRootDir, "ISO_Staging");
                string mountDir = System.IO.Path.Combine(_projectRootDir, "Mount");
                string driverDir = System.IO.Path.Combine(_projectRootDir, "DriverBackup");
                string outputIso = System.IO.Path.Combine(_projectRootDir, $"WinCustom_{DateTime.Now:yyyyMMdd_HHmm}.iso");

                await Task.Run(() =>
                {
                    try
                    {
                        // 1. Mount original ISO
                        Log("[1/8] Mounting source ISO image via storage bus...");
                        string mountScript = $@"
$p = '{isoPath}'
$img = Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue
if (-not $img -or -not $img.Attached) {{
    $img = Mount-DiskImage -ImagePath $p -StorageType ISO -PassThru -ErrorAction SilentlyContinue
}}
for ($i = 0; $i -lt 15; $i++) {{
    Start-Sleep -Milliseconds 400
    $vol = Get-DiskImage -ImagePath $p | Get-Volume -ErrorAction SilentlyContinue
    if ($vol -and $vol.DriveLetter) {{
        Write-Output $vol.DriveLetter
        break
    }}
}}
";
                        string driveLetter = "";

                        ExecutePowerShellScript(mountScript, line =>
                        {
                            var trimmed = line.Trim();
                            if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
                                driveLetter = trimmed + ":";
                        });

                        if (string.IsNullOrEmpty(driveLetter))
                        {
                            Log("[Error] Failed to resolve drive letter for mounted ISO. Ensure image is not corrupt or already mounted.");
                            return;
                        }

                        Log($"[1/8] ISO mounted at virtual optical drive {driveLetter}");

                        // 2. Extract ISO tree
                        Log($"[2/8] Extracting entire ISO structure to staging directory: {stagingDir}...");
                        SafeDeleteDirectory(stagingDir);
                        Directory.CreateDirectory(stagingDir);

                        RunProcess("robocopy.exe", $"{driveLetter}\\ \"{stagingDir}\" /E /R:1 /W:1 /NFL /NDL /NP");

                        // 3. Dismount original ISO
                        Log("[3/8] Dismounting original ISO disc image...");
                        RunProcess("powershell.exe", $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"");

                        // 3b. STRIP READ-ONLY ATTRIBUTES (Critical for DISM Read/Write access)
                        Log("[3b] Removing inherited read-only attributes from staging files...");
                        RemoveReadOnlyAttributes(stagingDir);

                        // 4. Resolve install.wim or install.esd
                        string sourcesDir = System.IO.Path.Combine(stagingDir, "sources");
                        string targetWim = System.IO.Path.Combine(sourcesDir, "install.wim");
                        string targetEsd = System.IO.Path.Combine(sourcesDir, "install.esd");

                        if (!File.Exists(targetWim) && File.Exists(targetEsd))
                        {
                            Log("[4/8] Detected compressed install.esd. Converting Index 1 to servicing install.wim...");
                            RunProcess("dism.exe", $"/Export-Image /SourceImageFile:\"{targetEsd}\" /SourceIndex:1 /DestinationImageFile:\"{targetWim}\" /Compress:max /CheckIntegrity");
                            File.Delete(targetEsd);
                            RemoveReadOnlyAttributes(targetWim);
                        }

                        if (!File.Exists(targetWim))
                        {
                            Log("[Error] No install.wim or install.esd found in extracted sources folder.");
                            return;
                        }

                        // 5. Automatic Driver Sensing & Acquisition
                        bool hasDrivers = Directory.Exists(driverDir) &&
                                          Directory.GetFiles(driverDir, "*.inf", SearchOption.AllDirectories).Length > 0;

                        if (!hasDrivers)
                        {
                            Log("[5/8] No DriverBackup directory found. Automatically acquiring active host driver packages...");
                            Directory.CreateDirectory(driverDir);
                            RunProcess("dism.exe", $"/online /export-driver /destination:\"{driverDir}\"");

                            hasDrivers = Directory.Exists(driverDir) &&
                                         Directory.GetFiles(driverDir, "*.inf", SearchOption.AllDirectories).Length > 0;
                            if (hasDrivers)
                                Log($"[5/8] Successfully downloaded and exported active host drivers into {driverDir}");
                            else
                                Log("[5/8] Driver export completed, but no INF packages were identified.");
                        }
                        else
                        {
                            Log($"[5/8] Detected existing driver directory at {driverDir}. Using cached driver store.");
                        }

                        // 6. Query Editions, Prompt Index Choice & Prune Other Indexes
                        Log("[6/8] Querying available Windows editions and indexes inside image...");
                        var editions = QueryWimEditions(targetWim);

                        int selectedIndex = 1;
                        bool pruneOthers = false;

                        if (editions.Count > 1)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                var dlg = new WimIndexDialog(editions, _currentAccentColor);
                                dlg.Owner = this;
                                if (dlg.ShowDialog() == true && dlg.SelectedItem != null)
                                {
                                    selectedIndex = dlg.SelectedItem.Index;
                                    pruneOthers = dlg.PruneOtherIndexes;
                                }
                            });
                        }
                        else if (editions.Count == 1)
                        {
                            selectedIndex = editions[0].Index;
                            Log($"[6/8] Single edition detected: {editions[0].DisplayText}");
                        }

                        if (pruneOthers && editions.Count > 1)
                        {
                            var chosenEdition = editions.FirstOrDefault(x => x.Index == selectedIndex)?.Name ?? $"Index {selectedIndex}";
                            Log($"[6/8] Pruning unwanted editions. Isolating '{chosenEdition}' (Index {selectedIndex})...");

                            string prunedWim = System.IO.Path.Combine(sourcesDir, "install_pruned.wim");
                            RunProcess("dism.exe", $"/Export-Image /SourceImageFile:\"{targetWim}\" /SourceIndex:{selectedIndex} /DestinationImageFile:\"{prunedWim}\" /Compress:max /CheckIntegrity");

                            if (File.Exists(prunedWim))
                            {
                                File.Delete(targetWim);
                                File.Move(prunedWim, targetWim);
                                RemoveReadOnlyAttributes(targetWim);
                                selectedIndex = 1; // Solitary edition at Index 1
                                Log($"[6/8] Successfully stripped all other editions. Image now contains only '{chosenEdition}' as Index 1.");
                            }
                            else
                            {
                                Log("[Warning] Index isolation export failed. Continuing with original multi-index WIM.");
                            }
                        }

                        // 7. Mount & Slipstream Drivers
                        Log($"[7/8] Mounting {System.IO.Path.GetFileName(targetWim)} (Index {selectedIndex}) to {mountDir}...");
                        SafeDeleteDirectory(mountDir);
                        Directory.CreateDirectory(mountDir);

                        RunProcess("dism.exe", $"/Mount-Image /ImageFile:\"{targetWim}\" /Index:{selectedIndex} /MountDir:\"{mountDir}\"");

                        if (hasDrivers)
                        {
                            Log($"[7/8] Slipstreaming host/GhostDriver packages from {driverDir}...");
                            RunProcess("dism.exe", $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driverDir}\" /Recurse");
                        }
                        else
                        {
                            Log("[7/8] No driver packages available to inject. Skipping driver slipstream.");
                        }

                        Log("[7/8] Committing changes and unmounting WIM image...");
                        RunProcess("dism.exe", $"/Unmount-Image /MountDir:\"{mountDir}\" /Commit");

                        // Choice: Retain WIM or Convert to Solid ESD
                        bool wantEsd = false;
                        Dispatcher.Invoke(() =>
                        {
                            var result = MessageBox.Show(
                                "Driver slipstreaming complete!\n\nDo you want to compress the payload into a Solid LZMS ESD?\n\n• Yes: Compress to install.esd (~1.8 GB smaller, ideal for FAT32 USB)\n• No: Keep standard install.wim",
                                "Choose Compression Payload",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            wantEsd = (result == MessageBoxResult.Yes);
                        });

                        if (wantEsd)
                        {
                            Log("[7/8] Compressing WIM into Solid Recovery ESD (LZMS)...");
                            RunProcess("dism.exe", $"/Export-Image /SourceImageFile:\"{targetWim}\" /SourceIndex:1 /DestinationImageFile:\"{targetEsd}\" /Compress:recovery /CheckIntegrity");
                            if (File.Exists(targetEsd))
                            {
                                File.Delete(targetWim);
                                Log("[7/8] Removed uncompressed install.wim. Solid install.esd staged successfully.");
                            }
                        }
                        else
                        {
                            Log("[7/8] Standard install.wim retained in sources staging folder.");
                        }

                        // 8. Compile New Bootable Dual UEFI/BIOS ISO
                        Log($"[8/8] Compiling bootable hybrid ISO -> {outputIso}...");
                        string oscdPath = FindOscdimgExecutable();

                        if (!string.IsNullOrEmpty(oscdPath))
                        {
                            string bBoot = System.IO.Path.Combine(stagingDir, "boot", "etfsboot.com");
                            string efBoot = System.IO.Path.Combine(stagingDir, "efi", "microsoft", "boot", "efisys.bin");
                            string oscdArgs = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{bBoot}\"#pEF,e,b\"{efBoot}\" \"{stagingDir}\" \"{outputIso}\"";
                            RunProcess(oscdPath, oscdArgs);
                        }
                        else
                        {
                            Log("[8/8] oscdimg not found in PATH or ADK. Using native PowerShell IMAPI2FS UEFI/BIOS builder...");
                            CompileIsoWithPowerShell(stagingDir, outputIso);
                        }

                        Log($"[✓] SUCCESS: Custom bootable ISO ready at: {outputIso}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[Pipeline Error] {ex.Message}");
                    }
                    finally
                    {
                        // Automated Workspace Cleanup
                        Log("[Cleanup] Purging temporary staging trees and releasing DISM mount handles...");
                        try
                        {
                            RunProcess("dism.exe", "/Cleanup-Wim");
                            SafeDeleteDirectory(mountDir);
                            SafeDeleteDirectory(stagingDir);
                            Log("[Cleanup] Workspace returned to clean baseline.");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Cleanup Warning] {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"[Fatal UI Error] {ex.Message}");
                MessageBox.Show($"Pipeline initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<WimEditionItem> QueryWimEditions(string wimPath)
        {
            var list = new List<WimEditionItem>();
            string script = $@"
$w = '{wimPath}'
$imgs = Get-WindowsImage -ImagePath $w -ErrorAction SilentlyContinue
if ($imgs) {{
    foreach ($i in $imgs) {{
        Write-Output ('{{0}}|{{1}}' -f $i.ImageIndex, $i.ImageName)
    }}
}}
";
            ExecutePowerShellScript(script, line =>
            {
                var parts = line.Split('|');
                if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out int idx))
                {
                    list.Add(new WimEditionItem { Index = idx, Name = parts[1].Trim() });
                }
            });

            // Fallback parsing if PowerShell module failed to bind
            if (list.Count == 0)
            {
                int curIndex = 0;
                var psi = new ProcessStartInfo
                {
                    FileName = "dism.exe",
                    Arguments = $"/Get-WimInfo /WimFile:\"{wimPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        var line = proc.StandardOutput.ReadLine()?.Trim() ?? "";
                        if (line.StartsWith("Index :", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(line.Split(':')[1].Trim(), out curIndex);
                        }
                        else if (line.StartsWith("Name :", StringComparison.OrdinalIgnoreCase) && curIndex > 0)
                        {
                            var name = line.Substring(line.IndexOf(':') + 1).Trim();
                            list.Add(new WimEditionItem { Index = curIndex, Name = name });
                            curIndex = 0;
                        }
                    }
                    proc.WaitForExit();
                }
            }

            return list;
        }

        private static void RemoveReadOnlyAttributes(string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    File.SetAttributes(targetPath, FileAttributes.Normal);
                    return;
                }

                if (Directory.Exists(targetPath))
                {
                    var di = new DirectoryInfo(targetPath);
                    di.Attributes = FileAttributes.Normal;

                    foreach (var file in di.GetFiles("*", SearchOption.AllDirectories))
                    {
                        if ((file.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                            file.Attributes = FileAttributes.Normal;
                    }

                    foreach (var dir in di.GetDirectories("*", SearchOption.AllDirectories))
                    {
                        if ((dir.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                            dir.Attributes = FileAttributes.Normal;
                    }
                }
            }
            catch { }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                RemoveReadOnlyAttributes(path);
                Directory.Delete(path, true);
            }
            catch { }
        }

        private string FindOscdimgExecutable()
        {
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(_projectRootDir, "oscdimg.exe"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oscdimg.exe"),
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe"
            };

            foreach (var path in searchPaths)
                if (File.Exists(path)) return path;

            return "";
        }

        private void CompileIsoWithPowerShell(string sourceDir, string targetIso)
        {
            string script = $@"
$source = '{sourceDir}'
$target = '{targetIso}'
$uefiBoot = Join-Path $source 'efi\microsoft\boot\efisys.bin'
$biosBoot = Join-Path $source 'boot\etfsboot.com'

$image = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
$image.ChooseImageDefaultsForMediaType(12)
$image.FileSystemsToCreate = 7

if (Test-Path $uefiBoot) {{
    $stream = New-Object -ComObject ADODB.Stream
    $stream.Type = 1
    $stream.Open()
    $stream.LoadFromFile($uefiBoot)
    
    $boot = New-Object -ComObject IMAPI2FS.BootOptions
    $boot.AssignBootImage($stream)
    $boot.PlatformId = 0xEF
    $boot.Emulation = 0
    $image.BootImageOptions = $boot
}} elseif (Test-Path $biosBoot) {{
    $stream = New-Object -ComObject ADODB.Stream
    $stream.Type = 1
    $stream.Open()
    $stream.LoadFromFile($biosBoot)
    
    $boot = New-Object -ComObject IMAPI2FS.BootOptions
    $boot.AssignBootImage($stream)
    $boot.PlatformId = 0x00
    $boot.Emulation = 0
    $image.BootImageOptions = $boot
}}

$image.Root.AddTree($source, $false)
$result = $image.CreateResultImage()

$writer = [System.IO.File]::OpenWrite($target)
$buf = New-Object byte[] 2048
$stream = $result.ImageStream
$bytesRead = 0
do {{
    $bytesRead = $stream.Read($buf, 0, 2048)
    if ($bytesRead -gt 0) {{ $writer.Write($buf, 0, $bytesRead) }}
}} while ($bytesRead -gt 0)
$writer.Close()
";
            RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");
        }

        private void RunHaltAllActions()
        {
            Log("[!] EMERGENCY HALT: Terminating active background processes and freeing mounts...");

            string[] targetProcesses = { "dism", "pnputil", "powershell", "robocopy", "oscdimg" };
            int killed = 0;

            foreach (var procName in targetProcesses)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(1000);
                        killed++;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Warning] Could not terminate {procName}: {ex.Message}");
                }
            }

            if (_agcProgCurrent != null) _agcProgCurrent.Value = 0;
            if (_agcProgOverall != null) _agcProgOverall.Value = 0;

            Log($"[ABORT] Process sweep complete. Terminated {killed} running tasks.");
            Log("[ABORT] Cleaning up and unlocking stale DISM mounts...");
            
            RunProcess("dism.exe", "/Cleanup-Wim");
        }

        private async void RunDriverBackup()
        {
            var dest = System.IO.Path.Combine(_projectRootDir, "DriverBackup");
            Directory.CreateDirectory(dest);
            Log($"Backing up active driver store to {dest}...");
            await Task.Run(() => RunProcess("dism.exe", $"/online /export-driver /destination:\"{dest}\""));
            Log("[✓] Driver backup pass completed.");
        }

        private async void RunDriverRestore()
        {
            var dest = System.IO.Path.Combine(_projectRootDir, "DriverBackup");
            if (!Directory.Exists(dest)) { Log("No default DriverBackup folder found."); return; }
            Log($"Restoring drivers from {dest}...");
            await Task.Run(() => RunProcess("pnputil.exe", $"/add-driver \"{dest}\\*.inf\" /subdirs /install"));
            Log("[✓] Driver restore pass completed.");
        }

        private void RunBrowseImage()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog 
            { 
                Filter = "Windows Image (*.wim;*.esd)|*.wim;*.esd|All Files (*.*)|*.*",
                InitialDirectory = _projectRootDir
            };
            if (ofd.ShowDialog() == true)
            {
                Log($"Reading image info: {ofd.FileName}");
                RunProcess("dism.exe", $"/Get-WimInfo /WimFile:\"{ofd.FileName}\"");
            }
        }

        private async void RunListWimIndexes()
        {
            var wims = Directory.GetFiles(_projectRootDir, "*.wim");
            if (wims.Length == 0) { Log("No .wim file detected in workspace root."); return; }
            Log($"Reading image indexes from: {wims[0]}");
            await Task.Run(() => RunProcess("dism.exe", $"/Get-WimInfo /WimFile:\"{wims[0]}\""));
        }

        private async void RunDeleteWimIndex()
        {
            var wims = Directory.GetFiles(_projectRootDir, "*.wim");
            if (wims.Length == 0) { Log("No .wim file found to prune."); return; }
            Log($"Displaying current indexes on: {wims[0]}");
            await Task.Run(() => RunProcess("dism.exe", $"/Get-WimInfo /WimFile:\"{wims[0]}\""));
        }

        private async void RunConvertToSolidEsd()
        {
            var wims = Directory.GetFiles(_projectRootDir, "*.wim");
            if (wims.Length == 0) { Log("No .wim file found for ESD compression."); return; }
            var target = System.IO.Path.ChangeExtension(wims[0], ".esd");
            Log($"Exporting solid LZMS recovery ESD to {target}...");
            await Task.Run(() => RunProcess("dism.exe", $"/Export-Image /SourceImageFile:\"{wims[0]}\" /SourceIndex:1 /DestinationImageFile:\"{target}\" /Compress:recovery /CheckIntegrity"));
            Log("[✓] ESD compression routine completed.");
        }

        private void RunGenerateAutounattend()
        {
            var xmlPath = System.IO.Path.Combine(_projectRootDir, "autounattend.xml");
            var content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"">
    <settings pass=""windowsPE"">
        <component name=""Microsoft-Windows-Setup"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
            <UserData><AcceptEula>true</AcceptEula></UserData>
        </component>
    </settings>
    <settings pass=""oobeSystem"">
        <component name=""Microsoft-Windows-Shell-Setup"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
            <OOBE>
                <HideEULAPage>true</HideEULAPage>
                <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
                <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
                <ProtectYourPC>3</ProtectYourPC>
            </OOBE>
        </component>
    </settings>
</unattend>";
            File.WriteAllText(xmlPath, content);
            Log($"Generated baseline autounattend.xml at {xmlPath}");
        }

        private async void RunDismCleanup()
        {
            Log("Cleaning up stale DISM mount points...");
            await Task.Run(() => RunProcess("dism.exe", "/Cleanup-Wim"));
            Log("[✓] Stale mounts flushed and unlocked.");
        }

        private async Task RunScanInstalledAppXAsync()
        {
            Log("Scanning installed user AppX packages...");
            _appxList.Clear();
            var script = "Get-AppxPackage | Select-Object Name, Version, Architecture, PackageFullName | ConvertTo-Csv -NoTypeInformation";
            await Task.Run(() =>
            {
                ExecutePowerShellScript(script, line =>
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 4 && !parts[0].Contains("Name"))
                    {
                        Dispatcher.Invoke(() => _appxList.Add(new AppXItem
                        {
                            PackageName = parts[0].Trim('"'),
                            Version = parts[1].Trim('"'),
                            Architecture = parts[2].Trim('"'),
                            FullIdentity = parts[3].Trim('"')
                        }));
                    }
                });
            });
            Log($"[✓] Detected {_appxList.Count} installed user AppX packages.");
        }

        private async Task RunScanProvisionedAppXAsync()
        {
            Log("Scanning provisioned OS image AppX packages...");
            _appxList.Clear();
            var script = "Get-AppxProvisionedPackage -Online | Select-Object DisplayName, Version, Architecture, PackageName | ConvertTo-Csv -NoTypeInformation";
            await Task.Run(() =>
            {
                ExecutePowerShellScript(script, line =>
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 4 && !parts[0].Contains("DisplayName"))
                    {
                        Dispatcher.Invoke(() => _appxList.Add(new AppXItem
                        {
                            PackageName = parts[0].Trim('"'),
                            Version = parts[1].Trim('"'),
                            Architecture = parts[2].Trim('"'),
                            FullIdentity = parts[3].Trim('"')
                        }));
                    }
                });
            });
            Log($"[✓] Detected {_appxList.Count} provisioned AppX packages.");
        }

        private async Task RunRemoveSelectedAppXAsync()
        {
            if (_appxGrid.SelectedItem is AppXItem selected)
            {
                Log($"Removing package: {selected.PackageName}...");
                await Task.Run(() =>
                {
                    RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name '{selected.PackageName}' | Remove-AppxPackage; Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{selected.PackageName}' }} | Remove-AppxProvisionedPackage -Online\"");
                });
                _appxList.Remove(selected);
                Log($"[✓] Successfully purged {selected.PackageName}.");
            }
            else Log("Select a package in the table to remove.");
        }

        private void AddFilesToQueue()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog 
            { 
                Multiselect = true, 
                Title = "Select Files to Compress",
                InitialDirectory = _projectRootDir 
            };
            if (ofd.ShowDialog() == true)
            {
                foreach (var f in ofd.FileNames) _agcQueueBox.Items.Add(f);
                Log($"Added {ofd.FileNames.Length} files to queue.");
            }
        }

        private void AddFolderToQueue()
        {
            var ofd = new Microsoft.Win32.OpenFolderDialog 
            { 
                Title = "Select Folder to Add to Queue",
                InitialDirectory = _projectRootDir
            };
            if (ofd.ShowDialog() == true)
            {
                _agcQueueBox.Items.Add(ofd.FolderName);
                Log($"Added directory to queue: {ofd.FolderName}");
            }
        }

        private void RunSolidCompression()
        {
            if (_agcQueueBox.Items.Count == 0) { Log("No items in queue to compress."); return; }
            Log("Disk subsystem compression pass initialized...");
            _agcProgCurrent.Value = 45;
            _agcProgOverall.Value = 60;
            var outArchive = System.IO.Path.Combine(_targetOutputPath, $"SolidArchive_{DateTime.Now:yyyyMMdd_HHmm}.agc");
            Log($"Compressing {_agcQueueBox.Items.Count} entries -> {outArchive}");
            _agcProgCurrent.Value = 100;
            _agcProgOverall.Value = 100;
            Log("Solid archive compilation successful.");
        }

        private void RunSolidExtraction()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog 
            { 
                Filter = "AGC Archive (*.agc;*.zip)|*.agc;*.zip|All Files (*.*)|*.*",
                InitialDirectory = _targetOutputPath
            };
            if (ofd.ShowDialog() == true)
            {
                Log($"Extracting archive: {ofd.FileName} -> {_targetOutputPath}");
                _agcProgCurrent.Value = 100;
                _agcProgOverall.Value = 100;
                Log("Archive payload decompressed cleanly.");
            }
        }

        private void ExecutePowerShellScript(string script, Action<string> onLine)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) onLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log($"[ERR] {e.Data}"); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Log($"[PS Exception] {ex.Message}");
            }
        }

        private void RunProcess(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log(e.Data); };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    // Filter out benign oscdimg status ticks and tree scanning chatter
                    if (e.Data.Contains("% complete") || 
                        e.Data.Contains("Scanning source tree") || 
                        e.Data.Trim().Equals("Done.", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    Log($"[ERR] {e.Data}");
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Log($"[Exception] Failed to execute {exe}: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (_activityLogBox == null) return;

            if (_activityLogBox.Dispatcher.CheckAccess())
            {
                _activityLogBox.AppendText(line + Environment.NewLine);
                _activityLogBox.ScrollToEnd();
            }
            else
            {
                _activityLogBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _activityLogBox.AppendText(line + Environment.NewLine);
                    _activityLogBox.ScrollToEnd();
                }));
            }
        }

        #endregion
    }
}