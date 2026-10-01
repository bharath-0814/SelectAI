using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;

namespace SelectAI.Ocr;

public sealed class WindowsMediaOcrProvider : IOcrProvider
{
    private OcrEngine? _ocrEngine;

    public string Name => "Windows Native OCR";

    public WindowsMediaOcrProvider()
    {
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        try
        {
            // First try user profile language
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            // Fallback to English if profile language not supported
            if (_ocrEngine == null)
            {
                var lang = new Windows.Globalization.Language("en-US");
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                }
            }

            // Or take the first available installed OCR language
            if (_ocrEngine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize Windows OCR Engine: {ex.Message}");
        }
    }

    public async Task<SelectAI.Core.Models.OcrResult> RecognizeTextAsync(Bitmap image, CancellationToken cancellationToken = default)
    {
        var result = new SelectAI.Core.Models.OcrResult();

        if (_ocrEngine == null || image == null)
        {
            return result;
        }

        try
        {
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            var randomAccessStream = ms.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.BmpDecoderId, randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var winOcrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

            result.FullText = winOcrResult.Text ?? string.Empty;

            foreach (var winLine in winOcrResult.Lines)
            {
                var line = new SelectAI.Core.Models.OcrLine
                {
                    Text = winLine.Text
                };

                foreach (var winWord in winLine.Words)
                {
                    line.Words.Add(new SelectAI.Core.Models.OcrWord
                    {
                        Text = winWord.Text,
                        BoundingRect = new RectangleF(
                            (float)winWord.BoundingRect.X,
                            (float)winWord.BoundingRect.Y,
                            (float)winWord.BoundingRect.Width,
                            (float)winWord.BoundingRect.Height)
                    });
                }

                result.Lines.Add(line);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR Recognition error: {ex.Message}");
        }

        return result;
    }
}
