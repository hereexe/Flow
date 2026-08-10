using System.Net;
using System.Net.Http;
using System.Text.Json;
using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Domain;
using Flow.Infrastructure.Translation;
using Flow.Infrastructure.Translation.Offline;
using Xunit;

namespace Flow.Application.Tests;

public class OpusCatOfflineTranslationTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handler { get; set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            Handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Handler(request);
        }
    }

    private class MockProcessManager : IOpusCatProcessManager
    {
        public int StartCallCount { get; private set; }
        public bool IsRunning { get; private set; }
        public Exception? ExceptionToThrowOnStart { get; set; }

        public Task EnsureStartedAsync(CancellationToken ct = default)
        {
            StartCallCount++;
            if (ExceptionToThrowOnStart != null)
            {
                throw ExceptionToThrowOnStart;
            }
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task<bool> CheckHealthAsync(CancellationToken ct = default)
        {
            return Task.FromResult(IsRunning);
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Dispose() { }
    }

    [Fact]
    public void TranslationProviderFactory_ReturnsOpusCatProvider_WhenModeIsOffline()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager();
        var offlineProvider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var factory = new TranslationProviderFactory(new ITranslationProvider[] { offlineProvider });
        var settings = new AppSettings { Mode = TranslationMode.Offline };

        // Act
        var activeProvider = factory.GetActive(settings);

        // Assert
        Assert.NotNull(activeProvider);
        Assert.Equal(ProviderIdentifiers.OpusCat, activeProvider.ProviderId);
    }

    [Fact]
    public void OpusCatSidecarProvider_DoesNotStartProcess_BeforeFirstTranslateCall()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager();

        // Act — Instantiate provider without calling TranslateAsync
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        // Assert — Process manager should not have been started
        Assert.Equal(0, processManager.StartCallCount);
        Assert.False(processManager.IsRunning);
    }

    [Fact]
    public async Task OpusCatSidecarProvider_TranslateAsync_TriggersLazyInitializationAndReturnsTranslation()
    {
        // Arrange
        var options = new OpusCatOptions { Port = 8500 };
        var handler = new FakeHttpMessageHandler(req =>
        {
            var jsonResponse = JsonSerializer.Serialize(new { translatedText = "Hello world" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager();
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var request = new TranslationRequest("Привет мир", Language.Ru, Language.En);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.Equal(1, processManager.StartCallCount);
        Assert.True(result.Success);
        Assert.Equal("Hello world", result.TranslatedText);
        Assert.Equal(ProviderIdentifiers.OpusCat, result.ProviderId);
    }

    [Fact]
    public async Task OpusCatSidecarProvider_WhenExecutableNotFound_ReturnsUserFriendlyErrorMessage()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager
        {
            ExceptionToThrowOnStart = new OpusCatExecutableNotFoundException("OpusCat/OpusCat.Engine.exe")
        };
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var request = new TranslationRequest("Test", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Компоненты офлайн-перевода не найдены", result.ErrorMessage);
    }

    [Fact]
    public async Task OpusCatSidecarProvider_WhenPortInUse_ReturnsUserFriendlyErrorMessage()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager
        {
            ExceptionToThrowOnStart = new OpusCatPortInUseException(8500)
        };
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var request = new TranslationRequest("Test", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Локальный порт 8500 уже занят", result.ErrorMessage);
    }

    [Fact]
    public async Task OpusCatSidecarProvider_WhenStartupTimesOut_ReturnsUserFriendlyErrorMessage()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager
        {
            ExceptionToThrowOnStart = new OpusCatStartupTimeoutException(10)
        };
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var request = new TranslationRequest("Test", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Превышено время ожидания запуска", result.ErrorMessage);
    }

    [Fact]
    public async Task OpusCatSidecarProvider_WhenProcessCrashes_ReturnsExitCodeErrorMessage()
    {
        // Arrange
        var options = new OpusCatOptions();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var processManager = new MockProcessManager
        {
            ExceptionToThrowOnStart = new OpusCatProcessCrashedException(-1)
        };
        var provider = new OpusCatSidecarProvider(processManager, httpClient, options);

        var request = new TranslationRequest("Test", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("код выхода: -1", result.ErrorMessage);
    }
}
