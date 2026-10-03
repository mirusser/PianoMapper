namespace PianoMapper.Practice;

/// <summary>
/// What a <see cref="NoteReadingMode"/> judges about a played note. Derived once per mode (see
/// <see cref="NoteReadingModeExtensions.GetGradedAxes"/>) and shared by every engine that grades exercises, so they
/// cannot disagree about what a mode means.
/// </summary>
[Flags]
public enum GradedAxes
{
    None = 0,

    /// <summary>Whether the right key was played.</summary>
    Pitch = 1,

    /// <summary>Whether the key was pressed on the beat.</summary>
    Onset = 2,

    /// <summary>Whether the key was held for the written value.</summary>
    Duration = 4,
}
