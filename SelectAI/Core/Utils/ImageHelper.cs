using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SelectAI.Core.Utils;

public static class ImageHelper
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    public static byte[] ToPngBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static Bitmap CropRect(Bitmap source, Rectangle region)
    {
        // Clamp to source bounds
        int x = Math.Max(0, Math.Min(region.X, source.Width - 1));
        int y = Math.Max(0, Math.Min(region.Y, source.Height - 1));
        int w = Math.Max(1, Math.Min(region.Width, source.Width - x));
        int h = Math.Max(1, Math.Min(region.Height, source.Height - y));

        var cropRect = new Rectangle(x, y, w, h);
        var cropped = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(cropped))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, w, h), cropRect, GraphicsUnit.Pixel);
        }

        return cropped;
    }

    public static Bitmap CropPolygon(Bitmap source, IReadOnlyList<PointF> polygon, RectangleF boundingBox)
    {
        int x = (int)Math.Max(0, Math.Min(boundingBox.X, source.Width - 1));
        int y = (int)Math.Max(0, Math.Min(boundingBox.Y, source.Height - 1));
        int w = (int)Math.Max(1, Math.Min(boundingBox.Width, source.Width - x));
        int h = (int)Math.Max(1, Math.Min(boundingBox.Height, source.Height - y));

        var cropped = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(cropped))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            // Shift polygon relative to bounding box top-left
            var localPoints = polygon.Select(p => new PointF(p.X - x, p.Y - y)).ToArray();

            using var path = new GraphicsPath();
            if (localPoints.Length > 2)
            {
                path.AddPolygon(localPoints);
                g.SetClip(path);
            }

            g.DrawImage(source, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
        }

        return cropped;
    }
}
