using System.Drawing;
using SelectAI.Core.Enums;
using SelectAI.Core.Models;
using SelectAI.Settings;

namespace SelectAI.Core.Interfaces;

public interface IScreenCapture
{
    CapturedScreen CaptureVirtualDesktop();
    Bitmap CropRegion(Bitmap source, RectangleF region);
    Bitmap CropFreeform(Bitmap source, IReadOnlyList<PointF> polygon, RectangleF boundingBox);
}

public interface ISelectionEngine
{
    List<PointF> SmoothPoints(IReadOnlyList<PointF> rawPoints);
    RectangleF CalculateBoundingBox(IReadOnlyList<PointF> points);
    bool IsClosedLoop(IReadOnlyList<PointF> points, float toleranceDistance = 45f);
}

public interface IOcrProvider
{
    string Name { get; }
    Task<OcrResult> RecognizeTextAsync(Bitmap image, CancellationToken cancellationToken = default);
}

public interface IAIProvider
{
    string Id { get; }
    string DisplayName { get; }
    bool IsConfigured { get; }
    Task<AiResponse> AskAsync(AiRequest request, CancellationToken cancellationToken = default);
}

public interface ISearchProvider
{
    string Name { get; }
    void SearchText(string query);
    void SearchImage(byte[] imageBytes, string mimeType = "image/png");
}

public interface IActionProvider
{
    Task ExecuteActionAsync(DetectedEntity entity);
}

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    void LoadSettings();
    void SaveSettings();
    void ResetToDefaults();
    string? GetSecret(string key);
    void SetSecret(string key, string? secret);
    bool IsAutoStartEnabled();
    void SetAutoStart(bool enable);
}
