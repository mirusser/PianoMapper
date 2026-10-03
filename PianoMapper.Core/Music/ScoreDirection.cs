namespace PianoMapper.Music;

/// <summary>
/// A printed marking attached to a point in a measure rather than to a note: a dynamic, words, a hairpin end, a pedal
/// mark, a tempo mark, a rehearsal mark, a segno or coda sign, or a chord symbol.
/// </summary>
/// <param name="BeatOffset">Where in the measure it sits, in the same beats as <see cref="ScoreNote.BeatOffset"/>.</param>
/// <param name="Staff">The staff it belongs to (MusicXML's &lt;staff&gt;, the upper staff by default).</param>
/// <param name="Text">What is printed, for the kinds that print text.</param>
/// <param name="IsBelow">Whether it is placed below its staff; dynamics, hairpins and pedal marks default to below.</param>
/// <param name="Number">Pairs a hairpin's start with its stop when several are open at once.</param>
public sealed record ScoreDirection(
    ScoreDirectionKind Kind,
    double BeatOffset,
    Staff Staff,
    string Text = "",
    bool IsBelow = false,
    int Number = 1);
