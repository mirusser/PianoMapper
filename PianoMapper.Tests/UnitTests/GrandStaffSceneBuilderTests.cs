using PianoMapper.Web.Rendering;
using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class GrandStaffSceneBuilderTests
{
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Notes.Count);
        Assert.Equal(scene.Notes[0].X, scene.Notes[1].X);
        Assert.All(scene.Notes, note => Assert.True(note.IsFilled));
        Assert.All(scene.Notes, note => Assert.True(note.HasStem));
        Assert.All(scene.Notes, note => Assert.True(note.HasDot));
        Assert.All(scene.Notes, note => Assert.Equal(1, note.FlagCount));
        Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal(5, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline));
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
    public void BuildScore_DifferentPitchesOnEachStaff_AlignsLabelsInInterStaffLane()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.F, 1, 5), new NoteValue(4), 0, 1, Staff.Treble),
            new(new Pitch(NoteLetter.C, 0, 3), new NoteValue(4), 0, 2, Staff.Bass),
            new(new Pitch(NoteLetter.A, 0, 2), new NoteValue(4), 0, 3, Staff.Bass),
        ];
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNotes = scene.Notes
            .ToDictionary(note => note.Label);
        var staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);

        Assert.Equal(renderedNotes["A4"].LabelY, renderedNotes["F#5"].LabelY);
        Assert.Equal(renderedNotes["C3"].LabelY, renderedNotes["A2"].LabelY);
        Assert.Equal(renderedNotes["A4"].LabelY, renderedNotes["C3"].LabelY);
        Assert.All(
            renderedNotes.Values,
            note => Assert.InRange(note.LabelY!.Value, bassTopLineY, trebleBottomLineY));
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
            var score = new Score(
                "test",
                new TimeSignature(4, new NoteValue(4)),
                new Tempo(120),
                0,
                [new ScoreMeasure([sourceNote], [])]);
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
    public void BuildScore_Fingering_PositionedInInterStaffLane(Staff staff)
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(3));
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);

        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.FingeringY.Value, bassTopLineY, trebleBottomLineY);
    }

    [Fact]
    public void BuildScore_TrebleFingering_UsesInterStaffLane()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(5));
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var staffLines = scene.Lines.Where(line => line.Kind == GrandStaffLineKind.Staff).ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);

        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.FingeringY.Value, bassTopLineY, trebleBottomLineY);
        Assert.InRange(renderedNote.FingeringY.Value, scene.Bands[0].Y0, scene.Bands[0].Y1);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
    public void BuildScore_PitchSelectedNotationStaff_StaysOutsideInterStaffLane(Staff staff)
    {
        // C4 on treble and C3 on bass both sit one ledger line below their own staff.
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, staff == Staff.Treble ? 4 : 3),
            new NoteValue(4),
            0,
            0,
            staff,
            Fingering: new ScoreFingering(3));
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.LabelY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.InRange(renderedNote.FingeringY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.True(staff == Staff.Treble
            ? renderedNote.Y > annotationLane.Y1
            : renderedNote.Y < annotationLane.Y0);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var renderedNotes = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes
            .ToDictionary(note => note.Label);

        Assert.Equal(renderedNotes["C4"].LabelY, renderedNotes["A4"].LabelY);
        Assert.Equal(renderedNotes["C4"].FingeringY, renderedNotes["A4"].FingeringY);
        Assert.True(renderedNotes["C4"].LabelY < renderedNotes["C4"].Y);
        Assert.True(renderedNotes["A4"].LabelY < renderedNotes["A4"].Y);
    }

    [Fact]
    public void BuildScore_NoteBelowTrebleStaff_LowersSharedAnnotationLane()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.C, 0, 3), new NoteValue(4), 0, 1, Staff.Bass),
        ];
        var scoreWithLowTreble = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);
        var scoreWithoutLowTreble = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([notes[1]], [])]);

        var bassLabelYWithLowTreble = GrandStaffSceneBuilder.BuildScore(scoreWithLowTreble, firstVisibleMeasure: 0)
            .Notes.Single(note => note.Label == "C3").LabelY;
        var bassLabelYAlone = GrandStaffSceneBuilder.BuildScore(scoreWithoutLowTreble, firstVisibleMeasure: 0)
            .Notes.Single(note => note.Label == "C3").LabelY;

        Assert.True(bassLabelYWithLowTreble < bassLabelYAlone);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
    public void BuildScore_LowPitchMarkedTreble_UsesBassNotationBelowInterStaffLane(
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.Equal("R4", renderedNote.Fingering);
        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.LabelY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.InRange(renderedNote.FingeringY.Value, annotationLane.Y0, annotationLane.Y1);
        Assert.True(renderedNote.Y < annotationLane.Y0);
    }

    [Fact]
    public void BuildScore_LowBassNote_AnnotationsStayInInterStaffLane()
    {
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.F, 0, 1),
            new NoteValue(4),
            0,
            0,
            Staff.Bass,
            Fingering: new ScoreFingering(2));
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);
        var renderedNote = Assert.Single(scene.Notes);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        var annotationLane = Assert.Single(scene.Bands);

        Assert.NotNull(renderedNote.LabelY);
        Assert.NotNull(renderedNote.FingeringY);
        Assert.InRange(renderedNote.LabelY.Value, bassTopLineY, trebleBottomLineY);
        Assert.InRange(renderedNote.FingeringY.Value, bassTopLineY, trebleBottomLineY);
        Assert.True(renderedNote.FingeringY < renderedNote.LabelY);
        Assert.True(renderedNote.Y < annotationLane.Y0);
    }

    [Fact]
    public void BuildScore_NoteWithoutFingering_LeavesFingeringFieldsNull()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var renderedNote = Assert.Single(GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0).Notes);

        Assert.Null(renderedNote.Fingering);
        Assert.Null(renderedNote.FingeringY);
    }

    [Fact]
    public void BuildScore_ShowNoteLabelsFalse_HidesNoteLabel()
    {
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false);

        Assert.NotNull(Assert.Single(scene.Notes).LabelY);
    }

    [Fact]
    public void BuildScore_Annotations_UseSingleLaneBetweenStaves()
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var annotationLane = Assert.Single(scene.Bands);
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
        Assert.True(annotationLane.Y1 < trebleBottomLineY);
        Assert.True(annotationLane.Y0 > bassTopLineY);
        Assert.Single(scene.Notes.Select(note => note.LabelY).Distinct());
        Assert.Single(scene.Notes.Select(note => note.FingeringY).Distinct());
        Assert.All(scene.Notes, note => Assert.InRange(note.LabelY!.Value, annotationLane.Y0, annotationLane.Y1));
        Assert.All(scene.Notes, note => Assert.InRange(note.FingeringY!.Value, annotationLane.Y0, annotationLane.Y1));
    }

    [Fact]
    public void BuildScore_HighLeftHandNotes_UseTrebleNotationAboveInterStaffAnnotationLane()
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var annotationLane = Assert.Single(scene.Bands);
        GrandStaffNote[] leftHandNotes = scene.Notes.Where(note => note.Fingering?.StartsWith('L') == true).ToArray();
        GrandStaffLine[] staffLines = scene.Lines
            .Where(line => line.Kind == GrandStaffLineKind.Staff)
            .ToArray();
        double renderedStaffSpace = staffLines[1].Y0 - staffLines[0].Y0;
        double trebleBottomLineY = staffLines.Take(5).Min(line => line.Y0);
        double bassTopLineY = staffLines.Skip(5).Max(line => line.Y0);
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
        Assert.True(annotationLane.Y0 > bassTopLineY);

        Assert.Single(scene.Notes.Select(note => note.LabelY).Distinct());
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            2,
            [new ScoreMeasure(notes, [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            2,
            [new ScoreMeasure(notes, [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(StemDirection.Up, Assert.Single(scene.Beams).StemDirection);
        Assert.All(scene.Notes, note => Assert.Equal(StemDirection.Up, note.StemDirection));
    }

    [Fact]
    public void BuildScore_CursorInWindow_ReturnsMappedCursorLine()
    {
        var score = CreateScore(measureCount: 6);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 4, cursorBeats: 17);

        var cursor = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(17, score.TimeSignature, firstVisibleMeasure: 4),
            cursor.X0,
            6);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([expectedNote], [])]);

        var scene = GrandStaffSceneBuilder.BuildScore(
            score,
            firstVisibleMeasure: 0,
            expectedNotes: new HashSet<ScoreNote> { expectedNote });

        Assert.True(Assert.Single(scene.Notes).IsActive);
    }

    [Fact]
    public void BuildScore_PerformedInput_OverlaysOnlyHeldNoteAtCursor()
    {
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([], [])]);
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
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(1, score.TimeSignature, firstVisibleMeasure: 0),
            indicator.X,
            6);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([], [])]);
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
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([], [])]);
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
    public void GrandStaffScene_WithoutTies_ReturnsEmptyTieCollection()
    {
        var scene = new GrandStaffScene([], [], []);

        Assert.Empty(scene.Ties);
    }

    [Fact]
    public void GrandStaffScene_WithFullTieAndEdgeStub_PreservesTiePrimitives()
    {
        GrandStaffTie[] ties =
        [
            new(-0.4, 0.2, -0.1, 0.2, StemDirection.Down, IsActive: false),
            new(GrandStaffLayout.ScoreX0, 0.2, -0.45, 0.2, StemDirection.Down, IsActive: true),
        ];

        var scene = new GrandStaffScene([], [], []) { Ties = ties };

        Assert.Equal(ties, scene.Ties);
        Assert.Empty(scene.Lines);
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
    public void Build_EmptyTimeline_ReturnsEmptyScene()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        Assert.Empty(scene.Notes);
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
}
