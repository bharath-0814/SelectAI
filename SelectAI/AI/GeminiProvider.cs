using System.Net.Http;
using System.Text;
using System.Text.Json;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;

namespace SelectAI.AI;

public sealed class GeminiProvider : IAIProvider
{
    private readonly ISettingsService _settingsService;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string Id => "gemini";
    public string DisplayName => "Google Gemini";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsService.GetSecret("GeminiApiKey"));

    public GeminiProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<AiResponse> AskAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _settingsService.GetSecret("GeminiApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiResponse
            {
                Success = false,
                ErrorMessage = "Gemini API Key is not configured. Please open Settings -> AI and enter your Gemini API key.",
                ProviderName = DisplayName
            };
        }

        var model = _settingsService.CurrentSettings.GeminiModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gemini-2.0-flash";
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        try
        {
            var parts = new List<object>();

            var textContent = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.Prompt))
            {
                textContent.AppendLine(request.Prompt);
            }
            if (!string.IsNullOrWhiteSpace(request.ExtractedText))
            {
                textContent.AppendLine($"\n[Context from Screen OCR]:\n{request.ExtractedText}");
            }

            if (textContent.Length > 0)
            {
                parts.Add(new { text = textContent.ToString() });
            }

            if (request.ImageBytes != null && request.ImageBytes.Length > 0)
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = request.ImageMimeType,
                        data = Convert.ToBase64String(request.ImageBytes)
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await HttpClient.PostAsync(url, jsonContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"Gemini API Error ({response.StatusCode}): {responseString}",
                    ProviderName = DisplayName
                };
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var respParts) &&
                respParts.GetArrayLength() > 0 &&
                respParts[0].TryGetProperty("text", out var textElem))
            {
                return new AiResponse
                {
                    Success = true,
                    Content = textElem.GetString() ?? string.Empty,
                    ProviderName = DisplayName
                };
            }

            return new AiResponse
            {
                Success = false,
                ErrorMessage = "Could not parse response from Gemini API.",
                ProviderName = DisplayName
            };
        }
        catch (OperationCanceledException)
        {
            return new AiResponse
            {
                Success = false,
                ErrorMessage = "Request was cancelled.",
                ProviderName = DisplayName
            };
        }
        catch (Exception ex)
        {
            return new AiResponse
            {
                Success = false,
                ErrorMessage = $"Network or connection error: {ex.Message}",
                ProviderName = DisplayName
            };
        }
    }
}
