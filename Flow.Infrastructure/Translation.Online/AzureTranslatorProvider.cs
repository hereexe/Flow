using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Infrastructure.Translation.Online;

public class AzureTranslatorProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;

    public string ProviderId => ProviderIdentifiers.Azure;
    public string Region { get; set; } = "global";

    public AzureTranslatorProvider(HttpClient httpClient, ISecretStore secretStore)
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
            var sourceLang = request.SourceLanguage.ToString().ToLowerInvariant();
            var targetLang = request.TargetLanguage.ToString().ToLowerInvariant();

            var url = $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from={sourceLang}&to={targetLang}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            if (!string.IsNullOrWhiteSpace(Region) && !Region.Equals("global", StringComparison.OrdinalIgnoreCase))
            {
                httpRequest.Headers.Add("Ocp-Apim-Subscription-Region", Region);
            }

            var body = new[] { new { Text = request.SourceText } };
            httpRequest.Content = JsonContent.Create(body);

            var response = await _httpClient.SendAsync(httpRequest, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return TranslationResult.Fail($"Недействительный API-ключ для {ProviderId}.", ProviderId);
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                return TranslationResult.Fail($"Превышен лимит запросов (квота) к {ProviderId}.", ProviderId);
            }

            if (!response.IsSuccessStatusCode)
            {
                return TranslationResult.Fail($"Ошибка сервиса перевода {ProviderId} (HTTP code: {(int)response.StatusCode}).", ProviderId);
            }

            var responseArray = await response.Content.ReadFromJsonAsync<AzureTranslateResponse[]>(cancellationToken: ct);
            var translatedText = responseArray?.FirstOrDefault()?.Translations?.FirstOrDefault()?.Text;

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

    private class AzureTranslateResponse
    {
        [JsonPropertyName("translations")]
        public AzureTranslationItem[]? Translations { get; set; }
    }

    private class AzureTranslationItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }
    }
}
