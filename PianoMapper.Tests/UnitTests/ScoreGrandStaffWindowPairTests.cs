using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreGrandStaffWindowPairTests
{
    [Fact]
    public void FromCursorBeats_ImplicitPickup_SelectsTheFollowingPageAtItsActualStart()
    {
        var score = new Score(
            "Pickup",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            Enumerable.Range(0, 10)
                .Select(index => new ScoreMeasure([], [], LengthInBeats: index == 0 ? 1 : null))
                .ToArray());

        var state = ScoreGrandStaffWindowPair.FromCursorBeats(
            score,
            cursorBeats: 17,
            visibleMeasureCount: GrandStaffLayout.DefaultVisibleMeasureCount);

        Assert.Equal(1, state.ActivePageIndex);
        Assert.Equal(GrandStaffLayout.DefaultVisibleMeasureCount, state.LowerFirstMeasure);
    }

    [Fact]
    public void FromCursorBeats_ExactPageBoundary_SelectsIncomingLowerPage()
    {
        int beatsPerMeasure = 4;
        double boundaryBeats = GrandStaffLayout.DefaultVisibleMeasureCount * beatsPerMeasure;

        var state = ScoreGrandStaffWindowPair.FromCursorBeats(
            measureCount: 20,
            boundaryBeats,
            beatsPerMeasure);

        Assert.Equal(1, state.ActivePageIndex);
        Assert.Equal(GrandStaffLayout.DefaultVisibleMeasureCount * 2, state.UpperFirstMeasure);
        Assert.Equal(GrandStaffLayout.DefaultVisibleMeasureCount, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Lower, state.ActiveRow);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(16, 4)]
    public void GetPageCount_DefaultWindow_RoundsUpToWholePages(int measureCount, int expectedPageCount)
    {
        Assert.Equal(expectedPageCount, ScoreGrandStaffWindowPair.GetPageCount(measureCount));
    }

    [Fact]
    public void GetPageCount_CustomWindow_UsesTheVisibleMeasureCount()
    {
        Assert.Equal(4, ScoreGrandStaffWindowPair.GetPageCount(measureCount: 10, visibleMeasureCount: 3));
    }

    [Fact]
    public void GetPageCount_NonPositiveWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScoreGrandStaffWindowPair.GetPageCount(measureCount: 8, visibleMeasureCount: 0));
    }

    [Theory]
    [InlineData(0, 0, 5, 0)]
    [InlineData(1, 10, 5, 1)]
    [InlineData(2, 10, 15, 0)]
    public void FromPageIndex_SequentialPages_AlternatesPhysicalRows(
        int activePageIndex,
        int expectedUpperFirstMeasure,
        int expectedLowerFirstMeasure,
        int expectedActiveRow)
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(measureCount: 20, activePageIndex);

        Assert.Equal(activePageIndex, state.ActivePageIndex);
        Assert.Equal(expectedUpperFirstMeasure, state.UpperFirstMeasure);
        Assert.Equal(expectedLowerFirstMeasure, state.LowerFirstMeasure);
        Assert.Equal((ScoreGrandStaffWindowPair.PhysicalRow)expectedActiveRow, state.ActiveRow);
    }

    [Theory]
    [InlineData(0, 0, 5, 0)]
    [InlineData(1, 0, 5, 1)]
    [InlineData(2, 10, 5, 0)]
    [InlineData(3, 10, 15, 1)]
    [InlineData(4, 20, 15, 0)]
    public void FromLivePageIndex_UnboundedPages_AlternatesRowsAndKeepsThePreviousPageVisible(
        int activePageIndex,
        int expectedUpperFirstMeasure,
        int expectedLowerFirstMeasure,
        int expectedActiveRow)
    {
        var state = ScoreGrandStaffWindowPair.FromLivePageIndex(activePageIndex);

        Assert.Equal(activePageIndex, state.ActivePageIndex);
        Assert.Equal(expectedUpperFirstMeasure, state.UpperFirstMeasure);
        Assert.Equal(expectedLowerFirstMeasure, state.LowerFirstMeasure);
        Assert.Equal((ScoreGrandStaffWindowPair.PhysicalRow)expectedActiveRow, state.ActiveRow);
    }

    [Fact]
    public void FromLivePageIndex_CustomVisibleMeasureCount_UsesConfiguredPageSize()
    {
        var state = ScoreGrandStaffWindowPair.FromLivePageIndex(activePageIndex: 3, visibleMeasureCount: 2);

        Assert.Equal(4, state.UpperFirstMeasure);
        Assert.Equal(6, state.LowerFirstMeasure);
    }

    [Fact]
    public void FromLivePageIndex_NegativePage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoreGrandStaffWindowPair.FromLivePageIndex(-1));
    }

    [Fact]
    public void FromCursorBeats_DirectMultiPageJump_DerivesTargetPairFromAbsolutePosition()
    {
        int beatsPerMeasure = 3;
        double pageThreeBeats = GrandStaffLayout.DefaultVisibleMeasureCount * 3 * beatsPerMeasure;

        var state = ScoreGrandStaffWindowPair.FromCursorBeats(
            measureCount: 24,
            pageThreeBeats,
            beatsPerMeasure);

        Assert.Equal(3, state.ActivePageIndex);
        Assert.Equal(20, state.UpperFirstMeasure);
        Assert.Equal(15, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Lower, state.ActiveRow);
    }

    [Fact]
    public void FromPageIndex_OnePageScore_OmitsLowerRow()
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(measureCount: 5, activePageIndex: 0);

        Assert.Equal(0, state.UpperFirstMeasure);
        Assert.Null(state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Upper, state.ActiveRow);
    }

    [Fact]
    public void FromPageIndex_PartialFinalPage_RetainsPreviousPageInInactiveRow()
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(measureCount: 13, activePageIndex: 2);

        Assert.Equal(2, state.ActivePageIndex);
        Assert.Equal(10, state.UpperFirstMeasure);
        Assert.Equal(5, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Upper, state.ActiveRow);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(10, 2)]
    public void FromPageIndex_RequestedPage_ClampsToScoreBounds(
        int requestedPageIndex,
        int expectedPageIndex)
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: GrandStaffLayout.DefaultVisibleMeasureCount * 3,
            requestedPageIndex);

        Assert.Equal(expectedPageIndex, state.ActivePageIndex);
    }

    [Fact]
    public void FromPageIndex_CustomVisibleMeasureCount_UsesConfiguredPageSize()
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex: 1,
            visibleMeasureCount: 2);

        Assert.Equal(1, state.ActivePageIndex);
        Assert.Equal(4, state.UpperFirstMeasure);
        Assert.Equal(2, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Lower, state.ActiveRow);
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(0)]
    public void FromCursorBeats_NonPositiveBeats_SelectsFirstPage(double cursorBeats)
    {
        var state = ScoreGrandStaffWindowPair.FromCursorBeats(
            measureCount: 20,
            cursorBeats,
            beatsPerMeasure: 4);

        Assert.Equal(0, state.ActivePageIndex);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Upper, state.ActiveRow);
    }
}
