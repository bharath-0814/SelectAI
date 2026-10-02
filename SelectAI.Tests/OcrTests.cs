using System.Drawing;
using SelectAI.Ocr;
using Xunit;

namespace SelectAI.Tests;

public class OcrTests
{
    [Fact]
    public async Task TestWindowsMediaOcr()
    {
        using var bmp = new Bitmap(400, 100);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.DrawString("Pikachu is a Pokemon", new Font("Arial", 20), Brushes.Black, 10, 10);
        }

        var ocr = new WindowsMediaOcrProvider();
        var result = await ocr.RecognizeTextAsync(bmp);
        
        Assert.NotNull(result);
        Assert.Contains("Pikachu", result.FullText);
    }
}
