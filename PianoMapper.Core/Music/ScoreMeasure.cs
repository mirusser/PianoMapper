namespace PianoMapper.Music;

/// <param name="KeyFifths">
/// The key signature (sharps positive, flats negative) that takes effect at this measure, or null while the key stays
/// what the measure before had. <see cref="Score.KeyFifths"/> is the key the score opens with.
/// </param>
/// <param name="LengthInBeats">
/// The actual length of an implicit MusicXML pickup measure, in the score's time-signature beats, or null for a
/// regular full-length measure.
/// </param>
/// <param name="LeftBarline">What the barline that opens this measure carries, or null for a plain one.</param>
/// <param name="RightBarline">What the barline that closes this measure carries, or null for a plain one.</param>
/// <param name="Directions">The measure's dynamics, words, pedal and tempo marks and chord symbols, or null for none.</param>
public sealed record ScoreMeasure(
    IReadOnlyList<ScoreNote> Notes,
    IReadOnlyList<ScoreRest> Rests,
    int? KeyFifths = null,
    ScoreBarline? LeftBarline = null,
    ScoreBarline? RightBarline = null,
    IReadOnlyList<ScoreDirection>? Directions = null,
    double? LengthInBeats = null);
