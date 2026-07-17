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

    private static GrandStaffScene CreateGrandStaffSceneWithVisibleNote() =>
        new([], [], [new GrandStaffNote("C4", 0, 0, 1, IsActive: false)]);
}
