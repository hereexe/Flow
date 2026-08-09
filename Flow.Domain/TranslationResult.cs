namespace Flow.Domain;

public record TranslationResult(
    bool Success,
    string TranslatedText,
    string? ErrorMessage = null,
    string? ProviderId = null,
    long ExecutionTimeMs = 0)
{
    public static TranslationResult Ok(string translatedText, string? providerId = null, long executionTimeMs = 0) =>
        new(true, translatedText, null, providerId, executionTimeMs);

    public static TranslationResult Fail(string errorMessage, string? providerId = null, long executionTimeMs = 0) =>
        new(false, string.Empty, errorMessage, providerId, executionTimeMs);
}

