using System.Windows;
using Xunit;

namespace SelectAI.Tests;

public class ToolbarPlacementTests
{
    private static (double x, double y) CalculateToolbarPosition(
        Rect selectionBounds,
        double screenWidth,
        double screenHeight,
        double tbWidth = 480,
        double tbHeight = 48)
    {
        // Horizontally center relative to selection
        double x = selectionBounds.Left + (selectionBounds.Width - tbWidth) / 2.0;

        // Clamp to virtual screen edges
        double minX = 16;
        double maxX = screenWidth - tbWidth - 16;
        x = Math.Max(minX, Math.Min(maxX, x));

        // Vertically place below selection if space permits, otherwise above
        double y;
        if (selectionBounds.Bottom + tbHeight + 16 <= screenHeight)
        {
            y = selectionBounds.Bottom + 12;
        }
        else
        {
            y = Math.Max(16, selectionBounds.Top - tbHeight - 12);
        }

        return (x, y);
    }

    [Theory]
    [InlineData(0, 0, 100, 100)]       // Top-left corner
    [InlineData(1820, 0, 100, 100)]    // Top-right corner
    [InlineData(0, 980, 100, 100)]     // Bottom-left corner
    [InlineData(1820, 980, 100, 100)]  // Bottom-right corner
    [InlineData(900, 500, 200, 200)]   // Center
    public void CalculateToolbarPosition_NeverExceedsScreenBoundaries(double selX, double selY, double selW, double selH)
    {
        const double screenW = 1920;
        const double screenH = 1080;
        const double tbW = 480;
        const double tbH = 48;

        var selection = new Rect(selX, selY, selW, selH);
        var (tbX, tbY) = CalculateToolbarPosition(selection, screenW, screenH, tbW, tbH);

        // Assert strictly inside visible screen
        Assert.True(tbX >= 16, $"Toolbar X {tbX} is less than margin 16");
        Assert.True(tbX + tbW <= screenW, $"Toolbar Right edge {tbX + tbW} exceeds screen width {screenW}");
        Assert.True(tbY >= 16, $"Toolbar Y {tbY} is less than margin 16");
        Assert.True(tbY + tbH <= screenH, $"Toolbar Bottom edge {tbY + tbH} exceeds screen height {screenH}");
    }
}
