using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class PredatorButton : Control
    {
        private bool _isHover;
        private bool _isActive;
        private Color? _customActiveColor;

        private static readonly Color BgNormal = Color.FromArgb(37, 37, 40);
        private static readonly Color BgHover = Color.FromArgb(48, 48, 52);
        private static readonly Color BgActive = Color.FromArgb(20, 50, 45);
        private static readonly Color BgDisabled = Color.FromArgb(28, 28, 30);
        private static readonly Color BorderNormal = Color.FromArgb(60, 60, 66);
        private static readonly Color BorderHover = Color.FromArgb(80, 80, 88);
        private static readonly Color BorderDisabled = Color.FromArgb(40, 40, 44);
        private static readonly Color Accent = Color.FromArgb(0, 200, 160);
        private static readonly Color TextNormal = Color.FromArgb(170, 170, 175);
        private static readonly Color TextDisabled = Color.FromArgb(70, 70, 75);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color? CustomActiveColor
        {
            get => _customActiveColor;
            set { _customActiveColor = value; Invalidate(); }
        }

        public PredatorButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Size = new Size(96, 40);
            Cursor = Cursors.Hand;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            g.Clear(Parent?.BackColor ?? Color.FromArgb(30, 30, 30));

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = RoundedRect(rect, 8);

            Color bg, border, textColor;
            float borderWidth;

            if (!Enabled)
            {
                bg = BgDisabled;
                border = BorderDisabled;
                textColor = TextDisabled;
                borderWidth = 1f;
            }
            else if (_isActive)
            {
                bg = BgActive;
                border = Accent;
                textColor = _customActiveColor ?? Accent;
                borderWidth = 1.6f;
            }
            else if (_isHover)
            {
                bg = BgHover;
                border = BorderHover;
                textColor = Color.White;
                borderWidth = 1f;
            }
            else
            {
                bg = BgNormal;
                border = BorderNormal;
                textColor = TextNormal;
                borderWidth = 1f;
            }

            using (var bgBrush = new SolidBrush(bg))
                g.FillPath(bgBrush, path);

            using (var pen = new Pen(border, borderWidth))
                g.DrawPath(pen, path);

            if (_isActive && Enabled)
            {
                using var glowPen = new Pen(Color.FromArgb(35, 0, 200, 160), 3f);
                g.DrawPath(glowPen, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (Enabled) { _isHover = true; Invalidate(); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled) { _isHover = false; Cursor = Cursors.Default; }
            else { Cursor = Cursors.Hand; }
            Invalidate();
            base.OnEnabledChanged(e);
        }
    }
}
