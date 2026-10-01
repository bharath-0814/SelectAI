namespace SelectAI.Core.Enums;

public enum SelectionMode
{
    Freeform,
    Rectangle,
    Text,
    Image
}

public enum ContentType
{
    Text,
    Url,
    Email,
    PhoneNumber,
    Code,
    SearchQuery,
    Image
}

public enum AiProviderType
{
    Mock,
    Gemini,
    OpenAi,
    Local
}
