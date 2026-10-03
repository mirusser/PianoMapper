using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

// What a measure's own state (a key change first) does to the grand staff.
public sealed partial class GrandStaffSceneBuilderTests
{
    private static ScoreNote QuarterAt(NoteLetter letter, int alter, int measureIndex, double beatOffset = 0, Staff staff = Staff.Treble) =>
        new(new Pitch(letter, alter, 4), new NoteValue(4), measureIndex, beatOffset, staff);

    private static Score KeyChangeScore(int initialKey, int changedKey, int changeMeasure, int measureCount = 3)
    {
        var measures = Enumerable.Range(0, measureCount)
            .Select(index => new ScoreMeasure(
                [QuarterAt(NoteLetter.F, 1, index)],
                [],
                index == changeMeasure ? changedKey : null))
            .ToArray();
        return new Score("keys", new TimeSignature(4, new NoteValue(4)), new Tempo(120), initialKey, measures);
    }

    private static GrandStaffGlyph[] KeySignatureGlyphs(GrandStaffScene scene) =>
        scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.KeySignature).ToArray();

    [Fact]
    public void BuildScore_KeyChangeInALaterMeasure_DerivesAccidentalsFromTheKeyInEffect()
    {
        var score = KeyChangeScore(initialKey: 0, changedKey: 1, changeMeasure: 1, measureCount: 2);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var sharps = scene.Glyphs.Where(glyph => glyph.Kind == GrandStaffGlyphKind.Accidental).ToArray();
        var onlySharp = Assert.Single(sharps);
        Assert.Equal("♯", onlySharp.Text);
        Assert.True(onlySharp.X < scene.Notes[1].X, "The sharp belongs to the first measure's F sharp, not the second's.");
    }

    [Fact]
    public void BuildScore_NaturalAgainstTheNewKey_DrawsANatural()
    {
        var score = new Score(
            "keys",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([QuarterAt(NoteLetter.C, 0, 0)], []),
                new ScoreMeasure([QuarterAt(NoteLetter.F, 0, 1)], [], KeyFifths: 1),
            ]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var natural = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Accidental);
        Assert.Equal("♮", natural.Text);
    }

    [Fact]
    public void BuildScore_WindowOpeningAfterAKeyChange_ShowsTheKeyInEffectAtTheClef()
    {
        var score = KeyChangeScore(initialKey: 0, changedKey: 2, changeMeasure: 1, measureCount: 4);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 2);

        var keyGlyphs = KeySignatureGlyphs(scene);
        Assert.Equal(4, keyGlyphs.Length);
        Assert.All(keyGlyphs, glyph => Assert.True(glyph.X < GrandStaffLayout.ScoreX0));
    }

    [Fact]
    public void BuildScore_WindowOpeningOnTheKeyChange_ShowsTheNewKeyAtTheClefOnly()
    {
        var score = KeyChangeScore(initialKey: 0, changedKey: 2, changeMeasure: 2, measureCount: 4);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 2);

        var keyGlyphs = KeySignatureGlyphs(scene);
        Assert.Equal(4, keyGlyphs.Length);
        Assert.All(keyGlyphs, glyph => Assert.True(glyph.X < GrandStaffLayout.ScoreX0));
    }

    [Fact]
    public void BuildScore_KeyChangeInsideTheWindow_DrawsTheNewSignatureAtTheStartOfItsMeasure()
    {
        var score = KeyChangeScore(initialKey: 0, changedKey: 2, changeMeasure: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var keyGlyphs = KeySignatureGlyphs(scene);
        Assert.Equal(4, keyGlyphs.Length);
        double measureStartX = GrandStaffLayout.MapScoreOnsetToX(1, 0, score.TimeSignature, 0);
        double measureEndX = GrandStaffLayout.MapScoreOnsetToX(2, 0, score.TimeSignature, 0);
        Assert.All(keyGlyphs, glyph => Assert.InRange(glyph.X, measureStartX, measureEndX));
        double firstNoteX = scene.Notes.Single(note => note.Address?.MeasureIndex == 1).X;
        Assert.All(keyGlyphs, glyph => Assert.True(glyph.X < firstNoteX));
    }

    [Fact]
    public void BuildScore_KeyChangeToCMajor_CancelsTheOldAccidentalsWithNaturals()
    {
        var score = KeyChangeScore(initialKey: 3, changedKey: 0, changeMeasure: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        // Three sharps are cancelled on each staff, and the old key's own glyphs open the system.
        Assert.Equal(6, KeySignatureGlyphs(scene).Count(glyph => glyph.Text == "♮"));
        Assert.Equal(6, KeySignatureGlyphs(scene).Count(glyph => glyph.Text == "♯"));
    }

    [Fact]
    public void BuildScore_KeyChangeToFewerSharps_CancelsOnlyTheDroppedOnes()
    {
        var score = KeyChangeScore(initialKey: 3, changedKey: 1, changeMeasure: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var changeGlyphs = KeySignatureGlyphs(scene).Where(glyph => glyph.X > GrandStaffLayout.ScoreX0).ToArray();
        Assert.Equal(4, changeGlyphs.Count(glyph => glyph.Text == "♮"));
        Assert.Equal(2, changeGlyphs.Count(glyph => glyph.Text == "♯"));
    }

    [Fact]
    public void BuildScore_KeyChangeFromSharpsToFlats_CancelsEverySharpFirst()
    {
        var score = KeyChangeScore(initialKey: 2, changedKey: -2, changeMeasure: 1);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0);

        var changeGlyphs = KeySignatureGlyphs(scene).Where(glyph => glyph.X > GrandStaffLayout.ScoreX0).ToArray();
        Assert.Equal(4, changeGlyphs.Count(glyph => glyph.Text == "♮"));
        Assert.Equal(4, changeGlyphs.Count(glyph => glyph.Text == "♭"));
    }

    [Fact]
    public void BuildScore_KeyChangeMeasure_PlacesRestsAndCursorWhereTheNotesAre()
    {
        var measures = new[]
        {
            new ScoreMeasure([QuarterAt(NoteLetter.C, 0, 0)], []),
            new ScoreMeasure(
                [QuarterAt(NoteLetter.C, 0, 1, 1)],
                [new ScoreRest(new NoteValue(4), 1, 0, Staff.Treble)],
                KeyFifths: 3),
        };
        var score = new Score("keys", new TimeSignature(4, new NoteValue(4)), new Tempo(120), 0, measures);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 5, drawRests: true);

        var rest = Assert.Single(scene.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
        var cursor = Assert.Single(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        double noteX = scene.Notes.Single(note => note.Address?.MeasureIndex == 1).X;
        Assert.Equal(noteX, cursor.X0, 6);
        Assert.True(rest.X < noteX);
        var lastKeyGlyph = KeySignatureGlyphs(scene).Where(glyph => glyph.X > GrandStaffLayout.ScoreX0).MaxBy(glyph => glyph.X)!;
        Assert.True(rest.X > lastKeyGlyph.X);
    }

    [Fact]
    public void BuildScore_ImplicitPickup_PlacesTheNextBarlineAtItsActualLength()
    {
        var score = new Score(
            "pickup",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([QuarterAt(NoteLetter.C, 0, 0)], [], LengthInBeats: 1),
                new ScoreMeasure([QuarterAt(NoteLetter.D, 0, 1)], []),
            ]);

        var scene = GrandStaffSceneBuilder.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);
        float pickupEndX = GrandStaffLayout.MapScoreOnsetToX(score, 1, 0, firstVisibleMeasure: 0, visibleMeasureCount: 2);

        Assert.Contains(scene.Lines, line =>
            line.Kind == GrandStaffLineKind.Barline && Math.Abs(line.X0 - pickupEndX) < 1e-6);
    }
}
