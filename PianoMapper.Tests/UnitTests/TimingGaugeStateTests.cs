using PianoMapper.Practice;
using PianoMapper.Web.Components;

namespace PianoMapper.Tests.UnitTests;

public sealed class TimingGaugeStateTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(60);

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(200, 0.75)]
    [InlineData(-200, 0.25)]
    [InlineData(400, 1.0)]
    [InlineData(-400, 0.0)]
    [InlineData(5000, 1.0)]
    [InlineData(-5000, 0.0)]
    public void GetPosition_PlacesTheBeatInTheMiddleAndClampsFarMissesToAnEdge(double milliseconds, double expected) =>
        Assert.Equal(expected, TimingGaugeState.GetPosition(TimeSpan.FromMilliseconds(milliseconds)), precision: 9);

    [Theory]
    [InlineData(0, Verdict.Correct)]
    [InlineData(60, Verdict.Correct)]
    [InlineData(-60, Verdict.Correct)]
    [InlineData(61, Verdict.Late)]
    [InlineData(-61, Verdict.Early)]
    [InlineData(5000, Verdict.Late)]
    public void Classify_MatchesTheExercisesOnTimeToleranceInclusively(double milliseconds, Verdict expected) =>
        Assert.Equal(expected, GaugeWith().Classify(TimeSpan.FromMilliseconds(milliseconds)));

    [Theory]
    [InlineData(0, "On time")]
    [InlineData(45, "On time")]
    [InlineData(120, "+120 ms late")]
    [InlineData(-85, "−85 ms early")]
    [InlineData(1234.4, "+1234 ms late")]
    public void Describe_NamesTheOffsetAndItsDirection(double milliseconds, string expected) =>
        Assert.Equal(expected, GaugeWith().Describe(TimeSpan.FromMilliseconds(milliseconds)));

    [Theory]
    [InlineData(0, -1, Verdict.Early)]
    [InlineData(0, 1, Verdict.Late)]
    [InlineData(-200, 3, Verdict.Late)]
    [InlineData(200, -2, Verdict.Early)]
    public void Classify_AWrongBeatIsEarlyOrLateByItsDirectionWhateverTheOffsetFromThePulse(
        double milliseconds,
        int beatShift,
        Verdict expected) =>
        Assert.Equal(expected, GaugeWith().Classify(TimeSpan.FromMilliseconds(milliseconds), beatShift));

    [Theory]
    [InlineData(0, 1, "a beat late")]
    [InlineData(20, -1, "a beat early")]
    [InlineData(0, 3, "3 beats late")]
    [InlineData(-85, -2, "2 beats early · −85 ms")]
    [InlineData(120, 1, "a beat late · +120 ms")]
    public void Describe_AWrongBeatNamesTheBeatsAndOnlyAddsAnOffsetThatIsOffThePulse(
        double milliseconds,
        int beatShift,
        string expected) =>
        Assert.Equal(expected, GaugeWith().Describe(TimeSpan.FromMilliseconds(milliseconds), beatShift));

    [Fact]
    public void LatestVerdictAndDescription_FollowTheLatestNotesBeatShift()
    {
        TimingGaugeState gauge = new([TimeSpan.FromMilliseconds(10)], Tolerance, LatestBeatShift: -1);

        Assert.Equal(Verdict.Early, gauge.LatestVerdict);
        Assert.Equal("a beat early", gauge.DescribeLatest());
        Assert.Null(GaugeWith().LatestVerdict);
        Assert.Null(GaugeWith().DescribeLatest());
        Assert.Equal("On time", GaugeWith(TimeSpan.FromMilliseconds(5)).DescribeLatest());
    }

    [Fact]
    public void OnTimeHalfWidth_IsTheToleranceAsAFractionOfTheScale()
    {
        Assert.Equal(60.0 / 400.0 / 2.0, GaugeWith().OnTimeHalfWidth, precision: 9);
    }

    [Fact]
    public void OnTimeHalfWidth_ToleranceBeyondTheScale_FillsItButNotMore() =>
        Assert.Equal(0.5, new TimingGaugeState([], TimeSpan.FromSeconds(2)).OnTimeHalfWidth, precision: 9);

    [Fact]
    public void Latest_IsTheLastDeviationAndNullBeforeAnyNote()
    {
        Assert.Null(GaugeWith().Latest);
        Assert.Equal(
            TimeSpan.FromMilliseconds(30),
            GaugeWith(TimeSpan.FromMilliseconds(-10), TimeSpan.FromMilliseconds(30)).Latest);
    }

    private static TimingGaugeState GaugeWith(params TimeSpan[] deviations) => new(deviations, Tolerance);
}
