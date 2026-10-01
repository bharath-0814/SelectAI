using System.Drawing;

namespace SelectAI.Core.Utils;

public static class GeometryHelper
{
    /// <summary>
    /// Smooths a list of points using Chaikin's corner-cutting algorithm.
    /// This removes all mouse jitter while preserving user's intended shape.
    /// </summary>
    public static List<PointF> SmoothChaikin(IReadOnlyList<PointF> points, int iterations = 3)
    {
        if (points == null || points.Count < 3)
        {
            return points?.ToList() ?? new List<PointF>();
        }

        var current = new List<PointF>(points);

        for (int it = 0; it < iterations; it++)
        {
            var next = new List<PointF>(current.Count * 2);
            next.Add(current[0]);

            for (int i = 0; i < current.Count - 1; i++)
            {
                var p0 = current[i];
                var p1 = current[i + 1];

                // Cut corners at 25% and 75%
                var q = new PointF(0.75f * p0.X + 0.25f * p1.X, 0.75f * p0.Y + 0.25f * p1.Y);
                var r = new PointF(0.25f * p0.X + 0.75f * p1.X, 0.25f * p0.Y + 0.75f * p1.Y);

                next.Add(q);
                next.Add(r);
            }

            next.Add(current[^1]);
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Smooths a closed polygon loop using circular Chaikin subdivision.
    /// </summary>
    public static List<PointF> SmoothClosedLoop(IReadOnlyList<PointF> points, int iterations = 3)
    {
        if (points == null || points.Count < 3)
        {
            return points?.ToList() ?? new List<PointF>();
        }

        var current = new List<PointF>(points);

        for (int it = 0; it < iterations; it++)
        {
            var next = new List<PointF>(current.Count * 2);
            int count = current.Count;

            for (int i = 0; i < count; i++)
            {
                var p0 = current[i];
                var p1 = current[(i + 1) % count];

                var q = new PointF(0.75f * p0.X + 0.25f * p1.X, 0.75f * p0.Y + 0.25f * p1.Y);
                var r = new PointF(0.25f * p0.X + 0.75f * p1.X, 0.25f * p0.Y + 0.75f * p1.Y);

                next.Add(q);
                next.Add(r);
            }

            current = next;
        }

        return current;
    }

    public static RectangleF CalculateBoundingBox(IReadOnlyList<PointF> points)
    {
        if (points == null || points.Count == 0)
        {
            return RectangleF.Empty;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new RectangleF(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
    }

    public static bool IsClosedLoop(IReadOnlyList<PointF> points, float toleranceDistance = 50f)
    {
        if (points == null || points.Count < 6)
        {
            return false;
        }

        var first = points[0];
        var last = points[^1];

        float dx = first.X - last.X;
        float dy = first.Y - last.Y;
        float distSq = dx * dx + dy * dy;

        return distSq <= (toleranceDistance * toleranceDistance);
    }

    public static float Distance(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static bool PointInPolygon(PointF point, IReadOnlyList<PointF> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }
}
