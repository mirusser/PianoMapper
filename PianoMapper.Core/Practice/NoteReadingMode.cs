namespace PianoMapper.Practice;

/// <summary>
/// Names are persisted in exercise history as strings: add members, never rename or reorder.
/// <see cref="NoteReadingModeExtensions.GetGradedAxes"/> says what each one grades.
/// </summary>
public enum NoteReadingMode
{
    Off,
    PitchAndOrder,
    PitchAndHold,
    PitchHoldAndRhythm,
    PitchAndRhythm,
    RhythmOnly,
}
