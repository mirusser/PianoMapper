using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class TextLayoutTests
{
    [Fact]
    public void BuildQuads_EmptyString_ReturnsEmpty()
    {
        var quads = TextLayout.BuildQuads("", anchorX: 0f, anchorY: 0f, glyphWidth: 0.1f, glyphHeight: 0.1f);

        Assert.Empty(quads);
    }

    [Fact]
    public void BuildQuads_KnownShortString_ProducesExpectedQuadCountFromFontTable()
    {
        // 'I' is 111/010/010/010/111 = 9 lit pixels.
        var quads = TextLayout.BuildQuads("I", anchorX: 0f, anchorY: 0f, glyphWidth: 0.3f, glyphHeight: 0.5f);

        Assert.Equal(9, quads.Count);
    }

    [Fact]
    public void BuildQuads_TwoKnownCharacters_QuadCountIsSumOfEachGlyphsLitPixels()
    {
        // 'I' = 9 lit pixels, '#' (010/111/010/111/010) = 9 lit pixels.
        var quads = TextLayout.BuildQuads("I#", anchorX: 0f, anchorY: 0f, glyphWidth: 0.3f, glyphHeight: 0.5f);

        Assert.Equal(18, quads.Count);
    }

    [Fact]
    public void BuildQuads_MultipleGlyphs_SecondGlyphDoesNotOverlapFirst()
    {
        const float glyphWidth = 0.1f;
        const float glyphHeight = 0.2f;

        var singleGlyphQuads = TextLayout.BuildQuads("A", anchorX: 0f, anchorY: 0f, glyphWidth, glyphHeight);
        var maxX1OfFirstGlyph = singleGlyphQuads.Max(quad => quad.X1);

        var twoGlyphQuads = TextLayout.BuildQuads("AA", anchorX: 0f, anchorY: 0f, glyphWidth, glyphHeight);
        var secondGlyphQuads = twoGlyphQuads.Skip(singleGlyphQuads.Count).ToList();
        var minX0OfSecondGlyph = secondGlyphQuads.Min(quad => quad.X0);

        Assert.True(minX0OfSecondGlyph >= maxX1OfFirstGlyph, $"expected {minX0OfSecondGlyph} >= {maxX1OfFirstGlyph}");
    }

    [Fact]
    public void BuildQuads_SingleGlyph_LeftEdgeAtAnchorX()
    {
        // 'I' has a fully-lit top row, so its leftmost pixel starts exactly at the anchor.
        var quads = TextLayout.BuildQuads("I", anchorX: 0.25f, anchorY: 0f, glyphWidth: 0.3f, glyphHeight: 0.5f);

        var minX0 = quads.Min(quad => quad.X0);
        Assert.Equal(0.25f, minX0, 3);
    }

    [Fact]
    public void BuildQuads_SingleGlyph_BottomEdgeAtAnchorY()
    {
        var quads = TextLayout.BuildQuads("I", anchorX: 0f, anchorY: 0.4f, glyphWidth: 0.3f, glyphHeight: 0.5f);

        var minY0 = quads.Min(quad => quad.Y0);
        Assert.Equal(0.4f, minY0, 3);
    }

    [Fact]
    public void BuildQuads_SingleGlyph_TopEdgeAtAnchorYPlusGlyphHeight()
    {
        var quads = TextLayout.BuildQuads("I", anchorX: 0f, anchorY: 0.4f, glyphWidth: 0.3f, glyphHeight: 0.5f);

        var maxY1 = quads.Max(quad => quad.Y1);
        Assert.Equal(0.9f, maxY1, 3);
    }
}
