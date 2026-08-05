using PianoMapper.Web.Pages;
using PianoMapper.Web.Rendering;

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

    private static GrandStaffScene CreateGrandStaffSceneWithVisibleNote() =>
        new([], [], [new GrandStaffNote("C4", 0, 0, 1, IsActive: false)]);
}
