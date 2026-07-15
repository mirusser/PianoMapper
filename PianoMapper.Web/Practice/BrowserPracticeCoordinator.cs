using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Playback;

namespace PianoMapper.Web.Practice;

internal sealed class BrowserPracticeCoordinator(IBrowserScoreAudio audio, NoteTimeline timeline)
{
    private static readonly IReadOnlyDictionary<ScoreNote, Verdict> NoVerdicts =
        new Dictionary<ScoreNote, Verdict>();
    private static readonly TimeSpan SchedulingLead = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan CountInClickDuration = TimeSpan.FromMilliseconds(50);
    private const double CountInClickFrequency = 880;

    private readonly BrowserPracticeTimeProvider timeProvider = new();
    private PracticeSession? session;

    internal PracticeSessionState State => session?.State ?? PracticeSessionState.Idle;

    internal TimeSpan PracticeAnchor => session?.PracticeAnchor ?? TimeSpan.Zero;

    internal GradingResult? Result { get; private set; }

    internal bool IsActive => State is PracticeSessionState.CountingIn or PracticeSessionState.Running;

    internal double CursorBeats => session?.CursorBeats ?? 0;

    internal int CountInTicksDue => session?.CountInTicksDue ?? 0;

    internal TimeSpan CurrentTime { get; private set; }

    internal async ValueTask StartAsync(Score score, CancellationToken cancellationToken = default)
    {
        await audio.StopScoreAsync(cancellationToken);
        TimeSpan startTime = await audio.GetCurrentTimeAsync(cancellationToken) + SchedulingLead;
        CurrentTime = startTime;
        timeProvider.SetTime(startTime);
        session = new PracticeSession(score, timeProvider);
        session.Start(startTime);
        Result = null;

        TimeSpan beatDuration = MusicalTime.BeatsToDuration(1, score.Tempo);
        var countInEvents = Enumerable.Range(0, score.TimeSignature.Numerator)
            .Select(index => new BrowserScoreAudioEvent(
                $"practice-count-in-{index}",
                CountInClickFrequency,
                startTime + (beatDuration * index),
                CountInClickDuration))
            .ToArray();
        await audio.ScheduleScoreAsync(countInEvents, cancellationToken);
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
            Result = session.Grade(timeline.Snapshot(currentTime));
        }
    }

    internal IReadOnlyDictionary<ScoreNote, Verdict> GetVisibleVerdicts() =>
        session is not null && Result is not null
            ? session.BuildVisibleVerdicts(Result)
            : NoVerdicts;

    internal async ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        session?.Abort();
        Result = null;
        await audio.StopScoreAsync(cancellationToken);
    }
}
