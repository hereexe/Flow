using System.Diagnostics;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Application.Services;

/// <summary>
/// Orchestrates the full in-place text translation lifecycle:
/// capturing selected text, auto-detecting language, invoking translation providers,
/// updating HUD status, and replacing text in-place while preserving clipboard state.
/// </summary>
public class TranslationOrchestrator : ITranslationOrchestrator
{
    private readonly IDirectionDetector _directionDetector;
    private readonly ITranslationProvider _translationProvider;
    private readonly IClipboardService _clipboardService;
    private readonly IHudStatusNotifier _hudNotifier;

    public TranslationOrchestrator(
        IDirectionDetector directionDetector,
        ITranslationProvider translationProvider,
        IClipboardService clipboardService,
        IHudStatusNotifier hudNotifier)
    {
        _directionDetector = directionDetector ?? throw new ArgumentNullException(nameof(directionDetector));
        _translationProvider = translationProvider ?? throw new ArgumentNullException(nameof(translationProvider));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _hudNotifier = hudNotifier ?? throw new ArgumentNullException(nameof(hudNotifier));
    }

    /// <inheritdoc />
    public async Task<TranslationResult> ExecuteTranslationAsync(CancellationToken ct = default)
    {
        var snapshot = _clipboardService.CaptureSnapshot();
        try
        {
            var sourceText = await _clipboardService.CaptureSelectedTextAsync(ct);

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                _clipboardService.RestoreSnapshot(snapshot);
                return TranslationResult.Fail("No text selected.");
            }

            var result = await TranslateTextAsync(sourceText, targetLanguage: null, ct);

            if (result.Success)
            {
                _hudNotifier.ShowSuccess();
                await _clipboardService.ReplaceSelectedTextAsync(result.TranslatedText, ct);
            }
            else
            {
                _hudNotifier.ShowError(result.ErrorMessage ?? "Translation failed.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _hudNotifier.ShowError(ex.Message);
            return TranslationResult.Fail(ex.Message);
        }
        finally
        {
            _clipboardService.RestoreSnapshot(snapshot);
        }
    }

    /// <inheritdoc />
    public async Task<TranslationResult> TranslateTextAsync(
        string text,
        Language? targetLanguage = null,
        CancellationToken ct = default)
    {
        Language source;
        Language target;

        if (targetLanguage.HasValue)
        {
            // Explicit target: infer source as the opposite language
            target = targetLanguage.Value;
            source = target == Language.Ru ? Language.En : Language.Ru;
        }
        else
        {
            // Auto-detect direction from text content
            (source, target) = _directionDetector.DetectDirection(text);
        }

        _hudNotifier.ShowTranslating();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = new TranslationRequest(text, source, target);
            var result = await _translationProvider.TranslateAsync(request, ct);
            stopwatch.Stop();

            return result with { ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return TranslationResult.Fail(ex.Message, _translationProvider.ProviderId, stopwatch.ElapsedMilliseconds);
        }
    }
}
