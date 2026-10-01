using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;
using SelectAI.Core.Utils;

namespace SelectAI.Capture;

public sealed class ScreenCaptureService : IScreenCapture
{
    public CapturedScreen CaptureVirtualDesktop()
    {
        // 1. Query user32 for virtual screen bounding all monitors
        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        // Fail-safe boundary detection
        if (vw <= 0 || vh <= 0)
        {
            try
            {
                var vs = SystemInformation.VirtualScreen;
                vx = vs.X;
                vy = vs.Y;
                vw = vs.Width;
                vh = vs.Height;
            }
            catch
            {
                vx = 0;
                vy = 0;
                vw = Math.Max(1920, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN));
                vh = Math.Max(1080, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN));
            }
        }

        var virtualBounds = new Rectangle(vx, vy, vw, vh);
        Bitmap? fullBitmap = null;

        // Tier 1: Direct Win32 GDI BitBlt
        try
        {
            if (TryCaptureWin32Gdi(virtualBounds, out var gdiBitmap) && gdiBitmap != null)
            {
                fullBitmap = gdiBitmap;
                AppLog.Info($"Screen captured successfully via Tier 1 Win32 GDI ({vw}x{vh})");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Tier 1 Win32 GDI capture failed: {ex.Message}");
        }

        // Tier 2: Standard Graphics.CopyFromScreen
        if (fullBitmap == null)
        {
            try
            {
                var bmp = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(
                        virtualBounds.X,
                        virtualBounds.Y,
                        0,
                        0,
                        virtualBounds.Size,
                        CopyPixelOperation.SourceCopy);
                }
                fullBitmap = bmp;
                AppLog.Info($"Screen captured successfully via Tier 2 CopyFromScreen ({vw}x{vh})");
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Tier 2 CopyFromScreen failed: {ex.Message}");
            }
        }

        // Tier 3: Per-Monitor Composite
        if (fullBitmap == null)
        {
            try
            {
                fullBitmap = CapturePerMonitor(virtualBounds);
                AppLog.Info($"Screen captured successfully via Tier 3 Per-Monitor ({vw}x{vh})");
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Tier 3 Per-Monitor capture failed: {ex.Message}");
            }
        }

        // Tier 4: Fail-safe Canvas (Never crash or throw!)
        if (fullBitmap == null)
        {
            AppLog.Warn("All screen capture tiers failed; generating fallback transparent canvas.");
            fullBitmap = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(fullBitmap))
            {
                g.Clear(Color.FromArgb(20, 10, 14, 22)); // Subtle tint
            }
        }

        var screen = new CapturedScreen(fullBitmap, virtualBounds);
        try
        {
            screen.CachedWpfBitmap = ImageHelper.ToBitmapSource(fullBitmap);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to convert captured bitmap to WPF BitmapSource", ex);
        }

        return screen;
    }

    private static bool TryCaptureWin32Gdi(Rectangle bounds, out Bitmap? bitmap)
    {
        bitmap = null;
        IntPtr hDeskDC = NativeMethods.GetDC(IntPtr.Zero);
        bool releaseDesk = true;

        if (hDeskDC == IntPtr.Zero)
        {
            hDeskDC = NativeMethods.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            releaseDesk = false;
        }

        if (hDeskDC == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr hMemDC = NativeMethods.CreateCompatibleDC(hDeskDC);
            if (hMemDC == IntPtr.Zero) return false;

            try
            {
                IntPtr hBmp = NativeMethods.CreateCompatibleBitmap(hDeskDC, bounds.Width, bounds.Height);
                if (hBmp == IntPtr.Zero) return false;

                try
                {
                    IntPtr hOld = NativeMethods.SelectObject(hMemDC, hBmp);

                    // Try with CAPTUREBLT for translucent windows
                    bool bltOk = NativeMethods.BitBlt(
                        hMemDC, 0, 0, bounds.Width, bounds.Height,
                        hDeskDC, bounds.X, bounds.Y,
                        NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);

                    if (!bltOk)
                    {
                        // Fallback to standard SRCCOPY
                        bltOk = NativeMethods.BitBlt(
                            hMemDC, 0, 0, bounds.Width, bounds.Height,
                            hDeskDC, bounds.X, bounds.Y,
                            NativeMethods.SRCCOPY);
                    }

                    NativeMethods.SelectObject(hMemDC, hOld);

                    if (bltOk)
                    {
                        using var tempBmp = Image.FromHbitmap(hBmp);
                        bitmap = new Bitmap(tempBmp);
                        return true;
                    }
                }
                finally
                {
                    NativeMethods.DeleteObject(hBmp);
                }
            }
            finally
            {
                NativeMethods.DeleteDC(hMemDC);
            }
        }
        finally
        {
            if (releaseDesk)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, hDeskDC);
            }
            else
            {
                NativeMethods.DeleteDC(hDeskDC);
            }
        }

        return false;
    }

    private static Bitmap CapturePerMonitor(Rectangle virtualBounds)
    {
        var masterBmp = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(masterBmp);
        g.Clear(Color.FromArgb(15, 10, 14, 22));

        foreach (var screen in Screen.AllScreens)
        {
            try
            {
                var sb = screen.Bounds;
                using var monBmp = new Bitmap(sb.Width, sb.Height, PixelFormat.Format32bppArgb);
                using (var mg = Graphics.FromImage(monBmp))
                {
                    mg.CopyFromScreen(sb.X, sb.Y, 0, 0, sb.Size, CopyPixelOperation.SourceCopy);
                }

                int destX = sb.X - virtualBounds.X;
                int destY = sb.Y - virtualBounds.Y;
                g.DrawImage(monBmp, destX, destY, sb.Width, sb.Height);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Failed to capture monitor {screen.DeviceName}: {ex.Message}");
            }
        }

        return masterBmp;
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
