using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;
using SelectAI.Core.Utils;

namespace SelectAI.Capture;

public sealed class ScreenCaptureService : IScreenCapture
{
    public CapturedScreen CaptureVirtualDesktop()
    {
        // Query user32 for virtual screen bounding all monitors
        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        var virtualBounds = new Rectangle(vx, vy, vw, vh);

        var fullBitmap = new Bitmap(
            virtualBounds.Width,
            virtualBounds.Height,
            PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(fullBitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CopyFromScreen(
                virtualBounds.X,
                virtualBounds.Y,
                0,
                0,
                virtualBounds.Size,
                CopyPixelOperation.SourceCopy);
        }

        var screen = new CapturedScreen(fullBitmap, virtualBounds);
        // Pre-create WPF bitmap source
        screen.CachedWpfBitmap = ImageHelper.ToBitmapSource(fullBitmap);
        return screen;
    }

    public Bitmap CropRegion(Bitmap source, RectangleF region)
    {
        var rect = new Rectangle(
            (int)Math.Round(region.X),
            (int)Math.Round(region.Y),
            (int)Math.Round(region.Width),
            (int)Math.Round(region.Height));

        return ImageHelper.CropRect(source, rect);
    }

    public Bitmap CropFreeform(Bitmap source, IReadOnlyList<PointF> polygon, RectangleF boundingBox)
    {
        return ImageHelper.CropPolygon(source, polygon, boundingBox);
    }
}
