using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Application.Services;

/// <summary>
/// Detects source language from input text using multi-script analysis (Cyrillic, CJK, Kana, Latin)
/// and Latin diacritics heuristics, resolving translation direction between configured language pairs.
/// </summary>
public class DirectionDetector : IDirectionDetector
{
    private const int MaxSampleLength = 256;

    /// <inheritdoc />
    public (Language Source, Language Target) DetectDirection(
        string text,
        Language defaultSource = Language.Ru,
        Language defaultTarget = Language.En)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (defaultSource, defaultTarget);

        int limit = Math.Min(text.Length, MaxSampleLength);
        int cyrillicCount = 0;
        int latinCount = 0;
        int kanaCount = 0;
        int cjkCount = 0;

        for (int i = 0; i < limit; i++)
        {
            char c = text[i];
            if (IsCyrillic(c))
                cyrillicCount++;
            else if (IsKana(c))
                kanaCount++;
            else if (IsCjkIdeograph(c))
                cjkCount++;
            else if (IsLatinLetter(c) || IsLatinDiacritic(c))
                latinCount++;
        }

        // If no script characters found at all, return default direction
        if (cyrillicCount == 0 && latinCount == 0 && kanaCount == 0 && cjkCount == 0)
        {
            return (defaultSource, defaultTarget);
        }

        Language detected;

        if (kanaCount > 0)
        {
            detected = Language.Ja;
        }
        else if (cjkCount > 0)
        {
            // If Japanese is in the pair and Chinese is not, treat Kanji as Japanese
            if ((defaultSource == Language.Ja || defaultTarget == Language.Ja) &&
                defaultSource != Language.Zh && defaultTarget != Language.Zh)
            {
                detected = Language.Ja;
            }
            else
            {
                detected = Language.Zh;
            }
        }
        else if (cyrillicCount > 0 && cyrillicCount == latinCount)
        {
            return (defaultSource, defaultTarget);
        }
        else if (cyrillicCount > latinCount)
        {
            detected = Language.Ru;
        }
        else // Latin or Latin diacritics
        {
            bool sourceIsLatin = IsLatinLanguage(defaultSource);
            bool targetIsLatin = IsLatinLanguage(defaultTarget);

            if (sourceIsLatin && targetIsLatin)
            {
                var sourceScore = GetDiacriticScore(text, limit, defaultSource);
                var targetScore = GetDiacriticScore(text, limit, defaultTarget);

                if (targetScore > sourceScore && targetScore > 0)
                {
                    detected = defaultTarget;
                }
                else if (sourceScore > targetScore && sourceScore > 0)
                {
                    detected = defaultSource;
                }
                else
                {
                    // Ambiguous Latin (e.g. plain ASCII without distinct diacritics)
                    return (defaultSource, defaultTarget);
                }
            }
            else if (targetIsLatin)
            {
                detected = defaultTarget;
            }
            else if (sourceIsLatin)
            {
                detected = defaultSource;
            }
            else
            {
                detected = Language.En;
            }
        }

        if (detected == defaultTarget)
        {
            return (defaultTarget, defaultSource);
        }

        if (detected == defaultSource)
        {
            return (defaultSource, defaultTarget);
        }

        // Detected language is outside the configured pair
        return (defaultSource, defaultTarget);
    }

    /// <inheritdoc />
    public Language DetectLanguage(string text, Language fallback = Language.Ru)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        int limit = Math.Min(text.Length, MaxSampleLength);
        int cyrillicCount = 0;
        int latinCount = 0;
        int kanaCount = 0;
        int cjkCount = 0;

        for (int i = 0; i < limit; i++)
        {
            char c = text[i];
            if (IsCyrillic(c))
                cyrillicCount++;
            else if (IsKana(c))
                kanaCount++;
            else if (IsCjkIdeograph(c))
                cjkCount++;
            else if (IsLatinLetter(c) || IsLatinDiacritic(c))
                latinCount++;
        }

        if (kanaCount > 0)
            return Language.Ja;

        if (cjkCount > 0)
            return fallback == Language.Ja ? Language.Ja : Language.Zh;

        if (cyrillicCount > 0 && cyrillicCount == latinCount)
            return fallback;

        if (cyrillicCount > latinCount)
            return Language.Ru;

        if (latinCount > cyrillicCount)
        {
            var deScore = GetDiacriticScore(text, limit, Language.De);
            var esScore = GetDiacriticScore(text, limit, Language.Es);
            var frScore = GetDiacriticScore(text, limit, Language.Fr);
            var ptScore = GetDiacriticScore(text, limit, Language.Pt);
            var itScore = GetDiacriticScore(text, limit, Language.It);

            double maxScore = Math.Max(deScore, Math.Max(esScore, Math.Max(frScore, Math.Max(ptScore, itScore))));
            if (maxScore > 0)
            {
                if (deScore == maxScore) return Language.De;
                if (ptScore == maxScore) return Language.Pt;
                if (esScore == maxScore) return Language.Es;
                if (frScore == maxScore) return Language.Fr;
                if (itScore == maxScore) return Language.It;
            }

            return IsLatinLanguage(fallback) ? fallback : Language.En;
        }

        return fallback;
    }

    private static bool IsCyrillic(char c) =>
        (c >= '\u0400' && c <= '\u04FF') || c == 'ё' || c == 'Ё';

    private static bool IsKana(char c) =>
        (c >= '\u3040' && c <= '\u309F') || (c >= '\u30A0' && c <= '\u30FF');

    private static bool IsCjkIdeograph(char c) =>
        (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\u3400' && c <= '\u4DBF');

    private static bool IsLatinLetter(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static bool IsLatinLanguage(Language lang) =>
        lang is Language.En or Language.Es or Language.De or Language.Fr or Language.Pt or Language.It;

    private static bool IsLatinDiacritic(char c) =>
        "äöüßÄÖÜñÑ¿¡áéíóúÁÉÍÓÚçÇœŒæÆ«»êÊëËâÂîÎïÏôÔûÛÿŸãÃõÕàÀèÈìÌòÒùÙ".Contains(c);

    private static double GetDiacriticScore(string text, int limit, Language lang)
    {
        double score = 0;
        for (int i = 0; i < limit; i++)
        {
            char c = text[i];
            switch (lang)
            {
                case Language.De:
                    if ("äöüßÄÖÜ".Contains(c)) score += 2.0;
                    break;

                case Language.Es:
                    if ("ñÑ¿¡".Contains(c)) score += 3.0;
                    else if ("áéíóúÁÉÍÓÚ".Contains(c)) score += 1.0;
                    break;

                case Language.Fr:
                    if ("œŒæÆ«»".Contains(c)) score += 3.0;
                    else if ("çÇêÊëËâÂîÎïÏôÔûÛÿŸ".Contains(c)) score += 2.0;
                    else if ("éÉèÈàÀùÙ".Contains(c)) score += 1.0;
                    break;

                case Language.Pt:
                    if ("ãÃõÕ".Contains(c)) score += 3.0;
                    else if ("çÇâÂêÊôÔ".Contains(c)) score += 2.0;
                    else if ("áÁéÉíÍóÓúÚàÀ".Contains(c)) score += 1.0;
                    break;

                case Language.It:
                    if ("ìÌòÒùÙ".Contains(c)) score += 2.0;
                    else if ("àÀèÈéÉ".Contains(c)) score += 1.0;
                    break;
            }
        }
        return score;
    }
}
