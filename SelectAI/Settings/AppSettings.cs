using System.Text.Json.Serialization;
using SelectAI.Core.Enums;

namespace SelectAI.Settings;

public class AppSettings
{
    public SelectAI.Core.Enums.SelectionMode DefaultMode { get; set; } = SelectAI.Core.Enums.SelectionMode.Freeform;
    public AiProviderType ActiveAiProvider { get; set; } = AiProviderType.Mock;
    public string GeminiModel { get; set; } = "gemini-2.0-flash";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string LocalModelEndpoint { get; set; } = "http://localhost:11434/v1";
    public string LocalModelName { get; set; } = "llama3";
    public string SearchEngineUrl { get; set; } = "https://www.google.com/search?q={0}";
    public string HotkeyModifiers { get; set; } = "Control, Shift";
    public string HotkeyKey { get; set; } = "C";
    public bool StartWithWindows { get; set; } = false;
    public bool AutoShowToolbar { get; set; } = true;
    public bool EnableGlowAnimation { get; set; } = true;
    public double AnimationSpeed { get; set; } = 1.0;
    public string OcrLanguage { get; set; } = "en";
    public bool CopyOcrAutomatically { get; set; } = false;

    // Encrypted secret tokens stored safely using DPAPI
    [JsonPropertyName("encryptedSecrets")]
    public Dictionary<string, string> EncryptedSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
