using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class GameSyncForm : Form
    {
        #region Theme Colors

        private static readonly Color FormBg = Color.FromArgb(22, 22, 26);
        private static readonly Color PanelBg = Color.FromArgb(30, 30, 34);
        private static readonly Color BorderColor = Color.FromArgb(50, 50, 55);
        private static readonly Color HeaderColor = Color.FromArgb(120, 120, 135);
        private static readonly Color SubHeaderColor = Color.FromArgb(100, 100, 110);
        private static readonly Color AccentColor = Color.FromArgb(0, 200, 160);
        private static readonly Color TextColor = Color.FromArgb(210, 210, 215);

        private static readonly Font FontTitle = new("Segoe UI", 11f, FontStyle.Bold);
        private static readonly Font FontSection = new("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Font FontBody = new("Segoe UI", 9.5f, FontStyle.Regular);

        #endregion

        #region Fields

        private readonly GameSyncController _controller;
        private readonly int _maxHz;

        private ListBox _lstProfiles = null!;
        private Panel _pnlEditor = null!;

        private Label _lblExeName = null!;
        private PredatorDropDown _cboPower = null!, _cboFan = null!, _cboRefresh = null!;
        private PredatorDropDown _cboBattery = null!, _cboRgbMode = null!;
        private PredatorSlider _trkBrightness = null!, _trkSpeed = null!;
        private Label _lblBrightVal = null!, _lblSpeedVal = null!;
        private PredatorButton _btnColorPick = null!;
        private Color _selectedColor = Color.FromArgb(0, 200, 160);
        private ColorDialog _colorPicker = new() { FullOpen = true };
        private PredatorButton _btnSave = null!, _btnRemove = null!, _btnAdd = null!;

        private GameProfile? _editingProfile;

        private static readonly string[] PowerModeNames = { "Quiet", "Balanced", "Performance", "Turbo", "Eco" };
        private static readonly byte[] PowerModeValues = { 0x00, 0x01, 0x04, 0x05, 0x06 };
        private static readonly string[] FanModeNames = { "Auto", "Max", "Custom" };
        private static readonly byte[] FanModeValues = { 0x01, 0x02, 0x03 };
        private static readonly string[] RgbModeNames = { "Don't Change", "Static", "Breathing", "Neon", "Wave", "Shifting", "Zoom", "Meteor", "Twinkling" };

        #endregion

        #region Constructor

        public GameSyncForm(GameSyncController controller, int maxHz)
        {
            _controller = controller;
            _maxHz = maxHz;

            this.Text = "Game Sync Configuration";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = FormBg;
            this.ForeColor = TextColor;
            this.ClientSize = new Size(780, 700);
            this.DoubleBuffered = true;
            this.ShowInTaskbar = false;
            try { this.Icon = new Icon("appicon.ico"); } catch { }

            BuildUI();
            PopulateList();
        }

        #endregion

        #region Window Dragging

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        }

        #endregion

        #region UI Building

        private void BuildUI()
        {
            int pad = 24;

            var pnlTitle = new Panel { Height = 42, Dock = DockStyle.Top, BackColor = Color.FromArgb(18, 18, 21) };
            pnlTitle.MouseDown += TitleBar_MouseDown;
            this.Controls.Add(pnlTitle);

            var lblTitle = new Label { Text = "🎮  Game Sync Configuration", Font = FontTitle, ForeColor = TextColor, AutoSize = true, Location = new Point(pad, 11), BackColor = Color.Transparent };
            lblTitle.MouseDown += TitleBar_MouseDown;
            pnlTitle.Controls.Add(lblTitle);

            var lblClose = new Label { Text = "●", ForeColor = Color.FromArgb(255, 95, 86), Font = new Font("Arial", 12f), AutoSize = true, Cursor = Cursors.Hand, BackColor = Color.Transparent };
            lblClose.Location = new Point(this.ClientSize.Width - pad - lblClose.PreferredWidth - 4, 9);
            lblClose.Click += (s, e) => this.Close();
            pnlTitle.Controls.Add(lblClose);

            int contentY = pnlTitle.Bottom + pad;
            int bottomPad = pad;

            int listW = 220;
            int listAreaH = this.ClientSize.Height - contentY - bottomPad;

            MakeLabel("CONFIGURED APPS", pad, contentY, FontSection, HeaderColor);

            int btnAddH = 34;
            int listH = listAreaH - 26 - (btnAddH * 3) - 28; 

            _lstProfiles = new ListBox
            {
                Location = new Point(pad, contentY + 24),
                Size = new Size(listW, listH),
                BackColor = PanelBg,
                ForeColor = TextColor,
                Font = FontBody,
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 32
            };
            _lstProfiles.DrawItem += LstProfiles_DrawItem;
            _lstProfiles.SelectedIndexChanged += LstProfiles_SelectedIndexChanged;
            this.Controls.Add(_lstProfiles);

            _btnAdd = MakeButton("\uff0b  Browse for .exe", pad, _lstProfiles.Bottom + 8, listW, btnAddH);
            _btnAdd.Click += BtnAdd_Click;

            var btnPick = MakeButton("⚡  Pick Running Process", pad, _lstProfiles.Bottom + 8 + btnAddH + 6, listW, btnAddH);
            btnPick.Click += BtnPickRunning_Click;

            int editorX = pad + listW + pad;
            int editorW = this.ClientSize.Width - editorX - pad;
            int editorH = listAreaH;

            _pnlEditor = new Panel
            {
                Location = new Point(editorX, contentY),
                Size = new Size(editorW, editorH),
                BackColor = FormBg,
                Visible = false
            };
            this.Controls.Add(_pnlEditor);

            var lblEmpty = new Label
            {
                Text = "Select an application from the list\nor add a new one to get started.",
                ForeColor = SubHeaderColor,
                Font = FontBody,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Name = "lblEmpty"
            };

            int ey = 0;
            int sectionGap = 50; 

            _lblExeName = new Label { Text = "", Font = FontTitle, ForeColor = AccentColor, AutoSize = true, Location = new Point(0, ey), MaximumSize = new Size(editorW, 0), BackColor = Color.Transparent };
            _pnlEditor.Controls.Add(_lblExeName);
            ey += 34;

            _pnlEditor.Controls.Add(new Panel { Location = new Point(0, ey), Size = new Size(editorW, 1), BackColor = BorderColor });
            ey += 20;

            MakeLabelIn(_pnlEditor, "POWER MODE", 0, ey, FontSection, HeaderColor);
            ey += 24;
            _cboPower = MakeComboIn(_pnlEditor, PowerModeNames, 0, ey, editorW);
            ey += sectionGap;

            MakeLabelIn(_pnlEditor, "FAN MODE", 0, ey, FontSection, HeaderColor);
            ey += 24;
            _cboFan = MakeComboIn(_pnlEditor, FanModeNames, 0, ey, editorW);
            ey += sectionGap;

            MakeLabelIn(_pnlEditor, "REFRESH RATE", 0, ey, FontSection, HeaderColor);
            ey += 24;
            _cboRefresh = MakeComboIn(_pnlEditor, new[] { "Don't Change", "60 Hz", $"{_maxHz} Hz (Max)" }, 0, ey, editorW);
            ey += sectionGap;

            MakeLabelIn(_pnlEditor, "BATTERY CHARGE LIMIT", 0, ey, FontSection, HeaderColor);
            ey += 24;
            _cboBattery = MakeComboIn(_pnlEditor, new[] { "Don't Change", "Full Charge (100%)", "Limit to 80% (Health)" }, 0, ey, editorW);
            ey += sectionGap;

            MakeLabelIn(_pnlEditor, "RGB KEYBOARD", 0, ey, FontSection, HeaderColor);
            ey += 24;
            _cboRgbMode = MakeComboIn(_pnlEditor, RgbModeNames, 0, ey, editorW);
            ey += sectionGap;

            int halfW = (editorW - 20) / 2;

            MakeLabelIn(_pnlEditor, "BRIGHTNESS", 0, ey, FontSection, HeaderColor);
            _lblBrightVal = MakeLabelIn(_pnlEditor, "100%", halfW - 40, ey, FontBody, SubHeaderColor);
            MakeLabelIn(_pnlEditor, "EFFECT SPEED", halfW + 20, ey, FontSection, HeaderColor);
            _lblSpeedVal = MakeLabelIn(_pnlEditor, "50%", editorW - 40, ey, FontBody, SubHeaderColor);
            ey += 24;

            _trkBrightness = MakeSliderIn(_pnlEditor, 0, ey, halfW, 0, 100, 100);
            _trkBrightness.ValueChanged += (s, e) => _lblBrightVal.Text = $"{_trkBrightness.Value}%";
            _trkSpeed = MakeSliderIn(_pnlEditor, halfW + 20, ey, halfW, 1, 100, 50);
            _trkSpeed.ValueChanged += (s, e) => _lblSpeedVal.Text = $"{_trkSpeed.Value}%";
            ey += sectionGap;

            MakeLabelIn(_pnlEditor, "COLOR CUSTOMIZATION", 0, ey, FontSection, HeaderColor);
            ey += 24;

            _btnColorPick = new PredatorButton { Text = "    Choose Custom Color", Location = new Point(0, ey), Size = new Size(editorW, 36) };
            _btnColorPick.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int cy = _btnColorPick.Height / 2;
                int cx = _btnColorPick.Width / 2 - 70;
                using var brush = new SolidBrush(_selectedColor);
                g.FillEllipse(brush, cx - 6, cy - 6, 12, 12);
                using var glowBrush = new SolidBrush(Color.FromArgb(100, _selectedColor));
                g.FillEllipse(glowBrush, cx - 8, cy - 8, 16, 16);
            };
            _btnColorPick.Click += (s, ev) =>
            {
                _colorPicker.Color = _selectedColor;
                if (_colorPicker.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedColor = _colorPicker.Color;
                    _btnColorPick.Invalidate();
                }
            };
            _pnlEditor.Controls.Add(_btnColorPick);

            ey += sectionGap;

            int btnW = (editorW - 16) / 2;
            _btnSave = new PredatorButton { Text = "💾  Save Profile", Location = new Point(0, ey), Size = new Size(btnW, 38) };
            _pnlEditor.Controls.Add(_btnSave);
            _btnSave.Click += BtnSave_Click;

            _btnRemove = new PredatorButton { Text = "🗑  Remove", Location = new Point(btnW + 12, ey), Size = new Size(btnW, 38) };
            _pnlEditor.Controls.Add(_btnRemove);
            _btnRemove.Click += BtnRemove_Click;
        }

        #endregion

        #region UI Helpers

        private Label MakeLabel(string text, int x, int y, Font font, Color color)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = font, ForeColor = color, BackColor = Color.Transparent };
            this.Controls.Add(lbl);
            return lbl;
        }

        private Label MakeLabelIn(Panel parent, string text, int x, int y, Font font, Color color)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = font, ForeColor = color, BackColor = Color.Transparent };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private PredatorButton MakeButton(string text, int x, int y, int w, int h)
        {
            var btn = new PredatorButton { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
            this.Controls.Add(btn);
            return btn;
        }

        private PredatorDropDown MakeComboIn(Panel parent, string[] items, int x, int y, int w)
        {
            var dd = new PredatorDropDown
            {
                Location = new Point(x, y),
                Size = new Size(w, 34)
            };
            foreach (var item in items) dd.Items.Add(item);
            if (dd.Items.Count > 0) dd.SelectedIndex = 0;
            parent.Controls.Add(dd);
            return dd;
        }

        private PredatorSlider MakeSliderIn(Panel parent, int x, int y, int w, int min, int max, int val)
        {
            var slider = new PredatorSlider
            {
                Location = new Point(x, y),
                Size = new Size(w, 28),
                Minimum = min,
                Maximum = max,
                Value = val
            };
            parent.Controls.Add(slider);
            return slider;
        }



        #endregion

        #region List Drawing

        private void LstProfiles_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var bgBrush = new SolidBrush(selected ? Color.FromArgb(20, 50, 45) : PanelBg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            string text = _lstProfiles.Items[e.Index]?.ToString() ?? "";
            using var textBrush = new SolidBrush(selected ? AccentColor : TextColor);
            var textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(text, FontBody, textBrush, textRect, sf);

            if (selected)
            {
                using var accentPen = new Pen(AccentColor, 2f);
                e.Graphics.DrawLine(accentPen, e.Bounds.X, e.Bounds.Y, e.Bounds.X, e.Bounds.Bottom);
            }
        }

        #endregion

        #region Populate & Select

        private void PopulateList()
        {
            _lstProfiles.Items.Clear();
            foreach (var p in _controller.Profiles)
            {
                _lstProfiles.Items.Add(string.IsNullOrEmpty(p.DisplayName) ? p.ExecutableName : p.DisplayName);
            }
        }

        private void LstProfiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = _lstProfiles.SelectedIndex;
            if (idx < 0 || idx >= _controller.Profiles.Count)
            {
                _pnlEditor.Visible = false;
                _editingProfile = null;
                return;
            }

            _editingProfile = _controller.Profiles[idx];
            LoadProfileToEditor(_editingProfile);
            _pnlEditor.Visible = true;
        }

        private void LoadProfileToEditor(GameProfile p)
        {
            _lblExeName.Text = p.ExecutableName;

            int powerIdx = Array.IndexOf(PowerModeValues, p.PowerMode);
            _cboPower.SelectedIndex = powerIdx >= 0 ? powerIdx : 1;

            int fanIdx = Array.IndexOf(FanModeValues, p.FanMode);
            _cboFan.SelectedIndex = fanIdx >= 0 ? fanIdx : 0;

            if (p.RefreshRate == -1) _cboRefresh.SelectedIndex = 0;
            else if (p.RefreshRate <= 60) _cboRefresh.SelectedIndex = 1;
            else _cboRefresh.SelectedIndex = 2;

            if (p.BatteryLimit == -1) _cboBattery.SelectedIndex = 0;
            else if (p.BatteryLimit == 0) _cboBattery.SelectedIndex = 1;
            else _cboBattery.SelectedIndex = 2;

            if (p.RgbMode == -1) _cboRgbMode.SelectedIndex = 0;
            else _cboRgbMode.SelectedIndex = Math.Clamp(p.RgbMode + 1, 0, RgbModeNames.Length - 1);

            _trkBrightness.Value = p.RgbBrightness >= 0 ? Math.Clamp(p.RgbBrightness, 0, 100) : 100;
            _trkSpeed.Value = p.RgbSpeed >= 0 ? Math.Clamp(p.RgbSpeed, 1, 100) : 50;

            int r = p.RgbR >= 0 ? Math.Clamp(p.RgbR, 0, 255) : 0;
            int g = p.RgbG >= 0 ? Math.Clamp(p.RgbG, 0, 255) : 200;
            int b = p.RgbB >= 0 ? Math.Clamp(p.RgbB, 0, 255) : 160;
            _selectedColor = Color.FromArgb(r, g, b);
            _btnColorPick.Invalidate();
        }

        private GameProfile EditorToProfile()
        {
            var p = new GameProfile
            {
                ExecutableName = _editingProfile?.ExecutableName ?? "",
                DisplayName = _editingProfile?.DisplayName ?? "",
                PowerMode = PowerModeValues[_cboPower.SelectedIndex],
                FanMode = FanModeValues[_cboFan.SelectedIndex],
            };

            p.RefreshRate = _cboRefresh.SelectedIndex switch
            {
                1 => 60,
                2 => _maxHz,
                _ => -1
            };

            p.BatteryLimit = _cboBattery.SelectedIndex switch
            {
                1 => 0,
                2 => 1,
                _ => -1
            };

            p.RgbMode = _cboRgbMode.SelectedIndex == 0 ? -1 : _cboRgbMode.SelectedIndex - 1;
            p.RgbBrightness = _trkBrightness.Value;
            p.RgbSpeed = _trkSpeed.Value;
            p.RgbR = _selectedColor.R;
            p.RgbG = _selectedColor.G;
            p.RgbB = _selectedColor.B;

            return p;
        }

        #endregion

        #region Actions

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select an Application",
                    Filter = "Executables (*.exe)|*.exe",
                    CheckFileExists = true
                };

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string exeName = Path.GetFileName(ofd.FileName);
                    string displayName = Path.GetFileNameWithoutExtension(ofd.FileName);
                    TryAddProfile(exeName, displayName);
                }
            }
            catch
            {
                MessageBox.Show(this,
                    "Could not browse to that location.\n\nYou can find the process name in Task Manager while the game is running, and use 'Pick Running Process' to add it.",
                    "Browse Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnPickRunning_Click(object? sender, EventArgs e)
        {
            var systemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "svchost", "csrss", "smss", "wininit", "winlogon", "services", "lsass",
                "fontdrvhost", "dwm", "conhost", "dllhost", "taskhostw", "sihost",
                "ctfmon", "spoolsv", "SearchIndexer", "WmiPrvSE", "RuntimeBroker",
                "ShellExperienceHost", "StartMenuExperienceHost", "TextInputHost",
                "SecurityHealthSystray", "SecurityHealthService", "MsMpEng",
                "NisSrv", "SgrmBroker", "audiodg", "PredatorControlApp", "System",
                "Registry", "Idle", "Memory Compression"
            };

            List<string> allProcs;
            try
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        string name = p.ProcessName;
                        if (!systemNames.Contains(name))
                            names.Add(name + ".exe");
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
                allProcs = names.OrderBy(n => n).ToList();
                GC.Collect(0, GCCollectionMode.Optimized);
            }
            catch
            {
                allProcs = new List<string>();
            }

            if (allProcs.Count == 0)
            {
                MessageBox.Show(this, "No running processes found.", "Pick Process",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dlgBg    = Color.FromArgb(22, 22, 26);
            var dlgTitle = Color.FromArgb(18, 18, 21);
            var dlgBorder= Color.FromArgb(55, 55, 62);
            var dlgText  = Color.FromArgb(210, 210, 215);
            var dlgSub   = Color.FromArgb(140, 140, 150);
            var titleFont= new Font("Segoe UI", 10f, FontStyle.Bold);
            var bodyFont = new Font("Segoe UI", 9.25f);

            using var dlg = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(360, 480),
                BackColor       = dlgBg,
                ForeColor       = dlgText
            };

            bool dragging = false; Point dragStart = Point.Empty;

            var pnlTitle = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = dlgTitle };
            pnlTitle.MouseDown += (s, ev) => { if (ev.Button == MouseButtons.Left) { dragging = true; dragStart = ev.Location; } };
            pnlTitle.MouseMove += (s, ev) => { if (dragging) dlg.Location = new Point(dlg.Left + ev.X - dragStart.X, dlg.Top + ev.Y - dragStart.Y); };
            pnlTitle.MouseUp   += (s, ev) => dragging = false;
            dlg.Controls.Add(pnlTitle);

            var lblTitle = new Label
            {
                Text = "Pick a Running Process",
                Font = titleFont, ForeColor = dlgText, AutoSize = true,
                Location = new Point(14, 11), BackColor = Color.Transparent
            };
            lblTitle.MouseDown += (s, ev) => { if (ev.Button == MouseButtons.Left) { dragging = true; dragStart = ev.Location; } };
            pnlTitle.Controls.Add(lblTitle);

            var lblClose = new Label
            {
                Text = "●", ForeColor = Color.FromArgb(255, 95, 86),
                Font = new Font("Arial", 11f), AutoSize = true,
                Cursor = Cursors.Hand, BackColor = Color.Transparent,
                Location = new Point(dlg.ClientSize.Width - 24, 10)
            };
            lblClose.Click += (s, ev) => dlg.Close();
            pnlTitle.Controls.Add(lblClose);

            dlg.Paint += (s, pe) =>
            {
                using var pen = new Pen(dlgBorder, 1f);
                pe.Graphics.DrawRectangle(pen, 0, 0, dlg.Width - 1, dlg.Height - 1);
            };

            var pnlSearch = new Panel
            {
                Location = new Point(14, 50), Size = new Size(332, 30),
                BackColor = dlgBorder
            };
            dlg.Controls.Add(pnlSearch);

            var txtSearch = new TextBox
            {
                Location = new Point(1, 1), Size = new Size(330, 28),
                BackColor = Color.FromArgb(28, 28, 32), ForeColor = dlgText,
                Font = bodyFont, BorderStyle = BorderStyle.None,
                PlaceholderText = "Search..."
            };
            pnlSearch.Controls.Add(txtSearch);

            var lstProcs = new ListBox
            {
                Location = new Point(14, 90), Size = new Size(332, 340),
                BackColor = Color.FromArgb(28, 28, 32), ForeColor = dlgText,
                Font = bodyFont, BorderStyle = BorderStyle.None,
                SelectionMode = SelectionMode.One
            };
            dlg.Controls.Add(lstProcs);

            void RefreshList(string filter)
            {
                lstProcs.BeginUpdate();
                lstProcs.Items.Clear();
                var filtered = string.IsNullOrWhiteSpace(filter)
                    ? allProcs
                    : allProcs.Where(p => p.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                foreach (var p in filtered) lstProcs.Items.Add(p);
                lstProcs.EndUpdate();
            }

            RefreshList("");
            txtSearch.TextChanged += (s, ev) => RefreshList(txtSearch.Text);

            var btnAdd2 = new PredatorButton
            {
                Text = "Add Selected", Location = new Point(14, 438), Size = new Size(332, 34)
            };
            dlg.Controls.Add(btnAdd2);

            DialogResult result = DialogResult.Cancel;
            string? picked = null;

            void DoAdd()
            {
                if (lstProcs.SelectedItem is string sel)
                {
                    picked = sel;
                    result = DialogResult.OK;
                    dlg.Close();
                }
            }

            btnAdd2.Click     += (s, ev) => DoAdd();
            lstProcs.DoubleClick += (s, ev) => DoAdd();
            txtSearch.KeyDown += (s, ke) =>
            {
                if (ke.KeyCode == Keys.Down && lstProcs.Items.Count > 0)
                {
                    lstProcs.SelectedIndex = 0;
                    lstProcs.Focus();
                    ke.Handled = true;
                }
                else if (ke.KeyCode == Keys.Escape) dlg.Close();
            };
            lstProcs.KeyDown += (s, ke) =>
            {
                if (ke.KeyCode == Keys.Enter) { ke.SuppressKeyPress = true; DoAdd(); }
                else if (ke.KeyCode == Keys.Escape) dlg.Close();
            };

            dlg.ShowDialog(this);

            if (result == DialogResult.OK && picked != null)
            {
                string displayName = Path.GetFileNameWithoutExtension(picked);
                TryAddProfile(picked, displayName);
            }
        }

        private void TryAddProfile(string exeName, string displayName)
        {
            foreach (var existing in _controller.Profiles)
            {
                if (existing.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, $"\"{exeName}\" is already configured.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            var profile = new GameProfile
            {
                ExecutableName = exeName,
                DisplayName = displayName,
                PowerMode = 0x01,
                FanMode = 0x01,
                RefreshRate = -1,
                BatteryLimit = -1,
                RgbMode = -1,
                RgbBrightness = -1,
                RgbSpeed = -1,
                RgbR = -1,
                RgbG = -1,
                RgbB = -1,
            };

            _controller.AddProfile(profile);
            PopulateList();
            _lstProfiles.SelectedIndex = _lstProfiles.Items.Count - 1;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_editingProfile == null) return;

            var updated = EditorToProfile();
            _controller.UpdateProfile(updated);

            int idx = _lstProfiles.SelectedIndex;
            PopulateList();
            if (idx >= 0 && idx < _lstProfiles.Items.Count)
                _lstProfiles.SelectedIndex = idx;
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (_editingProfile == null) return;

            var result = MessageBox.Show(this,
                $"Remove \"{_editingProfile.ExecutableName}\" from Game Sync?",
                "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _controller.RemoveProfile(_editingProfile.ExecutableName);
                _editingProfile = null;
                _pnlEditor.Visible = false;
                PopulateList();
            }
        }

        #endregion
    }
}
