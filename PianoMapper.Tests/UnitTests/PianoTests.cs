using PianoMapper.Web.Pages;
using PianoMapper.Web.Rendering;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoTests
{
    [Fact]
    public void ShouldContinueVisualizationRefresh_LiveGrandStaffWithVisibleNote_ReturnsTrue()
    {
        var scene = CreateGrandStaffSceneWithVisibleNote();

        bool shouldContinue = Piano.ShouldContinueVisualizationRefresh(
            showPianoRoll: false,
            hasLoadedScore: false,
            scene,
            activeNoteCount: 0);

        Assert.True(shouldContinue);
    }

    [Fact]
    public void ShouldContinueVisualizationRefresh_LiveGrandStaffWithoutNotes_ReturnsTrue()
    {
        var scene = new GrandStaffScene([], [], []);

        bool shouldContinue = Piano.ShouldContinueVisualizationRefresh(
            showPianoRoll: false,
            hasLoadedScore: false,
            scene,
            activeNoteCount: 0);

        Assert.True(shouldContinue);
    }

    [Fact]
    public void ShouldContinueVisualizationRefresh_ImportedScoreGrandStaff_ReturnsFalse()
    {
        var scene = CreateGrandStaffSceneWithVisibleNote();

        bool shouldContinue = Piano.ShouldContinueVisualizationRefresh(
            showPianoRoll: false,
            hasLoadedScore: true,
            scene,
            activeNoteCount: 0);

        Assert.False(shouldContinue);
    }

    [Fact]
    public void ShouldContinueVisualizationRefresh_ScheduledKeyboardNote_ReturnsTrue()
    {
        var scene = CreateGrandStaffSceneWithVisibleNote();

        bool shouldContinue = Piano.ShouldContinueVisualizationRefresh(
            showPianoRoll: false,
            hasLoadedScore: true,
            scene,
            activeNoteCount: 0,
            hasScheduledKeyboardNotes: true);

        Assert.True(shouldContinue);
    }

    [Theory]
    [InlineData(20, 0, 1, false, false, true)]
    [InlineData(20, 1, -1, false, false, true)]
    [InlineData(20, 0, -1, false, false, false)]
    [InlineData(20, 3, 1, false, false, false)]
    [InlineData(20, 0, 1, true, false, false)]
    [InlineData(20, 0, 1, false, true, false)]
    public void CanChangeScorePage_Request_ReturnsExpectedPolicy(
        int measureCount,
        int activePageIndex,
        int pageDelta,
        bool isScorePlaybackActive,
        bool isPracticeActive,
        bool expected)
    {
        bool canChange = Piano.CanChangeScorePage(
            measureCount,
            activePageIndex,
            pageDelta,
            isScorePlaybackActive,
            isPracticeActive);

        Assert.Equal(expected, canChange);
    }

    [Theory]
    [InlineData(0, (int)ScoreGrandStaffWindowPair.PhysicalRow.Upper, true)]
    [InlineData(0, (int)ScoreGrandStaffWindowPair.PhysicalRow.Lower, false)]
    [InlineData(1, (int)ScoreGrandStaffWindowPair.PhysicalRow.Upper, false)]
    [InlineData(1, (int)ScoreGrandStaffWindowPair.PhysicalRow.Lower, true)]
    public void GetPerformedNotesForScoreRow_CurrentAndLookAheadRows_ReturnsNotesOnlyForCurrentRow(
        int activePageIndex,
        int renderedRowValue,
        bool expectsNotes)
    {
        var performedNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var windowPair = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex);

        var result = Piano.GetPerformedNotesForScoreRow(
            [performedNote],
            windowPair,
            (ScoreGrandStaffWindowPair.PhysicalRow)renderedRowValue,
            suppressPerformedNotes: false);

        if (expectsNotes)
        {
            Assert.Same(performedNote, Assert.Single(result));
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Fact]
    public void GetPerformedNotesForScoreRow_IdleNoteCheckingEnabled_OmitsLiveMarker()
    {
        var performedNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var windowPair = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex: 0);

        var result = Piano.GetPerformedNotesForScoreRow(
            [performedNote],
            windowPair,
            ScoreGrandStaffWindowPair.PhysicalRow.Upper,
            suppressPerformedNotes: true);

        Assert.Empty(result);
    }

    private static GrandStaffScene CreateGrandStaffSceneWithVisibleNote() =>
        new([], [], [new GrandStaffNote("C4", 0, 0, 1, IsActive: false)]);
}
