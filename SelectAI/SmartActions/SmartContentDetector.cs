using System.Text.RegularExpressions;
using SelectAI.Core.Enums;
using SelectAI.Core.Models;

namespace SelectAI.SmartActions;

public static class SmartContentDetector
{
    private static readonly Regex UrlRegex = new(
        @"(https?:\/\/[^\s]+|www\.[^\s]+|[a-zA-Z0-9\-_]+\.(com|org|net|io|ai|dev|app|co|in|uk|de|tech)[^\s]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}",
        RegexOptions.Compiled);

    private static readonly string[] CodeKeywords =
    {
        "def ", "class ", "function ", "return ", "import ", "from ", "var ", "let ",
        "const ", "public ", "private ", "protected ", "static ", "void ", "int ", "string ",
        "bool ", "float ", "double ", "namespace ", "using ", "#include", "<iostream>",
        "console.log", "System.out.println", "Console.WriteLine", "Exception", "Error:",
        "Traceback", "NullReferenceException", "SyntaxError", "TypeError", "SELECT ", "FROM ", "WHERE "
    };

    public static List<DetectedEntity> DetectEntities(string text)
    {
        var entities = new List<DetectedEntity>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return entities;
        }

        var trimmed = text.Trim();

        // 1. Check for URL
        var urlMatch = UrlRegex.Match(trimmed);
        if (urlMatch.Success)
        {
            var rawUrl = urlMatch.Value.TrimEnd('.', ',', ';', ')');
            var fullUrl = rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? rawUrl
                : "https://" + rawUrl;

            entities.Add(new DetectedEntity
            {
                Type = ContentType.Url,
                Value = fullUrl,
                DisplayLabel = "Open Link",
                ActionIcon = "🌐",
                Tooltip = $"Open {fullUrl} in browser"
            });
        }

        // 2. Check for Email
        var emailMatch = EmailRegex.Match(trimmed);
        if (emailMatch.Success)
        {
            var email = emailMatch.Value;
            entities.Add(new DetectedEntity
            {
                Type = ContentType.Email,
                Value = email,
                DisplayLabel = "Send Email",
                ActionIcon = "✉️",
                Tooltip = $"Email {email}"
            });
        }

        // 3. Check for Phone
        var phoneMatch = PhoneRegex.Match(trimmed);
        if (phoneMatch.Success)
        {
            entities.Add(new DetectedEntity
            {
                Type = ContentType.PhoneNumber,
                Value = phoneMatch.Value,
                DisplayLabel = "Copy Phone",
                ActionIcon = "📞",
                Tooltip = $"Phone: {phoneMatch.Value}"
            });
        }

        // 4. Check for Code or Exception
        bool hasCodeKeywords = CodeKeywords.Any(k => trimmed.Contains(k, StringComparison.OrdinalIgnoreCase));
        bool hasCodePunctuation = (trimmed.Contains('{') && trimmed.Contains('}')) ||
                                  (trimmed.Contains("=>") || trimmed.Contains("();") || trimmed.Contains("->"));

        if (hasCodeKeywords || hasCodePunctuation)
        {
            entities.Add(new DetectedEntity
            {
                Type = ContentType.Code,
                Value = trimmed,
                DisplayLabel = "Explain Code",
                ActionIcon = "⚡",
                Tooltip = "Ask AI to analyze and explain this code or error"
            });
        }

        // 5. Check if it's a question or search query
        bool isQuestion = trimmed.EndsWith('?') ||
            trimmed.StartsWith("what", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("how", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("why", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("who", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("when", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("where", StringComparison.OrdinalIgnoreCase);

        if (isQuestion && trimmed.Length < 150)
        {
            entities.Add(new DetectedEntity
            {
                Type = ContentType.SearchQuery,
                Value = trimmed,
                DisplayLabel = "Search Query",
                ActionIcon = "🔍",
                Tooltip = $"Search Google for: \"{trimmed}\""
            });
        }

        return entities;
    }
}
