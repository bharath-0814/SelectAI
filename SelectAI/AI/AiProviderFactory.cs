using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;

namespace SelectAI.AI;

public sealed class AiProviderFactory
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<AiProviderType, IAIProvider> _providers;

    public AiProviderFactory(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _providers = new Dictionary<AiProviderType, IAIProvider>
        {
            [AiProviderType.Mock] = new MockAiProvider(),
            [AiProviderType.Gemini] = new GeminiProvider(settingsService),
            [AiProviderType.OpenAi] = new OpenAiProvider(settingsService)
        };
    }

    public IAIProvider GetCurrentProvider()
    {
        var activeType = _settingsService.CurrentSettings.ActiveAiProvider;
        if (_providers.TryGetValue(activeType, out var provider))
        {
            return provider;
        }

        return _providers[AiProviderType.Mock];
    }

    public IEnumerable<IAIProvider> GetAllProviders()
    {
        return _providers.Values;
    }
}
