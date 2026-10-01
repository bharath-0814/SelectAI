using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;

namespace SelectAI.AI;

public sealed class MockAiProvider : IAIProvider
{
    public string Id => "mock";
    public string DisplayName => "SelectAI Built-in Assistant";
    public bool IsConfigured => true;

    public async Task<AiResponse> AskAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        // Simulate realistic typing / generation latency
        await Task.Delay(400, cancellationToken);

        var prompt = request.Prompt?.Trim().ToLowerInvariant() ?? "";
        var context = request.ExtractedText?.Trim() ?? "";

        string content;

        if (prompt.Contains("explain") || prompt.Contains("what"))
        {
            if (!string.IsNullOrEmpty(context))
            {
                content = $"### 💡 Explanation\n\n**Selected Content:**\n> \"{Truncate(context, 100)}\"\n\n" +
                          "**Analysis:**\n" +
                          "• This appears to be relevant information captured directly from your screen.\n" +
                          "• SelectAI extracted this text cleanly using the native Windows OCR engine.\n" +
                          "• To enable real-time cloud AI reasoning, add your Gemini or OpenAI API key in **Settings**.";
            }
            else
            {
                content = "### 🖼️ Visual Content Analysis\n\n" +
                          "You selected an image region. The visual features have been captured in high definition.\n" +
                          "To get detailed vision analysis of diagrams, icons, or photos, configure Google Gemini in Settings!";
            }
        }
        else if (prompt.Contains("summar"))
        {
            content = $"### 📋 Summary\n\n" +
                      $"• **Key Point 1:** {Truncate(context, 60)}\n" +
                      $"• **Key Point 2:** Captured with high-fidelity coordinate resolution.\n" +
                      $"• **Key Point 3:** Ready for copying, sharing, or deep AI analysis.";
        }
        else if (prompt.Contains("code") || prompt.Contains("debug") || prompt.Contains("fix"))
        {
            content = "### ⚡ Code Review & Insights\n\n" +
                      "```csharp\n// Identified snippet from screen:\n" +
                      (string.IsNullOrEmpty(context) ? "// [Code region captured]" : context) +
                      "\n```\n\n" +
                      "**Review:**\n" +
                      "1. Ensure null-safety checks and proper exception handling.\n" +
                      "2. Verify asynchronous method calls propagate cancellation tokens.\n" +
                      "3. Connect your Gemini API Key in Settings for live debugging and unit test generation!";
        }
        else if (prompt.Contains("translat"))
        {
            content = "### 🌐 Translation Preview\n\n" +
                      $"**Original:** {Truncate(context, 80)}\n\n" +
                      $"**Translated:** {Truncate(context, 80)}\n\n" +
                      "*(Configure Gemini or OpenAI in Settings for 100+ language translation)*";
        }
        else
        {
            content = $"### 🤖 SelectAI Assistant\n\n" +
                      $"Received request: *\"{request.Prompt}\"*\n\n" +
                      (!string.IsNullOrEmpty(context) ? $"Context: \"{Truncate(context, 120)}\"\n\n" : "") +
                      "SelectAI is running smoothly with native Windows integration. Configure your preferred AI provider in **Settings** to unlock live multimodal AI!";
        }

        return new AiResponse
        {
            Success = true,
            Content = content,
            ProviderName = DisplayName
        };
    }

    private static string Truncate(string str, int maxLen)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Length <= maxLen ? str : str.Substring(0, maxLen) + "...";
    }
}
