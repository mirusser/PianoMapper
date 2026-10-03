namespace PianoMapper.Music;

/// <summary>
/// How one pitch differs from another, in the terms a reader of the staff uses. Distances count letter positions
/// (lines and spaces), not semitones, so an accidental never changes how many steps away a note looks.
/// </summary>
/// <param name="Kind">Which kind of difference this is.</param>
/// <param name="IsHigher">Whether the played pitch sits higher (or, for an accidental, is sharper).</param>
/// <param name="DiatonicSteps">The absolute number of letter positions between the two pitches.</param>
/// <param name="AlterDelta">The played accidental minus the expected one.</param>
public readonly record struct PitchDifference(
    PitchDifferenceKind Kind,
    bool IsHigher,
    int DiatonicSteps,
    int AlterDelta)
{
    private const int StepsPerOctave = 7;

    /// <summary>Whole octaves in <see cref="DiatonicSteps"/>.</summary>
    public int Octaves => DiatonicSteps / StepsPerOctave;

    /// <summary>The steps left over after whole octaves (0 for a pure octave jump).</summary>
    public int IntervalSteps => DiatonicSteps % StepsPerOctave;
}
