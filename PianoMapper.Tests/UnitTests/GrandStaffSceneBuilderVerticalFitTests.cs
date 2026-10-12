using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// A score window whose notation reaches far above or below the grand staff (a C7 is five ledger lines over the treble
// staff) has to stay on its canvas. The window's whole system is shifted, and only if that is not enough shrunk, just
// far enough to fit; a window that already fits is left exactly where the layout puts it. The pixel checks use
// canvas.js's mapping of scene Y onto a canvas and the two heights .score-row-canvas takes on a desktop layout.
public sealed partial class GrandStaffSceneBuilderTests
{
    // clamp(15rem, 22vw, 19rem): the tallest score canvas, where one scene unit is the most pixels and so the 18 px margin
    // reaches the least far past +-1 (ScoreCanvasMinimumHeightPixels, declared with the fingering tests, is the other end).
    private const double ScoreCanvasMaximumHeightPixels = 304;
    // Measured on a real canvas: a 16 px note name's ink hangs 12 px below its row.
    private const double NoteNameInkReachPixels = 12;
    private const int FirstWindowMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount;

    // canvas.js mapY: scene Y -1..1 onto the canvas less CanvasMarginPixels at each end, Y growing upward.
    internal static double CanvasPixelY(double sceneY, double canvasHeightPixels) =>
        canvasHeightPixels - CanvasMarginPixels - ((sceneY + 1) / 2 * (canvasHeightPixels - (2 * CanvasMarginPixels)));

    private static double PixelsToSceneUnits(double pixels, double canvasHeightPixels) =>
        pixels * 2 / (canvasHeightPixels - (2 * CanvasMarginPixels));

    private static double StaffSpaceOf(GrandStaffScene scene)
    {
        double[] staffLineYs = StaffLineYsOf(scene, Staff.Treble);
        return Math.Abs(staffLineYs[1] - staffLineYs[0]);
    }

