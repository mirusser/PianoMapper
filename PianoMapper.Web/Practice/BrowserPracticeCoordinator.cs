using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Playback;

namespace PianoMapper.Web.Practice;

internal sealed class BrowserPracticeCoordinator(IBrowserScoreAudio audio, NoteTimeline timeline)
{
    private static readonly IReadOnlyDictionary<ScoreNote, Verdict> NoVerdicts =
        new Dictionary<ScoreNote, Verdict>();
    private static readonly TimeSpan SchedulingLead = TimeSpan.FromMilliseconds(50);

    private readonly BrowserPracticeTimeProvider timeProvider = new();
    private readonly List<PerformedNote> capturedPerformedNotes = [];
    private readonly HashSet<PerformedNote> capturedPerformedNoteIdentities = new(ReferenceEqualityComparer.Instance);
    private PracticeSession? session;

    internal PracticeSessionState State => session?.State ?? PracticeSessionState.Idle;

    internal TimeSpan PracticeAnchor => session?.PracticeAnchor ?? TimeSpan.Zero;

    internal TimeSpan PerformanceCompletionTime => session?.PerformanceCompletionTime ?? TimeSpan.Zero;

    internal GradingResult? Result { get; private set; }

    internal bool IsActive => State is PracticeSessionState.CountingIn or PracticeSessionState.Running;

    internal double CursorBeats => session?.CursorBeats ?? 0;

    internal int CountInTicksDue => session?.CountInTicksDue ?? 0;

    internal TimeSpan CurrentTime { get; private set; }

    internal ValueTask StartAsync(Score score, CancellationToken cancellationToken = default) =>
        StartAsync(score, new GradingOptions(), cancellationToken);

    internal async ValueTask StartAsync(
        Score score,
        GradingOptions gradingOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gradingOptions);
        await audio.StopScoreAsync(cancellationToken);
        capturedPerformedNotes.Clear();
        capturedPerformedNoteIdentities.Clear();
        TimeSpan startTime = await audio.GetCurrentTimeAsync(cancellationToken) + SchedulingLead;
        CurrentTime = startTime;
        timeProvider.SetTime(startTime);
        session = new PracticeSession(score, timeProvider, gradingOptions);
        session.Start(startTime);
        Result = null;
    }

    internal async ValueTask UpdateAsync(CancellationToken cancellationToken = default)
    {
        if (session is null || State == PracticeSessionState.Idle)
        {
            return;
        }

        TimeSpan currentTime = await audio.GetCurrentTimeAsync(cancellationToken);
        CurrentTime = currentTime;
        timeProvider.SetTime(currentTime);
        session.Update();
        if (State is PracticeSessionState.Running or PracticeSessionState.Finished)
        {
            Result = session.Grade(GetPerformedNotes(currentTime));
        }
    }

    private IReadOnlyList<PerformedNote> GetPerformedNotes(TimeSpan currentTime)
    {
        IReadOnlyList<PerformedNote> snapshot = timeline.Snapshot(currentTime);
        foreach (PerformedNote note in snapshot)
        {
            if (capturedPerformedNoteIdentities.Add(note))
            {
                capturedPerformedNotes.Add(note);
            }
        }

        return capturedPerformedNotes.ToArray();
    }

    internal IReadOnlyDictionary<ScoreNote, Verdict> GetVisibleVerdicts() =>
        session is not null && Result is not null
            ? session.BuildVisibleVerdicts(Result)
            : NoVerdicts;

    internal IReadOnlyList<Pitch> GetNextPitches() => session?.GetNextPitches() ?? [];

    internal async ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        session?.Abort();
        Result = null;
        capturedPerformedNotes.Clear();
        capturedPerformedNoteIdentities.Clear();
        await audio.StopScoreAsync(cancellationToken);
    }
}
