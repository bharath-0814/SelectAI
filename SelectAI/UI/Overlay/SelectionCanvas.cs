using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SelectAI.Core.Enums;
using SelectAI.Core.Utils;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace SelectAI.UI.Overlay;

public sealed class SelectionCanvas : FrameworkElement
{
    private enum DragHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly List<PointF> _rawPoints = new();
    private List<PointF> _smoothedPoints = new();
    private SelectAI.Core.Enums.SelectionMode _currentMode = SelectAI.Core.Enums.SelectionMode.Freeform;
    private bool _isDrawing = false;
    private bool _selectionCompleted = false;

    // Corner handle dragging (resizing)
    private DragHandle _activeHandle = DragHandle.None;
    private bool _isDraggingHandle = false;

    // Box panning (moving the entire rectangle across the screen)
    private bool _isDraggingBox = false;

    private Point _dragStartPos;
    private Rect _initialBox;

    // Rectangle mode tracking
    private Point _rectStart;
    private Point _rectCurrent;

    // Glowing animation phase (0.0 to 1.0)
    private double _glowPhase = 0;
    private bool _animationRunning = false;

    // Drawing pens & brushes
    private static readonly SolidColorBrush DimMaskBrush = new(Color.FromArgb(160, 0, 0, 0)); // Darker, elegant overlay
    private static readonly SolidColorBrush BloomBrush = new(Color.FromArgb(60, 0, 212, 255));   // Wide neon bloom
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

        OuterBloomPen = new Pen(BloomBrush, 14)
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

