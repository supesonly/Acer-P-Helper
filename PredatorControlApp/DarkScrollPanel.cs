using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Drawing.Drawing2D;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class DarkScrollPanel : Panel
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        private const int SB_VERT = 1;
        private const int WM_NCPAINT = 0x0085;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_NCCALCSIZE = 0x0083;

        private float _dpi = 1f;
        private readonly SolidBrush _trackBrush = new(Color.FromArgb(32, 32, 36));
        private readonly SolidBrush _thumbBrush = new(Color.FromArgb(75, 78, 85));

        public DarkScrollPanel()
        {
            AutoScroll = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetDpiScale(float dpi) => _dpi = dpi;
        private int S(int v) => (int)(v * _dpi);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case WM_NCPAINT:
                case WM_VSCROLL:
                case WM_MOUSEWHEEL:
                case WM_NCCALCSIZE:
                    ShowScrollBar(Handle, SB_VERT, false);
                    break;
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ShowScrollBar(Handle, SB_VERT, false);
            InvalidateScrollBar();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ShowScrollBar(Handle, SB_VERT, false);
            InvalidateScrollBar();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsHandleCreated)
                ShowScrollBar(Handle, SB_VERT, false);
        }

        private void InvalidateScrollBar()
        {
            int barW = S(8);
            int barX = ClientSize.Width - barW;
            int scrollY = VerticalScroll.Value;
            Invalidate(new Rectangle(barX, scrollY, barW, ClientSize.Height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int totalContent = VerticalScroll.Maximum + 1;
            int viewH = ClientSize.Height;
            if (totalContent <= viewH) return;

            int scrollPos = VerticalScroll.Value;
            int barW = S(5);
            int barX = ClientSize.Width - barW - S(3);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int oy = scrollPos;

            g.FillRectangle(_trackBrush, barX - S(1), oy, barW + S(2), viewH);

            float thumbRatio = (float)viewH / totalContent;
            int thumbH = Math.Max(S(30), (int)(viewH * thumbRatio));
            float scrollFrac = (float)scrollPos / Math.Max(1, totalContent - viewH);
            int thumbY = oy + (int)(scrollFrac * (viewH - thumbH));

            int r = barW / 2;
            var rect = new Rectangle(barX, thumbY, barW, thumbH);
            using var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(_thumbBrush, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trackBrush.Dispose();
                _thumbBrush.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
