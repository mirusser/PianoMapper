using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// The fit keeps ink inside the largest .score-row-canvas (304 px, where the 18 px canvas margin reaches least far past the
// scene's -1..1) with 4 px to spare. Positions below are pixels from the top of that canvas, by canvas.js's own mapping,
// not the fit's own constants.
public sealed class GrandStaffVerticalFitTests
{
    private const double CanvasHeightPixels = 304;
    private const double ClearancePixels = 4;

    private static double PixelY(GrandStaffVerticalFit fit, double sceneY) =>
        GrandStaffSceneBuilderTests.CanvasPixelY(fit.Map(sceneY), CanvasHeightPixels);

    private static GrandStaffVerticalFit.InkEdge[] Edge(double y, double fixedPixels = 0) =>
        [new GrandStaffVerticalFit.InkEdge(y, fixedPixels)];

    [Fact]
    public void Create_InkInsideTheCanvas_IsIdentity()
    {
        var fit = GrandStaffVerticalFit.Create(Edge(0.9), Edge(-0.9));

        Assert.True(fit.IsIdentity);
        Assert.Same(GrandStaffVerticalFit.Identity, fit);
    }

    [Fact]
    public void Create_NoInk_IsIdentity()
    {
        Assert.True(GrandStaffVerticalFit.Create([], []).IsIdentity);
    }

    [Fact]
    public void Create_InkUsingTheCanvasMarginWithRoomToSpare_IsIdentity()
    {
        // The scene's -1..1 leaves 18 px of margin; ink may run into all but its last 4 px.
        double sceneYAtClearance = 1 + ((18 - ClearancePixels) / ((CanvasHeightPixels - 36) / 2));

        var fit = GrandStaffVerticalFit.Create(Edge(sceneYAtClearance - 0.001), Edge(-(sceneYAtClearance - 0.001)));

        Assert.True(fit.IsIdentity);
    }

    [Fact]
    public void Create_TopInkPastTheCanvas_ShiftsDownUntilItIsClearOfTheEdge()
    {
        var fit = GrandStaffVerticalFit.Create(Edge(1.3), Edge(-0.85));

        Assert.Equal(1, fit.Scale);
        Assert.Equal(ClearancePixels, PixelY(fit, 1.3), precision: 9);
        Assert.True(PixelY(fit, -0.85) < CanvasHeightPixels - ClearancePixels);
    }

    [Fact]
    public void Create_BottomInkPastTheCanvas_ShiftsUpUntilItIsClearOfTheEdge()
    {
        var fit = GrandStaffVerticalFit.Create(Edge(0.85), Edge(-1.3));

        Assert.Equal(1, fit.Scale);
        Assert.Equal(CanvasHeightPixels - ClearancePixels, PixelY(fit, -1.3), precision: 9);
        Assert.True(PixelY(fit, 0.85) > ClearancePixels);
    }

    [Fact]
    public void Create_InkTooTallForAShift_ShrinksUntilBothEdgesAreClearOfTheCanvas()
    {
        var fit = GrandStaffVerticalFit.Create(Edge(1.5), Edge(-1.3));

        Assert.True(fit.Scale < 1);
        Assert.Equal(ClearancePixels, PixelY(fit, 1.5), precision: 9);
        Assert.Equal(CanvasHeightPixels - ClearancePixels, PixelY(fit, -1.3), precision: 9);
    }

    [Fact]
    public void Create_TextReachingPastItsRow_KeepsTheTextOnTheCanvas()
    {
        // A fingering's ink reaches 6 px past its row at a fixed size, however the scene is scaled.
        var fit = GrandStaffVerticalFit.Create(Edge(1.3, fixedPixels: 6), Edge(-0.85));

        Assert.Equal(1, fit.Scale);
        Assert.Equal(ClearancePixels + 6, PixelY(fit, 1.3), precision: 9);
    }

    [Fact]
    public void Create_TextAtBothEnds_ShrinksOnlyTheSceneNotTheText()
    {
        var fit = GrandStaffVerticalFit.Create(Edge(1.5, fixedPixels: 6), Edge(-1.3, fixedPixels: 12));

        Assert.True(fit.Scale < 1);
        Assert.Equal(ClearancePixels + 6, PixelY(fit, 1.5), precision: 9);
        Assert.Equal(CanvasHeightPixels - ClearancePixels - 12, PixelY(fit, -1.3), precision: 9);
    }

    [Fact]
    public void Create_OnlyTheOutermostEdgeOfEachTextSizeBinds()
    {
        var fit = GrandStaffVerticalFit.Create(
            [new GrandStaffVerticalFit.InkEdge(1.3), new GrandStaffVerticalFit.InkEdge(1.1), new GrandStaffVerticalFit.InkEdge(1.32, 6)],
            Edge(-0.85));

        Assert.Equal(ClearancePixels + 6, PixelY(fit, 1.32), precision: 9);
        Assert.True(PixelY(fit, 1.3) >= ClearancePixels);
    }
}
