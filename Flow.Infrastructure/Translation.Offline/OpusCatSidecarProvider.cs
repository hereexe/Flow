using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Infrastructure.Translation.Offline;

public class OpusCatSidecarProvider : ITranslationProvider
{
    private readonly IOpusCatProcessManager _processManager;
    private readonly HttpClient _httpClient;
    private readonly OpusCatOptions _options;

    public string ProviderId => ProviderIdentifiers.OpusCat;

    public OpusCatSidecarProvider(
        IOpusCatProcessManager processManager,
        HttpClient httpClient,
        OpusCatOptions options)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return _processManager.CheckHealthAsync(ct);
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsEmpty)
        {
            return TranslationResult.Fail("Source text is empty.", ProviderId);
        }

        // Lazy start process on first translation invocation
        try
        {
            await _processManager.EnsureStartedAsync(ct);
        }
        catch (OpusCatException ex)
        {
            return TranslationResult.Fail(ex.Message, ProviderId);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail($"Ошибка инициализации офлайн-переводчика: {ex.Message}", ProviderId);
        }

        try
        {
            var payload = new OpusCatTranslateRequest
            {
                Text = request.SourceText,
                SourceLanguage = request.SourceLanguage.ToString().ToLowerInvariant(),
                TargetLanguage = request.TargetLanguage.ToString().ToLowerInvariant()
            };

            var response = await _httpClient.PostAsJsonAsync($"{_options.BaseUrl}/translate", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return TranslationResult.Fail(
                    $"Ошибка сервиса перевода (HTTP code: {(int)response.StatusCode}).",
                    ProviderId);
            }

            var resultDto = await response.Content.ReadFromJsonAsync<OpusCatTranslateResponse>(cancellationToken: ct);
            if (resultDto == null || string.IsNullOrEmpty(resultDto.TranslatedText))
            {
                // Fallback: try raw text reading if response wasn't JSON object
                string rawText = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return TranslationResult.Ok(rawText.Trim(), ProviderId);
                }

                return TranslationResult.Fail("Получен пустой ответ от офлайн-переводчика.", ProviderId);
            }

            return TranslationResult.Ok(resultDto.TranslatedText, ProviderId);
        }
        catch (HttpRequestException ex)
        {
            return TranslationResult.Fail($"Ошибка связи с локальным офлайн-сервисом: {ex.Message}", ProviderId);
        }
        catch (TaskCanceledException)
        {
            return TranslationResult.Fail("Превышено время ожидания ответа от офлайн-переводчика.", ProviderId);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail($"Ошибка при выполнении перевода: {ex.Message}", ProviderId);
        }
    }

    private class OpusCatTranslateRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("sourceLanguage")]
        public string SourceLanguage { get; set; } = string.Empty;

        [JsonPropertyName("targetLanguage")]
        public string TargetLanguage { get; set; } = string.Empty;
    }

    private class OpusCatTranslateResponse
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}
