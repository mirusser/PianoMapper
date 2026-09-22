using PianoMapper.Web.Rendering;
using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class GrandStaffSceneBuilderTests
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
    public void BuildScore_NoteWithoutFingering_LeavesFingeringFieldsNull()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var renderedNote = Assert.Single(GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Null(renderedNote.Fingering);
        Assert.Null(renderedNote.FingeringY);
    }

    [Fact]
    public void BuildScore_ShowNoteLabelsFalse_HidesNoteLabel()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showNoteLabels: false);

        Assert.Null(Assert.Single(scene.Notes).LabelY);
    }

    [Fact]
    public void BuildScore_ShowNoteLabelsFalse_StillShowsFingering()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showNoteLabels: false);

        var renderedNote = Assert.Single(scene.Notes);
        Assert.NotNull(renderedNote.Fingering);
        Assert.NotNull(renderedNote.FingeringY);
    }

    [Fact]
    public void BuildScore_ShowFingeringsFalse_HidesFingeringEvenWhenNoteHasFingering()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false);

        var renderedNote = Assert.Single(scene.Notes);
        Assert.Null(renderedNote.Fingering);
        Assert.Null(renderedNote.FingeringY);
    }

    [Fact]
    public void BuildScore_ShowFingeringsFalse_StillShowsNoteLabel()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(3));
        var score = SingleNoteScore(sourceNote);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false);

        Assert.NotNull(Assert.Single(scene.Notes).LabelY);
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
    public void BuildScore_CursorAtWindowBoundary_BelongsOnlyToIncomingWindow()
    {
        var score = CreateScore(measureCount: 12);
        double boundaryBeats = GrandStaffLayout.VisibleMeasureCount * score.TimeSignature.Numerator;

        var outgoingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            cursorBeats: boundaryBeats);
        var incomingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: GrandStaffLayout.VisibleMeasureCount,
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
        double boundaryBeats = GrandStaffLayout.VisibleMeasureCount * score.TimeSignature.Numerator;

        var outgoingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            performedNotes: [heldNote],
            performedNoteBeats: boundaryBeats);
        var incomingScene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: GrandStaffLayout.VisibleMeasureCount,
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
