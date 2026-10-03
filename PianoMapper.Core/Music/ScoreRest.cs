namespace PianoMapper.Music;

/// <param name="IsMeasureRest">
/// True for a whole-measure rest (MusicXML <c>&lt;rest measure="yes"/&gt;</c>), which fills its measure whatever the
/// meter and is drawn centered as a whole rest. <see cref="NoteValue"/> then carries the measure's length as a single
/// value where one exists (a whole rest in 4/4, a dotted half in 3/4) and a whole rest otherwise.
/// </param>
public sealed record ScoreRest(
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff,
    bool IsMeasureRest = false);
