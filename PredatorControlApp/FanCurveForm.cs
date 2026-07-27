using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    public class FanCurveApplyEventArgs : EventArgs
    {
        public List<Point> CpuPoints { get; set; } = new();
        public List<Point> GpuPoints { get; set; } = new();
        public bool Success { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public class FanCurveForm : Form
    {
        #region Theme Colors & Fonts

        private static readonly Color FormBg = Color.FromArgb(22, 22, 26);
        private static readonly Color TitleBarBg = Color.FromArgb(18, 18, 21);
        private static readonly Color SeparatorColor = Color.FromArgb(40, 40, 44);
        private static readonly Color TitleTextColor = Color.FromArgb(200, 200, 205);
        private static readonly Color CloseHoverColor = Color.FromArgb(220, 50, 50);

        private static readonly Color CpuCurveColor = Color.FromArgb(0, 180, 255);
        private static readonly Color GpuCurveColor = Color.FromArgb(255, 77, 109);

        private static readonly Color FlashGreen = Color.FromArgb(0, 180, 80);
        private static readonly Color FlashRed = Color.FromArgb(220, 50, 50);

        private static readonly Font FontTitle = new("Segoe UI", 10f, FontStyle.Bold);

        #endregion

        #region Fields

        private FanCurveGraph _graphCpu = null!;
        private FanCurveGraph _graphGpu = null!;
        private PredatorButton _btnReset = null!;
        private PredatorButton _btnApply = null!;
        private System.Windows.Forms.Timer _flashTimer = null!;

        private const int SidePad = 16;
        private const int GraphHeight = 220;
        private const int TitleBarHeight = 36;
        private const int SepHeight = 1;
        private const int ButtonRowHeight = 50;

        #endregion

        #region Events

        public event EventHandler<FanCurveApplyEventArgs>? ApplyClicked;

        #endregion

        #region Window Dragging

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        #endregion

        #region Constructor

        public FanCurveForm()
        {
            Text = "Fan Curve Editor";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = FormBg;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int totalHeight = TitleBarHeight + SepHeight
                            + GraphHeight + SepHeight
                            + GraphHeight + SepHeight
                            + ButtonRowHeight;

            int totalWidth = 460;
            ClientSize = new Size(totalWidth, totalHeight);
            
            _flashTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _flashTimer.Tick += FlashTimer_Tick;

            BuildUI();
        }

        #endregion

        #region UI Building

        private void BuildUI()
        {
            int y = 0;

            var pnlTitle = new Panel
            {
                Height = TitleBarHeight,
                Dock = DockStyle.Top,
                BackColor = TitleBarBg
            };
            pnlTitle.MouseDown += TitleBar_MouseDown;
            Controls.Add(pnlTitle);

            var lblTitle = new Label
            {
                Text = "Fan Curve Editor",
                Font = FontTitle,
                ForeColor = TitleTextColor,
                AutoSize = true,
                Location = new Point(SidePad, (TitleBarHeight - FontTitle.Height) / 2),
                BackColor = Color.Transparent
            };
            lblTitle.MouseDown += TitleBar_MouseDown;
            pnlTitle.Controls.Add(lblTitle);

            var lblClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 145),
                AutoSize = true,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            lblClose.Location = new Point(
                ClientSize.Width - SidePad - lblClose.PreferredWidth - 2,
                (TitleBarHeight - lblClose.PreferredHeight) / 2);
            lblClose.Click += (s, e) => Close();
            lblClose.MouseEnter += (s, e) => lblClose.ForeColor = CloseHoverColor;
            lblClose.MouseLeave += (s, e) => lblClose.ForeColor = Color.FromArgb(140, 140, 145);
            pnlTitle.Controls.Add(lblClose);

            y = TitleBarHeight;

            Controls.Add(MakeSeparator(y));
            y += SepHeight;

            _graphCpu = new FanCurveGraph
            {
                CurveColor = CpuCurveColor,
                FanLabel = "CPU FAN CURVE",
                Location = new Point(SidePad, y),
                Size = new Size(ClientSize.Width - SidePad * 2, GraphHeight)
            };
            Controls.Add(_graphCpu);
            y += GraphHeight;

            Controls.Add(MakeSeparator(y));
            y += SepHeight;

            _graphGpu = new FanCurveGraph
            {
                CurveColor = GpuCurveColor,
                FanLabel = "GPU FAN CURVE",
                Location = new Point(SidePad, y),
                Size = new Size(ClientSize.Width - SidePad * 2, GraphHeight)
            };
            Controls.Add(_graphGpu);
            y += GraphHeight;

            Controls.Add(MakeSeparator(y));
            y += SepHeight;

            int btnWidth = 96;
            int btnHeight = 34;
            int btnY = y + (ButtonRowHeight - btnHeight) / 2;

            _btnReset = new PredatorButton
            {
                Text = "Reset",
                Size = new Size(btnWidth, btnHeight),
                Location = new Point(SidePad, btnY)
            };
            _btnReset.Click += BtnReset_Click;
            Controls.Add(_btnReset);

            _btnApply = new PredatorButton
            {
                Text = "Apply",
                Size = new Size(btnWidth, btnHeight),
                Location = new Point(ClientSize.Width - SidePad - btnWidth, btnY)
            };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);
        }

        private Panel MakeSeparator(int yPos)
        {
            return new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(ClientSize.Width, SepHeight),
                BackColor = SeparatorColor
            };
        }

        #endregion

        #region Button Handlers

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            _graphCpu.Points = _graphCpu.DefaultPoints;
            _graphGpu.Points = _graphGpu.DefaultPoints;
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            var args = new FanCurveApplyEventArgs
            {
                CpuPoints = new List<Point>(_graphCpu.Points),
                GpuPoints = new List<Point>(_graphGpu.Points),
                Success = false
            };

            ApplyClicked?.Invoke(this, args);

            Color flashColor = args.Success ? FlashGreen : FlashRed;
            _btnApply.CustomActiveColor = flashColor;
            _btnApply.IsActive = true;

            _flashTimer.Stop();
            _flashTimer.Start();
        }

        private void FlashTimer_Tick(object? sender, EventArgs e)
        {
            _flashTimer.Stop();
            _btnApply.IsActive = false;
            _btnApply.CustomActiveColor = null;
        }

        #endregion

        #region Public Methods

        public void UpdateTemps(int cpuTemp, int gpuTemp)
        {
            _graphCpu.CurrentTemp = cpuTemp;
            _graphGpu.CurrentTemp = gpuTemp;
        }

        public void SetCpuCurve(List<Point> points)
        {
            _graphCpu.Points = new List<Point>(points);
        }

        public void SetGpuCurve(List<Point> points)
        {
            _graphGpu.Points = new List<Point>(points);
        }

        public List<Point> GetCpuCurve() => new(_graphCpu.Points);

        public List<Point> GetGpuCurve() => new(_graphGpu.Points);

        public int InterpolateCpuSpeed(int temp) => _graphCpu.InterpolateSpeed(temp);

        public int InterpolateGpuSpeed(int temp) => _graphGpu.InterpolateSpeed(temp);

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flashTimer?.Stop();
                _flashTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
