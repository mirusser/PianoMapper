namespace PianoMapper.Music;

/// <summary>
/// One visual beam level attached to a score note. Level one is the eighth-note beam;
/// higher levels represent shorter note values and hooks.
/// </summary>
public sealed record ScoreBeam(int Number, ScoreBeamKind Kind);
