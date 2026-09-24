namespace PianoMapper.Music;

/// <summary>
/// One end of a MusicXML &lt;slur&gt; notation on a note: whether this note is the phrase's
/// start or stop, and the MusicXML <c>number</c> attribute used to match a start to its stop when
/// more than one slur is open at once (overlapping/nested phrase marks). Matching is resolved
/// later, at render time, by walking notes for a same-<see cref="Number"/> pair — see
/// <c>GrandStaffSceneBuilder</c>.
/// </summary>
public sealed record ScoreSlur(bool IsStart, int Number);
