namespace Flow.Domain;

public record TranslationRequest(
    string SourceText,
    Language SourceLanguage,
    Language TargetLanguage);
