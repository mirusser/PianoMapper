namespace PianoMapper.Music;

/// <summary>
/// One end of a MusicXML &lt;slur&gt; notation on a note: whether this note is the phrase's
/// start or stop, and the MusicXML <c>number</c> attribute used to match a start to its stop when
/// more than one slur is open at once (overlapping/nested phrase marks). Matching is resolved
/// later, at render time, by walking notes for a same-<see cref="Number"/> pair — see
/// <c>GrandStaffSceneBuilder</c>.
/// <para>
/// A note can carry several ends (the stop of one phrase and the start of the next, or two phrases starting
/// together). They are chained through <see cref="Next"/> in document order, so a note's slur data stays one value
/// and note equality stays value equality.
/// </para>
/// </summary>
public sealed record ScoreSlur(bool IsStart, int Number, ScoreSlur? Next = null)
{
    /// <summary>This slur end followed by the ones chained after it.</summary>
    public IEnumerable<ScoreSlur> Chain()
    {
        for (ScoreSlur? slur = this; slur is not null; slur = slur.Next)
        {
            yield return slur;
        }
    }
}
