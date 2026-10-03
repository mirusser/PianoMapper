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

    /// <summary>4/4 using dotted half notes, a whole note, a dotted quarter with an eighth, and an eighth rest.</summary>
    Extended,

    /// <summary>3/4 using quarter notes, half notes, a dotted half, beamed eighth pairs, and a quarter rest.</summary>
    ThreeFour,

    /// <summary>2/4 using quarter notes, a half note, beamed eighth pairs, and a quarter rest.</summary>
    TwoFour,

    /// <summary>
    /// 4/4 with off-beat eighths and notes tied across the beat and across the barline (a tied pair is one note to play,
    /// held for both values).
    /// </summary>
    Syncopated,
}
