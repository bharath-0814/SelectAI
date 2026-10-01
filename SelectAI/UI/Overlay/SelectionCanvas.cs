using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SelectAI.Core.Enums;
using SelectAI.Core.Utils;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace SelectAI.UI.Overlay;

public sealed class SelectionCanvas : FrameworkElement
{
    private readonly List<PointF> _rawPoints = new();
    private List<PointF> _smoothedPoints = new();
    private SelectAI.Core.Enums.SelectionMode _currentMode = SelectAI.Core.Enums.SelectionMode.Freeform;
    private bool _isDrawing = false;
    private bool _selectionCompleted = false;

    // Rectangle mode tracking
    private Point _rectStart;
    private Point _rectCurrent;

    // Glowing animation phase (0.0 to 1.0)
    private double _glowPhase = 0;
    private bool _animationRunning = false;

    // Drawing pens & brushes
    private static readonly SolidColorBrush DimMaskBrush = new(Color.FromArgb(115, 10, 14, 22)); // Subtle dark overlay
    private static readonly SolidColorBrush BloomBrush = new(Color.FromArgb(50, 0, 212, 255));   // Wide neon bloom
    private static readonly SolidColorBrush MidGlowBrush = new(Color.FromArgb(160, 0, 240, 255)); // Mid-range laser
    private static readonly SolidColorBrush CoreStrokeBrush = new(Color.FromArgb(255, 255, 255, 255)); // Pure luminous white core

    private static readonly Pen OuterBloomPen;
    private static readonly Pen MidGlowPen;
    private static readonly Pen CoreLaserPen;
    private static readonly Pen RectBorderPen;

