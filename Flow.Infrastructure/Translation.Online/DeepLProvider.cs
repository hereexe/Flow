using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Infrastructure.Translation.Online;

public class DeepLProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;

    public string ProviderId => ProviderIdentifiers.DeepL;

    public DeepLProvider(HttpClient httpClient, ISecretStore secretStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var apiKey = _secretStore.GetSecret(ProviderId);
        return Task.FromResult(!string.IsNullOrWhiteSpace(apiKey));
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsEmpty)
        {
            return TranslationResult.Fail("Source text is empty.", ProviderId);
        }

        var apiKey = _secretStore.GetSecret(ProviderId);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TranslationResult.Fail($"API-ключ для провайдера {ProviderId} не настроен. Укажите ключ в Настройках.", ProviderId);
        }

        try
        {
            var host = apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
                ? "https://api-free.deepl.com"
                : "https://api.deepl.com";

            var url = $"{host}/v2/translate";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"DeepL-Auth-Key {apiKey}");

            var payload = new DeepLTranslateRequest
            {
                Text = new[] { request.SourceText },
                SourceLanguage = MapSourceLanguage(request.SourceLanguage),
                TargetLanguage = MapTargetLanguage(request.TargetLanguage)
            };

            httpRequest.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(httpRequest, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return TranslationResult.Fail($"Недействительный API-ключ для {ProviderId}.", ProviderId);
            }

            if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == (HttpStatusCode)456)
            {
                return TranslationResult.Fail($"Превышен лимит запросов (квота) к {ProviderId}.", ProviderId);
            }

            if (!response.IsSuccessStatusCode)
            {
                return TranslationResult.Fail($"Ошибка сервиса перевода {ProviderId} (HTTP code: {(int)response.StatusCode}).", ProviderId);
            }

            var resultDto = await response.Content.ReadFromJsonAsync<DeepLTranslateResponse>(cancellationToken: ct);
            var translatedText = resultDto?.Translations?.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(translatedText))
            {
                return TranslationResult.Fail($"Получен пустой ответ от сервиса {ProviderId}.", ProviderId);
            }

            return TranslationResult.Ok(translatedText, ProviderId);
        }
        catch (HttpRequestException ex)
        {
            return TranslationResult.Fail($"Ошибка связи с сервером {ProviderId}: {ex.Message}", ProviderId);
        }
        catch (TaskCanceledException)
        {
            return TranslationResult.Fail($"Превышено время ожидания ответа от сервиса {ProviderId}.", ProviderId);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail($"Ошибка при выполнении перевода {ProviderId}: {ex.Message}", ProviderId);
        }
    }

    private static string MapSourceLanguage(Language language) => language switch
    {
        Language.Ru => "RU",
        Language.En => "EN",
        Language.Es => "ES",
        Language.De => "DE",
        Language.Fr => "FR",
        Language.Pt => "PT",
        Language.It => "IT",
        Language.Zh => "ZH",
        Language.Ja => "JA",
        _ => language.ToIsoCode().ToUpperInvariant()
    };

    private static string MapTargetLanguage(Language language) => language switch
    {
        Language.En => "EN-US",
        Language.Pt => "PT-PT",
        Language.Ru => "RU",
        Language.Es => "ES",
        Language.De => "DE",
        Language.Fr => "FR",
        Language.It => "IT",
        Language.Zh => "ZH",
        Language.Ja => "JA",
        _ => language.ToIsoCode().ToUpperInvariant()
    };

    private class DeepLTranslateRequest
    {
        [JsonPropertyName("text")]
        public string[] Text { get; set; } = Array.Empty<string>();

        [JsonPropertyName("source_lang")]
        public string SourceLanguage { get; set; } = string.Empty;

        [JsonPropertyName("target_lang")]
        public string TargetLanguage { get; set; } = string.Empty;
    }

    private class DeepLTranslateResponse
    {
        [JsonPropertyName("translations")]
        public DeepLTranslationItem[]? Translations { get; set; }
    }

    private class DeepLTranslationItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("detected_source_language")]
        public string? DetectedSourceLanguage { get; set; }
    }
}
