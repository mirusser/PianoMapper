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
        Assert.Equal(2, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Barline));
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
    public void Build_EmptyTimeline_ReturnsGrandStaffWithoutNotes()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        Assert.Empty(scene.Notes);
        Assert.Equal(10, scene.Lines.Count(line => line.Kind == GrandStaffLineKind.Staff));
        Assert.Equal(2, scene.Glyphs.Count(glyph => glyph.Kind == GrandStaffGlyphKind.Clef));
    }

    [Fact]
    public void Build_LiveGrandStaff_RequestsClefAwareNoteClipping()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero, selectedOctave: 4);

        Assert.True(scene.ShouldClipNotesAtClefs);
    }

    [Fact]
    public void Build_EmptyTimeline_CentersBassClefOnFLine()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var bassClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄢");
        Assert.Equal(GrandStaffLayout.BassLineYs[3], bassClef.Y, 6);
    }

    [Fact]
    public void Build_EmptyTimeline_PositionsTrebleClefForGLineAlignment()
    {
        var scene = GrandStaffSceneBuilder.Build([], TimeSpan.Zero);

        var trebleClef = Assert.Single(scene.Glyphs, glyph => glyph.Text == "𝄞");
        Assert.Equal(GrandStaffLayout.TrebleLineYs[2], trebleClef.Y, 6);
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
        Assert.Equal(trebleStaffSpace * 6, trebleClef.Height.Value, 6);
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
        Assert.Equal(GrandStaffLayout.MiddleCY, renderedNote.Y, 6);
        var ledgerLine = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Ledger);
        Assert.Equal(GrandStaffLayout.MiddleCY, ledgerLine.Y0, 6);
        Assert.Equal(0.13, ledgerLine.X1 - ledgerLine.X0, 6);
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
    }

    [Theory]
    [InlineData(2, false, false, 0)]
    [InlineData(1, false, true, 0)]
    [InlineData(0.46, true, true, 0)]
    [InlineData(0.25, true, true, 1)]
    [InlineData(0.125, true, true, 2)]
    public void Build_ReleasedRhythmicValue_ReturnsStandardNotation(
        double durationSeconds,
        bool expectedIsFilled,
        bool expectedHasStem,
        int expectedFlagCount)
    {
        var timeline = new NoteTimeline();
        var note = timeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.FromSeconds(1));
        timeline.Complete(note, TimeSpan.FromSeconds(1 + durationSeconds));

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
