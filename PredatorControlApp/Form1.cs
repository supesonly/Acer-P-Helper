using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
        private static readonly Mutex _appMutex = new(true, "PredatorControlApp_Unique_System_Mutex_999");

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

        #region Win32 Interop — Display Control

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int DM_DISPLAYFREQUENCY = 0x400000;

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

        private bool? _isPluggedIn = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
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
        private Label _lblPowerStatus = null!, _lblFanStatus = null!;
        private Label _lblBrightHdr = null!, _lblSpeedHdr = null!;

        private PredatorButton _btnQuiet = null!, _btnBalanced = null!, _btnPerform = null!,
                               _btnTurbo = null!, _btnEco = null!;

        private PredatorButton _btnAutoFan = null!, _btnMaxFan = null!, _btnCustomFan = null!;
        private PredatorButton _btn60Hz = null!, _btnMaxHz = null!;

        private PredatorDropDown _rgbDropDown = null!;
        private PredatorButton _btnColorPick = null!;

        private PredatorSlider _brightnessSlider = null!, _speedSlider = null!;
        
        private PredatorButton? _activePowerBtn, _activeFanBtn, _activeDisplayBtn;

        private static readonly string[] RgbModeNames = { "Static", "Breathing", "Neon", "Wave", "Shifting", "Zoom", "Meteor", "Twinkling" };

        private ToolStripMenuItem _trayPowerQuiet = null!, _trayPowerBal = null!, _trayPowerPerf = null!,
                                  _trayPowerTurbo = null!, _trayPowerEco = null!;
        private ToolStripMenuItem _trayFanAuto = null!, _trayFanMax = null!, _trayFanCustom = null!;
        private ToolStripMenuItem _trayDisplay60 = null!, _trayDisplayMax = null!;
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
            if (!_appMutex.WaitOne(TimeSpan.Zero, true))
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
            RegisterStartup();

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

            _timer.Interval = 2000;
            _timer.Tick += UpdateTelemetry;
            _timer.Start();

            this.Shown += (s, e) =>
            {
                if (Environment.CommandLine.Contains("-hidden")) HideApp();
            };
        }

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
            DEVMODE dm = new(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            int maxHz = 60, modeNum = 0;
            while (EnumDisplaySettings(null, modeNum, ref dm))
            {
                if (dm.dmDisplayFrequency > maxHz) maxHz = dm.dmDisplayFrequency;
                modeNum++;
            }
            return maxHz;
        }

        private void SetRefreshRate(int hz)
        {
            DEVMODE dm = new(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                dm.dmDisplayFrequency = hz;
                dm.dmFields = DM_DISPLAYFREQUENCY;
                ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);
            }
        }

        #endregion

        #region System Tray

        private void SetupSystemTray()
        {
            try { _trayIcon.Icon = new Icon("appicon.ico"); }
            catch { _trayIcon.Icon = SystemIcons.Application; }

            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Text = "Predator Control";
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (s, e) => ShowApp();
        }

        private void BuildTrayMenu()
        {
            _trayMenu = new ContextMenuStrip();

            var powerMenu = new ToolStripMenuItem("⚡  Power Mode");
            _trayPowerQuiet = new ToolStripMenuItem("Quiet", null, (s, e) => ApplyPowerMode(0x00, _btnQuiet));
            _trayPowerBal = new ToolStripMenuItem("Balanced", null, (s, e) => ApplyPowerMode(0x01, _btnBalanced));
            _trayPowerPerf = new ToolStripMenuItem("Performance", null, (s, e) => ApplyPowerMode(0x04, _btnPerform));
            _trayPowerTurbo = new ToolStripMenuItem("Turbo", null, (s, e) => ApplyPowerMode(0x05, _btnTurbo));
            _trayPowerEco = new ToolStripMenuItem("Eco", null, (s, e) => ApplyPowerMode(0x06, _btnEco));
            powerMenu.DropDownItems.AddRange([_trayPowerQuiet, _trayPowerBal, _trayPowerPerf, _trayPowerTurbo, _trayPowerEco]);

            var fanMenu = new ToolStripMenuItem("🌀  Fan Mode");
            _trayFanAuto = new ToolStripMenuItem("Auto", null, (s, e) => ApplyFanMode(0x01, _btnAutoFan));
            _trayFanMax = new ToolStripMenuItem("Max", null, (s, e) => ApplyFanMode(0x02, _btnMaxFan));
            _trayFanCustom = new ToolStripMenuItem("Custom", null, (s, e) => ApplyFanMode(0x03, _btnCustomFan));
            fanMenu.DropDownItems.AddRange([_trayFanAuto, _trayFanMax, _trayFanCustom]);

            var displayMenu = new ToolStripMenuItem("📺  Display");
            _trayDisplay60 = new ToolStripMenuItem("60 Hz", null, (s, e) => ApplyDisplayMode(60, _btn60Hz));
            _trayDisplayMax = new ToolStripMenuItem($"{_maxHz} Hz", null, (s, e) => ApplyDisplayMode(_maxHz, _btnMaxHz));
            displayMenu.DropDownItems.AddRange([_trayDisplay60, _trayDisplayMax]);

            var rgbMenu = new ToolStripMenuItem("🎨  Keyboard RGB");
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

            _trayMenu.Items.Add(powerMenu);
            _trayMenu.Items.Add(fanMenu);
            _trayMenu.Items.Add(displayMenu);
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
            this.ClientSize = new Size(_formW, S(660));
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            try { this.Icon = new Icon("appicon.ico"); } catch { }

            int pad = S(24);
            int contentW = _formW - pad * 2;
            int gap = S(6);
            int btnH = S(34);
            int y = 0;

            var pnlTitle = new Panel { Height = S(40), Width = _formW, BackColor = Color.FromArgb(18, 18, 21) };
            pnlTitle.MouseDown += TitleBar_MouseDown;
            this.Controls.Add(pnlTitle);
            var picIcon = new PictureBox { Image = new Icon("appicon.ico").ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(S(16), S(16)), Location = new Point(pad - S(4), S(12)), BackColor = Color.Transparent };
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

            y = pnlTitle.Bottom + S(24);

            MakeLabel("CPU:", pad, y, FontBody, SubHeaderColor);
            _lblCpuTemp = MakeLabel("43°C", pad + S(34), y, FontBodyBold, Color.White);

            MakeLabel("GPU:", _formW / 2 + S(10), y, FontBody, SubHeaderColor);
            _lblGpuTemp = MakeLabel("39°C", _formW / 2 + S(46), y, FontBodyBold, Color.White);

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

            y += btnH + S(28);
            MakeSectionHeader("FAN CONTROL", pad, y);
            
            y += S(24);
            int fanBtnW = (contentW - 2 * gap) / 3;
            _btnAutoFan = MakeButton("Auto", pad, y, fanBtnW, btnH);
            _btnMaxFan = MakeButton("Max", pad + (fanBtnW + gap), y, fanBtnW, btnH);
            _btnCustomFan = MakeButton("Custom", pad + (fanBtnW + gap) * 2, y, fanBtnW, btnH);

            _btnAutoFan.Click += (s, e) => ApplyFanMode(0x01, _btnAutoFan);
            _btnMaxFan.Click += (s, e) => ApplyFanMode(0x02, _btnMaxFan);
            _btnCustomFan.Click += (s, e) => ApplyFanMode(0x03, _btnCustomFan);

            y += btnH + S(28);
            MakeSectionHeader("DISPLAY REFRESH RATE", pad, y);
            
            y += S(24);
            int dispBtnW = (contentW - gap) / 2;
            _btn60Hz = MakeButton("60 Hz", pad, y, dispBtnW, btnH);
            _btnMaxHz = MakeButton($"{_maxHz} Hz (Max)", pad + dispBtnW + gap, y, dispBtnW, btnH);

            _btn60Hz.Click += (s, e) => ApplyDisplayMode(60, _btn60Hz);
            _btnMaxHz.Click += (s, e) => ApplyDisplayMode(_maxHz, _btnMaxHz);

            y += btnH + S(28);
            MakeSectionHeader("KEYBOARD RGB MODE", pad, y);
            
            y += S(24);
            int dropH = S(34);
            _rgbDropDown = new PredatorDropDown { Location = new Point(pad, y), Size = new Size(contentW, dropH) };
            foreach (var name in RgbModeNames) _rgbDropDown.Items.Add(name);
            _rgbDropDown.SelectedIndex = 3; 
            this.Controls.Add(_rgbDropDown);

            y += dropH + S(28);
            _lblBrightHdr = MakeLabel("BRIGHTNESS: 100%", pad, y, FontSectionHeader, SubHeaderColor);
            _lblSpeedHdr = MakeLabel("EFFECT SPEED: 50%", _formW / 2 + S(10), y, FontSectionHeader, SubHeaderColor);
            
            y += S(24);
            int sliderW = (contentW - gap * 4) / 2;
            _brightnessSlider = new PredatorSlider { Location = new Point(pad, y), Size = new Size(sliderW, S(28)), Minimum = 0, Maximum = 100, Value = 100 };
            this.Controls.Add(_brightnessSlider);
            
            _speedSlider = new PredatorSlider { Location = new Point(_formW / 2 + S(10), y), Size = new Size(sliderW, S(28)), Minimum = 1, Maximum = 100, Value = 50 };
            this.Controls.Add(_speedSlider);

            _brightnessSlider.ValueChanged += (s, e) => { _lblBrightHdr.Text = $"BRIGHTNESS: {_brightnessSlider.Value}%"; };
            _brightnessSlider.ValueCommitted += (s, e) => { _wmi.SetBrightness((byte)_brightnessSlider.Value); SaveState("Brightness", _brightnessSlider.Value); };

            _speedSlider.ValueChanged += (s, e) => { _lblSpeedHdr.Text = $"EFFECT SPEED: {_speedSlider.Value}%"; };
            _speedSlider.ValueCommitted += (s, e) => { _wmi.SetSpeed(GetMappedSpeed()); SaveState("RGB_Speed", _speedSlider.Value); };

            _rgbDropDown.SelectedIndexChanged += (s, e) =>
            {
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
            this.Controls.Add(lbl);
            return lbl;
        }

        private PredatorButton MakeButton(string text, int x, int y, int width, int height)
        {
            var btn = new PredatorButton { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
            this.Controls.Add(btn);
            return btn;
        }

        private void AddSeparator(int y)
        {
            int pad = S(24);
            this.Controls.Add(new Panel { Location = new Point(pad, y), Size = new Size(_formW - pad * 2, 1), BackColor = SeparatorColor });
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

            var trayItem = mode switch
            {
                0x01 => _trayFanAuto,
                0x02 => _trayFanMax,
                _ => _trayFanCustom
            };
            CheckTrayItem(trayItem, _trayFanAuto, _trayFanMax, _trayFanCustom);
        }

        private void ApplyDisplayMode(int hz, PredatorButton btn)
        {
            SetRefreshRate(hz);
            HighlightBtn(btn, ref _activeDisplayBtn);
            CheckTrayItem(hz <= 60 ? _trayDisplay60 : _trayDisplayMax, _trayDisplay60, _trayDisplayMax);
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

        #region State Persistence

        private void SaveState(string name, int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                key.SetValue(name, value);
            }
            catch { }
        }

        private void LoadMemory()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
                int savedPower = (int)key.GetValue("Power", 0x01);
                int savedFan = (int)key.GetValue("Fan", 0x01);
                int savedRgbMode = (int)key.GetValue("RGB_Mode", 3);
                int savedBrightness = (int)key.GetValue("Brightness", 100);
                int savedSpeed = (int)key.GetValue("RGB_Speed", 50);

                int savedR = (int)key.GetValue("RGB_R", 0);
                int savedG = (int)key.GetValue("RGB_G", 150);
                int savedB = (int)key.GetValue("RGB_B", 255);
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

                var (fanMode, fanBtn) = savedFan switch
                {
                    0x02 => ((byte)0x02, _btnMaxFan),
                    0x03 => ((byte)0x03, _btnCustomFan),
                    _ => ((byte)0x01, _btnAutoFan)
                };
                ApplyFanMode(fanMode, fanBtn);

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
            }
            catch { }
        }

        private void RegisterStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                string appPath = $"\"{Application.ExecutablePath}\" -hidden";
                key?.SetValue("PredatorControl", appPath);
            }
            catch { }
        }

        #endregion

        #region Telemetry & Power Rules

        private void UpdateTelemetry(object? sender, EventArgs e)
        {
            bool currentlyPluggedIn = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
            if (_isPluggedIn != currentlyPluggedIn)
            {
                _isPluggedIn = currentlyPluggedIn;
                ApplyPowerRules(currentlyPluggedIn);
            }

            _cpuTemp = _wmi.CpuTemp;
            _gpuTemp = _wmi.GpuTemp;

            _lblCpuTemp.Text = _cpuTemp > 0 ? $"{_cpuTemp}°C" : "--°C";
            _lblGpuTemp.Text = _gpuTemp > 0 ? $"{_gpuTemp}°C" : "--°C";
            _lblCpuTemp.ForeColor = TempColor(_cpuTemp);
            _lblGpuTemp.ForeColor = TempColor(_gpuTemp);

            _trayIcon.Text = $"Predator Control\nCPU: {(_cpuTemp > 0 ? $"{_cpuTemp}°C" : "N/A")}  GPU: {(_gpuTemp > 0 ? $"{_gpuTemp}°C" : "N/A")}";
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

                if (_activePowerBtn == _btnEco)
                    ApplyPowerMode(0x01, _btnBalanced);
            }
            else
            {
                _btnPerform.Enabled = false;
                _btnTurbo.Enabled = false;
                _btnEco.Enabled = true;
                _trayPowerPerf.Enabled = false;
                _trayPowerTurbo.Enabled = false;
                _trayPowerEco.Enabled = true;

                ApplyPowerMode(0x06, _btnEco);
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
                _appMutex.Dispose();
                base.OnFormClosing(e);
            }
        }

        #endregion
    }
}