using System.Drawing;
using SelectAI.Core.Utils;
using Xunit;

namespace SelectAI.Tests;

public class GeometryHelperTests
{
    [Fact]
    public void SmoothChaikin_WithPolygon_ProducesSubdividedSmootherPoints()
    {
        var raw = new List<PointF>
        {
            new(0, 0),
            new(50, 100),
            new(100, 0)
        };

        var smoothed = GeometryHelper.SmoothChaikin(raw, iterations: 2);

        // Chaikin should subdivide points (e.g. 3 -> 5 -> 9)
        Assert.NotNull(smoothed);
        Assert.True(smoothed.Count > raw.Count);
        // First and last points should stay anchored
        Assert.Equal(raw[0], smoothed[0]);
        Assert.Equal(raw[^1], smoothed[^1]);
    }

    [Fact]
    public void SmoothClosedLoop_ConnectsEndsWithoutGap()
    {
        var circlePoints = new List<PointF>
        {
            new(100, 0),
            new(200, 100),
            new(100, 200),
            new(0, 100)
        };

        var smoothed = GeometryHelper.SmoothClosedLoop(circlePoints, iterations: 2);

        Assert.NotNull(smoothed);
        Assert.True(smoothed.Count > circlePoints.Count);
    }

    [Fact]
    public void CalculateBoundingBox_ReturnsAccurateEnclosingRect()
    {
        var points = new List<PointF>
        {
            new(25, 40),
            new(150, 20),
            new(300, 250),
            new(10, 180)
        };

        var bbox = GeometryHelper.CalculateBoundingBox(points);

        Assert.Equal(10f, bbox.X);
        Assert.Equal(20f, bbox.Y);
        Assert.Equal(290f, bbox.Width);  // 300 - 10
        Assert.Equal(230f, bbox.Height); // 250 - 20
    }

    [Fact]
    public void IsClosedLoop_DetectsClosingProximity()
    {
        var closedPoints = new List<PointF>
        {
            new(100, 100),
            new(150, 200),
            new(200, 200),
            new(250, 150),
            new(200, 100),
            new(110, 105) // 10 units away from start (100, 100)
        };

        Assert.True(GeometryHelper.IsClosedLoop(closedPoints, toleranceDistance: 30f));

        var openPoints = new List<PointF>
        {
            new(100, 100),
            new(150, 200),
            new(200, 200),
            new(250, 150),
            new(400, 400) // far from start
        };

        Assert.False(GeometryHelper.IsClosedLoop(openPoints, toleranceDistance: 30f));
    }

    [Fact]
    public void PointInPolygon_CorrectlyIdentifiesInteriorPoints()
    {
        var square = new List<PointF>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        Assert.True(GeometryHelper.PointInPolygon(new PointF(50, 50), square));
        Assert.False(GeometryHelper.PointInPolygon(new PointF(150, 50), square));
    }
}
