using PianoMapper.Web.Rendering;
using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed partial class GrandStaffSceneBuilderTests
{
    [Fact]
    public void BuildScore_ChordEarlyInMeasureFollowedByMoreNotes_DoesNotCrowdUnrelatedNotes()
    {
        // Regression test for a real imported score: a single dense (chord) onset must not force
        // the rescale-to-fit step to crush every *unrelated* eighth note elsewhere in a busy
        // measure — the extra spacing budget for chords/accidentals is capped precisely so a busy
        // measure degrades toward plain proportional spacing instead of a collapsed mess.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 0.5, Staff.Treble),
            new(new Pitch(NoteLetter.G, 1, 4), new NoteValue(8), 0, 1, Staff.Treble),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8), 0, 1.5, Staff.Treble),
            new(new Pitch(NoteLetter.G, 1, 4), new NoteValue(8), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 2.5, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 3), new NoteValue(4), 0, 0, Staff.Bass),
            new(new Pitch(NoteLetter.A, 0, 3), new NoteValue(2), 0, 1, Staff.Bass),
            new(new Pitch(NoteLetter.B, 0, 3), new NoteValue(2), 0, 1, Staff.Bass, IsChordContinuation: true),
        ];
        var timeSignature = new TimeSignature(3, new NoteValue(4));
        var score = ScoreWithNotes(notes, timeSignature: timeSignature);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        // Beat 0 -> 0.5 and beat 2 -> 2.5 are both plain, undisturbed eighth-note intervals of
        // the same natural width, with no dense onset touching either end. A uniform final
        // rescale-to-fit should treat them equally; a crowding regression would instead crush the
        // later one disproportionately while leaving the first (before the chord) unaffected.
        double firstGap = scene.Notes.Single(note => note.ScoreOnsetBeats == 0.5).X -
            scene.Notes.Single(note => note.ScoreOnsetBeats == 0 && note.Label == "D4").X;
        double laterGap = scene.Notes.Single(note => note.ScoreOnsetBeats == 2.5).X -
            scene.Notes.Single(note => note.ScoreOnsetBeats == 2).X;
        Assert.Equal(firstGap, laterGap, 3);
    }

    [Fact]
    public void BuildScore_ChordEarlyInMeasureFollowedByMoreNotes_KeepsLaterOnsetsDistinctAndOrdered()
    {
        // Regression test for a real imported score: a bass 2nd-interval chord at beat 1,
        // followed by several more treble eighth notes through the rest of the measure. The
        // spacing fix that widens the gap around the chord must not "stick" and collapse every
        // later onset in the measure onto the same X.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 0.5, Staff.Treble),
            new(new Pitch(NoteLetter.G, 1, 4), new NoteValue(8), 0, 1, Staff.Treble),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8), 0, 1.5, Staff.Treble),
            new(new Pitch(NoteLetter.G, 1, 4), new NoteValue(8), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 2.5, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 3), new NoteValue(4), 0, 0, Staff.Bass),
            new(new Pitch(NoteLetter.A, 0, 3), new NoteValue(2), 0, 1, Staff.Bass),
            new(new Pitch(NoteLetter.B, 0, 3), new NoteValue(2), 0, 1, Staff.Bass, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        double[] trebleXsInBeatOrder = scene.Notes
            .Where(note => note.Address?.NoteIndex < 6)
            .OrderBy(note => note.ScoreOnsetBeats)
            .Select(note => note.X)
            .ToArray();
        Assert.Equal(6, trebleXsInBeatOrder.Length);
        for (int index = 1; index < trebleXsInBeatOrder.Length; index++)
        {
            Assert.True(
                trebleXsInBeatOrder[index] > trebleXsInBeatOrder[index - 1],
                $"Expected strictly increasing X by onset order, but note {index} ({trebleXsInBeatOrder[index]}) " +
                $"did not advance past note {index - 1} ({trebleXsInBeatOrder[index - 1]}).");
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(10, 5)]
    public void ClampFirstVisibleMeasure_RequestedWindow_ClampsToScoreBounds(
        int requestedMeasure,
        int expectedMeasure)
    {
        var score = CreateScore(measureCount: 6);

        int result = GrandStaffSceneBuilder.ClampFirstVisibleMeasure(score, requestedMeasure);

        Assert.Equal(expectedMeasure, result);
    }

    [Fact]
    public void BuildScore_DottedEighthChord_ReturnsNotationPrimitives()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8, 1), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8, 1), 0, 0, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        Assert.Equal(scene.Notes[0].X, scene.Notes[1].X);
        Assert.Equal(
            [new ScoreNoteAddress(0, 0), new ScoreNoteAddress(0, 1)],
            scene.Notes.Select(note => note.Address));
        Assert.All(scene.Notes, note => Assert.True(note.IsFilled));
        Assert.All(scene.Notes, note => Assert.True(note.HasStem));
        Assert.All(scene.Notes, note => Assert.True(note.HasDot));
        Assert.All(scene.Notes, note => Assert.Equal(1, note.FlagCount));
        Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal(5, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline));
    }

    [Fact]
    public void BuildScore_ChordNotesASecondApart_DisplacesOneNoteheadAndSharesOneStem()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        Assert.NotEqual(scene.Notes[0].X, scene.Notes[1].X);
        Assert.Single(scene.Notes, note => note.HasStem);
    }

    [Fact]
    public void BuildScore_ChordNotesAThirdApart_KeepsSameXButStillSharesOneStem()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        Assert.Equal(scene.Notes[0].X, scene.Notes[1].X);
        Assert.Single(scene.Notes, note => note.HasStem);
    }

    [Fact]
    public void BuildScore_DenseChordWithAccidental_WidensGapToNextOnsetWithoutMovingBarlines()
    {
        ScoreNote[] denseNotes =
        [
            new(new Pitch(NoteLetter.C, 1, 4), new NoteValue(4), 0, 2, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 2, Staff.Treble, IsChordContinuation: true),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2.25, Staff.Treble),
        ];
        ScoreNote[] sparseNotes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2.25, Staff.Treble),
        ];

        var denseScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(denseNotes), firstVisibleMeasure: 0);
        var sparseScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(sparseNotes), firstVisibleMeasure: 0);

        // Compare against the chord's undisplaced (stem-owning) member, C#4 — the displaced D4
        // moves for a different reason (Phase 2's notehead displacement) and would conflate the
        // two effects.
        double denseGap = denseScene.Notes.Single(note => note.Label == "E4").X -
            denseScene.Notes.Single(note => note.Label == "C#4").X;
        double sparseGap = sparseScene.Notes.Single(note => note.Label == "E4").X -
            sparseScene.Notes.Single(note => note.Label == "C4").X;
        Assert.True(
            denseGap > sparseGap,
            $"Expected the dense onset to reserve more room before its neighbor (dense: {denseGap}, sparse: {sparseGap}).");

        var denseBarlines = denseScene.Lines.Where(line => line.Kind == GrandStaffLineKind.Barline).Select(line => line.X0);
        var sparseBarlines = sparseScene.Lines.Where(line => line.Kind == GrandStaffLineKind.Barline).Select(line => line.X0);
        Assert.Equal(sparseBarlines, denseBarlines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BuildScore_MeasureLedByAnAccidental_KeepsTheGlyphClearOfTheBarlineBeforeIt(int measureIndex)
    {
        // The accidental's centre sits 0.019 scene-X left of its notehead and the glyph is roughly 0.018 wide (measured
        // from a rendered screenshot: about 8 px at 458 px per scene-X unit), so its left edge needs the centre to be at
        // least ~0.012 right of the barline. Before the leading-accidental clearance it was 0.001: the glyph was drawn on
        // top of the barline.
        ScoreMeasure[] measures = Enumerable.Range(0, 2)
            .Select(index => new ScoreMeasure(
                [
                    new ScoreNote(
                        new Pitch(NoteLetter.F, index == measureIndex ? 1 : 0, 4),
                        new NoteValue(4),
                        index,
                        0,
                        Staff.Treble,
                        Accidental: index == measureIndex ? ScoreAccidental.Sharp : null),
                    new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), index, 1, Staff.Treble),
                ],
                []))
            .ToArray();
        var score = new Score("test", new TimeSignature(4, new NoteValue(4)), new Tempo(120), 0, measures);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffGlyph accidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        double barlineBefore = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline && line.X0 < accidental.X)
            .Max(line => line.X0);
        Assert.True(
            accidental.X - barlineBefore >= 0.012,
            $"The accidental (X={accidental.X}) sits on the barline before it (X={barlineBefore}).");
    }

    [Fact]
    public void BuildScore_MeasureWithoutALeadingAccidental_KeepsItsNotesWhereTheyWere()
    {
        ScoreNote[] withLaterAccidental =
        [
            new(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(4), 0, 1, Staff.Treble, Accidental: ScoreAccidental.Sharp),
        ];
        ScoreNote[] withNone =
        [
            new(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 0, 4), new NoteValue(4), 0, 1, Staff.Treble),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(withLaterAccidental), firstVisibleMeasure: 0);
        var plain = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(withNone), firstVisibleMeasure: 0);

        Assert.Equal(plain.Notes[0].X, scene.Notes[0].X);
    }

    // Measured on a real 916 x 240 canvas (440 px per scene-X unit across the staff): the previous note's stem stands half a
    // notehead (4.5 px) right of its centre, and the accidental's ink reaches 3.3 px left of the glyph's centre. Both plus
    // 1 px of air are 0.020 scene-X (8.8 px); the glyph's centre sits 0.019 scene-X left of its note (0.024 for a stem-down
    // note), so the note needs to sit at least that much plus 0.020 right of the one before it (0.039 for a stem-up note).
    private const double ReachLeftOfAnAccidentalGlyph = 0.020;

    [Fact]
    public void BuildScore_AccidentalsOnCrowdedEighths_KeepEachGlyphClearOfThePreviousNote()
    {
        // The shape of a real generated Accidentals exercise (Basic rhythm, seed 1, measure 3): two accidentals on
        // beamed eighths, then more accidentals on the quarters after them. Four accidental onsets in one measure used
        // to exhaust the spacing budget, which left the second eighth's sharp drawn on the first eighth's stem.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 1, 4), new NoteValue(8), 0, 0, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 0.5, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.B, -1, 4), new NoteValue(4), 0, 1, Staff.Treble, Accidental: ScoreAccidental.Flat),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.E, -1, 4), new NoteValue(4), 0, 3, Staff.Treble, Accidental: ScoreAccidental.Flat),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        AssertAccidentalsClearOfPreviousNote(scene);
    }

    [Fact]
    public void BuildScore_AccidentalsOnCrowdedEighths_KeepEveryNoteInsideItsMeasure()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 1, 4), new NoteValue(8), 0, 0, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 0.5, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.B, -1, 4), new NoteValue(4), 0, 1, Staff.Treble, Accidental: ScoreAccidental.Flat),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.E, -1, 4), new NoteValue(4), 0, 3, Staff.Treble, Accidental: ScoreAccidental.Flat),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        double[] barlines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline)
            .Select(line => line.X0)
            .Order()
            .ToArray();
        double measureStart = barlines.First(barline => barline < scene.Notes.Min(note => note.X) - 0.0001);
        double measureEnd = barlines.First(barline => barline > scene.Notes.Max(note => note.X));
        Assert.All(scene.Notes, note => Assert.InRange(note.X, measureStart, measureEnd - 0.02 + 0.000001));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Basic, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Basic, Staff.Bass)]
    [InlineData(SightReadingRhythmPreset.Extended, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Syncopated, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Syncopated, Staff.Bass)]
    [InlineData(SightReadingRhythmPreset.Compound, Staff.Treble)]
    public void BuildScore_AccidentalsExercisesWithEighths_KeepEveryGlyphClearOfThePreviousNote(
        SightReadingRhythmPreset rhythmPreset,
        Staff staff)
    {
        for (int seed = 1; seed <= 25; seed++)
        {
            var options = new SightReadingExerciseOptions(
                staff,
                SightReadingPresetId.Accidentals,
                16,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: rhythmPreset);
            Score score = ScoreFingeringGenerator.Generate(
                SightReadingExerciseComposer.Compose(options, new Random(seed)));

            for (int firstMeasure = 0; firstMeasure < score.Measures.Count; firstMeasure += 5)
            {
                var scene = GrandStaffSceneBuilder.BuildScore(
                    score,
                    firstMeasure,
                    showNoteLabels: false,
                    showFingerings: false,
                    drawRests: true,
                    drawTies: true);

                AssertAccidentalsClearOfPreviousNote(scene, $"{rhythmPreset} {staff} seed {seed} from measure {firstMeasure}");
            }
        }
    }

    private static void AssertAccidentalsClearOfPreviousNote(GrandStaffScene scene, string context = "scene")
    {
        GrandStaffNote[] notes = scene.Notes.OrderBy(note => note.X).ToArray();
        foreach (GrandStaffGlyph accidental in scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental))
        {
            GrandStaffNote owner = FindAccidentalOwner(scene, accidental);
            GrandStaffNote? previous = notes.LastOrDefault(note => note.X < owner.X - 0.0001);
            if (previous is null || scene.Lines.Any(line =>
                    line.Kind == GrandStaffLineKind.Barline && line.X0 > previous.X && line.X0 < owner.X))
            {
                continue;
            }

            Assert.True(
                owner.X - previous.X >= (owner.X - accidental.X) + ReachLeftOfAnAccidentalGlyph - 0.000001,
                $"{context}: the {accidental.Text} before {owner.Label} (X={owner.X}) is {owner.X - previous.X} right of " +
                $"{previous.Label}, which would draw it on that note's stem or head.");
        }
    }

    // Measured on a real 916 x 240 canvas (440 px per scene-X unit, 7.5 px staff space): a note's stem stands half a
    // notehead (4.5 px) from its head's centre and is drawn 2 px wide, a sharp's ink reaches 3.83 px right of its centre
    // and a flat's 2.72 px. A stem-down stem is left of the head, in the accidental's way.
    private const double PixelsPerSceneX = 440;
    private const double StemOffsetFromHeadPx = 4.5;
    private const double StemHalfWidthPx = 1;
    private const double AirBetweenAccidentalAndStemPx = 1;

    [Theory]
    [InlineData(ScoreAccidental.Sharp, 3.83)]
    [InlineData(ScoreAccidental.Flat, 2.72)]
    public void BuildScore_StemDownNoteWithAnAccidental_KeepsTheGlyphClearOfItsOwnStem(
        ScoreAccidental accidental,
        double inkReachPx)
    {
        // C5 is above the middle line, so its stem points down: the stem stands on the left of the head, where the
        // accidental goes. At 0.019 scene-X the sharp's ink overlapped the stem and the flat's touched it (0.14 px).
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, accidental == ScoreAccidental.Sharp ? 1 : -1, 5), new NoteValue(4), 0, 0, Staff.Treble, Accidental: accidental),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        GrandStaffNote note = Assert.Single(scene.Notes);
        Assert.True(note.HasStem);
        Assert.Equal(StemDirection.Down, note.StemDirection);
        GrandStaffGlyph glyph = Assert.Single(scene.Glyphs, candidate => candidate.Kind == GrandStaffGlyphKind.Accidental);
        AssertGlyphClearOfOwnDownStem(glyph, note, inkReachPx, "a single stem-down note");
    }

    [Fact]
    public void BuildScore_BeamedStemDownPairWithAccidentals_KeepsEachGlyphClearOfItsOwnStem()
    {
        // The stem direction of a beamed pair comes from the beam, not from the note, so the clearance has to follow it.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.F, 1, 5), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.E, -1, 5), new NoteValue(8), 0, 0.5, Staff.Treble, BeamState: BeamState.End, Accidental: ScoreAccidental.Flat),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        Assert.All(scene.Notes, note => Assert.Equal(StemDirection.Down, note.StemDirection));
        GrandStaffNote[] byX = scene.Notes.OrderBy(note => note.X).ToArray();
        GrandStaffGlyph[] glyphs = scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental).OrderBy(glyph => glyph.X).ToArray();
        AssertGlyphClearOfOwnDownStem(glyphs[0], byX[0], 3.83, "the beamed sharp");
        AssertGlyphClearOfOwnDownStem(glyphs[1], byX[1], 2.72, "the beamed flat");
    }

    [Fact]
    public void BuildScore_StemUpNoteWithAnAccidental_KeepsTheGlyphWhereItAlwaysWas()
    {
        // A stem-up stem is on the right of the head, away from the accidental, so those notes are untouched.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(4), 0, 0, Staff.Treble, Accidental: ScoreAccidental.Sharp),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        GrandStaffNote note = Assert.Single(scene.Notes);
        Assert.Equal(StemDirection.Up, note.StemDirection);
        GrandStaffGlyph glyph = Assert.Single(scene.Glyphs, candidate => candidate.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal(0.019, note.X - glyph.X, precision: 5);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Fixed, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Fixed, Staff.Bass)]
    [InlineData(SightReadingRhythmPreset.Basic, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Basic, Staff.Bass)]
    [InlineData(SightReadingRhythmPreset.Extended, Staff.Treble)]
    [InlineData(SightReadingRhythmPreset.Syncopated, Staff.Bass)]
    [InlineData(SightReadingRhythmPreset.Compound, Staff.Treble)]
    public void BuildScore_AccidentalsExercises_KeepEveryGlyphClearOfItsOwnStemDownStem(
        SightReadingRhythmPreset rhythmPreset,
        Staff staff)
    {
        int checkedGlyphs = 0;
        for (int seed = 1; seed <= 25; seed++)
        {
            var options = new SightReadingExerciseOptions(
                staff,
                SightReadingPresetId.Accidentals,
                16,
                rhythmPreset == SightReadingRhythmPreset.Fixed ? NoteReadingMode.PitchAndOrder : NoteReadingMode.PitchAndRhythm,
                RhythmPreset: rhythmPreset);
            Score score = ScoreFingeringGenerator.Generate(
                SightReadingExerciseComposer.Compose(options, new Random(seed)));

            for (int firstMeasure = 0; firstMeasure < score.Measures.Count; firstMeasure += 5)
            {
                var scene = GrandStaffSceneBuilder.BuildScore(
                    score,
                    firstMeasure,
                    showNoteLabels: false,
                    showFingerings: false,
                    drawRests: true,
                    drawTies: true);

                foreach (GrandStaffGlyph glyph in scene.Glyphs.Where(candidate => candidate.Kind == GrandStaffGlyphKind.Accidental))
                {
                    GrandStaffNote owner = FindAccidentalOwner(scene, glyph);
                    if (owner.HasStem && owner.StemDirection == StemDirection.Down)
                    {
                        AssertGlyphClearOfOwnDownStem(
                            glyph,
                            owner,
                            glyph.Text == "♯" ? 3.83 : 2.72,
                            $"{rhythmPreset} {staff} seed {seed} from measure {firstMeasure}");
                        checkedGlyphs++;
                    }
                }
            }
        }

        Assert.True(checkedGlyphs >= 10, $"Only {checkedGlyphs} stem-down accidentals were examined.");
    }

    /// <summary>The accidental's note: the nearest note at the glyph's height a little to its right.</summary>
    private static GrandStaffNote FindAccidentalOwner(GrandStaffScene scene, GrandStaffGlyph accidental) =>
        scene.Notes
            .Where(note => note.X > accidental.X && note.X - accidental.X < 0.035)
            .MinBy(note => Math.Abs(note.Y - accidental.Y) + (note.X - accidental.X))!;

    private static void AssertGlyphClearOfOwnDownStem(
        GrandStaffGlyph glyph,
        GrandStaffNote note,
        double inkReachPx,
        string context)
    {
        double stemLeftEdgeX = note.X - ((StemOffsetFromHeadPx + StemHalfWidthPx) / PixelsPerSceneX);
        double inkRightEdgeX = glyph.X + (inkReachPx / PixelsPerSceneX);
        double gapPx = (stemLeftEdgeX - inkRightEdgeX) * PixelsPerSceneX;
        Assert.True(
            gapPx >= AirBetweenAccidentalAndStemPx - 0.01,
            $"{context}: the {glyph.Text} before {note.Label} leaves {gapPx:F2} px to its own stem, " +
            $"wanted at least {AirBetweenAccidentalAndStemPx} px.");
    }

    [Fact]
    public void BuildScore_AccidentalsDerivedFromTheKeySignature_ReserveTheSameRoomAsExplicitOnes()
    {
        // The renderer draws a glyph for a note whose alteration differs from the key signature even when the score states
        // no accidental for it (MusicXML omits the element for a repeated accidental, and a hand-built score may never
        // state one). Those glyphs landed on the previous note's stem because only explicit accidentals got spacing room.
        Pitch[] pitches = [new(NoteLetter.C, 1, 4), new(NoteLetter.F, 1, 4), new(NoteLetter.B, -1, 4), new(NoteLetter.A, 0, 4), new(NoteLetter.E, -1, 4)];
        NoteValue[] values = [new(8), new(8), new(4), new(4), new(4)];
        double[] beats = [0, 0.5, 1, 2, 3];
        ScoreAccidental?[] explicitAccidentals = [ScoreAccidental.Sharp, ScoreAccidental.Sharp, ScoreAccidental.Flat, null, ScoreAccidental.Flat];

        ScoreNote[] Notes(bool stateAccidentals) => pitches
            .Select((pitch, index) => new ScoreNote(
                pitch, values[index], 0, beats[index], Staff.Treble,
                Accidental: stateAccidentals ? explicitAccidentals[index] : null))
            .ToArray();
        var explicitScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(stateAccidentals: true)), firstVisibleMeasure: 0);
        var derivedScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(stateAccidentals: false)), firstVisibleMeasure: 0);

        Assert.Equal(
            explicitScene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental).Select(glyph => glyph.Text),
            derivedScene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental).Select(glyph => glyph.Text));
        Assert.Equal(explicitScene.Notes.Select(note => note.X), derivedScene.Notes.Select(note => note.X));
        AssertAccidentalsClearOfPreviousNote(derivedScene, "derived accidentals in C major");
    }

    [Fact]
    public void BuildScore_DerivedNaturalInASharpKey_ReservesRoomLikeAnExplicitNatural()
    {
        // In G major (one sharp) a plain F is a natural against the key signature and is drawn with a natural sign.
        Pitch[] pitches = [new(NoteLetter.D, 0, 5), new(NoteLetter.F, 0, 5), new(NoteLetter.G, 0, 5), new(NoteLetter.F, 0, 5)];
        NoteValue[] values = [new(8), new(8), new(4), new(4)];
        double[] beats = [0, 0.5, 1, 2];

        ScoreNote[] Notes(bool stateAccidentals) => pitches
            .Select((pitch, index) => new ScoreNote(
                pitch, values[index], 0, beats[index], Staff.Treble,
                Accidental: stateAccidentals && pitch.Letter == NoteLetter.F ? ScoreAccidental.Natural : null))
            .ToArray();
        var explicitScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(true), keyFifths: 1), firstVisibleMeasure: 0);
        var derivedScene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(false), keyFifths: 1), firstVisibleMeasure: 0);

        Assert.Equal(2, derivedScene.Glyphs.Count(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental && glyph.Text == "♮"));
        Assert.Equal(explicitScene.Notes.Select(note => note.X), derivedScene.Notes.Select(note => note.X));
    }

    [Fact]
    public void BuildScore_NoteThatMatchesTheKeySignature_ReservesNoExtraRoom()
    {
        // Guard: in G major an F sharp prints no glyph, so it must be spaced exactly like a plain note with no glyph.
        Pitch[] sharpPitches = [new(NoteLetter.G, 0, 4), new(NoteLetter.F, 1, 4), new(NoteLetter.A, 0, 4), new(NoteLetter.F, 1, 4)];
        Pitch[] plainPitches = [new(NoteLetter.G, 0, 4), new(NoteLetter.E, 0, 4), new(NoteLetter.A, 0, 4), new(NoteLetter.E, 0, 4)];
        NoteValue[] values = [new(8), new(8), new(4), new(4)];
        double[] beats = [0, 0.5, 1, 2];

        ScoreNote[] Notes(Pitch[] pitches) => pitches
            .Select((pitch, index) => new ScoreNote(pitch, values[index], 0, beats[index], Staff.Treble))
            .ToArray();
        var inKey = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(sharpPitches), keyFifths: 1), firstVisibleMeasure: 0);
        var plain = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(Notes(plainPitches), keyFifths: 1), firstVisibleMeasure: 0);

        Assert.DoesNotContain(inKey.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal(plain.Notes.Select(note => note.X), inKey.Notes.Select(note => note.X));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BuildScore_MeasureLedByADerivedAccidental_KeepsTheGlyphClearOfTheBarlineBeforeIt(int measureIndex)
    {
        // Same as the explicit-accidental case above, for a first note whose sharp comes from the key signature (C major).
        ScoreMeasure[] measures = Enumerable.Range(0, 2)
            .Select(index => new ScoreMeasure(
                [
                    new ScoreNote(new Pitch(NoteLetter.F, index == measureIndex ? 1 : 0, 4), new NoteValue(4), index, 0, Staff.Treble),
                    new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), index, 1, Staff.Treble),
                ],
                []))
            .ToArray();
        var score = new Score("test", new TimeSignature(4, new NoteValue(4)), new Tempo(120), 0, measures);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffGlyph accidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        double barlineBefore = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline && line.X0 < accidental.X)
            .Max(line => line.X0);
        Assert.True(
            accidental.X - barlineBefore >= 0.012,
            $"The derived accidental (X={accidental.X}) sits on the barline before it (X={barlineBefore}).");
    }

    [Fact]
    public void BuildScore_CursorAtDenseChordOnset_AlignsWithChordsUndisplacedNotehead()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 1, 4), new NoteValue(4), 0, 2, Staff.Treble, Accidental: ScoreAccidental.Sharp),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 2, Staff.Treble, IsChordContinuation: true),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2.25, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 2);

        var cursorLine = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        double stemOwnerX = scene.Notes.Single(note => note.HasStem && note.ScoreOnsetBeats == 2).X;
        Assert.Equal(stemOwnerX, cursorLine.X0);
    }

    [Fact]
    public void BuildScore_ChordFillsRestOfMeasureAfterLeadingNote_DisplacedMemberStaysAheadOfLeadingNote()
    {
        // Regression test for a real imported score: a single leading note followed immediately
        // by a 2nd-interval chord that fills the rest of the measure (nothing after it). The
        // chord's displaced member moves toward the leading note, so the reserved spacing gap
        // must be strictly more than the displacement itself, or floating-point rounding alone
        // can push the displaced notehead behind the leading note.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.E, 0, 3), new NoteValue(4), 0, 0, Staff.Bass),
            new(new Pitch(NoteLetter.A, 0, 3), new NoteValue(2), 0, 1, Staff.Bass),
            new(new Pitch(NoteLetter.B, 0, 3), new NoteValue(2), 0, 1, Staff.Bass, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes, timeSignature: new TimeSignature(3, new NoteValue(4)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        double leadingNoteX = scene.Notes.Single(note => note.Label == "E3").X;
        double displacedChordMemberX = scene.Notes.Where(note => !note.HasStem).Select(note => note.X).Single();
        Assert.True(
            displacedChordMemberX > leadingNoteX,
            $"Displaced chord member (X={displacedChordMemberX}) should stay ahead of the leading note (X={leadingNoteX}).");
    }

    [Fact]
    public void BuildScore_IndependentVoicesAtSharedOnset_DisplaceApartButKeepSeparateStems()
    {
        // Not a chord: neither note carries IsChordContinuation, matching two voices reached via
        // MusicXML <backup> rather than a real <chord/> — see MusicXmlScoreReaderTests for the
        // parser-level distinction this relies on. They're still a 2nd apart and simultaneous, so
        // they must be pulled apart just like a real chord would be — see
        // BuildScore_TwoIndependentVoicesDifferentDurations_DisplaceApartWithSeparateStems for the
        // real-world shape (different durations) this generalizes from.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        Assert.NotEqual(scene.Notes[0].X, scene.Notes[1].X);
        Assert.All(scene.Notes, note => Assert.True(note.HasStem));
    }

    [Fact]
    public void BuildScore_TwoIndependentVoicesDifferentDurations_DisplaceApartWithSeparateStems()
    {
        // Regression test for a real imported score ("foo1", tact 12): a dotted-half A4 and a
        // quarter G#4 share an onset on the treble staff, a 2nd apart, with neither carrying
        // IsChordContinuation. Different durations prove they can never be one real chord (a
        // chord shares a single stem and duration across every member) — they're two independent
        // voices that happen to start together. They still need to be visually pulled apart, or
        // they render on top of each other, but each must keep its own independently-shaped stem.
        // Two-voice notation convention: the upper voice (A4) always stems up and the lower voice
        // (G#4) always stems down, regardless of where either sits relative to the middle line —
        // NOT the single-note automatic rule, which would have put both notes' stems up here.
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(2, dots: 1), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.G, 1, 4), new NoteValue(4), 0, 0, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        GrandStaffNote a4 = scene.Notes.Single(note => note.Label == "A4");
        GrandStaffNote gSharp4 = scene.Notes.Single(note => note.Label == "G#4");
        Assert.NotEqual(a4.X, gSharp4.X);
        Assert.True(a4.HasStem);
        Assert.True(gSharp4.HasStem);
        Assert.True(a4.HasDot);
        Assert.False(gSharp4.HasDot);
        Assert.Equal(StemDirection.Up, a4.StemDirection);
        Assert.Equal(StemDirection.Down, gSharp4.StemDirection);
    }

    [Fact]
    public void BuildScore_TwoIndependentVoicesFarApart_KeepsIndependentStemDirectionsUnchanged()
    {
        // Two voices sharing an onset but far enough apart (a 5th, not a 2nd) that they never
        // visually collide must NOT be forced into the top-up/bottom-down two-voice convention —
        // that convention exists only to disambiguate notes that would otherwise overlap. Each
        // keeps whatever direction its own explicit MusicXML <stem> specified.
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.G, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                StemDirection: ScoreStemDirection.Down),
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                StemDirection: ScoreStemDirection.Up),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffNote g4 = scene.Notes.Single(note => note.Label == "G4");
        GrandStaffNote c4 = scene.Notes.Single(note => note.Label == "C4");
        Assert.Equal(StemDirection.Down, g4.StemDirection);
        Assert.Equal(StemDirection.Up, c4.StemDirection);
    }

    [Fact]
    public void BuildScore_IvanovskayaOpeningCSharp_RendersOnTrebleLedgerLine()
    {
        string fixture = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "mia_sebastians_theme_ivanovskaya_transcription.musicxml");
        var score = new MusicXmlScoreReader().Read(fixture);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var openingNote = Assert.Single(
            scene.Notes,
            note => note.Address == new ScoreNoteAddress(0, 0));
        Assert.Equal("C#4", openingNote.Label);

        GrandStaffLine[] trebleStaffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .Take(5)
            .ToArray();
        double staffSpace = trebleStaffLines[1].Y0 - trebleStaffLines[0].Y0;
        Assert.Equal(trebleStaffLines[0].Y0 - staffSpace, openingNote.Y, 6);

        var ledgerLine = Assert.Single(
            scene.Lines,
            line => line.Kind == GrandStaffLineKind.Ledger
                && Math.Abs(line.Y0 - openingNote.Y) < 0.000001
                && line.X0 < openingNote.X
                && line.X1 > openingNote.X);
        Assert.Equal(openingNote.Y, ledgerLine.Y1, 6);
    }

    [Fact]
    public void BuildScore_TrebleNoteWellBelowMiddleC_StaysOnTrebleStaffWithLedgerLines()
    {
        // Regression test: a treble-only exercise (e.g. the "ledger lines" preset, which deliberately
        // requires at least one prompt below the staff) can legitimately compose a note as low as A3 —
        // still Staff.Treble throughout, notated with ledger lines below the staff, never Staff.Bass.
        // GrandStaffLayout.GetLivePosition (MIDI < 60 => Bass) exists for *live*, un-scored pitches with
        // no authored staff at all; it must not override an explicitly authored Staff.Treble note just
        // because the pitch alone would read as "bass register" in isolation.
        var lowNote = new ScoreNote(new Pitch(NoteLetter.A, 0, 3), new NoteValue(4), 0, 0, Staff.Treble);
        var higherNote = new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 1, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([lowNote, higherNote], [])]);

        GrandStaffScene scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffNote renderedLowNote = scene.Notes.Single(note => note.Address == new ScoreNoteAddress(0, 0));

        // GetPosition's raw Y is staff-independent by design (both staves share one continuous diatonic
        // pitch axis) — only GrandStaffLayout.SeparateStaffY's staff-specific push actually splits the two
        // staff systems apart on screen. So the visible symptom of this bug isn't a slightly-off Y among
        // extra ledger lines: a misclassified note gets yanked by a full 2 * StaffSeparationOffset into the
        // other staff's block, exactly matching the reported screenshot (a note sitting on the bass staff,
        // not just under-ledgered on the treble staff).
        double expectedTrebleY = GrandStaffLayout.SeparateStaffY(
            GrandStaffLayout.GetPosition(lowNote.Pitch, Staff.Treble).Y,
            Staff.Treble);
        double bassY = GrandStaffLayout.SeparateStaffY(
            GrandStaffLayout.GetPosition(lowNote.Pitch, Staff.Bass).Y,
            Staff.Bass);
        Assert.NotEqual(bassY, expectedTrebleY); // sanity: the two staves really do render this pitch differently
        Assert.Equal(expectedTrebleY, renderedLowNote.Y, 6);
    }

    [Fact]
    public void Build_LivePerformedNote_HasNoScoreNoteAddress()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(0.5));

        Assert.Null(Assert.Single(scene.Notes).Address);
    }

    [Fact]
    public void BuildScore_VisibleNote_ReturnsPlaybackBeatInterval()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(8),
            MeasureIndex: 2,
            BeatOffset: 1,
            Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            0,
            [new ScoreMeasure([], []), new ScoreMeasure([], []), new ScoreMeasure([note], [])]);

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Equal(13, renderedNote.ScoreOnsetBeats);
        Assert.Equal(14, renderedNote.ScoreEndBeats);
    }

    [Fact]
    public void BuildScore_DifferentPitchesOnEachStaff_UsesSeparateLabelRowsBelowEachStaff()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 5), new NoteValue(4), 0, 1, Staff.Treble),
            new(new Pitch(NoteLetter.C, 0, 3), new NoteValue(4), 0, 2, Staff.Bass),
            new(new Pitch(NoteLetter.A, 0, 2), new NoteValue(4), 0, 3, Staff.Bass),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNotes = scene.Notes
            .ToDictionary(note => note.Label);
        var staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassBottomLineY = staffLines.Skip(5).Min(line => line.Y0);

        Assert.Equal(renderedNotes["A4"].LabelY, renderedNotes["F#5"].LabelY);
        Assert.Equal(renderedNotes["C3"].LabelY, renderedNotes["A2"].LabelY);
        Assert.NotEqual(renderedNotes["A4"].LabelY, renderedNotes["C3"].LabelY);
        Assert.True(renderedNotes["A4"].LabelY < trebleBottomLineY);
        Assert.True(renderedNotes["C3"].LabelY < bassBottomLineY);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void BuildScore_SimultaneousNotesOnSameStaff_StacksLabels(int noteCount)
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 3), new NoteValue(4), 0, 2, Staff.Bass),
            new(new Pitch(NoteLetter.E, 0, 3), new NoteValue(4), 0, 2, Staff.Bass),
            new(
                new Pitch(NoteLetter.G, 0, 3),
                new NoteValue(4),
                0,
                2,
                Staff.Bass,
                Fingering: new ScoreFingering(5)),
        ];
        var score = ScoreWithNotes(notes.Take(noteCount).ToArray());

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        GrandStaffNote[] renderedNotes = scene.Notes.ToArray();
        var annotationLane = Assert.Single(scene.Bands);
        double[] bassStaffLineYs = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .Skip(5)
            .Take(2)
            .Select(line => line.Y0)
            .ToArray();
        double renderedStaffSpace = bassStaffLineYs[1] - bassStaffLineYs[0];
        double[] labelYs = renderedNotes
            .Select(note => note.LabelY!.Value)
            .OrderDescending()
            .ToArray();

        Assert.Single(renderedNotes.Select(note => note.X).Distinct());
        Assert.Equal(noteCount, labelYs.Distinct().Count());
        Assert.All(
            labelYs.Zip(labelYs.Skip(1)),
            pair => Assert.True(pair.First - pair.Second >= 2 * renderedStaffSpace));
        Assert.All(
            renderedNotes,
            note => Assert.InRange(note.LabelY!.Value, annotationLane.Y0, annotationLane.Y1));
        if (noteCount == 3)
        {
            double fingeringY = Assert.Single(renderedNotes, note => note.Fingering is not null).FingeringY!.Value;
            Assert.True(fingeringY < renderedNotes.Min(note => note.LabelY));
            Assert.InRange(fingeringY, annotationLane.Y0, annotationLane.Y1);
        }
    }

    [Fact]
    public void BuildScore_IvanovskayaThirdMeasureBassChord_StacksLabelsAndDisplacesNoteheads()
    {
        string fixture = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "mia_sebastians_theme_ivanovskaya_transcription.musicxml");
        var score = new MusicXmlScoreReader().Read(fixture);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        GrandStaffNote[] chordNotes = scene.Notes
            .Where(note => note.Address?.MeasureIndex == 2 && note.Label is "A3" or "B3")
            .ToArray();

        Assert.Equal(2, chordNotes.Length);
        // A3 and B3 are a 2nd apart, so the shared-stem chord displaces one notehead to the side.
        Assert.Equal(2, chordNotes.Select(note => note.X).Distinct().Count());
        Assert.Equal(2, chordNotes.Select(note => note.LabelY).Distinct().Count());
        Assert.Single(chordNotes, note => note.HasStem);
    }

    [Theory]
    [InlineData(Staff.Treble, "R3")]
    [InlineData(Staff.Bass, "L3")]
    public void BuildScore_Fingering_LabelsWhichHandPlaysTheNote(Staff staff, string expectedLabel)
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Equal(expectedLabel, renderedNote.Fingering);
    }

    [Fact]
    public void BuildScore_Fingering_YPositionIgnoresMusicXmlPlacement()
    {
        double? GetFingeringY(ScoreFingeringPlacement? placement)
        {
            var sourceNote = new ScoreNote(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                Fingering: new ScoreFingering(3, placement));
            var score = SingleNoteScore(sourceNote);
            return GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes.Single().FingeringY;
        }

        double? aboveY = GetFingeringY(ScoreFingeringPlacement.Above);
        double? belowY = GetFingeringY(ScoreFingeringPlacement.Below);
        double? unspecifiedY = GetFingeringY(null);

        Assert.NotNull(aboveY);
        Assert.Equal(aboveY, belowY);
        Assert.Equal(aboveY, unspecifiedY);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_Fingering_PositionedBelowNotationStaff(Staff staff)
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double staffBottomLineY = staff == Staff.Treble
            ? staffLines.Take(5).Min(line => line.Y0)
            : staffLines.Skip(5).Min(line => line.Y0);

        Assert.NotNull(renderedNote.FingeringY);
        Assert.True(renderedNote.FingeringY < staffBottomLineY);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_Fingering_ClearsGapBelowItsNoteLabel(Staff staff)
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(5));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double staffSpace = staffLines[1].Y0 - staffLines[0].Y0;

        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.True(renderedNote.LabelY > renderedNote.FingeringY);
        Assert.True(renderedNote.LabelY - renderedNote.FingeringY >= staffSpace);
    }

    [Theory]
    [InlineData(NoteLetter.C, 4)] // five-note preset's lowest possible note: one ledger line below the staff
    [InlineData(NoteLetter.A, 3)] // ledger-lines preset's lowest possible note: two ledger lines below the staff
    public void BuildScore_LowTrebleNoteWithBothLabelAndFingering_FingeringClearsTheBassStaff(
        NoteLetter letter,
        int octave)
    {
        // Regression test for a reported bug: with both the note-name and fingering rows shown at once for a
        // low treble note (needing ledger lines below the staff), the fingering row could run far enough down
        // to visually overlap the bass staff below it — most visible at the smallest clamped canvas height.
        // Asserts the fix's invariant (a real, positive clearance above the bass staff's top line) rather than
        // recomputing the exact clamped value the production code itself computes.
        var sourceNote = new ScoreNote(
            new Pitch(letter, 0, octave),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        double staffSpace = staffLines[1].Y0 - staffLines[0].Y0;

        Assert.NotNull(renderedNote.FingeringY);
        Assert.True(
            renderedNote.FingeringY!.Value > bassTopLineY,
            $"Fingering Y ({renderedNote.FingeringY}) must stay above the bass staff's top line ({bassTopLineY}).");
        Assert.True(
            renderedNote.FingeringY!.Value - bassTopLineY >= staffSpace / 2,
            "Fingering must clear the bass staff by a real margin, not just barely avoid touching it.");
    }

    [Fact]
    public void BuildScore_LowTrebleNoteWithFingering_FloorClampStillEngagesAtTheWidenedRowSeparation()
    {
        // Proves the bass-staff-overlap clamp is doing real work, not just coincidentally already-fine: after
        // widening FingeringRowSeparation (8 -> 10 diatonic steps) for user-requested breathing room in the
        // common single-note case, the *natural*, unclamped fingering position for this low note would fall
        // below the bass staff's own top line — i.e. GetAnnotationRows's Math.Max(naturalFingeringY, floorY)
        // clamp is the only thing keeping it in place, not a coincidence of the new, larger constant.
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        // Independently reconstructs GrandStaffSceneBuilder's own floorY (bass staff's top line plus a
        // LabelRowSeparation margin) rather than importing it, so this test would catch a regression in either
        // the margin GrandStaffSceneBuilder passes in or FingeringRowSeparation itself.
        double floorY = bassTopLineY + GrandStaffLayout.LabelRowSeparation;

        double naturalFingeringY = renderedNote.LabelY!.Value - GrandStaffLayout.FingeringRowSeparation;
        Assert.True(
            naturalFingeringY < floorY,
            $"Expected the unclamped position ({naturalFingeringY}) to fall below the bass-staff floor " +
            $"({floorY}) at the new, wider separation — otherwise this test can't prove the clamp is actually " +
            "the thing keeping the rendered fingering row in place.");
        Assert.NotNull(renderedNote.FingeringY);
        Assert.True(renderedNote.FingeringY!.Value > naturalFingeringY);
        Assert.True(renderedNote.FingeringY!.Value >= floorY);
        Assert.True(renderedNote.FingeringY!.Value < trebleBottomLineY);
    }

    [Fact]
    public void BuildScore_ThreeNoteTrebleChordWithLabelsAndFingering_ShrinksLabelFontAndDropsFingering()
    {
        // Regression test for a reported bug, worst-case variant: a 3-note chord (e.g. the "Chords" exercise
        // preset's triads) stacks three label rows, and together with a fingering row that adds up to more
        // depth than a single label row ever needed — confirmed (via manual reproduction against the unfixed
        // code) to spill directly onto the bass staff's lines, and confirmed separately that merely
        // clamping/compressing all three rows' *positions* to fit still left the label *text* illegibly
        // overlapping (16px-tall text squeezed into much less than 16px of row separation). The actual fix:
        // fingering is dropped for this staff (see GetAnnotationRows's doc comment for the exact ratio-based
        // cutoff and the milder two-note case it deliberately leaves alone), and the label font shrinks in
        // lockstep with the compressed row spacing (see GrandStaffSceneBuilder's LabelFontScale comment) so
        // the row-gap-to-text-height ratio — and so legibility — stays the same as an uncompressed row, just
        // smaller. An earlier attempt combined all three names into one row on the highest note instead; that
        // was abandoned after visual testing showed it just traded vertical crowding for horizontal crowding
        // between adjacent chords (the "Chords" preset puts one triad on every beat).
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, Fingering: new ScoreFingering(5)),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, Fingering: new ScoreFingering(3)),
            new(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, Fingering: new ScoreFingering(1)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);

        Assert.All(scene.Notes, note => Assert.Null(note.FingeringY));
        Assert.All(scene.Notes, note => Assert.Null(note.Fingering));

        // Still one stacked row per note (not combined) — each clears the bass staff.
        Assert.All(scene.Notes, note => Assert.NotNull(note.LabelY));
        Assert.All(scene.Notes, note => Assert.True(note.LabelY!.Value >= bassTopLineY));
        Assert.Equal(3, scene.Notes.Select(note => note.LabelY).Distinct().Count());

        // All three share this staff's one compressed row separation, so they all shrink by the same amount —
        // meaningfully smaller than full size, but not scaled down to nothing.
        double fontScale = Assert.Single(scene.Notes.Select(note => note.LabelFontScale).Distinct());
        Assert.InRange(fontScale, 0.3, 0.99);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_PitchSelectedNotationStaff_KeepsNoteAboveItsAnnotationLane(Staff staff)
    {
        // C4 on treble and C3 on bass both sit one ledger line below their own staff.
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.LabelY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.InRange(renderedNote.FingeringY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.True(renderedNote.Y > annotationLane.Y1);
    }

    [Fact]
    public void BuildScore_DifferentPitches_StayInSameDedicatedStaffLane()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                Fingering: new ScoreFingering(1)),
            new(
                new Pitch(NoteLetter.A, 0, 4),
                new NoteValue(4),
                0,
                1,
                Staff.Treble,
                Fingering: new ScoreFingering(5)),
        ];
        var score = ScoreWithNotes(notes);

        var renderedNotes = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes
            .ToDictionary(note => note.Label);

        Assert.Equal(renderedNotes["C4"].LabelY, renderedNotes["A4"].LabelY);
        Assert.Equal(renderedNotes["C4"].FingeringY, renderedNotes["A4"].FingeringY);
        Assert.True(renderedNotes["C4"].LabelY < renderedNotes["C4"].Y);
        Assert.True(renderedNotes["A4"].LabelY < renderedNotes["A4"].Y);
    }

    [Fact]
    public void BuildScore_NoteBelowTrebleStaff_DoesNotMoveBassAnnotationLane()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.C, 0, 3), new NoteValue(4), 0, 1, Staff.Bass),
        ];
        var scoreWithLowTreble = ScoreWithNotes(notes);
        var scoreWithoutLowTreble = SingleNoteScore(notes[1]);

        var bassLabelYWithLowTreble = GrandStaffSceneBuilder.BuildScore(scoreWithLowTreble, firstVisibleMeasure: 0)
            .Notes.Single(note => note.Label == "C3").LabelY;
        var bassLabelYAlone = GrandStaffSceneBuilder.BuildScore(scoreWithoutLowTreble, firstVisibleMeasure: 0)
            .Notes.Single(note => note.Label == "C3").LabelY;

        Assert.Equal(bassLabelYAlone, bassLabelYWithLowTreble);
    }

    [Fact]
    public void BuildScore_NoteBelowTrebleStaff_FingeringUsesInterStaffLane()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(1));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);

        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.FingeringY.Value, bassTopLineY, trebleBottomLineY);
    }

    [Theory]
    [InlineData(NoteLetter.F, 3)]
    [InlineData(NoteLetter.D, 3)]
    public void BuildScore_LowPitchMarkedTreble_UsesBassNotationAndAnnotationLane(
        NoteLetter letter,
        int octave)
    {
        var sourceNote = new ScoreNote(
            new Pitch(letter, 0, octave),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(4));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.Equal("R4", renderedNote.Fingering);
        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.LabelY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.InRange(renderedNote.FingeringY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.True(renderedNote.Y > annotationLane.Y1);
    }

    [Fact]
    public void BuildScore_OctaveShiftedNote_AutoSelectsStaffFromNotatedPitch()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.B, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            SoundingOctavesAboveNotated: 1);
        var score = SingleNoteScore(sourceNote);
        StaffPlacement expectedPosition = GrandStaffLayout.GetPosition(
            new Pitch(NoteLetter.B, 0, 3),
            Staff.Bass);

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Equal(
            GrandStaffLayout.SeparateStaffY(expectedPosition.Y, Staff.Bass),
            renderedNote.Y,
            precision: 6);
    }

    [Fact]
    public void BuildScore_LowBassNote_AnnotationsStayBelowBassStaff()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.F, 0, 1),
            new NoteValue(4),
            0,
            0,
            Staff.Bass,
            Fingering: new ScoreFingering(2));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double bassBottomLineY = staffLines.Skip(5).Min(line => line.Y0);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.True(renderedNote.LabelY < bassBottomLineY);
        Assert.True(renderedNote.FingeringY < bassBottomLineY);
        Assert.True(renderedNote.FingeringY < renderedNote.LabelY);
        Assert.True(renderedNote.Y > annotationLane.Y1);
    }

    [Fact]
    public void BuildScore_HighBassNoteWithLowerBassNote_StaysOnBassStaff()
    {
        var bassNote = new ScoreNote(
            new Pitch(NoteLetter.C, 1, 4),
            new NoteValue(2),
            0,
            1,
            Staff.Bass,
            Fingering: new ScoreFingering(2));
        var score = ScoreWithNotes(
        [
            new ScoreNote(new Pitch(NoteLetter.F, 1, 3), new NoteValue(4), 0, 0, Staff.Bass),
            new ScoreNote(new Pitch(NoteLetter.F, 1, 4), new NoteValue(4), 0, 1, Staff.Treble),
            bassNote,
        ],
        timeSignature: new TimeSignature(3, new NoteValue(4)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes, note => note.Label == "C#4");
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);

        Assert.Equal("L2", renderedNote.Fingering);
        Assert.True(renderedNote.Y > bassTopLineY);
        Assert.True(renderedNote.Y - bassTopLineY < trebleBottomLineY - renderedNote.Y);
        Assert.Contains(scene.Lines, line =>
            line.Kind == GrandStaffLineKind.Ledger &&
            line.Y0 == renderedNote.Y &&
            line.X0 < renderedNote.X && line.X1 > renderedNote.X);
    }

    [Fact]
    public void BuildScore_SimultaneousTrebleAndMiddleCBass_PreservesSeparateStaves()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 5), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Bass),
        ];
        var score = ScoreWithNotes(notes);
        StaffPlacement expectedTreblePosition = GrandStaffLayout.GetPosition(notes[0].Pitch, Staff.Treble);
        StaffPlacement expectedBassPosition = GrandStaffLayout.GetPosition(notes[1].Pitch, Staff.Bass);

        var renderedNotes = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes;
        var renderedTrebleNote = Assert.Single(renderedNotes, note => note.Label == "C5");
        var renderedBassNote = Assert.Single(renderedNotes, note => note.Label == "C4");

        Assert.Equal(
            GrandStaffLayout.SeparateStaffY(expectedTreblePosition.Y, Staff.Treble),
            renderedTrebleNote.Y,
            precision: 6);
        Assert.Equal(
            GrandStaffLayout.SeparateStaffY(expectedBassPosition.Y, Staff.Bass),
            renderedBassNote.Y,
            precision: 6);
    }

    [Fact]
    public void BuildScore_NoteWithoutFingering_LeavesFingeringFieldsNull()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var renderedNote = Assert.Single(GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Null(renderedNote.Fingering);
        Assert.Null(renderedNote.FingeringY);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, false)]
    public void BuildScore_ShowLabelsOrFingeringsFalse_HidesOnlyThatOneAndKeepsTheOther(
        bool showNoteLabels,
        bool showFingerings,
        bool expectLabel,
        bool expectFingering)
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            showNoteLabels: showNoteLabels,
            showFingerings: showFingerings);

        var renderedNote = Assert.Single(scene.Notes);
        Assert.Equal(expectLabel, renderedNote.LabelY is not null);
        Assert.Equal(expectFingering, renderedNote.Fingering is not null);
        Assert.Equal(expectFingering, renderedNote.FingeringY is not null);
    }

    [Fact]
    public void BuildScore_Annotations_UseSeparateLanesBelowEachPopulatedStaff()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                Fingering: new ScoreFingering(1)),
            new(
                new Pitch(NoteLetter.D, 0, 4),
                new NoteValue(4),
                0,
                1,
                Staff.Treble,
                Fingering: new ScoreFingering(2)),
            new(
                new Pitch(NoteLetter.C, 0, 3),
                new NoteValue(4),
                0,
                2,
                Staff.Bass,
                StemDirection: ScoreStemDirection.Down,
                Fingering: new ScoreFingering(5)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Bands.Count);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassBottomLineY = staffLines.Skip(5).Min(line => line.Y0);
        GrandStaffNote[] trebleNotes = scene.Notes.Where(note => note.Label is "C4" or "D4").ToArray();
        var bassNote = Assert.Single(scene.Notes, note => note.Label == "C3");
        double trebleLabelY = Assert.Single(trebleNotes.Select(note => note.LabelY).Distinct())!.Value;
        double trebleFingeringY = Assert.Single(trebleNotes.Select(note => note.FingeringY).Distinct())!.Value;
        var trebleLane = Assert.Single(
            scene.Bands,
            band => trebleLabelY >= band.Y0 && trebleLabelY <= band.Y1);
        var bassLane = Assert.Single(
            scene.Bands,
            band => bassNote.LabelY >= band.Y0 && bassNote.LabelY <= band.Y1);

        Assert.True(trebleLane.Y1 < trebleBottomLineY);
        Assert.True(bassLane.Y1 < bassBottomLineY);
        Assert.NotEqual(trebleLabelY, bassNote.LabelY);
        Assert.InRange(trebleFingeringY, trebleLane.Y0, trebleLane.Y1);
        Assert.InRange(bassNote.FingeringY!.Value, bassLane.Y0, bassLane.Y1);
    }

    [Fact]
    public void BuildScore_HighLeftHandNotes_UseTrebleNotationAndStackSimultaneousLabels()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.G, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                StemDirection: ScoreStemDirection.Down,
                Fingering: new ScoreFingering(3)),
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Bass,
                StemDirection: ScoreStemDirection.Up,
                Fingering: new ScoreFingering(4)),
            new(
                new Pitch(NoteLetter.D, 0, 4),
                new NoteValue(4),
                0,
                1,
                Staff.Bass,
                StemDirection: ScoreStemDirection.Up,
                Fingering: new ScoreFingering(3)),
            new(
                new Pitch(NoteLetter.E, 0, 4),
                new NoteValue(4),
                0,
                2,
                Staff.Bass,
                StemDirection: ScoreStemDirection.Up,
                Fingering: new ScoreFingering(2)),
            new(
                new Pitch(NoteLetter.F, 0, 4),
                new NoteValue(4),
                0,
                3,
                Staff.Bass,
                StemDirection: ScoreStemDirection.Up,
                Fingering: new ScoreFingering(1)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var annotationLane = Assert.Single(scene.Bands);
        GrandStaffNote[] leftHandNotes = scene.Notes.Where(note => note.Fingering?.StartsWith('L') == true).ToArray();
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double renderedStaffSpace = staffLines[1].Y0 - staffLines[0].Y0;
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        var expectedNoteYs = new Dictionary<string, double>
        {
            ["C4"] = trebleBottomLineY - renderedStaffSpace,
            ["D4"] = trebleBottomLineY - (renderedStaffSpace / 2),
            ["E4"] = trebleBottomLineY,
            ["F4"] = trebleBottomLineY + (renderedStaffSpace / 2),
        };
        Assert.All(
            leftHandNotes,
            note => Assert.Equal(expectedNoteYs[note.Label], note.Y, 6));

        double lowestTrebleNotationY = scene.Notes.Min(note =>
        {
            double noteHeadBottomY = note.Y - (renderedStaffSpace * 0.4);
            double stemEndY = note.HasStem
                ? note.StemEndY ?? note.Y + (note.StemDirection == StemDirection.Up
                    ? renderedStaffSpace * 3
                    : -(renderedStaffSpace * 3))
                : note.Y;
            return Math.Min(noteHeadBottomY, stemEndY);
        });
        double lowestLedgerY = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Ledger)
            .Min(line => line.Y0);
        lowestTrebleNotationY = Math.Min(lowestTrebleNotationY, lowestLedgerY);
        Assert.True(
            lowestTrebleNotationY - annotationLane.Y1 >= (renderedStaffSpace / 2) - 0.000001);

        Assert.Equal(2, scene.Notes.Select(note => note.LabelY).Distinct().Count());
        Assert.Single(scene.Notes.Select(note => note.FingeringY).Distinct());
        Assert.All(scene.Notes, note => Assert.InRange(note.LabelY!.Value, annotationLane.Y0, annotationLane.Y1));
        Assert.All(scene.Notes, note => Assert.InRange(note.FingeringY!.Value, annotationLane.Y0, annotationLane.Y1));
        Assert.All(
            leftHandNotes.Where(note => note.Label == "C4"),
            note => Assert.Contains(
                scene.Lines,
                line => line.Kind == GrandStaffLineKind.Ledger && line.X0 < note.X && line.X1 > note.X));
        Assert.All(
            leftHandNotes.Where(note => note.Label is "D4" or "E4" or "F4"),
            note => Assert.DoesNotContain(
                scene.Lines,
                line => line.Kind == GrandStaffLineKind.Ledger && line.X0 < note.X && line.X1 > note.X));
    }

    [Fact]
    public void BuildScore_VisibleNotes_AddOneInterStaffAnnotationBand()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var band = Assert.Single(scene.Bands);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        Assert.True(band.Y1 < trebleBottomLineY);
        Assert.True(band.Y0 > bassTopLineY);
    }

    [Fact]
    public void BuildScore_ShowNoteLabelsAndFingeringsFalse_HasNoBands()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            showNoteLabels: false,
            showFingerings: false);

        Assert.Empty(scene.Bands);
    }

    [Fact]
    public void BuildScore_NoteWithFingering_BandCoversBothLabelAndFingeringRows()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var withFingering = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Bands);
        var labelOnly = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false).Bands);

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);
        Assert.True(withFingering.Y0 <= renderedNote.FingeringY);
        Assert.True(withFingering.Y1 >= renderedNote.LabelY);
        Assert.True(withFingering.Y1 - withFingering.Y0 > labelOnly.Y1 - labelOnly.Y0);
    }

    [Fact]
    public void BuildScore_Band_SpansFullStaffWidth()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var band = Assert.Single(scene.Bands);
        var staffLine = scene.Lines.First(line => line.Kind == GrandStaffLineKind.Staff);
        Assert.Equal(staffLine.X0, band.X0, 6);
        Assert.Equal(staffLine.X1, band.X1, 6);
    }

    [Fact]
    public void BuildScore_KeyAndTimeSignature_ReturnsGlyphsOnBothStaves()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.C, 1, 3), new NoteValue(4), 0, 1, Staff.Bass),
        ];
        var score = ScoreWithNotes(notes, keyFifths: 2, timeSignature: new TimeSignature(6, new NoteValue(8)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var keySignatureGlyphs = scene.Glyphs
            .Where(glyph => glyph.Kind == GrandStaffGlyphKind.KeySignature)
            .ToArray();
        var timeSignatureGlyphs = scene.Glyphs
            .Where(glyph => glyph.Kind == GrandStaffGlyphKind.TimeSignature)
            .ToArray();
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double renderedStaffSpace = staffLines[1].Y0 - staffLines[0].Y0;
        double expectedKeySignatureHeight = 2 * renderedStaffSpace;
        double expectedTimeSignatureHeight = 2 * renderedStaffSpace;
        double expectedTimeSignatureX = keySignatureGlyphs.Max(glyph => glyph.X) + 0.08;

        Assert.Equal(4, keySignatureGlyphs.Length);
        Assert.All(keySignatureGlyphs, glyph =>
        {
            Assert.True(glyph.Height.HasValue);
            Assert.Equal(expectedKeySignatureHeight, glyph.Height.Value, precision: 6);
        });
        Assert.Equal(4, timeSignatureGlyphs.Length);
        Assert.All(timeSignatureGlyphs, glyph =>
        {
            Assert.True(glyph.Height.HasValue);
            Assert.Equal(expectedTimeSignatureHeight, glyph.Height.Value, precision: 6);
            Assert.Equal(expectedTimeSignatureX, glyph.X, precision: 6);
        });
        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
    }

    [Fact]
    public void BuildScore_ExplicitAccidentalInKeySignature_ReturnsCourtesyGlyph()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.F, 1, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Accidental: ScoreAccidental.Sharp);
        var score = SingleNoteScore(note, keyFifths: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        var accidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal("♯", accidental.Text);
        Assert.True(accidental.X < renderedNote.X);
        Assert.Equal(renderedNote.Y, accidental.Y);
    }

    [Theory]
    [InlineData(ScoreFermata.Upright, "𝄐", true)]
    [InlineData(ScoreFermata.Inverted, "𝄑", false)]
    public void BuildScore_Fermata_ReturnsOrientedGlyph(
        ScoreFermata fermata,
        string expectedGlyph,
        bool isAboveNote)
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fermata: fermata);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        var renderedFermata = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Fermata);
        Assert.Equal(expectedGlyph, renderedFermata.Text);
        Assert.Equal(renderedNote.X, renderedFermata.X);
        Assert.Equal(isAboveNote, renderedFermata.Y > renderedNote.Y);
    }

    [Theory]
    [InlineData(ScoreArticulation.Staccato, "●")]
    [InlineData(ScoreArticulation.Tenuto, "–")]
    [InlineData(ScoreArticulation.Accent, ">")]
    [InlineData(ScoreArticulation.Staccatissimo, "▾")]
    public void BuildScore_Articulation_ReturnsGlyphAboveNoteOutsideStem(
        ScoreArticulation articulation,
        string expectedGlyph)
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Articulation: articulation);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        var renderedGlyph = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Articulation);
        Assert.Equal(expectedGlyph, renderedGlyph.Text);
        Assert.Equal(renderedNote.X, renderedGlyph.X);
        Assert.True(renderedGlyph.Y > renderedNote.Y);
    }

    [Fact]
    public void BuildScore_TrillMarkOrnament_ReturnsTrGlyphAboveNote()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Ornament: ScoreOrnament.TrillMark);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        var renderedGlyph = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Ornament);
        Assert.Equal("tr", renderedGlyph.Text);
        Assert.Equal(renderedNote.X, renderedGlyph.X);
        Assert.True(renderedGlyph.Y > renderedNote.Y);
    }

    [Fact]
    public void BuildScore_AccidentalMark_RendersDistinctFromPrintedAccidental()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 1, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Accidental: ScoreAccidental.Sharp,
            AccidentalMark: ScoreAccidental.Natural);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var renderedNote = Assert.Single(scene.Notes);
        var printedAccidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        var accidentalMark = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.AccidentalMark);
        Assert.Equal("♯", printedAccidental.Text);
        Assert.Equal("♮", accidentalMark.Text);
        Assert.True(accidentalMark.X.Equals(renderedNote.X));
        Assert.True(accidentalMark.Y > renderedNote.Y);
        Assert.NotEqual(printedAccidental.Y, accidentalMark.Y);
    }

    [Fact]
    public void BuildScore_NoteWithoutNewNotations_RendersNoArticulationOrnamentOrAccidentalMarkGlyphs()
    {
        var note = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Articulation);
        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Ornament);
        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.AccidentalMark);
    }

    [Fact]
    public void BuildScore_SlurSpanningThreeNotes_ReturnsOneArcFromFirstToLastNote()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                Slur: new ScoreSlur(IsStart: true, Number: 1)),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0.5, Staff.Treble),
            new(
                new Pitch(NoteLetter.E, 0, 4),
                new NoteValue(8),
                0,
                1,
                Staff.Treble,
                Slur: new ScoreSlur(IsStart: false, Number: 1)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var slur = Assert.Single(scene.Slurs);
        Assert.Equal(scene.Notes[0].X, slur.X0);
        Assert.Equal(scene.Notes[0].Y, slur.Y0);
        Assert.Equal(scene.Notes[2].X, slur.X1);
        Assert.Equal(scene.Notes[2].Y, slur.Y1);
    }

    [Fact]
    public void BuildScore_OverlappingSlursByNumber_MatchesEachIndependently()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                Slur: new ScoreSlur(true, 1)),
            new(
                new Pitch(NoteLetter.D, 0, 4),
                new NoteValue(8),
                0,
                0.5,
                Staff.Treble,
                Slur: new ScoreSlur(true, 2)),
            new(
                new Pitch(NoteLetter.E, 0, 4),
                new NoteValue(8),
                0,
                1,
                Staff.Treble,
                Slur: new ScoreSlur(false, 1)),
            new(
                new Pitch(NoteLetter.F, 0, 4),
                new NoteValue(8),
                0,
                1.5,
                Staff.Treble,
                Slur: new ScoreSlur(false, 2)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Slurs.Count);
        var firstSlur = Assert.Single(scene.Slurs, slur => slur.X0 == scene.Notes[0].X);
        Assert.Equal(scene.Notes[2].X, firstSlur.X1);
        var secondSlur = Assert.Single(scene.Slurs, slur => slur.X0 == scene.Notes[1].X);
        Assert.Equal(scene.Notes[3].X, secondSlur.X1);
    }

    [Fact]
    public void BuildScore_NoteWithTieAndSlur_StillRendersSlurCorrectly()
    {
        // The printed grand-staff view does not currently draw a curve for TiesToNext at all (that
        // flag is only used for duration merging and the separate live piano-roll's own tie
        // rendering) — this test proves slur rendering is unaffected by a note also carrying tie
        // data, not that a tie curve and a slur curve visually coexist (there is no tie curve here
        // to coexist with).
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                TiesToNext: true,
                Slur: new ScoreSlur(true, 1)),
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0.5,
                Staff.Treble,
                Slur: new ScoreSlur(false, 1)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var slur = Assert.Single(scene.Slurs);
        Assert.Equal(scene.Notes[0].X, slur.X0);
        Assert.Equal(scene.Notes[1].X, slur.X1);
    }

    [Fact]
    public void BuildScore_UnmatchedSlurStart_RendersNoSlur()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Slur: new ScoreSlur(IsStart: true, Number: 1));
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Empty(scene.Slurs);
    }

    [Fact]
    public void BuildScore_ChordWithArpeggiateMark_ReturnsOneMarkSpanningChordExtent()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                Arpeggio: ScoreArpeggio.Arpeggiate),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
            new(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var mark = Assert.Single(scene.ArpeggioMarks);
        Assert.False(mark.IsNonArpeggiate);
        Assert.True(mark.X < scene.Notes.Min(note => note.X));
        Assert.Equal(scene.Notes.Min(note => note.Y), Math.Min(mark.Y0, mark.Y1));
        Assert.Equal(scene.Notes.Max(note => note.Y), Math.Max(mark.Y0, mark.Y1));
    }

    [Fact]
    public void BuildScore_ChordWithNonArpeggiateMark_ReturnsDistinguishableBracketVariant()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(4),
                0,
                0,
                Staff.Treble,
                Arpeggio: ScoreArpeggio.NonArpeggiate),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var mark = Assert.Single(scene.ArpeggioMarks);
        Assert.True(mark.IsNonArpeggiate);
    }

    [Fact]
    public void BuildScore_ChordWithoutArpeggioMark_RendersNoMark()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, IsChordContinuation: true),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Empty(scene.ArpeggioMarks);
    }

    [Fact]
    public void BuildScore_NonChordNoteWithArpeggioMark_RendersNoMarkAndDoesNotThrow()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Arpeggio: ScoreArpeggio.Arpeggiate);
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Empty(scene.ArpeggioMarks);
    }

    [Theory]
    [InlineData(ScoreGlissandoKind.Glissando)]
    [InlineData(ScoreGlissandoKind.Slide)]
    public void BuildScore_GlissandoOrSlideStartAndStop_ReturnsOneLineConnectingBothNoteheads(
        ScoreGlissandoKind kind)
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                Glissando: new ScoreGlissando(true, 1, kind)),
            new(
                new Pitch(NoteLetter.G, 0, 4),
                new NoteValue(8),
                0,
                0.5,
                Staff.Treble,
                Glissando: new ScoreGlissando(false, 1, kind)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var line = Assert.Single(scene.Lines, candidate => candidate.Kind == GrandStaffLineKind.Glissando);
        Assert.Equal(scene.Notes[0].X, line.X0);
        Assert.Equal(scene.Notes[0].Y, line.Y0);
        Assert.Equal(scene.Notes[1].X, line.X1);
        Assert.Equal(scene.Notes[1].Y, line.Y1);
    }

    [Fact]
    public void BuildScore_OverlappingGlissandiByNumber_MatchesEachIndependently()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.C, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                Glissando: new ScoreGlissando(true, 1, ScoreGlissandoKind.Glissando)),
            new(
                new Pitch(NoteLetter.D, 0, 4),
                new NoteValue(8),
                0,
                0.5,
                Staff.Treble,
                Glissando: new ScoreGlissando(true, 2, ScoreGlissandoKind.Glissando)),
            new(
                new Pitch(NoteLetter.E, 0, 4),
                new NoteValue(8),
                0,
                1,
                Staff.Treble,
                Glissando: new ScoreGlissando(false, 1, ScoreGlissandoKind.Glissando)),
            new(
                new Pitch(NoteLetter.F, 0, 4),
                new NoteValue(8),
                0,
                1.5,
                Staff.Treble,
                Glissando: new ScoreGlissando(false, 2, ScoreGlissandoKind.Glissando)),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var glissandoLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Glissando).ToArray();
        Assert.Equal(2, glissandoLines.Length);
        var firstLine = Assert.Single(glissandoLines, line => line.X0 == scene.Notes[0].X);
        Assert.Equal(scene.Notes[2].X, firstLine.X1);
        var secondLine = Assert.Single(glissandoLines, line => line.X0 == scene.Notes[1].X);
        Assert.Equal(scene.Notes[3].X, secondLine.X1);
    }

    [Fact]
    public void BuildScore_UnmatchedGlissandoStart_RendersNoLine()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Glissando: new ScoreGlissando(IsStart: true, Number: 1, ScoreGlissandoKind.Glissando));
        var score = SingleNoteScore(note);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.DoesNotContain(scene.Lines, line => line.Kind == GrandStaffLineKind.Glissando);
    }

    [Fact]
    public void BuildScore_OctaveShift8VaFixture_ReturnsBracketAboveShiftedNotes()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "octave-shift-8va.musicxml");
        var score = new MusicXmlScoreReader().Read(fixture);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffLine[] lines = scene.Lines
            .Where(candidate => candidate.Kind == GrandStaffLineKind.OctaveShift)
            .OrderBy(candidate => candidate.X0)
            .ToArray();
        Assert.Equal(2, lines.Length);
        GrandStaffLine firstSide = lines[0];
        GrandStaffLine secondSide = lines[1];
        var numeral = Assert.Single(
            scene.Glyphs,
            glyph => glyph.Kind == GrandStaffGlyphKind.OctaveShiftNumeral);
        GrandStaffNote firstShiftedNote = Assert.Single(scene.Notes, note => note.Label == "D5");
        GrandStaffNote lastShiftedNote = Assert.Single(scene.Notes, note => note.Label == "F5");
        double[] trebleStaffLineYs = scene.Lines
            .Where(candidate => candidate.Kind == GrandStaffLineKind.Staff)
            .Take(5)
            .Select(candidate => candidate.Y0)
            .Order()
            .ToArray();
        double trebleTopLineY = trebleStaffLineYs[^1];
        double staffSpace = trebleStaffLineYs[1] - trebleStaffLineYs[0];

        Assert.Equal(firstShiftedNote.X, firstSide.X0);
        Assert.Equal(lastShiftedNote.X, secondSide.X1);
        Assert.Equal((firstShiftedNote.X + lastShiftedNote.X) / 2d, firstSide.X1, precision: 10);
        Assert.Equal(firstSide.X1, secondSide.X0);
        Assert.Equal(firstSide.Y0, secondSide.Y1);
        Assert.Equal(firstSide.Y1, secondSide.Y0);
        Assert.True(firstSide.Y1 > firstSide.Y0);
        Assert.True(firstSide.Y0 >= trebleTopLineY + (2.25 * staffSpace));
        Assert.True(firstSide.Y1 < 1);
        Assert.Equal("8", numeral.Text);
        Assert.Equal(firstSide.X0, numeral.X);
        Assert.Equal(firstSide.Y0, numeral.Y);
    }

    [Fact]
    public void BuildScore_OctaveShift8VbFixture_ReturnsBracketBelowShiftedNotes()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "octave-shift-8vb.musicxml");
        var score = new MusicXmlScoreReader().Read(fixture);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        GrandStaffLine[] lines = scene.Lines
            .Where(candidate => candidate.Kind == GrandStaffLineKind.OctaveShift)
            .OrderBy(candidate => candidate.X0)
            .ToArray();
        Assert.Equal(2, lines.Length);
        GrandStaffLine firstSide = lines[0];
        GrandStaffLine secondSide = lines[1];
        var numeral = Assert.Single(
            scene.Glyphs,
            glyph => glyph.Kind == GrandStaffGlyphKind.OctaveShiftNumeral);
        GrandStaffNote firstShiftedNote = Assert.Single(scene.Notes, note => note.Label == "B2");
        GrandStaffNote lastShiftedNote = Assert.Single(scene.Notes, note => note.Label == "G2");
        double[] bassStaffLineYs = scene.Lines
            .Where(candidate => candidate.Kind == GrandStaffLineKind.Staff)
            .Skip(5)
            .Select(candidate => candidate.Y0)
            .Order()
            .ToArray();
        double bassBottomLineY = bassStaffLineYs[0];
        double staffSpace = bassStaffLineYs[1] - bassStaffLineYs[0];

        Assert.Equal(firstShiftedNote.X, firstSide.X0);
        Assert.Equal(lastShiftedNote.X, secondSide.X1);
        Assert.Equal((firstShiftedNote.X + lastShiftedNote.X) / 2d, firstSide.X1, precision: 10);
        Assert.Equal(firstSide.X1, secondSide.X0);
        Assert.Equal(firstSide.Y0, secondSide.Y1);
        Assert.Equal(firstSide.Y1, secondSide.Y0);
        Assert.True(firstSide.Y1 < firstSide.Y0);
        Assert.True(firstSide.Y0 <= bassBottomLineY - (2.25 * staffSpace));
        Assert.Equal("8", numeral.Text);
        Assert.Equal(firstSide.X0, numeral.X);
        Assert.Equal(firstSide.Y0, numeral.Y);
        double bassLabelY = Assert.IsType<double>(firstShiftedNote.LabelY);
        GrandStaffBand bassAnnotationBand = Assert.Single(
            scene.Bands,
            band => bassLabelY >= band.Y0 && bassLabelY <= band.Y1);
        Assert.True(firstSide.Y1 > bassAnnotationBand.Y1);
    }

    [Fact]
    public void BuildScore_NotesAtMeasureStarts_PositionsAfterBarlines()
    {
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            2,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble)],
                    []),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 1, 5), new NoteValue(8), 1, 0, Staff.Treble)],
                    []),
            ]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var barlineXs = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline)
            .Select(line => line.X0)
            .ToArray();
        double finalBoundaryX = GrandStaffLayout.GetScoreBarlineXs(0, score.Measures.Count)[^1];
        Assert.Equal(2, scene.Notes.Count);
        Assert.All(scene.Notes, note =>
        {
            double precedingBarlineX = barlineXs.Where(x => x <= note.X).Max();
            Assert.Equal(0.02, note.X - precedingBarlineX, precision: 6);
        });
        Assert.Contains(barlineXs, x => Math.Abs(x - finalBoundaryX) < 1e-6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void BuildScore_ConsecutiveEighthNotes_HaveEvenSpacingAndAlignedCursor(int measureIndex)
    {
        var score = new Score(
            "test",
            new TimeSignature(3, new NoteValue(4)),
            new Tempo(120),
            0,
            Enumerable.Range(0, 5)
                .Select(index => new ScoreMeasure(
                    index == measureIndex
                        ? Enumerable.Range(0, 6)
                            .Select(noteIndex => new ScoreNote(
                                new Pitch(NoteLetter.D, 0, 4),
                                new NoteValue(8),
                                index,
                                noteIndex * 0.5,
                                Staff.Treble))
                            .ToArray()
                        : [],
                    []))
                .ToArray());

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            cursorBeats: (measureIndex * 3) + 0.5);
        double[] noteXs = scene.Notes.Select(note => note.X).ToArray();
        var cursor = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);

        Assert.Equal(6, noteXs.Length);
        Assert.Equal(noteXs[1], cursor.X0, 6);
        for (int index = 2; index < noteXs.Length; index++)
        {
            Assert.Equal(noteXs[1] - noteXs[0], noteXs[index] - noteXs[index - 1], precision: 6);
        }
    }

    [Fact]
    public void BuildScore_BeamEndingNearMeasureBoundary_KeepsNoteAndBeamInsideMeasure()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(8), 0, 0.5, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(8), 0, 1, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(8), 0, 1.5, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 2, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 2.5, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 3, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 3.5, Staff.Treble, BeamState: BeamState.End),
        ];
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, []), new ScoreMeasure([], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var beam = Assert.Single(scene.Beams);
        double followingBarlineX = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline && line.X0 > beam.X0)
            .Min(line => line.X0);
        var lastNote = scene.Notes.MaxBy(note => note.X)!;
        Assert.True(followingBarlineX - lastNote.X >= 0.02);
        Assert.True(followingBarlineX - beam.X1 >= 0.02);
    }

    [Fact]
    public void BuildScore_WithoutKeySignature_PreservesTimeSignatureClearanceBeforeOpeningBarline()
    {
        var score = CreateScore(measureCount: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        double timeSignatureX = scene.Glyphs
            .First(glyph => glyph.Kind == GrandStaffGlyphKind.TimeSignature)
            .X;
        double openingBarlineX = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline && line.X0 < GrandStaffLayout.ScoreX0)
            .Max(line => line.X0);
        Assert.Equal(0.03, openingBarlineX - timeSignatureX, precision: 6);
    }

    [Fact]
    public void BuildScore_PrimaryBeamGroup_ReturnsConnectedBeamWithoutIndividualFlags()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 1, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8), 0, 2, Staff.Treble, BeamState: BeamState.End),
        ];
        var score = ScoreWithNotes(notes, keyFifths: 2, timeSignature: new TimeSignature(6, new NoteValue(8)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var beam = Assert.Single(scene.Beams);
        Assert.Equal(1, beam.Count);
        Assert.Equal(StemDirection.Up, beam.StemDirection);
        Assert.True(beam.X1 > beam.X0);
        Assert.All(scene.Notes, note => Assert.Equal(0, note.FlagCount));
        Assert.All(scene.Notes, note => Assert.NotNull(note.StemEndY));
        Assert.All(scene.Notes, note => Assert.Equal(StemDirection.Up, note.StemDirection));
    }

    [Fact]
    public void BuildScore_BeamedEighthNoteTriplet_ReturnsOneCenteredTupletNumeral()
    {
        var tripletValue = new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2);
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), tripletValue, 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.E, 0, 4), tripletValue, 0, 1.0 / 3, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.F, 0, 4), tripletValue, 0, 2.0 / 3, Staff.Treble, BeamState: BeamState.End),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var beam = Assert.Single(scene.Beams);
        var tuplet = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Tuplet);
        Assert.Equal("3", tuplet.Text);
        Assert.Equal((beam.X0 + beam.X1) / 2, tuplet.X, precision: 6);
        Assert.True(tuplet.Y > Math.Max(beam.Y0, beam.Y1));
    }

    [Fact]
    public void BuildScore_EighthNoteTriplet_OccupiesSameXSpanAsTwoPlainBeats()
    {
        var tripletValue = new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2);
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.D, 0, 4), tripletValue, 0, 1, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.E, 0, 4), tripletValue, 0, 4.0 / 3, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.F, 0, 4), tripletValue, 0, 5.0 / 3, Staff.Treble, BeamState: BeamState.End),
            new(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 0, 3, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        double XOf(NoteLetter letter) => scene.Notes.Single(note => note.Label.StartsWith(letter.ToString())).X;
        double tripletGroupSpan = XOf(NoteLetter.G) - XOf(NoteLetter.C);
        double onePlainBeatSpan = XOf(NoteLetter.A) - XOf(NoteLetter.G);

        // C→G covers a quarter (1 beat) plus the whole triplet group (1 beat, ratio-adjusted) = 2
        // beats; G→A covers exactly 1 plain beat. Rendered X must reflect that 2:1 ratio directly,
        // not just trust MusicalTime's beat math transitively (see lessons.md's axis-trap history
        // in this file).
        Assert.Equal(2 * onePlainBeatSpan, tripletGroupSpan, precision: 3);
    }

    [Fact]
    public void BuildScore_BeamedNonTupletNotes_ReturnsNoTupletNumeral()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.F, 1, 4), new NoteValue(8), 0, 1, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8), 0, 2, Staff.Treble, BeamState: BeamState.End),
        ];
        var score = ScoreWithNotes(notes, keyFifths: 2, timeSignature: new TimeSignature(6, new NoteValue(8)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Tuplet);
    }

    [Fact]
    public void BuildScore_BeamGroupWithExplicitDirection_UsesDirectionForBeamAndStems()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(
                new Pitch(NoteLetter.F, 0, 4),
                new NoteValue(8),
                0,
                1,
                Staff.Treble,
                BeamState: BeamState.Continue,
                StemDirection: ScoreStemDirection.Down),
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(8), 0, 2, Staff.Treble, BeamState: BeamState.End),
        ];
        var score = ScoreWithNotes(notes, timeSignature: new TimeSignature(6, new NoteValue(8)));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var beam = Assert.Single(scene.Beams);
        Assert.Equal(StemDirection.Down, beam.StemDirection);
        Assert.True(beam.Y0 < scene.Notes[0].Y);
        Assert.True(beam.Y1 < scene.Notes[^1].Y);
        Assert.All(scene.Notes, note =>
        {
            Assert.Equal(StemDirection.Down, note.StemDirection);
            Assert.NotNull(note.StemEndY);
            Assert.True(note.StemEndY.Value < note.Y);
        });
        Assert.All(scene.Notes, note => Assert.Equal(0, note.FlagCount));
    }

    [Fact]
    public void BuildScore_BeamGroupWithConflictingDirections_UsesAutomaticDirection()
    {
        ScoreNote[] notes =
        [
            new(
                new Pitch(NoteLetter.D, 0, 4),
                new NoteValue(8),
                0,
                0,
                Staff.Treble,
                BeamState: BeamState.Begin,
                StemDirection: ScoreStemDirection.Up),
            new(
                new Pitch(NoteLetter.F, 0, 4),
                new NoteValue(8),
                0,
                1,
                Staff.Treble,
                BeamState: BeamState.End,
                StemDirection: ScoreStemDirection.Down),
        ];
        var score = ScoreWithNotes(notes);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(StemDirection.Up, Assert.Single(scene.Beams).StemDirection);
        Assert.All(scene.Notes, note => Assert.Equal(StemDirection.Up, note.StemDirection));
    }

    [Fact]
    public void BuildScore_CustomVisibleMeasureCount_ProducesBarlinesConsistentWithConfiguredWindow()
    {
        var score = CreateScore(measureCount: 6);

        var defaultScene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var narrowedScene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);

        // A 6-measure score shows all 5 of the default window's internal/leading barlines, but
        // only 2 of a 2-measure window's — fewer measures fit per page, so fewer barlines are
        // drawn for the same underlying score, directly reflecting the configured window size.
        int defaultBarlineCount = defaultScene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline);
        int narrowedBarlineCount = narrowedScene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline);
        Assert.True(
            narrowedBarlineCount < defaultBarlineCount,
            $"Expected fewer barlines in a 2-measure window than the default (default: {defaultBarlineCount}, narrowed: {narrowedBarlineCount}).");

        var expectedBarlineXs = GrandStaffLayout.GetScoreBarlineXs(
            firstVisibleMeasure: 0,
            score.Measures.Count,
            visibleMeasureCount: 2);
        Assert.Equal(2, expectedBarlineXs.Count(x => x < GrandStaffLayout.ScoreX1));
    }

    [Fact]
    public void BuildScore_LowerVisibleMeasureCount_WidensNoteSpacingWithinMeasure()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
        ];
        var score = ScoreWithNotes(notes);

        var defaultScene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var narrowedScene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);

        double defaultDistance = Math.Abs(defaultScene.Notes[1].X - defaultScene.Notes[0].X);
        double narrowedDistance = Math.Abs(narrowedScene.Notes[1].X - narrowedScene.Notes[0].X);
        Assert.True(narrowedDistance > defaultDistance);
    }

    [Fact]
    public void BuildScore_CursorAtWindowBoundary_BelongsOnlyToIncomingWindow()
    {
        var score = CreateScore(measureCount: 12);
        double boundaryBeats = GrandStaffLayout.DefaultVisibleMeasureCount * score.TimeSignature.Numerator;

        var outgoingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            cursorBeats: boundaryBeats);
        var incomingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: GrandStaffLayout.DefaultVisibleMeasureCount,
            cursorBeats: boundaryBeats);

        Assert.DoesNotContain(outgoingScene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        var incomingCursor = Assert.Single(
            incomingScene.Lines,
            line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(GrandStaffLayout.ScoreX0, incomingCursor.X0, 6);
    }

    [Fact]
    public void BuildScore_VisibleVerdict_ReturnsVerdictOnSourceNote()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: new Dictionary<ScoreNote, Verdict> { [sourceNote] = Verdict.Correct });

        Assert.Equal(Verdict.Correct, Assert.Single(scene.Notes).Verdict);
    }

    [Fact]
    public void BuildScore_ReviewMarks_CarryTheMarkOnTheSourceNoteWithoutTouchingItsVerdict()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: new Dictionary<ScoreNote, Verdict> { [sourceNote] = Verdict.Late },
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Timing });

        var note = Assert.Single(scene.Notes);
        Assert.Equal(ReviewMark.Timing, note.ReviewMark);
        Assert.Equal(Verdict.Late, note.Verdict);
    }

    [Fact]
    public void BuildScore_WithoutReviewMarks_NotesCarryNoMarkAndNoGroup()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(SingleNoteScore(sourceNote), firstVisibleMeasure: 0);

        var note = Assert.Single(scene.Notes);
        Assert.Null(note.ReviewMark);
        Assert.Null(note.ReviewMarkGroup);
    }

    [Fact]
    public void BuildScore_ReviewMarks_ANoteWithoutAMarkStaysUnmarked()
    {
        var marked = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var unmarked = new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 1, Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([marked, unmarked]),
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [marked] = ReviewMark.Pitch });

        Assert.Equal(ReviewMark.Pitch, scene.Notes[0].ReviewMark);
        Assert.Null(scene.Notes[1].ReviewMark);
        Assert.Null(scene.Notes[1].ReviewMarkGroup);
    }

    [Fact]
    public void BuildScore_ReviewMarks_ChordMembersOnOneStaffShareOneGroupAndOtherOnsetsDoNot()
    {
        var chord = new[]
        {
            new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
        };
        var next = new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 1, Staff.Treble);
        var marks = chord.Append(next).ToDictionary(note => note, _ => ReviewMark.Missed);

        var scene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([.. chord, next]),
            firstVisibleMeasure: 0,
            reviewMarks: marks);

        int?[] groups = scene.Notes.Select(note => note.ReviewMarkGroup).ToArray();
        Assert.All(groups, group => Assert.NotNull(group));
        Assert.Single(groups.Take(3).Distinct());
        Assert.NotEqual(groups[0], groups[3]);
    }

    [Fact]
    public void BuildScore_ReviewMarks_AHandsTogetherPromptOnTwoStavesGetsOneGroupPerStaff()
    {
        var treble = new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var bass = new ScoreNote(new Pitch(NoteLetter.E, 0, 3), new NoteValue(4), 0, 0, Staff.Bass);

        var scene = GrandStaffSceneBuilder.BuildScore(
            ScoreWithNotes([treble, bass]),
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark>
            {
                [treble] = ReviewMark.Pitch,
                [bass] = ReviewMark.Pitch,
            });

        Assert.Equal(2, scene.Notes.Select(note => note.ReviewMarkGroup).Distinct().Count());
    }

    [Fact]
    public void BuildScore_ReviewMarks_DoNotChangeAnyGeometry()
    {
        var notes = new[]
        {
            new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(8), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0.5, Staff.Treble, BeamState: BeamState.End),
            new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 1, Staff.Treble),
            new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 1, Staff.Treble),
        };
        var score = ScoreWithNotes(notes);
        var marks = notes.ToDictionary(note => note, _ => ReviewMark.Pitch);

        var plain = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var marked = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, reviewMarks: marks);

        Assert.Equal(plain.Lines, marked.Lines);
        Assert.Equal(plain.Glyphs, marked.Glyphs);
        Assert.Equal(plain.Beams, marked.Beams);
        Assert.Equal(plain.Bands, marked.Bands);
        Assert.Equal(plain.Ties, marked.Ties);
        Assert.Equal(
            plain.Notes,
            marked.Notes.Select(note => note with { ReviewMark = null, ReviewMarkGroup = null }));
    }

    [Fact]
    public void BuildScore_ReviewMarks_ForNotesOutsideTheVisibleWindowAreIgnored()
    {
        var visible = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var hidden = new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 1, 0, Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(
            TwoMeasureScore(visible, hidden),
            firstVisibleMeasure: 0,
            visibleMeasureCount: 1,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark>
            {
                [visible] = ReviewMark.Timing,
                [hidden] = ReviewMark.Pitch,
            });

        Assert.Equal(ReviewMark.Timing, Assert.Single(scene.Notes).ReviewMark);
    }

    [Fact]
    public void BuildScore_ExpectedNote_ReturnsActiveScoreNote()
    {
        var expectedNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble);
        var score = SingleNoteScore(expectedNote);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            expectedNotes: new HashSet<ScoreNote> { expectedNote });

        Assert.True(Assert.Single(scene.Notes).IsActive);
    }

    [Fact]
    public void BuildScore_PerformedInput_OverlaysOnlyHeldNoteAtCursor()
    {
        var score = ScoreWithNotes([]);
        var timeline = new NoteTimeline();
        var heldNote = timeline.Start(new Pitch(NoteLetter.C, 0, 4), TimeSpan.FromSeconds(1));
        var releasedNote = timeline.Start(new Pitch(NoteLetter.D, 0, 4), TimeSpan.FromSeconds(1));
        timeline.Complete(releasedNote, TimeSpan.FromSeconds(1.5));

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            performedNotes: [heldNote, releasedNote],
            performedNoteBeats: 1);

        var indicator = Assert.Single(scene.Notes);
        Assert.DoesNotContain(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal("C4", indicator.Label);
        Assert.True(indicator.IsActive);
        Assert.False(indicator.IsFilled);
        var cursorScene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 1);
        var cursor = Assert.Single(cursorScene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(cursor.X0, indicator.X, 6);
        GrandStaffLine[] trebleStaffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .Take(5)
            .ToArray();
        double renderedStaffSpace = trebleStaffLines[1].Y0 - trebleStaffLines[0].Y0;
        Assert.Equal(trebleStaffLines[0].Y0 - renderedStaffSpace, indicator.Y, 6);
    }

    [Fact]
    public void BuildScore_HeldNoteUnderOwnStaff_LabelClearsTheNote()
    {
        var score = ScoreWithNotes([]);
        var timeline = new NoteTimeline();
        var heldNote = timeline.Start(new Pitch(NoteLetter.C, 0, 4), TimeSpan.FromSeconds(1));

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            performedNotes: [heldNote],
            performedNoteBeats: 1);

        var indicator = Assert.Single(scene.Notes);
        Assert.NotNull(indicator.LabelY);
        Assert.True(indicator.LabelY < indicator.Y);
    }

    [Fact]
    public void BuildScore_PerformedInput_ShowNoteLabelsFalse_HidesHeldNoteLabel()
    {
        var score = ScoreWithNotes([]);
        var timeline = new NoteTimeline();
        var heldNote = timeline.Start(new Pitch(NoteLetter.C, 0, 4), TimeSpan.FromSeconds(1));

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            performedNotes: [heldNote],
            performedNoteBeats: 1,
            showNoteLabels: false);

        Assert.Null(Assert.Single(scene.Notes).LabelY);
    }

    [Fact]
    public void BuildScore_HeldNoteAtWindowBoundary_BelongsOnlyToIncomingWindow()
    {
        var score = CreateScore(measureCount: 12);
        var heldNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        double boundaryBeats = GrandStaffLayout.DefaultVisibleMeasureCount * score.TimeSignature.Numerator;

        var outgoingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            performedNotes: [heldNote],
            performedNoteBeats: boundaryBeats);
        var incomingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: GrandStaffLayout.DefaultVisibleMeasureCount,
            performedNotes: [heldNote],
            performedNoteBeats: boundaryBeats);

        Assert.Empty(outgoingScene.Notes);
        Assert.True(Assert.Single(incomingScene.Notes).IsActive);
    }

    [Fact]
    public void Build_EmptyTimeline_ReturnsGrandStaffWithoutNotes()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        Assert.Empty(scene.Notes);
        Assert.Equal(10, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Staff));
        Assert.Equal(2, scene.Glyphs.Count(glyph => glyph.Kind == GrandStaffGlyphKind.Clef));
    }

    [Fact]
    public void Build_EmptyTimeline_ReturnsLiveMeasureGridAndCursor()
    {
        var scene = GrandStaffSceneBuilder.Build(
            [],
            TimeSpan.Zero,
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120));

        Assert.Equal(8, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline));
        Assert.Equal(15, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Beat));
        var cursor = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(GrandStaffLayout.ScoreX0, cursor.X0, 6);
    }

    [Fact]
    public void Build_EmptyTimeline_AddsStartingBarlineAndEndingDoubleBarline()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var staffLine = scene.Lines.First(line => line.Kind == GrandStaffLineKind.Staff);
        var barlines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Barline).ToArray();
        Assert.Single(barlines, line => line.X0 == staffLine.X0);
        Assert.Equal(2, barlines.Count(line => line.X0 > staffLine.X1 - 0.02 && line.X0 <= staffLine.X1));
    }

    [Fact]
    public void Build_LiveGrandStaff_RequestsClefAwareNoteClipping()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero, selectedOctave: 4);

        Assert.True(scene.ShouldClipNotesAtClefs);
    }

    [Fact]
    public void FitToSelectedOctave_TieCoordinates_TransformsYAndPreservesX()
    {
        var tie = new GrandStaffTie(-0.4, 5, 0.4, 5, StemDirection.Down, IsActive: true);
        var scene = new GrandStaffScene(
            [new GrandStaffLine(-1, -0.5, 1, 0.5, GrandStaffLineKind.Staff)],
            [],
            [])
        {
            Ties = [tie],
        };

        var fittedScene = GrandStaffSceneBuilder.FitToSelectedOctave(scene, selectedOctave: 4);

        var fittedTie = Assert.Single(fittedScene.Ties);
        Assert.Equal(tie.X0, fittedTie.X0);
        Assert.Equal(tie.X1, fittedTie.X1);
        Assert.NotEqual(tie.Y0, fittedTie.Y0);
        Assert.InRange(fittedTie.Y0, -1, 1);
        Assert.InRange(fittedTie.Y1, -1, 1);
    }

    [Fact]
    public void FitToSelectedOctave_ContentThatFitsAtScoreScale_IsNotEnlarged()
    {
        var scene = new GrandStaffScene(
            [
                new GrandStaffLine(-1, 0.1, 1, 0.1, GrandStaffLineKind.Staff),
                new GrandStaffLine(-1, 0.2, 1, 0.2, GrandStaffLineKind.Staff),
            ],
            [],
            []);

        var fittedScene = GrandStaffSceneBuilder.FitToSelectedOctave(scene, selectedOctave: 4);

        Assert.Equal(0.1, fittedScene.Lines[1].Y0 - fittedScene.Lines[0].Y0, 9);
    }

    [Fact]
    public void Build_ScenesWithoutTieInput_ReturnEmptyTieCollections()
    {
        var liveScene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);
        var scoreScene = GrandStaffSceneBuilder.BuildScore(CreateScore(measureCount: 1), firstVisibleMeasure: 0);

        Assert.Empty(liveScene.Ties);
        Assert.Empty(scoreScene.Ties);
    }

    [Fact]
    public void Build_EmptyTimeline_PositionsBassClefForFLineAlignment()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var bassClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄢");
        var bassLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Skip(5).ToArray();
        double expectedY = (bassLines[2].Y0 + bassLines[3].Y0) / 2;
        Assert.Equal(expectedY, bassClef.Y, 6);
    }

    [Fact]
    public void Build_EmptyTimeline_KeepsNotationGapBetweenStaves()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double renderedStaffSpace = staffLines[1].Y0 - staffLines[0].Y0;
        Assert.Equal(renderedStaffSpace * 8, staffLines[0].Y0 - staffLines[^1].Y0, 6);
    }

    [Fact]
    public void Build_EmptyTimeline_PositionsTrebleClefForGLineAlignment()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var trebleClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄞");
        var trebleLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).Take(5).ToArray();
        Assert.Equal(trebleLines[2].Y0, trebleClef.Y, 6);
    }

    [Fact]
    public void Build_EmptyTimeline_SizesClefsFromTheirStaffSpaces()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero, selectedOctave: 4);

        var trebleClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄞");
        var bassClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄢");
        double trebleStaffSpace = Math.Abs(scene.Lines[1].Y0 - scene.Lines[0].Y0);
        double bassStaffSpace = Math.Abs(scene.Lines[6].Y0 - scene.Lines[5].Y0);
        Assert.NotNull(trebleClef.Height);
        Assert.NotNull(bassClef.Height);
        Assert.Equal(trebleStaffSpace * 7, trebleClef.Height.Value, 6);
        Assert.Equal(bassStaffSpace * 3, bassClef.Height.Value, 6);
    }

    [Fact]
    public void Build_MiddleC_ReturnsTrebleNoteAndLedgerLine()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2));

        var renderedNote = Assert.Single(scene.Notes);
        GrandStaffLine[] trebleStaffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .Take(5)
            .ToArray();
        double renderedStaffSpace = trebleStaffLines[1].Y0 - trebleStaffLines[0].Y0;
        Assert.Equal(trebleStaffLines[0].Y0 - renderedStaffSpace, renderedNote.Y, 6);
        var ledgerLine = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Ledger);
        Assert.Equal(renderedNote.Y, ledgerLine.Y0, 6);
        Assert.Equal(0.13, ledgerLine.X1 - ledgerLine.X0, 6);
    }

    [Fact]
    public void Build_MiddleC_LabelClearsTheNoteOnItsLedgerLine()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2), selectedOctave: null);

        var renderedNote = Assert.Single(scene.Notes);
        Assert.NotNull(renderedNote.LabelY);
        Assert.True(renderedNote.LabelY < renderedNote.Y);
    }

    [Fact]
    public void Build_ShowNoteLabelsFalse_HidesLabel()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build(
            [note],
            TimeSpan.FromSeconds(2),
            selectedOctave: null,
            showNoteLabels: false);

        Assert.Null(Assert.Single(scene.Notes).LabelY);
    }

    [Fact]
    public void Build_MiddleC_AddsBandForTrebleStaff()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2), selectedOctave: null);

        Assert.Single(scene.Bands);
    }

    [Fact]
    public void Build_WithSelectedOctave_FitsBandWithinView()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2), selectedOctave: 4);

        var band = Assert.Single(scene.Bands);
        Assert.True(band.Y0 < band.Y1);
        Assert.InRange(band.Y0, -1, 1);
        Assert.InRange(band.Y1, -1, 1);
    }

    [Fact]
    public void Build_AccidentalNote_ReturnsAccidentalBesideNote()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.F, 1, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2));

        var renderedNote = Assert.Single(scene.Notes);
        var accidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal("♯", accidental.Text);
        Assert.True(accidental.X < renderedNote.X);
        Assert.Equal(renderedNote.Y, accidental.Y);
    }

    [Fact]
    public void Build_ActiveNote_ReturnsGrowingActiveMarker()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2));

        var marker = Assert.Single(scene.Notes);
        Assert.True(marker.IsActive);
        Assert.Equal("C4", marker.Label);
        Assert.Equal(1, marker.DurationSeconds);
        Assert.False(marker.HasStem);
        Assert.Equal(0, marker.FlagCount);
        Assert.NotNull(marker.DurationEndX);
        Assert.True(marker.DurationEndX > marker.X);
    }

    [Fact]
    public void Build_ReleasedNoteCrossingBarline_ReturnsCompletedHeadsAndFullTie()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(
            new Pitch(NoteLetter.E, 0, 4),
            MusicalTime.BeatsToDuration(3, tempo));
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(5, tempo);
        timeline.Complete(note, releaseTime);

        var scene = GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo);

        Assert.Equal(2, scene.Notes.Count);
        Assert.All(scene.Notes, renderedNote => Assert.False(renderedNote.IsActive));
        double barlineX = GrandStaffLayout.MapAbsoluteBeatToScoreX(4, signature, firstVisibleMeasure: 0);
        Assert.True(scene.Notes[0].X < barlineX);
        Assert.True(scene.Notes[1].X > barlineX);
        var tie = Assert.Single(scene.Ties);
        Assert.Equal(scene.Notes[0].X, tie.X0);
        Assert.Equal(scene.Notes[1].X, tie.X1);
        Assert.False(tie.IsActive);
    }

    [Fact]
    public void Build_ActiveNoteCrossingBarline_ReturnsClosedEarlierHeadAndActiveTail()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.E, 0, 4),
            StartTime = MusicalTime.BeatsToDuration(3, tempo),
        };
        TimeSpan currentTime = MusicalTime.BeatsToDuration(5, tempo);

        var scene = GrandStaffSceneBuilder.Build([note], currentTime, signature, tempo);

        Assert.Equal(2, scene.Notes.Count);
        Assert.False(scene.Notes[0].IsActive);
        Assert.True(scene.Notes[0].HasStem);
        Assert.Null(scene.Notes[0].DurationEndX);
        Assert.True(scene.Notes[1].IsActive);
        Assert.NotNull(scene.Notes[1].DurationEndX);
        Assert.True(scene.Notes[1].DurationEndX > scene.Notes[1].X);
        Assert.True(Assert.Single(scene.Ties).IsActive);
    }

    [Fact]
    public void Build_ThreeNoteChordCrossingBarline_DistributesTieDirectionsAwayFromChordCenter()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan startTime = MusicalTime.BeatsToDuration(3, tempo);
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(5, tempo);
        var timeline = new NoteTimeline();
        PerformedNote[] notes =
        [
            timeline.Start(new Pitch(NoteLetter.E, 0, 4), startTime),
            timeline.Start(new Pitch(NoteLetter.D, 0, 4), startTime),
            timeline.Start(new Pitch(NoteLetter.C, 0, 4), startTime),
        ];
        foreach (var note in notes)
        {
            timeline.Complete(note, releaseTime);
        }

        var scene = GrandStaffSceneBuilder.Build(notes, releaseTime, signature, tempo);

        StemDirection[] directions = scene.Ties
            .OrderByDescending(tie => tie.Y0)
            .Select(tie => tie.CurveDirection)
            .ToArray();
        Assert.Equal(
            [StemDirection.Up, StemDirection.Down, StemDirection.Down],
            directions);
    }

    [Fact]
    public void Build_ActiveContinuationAfterRollover_ReturnsIncomingStubInsideScoreArea()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.F, 1, 4),
            StartTime = MusicalTime.BeatsToDuration(19, tempo),
        };
        TimeSpan currentTime = MusicalTime.BeatsToDuration(21, tempo);

        var scene = GrandStaffSceneBuilder.Build([note], currentTime, signature, tempo);

        var continuation = Assert.Single(scene.Notes);
        Assert.True(continuation.IsActive);
        Assert.True(continuation.X > GrandStaffLayout.ScoreX0);
        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        var stub = Assert.Single(scene.Ties);
        Assert.Equal(GrandStaffLayout.ScoreX0, stub.X0);
        Assert.Equal(continuation.X, stub.X1);
        Assert.True(stub.X0 >= GrandStaffLayout.ScoreX0);
    }

    [Fact]
    public void Build_ReleasedContinuationAfterRollover_ReturnsCompletedHeadAtSamePosition()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(
            new Pitch(NoteLetter.E, 0, 4),
            MusicalTime.BeatsToDuration(19, tempo));
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(21, tempo);

        var activeScene = GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo);
        timeline.Complete(note, releaseTime);
        var releasedScene = GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo);

        var activeContinuation = Assert.Single(activeScene.Notes);
        var releasedContinuation = Assert.Single(releasedScene.Notes);
        Assert.Equal(activeContinuation.X, releasedContinuation.X);
        Assert.False(releasedContinuation.IsActive);
        Assert.Null(releasedContinuation.DurationEndX);
        Assert.False(Assert.Single(releasedScene.Ties).IsActive);
    }

    [Fact]
    public void Build_NoteReleasedExactlyOnBarline_ReturnsOneHeadWithoutTie()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(
            new Pitch(NoteLetter.E, 0, 4),
            MusicalTime.BeatsToDuration(3, tempo));
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(4, tempo);
        timeline.Complete(note, releaseTime);

        var scene = GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo);

        Assert.Single(scene.Notes);
        Assert.Empty(scene.Ties);
    }

    [Fact]
    public void Build_TiedAccidentalPitch_ReturnsAccidentalOnlyAtAttack()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(
            new Pitch(NoteLetter.F, 1, 4),
            MusicalTime.BeatsToDuration(3, tempo));
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(5, tempo);
        timeline.Complete(note, releaseTime);

        var scene = GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo);

        Assert.Equal(2, scene.Notes.Count);
        var accidental = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.True(accidental.X < scene.Notes[0].X);
    }

    [Fact]
    public void Build_ThreeBeatFragment_ReturnsDottedHalfNotation()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.Zero);
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(3, tempo);
        timeline.Complete(note, releaseTime);

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo).Notes);

        Assert.True(renderedNote.HasDot);
        Assert.False(renderedNote.IsFilled);
        Assert.True(renderedNote.HasStem);
        Assert.Equal(0, renderedNote.FlagCount);
    }

    [Fact]
    public void Build_TiedNoteWithSelectedOctave_FitsTieYAndPreservesTieX()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var timeline = new NoteTimeline();
        var note = timeline.Start(
            new Pitch(NoteLetter.E, 0, 4),
            MusicalTime.BeatsToDuration(3, tempo));
        TimeSpan releaseTime = MusicalTime.BeatsToDuration(5, tempo);
        timeline.Complete(note, releaseTime);

        var originalTie = Assert.Single(
            GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo).Ties);
        var fittedTie = Assert.Single(
            GrandStaffSceneBuilder.Build([note], releaseTime, signature, tempo, selectedOctave: 4).Ties);

        Assert.Equal(originalTie.X0, fittedTie.X0);
        Assert.Equal(originalTie.X1, fittedTie.X1);
        Assert.InRange(fittedTie.Y0, -1, 1);
        Assert.InRange(fittedTie.Y1, -1, 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void Build_SelectedOctave_KeepsKeyboardRangeVisible(int selectedOctave)
    {
        PerformedNote[] notes =
        [
            new()
            {
                Pitch = new Pitch(NoteLetter.C, 0, selectedOctave),
                StartTime = TimeSpan.FromSeconds(1),
            },
            new()
            {
                Pitch = new Pitch(NoteLetter.C, 0, selectedOctave + 1),
                StartTime = TimeSpan.FromSeconds(1.5),
            },
        ];

        var scene = GrandStaffSceneBuilder.Build(
            notes,
            TimeSpan.FromSeconds(2),
            selectedOctave);

        Assert.All(scene.Notes, note => Assert.InRange(note.Y, -1, 1));
        Assert.All(
            scene.Lines,
            line =>
            {
                Assert.InRange(line.Y0, -1, 1);
                Assert.InRange(line.Y1, -1, 1);
            });
    }

    [Fact]
    public void Build_ReleasedNote_ReturnsClosedMarkerWithMeasuredDuration()
    {
        var timeline = new NoteTimeline();
        var note = timeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.FromSeconds(1));
        timeline.Complete(note, TimeSpan.FromSeconds(1.5));

        var scene = GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2));

        var marker = Assert.Single(scene.Notes);
        Assert.False(marker.IsActive);
        Assert.Equal(0.5, marker.DurationSeconds);
        Assert.Null(marker.DurationEndX);
    }

    [Theory]
    [InlineData(2, false, false, 0)]
    [InlineData(1, false, true, 0)]
    [InlineData(0.46, true, true, 0)]
    [InlineData(0.25, true, true, 1)]
    [InlineData(0.125, true, true, 2)]
    public void Build_ReleasedRhythmicValueWithinMeasure_ReturnsStandardNotation(
        double durationSeconds,
        bool expectedIsFilled,
        bool expectedHasStem,
        int expectedFlagCount)
    {
        var timeline = new NoteTimeline();
        var note = timeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.Zero);
        timeline.Complete(note, TimeSpan.FromSeconds(durationSeconds));

        var scene = GrandStaffSceneBuilder.Build(
            [note],
            TimeSpan.FromSeconds(4),
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120));

        var marker = Assert.Single(scene.Notes);
        Assert.Equal(expectedIsFilled, marker.IsFilled);
        Assert.Equal(expectedHasStem, marker.HasStem);
        Assert.Equal(expectedFlagCount, marker.FlagCount);
    }

    [Fact]
    public void Build_ReleasedNoteWithEighthBeatUnit_ReturnsQuarterNotation()
    {
        var timeline = new NoteTimeline();
        var note = timeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.FromSeconds(1));
        timeline.Complete(note, TimeSpan.FromSeconds(3));

        var scene = GrandStaffSceneBuilder.Build(
            [note],
            TimeSpan.FromSeconds(4),
            new TimeSignature(4, new NoteValue(8)),
            new Tempo(60));

        var marker = Assert.Single(scene.Notes);
        Assert.True(marker.IsFilled);
        Assert.True(marker.HasStem);
        Assert.Equal(0, marker.FlagCount);
    }

    [Fact]
    public void Build_LiveNote_ReturnsResizeIndependentNormalizedCoordinates()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.A, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var renderedNote = Assert.Single(
            GrandStaffSceneBuilder.Build([note], TimeSpan.FromSeconds(2)).Notes);

        Assert.InRange(renderedNote.X, -1, 1);
        Assert.InRange(renderedNote.Y, -1, 1);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_QuarterRestWithRestsEnabled_DrawsRestAtItsBeatOnTheStaffMiddleLine(Staff staff)
    {
        // A rest at beat 2 sits where a note at beat 2 would: compare against the same measure with a note there.
        ScoreNote beatOne = ExerciseNote(staff, beatOffset: 1);
        ScoreNote beatThree = ExerciseNote(staff, beatOffset: 3);
        var restScore = new Score(
            "rest",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([beatOne, beatThree], [new ScoreRest(new NoteValue(4), 0, 2, staff)])]);
        var noteScore = new Score(
            "note",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([beatOne, ExerciseNote(staff, beatOffset: 2), beatThree], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(restScore, firstVisibleMeasure: 0, drawRests: true);
        var referenceScene = GrandStaffSceneBuilder.BuildScore(noteScore, firstVisibleMeasure: 0);

        var rest = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
        GrandStaffNote referenceNoteAtBeatTwo = referenceScene.Notes.Single(note => note.ScoreOnsetBeats == 2);
        IReadOnlyList<float> staffLines = staff == Staff.Treble
            ? GrandStaffLayout.TrebleLineYs
            : GrandStaffLayout.BassLineYs;
        Assert.Equal("𝄽", rest.Text);
        Assert.Equal(referenceNoteAtBeatTwo.X, rest.X, precision: 6);
        Assert.Equal(GrandStaffLayout.SeparateStaffY(staffLines[2], staff), rest.Y, precision: 6);
        Assert.Equal(3 * GrandStaffLayout.GetRenderedStaffSpace(staff), rest.Height!.Value, precision: 6);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void BuildScore_EighthRestWithRestsEnabled_DrawsAnEighthRestAtItsBeatOnTheStaffMiddleLine(Staff staff)
    {
        ScoreNote beatOne = ExerciseNote(staff, beatOffset: 1);
        ScoreNote offBeat = ExerciseNote(staff, beatOffset: 2.5);
        var restScore = new Score(
            "rest",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([beatOne, offBeat], [new ScoreRest(new NoteValue(8), 0, 2, staff)])]);
        var noteScore = new Score(
            "note",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([beatOne, ExerciseNote(staff, beatOffset: 2), offBeat], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(restScore, firstVisibleMeasure: 0, drawRests: true);
        var referenceScene = GrandStaffSceneBuilder.BuildScore(noteScore, firstVisibleMeasure: 0);

        var rest = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
        GrandStaffNote referenceNoteAtBeatThree = referenceScene.Notes.Single(note => note.ScoreOnsetBeats == 2);
        IReadOnlyList<float> staffLines = staff == Staff.Treble
            ? GrandStaffLayout.TrebleLineYs
            : GrandStaffLayout.BassLineYs;
        Assert.Equal("𝄾", rest.Text);
        Assert.Equal(referenceNoteAtBeatThree.X, rest.X, precision: 6);
        Assert.Equal(GrandStaffLayout.SeparateStaffY(staffLines[2], staff), rest.Y, precision: 6);
        Assert.Equal(2 * GrandStaffLayout.GetRenderedStaffSpace(staff), rest.Height!.Value, precision: 6);
    }

    [Fact]
    public void BuildScore_HalfAndWholeRests_AreNotDrawnBecauseNoPatternUsesThem()
    {
        var score = new Score(
            "rest",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([], [new ScoreRest(new NoteValue(2), 0, 0, Staff.Treble), new ScoreRest(new NoteValue(2), 0, 2, Staff.Treble)])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, drawRests: true);

        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
    }

    private static ScoreNote TiedNote(Pitch pitch, int measureIndex, double beatOffset, bool tiesToNext, NoteValue? value = null) =>
        new(pitch, value ?? new NoteValue(4), measureIndex, beatOffset, Staff.Treble, TiesToNext: tiesToNext);

    private static Score TwoMeasureScore(params ScoreNote[] notes) =>
        new(
            "tie",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [
                new ScoreMeasure(notes.Where(note => note.MeasureIndex == 0).ToArray(), []),
                new ScoreMeasure(notes.Where(note => note.MeasureIndex == 1).ToArray(), []),
            ]);

    [Theory]
    [InlineData(NoteLetter.E, 4, StemDirection.Down)] // E4 sits below the middle line: stem up, so the tie curves below
    [InlineData(NoteLetter.B, 4, StemDirection.Up)] // B4 is on the middle line: stem down, so the tie curves above
    public void BuildScore_TiedNotesWithTiesEnabled_DrawsOneTieBetweenTheirNoteheadsOppositeTheStem(
        NoteLetter letter,
        int octave,
        StemDirection expectedCurve)
    {
        var pitch = new Pitch(letter, 0, octave);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 1, true), TiedNote(pitch, 0, 2, false));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, drawTies: true);

        var tie = Assert.Single(scene.Ties);
        Assert.Equal(scene.Notes[0].X, tie.X0);
        Assert.Equal(scene.Notes[1].X, tie.X1);
        Assert.Equal(scene.Notes[0].Y, tie.Y0);
        Assert.Equal(scene.Notes[1].Y, tie.Y1);
        Assert.Equal(expectedCurve, tie.CurveDirection);
        Assert.False(tie.IsActive);
    }

    [Fact]
    public void BuildScore_TieAcrossTheBarline_ConnectsTheLastNoteOfOneMeasureToTheFirstOfTheNext()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 3, true), TiedNote(pitch, 1, 0, false));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, drawTies: true);

        var tie = Assert.Single(scene.Ties);
        Assert.Equal(scene.Notes[0].X, tie.X0);
        Assert.Equal(scene.Notes[1].X, tie.X1);
        Assert.True(tie.X1 > tie.X0);
    }

    [Fact]
    public void BuildScore_TiedNotesWithoutTheTiesOption_DrawsNoTie()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 1, true), TiedNote(pitch, 0, 2, false));

        Assert.Empty(GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Ties);
    }

    [Fact]
    public void BuildScore_TieThatLeavesTheVisibleMeasures_DrawsAHalfTieToTheRightEdgeAndTheNextWindowOneFromTheLeftEdge()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 3, true), TiedNote(pitch, 1, 0, false));

        var firstWindow = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 1, drawTies: true);
        var secondWindow = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 1, visibleMeasureCount: 1, drawTies: true);

        var outgoing = Assert.Single(firstWindow.Ties);
        Assert.Equal(firstWindow.Notes[0].X, outgoing.X0);
        Assert.Equal(GrandStaffLayout.ScoreX1, outgoing.X1);
        var incoming = Assert.Single(secondWindow.Ties);
        Assert.Equal(OpeningBarlineX(secondWindow), incoming.X0);
        Assert.Equal(secondWindow.Notes[0].X, incoming.X1);
    }

    // Measured on a real 916 x 240 canvas (440 px per scene-X unit, 7.5 px staff space): a tie stops 0.8 staff space (6 px)
    // short of the notehead centre it arrives at, so a half tie from the opening barline to a note 8.8 px (0.02) right of
    // the barline was about 2.8 px long, and when the note sat on the row's left edge it had no length at all.
    private const double TieEndGapPx = 6.0;

    /// <summary>The barline that opens the row: the nearest one left of its first note.</summary>
    private static double OpeningBarlineX(GrandStaffScene scene) =>
        scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Barline && line.X0 < scene.Notes.Min(note => note.X))
            .Max(line => line.X0);

    [Fact]
    public void BuildScore_RowStartingWithATiedContinuation_LeavesRoomForTheArrivingTieToBeSeen()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 3, true), TiedNote(pitch, 1, 0, false));

        var secondWindow = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 1, visibleMeasureCount: 1, drawTies: true);

        var incoming = Assert.Single(secondWindow.Ties);
        double visibleLengthPx = ((incoming.X1 - incoming.X0) * PixelsPerSceneX) - TieEndGapPx;
        Assert.True(
            visibleLengthPx >= 10,
            $"The tie arriving at the start of the row is only {visibleLengthPx:F1} px long between the barline and its note.");
    }

    [Fact]
    public void BuildScore_RowStartingWithATiedContinuationButNoTieOption_KeepsItsNotesWhereTheyWere()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score tied = TwoMeasureScore(TiedNote(pitch, 0, 3, true), TiedNote(pitch, 1, 0, false));
        Score untied = TwoMeasureScore(TiedNote(pitch, 0, 3, false), TiedNote(pitch, 1, 0, false));

        var withoutTies = GrandStaffSceneBuilder.BuildScore(tied, firstVisibleMeasure: 1, visibleMeasureCount: 1);
        var reference = GrandStaffSceneBuilder.BuildScore(untied, firstVisibleMeasure: 1, visibleMeasureCount: 1, drawTies: true);

        // Imported scores draw no ties, so a tie flag alone must not move anything.
        Assert.Equal(reference.Notes[0].X, withoutTies.Notes[0].X);
    }

    [Fact]
    public void BuildScore_RowStartingWithAnUntiedNote_KeepsItsNotesWhereTheyWere()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 3, false), TiedNote(pitch, 1, 0, false));

        var withTies = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 1, visibleMeasureCount: 1, drawTies: true);
        var withoutTies = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 1, visibleMeasureCount: 1);

        Assert.Equal(GrandStaffLayout.ScoreX0, withTies.Notes[0].X, 6);
        Assert.Equal(withoutTies.Notes[0].X, withTies.Notes[0].X);
    }

    [Fact]
    public void BuildScore_RowStartingWithATiedContinuation_KeepsTheCursorOnItsNote()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 0, 3, true), TiedNote(pitch, 1, 0, false));

        var scene = GrandStaffSceneBuilder.BuildScore(
            score, firstVisibleMeasure: 1, cursorBeats: 4, visibleMeasureCount: 1, drawTies: true);

        var cursor = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(scene.Notes[0].X, cursor.X0, 6);
    }

    [Fact]
    public void BuildScore_TieFlagWithNoContinuation_DrawsNothingForItBeyondTheScoresEnd()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        Score score = TwoMeasureScore(TiedNote(pitch, 1, 3, true));

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, drawTies: true);

        Assert.Empty(scene.Ties);
    }

    [Fact]
    public void BuildScore_QuarterRestWithoutTheRestsOption_DrawsNoRestGlyph()
    {
        var score = new Score(
            "rest",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([ExerciseNote(Staff.Treble, beatOffset: 0)], [new ScoreRest(new NoteValue(4), 0, 1, Staff.Treble)])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
    }

    [Fact]
    public void BuildScore_RestsOutsideTheVisibleWindowOrOfAnUnmappedValue_AreNotDrawn()
    {
        var score = new Score(
            "rests",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [
                new ScoreMeasure([], [new ScoreRest(new NoteValue(2), 0, 0, Staff.Treble)]),
                new ScoreMeasure([], []),
                new ScoreMeasure([], [new ScoreRest(new NoteValue(4), 2, 0, Staff.Treble)]),
            ]);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            drawRests: true,
            visibleMeasureCount: 2);

        // The half rest has no glyph mapping yet, and the quarter rest is in measure 2, outside the 2-measure window.
        Assert.DoesNotContain(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
    }

    private static ScoreNote ExerciseNote(Staff staff, double beatOffset) =>
        new(new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 5 : 3), new NoteValue(4), 0, beatOffset, staff);

    private static Score CreateScore(int measureCount) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            Enumerable.Range(0, measureCount)
                .Select(_ => new ScoreMeasure([], []))
                .ToArray());

    private static Score SingleNoteScore(
        ScoreNote note,
        int keyFifths = 0,
        TimeSignature? timeSignature = null) =>
        ScoreWithNotes([note], keyFifths, timeSignature);

    private static Score ScoreWithNotes(
        ScoreNote[] notes,
        int keyFifths = 0,
        TimeSignature? timeSignature = null) =>
        new(
            "test",
            timeSignature ?? new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            keyFifths,
            [new ScoreMeasure(notes, [])]);
}
