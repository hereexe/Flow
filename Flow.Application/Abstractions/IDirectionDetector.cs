using Flow.Domain;

namespace Flow.Application.Abstractions;

/// <summary>
/// Detects source language from input text (Cyrillic vs Latin character distribution)
/// and resolves target translation direction (RU -> EN or EN -> RU) with fallback options.
/// </summary>
public interface IDirectionDetector
{
    (Language Source, Language Target) DetectDirection(string text, Language defaultSource = Language.Ru, Language defaultTarget = Language.En);
    Language DetectLanguage(string text, Language fallback = Language.Ru);
}
