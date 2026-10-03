using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>The status-line wording of a coach hint. The structure lives in <see cref="ExerciseCoachHint"/>.</summary>
internal static class ExerciseCoachHintText
{
    internal static string Describe(ExerciseCoachHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);
        return hint.Level switch
        {
            ExerciseCoachHintLevel.Name => DescribeName(hint.ExpectedPitches),
            ExerciseCoachHintLevel.Direction => DescribeDirection(hint),
            _ => string.Empty,
        };
    }

    private static string DescribeName(IReadOnlyList<Pitch> expectedPitches) => expectedPitches.Count == 1
        ? $"It's {expectedPitches[0]} — find it and play it."
        : $"It's {string.Join("+", expectedPitches)} — find them and play them together.";

    private static string DescribeDirection(ExerciseCoachHint hint)
    {
        if (hint is not { PressedPitch: { } pressed, Difference: { } difference } || hint.ExpectedPitches.Count == 0)
        {
            return "Look again at where the note sits on the staff.";
        }

        Pitch nearest = hint.ExpectedPitches.MinBy(expected => Math.Abs(expected.DiatonicIndex - pressed.DiatonicIndex));
        string distance = PitchDistance.Describe(pressed, nearest);
        return difference.Kind switch
        {
            PitchDifferenceKind.Interval => $"The note is {distance} than {pressed}.",
            PitchDifferenceKind.Octaves => $"{pressed} is the right note in the wrong octave ({distance}).",
            PitchDifferenceKind.Accidental => $"{pressed} has the wrong accidental ({distance}).",
            _ => "Look again at where the note sits on the staff.",
        };
    }
}
