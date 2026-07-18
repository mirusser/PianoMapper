namespace PianoMapper.Rendering;

/// <summary>
/// Pure pixel-grid -> screen-space math for a <see cref="StaffGlyph"/>, kept free of any
/// GL dependency so it can be unit tested without a live rendering context. Mirrors
/// <see cref="TextLayout.BuildQuads(string, float, float, float, float)"/>'s shape.
/// </summary>
internal static class ClefGlyphLayout
{
    public static IReadOnlyList<BarRect> BuildQuads(
        StaffGlyph glyph,
        float anchorX,
        float anchorY,
        float cellWidth,
        float cellHeight)
    {
        var quads = new List<BarRect>();

        for (int row = 0; row < glyph.Height; row++)
        {
            for (int column = 0; column < glyph.Width; column++)
            {
                if (!glyph.Pixels[(row * glyph.Width) + column])
                {
                    continue;
                }

                float x0 = anchorX + (column * cellWidth);
                float yCenter = anchorY + ((glyph.AnchorRow - row) * cellHeight);
                quads.Add(new BarRect(x0, x0 + cellWidth, yCenter - (cellHeight / 2f), yCenter + (cellHeight / 2f)));
            }
        }

        return quads;
    }
}
