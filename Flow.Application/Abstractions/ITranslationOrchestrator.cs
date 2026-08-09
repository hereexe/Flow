using Flow.Domain;

namespace Flow.Application.Abstractions;

/// <summary>
/// Orchestrates the full in-place text translation lifecycle:
/// capturing selected text, auto-detecting language, invoking translation providers,
/// updating HUD status, and replacing text in-place while preserving clipboard state.
/// </summary>
public interface ITranslationOrchestrator
{
    Task<TranslationResult> ExecuteTranslationAsync(CancellationToken ct = default);
    Task<TranslationResult> TranslateTextAsync(string text, Language? targetLanguage = null, CancellationToken ct = default);
}
