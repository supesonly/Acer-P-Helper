using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class PredatorDropDown : Control
    {
        private bool _isHover;
        private bool _isOpen;
        private bool _isClosingPopup; 
        private int _selectedIndex = -1;
        private readonly List<string> _items = new();
        private Form? _popup;
        private ListBox? _listBox;
        private int _hoverIndex = -1;

        private static readonly Color BgNormal = Color.FromArgb(37, 37, 40);
        private static readonly Color BgHover = Color.FromArgb(48, 48, 52);
        private static readonly Color BorderNormal = Color.FromArgb(60, 60, 66);
        private static readonly Color BorderHover = Color.FromArgb(80, 80, 88);
        private static readonly Color Accent = Color.FromArgb(0, 200, 160);
        private static readonly Color TextNormal = Color.FromArgb(170, 170, 175);
        private static readonly Color DropBg = Color.FromArgb(28, 28, 30);
        private static readonly Color DropHover = Color.FromArgb(48, 48, 52);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value >= -1 && value < _items.Count && value != _selectedIndex)
                {
                    _selectedIndex = value;
                    Invalidate();
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedText => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<string> Items => _items;

        public event EventHandler? SelectedIndexChanged;

        public PredatorDropDown()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Size = new Size(180, 34);
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
            using var path = RoundedRect(rect, 6);

            Color bg = _isHover || _isOpen ? BgHover : BgNormal;
            Color border = _isHover || _isOpen ? BorderHover : BorderNormal;

            using (var bgBrush = new SolidBrush(bg))
                g.FillPath(bgBrush, path);

            using (var pen = new Pen(border, 1f))
                g.DrawPath(pen, path);

            string displayText = SelectedText;
            if (string.IsNullOrEmpty(displayText)) displayText = "Select...";
            var textRect = new Rectangle(12, 0, Width - 36, Height);
            TextRenderer.DrawText(g, displayText, Font, textRect, TextNormal,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int arrowX = Width - 22;
            int arrowY = Height / 2 - 2;
            using var arrowPen = new Pen(Color.FromArgb(140, 140, 160), 1.5f);
            arrowPen.StartCap = LineCap.Round;
            arrowPen.EndCap = LineCap.Round;
            g.DrawLine(arrowPen, arrowX, arrowY, arrowX + 5, arrowY + 4);
            g.DrawLine(arrowPen, arrowX + 5, arrowY + 4, arrowX + 10, arrowY);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHover = true; Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHover = false; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_isOpen) ClosePopup();
                else OpenPopup();
            }
            base.OnMouseClick(e);
        }

        private void OpenPopup()
        {
            if (_items.Count == 0 || _isOpen) return;

            _isOpen = true;
            _hoverIndex = -1;
            Invalidate();

            int itemHeight = 30;
            int popupHeight = Math.Min(_items.Count * itemHeight + 4, 300);

            var listBox = new ListBox
            {
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = itemHeight,
                BackColor = DropBg,
                ForeColor = TextNormal,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                Font = this.Font,
                Dock = DockStyle.Fill
            };

            foreach (var item in _items) listBox.Items.Add(item);
            if (_selectedIndex >= 0) listBox.SelectedIndex = _selectedIndex;

            listBox.DrawItem += ListBox_DrawItem;
            listBox.MouseMove += ListBox_MouseMove;

            listBox.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                int idx = listBox.IndexFromPoint(e.Location);
                if (idx >= 0 && idx < _items.Count)
                {
                    _selectedIndex = idx;
                    Invalidate();
                    ClosePopup();
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            };

            var screenPt = this.PointToScreen(new Point(0, Height));

            var popup = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = screenPt,
                Size = new Size(this.Width, popupHeight),
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                BackColor = DropBg,
                TopMost = true
            };

            popup.Controls.Add(listBox);
            popup.Deactivate += (s, e) => ClosePopup();

            _popup = popup;
            _listBox = listBox;

            popup.Show(this.FindForm()!);
            listBox.Focus();
        }

        private void ClosePopup()
        {
            if (_isClosingPopup) return;
            _isClosingPopup = true;

            try
            {
                _isOpen = false;
                Invalidate();

                var popup = _popup;
                _popup = null;
                _listBox = null;

                if (popup != null)
                {
                    popup.Close();
                    popup.Dispose();
                }
            }
            finally
            {
                _isClosingPopup = false;
            }
        }

        private void ListBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (sender is not ListBox lb) return;
            int idx = lb.IndexFromPoint(e.Location);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                lb.Invalidate();
            }
        }

        private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isHovered = e.Index == _hoverIndex;
            bool isSelected = e.Index == _selectedIndex;

            Color bg = isHovered ? DropHover : DropBg;
            Color textCol = isSelected ? Accent : TextNormal;

            using (var bgBrush = new SolidBrush(bg))
                g.FillRectangle(bgBrush, e.Bounds);

            string text = _items[e.Index];
            var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
            TextRenderer.DrawText(g, text, Font, textRect, textCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (isSelected)
            {
                int checkX = e.Bounds.Right - 24;
                int checkY = e.Bounds.Y + e.Bounds.Height / 2;
                using var checkPen = new Pen(Accent, 1.8f);
                checkPen.StartCap = LineCap.Round;
                checkPen.EndCap = LineCap.Round;
                g.DrawLine(checkPen, checkX, checkY, checkX + 4, checkY + 4);
                g.DrawLine(checkPen, checkX + 4, checkY + 4, checkX + 10, checkY - 4);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClosePopup();
            base.Dispose(disposing);
        }
    }
}
