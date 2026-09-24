using PianoMapper.Practice;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// Pins the ordinals of every enum that crosses the JS interop seam in
/// PianoMapper.Web/wwwroot/js/canvas.js. Canvas.js has no access to these C# types — it matches
/// scene fields by raw ordinal, duplicating the mapping by hand (see the "Scene contract" header
/// comment in canvas.js). If either side's ordinals change without the other, one of these tests
/// or the matching assertions in PianoMapper.Tests/JavaScript/scene-contract.test.mjs will fail.
/// </summary>
public sealed class GrandStaffSceneContractTests
{
    [Fact]
    public void PianoCanvasSceneKind_MatchesCanvasJsSceneKindConstants()
    {
        Assert.Equal(0, (int)PianoCanvasSceneKind.GrandStaff);
        Assert.Equal(1, (int)PianoCanvasSceneKind.PianoRoll);
    }

    [Fact]
    public void GrandStaffLineKind_MatchesCanvasJsLineKindConstants()
    {
        Assert.Equal(0, (int)GrandStaffLineKind.Staff);
        Assert.Equal(1, (int)GrandStaffLineKind.Ledger);
        Assert.Equal(2, (int)GrandStaffLineKind.Barline);
        Assert.Equal(3, (int)GrandStaffLineKind.Cursor);
        Assert.Equal(4, (int)GrandStaffLineKind.Beat);
        Assert.Equal(5, (int)GrandStaffLineKind.Glissando);
    }

    [Fact]
    public void GrandStaffGlyphKind_ClefMatchesCanvasJsClefGlyphKindConstant()
    {
        Assert.Equal(0, (int)GrandStaffGlyphKind.Clef);
    }

    [Fact]
    public void GrandStaffGlyphKind_AccidentalMatchesCanvasJsAccidentalGlyphKindConstant()
    {
        Assert.Equal(1, (int)GrandStaffGlyphKind.Accidental);
    }

    [Fact]
    public void StemDirection_UpMatchesCanvasJsStemDirectionUpConstant()
    {
        Assert.Equal(0, (int)StemDirection.Up);
    }

    [Fact]
    public void Verdict_OrdinalsMatchCanvasJsVerdictColorsArrayOrder()
    {
        Assert.Equal(0, (int)Verdict.Correct);
        Assert.Equal(1, (int)Verdict.WrongPitch);
        Assert.Equal(2, (int)Verdict.Early);
        Assert.Equal(3, (int)Verdict.Late);
        Assert.Equal(4, (int)Verdict.TooShort);
        Assert.Equal(5, (int)Verdict.TooLong);
        Assert.Equal(6, (int)Verdict.Missed);
        Assert.Equal(7, (int)Verdict.Extra);

        // A new Verdict value needs a matching entry appended to canvas.js's verdictColors array,
        // or verdictColors[note.verdict] resolves to undefined for that verdict at draw time.
        Assert.Equal(8, Enum.GetValues<Verdict>().Length);
    }
}
