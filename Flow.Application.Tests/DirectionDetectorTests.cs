using Flow.Application.Abstractions;
using Flow.Application.Services;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class DirectionDetectorTests
{
    private readonly DirectionDetector _detector = new();

    // --- DetectDirection tests ---

    [Theory]
    [InlineData("Привет мир", Language.Ru, Language.En)]
    [InlineData("Здравствуйте! Как дела?", Language.Ru, Language.En)]
    [InlineData("Hello world", Language.En, Language.Ru)]
    [InlineData("How are you doing today?", Language.En, Language.Ru)]
    public void DetectDirection_WithClearLanguage_ResolvesCorrectDirection(string text, Language expectedSource, Language expectedTarget)
    {
        var (source, target) = _detector.DetectDirection(text);

        Assert.Equal(expectedSource, source);
        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void DetectDirection_WithAmbiguousNumbersOrSymbols_FallsBackToDefaultDirection()
    {
        string ambiguousText = "12345 !@#$%";

        var (source, target) = _detector.DetectDirection(ambiguousText, defaultSource: Language.Ru, defaultTarget: Language.En);

        Assert.Equal(Language.Ru, source);
        Assert.Equal(Language.En, target);
    }

    [Fact]
    public void DetectDirection_WithAmbiguousText_UsesCustomDefaults()
    {
        // When defaults are flipped, ambiguous text should return flipped defaults
        var (source, target) = _detector.DetectDirection("12345", defaultSource: Language.En, defaultTarget: Language.Ru);

        Assert.Equal(Language.En, source);
        Assert.Equal(Language.Ru, target);
    }

    [Fact]
    public void DetectDirection_RussianText_ProducesRuToEn()
    {
        var (source, target) = _detector.DetectDirection("Тестирование системы перевода");

        Assert.Equal(Language.Ru, source);
        Assert.Equal(Language.En, target);
    }

    [Fact]
    public void DetectDirection_EnglishText_ProducesEnToRu()
    {
        var (source, target) = _detector.DetectDirection("Testing the translation system");

        Assert.Equal(Language.En, source);
        Assert.Equal(Language.Ru, target);
    }

    // --- DetectLanguage tests ---

    [Theory]
    [InlineData("Привет")]
    [InlineData("Мир")]
    [InlineData("Тестирование")]
    public void DetectLanguage_PureCyrillicText_ReturnsRu(string text)
    {
        Assert.Equal(Language.Ru, _detector.DetectLanguage(text));
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("World")]
    [InlineData("Testing")]
    public void DetectLanguage_PureLatinText_ReturnsEn(string text)
    {
        Assert.Equal(Language.En, _detector.DetectLanguage(text));
    }

    [Fact]
    public void DetectLanguage_EmptyString_ReturnsFallback()
    {
        Assert.Equal(Language.Ru, _detector.DetectLanguage(""));
        Assert.Equal(Language.En, _detector.DetectLanguage("", Language.En));
    }

    [Fact]
    public void DetectLanguage_WhitespaceOnly_ReturnsFallback()
    {
        Assert.Equal(Language.Ru, _detector.DetectLanguage("   \t\n"));
    }

    [Fact]
    public void DetectLanguage_DigitsAndSymbolsOnly_ReturnsFallback()
    {
        Assert.Equal(Language.Ru, _detector.DetectLanguage("12345 !@#$%"));
        Assert.Equal(Language.En, _detector.DetectLanguage("98765 &*()", Language.En));
    }

    [Fact]
    public void DetectLanguage_EqualCyrillicAndLatinCounts_ReturnsFallback()
    {
        // "аб" = 2 Cyrillic, "cd" = 2 Latin → equal → fallback
        Assert.Equal(Language.Ru, _detector.DetectLanguage("абcd"));
        Assert.Equal(Language.En, _detector.DetectLanguage("абcd", Language.En));
    }

    [Theory]
    [InlineData("ёжик")]
    [InlineData("Ёлка")]
    [InlineData("объём")]
    public void DetectLanguage_TextWithYoCharacter_CountsAsCyrillic(string text)
    {
        Assert.Equal(Language.Ru, _detector.DetectLanguage(text));
    }

    [Fact]
    public void DetectLanguage_MixedWithCyrillicDominant_ReturnsRu()
    {
        // "Привет world" — 6 Cyrillic, 5 Latin → Ru
        Assert.Equal(Language.Ru, _detector.DetectLanguage("Привет world"));
    }

    [Fact]
    public void DetectLanguage_MixedWithLatinDominant_ReturnsEn()
    {
        // "Hello мир" — 5 Latin, 3 Cyrillic → En
        Assert.Equal(Language.En, _detector.DetectLanguage("Hello мир"));
    }
}

