using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Domain;

namespace Flow.Infrastructure.Translation;

public class TranslationProviderFactory : ITranslationProviderFactory
{
    private readonly IEnumerable<ITranslationProvider> _providers;

    public TranslationProviderFactory(IEnumerable<ITranslationProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public ITranslationProvider GetActive(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Mode == TranslationMode.Offline)
        {
            var offlineProvider = _providers.FirstOrDefault(p => p.ProviderId == ProviderIdentifiers.OpusCat);
            if (offlineProvider == null)
            {
                throw new InvalidOperationException($"Offline provider '{ProviderIdentifiers.OpusCat}' is not registered in service collection.");
            }
            return offlineProvider;
        }

        var onlineProvider = _providers.FirstOrDefault(p => p.ProviderId.Equals(settings.ActiveOnlineProvider, StringComparison.OrdinalIgnoreCase));
        if (onlineProvider == null)
        {
            // Fallback to first available provider or throw
            var fallback = _providers.FirstOrDefault();
            if (fallback == null)
            {
                throw new InvalidOperationException("No translation providers are registered in service collection.");
            }
            return fallback;
        }

        return onlineProvider;
    }
}
