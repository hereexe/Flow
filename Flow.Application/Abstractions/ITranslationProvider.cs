using Flow.Domain;

namespace Flow.Application.Abstractions;

/// <summary>
/// Abstraction for offline (OpusCat) and online (Azure, DeepL, Google) translation engine adapters.
/// </summary>
public interface ITranslationProvider
{
    string ProviderId { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default);
}
