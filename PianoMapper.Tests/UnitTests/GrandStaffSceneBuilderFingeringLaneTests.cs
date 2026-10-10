using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// Fingering numbers render in a lane above their own staff: treble numbers above the treble staff, bass numbers above the
// bass staff (in the gap between the staves). The lane is a hard boundary for that staff's notation, and the pixel sizes
// below are the ones canvas.js draws at the smallest CSS-clamped score canvas, since canvas text does not scale with the
// scene's geometry.
public sealed partial class GrandStaffSceneBuilderTests
{
    // .score-row-canvas is clamp(15rem, 22vw, 19rem); canvas.js maps scene Y in [-1, 1] onto the canvas minus 18 px each side.
    private const double ScoreCanvasMinimumHeightPixels = 240;
    private const double CanvasMarginPixels = 18;
    // canvas.js draws a fingering at "600 15px", centred on its row, and a note name at 16px hanging below its row.
    private const double FingeringFontPixels = 15;
    private const double NoteNameFontPixels = 16;
    // Measured on a real 916 x 240 canvas: a 15 px fingering digit's ink reaches 5 px either side of its row.
    private const double FingeringInkReachPixels = 5;
    // canvas.js draws an 18 px tuplet numeral centred on its row; its digit is about 13 px tall.
    private const double TupletNumeralInkReachPixels = 6.5;
    // A slur's arc is a cubic Bezier whose control points sit slurMaximumHeightInStaffSpaces (1.6) above its end notes
    // at most, so its highest point is 0.75 of that above the chord between them, plus half its stroke (0.06).
    private const double SlurApexInStaffSpaces = (0.75 * 1.6) + 0.06;

    private static double SceneUnitsPerPixel =>
        2 / (ScoreCanvasMinimumHeightPixels - (2 * CanvasMarginPixels));

    private static double SceneTopAtMinimumCanvasHeight =>
        1 + (CanvasMarginPixels * SceneUnitsPerPixel);

    private static double FingeringHalfHeight => FingeringFontPixels / 2 * SceneUnitsPerPixel;

    private static double FingeringInkReach => FingeringInkReachPixels * SceneUnitsPerPixel;

    private static ScoreNote FingeredNote(
        Pitch pitch,
        Staff staff,
        double beatOffset = 0,
        int finger = 3,
        bool isChordContinuation = false,
        int soundingOctavesAboveNotated = 0,
        int measureIndex = 0) =>
        new(
            pitch,
            new NoteValue(4),
            measureIndex,
            beatOffset,
            staff,
            IsChordContinuation: isChordContinuation,
            Fingering: new ScoreFingering(finger),
            SoundingOctavesAboveNotated: soundingOctavesAboveNotated);

    private static double[] StaffLineYsOf(GrandStaffScene scene, Staff staff)
    {
        double[] staffLineYs = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .Select(line => line.Y0)
            .ToArray();
        return (staff == Staff.Treble ? staffLineYs.Take(5) : staffLineYs.Skip(5).Take(5)).ToArray();
    }

