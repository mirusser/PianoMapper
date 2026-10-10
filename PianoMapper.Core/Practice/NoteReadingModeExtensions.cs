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

    /// <summary>
    /// Whether the mode judges onset or release timing, not just pitch. False for an undefined mode (stored history can
    /// name a mode this version does not know) instead of throwing like <see cref="GetGradedAxes"/>.
    /// </summary>
    public static bool IsTimingGraded(this NoteReadingMode mode) =>
        Enum.IsDefined(mode) &&
        (mode.GetGradedAxes() & (GradedAxes.Onset | GradedAxes.Duration)) != GradedAxes.None;
}
