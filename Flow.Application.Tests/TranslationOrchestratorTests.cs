using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Application.Services;
using Flow.Domain;
using Xunit;
using ClipboardSnapshot = Flow.Application.Models.ClipboardSnapshot;

namespace Flow.Application.Tests;

public class TranslationOrchestratorTests
{
    // --- Fakes ---

    private class FakeTranslationProvider : ITranslationProvider
    {
        public string ProviderId { get; set; } = "FakeProvider";
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
        public bool SnapshotCaptured { get; private set; }
        public bool SnapshotRestored { get; private set; }

        public ClipboardSnapshot CaptureSnapshot()
        {
            SnapshotCaptured = true;
            return new ClipboardSnapshot();
        }

        public Task<string> CaptureSelectedTextAsync(CancellationToken ct = default) => Task.FromResult(SelectedText);

        public Task ReplaceSelectedTextAsync(string translatedText, CancellationToken ct = default)
        {
            ReplacedText = translatedText;
            return Task.CompletedTask;
        }

        public void RestoreSnapshot(ClipboardSnapshot snapshot)
        {
            SnapshotRestored = true;
        }
    }

    private class FakeHudNotifier : IHudStatusNotifier
    {
        public event EventHandler<HudStatusChangedEventArgs>? StatusChanged;
        public void TriggerStatusChanged(HudStatusChangedEventArgs e) => StatusChanged?.Invoke(this, e);
        public HudStatusState CurrentState { get; private set; } = HudStatusState.Hidden;
        public List<HudStatusState> StateHistory { get; } = new();

        public void ShowTranslating() { CurrentState = HudStatusState.Translating; StateHistory.Add(CurrentState); }
        public void ShowSuccess(string? message = null) { CurrentState = HudStatusState.Success; StateHistory.Add(CurrentState); }
        public void ShowError(string? errorMessage = null) { CurrentState = HudStatusState.Error; StateHistory.Add(CurrentState); }
        public void Hide() { CurrentState = HudStatusState.Hidden; StateHistory.Add(CurrentState); }
    }

    private class FakeTranslationProviderFactory : ITranslationProviderFactory
    {
        public ITranslationProvider ActiveProvider { get; set; }

        public FakeTranslationProviderFactory(ITranslationProvider activeProvider)
        {
            ActiveProvider = activeProvider;
        }

        public ITranslationProvider GetActive(AppSettings settings) => ActiveProvider;
    }

    private class FakeSettingsRepository : ISettingsRepository
    {
        public AppSettings Settings { get; set; } = new AppSettings();

        public AppSettings LoadSettings() => Settings;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default) => Task.FromResult(Settings);
        public void SaveSettings(AppSettings settings) => Settings = settings;
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private static TranslationOrchestrator CreateOrchestrator(
        IDirectionDetector detector,
        ITranslationProvider provider,
        IClipboardService clipboard,
        IHudStatusNotifier hud,
        AppSettings? settings = null)
    {
        var factory = new FakeTranslationProviderFactory(provider);
        var settingsRepo = new FakeSettingsRepository { Settings = settings ?? new AppSettings() };
        return new TranslationOrchestrator(detector, factory, settingsRepo, clipboard, hud);
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

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

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

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

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

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.TranslateTextAsync("Привет мир", targetLanguage: Language.Ru);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(Language.Ru, provider.LastRequest!.TargetLanguage);
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

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

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

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.TranslateTextAsync("Hello world");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Provider service connection failed.", result.ErrorMessage);
        Assert.Equal("FakeProvider", result.ProviderId);
    }

    // --- ExecuteTranslationAsync tests ---

    [Fact]
    public async Task ExecuteTranslationAsync_EndToEndSuccess_ReplacesTextAndRestoresSnapshot()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService { SelectedText = "Привет мир" };
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.ExecuteTranslationAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("[Translated] Привет мир", clipboard.ReplacedText);
        Assert.True(clipboard.SnapshotCaptured);
        Assert.True(clipboard.SnapshotRestored);
        Assert.Equal(HudStatusState.Success, hud.CurrentState);
    }

    [Fact]
    public async Task ExecuteTranslationAsync_EmptyTextSelected_AbortsWithoutReplacingTextAndRestoresSnapshot()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService { SelectedText = "   " };
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.ExecuteTranslationAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Null(clipboard.ReplacedText);
        Assert.True(clipboard.SnapshotRestored);
    }

    [Fact]
    public async Task ExecuteTranslationAsync_ProviderThrowsException_SkipsReplacementAndRestoresSnapshot()
    {
        // Arrange
        var provider = new FakeTranslationProvider { ShouldFail = true };
        var clipboard = new FakeClipboardService { SelectedText = "Hello world" };
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud);

        // Act
        var result = await orchestrator.ExecuteTranslationAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Null(clipboard.ReplacedText);
        Assert.True(clipboard.SnapshotRestored);
        Assert.Equal(HudStatusState.Error, hud.CurrentState);
    }

    [Fact]
    public async Task ExecuteTranslationAsync_WithFactoryAndSettings_ResolvesActiveProviderDynamically()
    {
        // Arrange
        var provider = new FakeTranslationProvider { ProviderId = "DynamicProvider" };
        var factory = new FakeTranslationProviderFactory(provider);
        var settingsRepo = new FakeSettingsRepository();
        var clipboard = new FakeClipboardService { SelectedText = "Dynamic Test" };
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();

        var orchestrator = new TranslationOrchestrator(detector, factory, settingsRepo, clipboard, hud);

        // Act
        var result = await orchestrator.ExecuteTranslationAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("DynamicProvider", result.ProviderId);
        Assert.Equal("[Translated] Dynamic Test", clipboard.ReplacedText);
        Assert.True(clipboard.SnapshotRestored);
    }

    [Theory]
    [InlineData("こんにちは", Language.En, Language.Ja, Language.Ja, Language.En)]
    [InlineData("Hello", Language.En, Language.Ja, Language.En, Language.Ja)]
    [InlineData("Schöne Grüße", Language.En, Language.De, Language.De, Language.En)]
    [InlineData("¿Cómo estás?", Language.En, Language.Es, Language.Es, Language.En)]
    [InlineData("Bonjour, ça va?", Language.En, Language.Fr, Language.Fr, Language.En)]
    public async Task TranslateTextAsync_WithConfiguredLanguagePairs_PassesLanguagesToDetectorAndProvider(
        string text, Language primary, Language secondary, Language expectedSource, Language expectedTarget)
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();
        var settings = new AppSettings
        {
            PrimaryLanguage = primary,
            SecondaryLanguage = secondary
        };

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud, settings);

        // Act
        var result = await orchestrator.TranslateTextAsync(text);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(expectedSource, provider.LastRequest!.SourceLanguage);
        Assert.Equal(expectedTarget, provider.LastRequest!.TargetLanguage);
    }

    [Fact]
    public async Task TranslateTextAsync_WithExplicitTargetAndConfiguredPair_DeterminesOppositeFromPair()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var clipboard = new FakeClipboardService();
        var hud = new FakeHudNotifier();
        var detector = new DirectionDetector();
        var settings = new AppSettings
        {
            PrimaryLanguage = Language.En,
            SecondaryLanguage = Language.De
        };

        var orchestrator = CreateOrchestrator(detector, provider, clipboard, hud, settings);

        // Act - Explicit target is German -> Source should be English
        var result = await orchestrator.TranslateTextAsync("Hello", targetLanguage: Language.De);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(Language.De, provider.LastRequest!.TargetLanguage);
        Assert.Equal(Language.En, provider.LastRequest!.SourceLanguage);
    }
}

