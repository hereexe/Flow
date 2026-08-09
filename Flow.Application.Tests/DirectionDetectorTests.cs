using Flow.Application.Abstractions;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class DirectionDetectorTests
{
    private class SimpleDirectionDetector : IDirectionDetector
    {
        public (Language Source, Language Target) DetectDirection(string text, Language defaultSource = Language.Ru, Language defaultTarget = Language.En)
        {
            var source = DetectLanguage(text, defaultSource);
            var target = source == Language.Ru ? Language.En : Language.Ru;
            return (source, target);
        }

        public Language DetectLanguage(string text, Language fallback = Language.Ru)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            int cyrillicCount = 0;
            int latinCount = 0;

            foreach (char c in text)
            {
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

    [Theory]
    [InlineData("Привет мир", Language.Ru, Language.En)]
    [InlineData("Здравствуйте! Как дела?", Language.Ru, Language.En)]
    [InlineData("Hello world", Language.En, Language.Ru)]
    [InlineData("How are you doing today?", Language.En, Language.Ru)]
    public void DetectDirection_WithClearLanguage_ResolvesCorrectDirection(string text, Language expectedSource, Language expectedTarget)
    {
        // Arrange
        IDirectionDetector detector = new SimpleDirectionDetector();

        // Act
        var (source, target) = detector.DetectDirection(text);

        // Assert
        Assert.Equal(expectedSource, source);
        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void DetectDirection_WithAmbiguousNumbersOrSymbols_FallsBackToDefaultDirection()
    {
        // Arrange
        IDirectionDetector detector = new SimpleDirectionDetector();
        string ambiguousText = "12345 !@#$%";

        // Act
        var (source, target) = detector.DetectDirection(ambiguousText, defaultSource: Language.Ru, defaultTarget: Language.En);

        // Assert
        Assert.Equal(Language.Ru, source);
        Assert.Equal(Language.En, target);
    }
}
