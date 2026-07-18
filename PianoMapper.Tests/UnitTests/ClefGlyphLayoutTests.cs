using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class ClefGlyphLayoutTests
{
    [Fact]
    public void BuildQuads_SinglePixelAtAnchorRow_ReturnsOneQuadCenteredOnAnchor()
    {
        var glyph = new StaffGlyph(Width: 1, Height: 1, AnchorRow: 0, Pixels: [true]);

        var quads = ClefGlyphLayout.BuildQuads(glyph, anchorX: 0f, anchorY: 0f, cellWidth: 0.1f, cellHeight: 0.2f);

        var quad = Assert.Single(quads);
        Assert.Equal(0f, quad.X0, 5);
        Assert.Equal(0.1f, quad.X1, 5);
        Assert.Equal(-0.1f, quad.Y0, 5);
        Assert.Equal(0.1f, quad.Y1, 5);
    }

    [Fact]
    public void BuildQuads_UnsetPixel_ProducesNoQuad()
    {
        var glyph = new StaffGlyph(Width: 1, Height: 1, AnchorRow: 0, Pixels: [false]);

        var quads = ClefGlyphLayout.BuildQuads(glyph, anchorX: 0f, anchorY: 0f, cellWidth: 0.1f, cellHeight: 0.2f);

        Assert.Empty(quads);
    }

    [Fact]
    public void BuildQuads_MultiRowColumnGrid_PlacesEachLitPixelAtItsColumnAndRow()
    {
        // 2x2 grid, row-major flat pixels: [row0col0, row0col1, row1col0, row1col1].
        // Only row0/col1 and row1/col0 are lit.
        var glyph = new StaffGlyph(
            Width: 2,
            Height: 2,
            AnchorRow: 0,
            Pixels: [false, true, true, false]);

        var quads = ClefGlyphLayout.BuildQuads(glyph, anchorX: 0f, anchorY: 0f, cellWidth: 0.1f, cellHeight: 0.2f);

        Assert.Equal(2, quads.Count);

        var rowZeroColOneQuad = quads[0];
        Assert.Equal(0.1f, rowZeroColOneQuad.X0, 5);
        Assert.Equal(0.2f, rowZeroColOneQuad.X1, 5);
        Assert.Equal(-0.1f, rowZeroColOneQuad.Y0, 5);
        Assert.Equal(0.1f, rowZeroColOneQuad.Y1, 5);

        var rowOneColZeroQuad = quads[1];
        Assert.Equal(0f, rowOneColZeroQuad.X0, 5);
        Assert.Equal(0.1f, rowOneColZeroQuad.X1, 5);
        Assert.Equal(-0.3f, rowOneColZeroQuad.Y0, 5);
        Assert.Equal(-0.1f, rowOneColZeroQuad.Y1, 5);
    }

    [Theory]
    [InlineData(0, 0.4f)]
    [InlineData(2, 0f)]
    [InlineData(4, -0.4f)]
    public void BuildQuads_RowRelativeToAnchorRow_OffsetsYByRowDistanceTimesCellHeight(int row, float expectedYCenter)
    {
        const int width = 1;
        const int height = 5;
        const int anchorRow = 2;
        var pixels = new bool[width * height];
        pixels[row] = true;
        var glyph = new StaffGlyph(width, height, anchorRow, pixels);

        var quads = ClefGlyphLayout.BuildQuads(glyph, anchorX: 0f, anchorY: 0f, cellWidth: 0.1f, cellHeight: 0.2f);

        var quad = Assert.Single(quads);
        float actualYCenter = (quad.Y0 + quad.Y1) / 2f;
        Assert.Equal(expectedYCenter, actualYCenter, 5);
    }

    [Fact]
    public void BuildQuads_AnchorOffset_ShiftsAllQuadsByAnchorXAndAnchorY()
    {
        var glyph = new StaffGlyph(Width: 1, Height: 1, AnchorRow: 0, Pixels: [true]);

        var quads = ClefGlyphLayout.BuildQuads(glyph, anchorX: 0.5f, anchorY: 0.3f, cellWidth: 0.1f, cellHeight: 0.2f);

        var quad = Assert.Single(quads);
        Assert.Equal(0.5f, quad.X0, 5);
        Assert.Equal(0.6f, quad.X1, 5);
        Assert.Equal(0.2f, quad.Y0, 5);
        Assert.Equal(0.4f, quad.Y1, 5);
    }
}
