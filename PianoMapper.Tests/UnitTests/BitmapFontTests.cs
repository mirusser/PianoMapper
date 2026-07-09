using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class BitmapFontTests
{
    [Theory]
    [InlineData('A')]
    [InlineData('9')]
    [InlineData('#')]
    [InlineData('/')]
    [InlineData(' ')]
    [InlineData('?')]
    public void GetGlyph_AnyInput_ReturnsGridOfExpectedSizeWithoutThrowing(char c)
    {
        var glyph = BitmapFont.GetGlyph(c);

        Assert.Equal(BitmapFont.GlyphWidth * BitmapFont.GlyphHeight, glyph.Count);
    }

    [Fact]
    public void GetGlyph_UnknownCharacter_ReturnsBlankGlyph()
    {
        var glyph = BitmapFont.GetGlyph('?');

        Assert.DoesNotContain(true, glyph);
    }

    [Fact]
    public void GetGlyph_Space_ReturnsBlankGlyph()
    {
        var glyph = BitmapFont.GetGlyph(' ');

        Assert.DoesNotContain(true, glyph);
    }

    [Fact]
    public void GetGlyph_LowercaseLetter_MatchesUppercaseGlyph()
    {
        var lower = BitmapFont.GetGlyph('c');
        var upper = BitmapFont.GetGlyph('C');

        Assert.Equal(upper, lower);
    }

    [Fact]
    public void GetGlyph_LetterWithLitPixels_HasAtLeastOneLitPixel()
    {
        var glyph = BitmapFont.GetGlyph('A');

        Assert.Contains(true, glyph);
    }

    [Fact]
    public void GetGlyph_Slash_HasAtLeastOneLitPixel()
    {
        var glyph = BitmapFont.GetGlyph('/');

        Assert.Contains(true, glyph);
    }
}
