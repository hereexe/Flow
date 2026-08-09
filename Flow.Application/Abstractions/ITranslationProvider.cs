using Flow.Domain;

namespace Flow.Application.Abstractions;

public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default);
}
