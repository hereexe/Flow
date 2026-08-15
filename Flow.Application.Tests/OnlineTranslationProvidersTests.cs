using System.Net;
using System.Net.Http;
using System.Text.Json;
using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Domain;
using Flow.Infrastructure.Translation;
using Flow.Infrastructure.Translation.Online;
using Xunit;

namespace Flow.Application.Tests;

public class OnlineTranslationProvidersTests
{
    private class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public void SaveSecret(string providerId, string secret) => _secrets[providerId] = secret;
        public string? GetSecret(string providerId) => _secrets.TryGetValue(providerId, out var s) ? s : null;
        public bool HasSecret(string providerId) => _secrets.ContainsKey(providerId) && !string.IsNullOrEmpty(_secrets[providerId]);
        public void DeleteSecret(string providerId) => _secrets.Remove(providerId);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handler { get; set; }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            Handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return await Handler(request);
        }
    }

    // --- Azure Translator Tests ---

    [Fact]
    public async Task AzureTranslatorProvider_MissingApiKey_ReturnsFailResult()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("не настроен", result.ErrorMessage);
    }

    [Fact]
    public async Task AzureTranslatorProvider_SuccessResponse_ParsesTranslatedText()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Azure, "test-azure-key");

        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.True(req.Headers.Contains("Ocp-Apim-Subscription-Key"));
            Assert.Equal("test-azure-key", req.Headers.GetValues("Ocp-Apim-Subscription-Key").First());

            var jsonResponse = JsonSerializer.Serialize(new[]
            {
                new { translations = new[] { new { text = "Привет", to = "ru" } } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Привет", result.TranslatedText);
        Assert.Equal(ProviderIdentifiers.Azure, result.ProviderId);
    }

    [Fact]
    public async Task AzureTranslatorProvider_Http401_ReturnsInvalidApiKeyErrorMessage()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Azure, "invalid-key");

        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var client = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Недействительный API-ключ", result.ErrorMessage);
    }

    [Fact]
    public async Task AzureTranslatorProvider_Http429_ReturnsQuotaExceededErrorMessage()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Azure, "test-key");

        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)));
        var client = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Превышен лимит запросов", result.ErrorMessage);
    }

    // --- DeepL Provider Tests ---

    [Fact]
    public async Task DeepLProvider_MissingApiKey_ReturnsFailResult()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var provider = new DeepLProvider(client, secretStore);

        var request = new TranslationRequest("World", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("не настроен", result.ErrorMessage);
    }

    [Fact]
    public async Task DeepLProvider_FreeKeySuffix_SelectsFreeEndpointAndTranslates()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.DeepL, "test-key-123:fx");

        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            var jsonResponse = JsonSerializer.Serialize(new
            {
                translations = new[] { new { text = "Мир", detected_source_language = "EN" } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new DeepLProvider(client, secretStore);

        var request = new TranslationRequest("World", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Мир", result.TranslatedText);
        Assert.NotNull(capturedRequest);
        Assert.StartsWith("https://api-free.deepl.com", capturedRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeepLProvider_Http401_ReturnsInvalidApiKeyErrorMessage()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.DeepL, "bad-key");

        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var client = new HttpClient(handler);
        var provider = new DeepLProvider(client, secretStore);

        var request = new TranslationRequest("World", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Недействительный API-ключ", result.ErrorMessage);
    }

    // --- Google Translate Provider Tests ---

    [Fact]
    public async Task GoogleTranslateProvider_MissingApiKey_ReturnsFailResult()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var provider = new GoogleTranslateProvider(client, secretStore);

        var request = new TranslationRequest("Cat", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("не настроен", result.ErrorMessage);
    }

    [Fact]
    public async Task GoogleTranslateProvider_SuccessResponse_ParsesTranslatedText()
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Google, "google-api-key");

        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Contains("key=google-api-key", req.RequestUri!.ToString());

            var jsonResponse = JsonSerializer.Serialize(new
            {
                data = new { translations = new[] { new { translatedText = "Кот" } } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleTranslateProvider(client, secretStore);

        var request = new TranslationRequest("Cat", Language.En, Language.Ru);

        // Act
        var result = await provider.TranslateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Кот", result.TranslatedText);
        Assert.Equal(ProviderIdentifiers.Google, result.ProviderId);
    }

    // --- Factory Provider Selection Tests ---

    [Theory]
    [InlineData(ProviderIdentifiers.Azure)]
    [InlineData(ProviderIdentifiers.DeepL)]
    [InlineData(ProviderIdentifiers.Google)]
    public void TranslationProviderFactory_SelectsConfiguredActiveOnlineProvider(string targetProvider)
    {
        // Arrange
        var secretStore = new FakeSecretStore();
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);

        var azure = new AzureTranslatorProvider(client, secretStore);
        var deepl = new DeepLProvider(client, secretStore);
        var google = new GoogleTranslateProvider(client, secretStore);

        var factory = new TranslationProviderFactory(new ITranslationProvider[] { azure, deepl, google });
        var settings = new AppSettings
        {
            ActiveOnlineProvider = targetProvider
        };

        // Act
        var selectedProvider = factory.GetActive(settings);

        // Assert
        Assert.NotNull(selectedProvider);
        Assert.Equal(targetProvider, selectedProvider.ProviderId);
    }

    // --- Provider Language Code Mapping Tests ---

    [Theory]
    [InlineData(Language.Zh, "zh-Hans")]
    [InlineData(Language.Ja, "ja")]
    [InlineData(Language.Es, "es")]
    [InlineData(Language.De, "de")]
    [InlineData(Language.Fr, "fr")]
    [InlineData(Language.Pt, "pt")]
    [InlineData(Language.It, "it")]
    public async Task AzureTranslatorProvider_MapsLanguageCodesCorrectly(Language targetLang, string expectedTargetCode)
    {
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Azure, "test-key");

        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            var jsonResponse = JsonSerializer.Serialize(new[]
            {
                new { translations = new[] { new { text = "Sample", to = expectedTargetCode } } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, targetLang);
        var result = await provider.TranslateAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Contains($"to={expectedTargetCode}", capturedRequest!.RequestUri!.ToString());
        Assert.Contains("from=en", capturedRequest.RequestUri.ToString());
    }

    [Theory]
    [InlineData(Language.En, "EN-US")]
    [InlineData(Language.Pt, "PT-PT")]
    [InlineData(Language.Zh, "ZH")]
    [InlineData(Language.Ja, "JA")]
    [InlineData(Language.De, "DE")]
    [InlineData(Language.Es, "ES")]
    [InlineData(Language.Fr, "FR")]
    [InlineData(Language.It, "IT")]
    [InlineData(Language.Ru, "RU")]
    public async Task DeepLProvider_MapsTargetLanguageCodesCorrectly(Language targetLang, string expectedTargetCode)
    {
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.DeepL, "test-key");

        string? capturedPayload = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedPayload = req.Content?.ReadAsStringAsync().Result;
            var jsonResponse = JsonSerializer.Serialize(new
            {
                translations = new[] { new { text = "Sample", detected_source_language = "RU" } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new DeepLProvider(client, secretStore);

        var request = new TranslationRequest("Текст", Language.Ru, targetLang);
        var result = await provider.TranslateAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(capturedPayload);
        Assert.Contains($"\"target_lang\":\"{expectedTargetCode}\"", capturedPayload);
        Assert.Contains("\"source_lang\":\"RU\"", capturedPayload);
    }

    [Theory]
    [InlineData(Language.Zh, "zh")]
    [InlineData(Language.Ja, "ja")]
    [InlineData(Language.De, "de")]
    [InlineData(Language.Es, "es")]
    [InlineData(Language.Fr, "fr")]
    [InlineData(Language.It, "it")]
    [InlineData(Language.Pt, "pt")]
    public async Task GoogleTranslateProvider_MapsLanguageCodesCorrectly(Language targetLang, string expectedTargetCode)
    {
        var secretStore = new FakeSecretStore();
        secretStore.SaveSecret(ProviderIdentifiers.Google, "test-key");

        string? capturedPayload = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedPayload = req.Content?.ReadAsStringAsync().Result;
            var jsonResponse = JsonSerializer.Serialize(new
            {
                data = new { translations = new[] { new { translatedText = "Sample" } } }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleTranslateProvider(client, secretStore);

        var request = new TranslationRequest("Hello", Language.En, targetLang);
        var result = await provider.TranslateAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(capturedPayload);
        Assert.Contains($"\"target\":\"{expectedTargetCode}\"", capturedPayload);
        Assert.Contains("\"source\":\"en\"", capturedPayload);
    }
}

