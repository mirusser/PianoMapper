using PianoMapper.Practice;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoMapperWindowTests
{
    [Fact]
    public void BuildPracticeSummaryLines_AllVerdicts_IncludesEveryCount()
    {
        var counts = Enum.GetValues<Verdict>().ToDictionary(verdict => verdict, verdict => (int)verdict + 1);
        var summary = new GradingSummary(42, counts);

        var lines = PianoMapperWindow.BuildPracticeSummaryLines(summary);
        string renderedText = string.Join(' ', lines);

        Assert.Contains("SCORE 42", renderedText);
        Assert.Contains("CORRECT 1", renderedText);
        Assert.Contains("WRONG 2", renderedText);
        Assert.Contains("EARLY 3", renderedText);
        Assert.Contains("LATE 4", renderedText);
        Assert.Contains("SHORT 5", renderedText);
        Assert.Contains("LONG 6", renderedText);
        Assert.Contains("MISSED 7", renderedText);
        Assert.Contains("EXTRA 8", renderedText);
    }

    [Fact]
    public void GetRandomMeasureEventDuration_WholeNoteAtNinetyBpm_UsesRhythmBelowNaturalDecay()
    {
        var note = new RandomMeasureEvent(new Pitch(NoteLetter.A, 0, 4), new NoteValue(1), Beats: 4);

        TimeSpan duration = PianoMapperWindow.GetRandomMeasureEventDuration(
            note,
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(90));

        Assert.Equal(TimeSpan.FromMinutes(4.0 / 90.0), duration);
    }
}
