using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// Several marks on one note, and the articulations, ornaments and slur ends added by the MusicXML coverage work.
public sealed partial class GrandStaffSceneBuilderTests
{
    private static ScoreNote NoteAt(double beatOffset, ScoreSlur? slur = null, ScoreArticulation? articulation = null, ScoreOrnament? ornament = null) =>
        new(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            beatOffset,
            Staff.Treble,
            Slur: slur,
            Articulation: articulation,
            Ornament: ornament);

    [Fact]
    public void BuildScore_SlurEndingAndStartingOnOneNote_DrawsTwoArcsMeetingAtThatNote()
    {
        var score = ScoreWithNotes(
        [
            NoteAt(0, new ScoreSlur(true, 1)),
            NoteAt(1, new ScoreSlur(false, 1, new ScoreSlur(true, 1))),
            NoteAt(2, new ScoreSlur(false, 1)),
        ]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Slurs.Count);
        var first = Assert.Single(scene.Slurs, slur => slur.X0 == scene.Notes[0].X);
        var second = Assert.Single(scene.Slurs, slur => slur.X0 == scene.Notes[1].X);
        Assert.Equal(scene.Notes[1].X, first.X1);
        Assert.Equal(scene.Notes[2].X, second.X1);
    }

    [Fact]
    public void BuildScore_TwoSlursStartingTogether_DrawsBothArcs()
    {
        var score = ScoreWithNotes(
        [
            NoteAt(0, new ScoreSlur(true, 1, new ScoreSlur(true, 2))),
            NoteAt(1, new ScoreSlur(false, 2)),
            NoteAt(2, new ScoreSlur(false, 1)),
        ]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Slurs.Count);
        Assert.Contains(scene.Slurs, slur => slur.X1 == scene.Notes[1].X);
        Assert.Contains(scene.Slurs, slur => slur.X1 == scene.Notes[2].X);
    }

    [Fact]
    public void BuildScore_TwoArticulations_DrawsBothMarksStackedAboveTheNote()
    {
        var score = ScoreWithNotes([NoteAt(0, articulation: ScoreArticulation.Staccato | ScoreArticulation.Accent)]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var marks = scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Articulation).ToArray();
        Assert.Equal(["●", ">"], marks.Select(mark => mark.Text));
        Assert.All(marks, mark => Assert.Equal(scene.Notes[0].X, mark.X));
        Assert.True(marks[1].Y > marks[0].Y);
        Assert.True(marks[0].Y > scene.Notes[0].Y);
    }

