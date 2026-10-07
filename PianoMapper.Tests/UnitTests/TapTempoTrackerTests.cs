using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class TapTempoTrackerTests
{
    [Fact]
    public void Register_FourSteadyAudioClockTaps_ReturnsTheTempo()
    {
        var tracker = new TapTempoTracker();

        Assert.Null(tracker.Register(TimeSpan.Zero));
        Assert.Null(tracker.Register(TimeSpan.FromSeconds(0.5)));
        Assert.Null(tracker.Register(TimeSpan.FromSeconds(1)));

        int? tempo = tracker.Register(TimeSpan.FromSeconds(1.5));

        Assert.Equal(120, tempo);
    }

    [Fact]
    public void Register_OneValidOutlier_UsesTheMedianInterval()
    {
        var tracker = new TapTempoTracker();
        tracker.Register(TimeSpan.Zero);
        tracker.Register(TimeSpan.FromSeconds(0.5));
        tracker.Register(TimeSpan.FromSeconds(1.5));

        int? tempo = tracker.Register(TimeSpan.FromSeconds(2));

        Assert.Equal(120, tempo);
    }

    [Fact]
    public void Register_GapOutsideTheSupportedRange_RestartsTheTapSequence()
    {
        var tracker = new TapTempoTracker();
        tracker.Register(TimeSpan.Zero);
        tracker.Register(TimeSpan.FromSeconds(0.5));

        int? tempo = tracker.Register(TimeSpan.FromSeconds(4));

        Assert.Null(tempo);
        Assert.Equal(1, tracker.TapCount);
    }
}
