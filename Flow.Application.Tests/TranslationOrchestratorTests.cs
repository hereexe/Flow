using Flow.Application.Abstractions;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class TranslationOrchestratorTests
{
    private class FakeTranslationProvider : ITranslationProvider
    {
        public string ProviderId => "FakeProvider";
        public bool ShouldFail { get; set; }
        public bool IsAvailable { get; set; } = true;

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(IsAvailable);

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("Provider service connection failed.");
            }

            return Task.FromResult(TranslationResult.Ok(
                $"[Translated] {request.SourceText}",
                ProviderId));
        }
    }

    [Fact]
    public async Task TranslateTextAsync_WithValidProvider_ReturnsSuccessfulTranslationResult()
    {
        // Arrange
        var provider = new FakeTranslationProvider();
        var request = new TranslationRequest("Привет мир", Language.Ru, Language.En);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("[Translated] Привет мир", result.TranslatedText);
        Assert.Equal("FakeProvider", result.ProviderId);
    }

    [Fact]
    public async Task TranslateTextAsync_WhenProviderFails_ThrowsOrHandlesGracefully()
    {
        // Arrange
        var provider = new FakeTranslationProvider { ShouldFail = true };
        var request = new TranslationRequest("Hello world", Language.En, Language.Ru);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TranslateAsync(request));
        Assert.Equal("Provider service connection failed.", ex.Message);
    }
}
