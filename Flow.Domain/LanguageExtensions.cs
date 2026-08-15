namespace Flow.Domain;

public static class LanguageExtensions
{
    public static string ToIsoCode(this Language language) => language switch
    {
        Language.Ru => "ru",
        Language.En => "en",
        Language.Es => "es",
        Language.De => "de",
        Language.Fr => "fr",
        Language.Pt => "pt",
        Language.It => "it",
        Language.Zh => "zh",
        Language.Ja => "ja",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported language")
    };

    public static string ToDisplayName(this Language language) => language switch
    {
        Language.Ru => "Russian",
        Language.En => "English",
        Language.Es => "Spanish",
        Language.De => "German",
        Language.Fr => "French",
        Language.Pt => "Portuguese",
        Language.It => "Italian",
        Language.Zh => "Chinese",
        Language.Ja => "Japanese",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported language")
    };

    public static Language ParseIsoCode(string input)
    {
        if (TryParseIsoCode(input, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Unsupported language identifier: '{input}'", nameof(input));
    }

    public static bool TryParseIsoCode(string? input, out Language language)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            language = default;
            return false;
        }

        var normalized = input.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "ru":
            case "russian":
            case "русский":
                language = Language.Ru;
                return true;

            case "en":
            case "english":
            case "английский":
                language = Language.En;
                return true;

            case "es":
            case "spanish":
            case "испанский":
                language = Language.Es;
                return true;

            case "de":
            case "german":
            case "немецкий":
                language = Language.De;
                return true;

            case "fr":
            case "french":
            case "французский":
                language = Language.Fr;
                return true;

            case "pt":
            case "portuguese":
            case "португальский":
                language = Language.Pt;
                return true;

            case "it":
            case "italian":
            case "итальянский":
                language = Language.It;
                return true;

            case "zh":
            case "chinese":
            case "китайский":
                language = Language.Zh;
                return true;

            case "ja":
            case "japanese":
            case "японский":
                language = Language.Ja;
                return true;

            default:
                language = default;
                return false;
        }
    }
}

