namespace Flow.Domain;

public static class LanguageExtensions
{
    public static string ToIsoCode(this Language language) => language switch
    {
        Language.Ru => "ru",
        Language.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported language")
    };

    public static string ToDisplayName(this Language language) => language switch
    {
        Language.Ru => "Russian",
        Language.En => "English",
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

            default:
                language = default;
                return false;
        }
    }
}
