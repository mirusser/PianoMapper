using Microsoft.Extensions.Time.Testing;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class NoteTimelineTests
{
    private static readonly short[] Samples = [1, 2, 3];

    [Fact]
    public void Add_SingleNote_AppearsInSnapshotWithMetadata()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        time.Advance(TimeSpan.FromSeconds(2));
        var note = timeline.Add("C4", 261.63f, 1.5f, Samples, sourceId: 1, bufferId: 2);

        var snapshot = timeline.Snapshot();

        var entry = Assert.Single(snapshot);
        Assert.Same(note, entry);
        Assert.Equal("C4", entry.NoteName);
        Assert.Equal(261.63f, entry.Frequency);
        Assert.Equal(TimeSpan.FromSeconds(2), entry.StartTime);
        Assert.Equal(1.5f, entry.Duration);
        Assert.Same(Samples, entry.Samples);
        Assert.Equal(1, entry.SourceId);
        Assert.Equal(2, entry.BufferId);
    }

    [Fact]
    public void Add_MultipleNotes_AllAppearAsSeparateEntries()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        timeline.Add("E4", 329.63f, 1f, Samples, sourceId: 2, bufferId: 2);
        timeline.Add("G4", 392.00f, 1f, Samples, sourceId: 3, bufferId: 3);

        var snapshot = timeline.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Contains(snapshot, n => n.NoteName == "C4");
        Assert.Contains(snapshot, n => n.NoteName == "E4");
        Assert.Contains(snapshot, n => n.NoteName == "G4");
    }

    [Fact]
    public async Task Add_FromMultipleThreadsConcurrently_AllNotesAppearInSnapshot()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        const int threadCount = 20;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(i => Task.Run(() => timeline.Add($"N{i}", 440f, 1f, Samples, sourceId: i, bufferId: i)));

        await Task.WhenAll(tasks);

        var snapshot = timeline.Snapshot();

        Assert.Equal(threadCount, snapshot.Count);
    }

    [Fact]
    public void Snapshot_NoteJustPastItsOwnDuration_StillPresent()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        time.Advance(TimeSpan.FromSeconds(1.5));

        var snapshot = timeline.Snapshot();

        Assert.Single(snapshot);
    }

    [Fact]
    public void Snapshot_NoteOlderThanRetentionWindow_IsPruned()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        time.Advance(TimeSpan.FromSeconds(60));

        var snapshot = timeline.Snapshot();

        Assert.Empty(snapshot);
    }

    [Fact]
    public void Remove_ActiveNote_NoLongerAppearsInSnapshot()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        var note = timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        timeline.Remove([note]);

        var snapshot = timeline.Snapshot();

        Assert.Empty(snapshot);
    }

    [Fact]
    public void Remove_OneOfMultipleNotes_OnlyRemovesSpecifiedNote()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        var noteToRemove = timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        var noteToKeep = timeline.Add("E4", 329.63f, 1f, Samples, sourceId: 2, bufferId: 2);

        timeline.Remove([noteToRemove]);

        var snapshot = timeline.Snapshot();

        var entry = Assert.Single(snapshot);
        Assert.Same(noteToKeep, entry);
    }

    [Fact]
    public void Remove_NotesWithIdenticalMetadataButDifferentSourceId_OnlyRemovesMatchingInstance()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);

        var noteToRemove = timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 1, bufferId: 1);
        var noteToKeep = timeline.Add("C4", 261.63f, 1f, Samples, sourceId: 2, bufferId: 2);

        timeline.Remove([noteToRemove]);

        var snapshot = timeline.Snapshot();

        var entry = Assert.Single(snapshot);
        Assert.Equal(2, entry.SourceId);
    }

    [Fact]
    public void RetentionSeconds_ExceedsPianoRollRollingWindow()
    {
        Assert.True(
            NoteTimeline.RetentionSeconds > PianoRollLayout.RollingWindowSeconds,
            "NoteTimeline must retain notes longer than the piano-roll's rolling window, or bars would be pruned mid-scroll.");
    }
}
