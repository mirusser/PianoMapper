namespace PianoMapper.Rendering;

/// <summary>
/// Hand-rolled 3x5 pixel glyph table, consistent with the repo's existing hand-rolled
/// DSP/rendering style (no font/NuGet dependency). Covers A-Z, 0-9, '#', '/', and space --
/// enough for note names (e.g. "C#4") and on-screen labels (e.g. "OCTAVE 3",
/// "OSCILLOSCOPE", "SPECTRUM", "TEMPO 60 BPM 4/4").
/// </summary>
public static class BitmapFont
{
    public const int GlyphWidth = 3;
    public const int GlyphHeight = 5;

    private static readonly IReadOnlyDictionary<char, IReadOnlyList<bool>> Glyphs = BuildGlyphs();

    /// <summary>
    /// Returns the glyph's lit-pixel grid, row-major (index = row * GlyphWidth + col),
    /// row 0 is the top of the character. Unknown characters fall back to a blank glyph.
    /// </summary>
    public static IReadOnlyList<bool> GetGlyph(char c)
    {
        char upper = char.ToUpperInvariant(c);
        return Glyphs.TryGetValue(upper, out var glyph) ? glyph : Glyphs[' '];
    }

    private static IReadOnlyDictionary<char, IReadOnlyList<bool>> BuildGlyphs() => new Dictionary<char, IReadOnlyList<bool>>
    {
        [' '] = Parse("000", "000", "000", "000", "000"),
        ['#'] = Parse("010", "111", "010", "111", "010"),
        ['♭'] = Parse("100", "100", "110", "101", "110"),
        ['/'] = Parse("001", "001", "010", "100", "100"),

        ['A'] = Parse("010", "101", "111", "101", "101"),
        ['B'] = Parse("110", "101", "110", "101", "110"),
        ['C'] = Parse("011", "100", "100", "100", "011"),
        ['D'] = Parse("110", "101", "101", "101", "110"),
        ['E'] = Parse("111", "100", "111", "100", "111"),
        ['F'] = Parse("111", "100", "111", "100", "100"),
        ['G'] = Parse("011", "100", "101", "101", "011"),
        ['H'] = Parse("101", "101", "111", "101", "101"),
        ['I'] = Parse("111", "010", "010", "010", "111"),
        ['J'] = Parse("001", "001", "001", "101", "010"),
        ['K'] = Parse("101", "101", "110", "101", "101"),
        ['L'] = Parse("100", "100", "100", "100", "111"),
        ['M'] = Parse("101", "111", "101", "101", "101"),
        ['N'] = Parse("110", "111", "101", "101", "101"),
        ['O'] = Parse("010", "101", "101", "101", "010"),
        ['P'] = Parse("110", "101", "110", "100", "100"),
        ['Q'] = Parse("010", "101", "101", "011", "001"),
        ['R'] = Parse("110", "101", "110", "101", "101"),
        ['S'] = Parse("011", "100", "010", "001", "110"),
        ['T'] = Parse("111", "010", "010", "010", "010"),
        ['U'] = Parse("101", "101", "101", "101", "111"),
        ['V'] = Parse("101", "101", "101", "101", "010"),
        ['W'] = Parse("101", "101", "101", "111", "101"),
        ['X'] = Parse("101", "101", "010", "101", "101"),
        ['Y'] = Parse("101", "101", "010", "010", "010"),
        ['Z'] = Parse("111", "001", "010", "100", "111"),

        ['0'] = Parse("111", "101", "101", "101", "111"),
        ['1'] = Parse("010", "110", "010", "010", "111"),
        ['2'] = Parse("111", "001", "111", "100", "111"),
        ['3'] = Parse("111", "001", "111", "001", "111"),
        ['4'] = Parse("101", "101", "111", "001", "001"),
        ['5'] = Parse("111", "100", "111", "001", "111"),
        ['6'] = Parse("111", "100", "111", "101", "111"),
        ['7'] = Parse("111", "001", "001", "001", "001"),
        ['8'] = Parse("111", "101", "111", "101", "111"),
        ['9'] = Parse("111", "101", "111", "001", "111"),
    };

    private static IReadOnlyList<bool> Parse(string row0, string row1, string row2, string row3, string row4)
    {
        string[] rows = [row0, row1, row2, row3, row4];
        var pixels = new bool[GlyphWidth * GlyphHeight];

        for (int row = 0; row < GlyphHeight; row++)
        {
            for (int col = 0; col < GlyphWidth; col++)
            {
                pixels[row * GlyphWidth + col] = rows[row][col] == '1';
            }
        }

        return pixels;
    }
}
