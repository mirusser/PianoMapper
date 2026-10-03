namespace PianoMapper.Music;

/// <summary>
/// One end of a volta bracket (MusicXML &lt;ending&gt;): <see cref="Numbers"/> is the printed repeat-pass list such as
/// "1, 2", and the bracket spans from the measure that <see cref="ScoreEndingType.Start"/>s it to the one that stops it.
/// </summary>
public sealed record ScoreEnding(string Numbers, ScoreEndingType Type);
