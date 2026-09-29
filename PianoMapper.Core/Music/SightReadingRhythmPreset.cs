namespace PianoMapper.Music;

/// <summary>
/// The rhythmic vocabulary an exercise draws its measure patterns from. Orthogonal to
/// <see cref="SightReadingPresetId"/> (the pitch palette) and to <see cref="SightReadingExerciseOptions.IsGrandStaff"/>.
/// </summary>
public enum SightReadingRhythmPreset
{
    /// <summary>The existing behavior: fixed quarter notes, one per beat, in 4/4. No rests.</summary>
    Fixed,

    /// <summary>4/4 using half notes, quarter notes, beamed eighth-note pairs, and quarter rests.</summary>
    Basic,

    /// <summary>6/8 using dotted-quarter notes and beamed eighth-note groups.</summary>
    Compound,
}
