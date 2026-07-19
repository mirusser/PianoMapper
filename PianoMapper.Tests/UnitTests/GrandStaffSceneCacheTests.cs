using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class GrandStaffSceneCacheTests
{
    [Fact]
    public void BuildScore_SameScoreWindowAndVerdicts_ReusesStaticGeometryInstances()
    {
        var cache = new GrandStaffSceneCache();
        var score = CreateScore(measureCount: 6);

        var first = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 1);
        var second = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 2);

        Assert.Same(first.Glyphs, second.Glyphs);
        Assert.Same(first.Notes, second.Notes);
    }

    [Fact]
    public void BuildScore_CursorBeatsChange_MovesCursorLineWithoutChangingItsCount()
    {
        var cache = new GrandStaffSceneCache();
        var score = CreateScore(measureCount: 6);

        var first = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 1);
        var second = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 2);

        var firstCursor = Assert.Single(first.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        var secondCursor = Assert.Single(second.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.NotEqual(firstCursor.X0, secondCursor.X0);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(2, score.TimeSignature, firstVisibleMeasure: 0),
            secondCursor.X0,
            6);
    }

    [Fact]
    public void BuildScore_NoCursorBeats_OmitsCursorLine()
    {
        var cache = new GrandStaffSceneCache();
        var score = CreateScore(measureCount: 6);

        var scene = cache.BuildScore(score, firstVisibleMeasure: 0);

        Assert.DoesNotContain(scene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
    }

    [Fact]
    public void BuildScore_DifferentScoreInstance_RebuildsStaticGeometry()
    {
        var cache = new GrandStaffSceneCache();
        var firstScore = CreateScore(measureCount: 6);
        var secondScore = CreateScore(measureCount: 6);

        var first = cache.BuildScore(firstScore, firstVisibleMeasure: 0);
        var second = cache.BuildScore(secondScore, firstVisibleMeasure: 0);

        Assert.NotSame(first.Glyphs, second.Glyphs);
    }

    [Fact]
    public void BuildScore_FirstVisibleMeasureChanges_RebuildsStaticGeometry()
    {
        var cache = new GrandStaffSceneCache();
        var score = CreateScore(measureCount: 6);

        var first = cache.BuildScore(score, firstVisibleMeasure: 0);
        var second = cache.BuildScore(score, firstVisibleMeasure: 4);

        Assert.NotSame(first.Glyphs, second.Glyphs);
    }

    [Fact]
    public void BuildScore_VerdictsSameContentDifferentDictionaryInstance_ReusesStaticGeometry()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var first = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: new Dictionary<ScoreNote, Verdict> { [sourceNote] = Verdict.Correct });
        var second = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: new Dictionary<ScoreNote, Verdict> { [sourceNote] = Verdict.Correct });

        Assert.Same(first.Notes, second.Notes);
    }

    [Fact]
    public void BuildScore_VerdictsContentChanges_RebuildsNotesWithNewVerdict()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        cache.BuildScore(score, firstVisibleMeasure: 0);
        var withVerdict = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: new Dictionary<ScoreNote, Verdict> { [sourceNote] = Verdict.Late });

        Assert.Equal(Verdict.Late, Assert.Single(withVerdict.Notes).Verdict);
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
