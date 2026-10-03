namespace PianoMapper.Practice;

public static class NoteReadingModeExtensions
{
    /// <summary>The axes a mode grades. Throws for an undefined mode so a new member cannot be silently ungraded.</summary>
    public static GradedAxes GetGradedAxes(this NoteReadingMode mode) => mode switch
    {
        NoteReadingMode.Off => GradedAxes.None,
        NoteReadingMode.PitchAndOrder => GradedAxes.Pitch,
        NoteReadingMode.PitchAndHold => GradedAxes.Pitch | GradedAxes.Duration,
        NoteReadingMode.PitchAndRhythm => GradedAxes.Pitch | GradedAxes.Onset,
        NoteReadingMode.PitchHoldAndRhythm => GradedAxes.Pitch | GradedAxes.Onset | GradedAxes.Duration,
        NoteReadingMode.RhythmOnly => GradedAxes.Onset,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
