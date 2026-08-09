using Flow.Application.Abstractions;
using Flow.Application.Services;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class TranslationOrchestratorTests
{
    // --- Fakes ---

    private class FakeTranslationProvider : ITranslationProvider
    {
        public string ProviderId => "FakeProvider";
        public bool ShouldFail { get; set; }
        public bool IsAvailable { get; set; } = true;
        public TranslationRequest? LastRequest { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(IsAvailable);

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (ShouldFail)
                throw new InvalidOperationException("Provider service connection failed.");

            return Task.FromResult(TranslationResult.Ok(
                $"[Translated] {request.SourceText}",
                ProviderId));
        }
    }

    private class FakeClipboardService : IClipboardService
    {
        public string SelectedText { get; set; } = "";
        public string? ReplacedText { get; private set; }

        public Models.ClipboardSnapshot CaptureSnapshot() => new();
        public Task<string> CaptureSelectedTextAsync(CancellationToken ct = default) => Task.FromResult(SelectedText);
        public Task ReplaceSelectedTextAsync(string translatedText, CancellationToken ct = default)
        {
            ReplacedText = translatedText;
            return Task.CompletedTask;
        }
        public void RestoreSnapshot(Models.ClipboardSnapshot snapshot) { }
    }

    private class FakeHudNotifier : IHudStatusNotifier
    {
        public event EventHandler<HudStatusChangedEventArgs>? StatusChanged;
        public HudStatusState CurrentState { get; private set; } = HudStatusState.Hidden;
        public List<HudStatusState> StateHistory { get; } = new();

        public void ShowTranslating() { CurrentState = HudStatusState.Translating; StateHistory.Add(CurrentState); }
        public void ShowSuccess(string? message = null) { CurrentState = HudStatusState.Success; StateHistory.Add(CurrentState); }
        public void ShowError(string errorMessage) { CurrentState = HudStatusState.Error; StateHistory.Add(CurrentState); }
        public void Hide() { CurrentState = HudStatusState.Hidden; StateHistory.Add(CurrentState); }
    }

    // --- TranslateTextAsync tests ---

    [Fact]
    public async Task TranslateTextAsync_WithoutExplicitTarget_InvokesDirectionDetector()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, provider, clipboard, hud);

        // Act — Russian text, no explicit target → should auto-detect RU→EN
        var result = await orchestrator.TranslateTextAsync("Привет мир");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("[Translated] Привет мир", result.TranslatedText);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(Language.Ru, provider.LastRequest!.SourceLanguage);
        Assert.Equal(Language.En, provider.LastRequest!.TargetLanguage);
    }

    [Fact]
    public async Task TranslateTextAsync_WithoutExplicitTarget_DetectsEnglishToRussian()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, provider, clipboard, hud);

        // Act — English text, no explicit target → should auto-detect EN→RU
        var result = await orchestrator.TranslateTextAsync("Hello world");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(Language.En, provider.LastRequest!.SourceLanguage);
        Assert.Equal(Language.Ru, provider.LastRequest!.TargetLanguage);
    }

    [Fact]
    public async Task TranslateTextAsync_WithExplicitTarget_DoesNotInvokeDetector()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, provider, clipboard, hud);

        // Act — Russian text with explicit target=Ru (unusual but valid — tests that detector is bypassed)
        var result = await orchestrator.TranslateTextAsync("Привет мир", targetLanguage: Language.Ru);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(Language.Ru, provider.LastRequest!.TargetLanguage);
        // Source should be inferred as opposite of explicit target
        Assert.Equal(Language.En, provider.LastRequest!.SourceLanguage);
    }

    [Fact]
    public async Task TranslateTextAsync_ShowsTranslatingStatus()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, provider, clipboard, hud);

        // Act
        await orchestrator.TranslateTextAsync("Hello");

        // Assert — HUD should have shown "Translating" state
        Assert.Contains(HudStatusState.Translating, hud.StateHistory);
    }

    [Fact]
    public async Task TranslateTextAsync_WhenProviderFails_ReturnsFailResult()
    {
        // Arrange
        var provider = new FakeTranslationProvider { ShouldFail = true };
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.TranslateTextAsync("Hello world");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Provider service connection failed.", result.ErrorMessage);
        Assert.Equal("FakeProvider", result.ProviderId);
    }
}
