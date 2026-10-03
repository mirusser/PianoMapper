using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed class PracticeSession
{
    private const double BeatComparisonTolerance = 1e-9;

    private readonly Score score;
    private readonly TimeProvider timeProvider;
    private readonly GradingOptions gradingOptions;
    private readonly IReadOnlyList<ScoreEvent> expectedEvents;
    private readonly TimeSpan countInDuration;
    private readonly TimeSpan performanceDuration;

    private long startTimestamp;
    private TimeSpan clockOrigin;

    public PracticeSession(Score score, TimeProvider timeProvider, GradingOptions? gradingOptions = null)
    {
        this.score = score;
        this.timeProvider = timeProvider;
        this.gradingOptions = gradingOptions ?? new GradingOptions();
        expectedEvents = ScoreDerivation.Flatten(score);
        countInDuration = MusicalTime.BeatsToDuration(score.TimeSignature.Numerator, score.Tempo);
        double totalBeats = expectedEvents.Count == 0
            ? 0
            : expectedEvents.Max(scoreEvent => scoreEvent.OnsetBeats + scoreEvent.DurationBeats);
        performanceDuration = MusicalTime.BeatsToDuration(totalBeats, score.Tempo);
    }

    public PracticeSessionState State { get; private set; } = PracticeSessionState.Idle;

    public TimeSpan PracticeAnchor { get; private set; }

    /// <summary>The audio-clock instant at which the score's last expected note finishes.</summary>
    public TimeSpan PerformanceCompletionTime => PracticeAnchor + performanceDuration;

    public int CountInTicksDue
    {
        get
        {
            if (State == PracticeSessionState.Idle)
            {
                return 0;
            }

            double elapsedBeats = MusicalTime.DurationToBeats(GetElapsedTime(), score.Tempo);
            return Math.Clamp((int)Math.Floor(elapsedBeats) + 1, 0, score.TimeSignature.Numerator);
        }
    }

    public double CursorBeats => State switch
    {
        PracticeSessionState.Running or PracticeSessionState.Finished => Math.Clamp(
            MusicalTime.DurationToBeats(CurrentTime - PracticeAnchor, score.Tempo),
            0,
            MusicalTime.DurationToBeats(performanceDuration, score.Tempo)),
        _ => 0,
    };

    public TimeSpan CurrentTime => clockOrigin + GetElapsedTime();

    public void Start(TimeSpan currentClockTime)
    {
        clockOrigin = currentClockTime;
        startTimestamp = timeProvider.GetTimestamp();
        PracticeAnchor = currentClockTime + countInDuration;
        State = PracticeSessionState.CountingIn;
    }

    public void Update()
    {
        if (State == PracticeSessionState.Idle || State == PracticeSessionState.Finished)
        {
            return;
        }

        TimeSpan elapsed = GetElapsedTime();
        if (elapsed >= countInDuration + performanceDuration + gradingOptions.OnsetTolerance)
        {
            State = PracticeSessionState.Finished;
        }
        else if (elapsed >= countInDuration)
        {
            State = PracticeSessionState.Running;
        }
    }

    public GradingResult Grade(IReadOnlyList<PerformedNote> performedNotes)
    {
        var sessionNotes = GetSessionNotes(performedNotes);
        return Grader.Grade(
            expectedEvents,
            score.Tempo,
            sessionNotes,
            PracticeAnchor,
            CurrentTime,
            gradingOptions);
    }

    public IReadOnlyList<PerformedNote> GetSessionNotes(IReadOnlyList<PerformedNote> performedNotes)
    {
        ArgumentNullException.ThrowIfNull(performedNotes);
        TimeSpan earliestIncludedOnset = PracticeAnchor - gradingOptions.OnsetTolerance;
        return performedNotes
            .Where(note => note.StartTime >= earliestIncludedOnset && note.StartTime <= CurrentTime)
            .ToArray();
    }

    public bool IsVerdictDue(ScoreEvent expected) =>
        State == PracticeSessionState.Finished ||
        CurrentTime >= PracticeAnchor + MusicalTime.BeatsToDuration(expected.OnsetBeats, score.Tempo) + gradingOptions.OnsetTolerance;

    /// <summary>
    /// The pitches of the chord to play next: the earliest onset whose grading window has not closed yet (the same
    /// window <see cref="IsVerdictDue"/> uses). Empty while idle, once finished, or when every window has closed.
    /// </summary>
    public IReadOnlyList<Pitch> GetNextPitches()
    {
        if (State is PracticeSessionState.Idle or PracticeSessionState.Finished)
        {
            return [];
        }

        ScoreEvent[] upcomingEvents = expectedEvents.Where(expected => !IsVerdictDue(expected)).ToArray();
        if (upcomingEvents.Length == 0)
        {
            return [];
        }

        double nextOnsetBeats = upcomingEvents.Min(expected => expected.OnsetBeats);
        return upcomingEvents
            .Where(expected => expected.OnsetBeats - nextOnsetBeats <= BeatComparisonTolerance)
            .Select(expected => expected.Pitch)
            .Distinct()
            .OrderBy(pitch => pitch.MidiNumber)
            .ToArray();
    }

    public IReadOnlyDictionary<ScoreNote, Verdict> BuildVisibleVerdicts(GradingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var verdicts = new Dictionary<ScoreNote, Verdict>();
        foreach (var gradedEvent in result.Events)
        {
            if (gradedEvent.Expected is not { } expected || !IsVerdictDue(expected))
            {
                continue;
            }

            foreach (var sourceNote in expected.SourceNotes)
            {
                verdicts.TryAdd(sourceNote, gradedEvent.Verdict);
            }
        }

        return verdicts;
    }

    public void Abort()
    {
        State = PracticeSessionState.Idle;
    }

    private TimeSpan GetElapsedTime() =>
        State == PracticeSessionState.Idle ? TimeSpan.Zero : timeProvider.GetElapsedTime(startTimestamp);
}
