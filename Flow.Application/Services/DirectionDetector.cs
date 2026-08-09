using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Application.Services;

/// <summary>
/// Detects source language from input text by comparing Cyrillic vs Latin character counts
/// and resolves the translation direction (RU → EN or EN → RU) with configurable fallback.
/// </summary>
public class DirectionDetector : IDirectionDetector
{
    /// <inheritdoc />
    public (Language Source, Language Target) DetectDirection(
        string text,
        Language defaultSource = Language.Ru,
        Language defaultTarget = Language.En)
    {
        var source = DetectLanguage(text, defaultSource);

        // If detection fell back to default (ambiguous input), use the caller's defaults as-is
        if (source == defaultSource)
        {
            return (defaultSource, defaultTarget);
        }

        // Otherwise, target is the opposite language
        var target = source == Language.Ru ? Language.En : Language.Ru;
        return (source, target);
    }

    /// <inheritdoc />
    public Language DetectLanguage(string text, Language fallback = Language.Ru)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        const int maxSampleLength = 256;
        int limit = Math.Min(text.Length, maxSampleLength);

        int cyrillicCount = 0;
        int latinCount = 0;

        for (int i = 0; i < limit; i++)
        {
            char c = text[i];
            if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё')
                cyrillicCount++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                latinCount++;
        }

        if (cyrillicCount > latinCount) return Language.Ru;
        if (latinCount > cyrillicCount) return Language.En;
        return fallback;
    }
}
