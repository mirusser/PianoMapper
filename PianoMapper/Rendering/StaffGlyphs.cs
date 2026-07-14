namespace PianoMapper.Rendering;

internal static class StaffGlyphs
{
    public const int GlyphWidth = 7;
    public const int GlyphHeight = 15;

    /// <summary>The treble clef anchor row is centered on the G4 staff line.</summary>
    public static StaffGlyph TrebleClef { get; } = new(
        GlyphWidth,
        GlyphHeight,
        AnchorRow: 8,
        Parse(
            "0011100",
            "0100100",
            "0100000",
            "0010000",
            "0011000",
            "0101000",
            "1001000",
            "1010000",
            "0111100",
            "0010010",
            "0010001",
            "0010001",
            "0100010",
            "0100010",
            "0011100"));

    /// <summary>The bass clef anchor row is centered on the F3 staff line.</summary>
    public static StaffGlyph BassClef { get; } = new(
        GlyphWidth,
        GlyphHeight,
        AnchorRow: 7,
        Parse(
            "0000000",
            "0011100",
            "0100010",
            "1000000",
            "1000000",
            "1000001",
            "0100000",
            "0011000",
            "0001000",
            "0001001",
            "0001000",
            "0000000",
            "0000000",
            "0000000",
            "0000000"));

    private static IReadOnlyList<bool> Parse(params string[] rows) =>
        rows.SelectMany(row => row.Select(pixel => pixel == '1')).ToArray();
}
