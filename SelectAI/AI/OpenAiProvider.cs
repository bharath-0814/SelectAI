using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;

namespace SelectAI.AI;

public sealed class OpenAiProvider : IAIProvider
{
    private readonly ISettingsService _settingsService;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string Id => "openai";
    public string DisplayName => "OpenAI (ChatGPT)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsService.GetSecret("OpenAiApiKey"));

    public OpenAiProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<AiResponse> AskAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _settingsService.GetSecret("OpenAiApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiResponse
            {
                Success = false,
                ErrorMessage = "OpenAI API Key is not configured. Please open Settings -> AI and enter your OpenAI API key.",
                ProviderName = DisplayName
            };
        }

        var model = _settingsService.CurrentSettings.OpenAiModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gpt-4o-mini";
        }

        const string url = "https://api.openai.com/v1/chat/completions";

        try
        {
            var contentList = new List<object>();

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
                contentList.Add(new { type = "text", text = textContent.ToString() });
            }

            if (request.ImageBytes != null && request.ImageBytes.Length > 0)
            {
                var base64 = Convert.ToBase64String(request.ImageBytes);
                contentList.Add(new
                {
                    type = "image_url",
                    image_url = new { url = $"data:{request.ImageMimeType};base64,{base64}" }
                });
            }

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentList
                    }
                },
                max_tokens = 1000
            };

            var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json")
            };
            requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await HttpClient.SendAsync(requestMsg, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"OpenAI API Error ({response.StatusCode}): {responseString}",
                    ProviderName = DisplayName
                };
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentElem))
            {
                return new AiResponse
                {
                    Success = true,
                    Content = contentElem.GetString() ?? string.Empty,
                    ProviderName = DisplayName
                };
            }

            return new AiResponse
            {
                Success = false,
                ErrorMessage = "Could not parse response from OpenAI API.",
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
