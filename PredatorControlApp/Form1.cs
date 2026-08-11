using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public partial class Form1 : Form
    {
        #region Win32 Interop — Single Instance

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int HWND_BROADCAST = 0xffff;
        private static readonly uint WM_SHOWME = RegisterWindowMessage("PREDATOR_CONTROL_SHOW_INSTANCE");
        private static Mutex? _appMutex;

        private static bool TryTakeSingleInstanceLock()
        {
            try
            {
                _appMutex = new Mutex(false, "PredatorControlApp_Unique_System_Mutex_999");
                return _appMutex.WaitOne(TimeSpan.Zero, false);
            }
            catch (AbandonedMutexException) { return true; }  
            catch { return true; }
        }

        #endregion

        #region Win32 Interop — Window Dragging

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        #endregion

        #region Win32 Interop — Dark Scrollbar

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int SB_VERT = 1;

        #endregion

        #region Win32 Interop — Display Control

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DM_BITSPERPEL = 0x040000;
        private const int DM_PELSWIDTH = 0x080000;
        private const int DM_PELSHEIGHT = 0x100000;
        private const int DM_DISPLAYFREQUENCY = 0x400000;
        private const int DM_INTERLACED = 0x02;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        #endregion

        #region Fields
        
        private WmiController _wmi = new();
        private System.Windows.Forms.Timer _timer = new();
        private NotifyIcon _trayIcon = new();
        private ContextMenuStrip _trayMenu = new();
        private ColorDialog _colorPicker = new() { FullOpen = true };

        private int _cpuTemp, _gpuTemp;
        private DarkScrollPanel _contentPanel = null!;

        private bool? _isPluggedIn;
        private bool? _pendingPluggedIn;
        private int _powerLineStableTicks;
        private bool _isResyncing;
        private bool _isClosing;
        private int _maxHz;
        private float _dpiScale = 1f; 
        private int _formW;           

        private static readonly Color FormBg = Color.FromArgb(22, 22, 26);
        private static readonly Color SeparatorColor = Color.FromArgb(40, 40, 44);
        private static readonly Color HeaderColor = Color.FromArgb(120, 120, 135);
        private static readonly Color SubHeaderColor = Color.FromArgb(100, 100, 110);
        private static readonly Color AccentColor = Color.FromArgb(0, 200, 160);

        private static readonly Font FontTitle = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FontSectionHeader = new("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Font FontHeaderLight = new("Segoe UI", 8.5f, FontStyle.Regular);
        private static readonly Font FontBody = new("Segoe UI", 9.5f, FontStyle.Regular);
        private static readonly Font FontBodyBold = new("Segoe UI", 9.5f, FontStyle.Bold);

        private Label _lblTitle = null!, _lblCpuTemp = null!, _lblGpuTemp = null!;
        private Label _lblCpuRpm = null!, _lblGpuRpm = null!;
        private Label _lblPowerStatus = null!, _lblFanStatus = null!;
        private Label _lblBrightHdr = null!, _lblSpeedHdr = null!;
        private Label _lblCpuFanSpeedHdr = null!, _lblGpuFanSpeedHdr = null!;

        private PredatorButton _btnQuiet = null!, _btnBalanced = null!, _btnPerform = null!,
                               _btnTurbo = null!, _btnEco = null!;

        private PredatorButton _btnAutoFan = null!, _btnMaxFan = null!, _btnCustomFan = null!;
        private PredatorButton _btnFixedSpeed = null!, _btnFanCurve = null!;
        private PredatorButton? _activeCustomSubBtn;
        private PredatorButton _btn60Hz = null!, _btnMaxHz = null!;

        private PredatorDropDown _rgbDropDown = null!;
        private PredatorButton _btnColorPick = null!;

        private PredatorSlider _brightnessSlider = null!, _speedSlider = null!;
        private PredatorSlider _cpuFanSlider = null!, _gpuFanSlider = null!;

        private FanCurveForm? _fanCurveForm;
        private bool _fanCurveEnabled;
        private List<Point> _cpuCurvePoints = new() { new(30,10), new(45,15), new(55,30), new(65,50), new(72,65), new(80,80), new(88,92), new(95,100) };
        private List<Point> _gpuCurvePoints = new() { new(30,10), new(45,15), new(55,30), new(65,50), new(72,65), new(80,80), new(88,92), new(95,100) };
        private int _lastCurveCpuSpeed = -1;
        private int _lastCurveGpuSpeed = -1;
        
        private PredatorButton? _activePowerBtn, _activeFanBtn, _activeDisplayBtn;
        private bool _isUpdatingBattery;

        private PredatorDropDown _cboAcProfile = null!, _cboBatteryProfile = null!;
        private Label _lblAcProfileHdr = null!, _lblBatteryProfileHdr = null!;
        internal static readonly byte[] AcProfileValues = { 0xFF, 0x00, 0x01, 0x04, 0x05 };
        internal static readonly byte[] BatteryProfileValues = { 0xFF, 0x00, 0x01, 0x06 };

        private PredatorSwitch _switchBatteryLimit = null!;
        private Label _lblBatteryStatus = null!;

        
        private GameSyncController _gameSync = null!;
        private PredatorToggle _switchGameSync = null!;
        private Label _lblGameSyncStatus = null!;
        private PredatorButton _btnConfigureGames = null!;
        private bool _isGameSyncOverriding;

        private PredatorToggle _switchStartWithWindows = null!;
        private bool _suppressStartupToggle;
        private Label _lblStartupStatus = null!;

        private PredatorButton _btnCheckUpdates = null!;
        private bool _updateCheckRunning;

        private static readonly string[] RgbModeNames = { "Static", "Breathing", "Neon", "Wave", "Shifting", "Zoom", "Meteor", "Twinkling" };

        private ToolStripMenuItem _trayPowerQuiet = null!, _trayPowerBal = null!, _trayPowerPerf = null!,
                                  _trayPowerTurbo = null!, _trayPowerEco = null!;
        private ToolStripMenuItem _trayFanAuto = null!, _trayFanMax = null!, _trayFanCustom = null!;
        private ToolStripMenuItem _trayDisplay60 = null!, _trayDisplayMax = null!;
        private ToolStripMenuItem _trayBatteryLimit80 = null!, _trayBatteryLimit100 = null!;
        private ToolStripMenuItem _trayBatteryMenu = null!;
        private ToolStripMenuItem _trayRgbStatic = null!, _trayRgbBreathe = null!, _trayRgbNeon = null!,
                                  _trayRgbWave = null!, _trayRgbShift = null!, _trayRgbZoom = null!,
                                  _trayRgbMeteor = null!, _trayRgbTwinkle = null!;

        #endregion

        #region DPI Scaling Helper

        private int S(int px) => (int)(px * _dpiScale);

        #endregion

        #region Constructor

        public Form1()
        {
            if (!TryTakeSingleInstanceLock())
            {
                PostMessage((IntPtr)HWND_BROADCAST, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                Environment.Exit(0);
                return;
            }

            InitializeComponent();
            this.DoubleBuffered = true;
            _maxHz = GetMaxRefreshRate();

            _dpiScale = this.DeviceDpi / 96f;

            BuildUI();
            BuildTrayMenu();
            SetupSystemTray();

            if (GetCurrentRefreshRate() <= 60)
            {
                HighlightBtn(_btn60Hz, ref _activeDisplayBtn);
                CheckTrayItem(_trayDisplay60, _trayDisplay60, _trayDisplayMax);
            }
            else
            {
                HighlightBtn(_btnMaxHz, ref _activeDisplayBtn);
                CheckTrayItem(_trayDisplayMax, _trayDisplay60, _trayDisplayMax);
            }

            LoadMemory();

            _gameSync = new GameSyncController();
            _gameSync.GameDetected += OnGameDetected;
            _gameSync.GameExited += OnGameExited;

            if (_gameSync.IsEnabled)
            {
                _switchGameSync.Checked = true;
                _lblGameSyncStatus.Text = "Active \u2014 Monitoring";
            }

            _timer.Interval = 2000;
            _timer.Tick += UpdateTelemetry;
            _timer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            this.FormClosed += (s, e) => SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            this.Shown += (s, e) =>
            {
                Updater.ShowPendingNotes(this);

                if (Environment.CommandLine.Contains("-hidden")) HideApp();
            };
        }

        #region Updates

        private async Task CheckForUpdatesAsync()
        {
            if (_updateCheckRunning) return;
            _updateCheckRunning = true;
            _btnCheckUpdates.Enabled = false;

            try
            {
                var info = await Updater.CheckAsync();

                if (info == null)
                {
                    MessageBox.Show(this, $"You're on the latest version (v{Updater.CurrentText}).",
                        "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await PromptUpdateAsync(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not check for updates:\n{ex.Message}",
                    "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _updateCheckRunning = false;
                if (!_btnCheckUpdates.IsDisposed) _btnCheckUpdates.Enabled = true;
            }
        }

        private async Task PromptUpdateAsync(UpdateInfo info)
        {
            bool accepted = Updater.ShowNotes(this, "Update available",
                $"Version {info.Version.ToString(3)} is available — you have v{Updater.CurrentText}",
                info.Notes, confirm: true);

            if (!accepted) return;

            try
            {
                _btnCheckUpdates.Enabled = false;
                _btnCheckUpdates.Text = "Downloading update…";
                await Updater.ApplyAsync(info);

                _isClosing = true;
                Application.Exit();
            }
            catch (Exception ex)
            {
                _btnCheckUpdates.Enabled = true;
                _btnCheckUpdates.Text = $"⬇  Check for Updates  (v{Updater.CurrentText})";
                MessageBox.Show(this, $"Update failed:\n{ex.Message}",
                    "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SHOWME) ShowApp();
            base.WndProc(ref m);
        }

        #endregion

        #region Display Control

        private int GetCurrentRefreshRate()
        {
            DEVMODE dm = new(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) ? dm.dmDisplayFrequency : 60;
        }

        private int GetMaxRefreshRate()
        {
            DEVMODE cur = new(); cur.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur)) return 60;

            int maxHz = cur.dmDisplayFrequency > 0 ? cur.dmDisplayFrequency : 60;
            DEVMODE dm = new(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            for (int modeNum = 0; EnumDisplaySettings(null, modeNum, ref dm); modeNum++)
            {
                if (IsSameGeometry(dm, cur) && dm.dmDisplayFrequency > maxHz)
                    maxHz = dm.dmDisplayFrequency;
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            }
            return maxHz;
        }

        private static bool IsSameGeometry(DEVMODE a, DEVMODE b) =>
            a.dmPelsWidth == b.dmPelsWidth &&
            a.dmPelsHeight == b.dmPelsHeight &&
            a.dmBitsPerPel == b.dmBitsPerPel &&
            (a.dmDisplayFlags & DM_INTERLACED) == 0;

        private bool SetRefreshRate(int hz)
        {
            if (hz <= 0) return false;

            DEVMODE cur = new(); cur.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur)) return false;
            if (cur.dmDisplayFrequency == hz) return true;

            DEVMODE dm = new(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            DEVMODE? match = null;
            for (int modeNum = 0; EnumDisplaySettings(null, modeNum, ref dm); modeNum++)
            {
                if (IsSameGeometry(dm, cur) && dm.dmDisplayFrequency == hz) { match = dm; break; }
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            }
            if (match == null) return false;

            DEVMODE target = match.Value;
            target.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            target.dmFields = DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

            if (ChangeDisplaySettings(ref target, CDS_TEST) != DISP_CHANGE_SUCCESSFUL) return false;
            return ChangeDisplaySettings(ref target, CDS_UPDATEREGISTRY) == DISP_CHANGE_SUCCESSFUL;
        }

        #endregion

        #region System Tray

        private void SetupSystemTray()
        {
            try { _trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { _trayIcon.Icon = SystemIcons.Application; }

            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Text = "Predator Control";
            try { _trayIcon.Visible = true; } catch { }
            _trayIcon.DoubleClick += (s, e) => ShowApp();
        }

        private void BuildTrayMenu()
        {
            _trayMenu = new ContextMenuStrip();

            var powerMenu = new ToolStripMenuItem("  Power Mode"); 
            _trayPowerQuiet = new ToolStripMenuItem("Quiet", null, (s, e) => ApplyPowerMode(0x00, _btnQuiet));
            _trayPowerBal = new ToolStripMenuItem("Balanced", null, (s, e) => ApplyPowerMode(0x01, _btnBalanced));
            _trayPowerPerf = new ToolStripMenuItem("Performance", null, (s, e) => ApplyPowerMode(0x04, _btnPerform));
            _trayPowerTurbo = new ToolStripMenuItem("Turbo", null, (s, e) => ApplyPowerMode(0x05, _btnTurbo));
            _trayPowerEco = new ToolStripMenuItem("Eco", null, (s, e) => ApplyPowerMode(0x06, _btnEco));
            powerMenu.DropDownItems.AddRange([_trayPowerQuiet, _trayPowerBal, _trayPowerPerf, _trayPowerTurbo, _trayPowerEco]);

            var fanMenu = new ToolStripMenuItem("  Fan Mode");
            _trayFanAuto = new ToolStripMenuItem("Auto", null, (s, e) => ApplyFanMode(0x01, _btnAutoFan));
            _trayFanMax = new ToolStripMenuItem("Max", null, (s, e) => ApplyFanMode(0x02, _btnMaxFan));
            _trayFanCustom = new ToolStripMenuItem("Custom", null, (s, e) => ApplyFanMode(0x03, _btnCustomFan));
            fanMenu.DropDownItems.AddRange([_trayFanAuto, _trayFanMax, _trayFanCustom]);

            var displayMenu = new ToolStripMenuItem("  Display");
            _trayDisplay60 = new ToolStripMenuItem("60 Hz", null, (s, e) => ApplyDisplayMode(60, _btn60Hz));
            _trayDisplayMax = new ToolStripMenuItem($"{_maxHz} Hz", null, (s, e) => ApplyDisplayMode(_maxHz, _btnMaxHz));
            displayMenu.DropDownItems.AddRange([_trayDisplay60, _trayDisplayMax]);

            var rgbMenu = new ToolStripMenuItem("  Keyboard RGB");
            _trayRgbStatic = new ToolStripMenuItem("Static", null, (s, e) => ApplyRgbModeFromDropdown(0));
            _trayRgbBreathe = new ToolStripMenuItem("Breathing", null, (s, e) => ApplyRgbModeFromDropdown(1));
            _trayRgbNeon = new ToolStripMenuItem("Neon", null, (s, e) => ApplyRgbModeFromDropdown(2));
            _trayRgbWave = new ToolStripMenuItem("Wave", null, (s, e) => ApplyRgbModeFromDropdown(3));
            _trayRgbShift = new ToolStripMenuItem("Shifting", null, (s, e) => ApplyRgbModeFromDropdown(4));
            _trayRgbZoom = new ToolStripMenuItem("Zoom", null, (s, e) => ApplyRgbModeFromDropdown(5));
            _trayRgbMeteor = new ToolStripMenuItem("Meteor", null, (s, e) => ApplyRgbModeFromDropdown(6));
            _trayRgbTwinkle = new ToolStripMenuItem("Twinkling", null, (s, e) => ApplyRgbModeFromDropdown(7));
            rgbMenu.DropDownItems.AddRange([_trayRgbStatic, _trayRgbBreathe, _trayRgbNeon, _trayRgbWave,
                                            _trayRgbShift, _trayRgbZoom, _trayRgbMeteor, _trayRgbTwinkle]);

            _trayBatteryMenu = new ToolStripMenuItem("  Battery Limit");
            _trayBatteryLimit80 = new ToolStripMenuItem("Limit to 80%", null, (s, e) => ApplyBatteryLimit(true));
            _trayBatteryLimit100 = new ToolStripMenuItem("Full Charge (100%)", null, (s, e) => ApplyBatteryLimit(false));
            _trayBatteryMenu.DropDownItems.AddRange([_trayBatteryLimit80, _trayBatteryLimit100]);

            _trayMenu.Items.Add(powerMenu);
            _trayMenu.Items.Add(fanMenu);
            _trayMenu.Items.Add(displayMenu);
            _trayMenu.Items.Add(_trayBatteryMenu);
            _trayMenu.Items.Add(rgbMenu);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Open Dashboard", null, (s, e) => ShowApp());
            _trayMenu.Items.Add("Exit", null, (s, e) => { _isClosing = true; Application.Exit(); });
        }

        #endregion

        #region UI Building

        private void BuildUI()
        {
            this.Controls.Clear();
            this.BackColor = FormBg;
            this.ForeColor = Color.White;

            _formW = S(450);
            int workH = Screen.PrimaryScreen?.WorkingArea.Height ?? S(1000);
            this.ClientSize = new Size(_formW, Math.Max(S(400), Math.Min(S(960), workH - 40)));
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int pad = S(24);
            int contentW = _formW - pad * 2;
            int gap = S(6);
            int btnH = S(34);
            int y = 0;

            var pnlTitle = new Panel { Height = S(40), Width = _formW, BackColor = Color.FromArgb(18, 18, 21) };
            pnlTitle.MouseDown += TitleBar_MouseDown;
            this.Controls.Add(pnlTitle);
            var picIcon = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(S(16), S(16)), Location = new Point(pad - S(4), S(12)), BackColor = Color.Transparent };
            try { var extIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); if (extIcon != null) picIcon.Image = extIcon.ToBitmap(); } catch { }
            picIcon.MouseDown += TitleBar_MouseDown;
            pnlTitle.Controls.Add(picIcon);

            _lblTitle = new Label { Text = "Predator Control", ForeColor = Color.White, Font = FontTitle, AutoSize = true, Location = new Point(pad + S(20), S(11)), BackColor = Color.Transparent };
            _lblTitle.MouseDown += TitleBar_MouseDown;
            pnlTitle.Controls.Add(_lblTitle);

            var lblClose = new Label { Text = "●", ForeColor = Color.FromArgb(255, 95, 86), Font = new Font("Arial", 12f), AutoSize = true, Location = new Point(_formW - pad - S(4), S(9)), Cursor = Cursors.Hand, BackColor = Color.Transparent };
            var lblMin = new Label { Text = "●", ForeColor = Color.FromArgb(255, 189, 46), Font = new Font("Arial", 12f), AutoSize = true, Location = new Point(lblClose.Left - S(20), S(9)), Cursor = Cursors.Hand, BackColor = Color.Transparent };
            
            lblClose.Click += (s, e) => { this.Close(); };
            lblMin.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            
            pnlTitle.Controls.Add(lblClose);
            pnlTitle.Controls.Add(lblMin);

            y = pnlTitle.Bottom;

            _contentPanel = new DarkScrollPanel
            {
                Location = new Point(0, y),
                Size = new Size(_formW + DarkScrollPanel.NativeBarWidth, this.ClientSize.Height - y),
                BackColor = FormBg
            };
            _contentPanel.SetDpiScale(_dpiScale);
            this.Controls.Add(_contentPanel);
            _contentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;

            y = S(24); 

            MakeLabel("CPU:", pad, y, FontBody, SubHeaderColor);
            _lblCpuTemp = MakeLabel("43°C", pad + S(34), y, FontBodyBold, Color.White);

            MakeLabel("GPU:", _formW / 2 + S(10), y, FontBody, SubHeaderColor);
            _lblGpuTemp = MakeLabel("39°C", _formW / 2 + S(46), y, FontBodyBold, Color.White);

            y += S(24);
            MakeLabel("CPU FAN:", pad, y, FontBody, SubHeaderColor);
            _lblCpuRpm = MakeLabel("-- RPM", pad + S(64), y, FontBodyBold, Color.White);

            MakeLabel("GPU FAN:", _formW / 2 + S(10), y, FontBody, SubHeaderColor);
            _lblGpuRpm = MakeLabel("-- RPM", _formW / 2 + S(74), y, FontBodyBold, Color.White);

            y += S(24);
            MakeLabel("Fan speed:", pad, y, FontBody, SubHeaderColor);
            _lblFanStatus = MakeLabel("Auto", pad + S(74), y, FontBodyBold, Color.White);

            MakeLabel("Power:", _formW / 2 + S(10), y, FontBody, SubHeaderColor);
            _lblPowerStatus = MakeLabel("Plugged In", _formW / 2 + S(56), y, FontBodyBold, Color.White);

            y += S(30);
            AddSeparator(y);

            y += S(20);
            MakeSectionHeader("POWER MODE", pad, y);
            
            y += S(24);
            int btnW = (contentW - 4 * gap) / 5;
            _btnQuiet = MakeButton("Quiet", pad, y, btnW, btnH);
            _btnBalanced = MakeButton("Balanced", pad + (btnW + gap), y, btnW, btnH);
            _btnPerform = MakeButton("Perf", pad + (btnW + gap) * 2, y, btnW, btnH);
            _btnTurbo = MakeButton("Turbo", pad + (btnW + gap) * 3, y, btnW, btnH);
            _btnEco = MakeButton("Eco", pad + (btnW + gap) * 4, y, btnW, btnH);
            
            _btnQuiet.Click += (s, e) => ApplyPowerMode(0x00, _btnQuiet);
            _btnBalanced.Click += (s, e) => ApplyPowerMode(0x01, _btnBalanced);
            _btnPerform.Click += (s, e) => ApplyPowerMode(0x04, _btnPerform);
            _btnTurbo.Click += (s, e) => ApplyPowerMode(0x05, _btnTurbo);
            _btnEco.Click += (s, e) => ApplyPowerMode(0x06, _btnEco);

            y += btnH + S(14);
            int profileDropW = (contentW - gap) / 2;
            _lblAcProfileHdr = MakeLabel("ON AC POWER:", pad, y, FontSectionHeader, SubHeaderColor);
            _lblBatteryProfileHdr = MakeLabel("ON BATTERY:", pad + profileDropW + gap, y, FontSectionHeader, SubHeaderColor);

            y += S(20);
            _cboAcProfile = new PredatorDropDown { Location = new Point(pad, y), Size = new Size(profileDropW, S(30)) };
            _cboAcProfile.Items.AddRange(new[] { "Don't Change", "Quiet", "Balanced", "Perf", "Turbo" });
            _cboAcProfile.SelectedIndex = 0;
            _contentPanel.Controls.Add(_cboAcProfile);

            _cboBatteryProfile = new PredatorDropDown { Location = new Point(pad + profileDropW + gap, y), Size = new Size(profileDropW, S(30)) };
            _cboBatteryProfile.Items.AddRange(new[] { "Don't Change", "Quiet", "Balanced", "Eco" });
            _cboBatteryProfile.SelectedIndex = 0;
            _contentPanel.Controls.Add(_cboBatteryProfile);

            _cboAcProfile.SelectedIndexChanged += (s, e) =>
            {
                SaveState("AutoPowerAC", _cboAcProfile.SelectedIndex);
                if (_isPluggedIn == true) ApplyPowerRules(true);
            };
            _cboBatteryProfile.SelectedIndexChanged += (s, e) =>
            {
                SaveState("AutoPowerBattery", _cboBatteryProfile.SelectedIndex);
                if (_isPluggedIn == false) ApplyPowerRules(false);
            };

            y += S(30) + S(20);
            MakeSectionHeader("FAN CONTROL", pad, y);
            
            y += S(24);
            int fanBtnW = (contentW - 2 * gap) / 3;
            _btnAutoFan = MakeButton("Auto", pad, y, fanBtnW, btnH);
            _btnMaxFan = MakeButton("Max", pad + (fanBtnW + gap), y, fanBtnW, btnH);
            _btnCustomFan = MakeButton("Custom", pad + (fanBtnW + gap) * 2, y, fanBtnW, btnH);

            _btnAutoFan.Click += (s, e) => ApplyFanMode(0x01, _btnAutoFan);
            _btnMaxFan.Click += (s, e) => ApplyFanMode(0x02, _btnMaxFan);
            _btnCustomFan.Click += (s, e) => ApplyFanMode(0x03, _btnCustomFan);

            y += btnH + S(12);
            int fanSliderW = (contentW - gap) / 2;
            _lblCpuFanSpeedHdr = MakeLabel("CPU FAN: 50%", pad, y, FontSectionHeader, SubHeaderColor);
            _lblGpuFanSpeedHdr = MakeLabel("GPU FAN: 50%", pad + fanSliderW + gap, y, FontSectionHeader, SubHeaderColor);
            _lblCpuFanSpeedHdr.Visible = false;
            _lblGpuFanSpeedHdr.Visible = false;

            y += S(24);
            _cpuFanSlider = new PredatorSlider
            {
                Location = new Point(pad, y),
                Size     = new Size(fanSliderW, S(28)),
                Minimum  = 10, Maximum = 100, Value = 50,
                Visible  = false
            };
            _gpuFanSlider = new PredatorSlider
            {
                Location = new Point(pad + fanSliderW + gap, y),
                Size     = new Size(fanSliderW, S(28)),
                Minimum  = 10, Maximum = 100, Value = 50,
                Visible  = false
            };
            _contentPanel.Controls.Add(_cpuFanSlider);
            _contentPanel.Controls.Add(_gpuFanSlider);

            _cpuFanSlider.ValueChanged   += (s, e) => _lblCpuFanSpeedHdr.Text = $"CPU FAN: {_cpuFanSlider.Value}%";
            _gpuFanSlider.ValueChanged   += (s, e) => _lblGpuFanSpeedHdr.Text = $"GPU FAN: {_gpuFanSlider.Value}%";

            _cpuFanSlider.ValueCommitted += (s, e) =>
            {
                _wmi.SetCpuFanSpeed((byte)_cpuFanSlider.Value);
                SaveState("FanSpeedCpu", _cpuFanSlider.Value);
            };
            _gpuFanSlider.ValueCommitted += (s, e) =>
            {
                _wmi.SetGpuFanSpeed((byte)_gpuFanSlider.Value);
                SaveState("FanSpeedGpu", _gpuFanSlider.Value);
            };

            y += S(28) + S(8);
            int subBtnW = (contentW - gap) / 2;
            _btnFixedSpeed = MakeButton("Fixed Speed", pad, y, subBtnW, btnH);
            _btnFanCurve = MakeButton("Curve", pad + subBtnW + gap, y, subBtnW, btnH);
            _btnFixedSpeed.Visible = false;
            _btnFanCurve.Visible = false;

            _btnFixedSpeed.Click += (s, e) =>
            {
                _fanCurveEnabled = false;
                _lastCurveCpuSpeed = -1;
                _lastCurveGpuSpeed = -1;
                HighlightBtn(_btnFixedSpeed, ref _activeCustomSubBtn);
                SaveState("FanCurveEnabled", 0);

                if (_fanCurveForm != null && !_fanCurveForm.IsDisposed)
                    _fanCurveForm.Close();

                _cpuFanSlider.Enabled = true;
                _gpuFanSlider.Enabled = true;

                _wmi.SetCpuFanSpeed((byte)_cpuFanSlider.Value);
                _wmi.SetGpuFanSpeed((byte)_gpuFanSlider.Value);
            };

            _btnFanCurve.Click += (s, e) =>
            {
                HighlightBtn(_btnFanCurve, ref _activeCustomSubBtn);

                _cpuFanSlider.Enabled = false;
                _gpuFanSlider.Enabled = false;

                OpenFanCurveEditor();
            };

            y += btnH + S(20);
            MakeSectionHeader("DISPLAY REFRESH RATE", pad, y);
            
            y += S(24);
            int dispBtnW = (contentW - gap) / 2;
            _btn60Hz = MakeButton("60 Hz", pad, y, dispBtnW, btnH);
            _btnMaxHz = MakeButton($"{_maxHz} Hz (Max)", pad + dispBtnW + gap, y, dispBtnW, btnH);

            _btn60Hz.Click += (s, e) => ApplyDisplayMode(60, _btn60Hz);
            _btnMaxHz.Click += (s, e) => ApplyDisplayMode(_maxHz, _btnMaxHz);

            y += btnH + S(28);
            MakeSectionHeader("BATTERY CHARGE LIMIT", pad, y);

            y += S(24);
            int switchH = S(30);
            _lblBatteryStatus = MakeLabel("Full Charge (100%)", pad, y, FontBody, SubHeaderColor);
            CenterV(_lblBatteryStatus, y, switchH);

            _switchBatteryLimit = new PredatorSwitch
            {
                Location = new Point(_formW - pad - S(48), y),
                Size = new Size(S(48), switchH)
            };
            _contentPanel.Controls.Add(_switchBatteryLimit);

            _switchBatteryLimit.CheckedChanged += (s, e) =>
            {
                ApplyBatteryLimit(_switchBatteryLimit.Checked);
            };

            y += switchH + S(12);
            _lblStartupStatus = MakeLabel("Start with Windows", pad, y, FontBody, SubHeaderColor);
            CenterV(_lblStartupStatus, y, switchH);

            MigrateLegacyStartup();

            _switchStartWithWindows = new PredatorToggle
            {
                Location = new Point(_formW - pad - S(48), y),
                Size = new Size(S(48), switchH),
                Checked = IsStartupEnabled()
            };
            _contentPanel.Controls.Add(_switchStartWithWindows);

            _switchStartWithWindows.CheckedChanged += (s, e) =>
            {
                if (_suppressStartupToggle) return;

                bool wanted = _switchStartWithWindows.Checked;
                if (SetStartupEnabled(wanted) && IsStartupEnabled() == wanted) return;

                _suppressStartupToggle = true;
                _switchStartWithWindows.Checked = !wanted;
                _suppressStartupToggle = false;

                MessageBox.Show(this,
                    wanted
                        ? "Could not register Predator Control to start with Windows.\r\n\r\nThe scheduled task could not be created. Try running the app as administrator."
                        : "Could not remove the Predator Control startup task.",
                    "Start with Windows", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            y += switchH + S(28);
            MakeSectionHeader("KEYBOARD RGB MODE", pad, y);
            
            y += S(24);
            int dropH = S(34);
            _rgbDropDown = new PredatorDropDown { Location = new Point(pad, y), Size = new Size(contentW, dropH) };
            foreach (var name in RgbModeNames) _rgbDropDown.Items.Add(name);
            _rgbDropDown.SelectedIndex = 3; 
            _contentPanel.Controls.Add(_rgbDropDown);

            y += dropH + S(28);
            _lblBrightHdr = MakeLabel("BRIGHTNESS: 100%", pad, y, FontSectionHeader, SubHeaderColor);
            _lblSpeedHdr = MakeLabel("EFFECT SPEED: 50%", _formW / 2 + S(10), y, FontSectionHeader, SubHeaderColor);
            
            y += S(24);
            int sliderW = (contentW - gap * 4) / 2;
            _brightnessSlider = new PredatorSlider { Location = new Point(pad, y), Size = new Size(sliderW, S(28)), Minimum = 0, Maximum = 100, Value = 100 };
            _contentPanel.Controls.Add(_brightnessSlider);
            
            _speedSlider = new PredatorSlider { Location = new Point(_formW / 2 + S(10), y), Size = new Size(sliderW, S(28)), Minimum = 1, Maximum = 100, Value = 50 };
            _contentPanel.Controls.Add(_speedSlider);

            _brightnessSlider.ValueChanged += (s, e) => { _lblBrightHdr.Text = $"BRIGHTNESS: {_brightnessSlider.Value}%"; };
            _brightnessSlider.ValueCommitted += (s, e) => { _wmi.SetBrightness((byte)_brightnessSlider.Value); SaveState("Brightness", _brightnessSlider.Value); };

            _speedSlider.ValueChanged += (s, e) => { _lblSpeedHdr.Text = $"EFFECT SPEED: {_speedSlider.Value}%"; };
            _speedSlider.ValueCommitted += (s, e) => { _wmi.SetSpeed(GetMappedSpeed()); SaveState("RGB_Speed", _speedSlider.Value); };

            _rgbDropDown.SelectedIndexChanged += (s, e) =>
            {
                if (_isGameSyncOverriding) return; 
                int mode = _rgbDropDown.SelectedIndex;
                if (mode == 0)
                {
                    Color c = _colorPicker.Color;
                    _wmi.SetRgbMode(0, c.R, c.G, c.B, (byte)_brightnessSlider.Value, GetMappedSpeed(), 0);
                    SaveState("RGB_Mode", 0);
                    SaveState("RGB_R", c.R); SaveState("RGB_G", c.G); SaveState("RGB_B", c.B);
                }
                else ApplyRgbModeFromDropdown(mode);
                UpdateRgbControls(mode);
                CheckRgbTrayFromMode(mode);
            };

            y += S(44);
            MakeSectionHeader("COLOR CUSTOMIZATION", pad, y);
            
            y += S(24);
            _btnColorPick = MakeButton("    Choose Custom Color", pad, y, contentW, btnH);
            _btnColorPick.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int cy = _btnColorPick.Height / 2;
                int cx = _btnColorPick.Width / 2 - S(70);
                using var brush = new SolidBrush(Color.FromArgb(0, 200, 160));
                g.FillEllipse(brush, cx - S(6), cy - S(6), S(12), S(12));
                using var glowBrush = new SolidBrush(Color.FromArgb(100, 0, 200, 160));
                g.FillEllipse(glowBrush, cx - S(8), cy - S(8), S(16), S(16));
            };

            _btnColorPick.Click += (s, e) =>
            {
                if (_colorPicker.ShowDialog() == DialogResult.OK)
                {
                    Color c = _colorPicker.Color;
                    _wmi.SetRgbMode(0, c.R, c.G, c.B, (byte)_brightnessSlider.Value, GetMappedSpeed(), 0);
                    _rgbDropDown.SelectedIndex = 0;
                    SaveState("RGB_Mode", 0);
                    SaveState("RGB_R", c.R); SaveState("RGB_G", c.G); SaveState("RGB_B", c.B);
                    UpdateRgbControls(0);
                    CheckRgbTrayFromMode(0);
                }
            };

            y += btnH + S(28);
            AddSeparator(y);
            y += S(20);
            MakeSectionHeader("GAME SYNC", pad, y);

            y += S(24);
            int syncSwitchH = S(30);
            _lblGameSyncStatus = MakeLabel("Disabled", pad, y, FontBody, SubHeaderColor);
            CenterV(_lblGameSyncStatus, y, syncSwitchH);

            _switchGameSync = new PredatorToggle
            {
                Location = new Point(_formW - pad - S(48), y),
                Size = new Size(S(48), syncSwitchH)
            };
            _contentPanel.Controls.Add(_switchGameSync);

            _switchGameSync.CheckedChanged += (s, e) =>
            {
                _gameSync.IsEnabled = _switchGameSync.Checked;
                _lblGameSyncStatus.Text = _switchGameSync.Checked ? "Active — Monitoring" : "Disabled";
            };

            y += syncSwitchH + S(10);
            _btnConfigureGames = MakeButton("🎮  Configure Executables", pad, y, contentW, btnH);
            _btnConfigureGames.Click += (s, e) =>
            {
                using var form = new GameSyncForm(_gameSync, _maxHz);
                form.ShowDialog(this);
            };

            y += btnH + S(24);
            AddSeparator(y);

            y += S(20);
            MakeSectionHeader("UPDATES", pad, y);

            y += S(24);
            int updBtnH = S(30), updBtnW = S(160);
            var lblVersion = MakeLabel($"Version {Updater.CurrentText}", pad, y, FontBody, SubHeaderColor);
            CenterV(lblVersion, y, updBtnH);

            _btnCheckUpdates = MakeButton("⬇  Check for Updates", _formW - pad - updBtnW, y, updBtnW, updBtnH);
            _btnCheckUpdates.Click += async (s, e) => await CheckForUpdatesAsync();

            _contentPanel.AutoScrollMinSize = new Size(0, y + updBtnH + S(50));
        }

        private void UpdateRgbControls(int mode)
        {
            bool hasSpeed = mode != 0;
            _speedSlider.Enabled = hasSpeed;
            _btnColorPick.Enabled = mode == 0;
        }

        private void MakeSectionHeader(string label, int x, int y)
        {
            MakeLabel(label, x, y, FontSectionHeader, HeaderColor);
        }

        private Label MakeLabel(string text, int x, int y, Font font, Color color)
        {
            var lbl = new Label
            {
                Text = text, Location = new Point(x, y), AutoSize = true, Font = font, ForeColor = color, BackColor = Color.Transparent
            };
            _contentPanel.Controls.Add(lbl);
            return lbl;
        }

        private PredatorButton MakeButton(string text, int x, int y, int width, int height)
        {
            var btn = new PredatorButton { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
            _contentPanel.Controls.Add(btn);
            return btn;
        }

        private void AddSeparator(int y)
        {
            int pad = S(24);
            _contentPanel.Controls.Add(new Panel { Location = new Point(pad, y), Size = new Size(_formW - pad * 2, 1), BackColor = SeparatorColor });
        }

        private void CenterV(Label lbl, int controlY, int controlH)
        {
            lbl.Location = new Point(lbl.Left, controlY + (controlH - lbl.Height) / 2);
        }

        #endregion

        #region Action Handlers

        private void ApplyPowerMode(byte mode, PredatorButton btn)
        {
            _wmi.SetPowerMode(mode);
            HighlightBtn(btn, ref _activePowerBtn);
            SaveState("Power", mode);

            _lblPowerStatus.Text = mode switch
            {
                0x00 => "Silent",
                0x04 => "Performance",
                0x05 => "Turbo",
                0x06 => "Eco",
                _ => "Balanced"
            };

            var trayItem = mode switch
            {
                0x00 => _trayPowerQuiet,
                0x04 => _trayPowerPerf,
                0x05 => _trayPowerTurbo,
                0x06 => _trayPowerEco,
                _ => _trayPowerBal
            };
            CheckTrayItem(trayItem, _trayPowerQuiet, _trayPowerBal, _trayPowerPerf, _trayPowerTurbo, _trayPowerEco);
        }

        private void ApplyFanMode(byte mode, PredatorButton btn)
        {
            _wmi.SetFanBehavior(mode);
            HighlightBtn(btn, ref _activeFanBtn);
            SaveState("Fan", mode);

            _lblFanStatus.Text = mode switch
            {
                0x02 => "Max",
                0x03 => "Custom",
                _ => "Auto"
            };

            bool isCustom = mode == 0x03;
            _lblCpuFanSpeedHdr.Visible = isCustom;
            _lblGpuFanSpeedHdr.Visible = isCustom;
            _cpuFanSlider.Visible = isCustom;
            _gpuFanSlider.Visible = isCustom;
            _btnFixedSpeed.Visible = isCustom;
            _btnFanCurve.Visible = isCustom;

            if (isCustom)
            {
                if (_fanCurveEnabled)
                {
                    HighlightBtn(_btnFanCurve, ref _activeCustomSubBtn);
                    _cpuFanSlider.Enabled = false;
                    _gpuFanSlider.Enabled = false;
                }
                else
                {
                    HighlightBtn(_btnFixedSpeed, ref _activeCustomSubBtn);
                    _cpuFanSlider.Enabled = true;
                    _gpuFanSlider.Enabled = true;
                }
            }
            else
            {
                _lastCurveCpuSpeed = -1;
                _lastCurveGpuSpeed = -1;
                if (_activeCustomSubBtn != null)
                {
                    _activeCustomSubBtn.IsActive = false;
                    _activeCustomSubBtn = null;
                }
            }

            var trayItem = mode switch
            {
                0x01 => _trayFanAuto,
                0x02 => _trayFanMax,
                _ => _trayFanCustom
            };
            CheckTrayItem(trayItem, _trayFanAuto, _trayFanMax, _trayFanCustom);
        }

        private void OpenFanCurveEditor()
        {
            if (_fanCurveForm != null && !_fanCurveForm.IsDisposed)
            {
                _fanCurveForm.Activate();
                return;
            }

            _fanCurveForm = new FanCurveForm();
            _fanCurveForm.SetCpuCurve(_cpuCurvePoints);
            _fanCurveForm.SetGpuCurve(_gpuCurvePoints);
            _fanCurveForm.UpdateTemps(_cpuTemp, _gpuTemp);

            _fanCurveForm.ApplyClicked += (s, args) =>
            {
                _cpuCurvePoints = args.CpuPoints;
                _gpuCurvePoints = args.GpuPoints;

                int cpuSpeed = _fanCurveForm!.InterpolateCpuSpeed(_cpuTemp);
                int gpuSpeed = _fanCurveForm!.InterpolateGpuSpeed(_gpuTemp);

                bool cpuOk = _wmi.SetCpuFanSpeed((byte)cpuSpeed);
                bool gpuOk = _wmi.SetGpuFanSpeed((byte)gpuSpeed);

                args.Success = cpuOk && gpuOk;

                if (args.Success)
                {
                    _fanCurveEnabled = true;
                    _lastCurveCpuSpeed = cpuSpeed;
                    _lastCurveGpuSpeed = gpuSpeed;

                    _cpuFanSlider.Value = Math.Clamp(cpuSpeed, 10, 100);
                    _gpuFanSlider.Value = Math.Clamp(gpuSpeed, 10, 100);
                    _lblCpuFanSpeedHdr.Text = $"CPU FAN: {cpuSpeed}%";
                    _lblGpuFanSpeedHdr.Text = $"GPU FAN: {gpuSpeed}%";

                    SaveCurveToRegistry("CpuCurve", _cpuCurvePoints);
                    SaveCurveToRegistry("GpuCurve", _gpuCurvePoints);
                    SaveState("FanCurveEnabled", 1);
                }
            };

            _fanCurveForm.FormClosed += (s, e) => _fanCurveForm = null;
            _fanCurveForm.Show(this);
        }

        private void SaveCurveToRegistry(string name, List<Point> points)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                string data = string.Join(";", points.Select(p => $"{p.X},{p.Y}"));
                key.SetValue(name, data);
            }
            catch { }
        }

        private List<Point>? LoadCurveFromRegistry(string name)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                string? data = key.GetValue(name) as string;
                if (string.IsNullOrEmpty(data)) return null;

                var pts = new List<Point>();
                foreach (var pair in data.Split(';'))
                {
                    var parts = pair.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        pts.Add(new Point(x, y));
                }
                return pts.Count >= 2 ? FanCurveGraph.Normalize(pts) : null;
            }
            catch { return null; }
        }

        private void ApplyDisplayMode(int hz, PredatorButton btn)
        {
            if (!SetRefreshRate(hz)) return;   
            HighlightBtn(btn, ref _activeDisplayBtn);
            CheckTrayItem(hz <= 60 ? _trayDisplay60 : _trayDisplayMax, _trayDisplay60, _trayDisplayMax);
        }

        private void ApplyBatteryLimit(bool limit)
        {
            if (_isUpdatingBattery) return;
            _isUpdatingBattery = true;

            try
            {
                if (_wmi.SetBatteryChargeLimit(limit))
                {
                    if (_switchBatteryLimit.Checked != limit)
                        _switchBatteryLimit.Checked = limit;

                    _lblBatteryStatus.Text = limit ? "Limit to 80% (Health)" : "Full Charge (100%)";
                    CheckTrayItem(limit ? _trayBatteryLimit80 : _trayBatteryLimit100, _trayBatteryLimit80, _trayBatteryLimit100);
                    SaveState("BatteryLimit", limit ? 1 : 0);
                }
                else
                {
                    _switchBatteryLimit.Checked = !limit;
                }
            }
            finally
            {
                _isUpdatingBattery = false;
            }
        }

        private void ApplyRgbModeFromDropdown(int mode)
        {
            byte bright = (byte)_brightnessSlider.Value;
            byte speed = GetMappedSpeed();

            _wmi.SetRgbMode(mode, _wmi.LastR, _wmi.LastG, _wmi.LastB, bright, speed, 0);

            if (_rgbDropDown.SelectedIndex != mode)
                _rgbDropDown.SelectedIndex = mode;

            SaveState("RGB_Mode", mode);
            UpdateRgbControls(mode);
            CheckRgbTrayFromMode(mode);
        }

        private void CheckRgbTrayFromMode(int mode)
        {
            var active = mode switch
            {
                0 => _trayRgbStatic,
                1 => _trayRgbBreathe,
                2 => _trayRgbNeon,
                3 => _trayRgbWave,
                4 => _trayRgbShift,
                5 => _trayRgbZoom,
                6 => _trayRgbMeteor,
                _ => _trayRgbTwinkle
            };
            CheckTrayItem(active, _trayRgbStatic, _trayRgbBreathe, _trayRgbNeon, _trayRgbWave,
                          _trayRgbShift, _trayRgbZoom, _trayRgbMeteor, _trayRgbTwinkle);
        }

        #endregion

        #region Game Sync Handlers

        private DashboardSnapshot CaptureCurrentState()
        {
            return new DashboardSnapshot
            {
                PowerMode = GetCurrentPowerByte(),
                FanMode = GetCurrentFanByte(),
                CpuFanSpeed = _cpuFanSlider.Value,
                GpuFanSpeed = _gpuFanSlider.Value,
                FanCurveWasEnabled = _fanCurveEnabled,
                RefreshRate = GetCurrentRefreshRate(),
                BatteryLimit = _switchBatteryLimit.Checked ? 1 : 0,
                RgbMode = _rgbDropDown.SelectedIndex,
                RgbBrightness = _brightnessSlider.Value,
                RgbSpeed = _speedSlider.Value,
                RgbR = _wmi.LastR,
                RgbG = _wmi.LastG,
                RgbB = _wmi.LastB,
            };
        }

        private byte GetCurrentPowerByte()
        {
            if (_activePowerBtn == _btnQuiet) return 0x00;
            if (_activePowerBtn == _btnPerform) return 0x04;
            if (_activePowerBtn == _btnTurbo) return 0x05;
            if (_activePowerBtn == _btnEco) return 0x06;
            return 0x01; 
        }

        private byte GetCurrentFanByte()
        {
            if (_activeFanBtn == _btnMaxFan) return 0x02;
            if (_activeFanBtn == _btnCustomFan) return 0x03;
            return 0x01; 
        }

        private PredatorButton PowerByteToBtn(byte mode) => mode switch
        {
            0x00 => _btnQuiet,
            0x04 => _btnPerform,
            0x05 => _btnTurbo,
            0x06 => _btnEco,
            _ => _btnBalanced
        };

        private PredatorButton FanByteToBtn(byte mode) => mode switch
        {
            0x02 => _btnMaxFan,
            0x03 => _btnCustomFan,
            _ => _btnAutoFan
        };

        private async void OnGameDetected(GameProfile profile)
        {
            if (InvokeRequired) { Invoke(() => OnGameDetected(profile)); return; }

            _isGameSyncOverriding = true;
            try { await ApplyGameProfile(profile); }
            finally { _isGameSyncOverriding = false; }
        }

        private async Task ApplyGameProfile(GameProfile profile)
        {
            _lblGameSyncStatus.Text = $"Active \u2014 {profile.DisplayName}";

            _gameSync.SetPreGameSnapshot(CaptureCurrentState());

            ApplyPowerMode(profile.PowerMode, PowerByteToBtn(profile.PowerMode));
            ApplyFanMode(profile.FanMode, FanByteToBtn(profile.FanMode));

            if (profile.FanMode == 0x03)
            {
                _fanCurveEnabled = false;
                HighlightBtn(_btnFixedSpeed, ref _activeCustomSubBtn);
                _cpuFanSlider.Enabled = true;
                _gpuFanSlider.Enabled = true;

                if (profile.CpuFanSpeed >= 10)
                {
                    int cpuSpeed = Math.Clamp(profile.CpuFanSpeed, 10, 100);
                    _wmi.SetCpuFanSpeed((byte)cpuSpeed);
                    _cpuFanSlider.Value = cpuSpeed;
                    _lblCpuFanSpeedHdr.Text = $"CPU FAN: {cpuSpeed}%";
                }
                if (profile.GpuFanSpeed >= 10)
                {
                    int gpuSpeed = Math.Clamp(profile.GpuFanSpeed, 10, 100);
                    _wmi.SetGpuFanSpeed((byte)gpuSpeed);
                    _gpuFanSlider.Value = gpuSpeed;
                    _lblGpuFanSpeedHdr.Text = $"GPU FAN: {gpuSpeed}%";
                }
            }

            if (profile.RefreshRate > 0)
                ApplyDisplayMode(profile.RefreshRate, profile.RefreshRate <= 60 ? _btn60Hz : _btnMaxHz);

            if (profile.BatteryLimit >= 0)
                ApplyBatteryLimit(profile.BatteryLimit == 1);

            await Task.Delay(500);
            if (IsDisposed) return;

            if (profile.RgbMode >= 0)
            {
                int mode = Math.Clamp(profile.RgbMode, 0, 7);
                int brightVal = profile.RgbBrightness >= 0 ? Math.Clamp(profile.RgbBrightness, 0, 100) : _brightnessSlider.Value;
                int speedVal = profile.RgbSpeed >= 0 ? Math.Clamp(profile.RgbSpeed, 1, 100) : _speedSlider.Value;
                byte bright = (byte)brightVal;
                byte speed = profile.RgbSpeed >= 0 ? (byte)Math.Clamp(Math.Round(speedVal * 9.0 / 100.0), 1, 9) : GetMappedSpeed();
                byte r = profile.RgbR >= 0 ? (byte)Math.Clamp(profile.RgbR, 0, 255) : _wmi.LastR;
                byte g = profile.RgbG >= 0 ? (byte)Math.Clamp(profile.RgbG, 0, 255) : _wmi.LastG;
                byte b = profile.RgbB >= 0 ? (byte)Math.Clamp(profile.RgbB, 0, 255) : _wmi.LastB;

                _wmi.SetRgbMode(mode, r, g, b, bright, speed, 0);
                if (_rgbDropDown.SelectedIndex != mode)
                    _rgbDropDown.SelectedIndex = mode;
                _brightnessSlider.Value = brightVal;
                _speedSlider.Value = speedVal;
                UpdateRgbControls(mode);
                CheckRgbTrayFromMode(mode);
            }
        }

        private async void OnGameExited(DashboardSnapshot snap)
        {
            if (InvokeRequired) { Invoke(() => OnGameExited(snap)); return; }

            _lblGameSyncStatus.Text = "Active \u2014 Monitoring";

            _isGameSyncOverriding = true;
            try { await RestoreSnapshot(snap); }
            finally { _isGameSyncOverriding = false; }
        }

        private async Task RestoreSnapshot(DashboardSnapshot snap)
        {
            ApplyPowerMode(snap.PowerMode, PowerByteToBtn(snap.PowerMode));
            ApplyFanMode(snap.FanMode, FanByteToBtn(snap.FanMode));

            if (snap.FanMode == 0x03)
            {
                if (snap.FanCurveWasEnabled)
                {
                    _fanCurveEnabled = true;
                    HighlightBtn(_btnFanCurve, ref _activeCustomSubBtn);
                    _cpuFanSlider.Enabled = false;
                    _gpuFanSlider.Enabled = false;
                }
                else
                {
                    _fanCurveEnabled = false;
                    HighlightBtn(_btnFixedSpeed, ref _activeCustomSubBtn);
                    _cpuFanSlider.Enabled = true;
                    _gpuFanSlider.Enabled = true;
                    int cpuSpeed = Math.Clamp(snap.CpuFanSpeed, 10, 100);
                    int gpuSpeed = Math.Clamp(snap.GpuFanSpeed, 10, 100);
                    _wmi.SetCpuFanSpeed((byte)cpuSpeed);
                    _wmi.SetGpuFanSpeed((byte)gpuSpeed);
                    _cpuFanSlider.Value = cpuSpeed;
                    _gpuFanSlider.Value = gpuSpeed;
                    _lblCpuFanSpeedHdr.Text = $"CPU FAN: {cpuSpeed}%";
                    _lblGpuFanSpeedHdr.Text = $"GPU FAN: {gpuSpeed}%";
                }
            }

            if (snap.RefreshRate > 0)
                ApplyDisplayMode(snap.RefreshRate, snap.RefreshRate <= 60 ? _btn60Hz : _btnMaxHz);

            ApplyBatteryLimit(snap.BatteryLimit == 1);

            await Task.Delay(500);
            if (IsDisposed) return;

            int rgbMode = Math.Clamp(snap.RgbMode, 0, 7);
            int bright = Math.Clamp(snap.RgbBrightness, 0, 100);
            int speed = Math.Clamp(snap.RgbSpeed, 1, 100);

            _wmi.SetRgbMode(rgbMode,
                            (byte)Math.Clamp(snap.RgbR, 0, 255),
                            (byte)Math.Clamp(snap.RgbG, 0, 255),
                            (byte)Math.Clamp(snap.RgbB, 0, 255),
                            (byte)bright,
                            (byte)Math.Clamp(Math.Round(speed * 9.0 / 100.0), 1, 9), 0);
            if (_rgbDropDown.SelectedIndex != rgbMode)
                _rgbDropDown.SelectedIndex = rgbMode;
            _brightnessSlider.Value = bright;
            _speedSlider.Value = speed;
            UpdateRgbControls(rgbMode);
            CheckRgbTrayFromMode(rgbMode);
        }

        #endregion

        #region State Persistence

        private void SaveState(string name, int value)
        {
            if (_isGameSyncOverriding) return; 
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                key.SetValue(name, value);
            }
            catch { }
        }

        private static int GetInt(RegistryKey key, string name, int fallback, int min, int max)
        {
            int v = fallback;
            try
            {
                object? raw = key.GetValue(name);
                if (raw is int i) v = i;
                else if (raw != null && int.TryParse(raw.ToString(), out int p)) v = p;
            }
            catch { }
            return Math.Clamp(v, min, max);
        }

        private void LoadMemory()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                int savedPower = GetInt(key, "Power", 0x01, 0x00, 0xFF);
                int savedFan = GetInt(key, "Fan", 0x01, 0x00, 0xFF);
                int savedRgbMode = GetInt(key, "RGB_Mode", 3, 0, 7);
                int savedBrightness = GetInt(key, "Brightness", 100, 0, 100);
                int savedSpeed = GetInt(key, "RGB_Speed", 50, 1, 100);

                int savedR = GetInt(key, "RGB_R", 0, 0, 255);
                int savedG = GetInt(key, "RGB_G", 150, 0, 255);
                int savedB = GetInt(key, "RGB_B", 255, 0, 255);
                _colorPicker.Color = Color.FromArgb(savedR, savedG, savedB);

                _brightnessSlider.Value = Math.Clamp(savedBrightness, 0, 100);
                if (_lblBrightHdr != null) _lblBrightHdr.Text = $"BRIGHTNESS: {_brightnessSlider.Value}%";
                _speedSlider.Value = Math.Clamp(savedSpeed, 1, 100);
                if (_lblSpeedHdr != null) _lblSpeedHdr.Text = $"EFFECT SPEED: {_speedSlider.Value}%";

                var (powerMode, powerBtn) = savedPower switch
                {
                    0x00 => ((byte)0x00, _btnQuiet),
                    0x04 => ((byte)0x04, _btnPerform),
                    0x05 => ((byte)0x05, _btnTurbo),
                    0x06 => ((byte)0x06, _btnEco),
                    _ => ((byte)0x01, _btnBalanced)
                };
                ApplyPowerMode(powerMode, powerBtn);

                _cboAcProfile.SelectedIndex = GetInt(key, "AutoPowerAC", 0, 0, _cboAcProfile.Items.Count - 1);
                _cboBatteryProfile.SelectedIndex = GetInt(key, "AutoPowerBattery", 0, 0, _cboBatteryProfile.Items.Count - 1);

                int savedFanSpeedCpu = GetInt(key, "FanSpeedCpu", 50, 10, 100);
                int savedFanSpeedGpu = GetInt(key, "FanSpeedGpu", 50, 10, 100);

                var (fanMode, fanBtn) = savedFan switch
                {
                    0x02 => ((byte)0x02, _btnMaxFan),
                    0x03 => ((byte)0x03, _btnCustomFan),
                    _ => ((byte)0x01, _btnAutoFan)
                };

                var loadedCpu = LoadCurveFromRegistry("CpuCurve");
                var loadedGpu = LoadCurveFromRegistry("GpuCurve");
                if (loadedCpu != null) _cpuCurvePoints = loadedCpu;
                if (loadedGpu != null) _gpuCurvePoints = loadedGpu;

                int savedCurveEnabled = GetInt(key, "FanCurveEnabled", 0, 0, 1);
                if (savedCurveEnabled == 1 && fanMode == 0x03)
                    _fanCurveEnabled = true;

                ApplyFanMode(fanMode, fanBtn);

                if (fanMode == 0x03)
                {
                    if (_fanCurveEnabled)
                    {
                        int cpuSpeed = InterpolateCurve(_cpuCurvePoints, _cpuTemp);
                        int gpuSpeed = InterpolateCurve(_gpuCurvePoints, _gpuTemp);
                        _wmi.SetCpuFanSpeed((byte)cpuSpeed);
                        _wmi.SetGpuFanSpeed((byte)gpuSpeed);
                        _lastCurveCpuSpeed = cpuSpeed;
                        _lastCurveGpuSpeed = gpuSpeed;
                        _cpuFanSlider.Value = Math.Clamp(cpuSpeed, 10, 100);
                        _gpuFanSlider.Value = Math.Clamp(gpuSpeed, 10, 100);
                        _lblCpuFanSpeedHdr.Text = $"CPU FAN: {cpuSpeed}%";
                        _lblGpuFanSpeedHdr.Text = $"GPU FAN: {gpuSpeed}%";
                    }
                    else
                    {
                        _cpuFanSlider.Value = savedFanSpeedCpu;
                        _gpuFanSlider.Value = savedFanSpeedGpu;
                        _lblCpuFanSpeedHdr.Text = $"CPU FAN: {savedFanSpeedCpu}%";
                        _lblGpuFanSpeedHdr.Text = $"GPU FAN: {savedFanSpeedGpu}%";
                        _wmi.SetFanSpeed((byte)savedFanSpeedCpu, (byte)savedFanSpeedGpu);
                    }
                }

                int clampedMode = Math.Clamp(savedRgbMode, 0, 7);
                if (clampedMode == 0)
                {
                    _wmi.SetStaticColor((byte)savedR, (byte)savedG, (byte)savedB, (byte)savedBrightness);
                    _rgbDropDown.SelectedIndex = 0;
                    UpdateRgbControls(0);
                    CheckRgbTrayFromMode(0);
                }
                else
                {
                    ApplyRgbModeFromDropdown(clampedMode);
                }

                if (_wmi.IsBatteryControlSupported())
                {
                    bool limitEnabled = GetInt(key, "BatteryLimit", 0, 0, 1) == 1;

                    _wmi.SetBatteryChargeLimit(limitEnabled);

                    _isUpdatingBattery = true;
                    _switchBatteryLimit.Checked = limitEnabled;
                    _lblBatteryStatus.Text = limitEnabled ? "Limit to 80% (Health)" : "Full Charge (100%)";
                    CheckTrayItem(limitEnabled ? _trayBatteryLimit80 : _trayBatteryLimit100, _trayBatteryLimit80, _trayBatteryLimit100);
                    _isUpdatingBattery = false;
                }
                else
                {
                    _isUpdatingBattery = true;
                    _switchBatteryLimit.Checked = false;
                    _switchBatteryLimit.Enabled = false;
                    _lblBatteryStatus.Text = "Not Supported";
                    _lblBatteryStatus.ForeColor = SubHeaderColor;
                    _trayBatteryLimit80.Enabled = false;
                    _trayBatteryLimit100.Enabled = false;
                    _trayBatteryMenu.Enabled = false;
                    _isUpdatingBattery = false;
                }
            }
            catch { }
        }

        #endregion

        #region Telemetry & Power Rules

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            if (_isClosing || IsDisposed || !IsHandleCreated) return;

            try { BeginInvoke(new Action(async () => await ResyncAfterResume())); }
            catch { }
        }

        private async Task ResyncAfterResume()
        {
            if (_isResyncing) return;
            _isResyncing = true;

            try
            {
                _isPluggedIn = null;
                _lastCurveCpuSpeed = -1;
                _lastCurveGpuSpeed = -1;

                var snap = CaptureCurrentState();
                snap.RefreshRate = _activeDisplayBtn == _btn60Hz ? 60 : _maxHz;

                await Task.Delay(3000);
                if (_isClosing || IsDisposed) return;

                await RestoreSnapshot(snap);
            }
            catch (Exception ex) { Program.Report(ex, false); }
            finally
            {
                _pendingPluggedIn = null;
                _powerLineStableTicks = 0;
                _isResyncing = false;
            }
        }

        internal static bool? DebouncePowerLine(PowerLineStatus line, bool? current, ref bool? pending, ref int ticks)
        {
            if (line == PowerLineStatus.Unknown)
            {
                pending = null;
                ticks = 0;
                return current;
            }

            bool pluggedIn = line == PowerLineStatus.Online;

            if (pending != pluggedIn)
            {
                pending = pluggedIn;
                ticks = 1;
            }
            else if (ticks < 2)
            {
                ticks++;
            }

            return ticks >= 2 ? pluggedIn : current;
        }

        private void UpdateTelemetry(object? sender, EventArgs e)
        {
            try { UpdateTelemetryCore(); }
            catch (Exception ex) { Program.Report(ex, false); }
        }

        private void UpdateTelemetryCore()
        {
            bool? confirmed = DebouncePowerLine(SystemInformation.PowerStatus.PowerLineStatus,
                                                _isPluggedIn, ref _pendingPluggedIn, ref _powerLineStableTicks);

            if (confirmed != _isPluggedIn && !_isResyncing)
            {
                try { ApplyPowerRules(confirmed == true); }
                catch (Exception ex) { Program.Report(ex, false); }
                finally { _isPluggedIn = confirmed; }
            }

            _cpuTemp = _wmi.CpuTemp;

            int cpuRpm = _wmi.CpuFanRpm;
            int gpuRpm;

            if (_isPluggedIn == true)
            {
                _gpuTemp = _wmi.GpuTemp;
                gpuRpm = _wmi.GpuFanRpm;
            }
            else
            {
                _gpuTemp = 0;
                gpuRpm = 0;
            }

            _lblCpuTemp.Text = _cpuTemp > 0 ? $"{_cpuTemp}°C" : "--°C";
            _lblGpuTemp.Text = _gpuTemp > 0 ? $"{_gpuTemp}°C" : "--°C";
            _lblCpuTemp.ForeColor = TempColor(_cpuTemp);
            _lblGpuTemp.ForeColor = TempColor(_gpuTemp);

            _lblCpuRpm.Text = cpuRpm > 0 ? $"{cpuRpm} RPM" : "-- RPM";
            _lblGpuRpm.Text = gpuRpm > 0 ? $"{gpuRpm} RPM" : "-- RPM";

            _trayIcon.Text = $"Predator Control\nCPU: {(_cpuTemp > 0 ? $"{_cpuTemp}°C" : "N/A")}  GPU: {(_gpuTemp > 0 ? $"{_gpuTemp}°C" : "N/A")}";

            if (_fanCurveForm != null && !_fanCurveForm.IsDisposed)
                _fanCurveForm.UpdateTemps(_cpuTemp, _gpuTemp);

            ApplyFanCurve();
        }

        private void ApplyFanCurve()
        {
            if (!_fanCurveEnabled) return;
            if (GetCurrentFanByte() != 0x03) return;  
            if (_cpuTemp <= 0 && _gpuTemp <= 0) return; 

            int cpuSpeed = InterpolateCurve(_cpuCurvePoints, _cpuTemp);
            int gpuSpeed = InterpolateCurve(_gpuCurvePoints, _gpuTemp);

            if (cpuSpeed != _lastCurveCpuSpeed)
            {
                _wmi.SetCpuFanSpeed((byte)cpuSpeed);
                _lastCurveCpuSpeed = cpuSpeed;

                _cpuFanSlider.Value = Math.Clamp(cpuSpeed, 10, 100);
                _lblCpuFanSpeedHdr.Text = $"CPU FAN: {cpuSpeed}%";
            }

            if (gpuSpeed != _lastCurveGpuSpeed)
            {
                _wmi.SetGpuFanSpeed((byte)gpuSpeed);
                _lastCurveGpuSpeed = gpuSpeed;

                _gpuFanSlider.Value = Math.Clamp(gpuSpeed, 10, 100);
                _lblGpuFanSpeedHdr.Text = $"GPU FAN: {gpuSpeed}%";
            }
        }

        private static int InterpolateCurve(List<Point> curve, int temp)
        {
            if (curve.Count == 0) return 50;
            if (temp <= curve[0].X) return curve[0].Y;
            if (temp >= curve[^1].X) return curve[^1].Y;

            for (int i = 0; i < curve.Count - 1; i++)
            {
                if (temp >= curve[i].X && temp <= curve[i + 1].X)
                {
                    float span = curve[i + 1].X - curve[i].X;
                    if (span == 0) return curve[i].Y;
                    float t = (temp - curve[i].X) / span;
                    return (int)Math.Round(curve[i].Y + t * (curve[i + 1].Y - curve[i].Y));
                }
            }
            return curve[^1].Y;
        }

        private void ApplyPowerRules(bool pluggedIn)
        {
            if (pluggedIn)
            {
                _btnPerform.Enabled = true;
                _btnTurbo.Enabled = true;
                _btnEco.Enabled = false;
                _trayPowerPerf.Enabled = true;
                _trayPowerTurbo.Enabled = true;
                _trayPowerEco.Enabled = false;

                int acIdx = _cboAcProfile.SelectedIndex;
                if (acIdx > 0 && acIdx < AcProfileValues.Length)
                {
                    byte mode = AcProfileValues[acIdx];
                    ApplyPowerMode(mode, PowerByteToBtn(mode));
                }
                else if (_activePowerBtn == _btnEco)
                {
                    ApplyPowerMode(0x01, _btnBalanced);
                }
            }
            else
            {
                _btnPerform.Enabled = false;
                _btnTurbo.Enabled = false;
                _btnEco.Enabled = true;
                _trayPowerPerf.Enabled = false;
                _trayPowerTurbo.Enabled = false;
                _trayPowerEco.Enabled = true;

                int batIdx = _cboBatteryProfile.SelectedIndex;
                if (batIdx > 0 && batIdx < BatteryProfileValues.Length)
                {
                    byte mode = BatteryProfileValues[batIdx];
                    ApplyPowerMode(mode, PowerByteToBtn(mode));
                }
                else if (_activePowerBtn == _btnPerform || _activePowerBtn == _btnTurbo)
                {
                    ApplyPowerMode(0x01, _btnBalanced);
                }
            }
        }

        private static Color TempColor(int temp) => temp switch
        {
            <= 0 => Color.FromArgb(107, 114, 128),
            < 55 => Color.FromArgb(0, 200, 160),
            < 72 => Color.FromArgb(255, 220, 50),
            < 87 => Color.FromArgb(255, 140, 0),
            _ => Color.FromArgb(255, 60, 60)
        };

        #endregion

        #region UI Helpers

        private byte GetMappedSpeed() => (byte)Math.Clamp(Math.Round(_speedSlider.Value * 9.0 / 100.0), 1, 9);

        private void HighlightBtn(PredatorButton btn, ref PredatorButton? tracker)
        {
            if (tracker != null) tracker.IsActive = false;
            btn.IsActive = true;
            tracker = btn;
        }

        private static void CheckTrayItem(ToolStripMenuItem active, params ToolStripMenuItem[] group)
        {
            foreach (var item in group) item.Checked = false;
            active.Checked = true;
        }

        private void ShowApp()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void HideApp() => this.Hide();

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isClosing)
            {
                e.Cancel = true;
                HideApp();
            }
            else
            {
                _trayIcon.Visible = false;
                _timer.Stop();
                _gameSync.Dispose();
                _appMutex?.Dispose();
                base.OnFormClosing(e);
            }
        }

        private const string StartupTaskName = "PredatorControl";

        private static bool IsStartupEnabled()
        {
            return RunSchtasks($"/Query /TN \"{StartupTaskName}\"") == 0;
        }

        private static bool SetStartupEnabled(bool enable)
        {
            RemoveLegacyRunKey();

            if (!enable)
                return RunSchtasks($"/Delete /TN \"{StartupTaskName}\" /F") == 0 || !IsStartupEnabled();

            string xmlPath = Path.Combine(Path.GetTempPath(), "PredatorControlStartup.xml");
            try
            {
                File.WriteAllText(xmlPath, BuildStartupTaskXml(), Encoding.Unicode);
                return RunSchtasks($"/Create /TN \"{StartupTaskName}\" /XML \"{xmlPath}\" /F") == 0;
            }
            catch { return false; }
            finally
            {
                try { File.Delete(xmlPath); } catch { }
            }
        }

        private static string BuildStartupTaskXml()
        {
            string user = System.Security.SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
            string exe = System.Security.SecurityElement.Escape(Application.ExecutablePath);

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Starts Predator Control at logon with administrator rights.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{user}</UserId>
      <Delay>PT15S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{exe}</Command>
      <Arguments>-hidden</Arguments>
    </Exec>
  </Actions>
</Task>";
        }

        private static int RunSchtasks(string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (p == null) return -1;
                p.WaitForExit(15000);
                return p.HasExited ? p.ExitCode : -1;
            }
            catch { return -1; }
        }

        private static void RemoveLegacyRunKey()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue(StartupTaskName, false);
            }
            catch { }
        }

        private static void MigrateLegacyStartup()
        {
            bool hadLegacy;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                hadLegacy = key?.GetValue(StartupTaskName) != null;
            }
            catch { return; }

            if (hadLegacy && !IsStartupEnabled())
                SetStartupEnabled(true);
            else if (hadLegacy)
                RemoveLegacyRunKey();
        }

        #endregion
    }
}
