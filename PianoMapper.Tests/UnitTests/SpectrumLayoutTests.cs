using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class SpectrumLayoutTests
{
    [Fact]
    public void BuildBars_EmptyMagnitudes_ReturnsEmpty()
    {
        var bars = SpectrumLayout.BuildBars([]);

        Assert.Empty(bars);
    }

    [Fact]
    public void BuildBars_AllZeroMagnitudes_ReturnsEmpty()
    {
        double[] magnitudes = [0, 0, 0];

        var bars = SpectrumLayout.BuildBars(magnitudes);

        Assert.Empty(bars);
    }

    [Fact]
    public void BuildBars_SingleNonZeroBin_ReachesPanelTop()
    {
        double[] magnitudes = [5.0];

        var bars = SpectrumLayout.BuildBars(magnitudes);

        var bar = Assert.Single(bars);
        Assert.Equal(SpectrumLayout.PanelY1, bar.Y1, 3);
    }

    [Fact]
    public void BuildBars_FirstBar_StartsAtPanelLeftEdge()
    {
        double[] magnitudes = [1.0, 2.0];

        var bars = SpectrumLayout.BuildBars(magnitudes);

        Assert.Equal(SpectrumLayout.PanelX0, bars[0].X0, 3);
    }

    [Fact]
    public void BuildBars_FewerBinsThanVisibleCount_UsesAllAvailableBins()
    {
        var magnitudes = Enumerable.Repeat(1.0, 5).ToArray();

        var bars = SpectrumLayout.BuildBars(magnitudes);

        Assert.Equal(5, bars.Count);
    }

    [Fact]
    public void BuildBars_MoreBinsThanVisibleCount_OnlyUsesVisibleBinCount()
    {
        var magnitudes = Enumerable.Repeat(1.0, SpectrumLayout.VisibleBinCount + 50).ToArray();

        var bars = SpectrumLayout.BuildBars(magnitudes);

        Assert.Equal(SpectrumLayout.VisibleBinCount, bars.Count);
    }

    [Fact]
    public void BuildBars_HalfMagnitudeBin_ReachesHalfwayUpPanel()
    {
        double[] magnitudes = [10.0, 5.0];

        var bars = SpectrumLayout.BuildBars(magnitudes);

        var expectedY1 = SpectrumLayout.PanelY0 + 0.5f * (SpectrumLayout.PanelY1 - SpectrumLayout.PanelY0);
        Assert.Equal(expectedY1, bars[1].Y1, 3);
    }
}
