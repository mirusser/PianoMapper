using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreGrandStaffWindowPairTests
{
    [Fact]
    public void FromPageIndex_InitialPage_ReturnsUpperPageAndLowerLookAhead()
    {
        var state = ScoreGrandStaffWindowPair.FromPageIndex(measureCount: 20, activePageIndex: 0);

        Assert.Equal(0, state.ActivePageIndex);
        Assert.Equal(0, state.UpperFirstMeasure);
        Assert.Equal(GrandStaffLayout.VisibleMeasureCount, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Upper, state.ActiveRow);
    }

    [Fact]
    public void FromCursorBeats_ExactPageBoundary_SelectsIncomingLowerPage()
    {
        int beatsPerMeasure = 4;
        double boundaryBeats = GrandStaffLayout.VisibleMeasureCount * beatsPerMeasure;

        var state = ScoreGrandStaffWindowPair.FromCursorBeats(
            measureCount: 20,
            boundaryBeats,
            beatsPerMeasure);

        Assert.Equal(1, state.ActivePageIndex);
        Assert.Equal(GrandStaffLayout.VisibleMeasureCount * 2, state.UpperFirstMeasure);
        Assert.Equal(GrandStaffLayout.VisibleMeasureCount, state.LowerFirstMeasure);
        Assert.Equal(ScoreGrandStaffWindowPair.PhysicalRow.Lower, state.ActiveRow);
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

        Assert.Equal(expectedUpperFirstMeasure, state.UpperFirstMeasure);
        Assert.Equal(expectedLowerFirstMeasure, state.LowerFirstMeasure);
        Assert.Equal((ScoreGrandStaffWindowPair.PhysicalRow)expectedActiveRow, state.ActiveRow);
    }

    [Fact]
    public void FromCursorBeats_DirectMultiPageJump_DerivesTargetPairFromAbsolutePosition()
    {
        int beatsPerMeasure = 3;
        double pageThreeBeats = GrandStaffLayout.VisibleMeasureCount * 3 * beatsPerMeasure;

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
            measureCount: GrandStaffLayout.VisibleMeasureCount * 3,
            requestedPageIndex);

        Assert.Equal(expectedPageIndex, state.ActivePageIndex);
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
