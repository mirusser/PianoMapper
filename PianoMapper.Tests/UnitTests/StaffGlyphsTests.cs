using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class StaffGlyphsTests
{
    [Theory]
    [InlineData("treble")]
    [InlineData("bass")]
    public void Clef_FixedGrid_ExposesDimensionsAnchorAndLitPixels(string glyphName)
    {
        var glyph = glyphName == "treble" ? StaffGlyphs.TrebleClef : StaffGlyphs.BassClef;
        Assert.Equal(StaffGlyphs.GlyphWidth, glyph.Width);
        Assert.Equal(StaffGlyphs.GlyphHeight, glyph.Height);
        Assert.Equal(glyph.Width * glyph.Height, glyph.Pixels.Count);
        Assert.InRange(glyph.AnchorRow, 0, glyph.Height - 1);
        Assert.Contains(true, glyph.Pixels);
        Assert.Contains(true, glyph.Pixels.Skip(glyph.AnchorRow * glyph.Width).Take(glyph.Width));
    }

    [Fact]
    public void Clefs_MusicalAnchorsAndSignaturePixels_ArePinned()
    {
        Assert.Equal(8, StaffGlyphs.TrebleClef.AnchorRow);
        Assert.True(StaffGlyphs.TrebleClef.Pixels[(8 * StaffGlyphs.GlyphWidth) + 2]);

        Assert.Equal(7, StaffGlyphs.BassClef.AnchorRow);
        Assert.True(StaffGlyphs.BassClef.Pixels[(5 * StaffGlyphs.GlyphWidth) + 6]);
        Assert.True(StaffGlyphs.BassClef.Pixels[(9 * StaffGlyphs.GlyphWidth) + 6]);
    }
}
