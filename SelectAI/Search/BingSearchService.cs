using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SelectAI.Core.Utils;

namespace SelectAI.Search;

public static class BingSearchService
{
    private static readonly HttpClient _httpClient;

    static BingSearchService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Uploads an image to Bing Visual Search (kblob endpoint) and returns the full search results URL.
    /// Used natively by Samsung Galaxy Book AI Select.
    /// </summary>
    public static async Task<string> UploadImageAsync(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return "https://www.bing.com/visualsearch";
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "image", "selection.png");

            var response = await _httpClient.PostAsync("https://www.bing.com/images/kblob", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("redirectUrl", out var redirectUrlProp))
                {
                    var redirectUrl = redirectUrlProp.GetString();
                    if (!string.IsNullOrEmpty(redirectUrl))
                    {
                        var fullUrl = redirectUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? redirectUrl
                            : $"https://www.bing.com{redirectUrl}";

                        AppLog.Info($"Bing Visual Search upload succeeded: {fullUrl}");
                        return fullUrl;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to upload image to Bing Visual Search", ex);
        }

        return "https://www.bing.com/visualsearch";
    }
}
