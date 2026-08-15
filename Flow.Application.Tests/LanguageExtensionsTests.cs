using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class LanguageExtensionsTests
{
    [Theory]
    [InlineData(Language.Ru, "ru")]
    [InlineData(Language.En, "en")]
    [InlineData(Language.Es, "es")]
    [InlineData(Language.De, "de")]
    [InlineData(Language.Fr, "fr")]
    [InlineData(Language.Pt, "pt")]
    [InlineData(Language.It, "it")]
    [InlineData(Language.Zh, "zh")]
    [InlineData(Language.Ja, "ja")]
    public void ToIsoCode_ReturnsCorrectTwoLetterCode(Language language, string expectedIso)
    {
        Assert.Equal(expectedIso, language.ToIsoCode());
    }

    [Theory]
    [InlineData(Language.Ru, "Russian")]
    [InlineData(Language.En, "English")]
    [InlineData(Language.Es, "Spanish")]
    [InlineData(Language.De, "German")]
    [InlineData(Language.Fr, "French")]
    [InlineData(Language.Pt, "Portuguese")]
    [InlineData(Language.It, "Italian")]
    [InlineData(Language.Zh, "Chinese")]
    [InlineData(Language.Ja, "Japanese")]
    public void ToDisplayName_ReturnsFullEnglishName(Language language, string expectedDisplayName)
    {
        Assert.Equal(expectedDisplayName, language.ToDisplayName());
    }

    [Theory]
    [InlineData("ru", Language.Ru)]
    [InlineData("RU", Language.Ru)]
    [InlineData("Russian", Language.Ru)]
    [InlineData("русский", Language.Ru)]
    [InlineData("en", Language.En)]
    [InlineData("English", Language.En)]
    [InlineData("английский", Language.En)]
    [InlineData("es", Language.Es)]
    [InlineData("Spanish", Language.Es)]
    [InlineData("испанский", Language.Es)]
    [InlineData("de", Language.De)]
    [InlineData("German", Language.De)]
    [InlineData("немецкий", Language.De)]
    [InlineData("fr", Language.Fr)]
    [InlineData("French", Language.Fr)]
    [InlineData("французский", Language.Fr)]
    [InlineData("pt", Language.Pt)]
    [InlineData("Portuguese", Language.Pt)]
    [InlineData("португальский", Language.Pt)]
    [InlineData("it", Language.It)]
    [InlineData("Italian", Language.It)]
    [InlineData("итальянский", Language.It)]
    [InlineData("zh", Language.Zh)]
    [InlineData("Chinese", Language.Zh)]
    [InlineData("китайский", Language.Zh)]
    [InlineData("ja", Language.Ja)]
    [InlineData("Japanese", Language.Ja)]
    [InlineData("японский", Language.Ja)]
    public void TryParseIsoCode_ValidInputs_ReturnsTrueAndCorrectLanguage(string input, Language expected)
    {
        Assert.True(LanguageExtensions.TryParseIsoCode(input, out var lang));
        Assert.Equal(expected, lang);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("klingon")]
    [InlineData("123")]
    public void TryParseIsoCode_InvalidInputs_ReturnsFalse(string? input)
    {
        Assert.False(LanguageExtensions.TryParseIsoCode(input, out _));
    }

    [Fact]
    public void ParseIsoCode_InvalidInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => LanguageExtensions.ParseIsoCode("unknown_lang"));
    }
}
