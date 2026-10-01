using System.Drawing;
using System.Windows.Media.Imaging;

namespace SelectAI.Core.Models;

public sealed class CapturedScreen : IDisposable
{
    public Bitmap FullBitmap { get; }
    public Rectangle VirtualBounds { get; }
    public BitmapSource? CachedWpfBitmap { get; set; }

    public CapturedScreen(Bitmap fullBitmap, Rectangle virtualBounds)
    {
        FullBitmap = fullBitmap ?? throw new ArgumentNullException(nameof(fullBitmap));
        VirtualBounds = virtualBounds;
    }

    public void Dispose()
    {
        FullBitmap.Dispose();
        CachedWpfBitmap = null;
    }
}

public sealed class SelectionRegion : IDisposable
{
    public Enums.SelectionMode Mode { get; set; }
    public List<PointF> RawPoints { get; set; } = new();
    public List<PointF> SmoothedPoints { get; set; } = new();
    public RectangleF BoundingBox { get; set; }
    public Bitmap? CroppedBitmap { get; set; }
    public BitmapSource? CroppedImageSource { get; set; }

    public void Dispose()
    {
        CroppedBitmap?.Dispose();
        CroppedBitmap = null;
        CroppedImageSource = null;
    }
}

public sealed class OcrWord
{
    public string Text { get; set; } = string.Empty;
    public RectangleF BoundingRect { get; set; }
}

public sealed class OcrLine
{
    public string Text { get; set; } = string.Empty;
    public List<OcrWord> Words { get; set; } = new();
}

public sealed class OcrResult
{
    public string FullText { get; set; } = string.Empty;
    public List<OcrLine> Lines { get; set; } = new();
    public bool HasText => !string.IsNullOrWhiteSpace(FullText);
}

public sealed class DetectedEntity
{
    public Enums.ContentType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public string ActionIcon { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
}

public sealed class AiRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string ImageMimeType { get; set; } = "image/png";
}

public sealed class AiResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string ProviderName { get; set; } = string.Empty;
}