    static SelectionCanvas()
    {
        DimMaskBrush.Freeze();
        BloomBrush.Freeze();
        MidGlowBrush.Freeze();
        CoreStrokeBrush.Freeze();

        OuterBloomPen = new Pen(BloomBrush, 12)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        OuterBloomPen.Freeze();

        MidGlowPen = new Pen(MidGlowBrush, 5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        MidGlowPen.Freeze();

        CoreLaserPen = new Pen(CoreStrokeBrush, 2.2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        CoreLaserPen.Freeze();

        RectBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 212, 255)), 2);
        RectBorderPen.Freeze();
    }

    public event EventHandler<List<PointF>>? SelectionCompleted;

    public SelectAI.Core.Enums.SelectionMode Mode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            ResetSelection();
        }
    }

    public bool IsSelecting => _isDrawing;
    public bool HasCompletedSelection => _selectionCompleted;
    public IReadOnlyList<PointF> CurrentPoints => _smoothedPoints;
    public Rect CurrentBoundingBox { get; private set; }

    public SelectionCanvas()
    {
        Focusable = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_animationRunning)
        {
            _animationRunning = true;
            CompositionTarget.Rendering += OnRenderingFrame;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_animationRunning)
        {
            _animationRunning = false;
            CompositionTarget.Rendering -= OnRenderingFrame;
        }
    }

    private void OnRenderingFrame(object? sender, EventArgs e)
    {
        // Animate trailing glow phase continuously while drawing or after selection
        _glowPhase = (_glowPhase + 0.03) % 1.0;

        if (_isDrawing || _selectionCompleted)
        {
            InvalidateVisual();
        }
    }

    public void ResetSelection()
    {
        _rawPoints.Clear();
        _smoothedPoints.Clear();
        _isDrawing = false;
        _selectionCompleted = false;
        CurrentBoundingBox = Rect.Empty;
        InvalidateVisual();
    }

    public void HandleMouseDown(Point pos)
    {
        _isDrawing = true;
        _selectionCompleted = false;
        _rawPoints.Clear();
        _smoothedPoints.Clear();

        if (_currentMode == SelectionMode.Freeform)
        {
            var p = new PointF((float)pos.X, (float)pos.Y);
            _rawPoints.Add(p);
            _smoothedPoints.Add(p);
        }
        else
        {
            _rectStart = pos;
            _rectCurrent = pos;
        }

        InvalidateVisual();
    }

    public void HandleMouseMove(Point pos)
    {
        if (!_isDrawing) return;

        if (_currentMode == SelectionMode.Freeform)
        {
            var currentP = new PointF((float)pos.X, (float)pos.Y);

            // Filter points closer than 3 pixels to prevent clustering
            if (_rawPoints.Count == 0 || GeometryHelper.Distance(_rawPoints[^1], currentP) >= 3.5f)
            {
                _rawPoints.Add(currentP);

                // Real-time smoothing using corner-cutting
                if (_rawPoints.Count >= 3)
                {
                    _smoothedPoints = GeometryHelper.SmoothChaikin(_rawPoints, iterations: 2);
                }
                else
                {
                    _smoothedPoints = new List<PointF>(_rawPoints);
                }
            }
        }
        else
        {
            _rectCurrent = pos;
        }

        InvalidateVisual();
    }

    public void HandleMouseUp(Point pos)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        if (_currentMode == SelectionMode.Freeform)
        {
            if (_rawPoints.Count < 4)
            {
                ResetSelection();
                return;
            }

            // Final smoothing pass
            bool isClosed = GeometryHelper.IsClosedLoop(_rawPoints, toleranceDistance: 60f);
            if (isClosed)
            {
                _smoothedPoints = GeometryHelper.SmoothClosedLoop(_rawPoints, iterations: 3);
            }
            else
            {
                // Auto-close loop if points form an enclosing gesture
                _smoothedPoints = GeometryHelper.SmoothChaikin(_rawPoints, iterations: 3);
            }

            var box = GeometryHelper.CalculateBoundingBox(_smoothedPoints);
            if (box.Width < 10 || box.Height < 10)
            {
                ResetSelection();
                return;
            }

            CurrentBoundingBox = new Rect(box.X, box.Y, box.Width, box.Height);
            _selectionCompleted = true;
            SelectionCompleted?.Invoke(this, _smoothedPoints);
        }
        else
        {
            // Rectangle / Text / Image mode
            double x = Math.Min(_rectStart.X, _rectCurrent.X);
            double y = Math.Min(_rectStart.Y, _rectCurrent.Y);
            double w = Math.Abs(_rectStart.X - _rectCurrent.X);
            double h = Math.Abs(_rectStart.Y - _rectCurrent.Y);

            if (w < 10 || h < 10)
            {
                ResetSelection();
                return;
            }

            CurrentBoundingBox = new Rect(x, y, w, h);
            _smoothedPoints = new List<PointF>
            {
                new((float)x, (float)y),
                new((float)(x + w), (float)y),
                new((float)(x + w), (float)(y + h)),
                new((float)x, (float)(y + h))
            };

            _selectionCompleted = true;
            SelectionCompleted?.Invoke(this, _smoothedPoints);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth > 0 ? ActualWidth : SystemParameters.VirtualScreenWidth;
        double height = ActualHeight > 0 ? ActualHeight : SystemParameters.VirtualScreenHeight;

        var fullScreenGeom = new RectangleGeometry(new Rect(0, 0, width, height));

        // 1. Compute Cutout Geometry
        Geometry? cutoutGeom = null;

        if (_currentMode == SelectionMode.Freeform && _smoothedPoints.Count >= 3)
        {
            var pathGeom = CreatePathGeometry(_smoothedPoints, isClosed: _selectionCompleted);
            cutoutGeom = pathGeom;
        }
        else if (_currentMode != SelectionMode.Freeform && (_isDrawing || _selectionCompleted))
        {
            Rect rect;
            if (_isDrawing)
            {
                double rx = Math.Min(_rectStart.X, _rectCurrent.X);
                double ry = Math.Min(_rectStart.Y, _rectCurrent.Y);
                double rw = Math.Max(1, Math.Abs(_rectStart.X - _rectCurrent.X));
                double rh = Math.Max(1, Math.Abs(_rectStart.Y - _rectCurrent.Y));
                rect = new Rect(rx, ry, rw, rh);
            }
            else
            {
                rect = CurrentBoundingBox;
            }

            cutoutGeom = new RectangleGeometry(rect, 8, 8);
        }

        // 2. Draw Dimmed Mask with Cutout
        if (cutoutGeom != null && _selectionCompleted)
        {
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullScreenGeom, cutoutGeom);
            dc.DrawGeometry(DimMaskBrush, null, combined);
        }
        else
        {
            dc.DrawGeometry(DimMaskBrush, null, fullScreenGeom);
        }

        // 3. Render Strokes and Glow Effects
        if (_currentMode == SelectionMode.Freeform && _smoothedPoints.Count >= 2)
        {
            var strokeGeom = CreatePathGeometry(_smoothedPoints, isClosed: _selectionCompleted);

            // Layer 1: Wide neon bloom
            dc.DrawGeometry(null, OuterBloomPen, strokeGeom);

            // Layer 2: Mid-intensity electric cyan
            dc.DrawGeometry(null, MidGlowPen, strokeGeom);

            // Layer 3: Razor-sharp white laser core
            dc.DrawGeometry(null, CoreLaserPen, strokeGeom);

            // Layer 4: Animated Leading Particle Head (While user is drawing)
            if (_isDrawing && _smoothedPoints.Count > 0)
            {
                var head = _smoothedPoints[^1];
                var headPoint = new Point(head.X, head.Y);

                // Radiant halo
                var radialBrush = new RadialGradientBrush
                {
                    Center = headPoint,
                    GradientOrigin = headPoint,
                    RadiusX = 14,
                    RadiusY = 14,
                    MappingMode = BrushMappingMode.Absolute
                };
                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.0));
                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 0, 240, 255), 0.3));
                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 180, 255), 1.0));

                dc.DrawEllipse(radialBrush, null, headPoint, 14, 14);
                dc.DrawEllipse(Brushes.White, null, headPoint, 3.5, 3.5);
            }
        }
        else if (_currentMode != SelectionMode.Freeform && (_isDrawing || _selectionCompleted))
        {
            Rect rect = _isDrawing
                ? new Rect(Math.Min(_rectStart.X, _rectCurrent.X),
                           Math.Min(_rectStart.Y, _rectCurrent.Y),
                           Math.Max(1, Math.Abs(_rectStart.X - _rectCurrent.X)),
                           Math.Max(1, Math.Abs(_rectStart.Y - _rectCurrent.Y)))
                : CurrentBoundingBox;

            // Outer soft glow
            var rectGeom = new RectangleGeometry(rect, 8, 8);
            dc.DrawGeometry(null, OuterBloomPen, rectGeom);
            dc.DrawGeometry(null, RectBorderPen, rectGeom);

            // Corner Accents (modern camera/viewfinder brackets)
            DrawCornerBrackets(dc, rect);
        }
    }

    private void DrawCornerBrackets(DrawingContext dc, Rect r)
    {
        double len = Math.Min(16, Math.Min(r.Width / 3, r.Height / 3));
        var bracketPen = new Pen(Brushes.White, 2.5);
        bracketPen.Freeze();

        // Top-left
        dc.DrawLine(bracketPen, new Point(r.Left - 1, r.Top + len), new Point(r.Left - 1, r.Top - 1));
        dc.DrawLine(bracketPen, new Point(r.Left - 1, r.Top - 1), new Point(r.Left + len, r.Top - 1));

        // Top-right
        dc.DrawLine(bracketPen, new Point(r.Right + 1 - len, r.Top - 1), new Point(r.Right + 1, r.Top - 1));
        dc.DrawLine(bracketPen, new Point(r.Right + 1, r.Top - 1), new Point(r.Right + 1, r.Top + len));

        // Bottom-left
        dc.DrawLine(bracketPen, new Point(r.Left - 1, r.Bottom - len), new Point(r.Left - 1, r.Bottom + 1));
        dc.DrawLine(bracketPen, new Point(r.Left - 1, r.Bottom + 1), new Point(r.Left + len, r.Bottom + 1));

        // Bottom-right
        dc.DrawLine(bracketPen, new Point(r.Right + 1 - len, r.Bottom + 1), new Point(r.Right + 1, r.Bottom + 1));
        dc.DrawLine(bracketPen, new Point(r.Right + 1, r.Bottom + 1), new Point(r.Right + 1, r.Bottom + 1 - len));
    }

    private static PathGeometry CreatePathGeometry(IReadOnlyList<PointF> points, bool isClosed)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(points[0].X, points[0].Y),
            IsClosed = isClosed,
            IsFilled = isClosed
        };

        var polyLine = new PolyLineSegment();
        for (int i = 1; i < points.Count; i++)
        {
            polyLine.Points.Add(new Point(points[i].X, points[i].Y));
        }

        figure.Segments.Add(polyLine);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
