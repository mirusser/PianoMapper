using System.Text.Json.Serialization;

namespace PianoMapper.Music;

public readonly record struct NoteValue
{
    [JsonConstructor]
    public NoteValue(int denominator, int dots = 0, int tupletActualNotes = 1, int tupletNormalNotes = 1)
    {
        if (denominator is not (1 or 2 or 4 or 8 or 16))
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(dots);
        ArgumentOutOfRangeException.ThrowIfLessThan(tupletActualNotes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(tupletNormalNotes, 1);

        Denominator = denominator;
        Dots = dots;
        TupletActualNotes = tupletActualNotes;
        TupletNormalNotes = tupletNormalNotes;
    }

    public int Denominator { get; }

    public int Dots { get; }

    /// <summary>
    /// The MusicXML &lt;time-modification&gt;&lt;actual-notes&gt; count (e.g. 3 for a triplet).
    /// 1 (the default) means no tuplet.
    /// </summary>
    public int TupletActualNotes { get; }

    /// <summary>
    /// The MusicXML &lt;time-modification&gt;&lt;normal-notes&gt; count (e.g. 2 for a triplet).
    /// 1 (the default) means no tuplet.
    /// </summary>
    public int TupletNormalNotes { get; }
}
