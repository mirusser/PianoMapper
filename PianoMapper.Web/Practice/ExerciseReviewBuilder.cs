using System.Globalization;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Turns a finished exercise's prompt results into a short list of what to look at: where each mistake was (bar and
/// beat), what was expected, what was played instead and how it differs, and how the timing was off. The builder is
/// pure and says nothing while an exercise is still active, because it names the notes.
/// </summary>
internal static class ExerciseReviewBuilder
{
    internal const int MaximumLines = 8;

    internal static ExerciseReview Build(
        IReadOnlyList<NoteReadingPromptResult> promptResults,
        TimeSignature timeSignature)
    {
        ArgumentNullException.ThrowIfNull(promptResults);
        ExerciseReviewLine[] mistakes = promptResults
            .Where(result => !result.IsFirstTryCorrect)
            .OrderBy(result => result.PromptIndex)
            .Select(result => BuildLine(result, timeSignature))
            .ToArray();
        return new ExerciseReview(mistakes.Take(MaximumLines).ToArray(), Math.Max(0, mistakes.Length - MaximumLines));
    }

    /// <summary>One line of prose, e.g. "Bar 2 · beat 3 — expected C4, played D4 (a step higher) · late by 85 ms".</summary>
    internal static string Describe(ExerciseReviewLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string expected = string.Join("+", line.ExpectedPitches);
        string where = string.Create(
            CultureInfo.InvariantCulture,
            $"Bar {line.BarNumber} · beat {line.BeatNumber:0.##}");
        var findings = new List<string>();
        if (line.WasMissed)
        {
            findings.Add($"expected {expected}, not played");
        }
        else if (line.PlayedPitch is { } played)
        {
            Pitch nearest = NearestExpected(line.ExpectedPitches, played);
            findings.Add($"expected {expected}, played {played} ({PitchDistance.Describe(nearest, played)})");
        }
        else
        {
            findings.Add($"expected {expected}");
        }

        if (line.OnsetVerdict is Verdict.Early or Verdict.Late && line.OnsetDeviation is { } deviation)
        {
            int milliseconds = (int)Math.Round(Math.Abs(deviation.TotalMilliseconds), MidpointRounding.AwayFromZero);
            findings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{(line.OnsetVerdict == Verdict.Late ? "late" : "early")} by {milliseconds} ms"));
        }

        if (line.DurationVerdict is Verdict.TooShort)
        {
            findings.Add("released too soon");
        }
        else if (line.DurationVerdict is Verdict.TooLong)
        {
            findings.Add("held too long");
        }

        return $"{where} — {string.Join(" · ", findings)}";
    }

    private static ExerciseReviewLine BuildLine(NoteReadingPromptResult result, TimeSignature timeSignature)
    {
        int beatsPerBar = timeSignature.Numerator;
        int bar = (int)Math.Floor((result.OnsetBeats + 1e-9) / beatsPerBar);
        double beat = result.OnsetBeats - (bar * beatsPerBar);
        Pitch? played = result.WrongPlayedPitches.Length > 0 ? result.WrongPlayedPitches[^1] : null;
        PitchDifference? difference = played is { } wrongPitch
            ? PitchDistance.Measure(NearestExpected(result.ExpectedPitches, wrongPitch), wrongPitch)
            : null;
        bool hasTimingMistake = result.OnsetVerdict is Verdict.Early or Verdict.Late;
        return new ExerciseReviewLine(
            bar + 1,
            Math.Round(beat, 4) + 1,
            result.ExpectedPitches,
            played,
            difference,
            result.WasMissed,
            hasTimingMistake ? result.OnsetVerdict : null,
            hasTimingMistake ? result.OnsetDeviation : null,
            result.DurationVerdict is Verdict.TooShort or Verdict.TooLong ? result.DurationVerdict : null);
    }

    private static Pitch NearestExpected(IReadOnlyList<Pitch> expectedPitches, Pitch played) =>
        expectedPitches.MinBy(expected => Math.Abs(expected.DiatonicIndex - played.DiatonicIndex));
}
