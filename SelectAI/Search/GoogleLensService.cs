using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using SelectAI.Core.Utils;

namespace SelectAI.Search;

public static class GoogleLensService
{
    private static readonly HttpClient _httpClient;

    static GoogleLensService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Uploads an image to Google Lens and returns the HTTP 303 Redirect URL containing Lens / Gemini results.
    /// Returns a fallback Google/Lens URL if upload fails.
    /// </summary>
    public static async Task<string> UploadImageAsync(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return "https://lens.google.com/";
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "encoded_image", "selection.png");

            var response = await _httpClient.PostAsync("https://lens.google.com/v3/upload", content);

            if (response.Headers.Location != null)
            {
                var targetUrl = response.Headers.Location.ToString();
                AppLog.Info($"Google Lens upload succeeded. Redirect target: {targetUrl}");
                return targetUrl;
            }

            // In case of 200 or alternate redirect
            if (response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                response.StatusCode == System.Net.HttpStatusCode.SeeOther ||
                response.StatusCode == System.Net.HttpStatusCode.Found)
            {
                var loc = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(loc)) return loc;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to upload image to Google Lens", ex);
        }

        // Fallback to lens homepage or search
        return "https://lens.google.com/";
    }
}