    /// <summary>The ink a window draws highest, in pixels from the canvas top (negative: off the canvas).</summary>
    private static double TopmostInkPixelY(GrandStaffScene scene, double canvasHeightPixels)
    {
        double headHalfHeight = GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces * StaffSpaceOf(scene);
        var inkPixelYs = new List<double>();
        foreach (var note in scene.Notes)
        {
            inkPixelYs.Add(CanvasPixelY(note.Y + headHalfHeight, canvasHeightPixels));
            if (note.HasStem && note.StemEndY is { } stemEndY)
            {
                inkPixelYs.Add(CanvasPixelY(stemEndY, canvasHeightPixels));
            }

            if (note.FingeringY is { } fingeringY)
            {
                inkPixelYs.Add(CanvasPixelY(fingeringY, canvasHeightPixels) - FingeringInkReachPixels);
            }
        }

        inkPixelYs.AddRange(scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Ledger)
            .Select(line => CanvasPixelY(Math.Max(line.Y0, line.Y1), canvasHeightPixels)));
        return inkPixelYs.Min();
    }

    /// <summary>The ink a window draws lowest, in pixels from the canvas top (beyond the canvas height: off the canvas).</summary>
    private static double BottommostInkPixelY(GrandStaffScene scene, double canvasHeightPixels)
    {
        double headHalfHeight = GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces * StaffSpaceOf(scene);
        var inkPixelYs = new List<double>();
        foreach (var note in scene.Notes)
        {
            inkPixelYs.Add(CanvasPixelY(note.Y - headHalfHeight, canvasHeightPixels));
            if (note.HasStem && note.StemEndY is { } stemEndY)
            {
                inkPixelYs.Add(CanvasPixelY(stemEndY, canvasHeightPixels));
            }

            if (note.LabelY is { } labelY)
            {
                inkPixelYs.Add(CanvasPixelY(labelY, canvasHeightPixels) + NoteNameInkReachPixels);
            }
        }

        inkPixelYs.AddRange(scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Ledger)
            .Select(line => CanvasPixelY(Math.Min(line.Y0, line.Y1), canvasHeightPixels)));
        return inkPixelYs.Max();
    }

    private static double GapBetweenStaves(GrandStaffScene scene) =>
        StaffLineYsOf(scene, Staff.Treble).Min() - StaffLineYsOf(scene, Staff.Bass).Max();

    private static void AssertStaffLines(GrandStaffScene scene, Staff staff)
    {
        double[] expected = (staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs)
            .Select(y => GrandStaffLayout.SeparateStaffY(y, staff))
            .ToArray();
        double[] actual = StaffLineYsOf(scene, staff);
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index], precision: 9);
        }
    }

    // One 4/4 measure: a treble line that keeps returning to C7 and a plain bass line under it.
    private static ScoreNote[] HighTrebleOverBassNotes(bool withFingering = false) =>
    [
        HighWindowNote(NoteLetter.C, 7, Staff.Treble, 0, withFingering),
        HighWindowNote(NoteLetter.B, 6, Staff.Treble, 1, withFingering),
        HighWindowNote(NoteLetter.C, 7, Staff.Treble, 2, withFingering),
        HighWindowNote(NoteLetter.G, 6, Staff.Treble, 3, withFingering),
        HighWindowNote(NoteLetter.C, 3, Staff.Bass, 0, withFingering),
        HighWindowNote(NoteLetter.G, 3, Staff.Bass, 1, withFingering),
        HighWindowNote(NoteLetter.E, 3, Staff.Bass, 2, withFingering),
        HighWindowNote(NoteLetter.G, 3, Staff.Bass, 3, withFingering),
    ];

    private static ScoreNote HighWindowNote(NoteLetter letter, int octave, Staff staff, double beatOffset, bool withFingering) =>
        new(
            new Pitch(letter, 0, octave),
            new NoteValue(4),
            MeasureIndex: 0,
            beatOffset,
            staff,
            Fingering: withFingering ? new ScoreFingering(3) : null);

    [Theory]
    [InlineData(ScoreCanvasMinimumHeightPixels)]
    [InlineData(ScoreCanvasMaximumHeightPixels)]
    public void BuildScore_NotesFiveLedgerLinesAboveTheStaff_StayOnTheCanvas(double canvasHeightPixels)
    {
        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(HighTrebleOverBassNotes()), firstVisibleMeasure: 0);

        Assert.True(
            TopmostInkPixelY(scene, canvasHeightPixels) >= 0,
            $"The highest ink is {TopmostInkPixelY(scene, canvasHeightPixels):F1} px from the top of a {canvasHeightPixels} px canvas.");
        Assert.True(
            BottommostInkPixelY(scene, canvasHeightPixels) <= canvasHeightPixels,
            $"The lowest ink is {BottommostInkPixelY(scene, canvasHeightPixels):F1} px down a {canvasHeightPixels} px canvas.");
    }

    [Theory]
    [InlineData(ScoreCanvasMinimumHeightPixels)]
    [InlineData(ScoreCanvasMaximumHeightPixels)]
    public void BuildScore_FingeringOverNotesFiveLedgerLinesAboveTheStaff_StaysOnTheCanvas(double canvasHeightPixels)
    {
        var scene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes(HighTrebleOverBassNotes(withFingering: true)),
            firstVisibleMeasure: 0);

        Assert.True(
            TopmostInkPixelY(scene, canvasHeightPixels) >= 0,
            $"The highest ink is {TopmostInkPixelY(scene, canvasHeightPixels):F1} px from the top of a {canvasHeightPixels} px canvas.");
        Assert.True(BottommostInkPixelY(scene, canvasHeightPixels) <= canvasHeightPixels);
    }

    [Theory]
    [InlineData(ScoreCanvasMinimumHeightPixels)]
    [InlineData(ScoreCanvasMaximumHeightPixels)]
    public void BuildScore_NotesAndNoteNamesFarBelowTheStaff_StayOnTheCanvas(double canvasHeightPixels)
    {
        // A0 is thirteen diatonic steps under the bass staff, and its name hangs under the notation.
        ScoreNote[] notes =
        [
            HighWindowNote(NoteLetter.A, 0, Staff.Bass, 0, withFingering: false),
            HighWindowNote(NoteLetter.C, 2, Staff.Bass, 1, withFingering: false),
            HighWindowNote(NoteLetter.E, 4, Staff.Treble, 0, withFingering: false),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        Assert.True(
            BottommostInkPixelY(scene, canvasHeightPixels) <= canvasHeightPixels,
            $"The lowest ink is {BottommostInkPixelY(scene, canvasHeightPixels):F1} px down a {canvasHeightPixels} px canvas.");
        Assert.True(TopmostInkPixelY(scene, canvasHeightPixels) >= 0);
    }

    [Fact]
    public void BuildScore_NotationThatFits_StaysWhereTheLayoutPutsIt()
    {
        ScoreNote[] notes =
        [
            HighWindowNote(NoteLetter.C, 5, Staff.Treble, 0, withFingering: true),
            HighWindowNote(NoteLetter.G, 4, Staff.Treble, 1, withFingering: true),
            HighWindowNote(NoteLetter.C, 3, Staff.Bass, 0, withFingering: true),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        AssertStaffLines(scene, Staff.Treble);
        AssertStaffLines(scene, Staff.Bass);
        Assert.Equal(
            GrandStaffLayout.SeparateStaffY(GrandStaffLayout.GetLivePosition(new Pitch(NoteLetter.C, 0, 5)).Y, Staff.Treble),
            scene.Notes.Single(note => note.Label == "C5").Y,
            precision: 9);
    }

    [Fact]
    public void BuildScore_OnlyOneWindowReachesFarAboveTheStaff_LeavesTheOtherWindowAlone()
    {
        // Two score rows are two canvases: the window with a C7 moves, the window beside it is not touched.
        var score = new Score(
            "rows",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            Enumerable.Range(0, 2 * FirstWindowMeasureCount)
                .Select(measureIndex => new ScoreMeasure(
                    [HighWindowNote(
                        measureIndex == FirstWindowMeasureCount + 1 ? NoteLetter.C : NoteLetter.E,
                        measureIndex == FirstWindowMeasureCount + 1 ? 7 : 5,
                        Staff.Treble,
                        0,
                        withFingering: false) with { MeasureIndex = measureIndex }],
                    []))
                .ToArray());

        var plainWindow = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var tallWindow = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: FirstWindowMeasureCount);

        double trebleBottomLineY = GrandStaffLayout.SeparateStaffY(GrandStaffLayout.TrebleLineYs[0], Staff.Treble);
        Assert.Equal(trebleBottomLineY, StaffLineYsOf(plainWindow, Staff.Treble).Min(), precision: 9);
        Assert.True(StaffLineYsOf(tallWindow, Staff.Treble).Min() < trebleBottomLineY);
    }

    [Fact]
    public void BuildScore_TopHeavyWindowWithRoomBelow_ShiftsTheSystemDownWithoutShrinkingIt()
    {
        var plainScene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([HighWindowNote(NoteLetter.C, 4, Staff.Treble, 0, withFingering: false)]),
            firstVisibleMeasure: 0);
        var tallScene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([HighWindowNote(NoteLetter.C, 7, Staff.Treble, 0, withFingering: false)]),
            firstVisibleMeasure: 0);

        Assert.Equal(StaffSpaceOf(plainScene), StaffSpaceOf(tallScene), precision: 9);
        Assert.True(StaffLineYsOf(tallScene, Staff.Treble).Max() < StaffLineYsOf(plainScene, Staff.Treble).Max());
    }

    [Fact]
    public void BuildScore_WindowTooTallForAShiftAlone_ShrinksTheWholeSystemUniformly()
    {
        // A C7 with its fingering over an A0 with its name: no shift gets both on the canvas, so the system is scaled.
        ScoreNote[] notes =
        [
            HighWindowNote(NoteLetter.C, 7, Staff.Treble, 0, withFingering: true),
            HighWindowNote(NoteLetter.A, 0, Staff.Bass, 0, withFingering: true),
        ];

        var plainScene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([HighWindowNote(NoteLetter.C, 4, Staff.Treble, 0, withFingering: false)]),
            firstVisibleMeasure: 0);
        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        double scale = StaffSpaceOf(scene) / StaffSpaceOf(plainScene);
        Assert.True(scale < 1, $"The system should be shrunk, but its staff space scale is {scale}.");
        Assert.Equal(GapBetweenStaves(plainScene) * scale, GapBetweenStaves(scene), precision: 9);
        double[] bassLineYs = StaffLineYsOf(scene, Staff.Bass);
        double[] plainBassLineYs = StaffLineYsOf(plainScene, Staff.Bass);
        Assert.Equal((plainBassLineYs.Max() - plainBassLineYs.Min()) * scale, bassLineYs.Max() - bassLineYs.Min(), precision: 9);
        foreach (double canvasHeightPixels in new[] { ScoreCanvasMinimumHeightPixels, ScoreCanvasMaximumHeightPixels })
        {
            Assert.True(TopmostInkPixelY(scene, canvasHeightPixels) >= 0);
            Assert.True(BottommostInkPixelY(scene, canvasHeightPixels) <= canvasHeightPixels);
        }
    }

    [Fact]
    public void BuildScoreOverlay_HeldNoteInAFittedWindow_SitsOnTheSameYAsItsPrintedNote()
    {
        var score = ScoreWithNotes(HighTrebleOverBassNotes());
        var parts = GrandStaffSceneBuilder.BuildStaticScoreParts(score, firstVisibleMeasure: 0);
        var heldC7 = new PerformedNote { Pitch = new Pitch(NoteLetter.C, 0, 7), StartTime = TimeSpan.Zero };

        var overlay = GrandStaffSceneBuilder.BuildScoreOverlay(
            parts,
            score,
            firstVisibleMeasure: 0,
            cursorBeats: 0,
            performedNotes: [heldC7]);

        var printed = parts.Notes.First(note => note.Label == "C7");
        var held = Assert.Single(overlay.Notes);
        Assert.Equal(printed.Y, held.Y, precision: 9);
        Assert.Equal(printed.LabelY!.Value, held.LabelY!.Value, precision: 9);
        double[] printedLedgerYs = parts.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Ledger)
            .Select(line => line.Y0)
            .ToArray();
        Assert.Equal(5, overlay.LedgerLines.Count);
        Assert.All(
            overlay.LedgerLines,
            line => Assert.Contains(printedLedgerYs, printedY => Math.Abs(printedY - line.Y0) < 1e-9));
    }

    [Fact]
    public void BuildScoreOverlay_CursorInAFittedWindow_RunsBetweenTheFittedStaves()
    {
        var score = ScoreWithNotes(HighTrebleOverBassNotes());
        var parts = GrandStaffSceneBuilder.BuildStaticScoreParts(score, firstVisibleMeasure: 0);

        var overlay = GrandStaffSceneBuilder.BuildScoreOverlay(parts, score, firstVisibleMeasure: 0, cursorBeats: 1);

        var scene = GrandStaffSceneBuilder.CreateStaticScoreScene(parts);
        var cursor = Assert.IsType<GrandStaffLine>(overlay.Cursor);
        Assert.Equal(StaffLineYsOf(scene, Staff.Bass).Min(), cursor.Y0, precision: 9);
        Assert.Equal(StaffLineYsOf(scene, Staff.Treble).Max(), cursor.Y1, precision: 9);
    }

    [Fact]
    public void GetCursorLineYBounds_FittedWindow_RunsBetweenTheFittedStaves()
    {
        // ScoreCursorPlaybackState hands the browser these bounds to animate the cursor on its own.
        var parts = GrandStaffSceneBuilder.BuildStaticScoreParts(
            ScoreWithNotes(HighTrebleOverBassNotes()),
            firstVisibleMeasure: 0);

        var (y0, y1) = GrandStaffSceneBuilder.GetCursorLineYBounds(parts);

        var scene = GrandStaffSceneBuilder.CreateStaticScoreScene(parts);
        Assert.Equal(StaffLineYsOf(scene, Staff.Bass).Min(), y0, precision: 9);
        Assert.Equal(StaffLineYsOf(scene, Staff.Treble).Max(), y1, precision: 9);
    }

    [Fact]
    public void GetCursorLineYBounds_WindowThatFits_IsTheLayoutsOwnSpan()
    {
        var parts = GrandStaffSceneBuilder.BuildStaticScoreParts(
            ScoreWithNotes([HighWindowNote(NoteLetter.C, 4, Staff.Treble, 0, withFingering: false)]),
            firstVisibleMeasure: 0);

        Assert.Equal(GrandStaffSceneBuilder.GetCursorLineYBounds(), GrandStaffSceneBuilder.GetCursorLineYBounds(parts));
    }
}
