using Flow.Domain;

namespace Flow.Application.Abstractions;

/// <summary>
/// Detects source language from input text (multi-script analysis and diacritics heuristics)
/// and resolves translation direction between configured language pairs with fallback options.
/// </summary>
public interface IDirectionDetector
{
    (Language Source, Language Target) DetectDirection(string text, Language defaultSource = Language.Ru, Language defaultTarget = Language.En);
    Language DetectLanguage(string text, Language fallback = Language.Ru);
}

