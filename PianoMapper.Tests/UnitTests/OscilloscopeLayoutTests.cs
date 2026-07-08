using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class OscilloscopeLayoutTests
{
    [Fact]
    public void BuildPolyline_EmptyWindow_ReturnsEmpty()
    {
        var points = OscilloscopeLayout.BuildPolyline([]);

        Assert.Empty(points);
    }

    [Fact]
    public void BuildPolyline_SingleSample_PlacedAtLeftEdge()
    {
        short[] window = [0];

        var points = OscilloscopeLayout.BuildPolyline(window);

        var point = Assert.Single(points);
        Assert.Equal(OscilloscopeLayout.PanelX0, point.X, 3);
    }

    [Fact]
    public void BuildPolyline_MultipleSamples_SpansFromLeftToRightPanelEdge()
    {
        short[] window = [0, 0, 0, 0];

        var points = OscilloscopeLayout.BuildPolyline(window);

        Assert.Equal(OscilloscopeLayout.PanelX0, points[0].X, 3);
        Assert.Equal(OscilloscopeLayout.PanelX1, points[^1].X, 3);
    }

    [Fact]
    public void BuildPolyline_ZeroAmplitude_SitsAtPanelVerticalCenter()
    {
        short[] window = [0];

        var points = OscilloscopeLayout.BuildPolyline(window);

        var expectedCenter = (OscilloscopeLayout.PanelY0 + OscilloscopeLayout.PanelY1) / 2f;
        Assert.Equal(expectedCenter, points[0].Y, 3);
    }

    [Fact]
    public void BuildPolyline_MaxPositiveAmplitude_ReachesPanelTop()
    {
        short[] window = [short.MaxValue];

        var points = OscilloscopeLayout.BuildPolyline(window);

        Assert.Equal(OscilloscopeLayout.PanelY1, points[0].Y, 3);
    }

    [Fact]
    public void BuildPolyline_MaxNegativeAmplitude_ReachesPanelBottom()
    {
        short[] window = [short.MinValue];

        var points = OscilloscopeLayout.BuildPolyline(window);

        Assert.Equal(OscilloscopeLayout.PanelY0, points[0].Y, 3);
    }

    [Fact]
    public void BuildPolyline_MultipleSamples_MapsEachSampleToItsOwnAmplitude()
    {
        short[] window = [short.MaxValue, 0, short.MinValue];

        var points = OscilloscopeLayout.BuildPolyline(window);

        var expectedCenter = (OscilloscopeLayout.PanelY0 + OscilloscopeLayout.PanelY1) / 2f;
        Assert.Equal(OscilloscopeLayout.PanelY1, points[0].Y, 3);
        Assert.Equal(expectedCenter, points[1].Y, 3);
        Assert.Equal(OscilloscopeLayout.PanelY0, points[2].Y, 3);
    }
}
