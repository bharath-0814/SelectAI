using System.Diagnostics;
using System.IO;
using SelectAI.Core.Interfaces;

namespace SelectAI.Search;

public sealed class GoogleSearchProvider : ISearchProvider
{
    public string Name => "Google Search";

    public void SearchText(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var encoded = Uri.EscapeDataString(query.Trim());
        var url = $"https://www.google.com/search?q={encoded}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open Google Search in browser: {ex.Message}");
        }
    }

    public void SearchImage(byte[] imageBytes, string mimeType = "image/png")
    {
        if (imageBytes == null || imageBytes.Length == 0) return;

        try
        {
            // Save image temporarily
            var tempFolder = Path.Combine(Path.GetTempPath(), "SelectAI");
            Directory.CreateDirectory(tempFolder);
            var tempFile = Path.Combine(tempFolder, $"lens_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(tempFile, imageBytes);

            // Open Google Lens in user's default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://lens.google.com/",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch Google Lens: {ex.Message}");
        }
    }
}
