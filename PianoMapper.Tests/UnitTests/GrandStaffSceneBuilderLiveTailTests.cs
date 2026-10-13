using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// The held-note tail of the live grand staff, when the browser grows it from the audio clock (the audio-enabled path)
// instead of the scene carrying a clock-dependent end for every tick.
public sealed partial class GrandStaffSceneBuilderTests
{
    [Fact]
    public void BuildLivePair_TailGrownInBrowser_HeldNoteCarriesTheEndOfItsMeasureInsteadOfAnEndOfItsOwn()
    {
        PerformedNote held = LiveTailHeldNote(NoteLetter.C, startBeat: 1);

        var (upper, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            LiveRowsTime(2.5),
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);

        var note = Assert.Single(upper.Notes);
        Assert.True(note.IsActive);
        Assert.Null(note.DurationEndX);
        Assert.Equal(0, note.DurationSeconds);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(4, LiveRowsSignature, firstVisibleMeasure: 0),
            note.DurationGrowthLimitX!.Value,
            6);
    }

    [Fact]
    public void BuildLivePair_TailGrownInBrowser_NoteAndLedgerGeometryMatchTheClockDrivenScene()
    {
        // The head, its ledger line and everything else stay exactly where the clock-driven scene puts them; only the
        // tail's end moves to the browser.
        PerformedNote held = LiveTailHeldNote(NoteLetter.C, startBeat: 1);
        TimeSpan now = LiveRowsTime(2.5);

        var (clockDriven, _) = GrandStaffSceneBuilder.BuildLivePair([held], now, LiveRowsSignature, LiveRowsTempo);
        var (browserDriven, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            now,
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);

        var expected = Assert.Single(clockDriven.Notes);
        var actual = Assert.Single(browserDriven.Notes);
        Assert.Equal(
            expected with { DurationEndX = null, DurationSeconds = 0 },
            actual with { DurationGrowthLimitX = null });
        Assert.Equal(clockDriven.Lines, browserDriven.Lines);
        Assert.Equal(clockDriven.Glyphs, browserDriven.Glyphs);
        Assert.Equal(clockDriven.Ties, browserDriven.Ties);
    }

    [Fact]
    public void BuildLivePair_TailGrownInBrowser_SceneStaysTheSameWhileTheClockStaysInTheMeasure()
    {
        PerformedNote held = LiveTailHeldNote(NoteLetter.G, startBeat: 1);

        var (early, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            LiveRowsTime(1.5),
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);
        var (late, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            LiveRowsTime(3.75),
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);

        Assert.Equal(early.Notes, late.Notes);
        Assert.Equal(early.Ties, late.Ties);
        Assert.Equal(early.Glyphs, late.Glyphs);
        Assert.Equal(
            early.Lines.Where(line => line.Kind != GrandStaffLineKind.Cursor),
            late.Lines.Where(line => line.Kind != GrandStaffLineKind.Cursor));
    }

    [Fact]
    public void BuildLivePair_TailGrownInBrowser_HeldNoteCrossingABarlineLimitsTheTailToTheMeasureTheClockIsIn()
    {
        PerformedNote held = LiveTailHeldNote(NoteLetter.E, startBeat: 3);

        var (upper, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            LiveRowsTime(5.5),
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);

        Assert.Equal(2, upper.Notes.Count);
        Assert.False(upper.Notes[0].IsActive);
        Assert.Null(upper.Notes[0].DurationGrowthLimitX);
        Assert.True(upper.Notes[1].IsActive);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(8, LiveRowsSignature, firstVisibleMeasure: 0),
            upper.Notes[1].DurationGrowthLimitX!.Value,
            6);
    }

    [Fact]
    public void BuildLivePair_TailGrownInBrowser_ReleasedNoteIsDrawnAsAFinishedNote()
    {
        PerformedNote released = LiveRowsNote(NoteLetter.C, startBeat: 1, endBeat: 2);

        var (upper, _) = GrandStaffSceneBuilder.BuildLivePair(
            [released],
            LiveRowsTime(3),
            LiveRowsSignature,
            LiveRowsTempo,
            growsActiveNotesInBrowser: true);

        var note = Assert.Single(upper.Notes);
        Assert.False(note.IsActive);
        Assert.Null(note.DurationGrowthLimitX);
    }

    [Fact]
    public void BuildLivePair_TailGrownByTheClock_HeldNoteKeepsItsOwnEndAndHasNoGrowthLimit()
    {
        PerformedNote held = LiveTailHeldNote(NoteLetter.C, startBeat: 1);

        var (upper, _) = GrandStaffSceneBuilder.BuildLivePair(
            [held],
            LiveRowsTime(2.5),
            LiveRowsSignature,
            LiveRowsTempo);

        var note = Assert.Single(upper.Notes);
        Assert.NotNull(note.DurationEndX);
        Assert.Null(note.DurationGrowthLimitX);
    }

    private static PerformedNote LiveTailHeldNote(NoteLetter letter, double startBeat) =>
        new()
        {
            Pitch = new Pitch(letter, 0, 4),
            StartTime = LiveRowsTime(startBeat),
        };
}
