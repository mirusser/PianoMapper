namespace PianoMapper.Rendering;

/// <summary>
/// Pure string + anchor -> screen-space math for the bitmap-font text renderer, kept
/// free of any GL dependency so it can be unit tested without a live rendering context.
/// The anchor (<paramref name="anchorX"/>, <paramref name="anchorY"/>) is the bottom-left
/// corner of the rendered string; glyphs extend rightward and upward from it.
/// </summary>
public static class TextLayout
{
    // Blank columns left between glyphs so adjacent characters stay visually separated.
    private const int GlyphSpacingPixels = 1;

    public static IReadOnlyList<BarRect> BuildQuads(string text, float anchorX, float anchorY, float glyphWidth, float glyphHeight)
    {
        var quads = new List<BarRect>();

        var pixelWidth = glyphWidth / BitmapFont.GlyphWidth;
        var pixelHeight = glyphHeight / BitmapFont.GlyphHeight;
        var advance = glyphWidth + pixelWidth * GlyphSpacingPixels;

        var cursorX = anchorX;
        foreach (var c in text)
        {
            var glyph = BitmapFont.GetGlyph(c);

            for (var row = 0; row < BitmapFont.GlyphHeight; row++)
            {
                for (var col = 0; col < BitmapFont.GlyphWidth; col++)
                {
                    if (!glyph[row * BitmapFont.GlyphWidth + col])
                    {
                        continue;
                    }

                    var x0 = cursorX + col * pixelWidth;
                    var x1 = x0 + pixelWidth;

                    // Row 0 is the top of the glyph, but the anchor is the bottom of the
                    // string, so higher pixel rows sit at higher (less-inverted) Y.
                    var y1 = anchorY + glyphHeight - row * pixelHeight;
                    var y0 = y1 - pixelHeight;

                    quads.Add(new BarRect(x0, x1, y0, y1));
                }
            }

            cursorX += advance;
        }

        return quads;
    }
}
