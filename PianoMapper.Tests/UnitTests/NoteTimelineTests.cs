using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class NoteTimelineTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly Pitch G4 = new(NoteLetter.G, 0, 4);

    [Fact]
    public void Start_SingleNote_AppearsInSnapshotWithPerformanceMetadata()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        time.Advance(TimeSpan.FromSeconds(2));

        var note = timeline.Start(C4);

        var entry = Assert.Single(timeline.Snapshot());
        Assert.Same(note, entry);
        Assert.Equal(C4, entry.Pitch);
        Assert.Equal(TimeSpan.FromSeconds(2), entry.StartTime);
        Assert.Null(entry.ReleaseTime);
    }

    [Fact]
    public void Start_MultipleNotes_AllAppearAsSeparateEntries()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        timeline.Start(C4);
        timeline.Start(E4);
        timeline.Start(G4);

        var snapshot = timeline.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Contains(snapshot, note => note.Pitch == C4);
        Assert.Contains(snapshot, note => note.Pitch == E4);
        Assert.Contains(snapshot, note => note.Pitch == G4);
    }

    [Fact]
    public async Task Start_FromMultipleThreadsConcurrently_AllNotesAppearInSnapshot()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        const int threadCount = 20;
        var tasks = Enumerable.Range(0, threadCount)
            .Select(index => Task.Run(() => timeline.Start(new Pitch(NoteLetter.C, 0, index))));

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount, timeline.Snapshot().Count);
    }

    [Fact]
    public void Snapshot_OpenNote_IsNeverPruned()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        var note = timeline.Start(C4);
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Contains(note, timeline.Snapshot());
    }

    [Fact]
    public void Snapshot_CompletedNoteJustPastRelease_StillPresent()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        var note = timeline.Start(C4);
        timeline.Complete(note, TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1.5));

        Assert.Single(timeline.Snapshot());
    }

    [Fact]
    public void Snapshot_CompletedNoteOlderThanRetentionWindow_IsPruned()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        var note = timeline.Start(C4);
        timeline.Complete(note, TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(NoteTimeline.RetentionSeconds + 2));

        Assert.Empty(timeline.Snapshot());
    }

    [Fact]
    public void Complete_ActiveNote_RecordsMeasuredReleaseTime()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var note = timeline.Start(C4);
        var releaseTime = TimeSpan.FromSeconds(0.4);

        timeline.Complete(note, releaseTime);

        Assert.Equal(releaseTime, note.ReleaseTime);
    }

    [Fact]
    public void Complete_RemovedNote_IsNoOp()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var note = timeline.Start(C4);
        timeline.Remove([note]);

        timeline.Complete(note, TimeSpan.FromSeconds(1));

        Assert.Null(note.ReleaseTime);
    }

    [Fact]
    public void Remove_OneOfValueEqualNotes_OnlyRemovesSpecifiedIdentity()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var noteToRemove = timeline.Start(C4);
        var noteToKeep = timeline.Start(C4);
        timeline.Remove([noteToRemove]);

        var entry = Assert.Single(timeline.Snapshot());
        Assert.Same(noteToKeep, entry);
    }

    [Fact]
    public void RetentionSeconds_ExceedsPianoRollRollingWindow()
    {
        Assert.True(
            NoteTimeline.RetentionSeconds > PianoRollLayout.RollingWindowSeconds,
            "NoteTimeline must retain notes longer than the piano-roll's rolling window, or bars would be pruned mid-scroll.");
    }
}