        // Exact Galaxy AI Aurora Gradient (Cyan -> Mint Green -> Pink -> Violet)
        var auroraGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        auroraGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 240, 255), 0.0));
        auroraGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 52, 211, 153), 0.35));
        auroraGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 236, 72, 153), 0.70));
        auroraGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 139, 92, 246), 1.0));
        auroraGradient.Freeze();

        RectBorderPen = new Pen(auroraGradient, 4.0);
        RectBorderPen.Freeze();
    }

    public event EventHandler<List<PointF>>? SelectionCompleted;
    public event EventHandler<Rect>? BoundingBoxChanged;

    public SelectAI.Core.Enums.SelectionMode Mode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            ResetSelection();
        }
    }

    public bool IsSelecting => _isDrawing || _isDraggingHandle || _isDraggingBox;
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
        _glowPhase = (_glowPhase + 0.03) % 1.0;

        if (_isDrawing || _selectionCompleted || _isDraggingHandle || _isDraggingBox)
        {
            InvalidateVisual();
        }
    }

    public void ResetSelection()
    {
        _rawPoints.Clear();
        _smoothedPoints.Clear();
        _isDrawing = false;
        _isDraggingHandle = false;
        _isDraggingBox = false;
        _activeHandle = DragHandle.None;
        _selectionCompleted = false;
        CurrentBoundingBox = Rect.Empty;
        Cursor = Cursors.Cross;
        InvalidateVisual();
    }

    public void HandleMouseDown(Point pos)
    {
        if (_selectionCompleted && CurrentBoundingBox != Rect.Empty)
        {
            // 1. Check if user clicked a corner handle to resize
            var hitHandle = HitTestHandle(pos, CurrentBoundingBox);
            if (hitHandle != DragHandle.None)
            {
                _activeHandle = hitHandle;
                _isDraggingHandle = true;
                _dragStartPos = pos;
                _initialBox = CurrentBoundingBox;
                return;
            }

            // 2. Check if user clicked INSIDE the bounding box to move/drag the entire rectangle
            if (CurrentBoundingBox.Contains(pos))
            {
                _isDraggingBox = true;
                _dragStartPos = pos;
                _initialBox = CurrentBoundingBox;
                return;
            }

            // 3. User clicked OUTSIDE the bounding box -> seamlessly start a fresh circle (redraw)!
            ResetSelection();
        }

        // 4. Begin new drawing trace
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
        // A. Resizing bounding box via corner handles
        if (_isDraggingHandle)
        {
            UpdateHandleDrag(pos);
            return;
        }

        // B. Moving / dragging the entire bounding box across the screen
        if (_isDraggingBox)
        {
            double dx = pos.X - _dragStartPos.X;
            double dy = pos.Y - _dragStartPos.Y;

            double screenW = ActualWidth > 0 ? ActualWidth : SystemParameters.VirtualScreenWidth;
            double screenH = ActualHeight > 0 ? ActualHeight : SystemParameters.VirtualScreenHeight;

            double newX = Math.Max(0, Math.Min(screenW - _initialBox.Width, _initialBox.X + dx));
            double newY = Math.Max(0, Math.Min(screenH - _initialBox.Height, _initialBox.Y + dy));

            CurrentBoundingBox = new Rect(newX, newY, _initialBox.Width, _initialBox.Height);
            BoundingBoxChanged?.Invoke(this, CurrentBoundingBox);
            InvalidateVisual();
            return;
        }

        // C. Active drawing of circle or rectangle
        if (_isDrawing)
        {
            if (_currentMode == SelectionMode.Freeform)
            {
                var currentP = new PointF((float)pos.X, (float)pos.Y);

                if (_rawPoints.Count == 0 || GeometryHelper.Distance(_rawPoints[^1], currentP) >= 3.5f)
                {
                    _rawPoints.Add(currentP);

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
            return;
        }

        // D. Hover cursor updates
        if (_selectionCompleted && CurrentBoundingBox != Rect.Empty && !_isDrawing && !_isDraggingHandle && !_isDraggingBox)
        {
            var handle = HitTestHandle(pos, CurrentBoundingBox);
            if (handle == DragHandle.TopLeft || handle == DragHandle.BottomRight)
            {
                Cursor = Cursors.SizeNWSE;
            }
            else if (handle == DragHandle.TopRight || handle == DragHandle.BottomLeft)
            {
                Cursor = Cursors.SizeNESW;
            }
            else if (CurrentBoundingBox.Contains(pos))
            {
                // Hovering over the inside of the selected area allows moving it anywhere!
                Cursor = Cursors.SizeAll;
            }
            else
            {
                Cursor = Cursors.Cross;
            }
        }
    }

    public void HandleMouseUp(Point pos)
    {
        // A. Finished resizing corner handle
        if (_isDraggingHandle)
        {
            _isDraggingHandle = false;
            _activeHandle = DragHandle.None;

            _smoothedPoints = new List<PointF>
            {
                new((float)CurrentBoundingBox.Left, (float)CurrentBoundingBox.Top),
                new((float)CurrentBoundingBox.Right, (float)CurrentBoundingBox.Top),
                new((float)CurrentBoundingBox.Right, (float)CurrentBoundingBox.Bottom),
                new((float)CurrentBoundingBox.Left, (float)CurrentBoundingBox.Bottom)
            };

            SelectionCompleted?.Invoke(this, _smoothedPoints);
            InvalidateVisual();
            return;
        }

        // B. Finished moving entire box
        if (_isDraggingBox)
        {
            _isDraggingBox = false;

            _smoothedPoints = new List<PointF>
            {
                new((float)CurrentBoundingBox.Left, (float)CurrentBoundingBox.Top),
                new((float)CurrentBoundingBox.Right, (float)CurrentBoundingBox.Top),
                new((float)CurrentBoundingBox.Right, (float)CurrentBoundingBox.Bottom),
                new((float)CurrentBoundingBox.Left, (float)CurrentBoundingBox.Bottom)
            };

            SelectionCompleted?.Invoke(this, _smoothedPoints);
            InvalidateVisual();
            return;
        }

        if (!_isDrawing) return;
        _isDrawing = false;

        // C. Finished drawing circle
        if (_currentMode == SelectionMode.Freeform)
        {
            if (_rawPoints.Count < 4)
            {
                ResetSelection();
                return;
            }

            var box = GeometryHelper.CalculateBoundingBox(_smoothedPoints);
            if (box.Width < 12 || box.Height < 12)
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

            if (w < 12 || h < 12)
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

    private void UpdateHandleDrag(Point currentPos)
    {
        double left = _initialBox.Left;
        double top = _initialBox.Top;
        double right = _initialBox.Right;
        double bottom = _initialBox.Bottom;

        double dx = currentPos.X - _dragStartPos.X;
        double dy = currentPos.Y - _dragStartPos.Y;

        const double minSize = 24.0;

        switch (_activeHandle)
        {
            case DragHandle.TopLeft:
                left = Math.Min(right - minSize, _initialBox.Left + dx);
                top = Math.Min(bottom - minSize, _initialBox.Top + dy);
                break;
            case DragHandle.TopRight:
                right = Math.Max(left + minSize, _initialBox.Right + dx);
                top = Math.Min(bottom - minSize, _initialBox.Top + dy);
                break;
            case DragHandle.BottomLeft:
                left = Math.Min(right - minSize, _initialBox.Left + dx);
                bottom = Math.Max(top + minSize, _initialBox.Bottom + dy);
                break;
            case DragHandle.BottomRight:
                right = Math.Max(left + minSize, _initialBox.Right + dx);
                bottom = Math.Max(top + minSize, _initialBox.Bottom + dy);
                break;
        }

        CurrentBoundingBox = new Rect(left, top, Math.Max(minSize, right - left), Math.Max(minSize, bottom - top));
        BoundingBoxChanged?.Invoke(this, CurrentBoundingBox);
        InvalidateVisual();
    }

    private DragHandle HitTestHandle(Point pos, Rect r)
    {
        const double threshold = 18.0; // Comfort hit radius for easy mouse grab

        if (GetDistance(pos, new Point(r.Left, r.Top)) <= threshold) return DragHandle.TopLeft;
        if (GetDistance(pos, new Point(r.Right, r.Top)) <= threshold) return DragHandle.TopRight;
        if (GetDistance(pos, new Point(r.Left, r.Bottom)) <= threshold) return DragHandle.BottomLeft;
        if (GetDistance(pos, new Point(r.Right, r.Bottom)) <= threshold) return DragHandle.BottomRight;

        return DragHandle.None;
    }

    private static double GetDistance(Point p1, Point p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth > 0 ? ActualWidth : SystemParameters.VirtualScreenWidth;
        double height = ActualHeight > 0 ? ActualHeight : SystemParameters.VirtualScreenHeight;

        var fullScreenGeom = new RectangleGeometry(new Rect(0, 0, width, height));

        // 1. Compute Cutout Geometry
        Geometry? cutoutGeom = null;

        if (_isDrawing && _currentMode == SelectionMode.Freeform && _smoothedPoints.Count >= 3)
        {
            cutoutGeom = CreatePathGeometry(_smoothedPoints, isClosed: false);
        }
        else if (_isDrawing && _currentMode != SelectionMode.Freeform)
        {
            double rx = Math.Min(_rectStart.X, _rectCurrent.X);
            double ry = Math.Min(_rectStart.Y, _rectCurrent.Y);
            double rw = Math.Max(1, Math.Abs(_rectStart.X - _rectCurrent.X));
            double rh = Math.Max(1, Math.Abs(_rectStart.Y - _rectCurrent.Y));
            cutoutGeom = new RectangleGeometry(new Rect(rx, ry, rw, rh), 16, 16);
        }
        else if (_selectionCompleted)
        {
            cutoutGeom = new RectangleGeometry(CurrentBoundingBox, 16, 16);
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

        // 3. Render Active Stroke while Circling
        if (_currentMode == SelectionMode.Freeform && _smoothedPoints.Count >= 2 && !_selectionCompleted)
        {
            var strokeGeom = CreatePathGeometry(_smoothedPoints, isClosed: false);

            // Layer 1: Wide neon bloom
            dc.DrawGeometry(null, OuterBloomPen, strokeGeom);

            // Layer 2: Mid-intensity electric cyan
            dc.DrawGeometry(null, MidGlowPen, strokeGeom);

            // Layer 3: Razor-sharp white laser core
            dc.DrawGeometry(null, CoreLaserPen, strokeGeom);

            // Layer 4: Animated Leading Particle Head
            if (_isDrawing && _smoothedPoints.Count > 0)
            {
                var head = _smoothedPoints[^1];
                var headPoint = new Point(head.X, head.Y);

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
        else if (_selectionCompleted || (_currentMode != SelectionMode.Freeform && _isDrawing))
        {
            Rect rect = _isDrawing && _currentMode != SelectionMode.Freeform
                ? new Rect(Math.Min(_rectStart.X, _rectCurrent.X),
                           Math.Min(_rectStart.Y, _rectCurrent.Y),
                           Math.Max(1, Math.Abs(_rectStart.X - _rectCurrent.X)),
                           Math.Max(1, Math.Abs(_rectStart.Y - _rectCurrent.Y)))
                : CurrentBoundingBox;

            // Outer soft glow & rich aurora border (matching Image 2)
            var rectGeom = new RectangleGeometry(rect, 16, 16);
            dc.DrawGeometry(null, OuterBloomPen, rectGeom);
            dc.DrawGeometry(null, RectBorderPen, rectGeom);

            // Corner Accents (viewfinder brackets)
            DrawCornerBrackets(dc, rectGeom.Rect);

            // 4 Corner Drag Handles
            if (_selectionCompleted)
            {
                DrawCornerHandles(dc, rectGeom.Rect);
            }
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

    private void DrawCornerHandles(DrawingContext dc, Rect r)
    {
        var handleBrush = Brushes.White;
        var handleBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 240, 255)), 2);
        handleBorderPen.Freeze();

        Point[] corners = {
            new(r.Left, r.Top),
            new(r.Right, r.Top),
            new(r.Left, r.Bottom),
            new(r.Right, r.Bottom)
        };

        foreach (var pt in corners)
        {
            // Outer glowing ring
            dc.DrawEllipse(null, handleBorderPen, pt, 7.5, 7.5);
            // Solid crisp core
            dc.DrawEllipse(handleBrush, null, pt, 4.5, 4.5);
        }
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
