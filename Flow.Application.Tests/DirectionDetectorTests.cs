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

    [Theory]
    [InlineData("こんにちは", Language.En, Language.Ja, Language.Ja, Language.En)]
    [InlineData("ラーメンを食べます", Language.En, Language.Ja, Language.Ja, Language.En)]
    [InlineData("Hello there", Language.En, Language.Ja, Language.En, Language.Ja)]
    [InlineData("你好世界", Language.En, Language.Zh, Language.Zh, Language.En)]
    [InlineData("Schöne Grüße", Language.En, Language.De, Language.De, Language.En)]
    [InlineData("¿Cómo estás?", Language.En, Language.Es, Language.Es, Language.En)]
    [InlineData("Bonjour, ça va?", Language.En, Language.Fr, Language.Fr, Language.En)]
    [InlineData("Olá, não posso", Language.En, Language.Pt, Language.Pt, Language.En)]
    [InlineData("Città e università", Language.En, Language.It, Language.It, Language.En)]
    public void DetectDirection_MultiLanguagePairs_ResolvesCorrectDirection(
        string text, Language defaultSource, Language defaultTarget, Language expectedSource, Language expectedTarget)
    {
        var (source, target) = _detector.DetectDirection(text, defaultSource, defaultTarget);

        Assert.Equal(expectedSource, source);
        Assert.Equal(expectedTarget, target);
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

    [Theory]
    [InlineData("こんにちは", Language.Ja)]
    [InlineData("カタカナ", Language.Ja)]
    [InlineData("日本語", Language.Zh)] // Pure CJK ideographs default to Zh
    public void DetectLanguage_AsianScripts_IdentifiedCorrectly(string text, Language expected)
    {
        Assert.Equal(expected, _detector.DetectLanguage(text));
    }

    [Theory]
    [InlineData("Schöne Grüße aus München", Language.De)]
    [InlineData("¿Cómo estás Señor?", Language.Es)]
    [InlineData("C'est une belle journée «française»", Language.Fr)]
    [InlineData("Não tenho informações sobre isso", Language.Pt)]
    [InlineData("Così è la vita, più o meno", Language.It)]
    public void DetectLanguage_LatinWithDiacritics_IdentifiedCorrectly(string text, Language expected)
    {
        Assert.Equal(expected, _detector.DetectLanguage(text));
    }
}
