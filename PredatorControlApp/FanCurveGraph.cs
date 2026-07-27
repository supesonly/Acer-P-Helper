using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class FanCurveGraph : Control
    {
        private static readonly List<Point> _defaultPoints = new()
        {
            new Point(30, 10),
            new Point(45, 15),
            new Point(55, 30),
            new Point(65, 50),
            new Point(72, 65),
            new Point(80, 80),
            new Point(88, 92),
            new Point(95, 100)
        };

        private static readonly Color BackgroundColor = Color.FromArgb(28, 28, 32);
        private static readonly Color GridColor = Color.FromArgb(45, 45, 50);
        private static readonly Color LabelColor = Color.FromArgb(100, 100, 110);

        private const int PadLeft = 45;
        private const int PadRight = 15;
        private const int PadTop = 30;
        private const int PadBottom = 30;

        private const int TempMin = 30;
        private const int TempMax = 100;
        private const int SpeedMin = 0;
        private const int SpeedMax = 100;
        private const int MinTempGap = 5;

        private const int PointRadius = 5;
        private const int PointRadiusHover = 7;
        private const int HitTestRadius = 10;

        private List<Point> _points;
        private Color _curveColor = Color.FromArgb(0, 180, 255);
        private int _currentTemp;
        private string _fanLabel = "FAN CURVE";
        private int _dragIndex = -1;
        private int _hoverIndex = -1;

        public event EventHandler? CurveChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color CurveColor
        {
            get => _curveColor;
            set { _curveColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CurrentTemp
        {
            get => _currentTemp;
            set { _currentTemp = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string FanLabel
        {
            get => _fanLabel;
            set { _fanLabel = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<Point> Points
        {
            get => _points;
            set
            {
                _points = Normalize(value);
                Invalidate();
            }
        }

        public static List<Point> Normalize(List<Point>? src)
        {
            if (src == null || src.Count < 2) return new List<Point>(_defaultPoints);

            var pts = src
                .Select(p => new Point(Math.Clamp(p.X, TempMin, TempMax), Math.Clamp(p.Y, SpeedMin, SpeedMax)))
                .OrderBy(p => p.X)
                .ToList();

            pts[0] = new Point(TempMin, pts[0].Y);
            pts[^1] = new Point(TempMax, pts[^1].Y);
            return pts;
        }

        public List<Point> DefaultPoints => new(_defaultPoints);

        public FanCurveGraph()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Size = new Size(420, 220);
            _points = new List<Point>(_defaultPoints);
            Cursor = Cursors.Default;
        }

        private RectangleF GraphArea => new(
            PadLeft, PadTop,
            Width - PadLeft - PadRight,
            Height - PadTop - PadBottom);

        private float TempToX(int temp)
        {
            var g = GraphArea;
            float frac = (float)(temp - TempMin) / (TempMax - TempMin);
            return g.Left + frac * g.Width;
        }

        private float SpeedToY(int speed)
        {
            var g = GraphArea;
            float frac = (float)(speed - SpeedMin) / (SpeedMax - SpeedMin);
            return g.Bottom - frac * g.Height;        
        }

        private int XToTemp(float x)
        {
            var g = GraphArea;
            float frac = (x - g.Left) / g.Width;
            return TempMin + (int)Math.Round(frac * (TempMax - TempMin));
        }

        private int YToSpeed(float y)
        {
            var g = GraphArea;
            float frac = (g.Bottom - y) / g.Height;
            return SpeedMin + (int)Math.Round(frac * (SpeedMax - SpeedMin));
        }

        private PointF CurvePointToPixel(Point cp) => new(TempToX(cp.X), SpeedToY(cp.Y));

        public int InterpolateSpeed(int temperature)
        {
            if (_points.Count == 0) return 0;
            if (temperature <= _points[0].X) return _points[0].Y;
            if (temperature >= _points[^1].X) return _points[^1].Y;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                if (temperature >= _points[i].X && temperature <= _points[i + 1].X)
                {
                    float span = _points[i + 1].X - _points[i].X;
                    if (span == 0) return _points[i].Y;
                    float t = (temperature - _points[i].X) / span;
                    return (int)Math.Round(_points[i].Y + t * (_points[i + 1].Y - _points[i].Y));
                }
            }
            return _points[^1].Y;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackgroundColor);

            var area = GraphArea;

            DrawGrid(g, area);
            DrawAxisLabels(g, area);
            DrawTitle(g, area);
            DrawStatus(g, area);
            DrawCurveFill(g, area);
            DrawCurveLine(g);
            DrawCrosshair(g, area);
            DrawControlPoints(g);
            DrawDragTooltip(g);
        }

        private void DrawGrid(Graphics g, RectangleF area)
        {
            using var pen = new Pen(GridColor, 1f);

            for (int temp = TempMin; temp <= TempMax; temp += 10)
            {
                float x = TempToX(temp);
                g.DrawLine(pen, x, area.Top, x, area.Bottom);
            }

            for (int speed = SpeedMin; speed <= SpeedMax; speed += 20)
            {
                float y = SpeedToY(speed);
                g.DrawLine(pen, area.Left, y, area.Right, y);
            }
        }

        private void DrawAxisLabels(Graphics g, RectangleF area)
        {
            using var font = new Font("Segoe UI", 7.5f);
            using var brush = new SolidBrush(LabelColor);
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            using var sfRight = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };

            for (int temp = TempMin; temp <= TempMax; temp += 10)
            {
                float x = TempToX(temp);
                g.DrawString($"{temp}°", font, brush, x, area.Bottom + 4, sfCenter);
            }

            for (int speed = SpeedMin; speed <= SpeedMax; speed += 20)
            {
                float y = SpeedToY(speed);
                g.DrawString($"{speed}%", font, brush, area.Left - 4, y, sfRight);
            }
        }

        private void DrawTitle(Graphics g, RectangleF area)
        {
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var brush = new SolidBrush(_curveColor);
            g.DrawString(_fanLabel, font, brush, area.Left, area.Top - 22);
        }

        private void DrawStatus(Graphics g, RectangleF area)
        {
            int speed = InterpolateSpeed(_currentTemp);
            string status = $"{_currentTemp}°C → {speed}%";

            using var font = new Font("Segoe UI", 8f);
            using var brush = new SolidBrush(_curveColor);
            using var sf = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString(status, font, brush, area.Right, area.Top - 22, sf);
        }

        private void DrawCurveFill(Graphics g, RectangleF area)
        {
            if (_points.Count < 2) return;

            using var path = new GraphicsPath();
            var pixels = _points.Select(CurvePointToPixel).ToArray();

            path.AddLines(pixels);
            path.AddLine(pixels[^1].X, pixels[^1].Y, pixels[^1].X, area.Bottom);
            path.AddLine(pixels[^1].X, area.Bottom, pixels[0].X, area.Bottom);
            path.CloseFigure();

            using var fillBrush = new LinearGradientBrush(
                new PointF(0, area.Top),
                new PointF(0, area.Bottom),
                Color.FromArgb(25, _curveColor),
                Color.FromArgb(5, _curveColor));

            g.FillPath(fillBrush, path);
        }

        private void DrawCurveLine(Graphics g)
        {
            if (_points.Count < 2) return;

            var pixels = _points.Select(CurvePointToPixel).ToArray();

            using var pen = new Pen(_curveColor, 2f);
            g.DrawLines(pen, pixels);
        }

        private void DrawCrosshair(Graphics g, RectangleF area)
        {
            if (_currentTemp < TempMin || _currentTemp > TempMax) return;

            float x = TempToX(_currentTemp);
            int speed = InterpolateSpeed(_currentTemp);
            float y = SpeedToY(speed);

            using var pen = new Pen(Color.FromArgb(100, _curveColor), 1f)
            {
                DashStyle = DashStyle.Dash
            };

            g.DrawLine(pen, x, area.Top, x, area.Bottom);
            g.DrawLine(pen, area.Left, y, area.Right, y);

            const float d = 4f;
            PointF[] diamond =
            {
                new(x, y - d),
                new(x + d, y),
                new(x, y + d),
                new(x - d, y)
            };
            using var fill = new SolidBrush(_curveColor);
            g.FillPolygon(fill, diamond);
        }

        private void DrawControlPoints(Graphics g)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                var px = CurvePointToPixel(_points[i]);
                int r = (i == _hoverIndex || i == _dragIndex) ? PointRadiusHover : PointRadius;

                using var fill = new SolidBrush(_curveColor);
                g.FillEllipse(fill, px.X - r, px.Y - r, r * 2, r * 2);

                using var border = new Pen(Color.White, 1f);
                g.DrawEllipse(border, px.X - r, px.Y - r, r * 2, r * 2);
            }
        }

        private void DrawDragTooltip(Graphics g)
        {
            if (_dragIndex < 0 || _dragIndex >= _points.Count) return;

            var pt = _points[_dragIndex];
            var px = CurvePointToPixel(pt);
            string text = $"{pt.X}°C, {pt.Y}%";

            using var font = new Font("Segoe UI", 7.5f);
            var sz = g.MeasureString(text, font);

            float tx = px.X - sz.Width / 2;
            float ty = px.Y - PointRadiusHover - sz.Height - 6;

            tx = Math.Max(PadLeft, Math.Min(tx, Width - PadRight - sz.Width));
            ty = Math.Max(2, ty);

            using var bgBrush = new SolidBrush(Color.FromArgb(220, 22, 22, 26));
            using var fgBrush = new SolidBrush(_curveColor);
            g.FillRectangle(bgBrush, tx - 3, ty - 1, sz.Width + 6, sz.Height + 2);
            g.DrawString(text, font, fgBrush, tx, ty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = HitTestPoint(e.Location);
                if (index >= 0)
                {
                    _dragIndex = index;
                    Capture = true;
                    Cursor = Cursors.Hand;
                    Invalidate();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                UpdateDraggedPoint(e.Location);
            }
            else
            {
                int newHover = HitTestPoint(e.Location);
                if (newHover != _hoverIndex)
                {
                    _hoverIndex = newHover;
                    Cursor = _hoverIndex >= 0 ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                Capture = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex >= 0)
            {
                _hoverIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        private int HitTestPoint(Point mousePos)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                var px = CurvePointToPixel(_points[i]);
                float dx = mousePos.X - px.X;
                float dy = mousePos.Y - px.Y;
                if (dx * dx + dy * dy <= HitTestRadius * HitTestRadius)
                    return i;
            }
            return -1;
        }

        private void UpdateDraggedPoint(Point mousePos)
        {
            int temp = XToTemp(mousePos.X);
            int speed = YToSpeed(mousePos.Y);

            speed = Math.Clamp(speed, SpeedMin, SpeedMax);

            if (_dragIndex == 0)
            {
                temp = TempMin;
            }
            else if (_dragIndex == _points.Count - 1)
            {
                temp = TempMax;
            }
            else
            {
                temp = Math.Clamp(temp, TempMin, TempMax);

                int lo = _points[_dragIndex - 1].X + MinTempGap;
                int hi = _points[_dragIndex + 1].X - MinTempGap;
                temp = lo > hi ? (lo + hi) / 2 : Math.Clamp(temp, lo, hi);
            }

            var updated = new Point(temp, speed);
            if (_points[_dragIndex] != updated)
            {
                _points[_dragIndex] = updated;
                CurveChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }
}
