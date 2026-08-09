namespace Flow.Domain;

public record TranslationRequest
{
    public string SourceText { get; init; }
    public Language SourceLanguage { get; init; }
    public Language TargetLanguage { get; init; }
    public DateTime TimestampUtc { get; init; }

    public TranslationRequest(
        string sourceText,
        Language sourceLanguage,
        Language targetLanguage,
        DateTime? timestampUtc = null)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        SourceLanguage = sourceLanguage;
        TargetLanguage = targetLanguage;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(SourceText);
}