    /// <summary>
    /// The highest ink of one note's own notation: its head, the ledger lines between it and its staff, and a stem that
    /// goes up. A ledger line of the other staff at the same X is not this note's.
    /// </summary>
    private static double HighestNotationInkY(GrandStaffScene scene, GrandStaffNote note, Staff staff)
    {
        double[] staffLineYs = StaffLineYsOf(scene, staff);
        double staffSpace = Math.Abs(staffLineYs[1] - staffLineYs[0]);
        double highestY = Math.Max(staffLineYs.Max(), note.Y + (GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces * staffSpace));
        foreach (var ledgerLine in scene.Lines.Where(line =>
                     line.Kind == GrandStaffLineKind.Ledger && line.X0 < note.X && line.X1 > note.X))
        {
            bool isAboveStaffUpToNote = ledgerLine.Y0 > staffLineYs.Max() && ledgerLine.Y0 <= note.Y + 1e-9;
            bool isBelowStaffDownToNote = ledgerLine.Y0 < staffLineYs.Min() && ledgerLine.Y0 >= note.Y - 1e-9;
            if (isAboveStaffUpToNote || isBelowStaffDownToNote)
            {
                highestY = Math.Max(highestY, ledgerLine.Y0);
            }
        }

        if (note.HasStem)
        {
            highestY = Math.Max(
                highestY,
                note.StemEndY ?? note.Y + (note.StemDirection == StemDirection.Up ? 3 * staffSpace : -3 * staffSpace));
        }

        return highestY;
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_FingeringInsideTheStaff_SitsAboveItsOwnStaff(Staff staff)
    {
        var note = FingeredNote(new Pitch(NoteLetter.B, 0, staff == Staff.Treble ? 4 : 2), staff);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        double fingeringY = Assert.Single(scene.Notes).FingeringY!.Value;
        double staffTopY = StaffLineYsOf(scene, staff).Max();
        Assert.True(
            fingeringY - staffTopY >= FingeringHalfHeight,
            $"Fingering at {fingeringY} must clear the staff's top line ({staffTopY}) by its own half height.");
    }

    [Fact]
    public void BuildScore_BassFingering_SitsInTheGapAboveTheBassStaff()
    {
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.B, 0, 4), Staff.Treble),
            FingeredNote(new Pitch(NoteLetter.B, 0, 2), Staff.Bass),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        double trebleFingeringY = Assert.Single(scene.Notes, note => note.Fingering!.StartsWith('R')).FingeringY!.Value;
        double bassFingeringY = Assert.Single(scene.Notes, note => note.Fingering!.StartsWith('L')).FingeringY!.Value;
        Assert.True(trebleFingeringY > StaffLineYsOf(scene, Staff.Treble).Max());
        Assert.True(bassFingeringY > StaffLineYsOf(scene, Staff.Bass).Max());
        Assert.True(bassFingeringY < StaffLineYsOf(scene, Staff.Treble).Min());
    }

    [Theory]
    [InlineData(NoteLetter.A, 5, Staff.Treble)] // first ledger line above the treble staff
    [InlineData(NoteLetter.C, 6, Staff.Treble)] // second ledger line above the treble staff
    [InlineData(NoteLetter.C, 4, Staff.Bass)] // first ledger line above the bass staff
    [InlineData(NoteLetter.E, 4, Staff.Bass)] // second ledger line above the bass staff
    [InlineData(NoteLetter.G, 4, Staff.Bass)] // third ledger line above the bass staff
    public void BuildScore_FingeringOverHighNote_ClearsItsHeadLedgerLinesAndStem(
        NoteLetter letter,
        int octave,
        Staff staff)
    {
        var note = FingeredNote(new Pitch(letter, 0, octave), staff);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        double clearance = renderedNote.FingeringY!.Value - HighestNotationInkY(scene, renderedNote, staff);
        Assert.True(
            clearance >= FingeringHalfHeight,
            $"{renderedNote.Label}'s fingering must clear its own notation by half a digit height, got {clearance}.");
    }

    [Fact]
    public void BuildScore_FingeringOverHighTrebleNote_StaysInsideTheSmallestCanvas()
    {
        var note = FingeredNote(new Pitch(NoteLetter.C, 0, 6), Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        double fingeringTopY = Assert.Single(scene.Notes).FingeringY!.Value + FingeringHalfHeight;
        Assert.True(
            fingeringTopY <= SceneTopAtMinimumCanvasHeight,
            $"The fingering reaches Y={fingeringTopY}, above the canvas top at {SceneTopAtMinimumCanvasHeight}.");
    }

    [Fact]
    public void BuildScore_FingeringOverPrintedAccidental_ClearsTheAccidentalGlyph()
    {
        // F#5 sits on the top line; its sharp reaches well above the head.
        var note = FingeredNote(new Pitch(NoteLetter.F, 1, 5), Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        AssertFingeringClearsEveryGlyph(scene, GrandStaffGlyphKind.Accidental);
    }

    [Fact]
    public void BuildScore_FingeringOverUprightFermata_ClearsTheFermata()
    {
        var note = FingeredNote(new Pitch(NoteLetter.A, 0, 5), Staff.Treble) with { Fermata = ScoreFermata.Upright };

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        AssertFingeringClearsEveryGlyph(scene, GrandStaffGlyphKind.Fermata);
    }

    [Fact]
    public void BuildScore_FingeringOverStackedMarks_ClearsTheHighestMark()
    {
        var note = FingeredNote(new Pitch(NoteLetter.A, 0, 5), Staff.Treble) with
        {
            Articulation = ScoreArticulation.Staccato | ScoreArticulation.Accent,
            Ornament = ScoreOrnament.TrillMark,
            AccidentalMark = ScoreAccidental.Sharp,
        };

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        Assert.Equal(
            4,
            scene.Glyphs.Count(glyph => glyph.Kind is GrandStaffGlyphKind.Articulation
                or GrandStaffGlyphKind.Ornament or GrandStaffGlyphKind.AccidentalMark));
        AssertFingeringClearsEveryGlyph(
            scene,
            GrandStaffGlyphKind.Articulation,
            GrandStaffGlyphKind.Ornament,
            GrandStaffGlyphKind.AccidentalMark);
    }

    private static void AssertFingeringClearsEveryGlyph(GrandStaffScene scene, params GrandStaffGlyphKind[] kinds)
    {
        double fingeringY = Assert.Single(scene.Notes).FingeringY!.Value;
        GrandStaffGlyph[] glyphs = scene.Glyphs.Where(glyph => kinds.Contains(glyph.Kind)).ToArray();
        Assert.NotEmpty(glyphs);
        Assert.All(
            glyphs,
            glyph => Assert.True(
                fingeringY - (glyph.Y + (glyph.Height!.Value / 2)) >= FingeringHalfHeight,
                $"The fingering at {fingeringY} touches the {glyph.Kind} glyph '{glyph.Text}' at {glyph.Y}."));
    }

    [Fact]
    public void BuildScore_FingeringOverUpwardOctaveShift_ClearsTheGuideAndStaysInsideTheSmallestCanvas()
    {
        // Sounds C7, written C6 under an 8va: the guide runs above the staff and the fingering goes above the guide.
        var note = FingeredNote(
            new Pitch(NoteLetter.C, 0, 7),
            Staff.Treble,
            soundingOctavesAboveNotated: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(note), firstVisibleMeasure: 0);

        double guideTopY = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.OctaveShift)
            .Max(line => Math.Max(line.Y0, line.Y1));
        double fingeringY = Assert.Single(scene.Notes).FingeringY!.Value;
        Assert.True(fingeringY - guideTopY >= FingeringHalfHeight);
        Assert.True(fingeringY + FingeringHalfHeight <= SceneTopAtMinimumCanvasHeight);
    }

    [Fact]
    public void BuildScore_HighBassNoteAndLowTrebleNote_BassFingeringStaysBelowTheTrebleNotation()
    {
        // Both hands at once, each reaching into the gap between the staves: a low treble C4 and a high bass E4.
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.C, 0, 3), Staff.Bass, beatOffset: 0),
            FingeredNote(new Pitch(NoteLetter.C, 0, 4), Staff.Treble, beatOffset: 1),
            FingeredNote(new Pitch(NoteLetter.E, 0, 4), Staff.Bass, beatOffset: 1),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        var trebleNote = Assert.Single(scene.Notes, note => note.Label == "C4" && note.Fingering!.StartsWith('R'));
        var bassNote = Assert.Single(scene.Notes, note => note.Label == "E4");
        double staffSpace = Math.Abs(StaffLineYsOf(scene, Staff.Treble)[1] - StaffLineYsOf(scene, Staff.Treble)[0]);
        double trebleNotationBottomY = trebleNote.Y - (GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces * staffSpace);
        double bassFingeringY = bassNote.FingeringY!.Value;
        double bassNotationTopY = HighestNotationInkY(scene, bassNote, Staff.Bass);
        Assert.True(
            bassFingeringY - bassNotationTopY >= FingeringHalfHeight,
            $"The bass fingering at {bassFingeringY} must clear the high bass note's own head and ledger lines " +
            $"(top {bassNotationTopY}) by {FingeringHalfHeight}.");
        Assert.True(
            bassFingeringY + FingeringHalfHeight <= trebleNotationBottomY,
            "The bass fingering must not reach the low treble note's notation.");
    }

    [Fact]
    public void BuildScore_LowTrebleNoteNameAndBassFingering_DoNotOverlap()
    {
        // The treble note name hangs below C4's ledger line while the bass fingering rises above the bass staff.
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.C, 0, 4), Staff.Treble),
            FingeredNote(new Pitch(NoteLetter.B, 0, 2), Staff.Bass),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        double nameBottomY = Assert.Single(scene.Notes, note => note.Label == "C4").LabelY!.Value
            - (NoteNameFontPixels * SceneUnitsPerPixel);
        double bassFingeringTopY = Assert.Single(scene.Notes, note => note.Label == "B2").FingeringY!.Value
            + FingeringHalfHeight;
        Assert.True(
            nameBottomY >= bassFingeringTopY,
            $"The treble note name ends at {nameBottomY}, inside the bass fingering that starts at {bassFingeringTopY}.");
    }

    [Fact]
    public void BuildScore_LowTrebleNoteNameAndHighBassFingering_DoNotOverlap()
    {
        // Hands together at the closest the staves get: C4 under the treble staff, its name hanging below its ledger
        // line, over E4 above the bass staff with the bass fingering lane lifted above that note's two ledger lines.
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.C, 0, 3), Staff.Bass, beatOffset: 0),
            FingeredNote(new Pitch(NoteLetter.C, 0, 4), Staff.Treble, beatOffset: 1),
            FingeredNote(new Pitch(NoteLetter.E, 0, 4), Staff.Bass, beatOffset: 1),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        double nameBottomY = Assert.Single(scene.Notes, note => note.Label == "C4" && note.Fingering!.StartsWith('R'))
            .LabelY!.Value - (NoteNameFontPixels * SceneUnitsPerPixel);
        double bassFingeringTopY = Assert.Single(scene.Notes, note => note.Label == "E4").FingeringY!.Value
            + FingeringHalfHeight;
        // The 16 px and 15 px text boxes include about 2 px of leading each, which the digits and letters do not use.
        double leadingAllowance = 2 * 2 * SceneUnitsPerPixel;
        Assert.True(
            nameBottomY + leadingAllowance >= bassFingeringTopY,
            $"The treble note name ends at {nameBottomY}, inside the bass fingering that starts at {bassFingeringTopY}.");
    }

    [Theory]
    [InlineData(Staff.Treble, 2, "R15")]
    [InlineData(Staff.Treble, 3, "R135")]
    [InlineData(Staff.Bass, 2, "L51")]
    [InlineData(Staff.Bass, 3, "L531")]
    public void BuildScore_FingeringsOfOneChord_ShareOneLabelInOneRowOnTheHighestNote(
        Staff staff,
        int noteCount,
        string expectedLabel)
    {
        // The expected label lists the fingers lowest note first: the right hand thumb-up (1 lowest), the left hand
        // pinky-up from the low end (5 lowest).
        int octave = staff == Staff.Treble ? 4 : 3;
        NoteLetter[] letters = [NoteLetter.C, NoteLetter.E, NoteLetter.G];
        int[] fingers = expectedLabel.Where(char.IsDigit).Select(digit => digit - '0').ToArray();
        ScoreNote[] notes = Enumerable.Range(0, noteCount)
            .Select(index => FingeredNote(
                new Pitch(letters[index], 0, octave),
                staff,
                finger: fingers[index],
                isChordContinuation: index > 0))
            .ToArray();

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        GrandStaffNote labelled = Assert.Single(scene.Notes, note => note.Fingering is not null);
        Assert.Equal(expectedLabel, labelled.Fingering);
        Assert.Equal(scene.Notes.Max(note => note.Y), labelled.Y);
        Assert.Equal(noteCount - 1, scene.Notes.Count(note => note.Fingering is null && note.FingeringY is null));
        Assert.True(labelled.FingeringY > StaffLineYsOf(scene, staff).Max());
    }

    [Fact]
    public void BuildScore_FingeringsOfAChordPlayedByBothHands_NameTheHandWhereItChanges()
    {
        // Left-hand notes this high are drawn on the treble staff (no bass-register note anchors them to the bass staff),
        // so the left hand's C4 and the right hand's G4 sounding together are one chord with two hands.
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.C, 0, 4), Staff.Bass, beatOffset: 0, finger: 4),
            FingeredNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble, beatOffset: 0, finger: 3, isChordContinuation: true),
            FingeredNote(new Pitch(NoteLetter.D, 0, 4), Staff.Bass, beatOffset: 1, finger: 3),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        Assert.Equal("L4R3", Assert.Single(scene.Notes, note => note.Label == "G4").Fingering);
        Assert.Null(Assert.Single(scene.Notes, note => note.Label == "C4").Fingering);
        Assert.Equal("L3", Assert.Single(scene.Notes, note => note.Label == "D4").Fingering);
    }

    [Fact]
    public void BuildScore_ShowingFingerings_DoesNotMoveNoteNamesOrBands()
    {
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.C, 0, 4), Staff.Treble, isChordContinuation: false),
            FingeredNote(new Pitch(NoteLetter.E, 0, 4), Staff.Treble, isChordContinuation: true),
            FingeredNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble, isChordContinuation: true),
            FingeredNote(new Pitch(NoteLetter.C, 0, 3), Staff.Bass),
        ];
        var score = ScoreWithNotes(notes);

        var withFingerings = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: true);
        var withoutFingerings = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false);

        Assert.Equal(
            withoutFingerings.Notes.Select(note => (note.LabelY, note.LabelFontScale)),
            withFingerings.Notes.Select(note => (note.LabelY, note.LabelFontScale)));
        Assert.Equal(withoutFingerings.Bands, withFingerings.Bands);
    }

    [Fact]
    public void BuildScore_FingeringOverUpwardSlur_ClearsTheArcsHighestPoint()
    {
        // Notes above the middle line have stems down, so their slur arcs up over the heads, higher than the highest head.
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.F, 0, 5), Staff.Treble, beatOffset: 0) with { Slur = new ScoreSlur(true, 1) },
            FingeredNote(new Pitch(NoteLetter.E, 0, 5), Staff.Treble, beatOffset: 1),
            FingeredNote(new Pitch(NoteLetter.F, 0, 5), Staff.Treble, beatOffset: 2) with { Slur = new ScoreSlur(false, 1) },
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        var slur = Assert.Single(scene.Slurs);
        double apexY = Math.Max(slur.Y0, slur.Y1) + (SlurApexInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble));
        double fingeringY = scene.Notes[0].FingeringY!.Value;
        Assert.True(
            fingeringY - FingeringInkReach >= apexY,
            $"The fingering digits start at {fingeringY - FingeringInkReach}, inside the slur arc that reaches {apexY}.");
    }

    [Fact]
    public void BuildScore_FingeringOverBeamedTripletNumeral_ClearsTheNumeral()
    {
        // A flat beam of A4s, whose stems go up past the staff's top line, with its "3" above the beam: the fingering lane
        // starts above that numeral.
        var tripletValue = new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2);
        ScoreNote[] notes =
        [
            FingeredNote(new Pitch(NoteLetter.A, 0, 4), Staff.Treble, beatOffset: 0) with { NoteValue = tripletValue, BeamState = BeamState.Begin },
            FingeredNote(new Pitch(NoteLetter.A, 0, 4), Staff.Treble, beatOffset: 1.0 / 3) with { NoteValue = tripletValue, BeamState = BeamState.Continue },
            FingeredNote(new Pitch(NoteLetter.A, 0, 4), Staff.Treble, beatOffset: 2.0 / 3) with { NoteValue = tripletValue, BeamState = BeamState.End },
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        var numeral = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Tuplet);
        double numeralTopY = numeral.Y + (TupletNumeralInkReachPixels * SceneUnitsPerPixel);
        double fingeringY = scene.Notes[1].FingeringY!.Value;
        Assert.True(
            fingeringY - FingeringInkReach >= numeralTopY,
            $"The fingering digits start at {fingeringY - FingeringInkReach}, inside the tuplet numeral that reaches {numeralTopY}.");
    }

    private static Score ScoreWithMeasures(params ScoreNote[][] measures) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            measures.Select(notes => new ScoreMeasure(notes, [])).ToArray());

    [Fact]
    public void BuildScore_TallNotationInOneMeasure_DoesNotLiftTheFingeringLaneOfTheOthers()
    {
        // A C6 under two ledger lines needs a lane high above the staff, in its own measure only: the next measure's
        // fingering stays just above its own notation instead of floating where the tall note needs it.
        ScoreNote tall = FingeredNote(new Pitch(NoteLetter.C, 0, 6), Staff.Treble);
        ScoreNote plain = FingeredNote(new Pitch(NoteLetter.B, 0, 4), Staff.Treble, measureIndex: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithMeasures([tall], [plain]), firstVisibleMeasure: 0);
        var alone = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(plain with { MeasureIndex = 0 }), firstVisibleMeasure: 0);

        double tallLaneY = Assert.Single(scene.Notes, note => note.Label == "C6").FingeringY!.Value;
        double plainLaneY = Assert.Single(scene.Notes, note => note.Label == "B4").FingeringY!.Value;
        Assert.Equal(Assert.Single(alone.Notes).FingeringY!.Value, plainLaneY, precision: 9);
        Assert.True(tallLaneY > plainLaneY);
    }

    [Fact]
    public void BuildScore_SlurOverAMeasureBoundary_LiftsTheFingeringLaneOfBothMeasures()
    {
        // The arc runs from the last beat of one measure into the next, so it rises over both of them.
        ScoreNote start = FingeredNote(new Pitch(NoteLetter.F, 0, 5), Staff.Treble, beatOffset: 3) with { Slur = new ScoreSlur(true, 1) };
        ScoreNote stop = FingeredNote(new Pitch(NoteLetter.F, 0, 5), Staff.Treble, measureIndex: 1) with { Slur = new ScoreSlur(false, 1) };

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithMeasures([start], [stop]), firstVisibleMeasure: 0);

        var slur = Assert.Single(scene.Slurs);
        double apexY = Math.Max(slur.Y0, slur.Y1) + (SlurApexInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble));
        Assert.All(
            scene.Notes,
            note => Assert.True(
                note.FingeringY!.Value - FingeringInkReach >= apexY,
                $"The fingering of the note at X={note.X} starts inside the slur arc."));
    }

    [Fact]
    public void BuildScore_OctaveShiftOverTwoMeasures_LiftsTheFingeringLaneOfBothButNotTheNext()
    {
        ScoreNote[] shiftedFirst = [FingeredNote(new Pitch(NoteLetter.C, 0, 7), Staff.Treble, soundingOctavesAboveNotated: 1)];
        ScoreNote[] shiftedSecond = [FingeredNote(new Pitch(NoteLetter.D, 0, 7), Staff.Treble, soundingOctavesAboveNotated: 1, measureIndex: 1)];
        ScoreNote[] plain = [FingeredNote(new Pitch(NoteLetter.B, 0, 4), Staff.Treble, measureIndex: 2)];

        var scene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithMeasures(shiftedFirst, shiftedSecond, plain),
            firstVisibleMeasure: 0);

        double guideTopY = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.OctaveShift)
            .Max(line => Math.Max(line.Y0, line.Y1));
        double[] laneYs = scene.Notes.OrderBy(note => note.X).Select(note => note.FingeringY!.Value).ToArray();
        Assert.True(laneYs[0] - FingeringInkReach >= guideTopY);
        Assert.True(laneYs[1] - FingeringInkReach >= guideTopY);
        Assert.True(laneYs[2] < guideTopY);
    }
}
