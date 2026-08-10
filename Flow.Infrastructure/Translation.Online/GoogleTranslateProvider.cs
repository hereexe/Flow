using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Infrastructure.Translation.Online;

public class GoogleTranslateProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;

    public string ProviderId => ProviderIdentifiers.Google;

    public GoogleTranslateProvider(HttpClient httpClient, ISecretStore secretStore)
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
            var url = $"https://translation.googleapis.com/language/translate/v2?key={apiKey}";

            var payload = new GoogleTranslateRequest
            {
                Query = request.SourceText,
                SourceLanguage = request.SourceLanguage.ToString().ToLowerInvariant(),
                TargetLanguage = request.TargetLanguage.ToString().ToLowerInvariant(),
                Format = "text"
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };

            var response = await _httpClient.SendAsync(httpRequest, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            {
                return TranslationResult.Fail($"Недействительный API-ключ или параметры для {ProviderId}.", ProviderId);
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                return TranslationResult.Fail($"Превышен лимит запросов (квота) к {ProviderId}.", ProviderId);
            }

            if (!response.IsSuccessStatusCode)
            {
                return TranslationResult.Fail($"Ошибка сервиса перевода {ProviderId} (HTTP code: {(int)response.StatusCode}).", ProviderId);
            }

            var resultDto = await response.Content.ReadFromJsonAsync<GoogleTranslateResponse>(cancellationToken: ct);
            var translatedText = resultDto?.Data?.Translations?.FirstOrDefault()?.TranslatedText;

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

    private class GoogleTranslateRequest
    {
        [JsonPropertyName("q")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string SourceLanguage { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string TargetLanguage { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = "text";
    }

    private class GoogleTranslateResponse
    {
        [JsonPropertyName("data")]
        public GoogleTranslationData? Data { get; set; }
    }

    private class GoogleTranslationData
    {
        [JsonPropertyName("translations")]
        public GoogleTranslationItem[]? Translations { get; set; }
    }

    private class GoogleTranslationItem
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}