    [Fact]
    public void BuildScore_ArticulationAndOrnamentOnOneNote_DoNotShareAPosition()
    {
        var score = ScoreWithNotes(
            [NoteAt(0, articulation: ScoreArticulation.Staccato, ornament: ScoreOrnament.TrillMark)]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var articulation = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Articulation);
        var ornament = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Ornament);
        Assert.NotEqual(articulation.Y, ornament.Y);
    }

    [Theory]
    [InlineData(ScoreArticulation.StrongAccent)]
    [InlineData(ScoreArticulation.BreathMark)]
    [InlineData(ScoreArticulation.Caesura)]
    public void BuildScore_NewArticulation_DrawsOneMarkAboveTheNote(ScoreArticulation articulation)
    {
        var score = ScoreWithNotes([NoteAt(0, articulation: articulation)]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var mark = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Articulation);
        Assert.False(string.IsNullOrWhiteSpace(mark.Text));
        Assert.True(mark.Y > scene.Notes[0].Y);
    }

    [Theory]
    [InlineData(ScoreOrnament.Turn)]
    [InlineData(ScoreOrnament.InvertedTurn)]
    [InlineData(ScoreOrnament.Mordent)]
    [InlineData(ScoreOrnament.InvertedMordent)]
    [InlineData(ScoreOrnament.Shake)]
    public void BuildScore_NewOrnament_DrawsOneMarkAboveTheNote(ScoreOrnament ornament)
    {
        var score = ScoreWithNotes([NoteAt(0, ornament: ornament)]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var mark = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Ornament);
        Assert.False(string.IsNullOrWhiteSpace(mark.Text));
        Assert.True(mark.Y > scene.Notes[0].Y);
    }

    [Fact]
    public void BuildScore_TrillAndTurnOnOneNote_DrawsBothOrnaments()
    {
        var score = ScoreWithNotes([NoteAt(0, ornament: ScoreOrnament.TrillMark | ScoreOrnament.Turn)]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        Assert.Equal(2, scene.Glyphs.Count(glyph => glyph.Kind == GrandStaffGlyphKind.Ornament));
    }

    [Theory]
    [InlineData(16, 2)]
    [InlineData(32, 3)]
    [InlineData(64, 4)]
    public void BuildScore_ShortNote_DrawsOneFlagPerBeamCount(int denominator, int expectedFlags)
    {
        var note = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(denominator), 0, 0, Staff.Treble);

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes([note]), firstVisibleMeasure: 0);

        var rendered = Assert.Single(scene.Notes);
        Assert.Equal(expectedFlags, rendered.FlagCount);
        Assert.True(rendered.HasStem);
        Assert.True(rendered.IsFilled);
    }

    [Fact]
    public void BuildScore_BeamedThirtySecondNotes_DrawThreeBeams()
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(32), 0, 0, Staff.Treble, BeamState: BeamState.Begin),
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(32), 0, 0.125, Staff.Treble, BeamState: BeamState.Continue),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(32), 0, 0.25, Staff.Treble, BeamState: BeamState.End),
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        var beam = Assert.Single(scene.Beams);
        Assert.Equal(3, beam.Count);
        Assert.All(scene.Notes, note => Assert.Equal(0, note.FlagCount));
    }

    [Theory]
    [InlineData(ScoreBeamKind.ForwardHook, true)]
    [InlineData(ScoreBeamKind.BackwardHook, false)]
    public void BuildScore_SecondaryBeamHook_DrawsHookAndSuppressesTheFlag(
        ScoreBeamKind hookKind,
        bool extendsForward)
    {
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(16), 0, 0, Staff.Treble, BeamState: BeamState.Begin)
            {
                Beams =
                [
                    new ScoreBeam(1, ScoreBeamKind.Begin),
                    new ScoreBeam(2, hookKind),
                ],
            },
            new(new Pitch(NoteLetter.D, 0, 4), new NoteValue(8), 0, 0.5, Staff.Treble, BeamState: BeamState.End)
            {
                Beams = [new ScoreBeam(1, ScoreBeamKind.End)],
            },
        ];

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes(notes), firstVisibleMeasure: 0);

        var primaryBeam = Assert.Single(scene.Beams, beam => beam.Level == 0 && beam.Count == 1);
        var hook = Assert.Single(
            scene.Beams,
            beam => beam.Level == 1 && beam.Count == 1 && (beam.X1 > beam.X0) == extendsForward);
        double primarySlope = (primaryBeam.Y1 - primaryBeam.Y0) / (primaryBeam.X1 - primaryBeam.X0);
        double hookSlope = (hook.Y1 - hook.Y0) / (hook.X1 - hook.X0);
        Assert.Equal(primarySlope, hookSlope, precision: 6);
        Assert.Equal(0, scene.Notes.Single(note => note.Label == "C4").FlagCount);
    }

    [Fact]
    public void BuildScore_ImportedStemEndY_UsesTheMusicXmlStemEndpoint()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            StemDirection: ScoreStemDirection.Up)
        {
            StemEndYInTenths = 18.5,
        };

        var scene = GrandStaffSceneBuilder.BuildScore(ScoreWithNotes([note]), firstVisibleMeasure: 0);

        var rendered = Assert.Single(scene.Notes);
        double expectedStemEndY = GrandStaffLayout.SeparateStaffY(
            GrandStaffLayout.TrebleLineYs[^1] +
            (1.85 * (GrandStaffLayout.TrebleLineYs[1] - GrandStaffLayout.TrebleLineYs[0])),
            Staff.Treble);
        Assert.NotNull(rendered.StemEndY);
        Assert.Equal(expectedStemEndY, rendered.StemEndY.Value, precision: 6);
    }
}
