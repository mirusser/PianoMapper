using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class TempoFeedbackTrackerTests
{
    [Fact]
    public void Record_ActiveGrid_UpdatesLastAlignmentAndSummary()
    {
        var tracker = new TempoFeedbackTracker();
        var grid = new MetronomeGrid(
            TimeSpan.FromSeconds(10),
            new Tempo(60),
            new TimeSignature(4, new NoteValue(4)));

        tracker.Record(grid.Anchor - TimeSpan.FromMilliseconds(40), grid, TimeSpan.FromMilliseconds(60));
        tracker.Record(grid.Anchor + TimeSpan.FromMilliseconds(80), grid, TimeSpan.FromMilliseconds(60));

        Assert.Equal(Verdict.Late, tracker.LastAlignment?.Verdict);
        Assert.Equal(TimeSpan.FromMilliseconds(80), tracker.LastAlignment?.Deviation);
        Assert.Equal(1, tracker.OnTimeCount);
        Assert.Equal(2, tracker.TotalCount);
        Assert.Equal(TimeSpan.FromMilliseconds(20), tracker.MedianDeviation);
    }

    [Fact]
    public void Record_InactiveGrid_DoesNotTrackOnset()
    {
        var tracker = new TempoFeedbackTracker();

        var alignment = tracker.Record(TimeSpan.FromSeconds(1), null, TimeSpan.FromMilliseconds(60));

        Assert.Null(alignment);
        Assert.Equal(0, tracker.TotalCount);
        Assert.Null(tracker.LastAlignment);
    }

    [Fact]
    public void Record_OverTenOnsets_EvictsOldestAndResetClearsSummary()
    {
        var tracker = new TempoFeedbackTracker();
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(60),
            new TimeSignature(4, new NoteValue(4)));
        for (int beatIndex = 0; beatIndex < 12; beatIndex++)
        {
            tracker.Record(grid.GetBeatTime(beatIndex), grid, TimeSpan.FromMilliseconds(60));
        }

        Assert.Equal(10, tracker.TotalCount);
        Assert.Equal(10, tracker.OnTimeCount);
        Assert.Equal(2, tracker.RecentAlignments[0].BeatIndex);
        Assert.Equal(11, tracker.RecentAlignments[^1].BeatIndex);

        tracker.Reset();

        Assert.Empty(tracker.RecentAlignments);
        Assert.Null(tracker.LastAlignment);
        Assert.Null(tracker.MedianDeviation);
    }
}
