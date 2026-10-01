using System.Drawing;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Utils;

namespace SelectAI.Selection;

public sealed class SelectionEngine : ISelectionEngine
{
    private const float MinPointDistance = 4f;

    public List<PointF> FilterMicroJitter(IReadOnlyList<PointF> rawPoints)
    {
        if (rawPoints == null || rawPoints.Count < 2)
            return rawPoints?.ToList() ?? new List<PointF>();

        var filtered = new List<PointF> { rawPoints[0] };
        for (int i = 1; i < rawPoints.Count; i++)
        {
            var prev = filtered[^1];
            var current = rawPoints[i];

            if (GeometryHelper.Distance(prev, current) >= MinPointDistance)
            {
                filtered.Add(current);
            }
        }

        // Always keep the last point
        if (filtered[^1] != rawPoints[^1])
        {
            filtered.Add(rawPoints[^1]);
        }

        return filtered;
    }

    public List<PointF> SmoothPoints(IReadOnlyList<PointF> rawPoints)
    {
        var filtered = FilterMicroJitter(rawPoints);
        if (filtered.Count < 3)
            return filtered;

        bool isClosed = IsClosedLoop(filtered);
        if (isClosed)
        {
            return GeometryHelper.SmoothClosedLoop(filtered, iterations: 3);
        }

        return GeometryHelper.SmoothChaikin(filtered, iterations: 3);
    }

    public RectangleF CalculateBoundingBox(IReadOnlyList<PointF> points)
    {
        return GeometryHelper.CalculateBoundingBox(points);
    }

    public bool IsClosedLoop(IReadOnlyList<PointF> points, float toleranceDistance = 55f)
    {
        return GeometryHelper.IsClosedLoop(points, toleranceDistance);
    }
}
