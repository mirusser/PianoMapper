using System.Collections.Immutable;
using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// Turns a time-driven <see cref="GradingResult"/> into the exercise's prompt results, so review, history and the
/// level ladder consume one result type whichever pacing produced it. Only the outcomes the mode grades (see
/// <see cref="NoteReadingModeExtensions.GetGradedAxes"/>) count toward a prompt being clean.
/// </summary>
public static class PlayAlongResultMapper
{
    public static PlayAlongOutcome Map(
        GradingResult result,
        NoteReadingMode mode,
        Tempo tempo,
        TimeSpan practiceAnchor,
        GradingOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        GradedAxes axes = mode.GetGradedAxes();

        GradedEvent[] gradedExpected = result.Events.Where(gradedEvent => gradedEvent.Expected is not null).ToArray();
        // The grader hands back the very event instances it was given, so they identify their graded outcome.
        var gradedByEvent = new Dictionary<ScoreEvent, GradedEvent>(ReferenceEqualityComparer.Instance);
        foreach (GradedEvent gradedEvent in gradedExpected)
        {
            gradedByEvent[gradedEvent.Expected!] = gradedEvent;
        }

        IReadOnlyList<IReadOnlyList<ScoreEvent>> prompts = ScoreDerivation.GroupByOnset(
            gradedExpected.Select(gradedEvent => gradedEvent.Expected!).ToArray());
        var promptResults = new List<NoteReadingPromptResult>(prompts.Count);
        for (int promptIndex = 0; promptIndex < prompts.Count; promptIndex++)
        {
            GradedEvent[] events = prompts[promptIndex].Select(scoreEvent => gradedByEvent[scoreEvent]).ToArray();
            promptResults.Add(BuildPromptResult(promptIndex, events, axes, tempo, practiceAnchor, options));
        }

        return new PlayAlongOutcome(
            promptResults,
            result.Summary.Counts.GetValueOrDefault(Verdict.Extra),
            CountVerdicts(result, axes));
    }

    /// <summary>
    /// The grader always judges duration, so a short note in a mode that does not grade duration is "too short" in
    /// its raw result. The counts shown for the run only report what the mode grades, so that outcome counts as
    /// correct there (the grader only reaches the duration check once pitch and onset were fine).
    /// </summary>
    private static IReadOnlyDictionary<Verdict, int> CountVerdicts(GradingResult result, GradedAxes axes)
    {
        bool gradesDuration = axes.HasFlag(GradedAxes.Duration);
        Dictionary<Verdict, int> counts = Enum.GetValues<Verdict>().ToDictionary(verdict => verdict, _ => 0);
        foreach (GradedEvent graded in result.Events)
        {
            Verdict verdict = !gradesDuration && graded.Verdict is Verdict.TooShort or Verdict.TooLong
                ? Verdict.Correct
                : graded.Verdict;
            counts[verdict]++;
        }

        return counts;
    }

    private static NoteReadingPromptResult BuildPromptResult(
        int promptIndex,
        GradedEvent[] events,
        GradedAxes axes,
        Tempo tempo,
        TimeSpan practiceAnchor,
        GradingOptions options)
    {
        bool gradesPitch = axes.HasFlag(GradedAxes.Pitch);
        bool gradesOnset = axes.HasFlag(GradedAxes.Onset);
        bool gradesDuration = axes.HasFlag(GradedAxes.Duration);

        var deviations = new List<TimeSpan>();
        int mistakeCount = 0;
        bool hasPitchMiss = false;
        bool hasOnsetMiss = false;
        bool hasDurationMiss = false;
        bool wasMissed = false;
        Verdict? durationVerdict = null;
        var wrongPlayedPitches = new List<Pitch>();
        foreach (GradedEvent graded in events)
        {
            ScoreEvent expected = graded.Expected!;
            bool isMissed = graded.Verdict == Verdict.Missed;
            wasMissed |= isMissed;
            bool pitchMiss = gradesPitch && (isMissed || graded.Verdict == Verdict.WrongPitch);
            bool onsetMiss = gradesOnset && isMissed;
            if (graded.Verdict == Verdict.WrongPitch && graded.Performed is { } wrongNote)
            {
                wrongPlayedPitches.Add(wrongNote.Pitch);
            }

            if (graded.Performed is { } performed)
            {
                TimeSpan expectedOnset = practiceAnchor + MusicalTime.BeatsToDuration(expected.OnsetBeats, tempo);
                TimeSpan deviation = performed.StartTime - expectedOnset;
                deviations.Add(deviation);
                onsetMiss |= gradesOnset && deviation.Duration() > options.OnTimeTolerance;
            }

            bool durationMiss = gradesDuration && graded.Verdict is Verdict.TooShort or Verdict.TooLong;
            if (gradesDuration)
            {
                if (durationMiss)
                {
                    // A chord can contain both a correctly held key and a duration mistake. Keep the first
                    // duration mistake so a correct member cannot hide the prompt's observable outcome.
                    durationVerdict = durationVerdict is null or Verdict.Correct
                        ? graded.Verdict
                        : durationVerdict;
                }
                else if (graded.Verdict == Verdict.Correct)
                {
                    durationVerdict ??= Verdict.Correct;
                }
            }

            hasPitchMiss |= pitchMiss;
            hasOnsetMiss |= onsetMiss;
            hasDurationMiss |= durationMiss;
            if (pitchMiss || onsetMiss || durationMiss)
            {
                mistakeCount++;
            }
        }

        Verdict? onsetVerdict = null;
        TimeSpan? onsetDeviation = null;
        if (gradesOnset)
        {
            if (deviations.Count == 0)
            {
                onsetVerdict = Verdict.Missed;
            }
            else
            {
                onsetDeviation = deviations.MaxBy(deviation => deviation.Duration());
                onsetVerdict = ClassifyOnset(onsetDeviation.Value, options.OnTimeTolerance);
            }
        }

        return new NoteReadingPromptResult(
            promptIndex,
            events[0].Expected!.OnsetBeats,
            events.SelectMany(graded => graded.Expected!.SourceNotes).ToImmutableArray(),
            events.Select(graded => graded.Expected!.Pitch).ToImmutableArray(),
            wrongPlayedPitches.ToImmutableArray(),
            mistakeCount,
            IsFirstTryCorrect: !hasPitchMiss && !hasOnsetMiss && !hasDurationMiss,
            IsComplete: true,
            CompletedAt: null)
        {
            IsPitchFirstTryCorrect = !hasPitchMiss,
            OnsetVerdict = onsetVerdict,
            DurationVerdict = durationVerdict,
            OnsetDeviation = onsetDeviation,
            WasMissed = wasMissed,
        };
    }

    private static Verdict ClassifyOnset(TimeSpan deviation, TimeSpan tolerance) =>
        deviation < -tolerance
            ? Verdict.Early
            : deviation > tolerance
                ? Verdict.Late
                : Verdict.Correct;
}
