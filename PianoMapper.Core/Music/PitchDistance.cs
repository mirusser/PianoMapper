namespace PianoMapper.Music;

/// <summary>
/// Compares a played pitch with the expected one and puts the difference in words ("a step higher", "a third
/// lower", "same note, an octave higher", "same note, sharp"). Pure and staff-agnostic: only the two pitches matter.
/// </summary>
public static class PitchDistance
{
    private static readonly string[] IntervalNames =
    [
        string.Empty,
        "a step",
        "a third",
        "a fourth",
        "a fifth",
        "a sixth",
        "a seventh",
    ];

    public static PitchDifference Measure(Pitch expected, Pitch played)
    {
        int alterDelta = played.Alter - expected.Alter;
        int signedSteps = played.DiatonicIndex - expected.DiatonicIndex;
        int steps = Math.Abs(signedSteps);
        if (expected.MidiNumber == played.MidiNumber)
        {
            return new PitchDifference(PitchDifferenceKind.SameKey, IsHigher: false, steps, alterDelta);
        }

        if (signedSteps == 0)
        {
            return new PitchDifference(PitchDifferenceKind.Accidental, alterDelta > 0, 0, alterDelta);
        }

        PitchDifferenceKind kind = steps % 7 == 0 ? PitchDifferenceKind.Octaves : PitchDifferenceKind.Interval;
        return new PitchDifference(kind, signedSteps > 0, steps, alterDelta);
    }

    public static string Describe(Pitch expected, Pitch played) =>
        expected == played ? "the same note" : Describe(Measure(expected, played));

    /// <summary>The words for a difference that is already measured, e.g. for a habit across many notes.</summary>
    public static string Describe(PitchDifference difference)
    {
        string direction = difference.IsHigher ? "higher" : "lower";
        return difference.Kind switch
        {
            PitchDifferenceKind.SameKey => "the same key, spelled differently",
            PitchDifferenceKind.Accidental => $"same note, {DescribeAccidental(difference.AlterDelta)}",
            PitchDifferenceKind.Octaves =>
                $"same note, {DescribeOctaves(difference.Octaves)} {direction}{DescribeAccidentalSuffix(difference.AlterDelta)}",
            _ => difference.Octaves == 0
                ? $"{IntervalNames[difference.IntervalSteps]} {direction}"
                : $"{DescribeOctaves(difference.Octaves)} and {IntervalNames[difference.IntervalSteps]} {direction}",
        };
    }

    private static string DescribeAccidental(int alterDelta) => alterDelta switch
    {
        1 => "sharp",
        2 => "double sharp",
        -1 => "flat",
        -2 => "double flat",
        > 2 => "much sharper",
        _ => "much flatter",
    };

    private static string DescribeAccidentalSuffix(int alterDelta) =>
        alterDelta == 0 ? string.Empty : $", {DescribeAccidental(alterDelta)}";

    private static string DescribeOctaves(int octaves) => octaves switch
    {
        1 => "an octave",
        2 => "two octaves",
        3 => "three octaves",
        _ => $"{octaves} octaves",
    };
}
