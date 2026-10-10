using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed partial class GrandStaffSceneBuilderTests
{
    private static readonly TimeSignature LiveRowsSignature = new(4, new NoteValue(4));
    private static readonly Tempo LiveRowsTempo = new(120);

    // The keyboard's selectable octaves (BrowserKeyboardState clamps the current octave to 1-8).
    private const int LowestSelectableOctave = 1;
    private const int HighestSelectableOctave = 8;
    // canvas.js maps scene Y in [-1, 1] onto the drawable canvas area.
    private const double SceneBound = 1;
    private const double StaffSpaceTolerance = 1e-9;

    public enum LiveRowsContent
    {
        Empty,
        KeyboardRange,
        PianoEnds,
        PianoEndsWithAccidentals,
    }

    [Fact]
    public void BuildLivePair_EmptyTimeline_ShowsTheFirstTwoPagesWithTheCursorOnlyInTheUpperRow()
    {
        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            [],
            TimeSpan.Zero,
            LiveRowsSignature,
            LiveRowsTempo);

        var cursor = Assert.Single(upper.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(GrandStaffLayout.ScoreX0, cursor.X0, 6);
        Assert.DoesNotContain(lower.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(10, upper.Lines.Count(line => line.Kind == GrandStaffLineKind.Staff));
        Assert.Equal(10, lower.Lines.Count(line => line.Kind == GrandStaffLineKind.Staff));
        Assert.Equal(2, lower.Glyphs.Count(glyph => glyph.Kind == GrandStaffGlyphKind.Clef));
        Assert.Equal(
            upper.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline),
            lower.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline));
    }

    [Fact]
    public void BuildLivePair_CursorInSecondPage_KeepsTheFirstPageAboveAndPutsTheCursorInTheLowerRow()
    {
        PerformedNote firstPageNote = LiveRowsNote(NoteLetter.C, startBeat: 2, endBeat: 3);
        PerformedNote secondPageNote = LiveRowsNote(NoteLetter.D, startBeat: 22, endBeat: 23);

        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            [firstPageNote, secondPageNote],
            LiveRowsTime(22.5),
            LiveRowsSignature,
            LiveRowsTempo);

        Assert.Equal(["C4"], upper.Notes.Select(note => note.Label));
        Assert.Equal(["D4"], lower.Notes.Select(note => note.Label));
        Assert.DoesNotContain(upper.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        var cursor = Assert.Single(lower.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(22.5, LiveRowsSignature, firstVisibleMeasure: 5),
            cursor.X0,
            5);
    }

    [Fact]
    public void BuildLivePair_CursorInThirdPage_ReplacesTheUpperRowAndKeepsTheSecondPageBelow()
    {
        PerformedNote firstPageNote = LiveRowsNote(NoteLetter.C, startBeat: 2, endBeat: 3);
        PerformedNote secondPageNote = LiveRowsNote(NoteLetter.D, startBeat: 22, endBeat: 23);
        PerformedNote thirdPageNote = LiveRowsNote(NoteLetter.E, startBeat: 42, endBeat: 43);

        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            [firstPageNote, secondPageNote, thirdPageNote],
            LiveRowsTime(43.5),
            LiveRowsSignature,
            LiveRowsTempo);

        Assert.Equal(["E4"], upper.Notes.Select(note => note.Label));
        Assert.Equal(["D4"], lower.Notes.Select(note => note.Label));
        Assert.Single(upper.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.DoesNotContain(lower.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
    }

    [Fact]
    public void BuildLivePair_HeldNoteCrossingIntoTheLowerRow_ContinuesAsATiedActiveNote()
    {
        var heldNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = LiveRowsTime(18),
        };

        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            [heldNote],
            LiveRowsTime(22),
            LiveRowsSignature,
            LiveRowsTempo);

        var upperNote = Assert.Single(upper.Notes);
        var lowerNote = Assert.Single(lower.Notes);
        Assert.False(upperNote.IsActive);
        Assert.True(lowerNote.IsActive);
        var outgoingTie = Assert.Single(upper.Ties);
        Assert.Equal(GrandStaffLayout.ScoreX1, outgoingTie.X1, 6);
        var incomingTie = Assert.Single(lower.Ties);
        Assert.Equal(GrandStaffLayout.ScoreX0, incomingTie.X0, 6);
    }

    [Fact]
    public void BuildLivePair_SelectedOctave_FitsBothRowsToTheSameVerticalRange()
    {
        PerformedNote lowNoteInLowerRow = LiveRowsNote(NoteLetter.C, startBeat: 22, endBeat: 23, octave: 1);

        var (emptyUpper, _) = GrandStaffSceneBuilder.BuildLivePair(
            [],
            LiveRowsTime(22.5),
            LiveRowsSignature,
            LiveRowsTempo,
            selectedOctave: 4);
        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            [lowNoteInLowerRow],
            LiveRowsTime(22.5),
            LiveRowsSignature,
            LiveRowsTempo,
            selectedOctave: 4);

        double[] StaffLineYs(GrandStaffScene scene) =>
            scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Select(line => line.Y0).ToArray();

        Assert.Equal(StaffLineYs(upper), StaffLineYs(lower));
        Assert.NotEqual(StaffLineYs(emptyUpper), StaffLineYs(upper));
        Assert.All(lower.Notes, note => Assert.InRange(note.Y, -1, 1));
    }

    [Fact]
    public void BuildLivePair_SelectedOctave_KeepsTheSingleRowFitWhenBothRowsAreEmpty()
    {
        var (upper, _) = GrandStaffSceneBuilder.BuildLivePair([], TimeSpan.Zero, selectedOctave: 4);

        var singleRow = GrandStaffSceneBuilder.Build([], TimeSpan.Zero, selectedOctave: 4);

        Assert.Equal(
            singleRow.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Select(line => line.Y0),
            upper.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Select(line => line.Y0));
    }

    [Theory]
    [MemberData(nameof(LiveRowsOctavesAndContent))]
    public void BuildLivePair_SelectedOctave_NeverDrawsTheStavesLargerThanAScoreRow(
        int selectedOctave,
        LiveRowsContent content)
    {
        var (upper, lower) = BuildLiveRowsFor(selectedOctave, content);

        double scoreStaffSpace = GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble);
        Assert.True(GetStaffSpace(upper) <= scoreStaffSpace + StaffSpaceTolerance);
        Assert.True(GetStaffSpace(lower) <= scoreStaffSpace + StaffSpaceTolerance);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void BuildLivePair_SelectedOctaveWithRoomToSpare_DrawsTheStavesAtTheScoreStaffSpace(int selectedOctave)
    {
        var (upper, lower) = BuildLiveRowsFor(selectedOctave, LiveRowsContent.Empty);

        double scoreStaffSpace = GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble);
        Assert.Equal(scoreStaffSpace, GetStaffSpace(upper), StaffSpaceTolerance);
        Assert.Equal(scoreStaffSpace, GetStaffSpace(lower), StaffSpaceTolerance);
    }

    // Octaves 1 and 6 only just fitted at the score scale before the staves moved one diatonic step further apart.
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void BuildLivePair_SelectedOctaveTooTallForTheScoreScale_StillShrinksTheStavesToFit(int selectedOctave)
    {
        var (upper, _) = BuildLiveRowsFor(selectedOctave, LiveRowsContent.Empty);

        Assert.True(GetStaffSpace(upper) < GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble) - StaffSpaceTolerance);
    }

    [Theory]
    [MemberData(nameof(LiveRowsOctavesAndContent))]
    public void BuildLivePair_SelectedOctave_KeepsEveryNoteLedgerLineAndAccidentalInsideTheScene(
        int selectedOctave,
        LiveRowsContent content)
    {
        var (upper, lower) = BuildLiveRowsFor(selectedOctave, content);

        Assert.Equal(GetLiveRowsNotes(selectedOctave, content).Length, upper.Notes.Count + lower.Notes.Count);
        AssertNotationInsideTheScene(upper);
        AssertNotationInsideTheScene(lower);
    }

    [Theory]
    [MemberData(nameof(LiveRowsOctavesAndContent))]
    public void BuildLivePair_SelectedOctave_KeepsBothRowsTheSameSizeAndPosition(
        int selectedOctave,
        LiveRowsContent content)
    {
        var (upper, lower) = BuildLiveRowsFor(selectedOctave, content);

        Assert.Equal(GetStaffSpace(upper), GetStaffSpace(lower), StaffSpaceTolerance);
        Assert.Equal(GetStaffLineYs(upper), GetStaffLineYs(lower));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void BuildLivePair_SelectedOctaveWithRoomToSpare_CentersTheStavesInTheScene(int selectedOctave)
    {
        var (upper, _) = BuildLiveRowsFor(selectedOctave, LiveRowsContent.Empty);

        double[] staffLineYs = GetStaffLineYs(upper);
        Assert.Equal(0, (staffLineYs.Min() + staffLineYs.Max()) / 2, 6);
    }

    public static TheoryData<int, LiveRowsContent> LiveRowsOctavesAndContent()
    {
        var data = new TheoryData<int, LiveRowsContent>();
        for (int octave = LowestSelectableOctave; octave <= HighestSelectableOctave; octave++)
        {
            foreach (var content in Enum.GetValues<LiveRowsContent>())
            {
                data.Add(octave, content);
            }
        }

        return data;
    }

    private static TimeSpan LiveRowsTime(double beats) => MusicalTime.BeatsToDuration(beats, LiveRowsTempo);

    private static PerformedNote LiveRowsNote(NoteLetter letter, double startBeat, double endBeat, int octave = 4) =>
        LiveRowsNote(new Pitch(letter, 0, octave), startBeat, endBeat);

    private static PerformedNote LiveRowsNote(Pitch pitch, double startBeat, double endBeat) =>
        new()
        {
            Pitch = pitch,
            StartTime = LiveRowsTime(startBeat),
            ReleaseTime = LiveRowsTime(endBeat),
        };

    // The first note falls in the upper row's page (measures 1-5) and the second in the lower row's (measures 6-10).
    private static PerformedNote[] GetLiveRowsNotes(int selectedOctave, LiveRowsContent content) => content switch
    {
        LiveRowsContent.Empty => [],
        LiveRowsContent.KeyboardRange =>
        [
            LiveRowsNote(new Pitch(NoteLetter.C, 0, selectedOctave), startBeat: 1, endBeat: 2),
            LiveRowsNote(new Pitch(NoteLetter.C, 0, selectedOctave + 1), startBeat: 22, endBeat: 23),
        ],
        LiveRowsContent.PianoEnds =>
        [
            LiveRowsNote(new Pitch(NoteLetter.A, 0, 0), startBeat: 1, endBeat: 2),
            LiveRowsNote(new Pitch(NoteLetter.C, 0, 8), startBeat: 22, endBeat: 23),
        ],
        LiveRowsContent.PianoEndsWithAccidentals =>
        [
            LiveRowsNote(new Pitch(NoteLetter.A, 1, 0), startBeat: 1, endBeat: 2),
            LiveRowsNote(new Pitch(NoteLetter.B, -1, 7), startBeat: 22, endBeat: 23),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(content), content, message: null),
    };

    private static (GrandStaffScene Upper, GrandStaffScene Lower) BuildLiveRowsFor(
        int selectedOctave,
        LiveRowsContent content) =>
        GrandStaffSceneBuilder.BuildLivePair(
            GetLiveRowsNotes(selectedOctave, content),
            LiveRowsTime(22.5),
            LiveRowsSignature,
            LiveRowsTempo,
            selectedOctave);

    private static double[] GetStaffLineYs(GrandStaffScene scene) =>
        scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Select(line => line.Y0).ToArray();

    private static double GetStaffSpace(GrandStaffScene scene)
    {
        double[] staffLineYs = GetStaffLineYs(scene);
        return staffLineYs[1] - staffLineYs[0];
    }

    /// <summary>
    /// Every note head, stem, ledger line, accidental, label band and tie must stay inside the scene's [-1, 1] range,
    /// which canvas.js maps onto the drawable canvas area. The live scene carries no stem end, so the stem reaches the
    /// same three staff spaces the canvas draws it, toward the middle of its staff.
    /// </summary>
    private static void AssertNotationInsideTheScene(GrandStaffScene scene)
    {
        double staffSpace = GetStaffSpace(scene);
        double stemLength = GrandStaffLayout.StemLength / GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble) * staffSpace;
        double noteHeadHalfHeight = GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces * staffSpace;
        var extents = new List<(string What, double Y)>();
        foreach (var note in scene.Notes)
        {
            extents.Add(($"{note.Label} head top", note.Y + noteHeadHalfHeight));
            extents.Add(($"{note.Label} head bottom", note.Y - noteHeadHalfHeight));
            if (note.HasStem)
            {
                extents.Add((
                    $"{note.Label} stem end",
                    note.Y + (note.StemDirection == StemDirection.Up ? stemLength : -stemLength)));
            }

            if (note.LabelY is { } labelY)
            {
                extents.Add(($"{note.Label} label", labelY));
            }
        }

        foreach (var ledgerLine in scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Ledger))
        {
            extents.Add(("ledger line", ledgerLine.Y0));
        }

        foreach (var accidental in scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental))
        {
            double halfHeight = accidental.Height!.Value / 2;
            extents.Add(($"{accidental.Text} top", accidental.Y + halfHeight));
            extents.Add(($"{accidental.Text} bottom", accidental.Y - halfHeight));
        }

        foreach (var band in scene.Bands)
        {
            extents.Add(("label band top", band.Y1));
            extents.Add(("label band bottom", band.Y0));
        }

        foreach (var (what, y) in extents)
        {
            Assert.True(y is >= -SceneBound and <= SceneBound, $"{what} is at Y={y:F4}, outside the scene.");
        }
    }
}
