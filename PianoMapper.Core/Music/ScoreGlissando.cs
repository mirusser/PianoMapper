namespace PianoMapper.Music;

/// <summary>
/// One end of a MusicXML &lt;glissando&gt; or &lt;slide&gt; notation on a note — the same
/// start/stop-by-<see cref="Number"/> pairing shape as <see cref="ScoreSlur"/>, plus which of the
/// two distinct MusicXML concepts this is. Glissando (a discrete pitch slide) and slide (a
/// continuous one) render identically in v1 (see <c>GrandStaffSceneBuilder</c>), but are kept as
/// separate domain data rather than conflated into one concept.
/// </summary>
public sealed record ScoreGlissando(bool IsStart, int Number, ScoreGlissandoKind Kind);
