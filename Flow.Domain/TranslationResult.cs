namespace Flow.Domain;

public record TranslationResult(
    bool Success,
    string TranslatedText,
    string? ErrorMessage = null)
{
    public static TranslationResult Ok(string translatedText) =>
        new(true, translatedText);

    public static TranslationResult Fail(string errorMessage) =>
        new(false, string.Empty, errorMessage);
}
