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
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    public void CanChangeScorePage_CustomVisibleMeasureCount_UsesConfiguredPageSize(
        int activePageIndex,
        int pageDelta,
        bool expected)
    {
        bool canChange = Piano.CanChangeScorePage(
            measureCount: 6,
            activePageIndex,
            pageDelta,
            isScorePlaybackActive: false,
            isPracticeActive: false,
            visibleMeasureCount: 2);

        Assert.Equal(expected, canChange);
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    public void CanResetScoreHighlights_State_ReturnsExpectedPolicy(
        bool hasScore,
        bool isNoteCheckingEnabled,
        bool isScorePlaybackActive,
        bool isPracticeActive,
        bool expected)
    {
        bool canReset = Piano.CanResetScoreHighlights(
            hasScore,
            isNoteCheckingEnabled,
            isScorePlaybackActive,
            isPracticeActive);

        Assert.Equal(expected, canReset);
    }

    [Fact]
    public void FromPageIndex_RecomputedAfterVisibleMeasureCountChange_KeepsPreviouslyActiveMeasureVisible()
    {
        // Mirrors Piano.SelectVisibleMeasureCountAsync's position-preserving recompute: a user
        // viewing measure 10 (0-indexed) at the old count of 5 changes the count to 3. The new
        // page index is oldFirstMeasure / newVisibleMeasureCount, so the previously active measure
        // stays visible on the newly active row rather than snapping back to page 0.
        const int oldFirstMeasure = 10;
        const int newVisibleMeasureCount = 3;

        var state = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 30,
            oldFirstMeasure / newVisibleMeasureCount,
            newVisibleMeasureCount);

        int activeFirstMeasure = state.ActiveRow == ScoreGrandStaffWindowPair.PhysicalRow.Upper
            ? state.UpperFirstMeasure
            : state.LowerFirstMeasure!.Value;
        Assert.InRange(oldFirstMeasure, activeFirstMeasure, activeFirstMeasure + newVisibleMeasureCount - 1);
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
            (ScoreGrandStaffWindowPair.PhysicalRow)renderedRowValue);

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
    public void GetPerformedNotesForScoreRow_IdleNoteChecking_ReturnsOnlyWrongLiveMarkers()
    {
        var correctNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var wrongNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.D, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var windowPair = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex: 0);
        var wrongNotes = new HashSet<PerformedNote>(ReferenceEqualityComparer.Instance)
        {
            wrongNote,
        };

        var result = Piano.GetPerformedNotesForScoreRow(
            [correctNote, wrongNote],
            windowPair,
            ScoreGrandStaffWindowPair.PhysicalRow.Upper,
            wrongNotes);

        Assert.Same(wrongNote, Assert.Single(result));
    }

    private static GrandStaffScene CreateGrandStaffSceneWithVisibleNote() =>
        new([], [], [new GrandStaffNote("C4", 0, 0, 1, IsActive: false)]);
}
