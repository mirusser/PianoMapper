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
        Assert.True(secondCursor.X0 > firstCursor.X0);
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
    public void BuildScore_DrawRestsToggles_RebuildsStaticGeometryAndShowsTheRest()
    {
        var cache = new GrandStaffSceneCache();
        var score = new Score(
            "rest",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [new ScoreMeasure([], [new ScoreRest(new NoteValue(4), 0, 1, Staff.Treble)])]);

        var withoutRests = cache.BuildScore(score, firstVisibleMeasure: 0);
        var withRests = cache.BuildScore(score, firstVisibleMeasure: 0, drawRests: true);
        var withRestsAgain = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 1, drawRests: true);

        Assert.DoesNotContain(withoutRests.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
        Assert.Single(withRests.Glyphs, glyph => glyph.Kind == GrandStaffGlyphKind.Rest);
        Assert.NotSame(withoutRests.Glyphs, withRests.Glyphs);
        Assert.Same(withRests.Glyphs, withRestsAgain.Glyphs);
    }

    [Fact]
    public void BuildScore_DrawTiesToggles_RebuildsStaticGeometryAndShowsTheTie()
    {
        var cache = new GrandStaffSceneCache();
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        var score = new Score(
            "tie",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [
                new ScoreMeasure(
                    [
                        new ScoreNote(pitch, new NoteValue(4), 0, 1, Staff.Treble, TiesToNext: true),
                        new ScoreNote(pitch, new NoteValue(4), 0, 2, Staff.Treble),
                    ],
                    []),
            ]);

        var withoutTies = cache.BuildScore(score, firstVisibleMeasure: 0);
        var withTies = cache.BuildScore(score, firstVisibleMeasure: 0, drawTies: true);
        var withTiesAgain = cache.BuildScore(score, firstVisibleMeasure: 0, cursorBeats: 1, drawTies: true);

        Assert.Empty(withoutTies.Ties);
        Assert.Single(withTies.Ties);
        Assert.Same(withTies.Ties, withTiesAgain.Ties);
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

    [Fact]
    public void BuildScore_ReviewMarksSameContentDifferentDictionaryInstance_ReusesStaticGeometry()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var first = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Pitch });
        var second = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            cursorBeats: 1,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Pitch });

        Assert.Same(first.Notes, second.Notes);
    }

    [Fact]
    public void BuildScore_ReviewMarksChange_RebuildsNotesWithTheNewMark()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);

        var unmarked = cache.BuildScore(score, firstVisibleMeasure: 0);
        var timing = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Timing });
        var missed = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Missed });

        Assert.Null(Assert.Single(unmarked.Notes).ReviewMark);
        Assert.Equal(ReviewMark.Timing, Assert.Single(timing.Notes).ReviewMark);
        Assert.Equal(ReviewMark.Missed, Assert.Single(missed.Notes).ReviewMark);
    }

    [Fact]
    public void BuildScore_ReviewMarksGoBackToNone_DropsTheMarksAgain()
    {
        // The exercise's Retry leaves review: the same score is shown again with no marks.
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = SingleNoteScore(sourceNote);
        cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            reviewMarks: new Dictionary<ScoreNote, ReviewMark> { [sourceNote] = ReviewMark.Pitch });

        var afterRetry = cache.BuildScore(score, firstVisibleMeasure: 0);

        var note = Assert.Single(afterRetry.Notes);
        Assert.Null(note.ReviewMark);
        Assert.Null(note.ReviewMarkGroup);
    }

    [Fact]
    public void BuildScore_NoteReadingVerdictChanges_RebuildsNotesWithReleasedHoldVerdict()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);
        var session = new NoteReadingSession();
        session.Reset(
            score,
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        cache.BuildScore(score, firstVisibleMeasure: 0, verdicts: session.Verdicts);
        session.Check(sourceNote.Pitch, TimeSpan.Zero);
        session.Release(sourceNote.Pitch, TimeSpan.FromMilliseconds(100));
        var released = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            verdicts: session.Verdicts);

        Assert.Equal(Verdict.TooShort, Assert.Single(released.Notes).Verdict);
    }

    [Fact]
    public void BuildScore_ExpectedNotesChange_RebuildsActiveScoreNote()
    {
        var cache = new GrandStaffSceneCache();
        var firstNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var secondNote = new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 0, 1, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([firstNote, secondNote], [])]);

        var first = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            expectedNotes: new HashSet<ScoreNote> { firstNote });
        var second = cache.BuildScore(
            score,
            firstVisibleMeasure: 0,
            expectedNotes: new HashSet<ScoreNote> { secondNote });

        Assert.True(first.Notes.Single(note => note.Label == "C4").IsActive);
        Assert.False(first.Notes.Single(note => note.Label == "D4").IsActive);
        Assert.False(second.Notes.Single(note => note.Label == "C4").IsActive);
        Assert.True(second.Notes.Single(note => note.Label == "D4").IsActive);
    }

    [Fact]
    public void BuildScore_ShowNoteLabelsChanges_RebuildsNotesWithoutLabels()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var shown = cache.BuildScore(score, firstVisibleMeasure: 0, showNoteLabels: true);
        var hidden = cache.BuildScore(score, firstVisibleMeasure: 0, showNoteLabels: false);

        Assert.NotSame(shown.Notes, hidden.Notes);
        Assert.NotNull(Assert.Single(shown.Notes).LabelY);
        Assert.Null(Assert.Single(hidden.Notes).LabelY);
    }

    [Fact]
    public void BuildScore_VisibleMeasureCountUnchanged_ReusesStaticGeometryInstances()
    {
        var cache = new GrandStaffSceneCache();
        var score = CreateScore(measureCount: 6);

        var first = cache.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);
        var second = cache.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);

        Assert.Same(first.Glyphs, second.Glyphs);
        Assert.Same(first.Notes, second.Notes);
    }

    [Fact]
    public void BuildScore_VisibleMeasureCountChanges_RebuildsStaticGeometryWithNewSpacing()
    {
        var cache = new GrandStaffSceneCache();
        ScoreNote[] notes =
        [
            new(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
            new(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2, Staff.Treble),
        ];
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

        var defaultScene = cache.BuildScore(score, firstVisibleMeasure: 0);
        var narrowedScene = cache.BuildScore(score, firstVisibleMeasure: 0, visibleMeasureCount: 2);

        Assert.NotSame(defaultScene.Glyphs, narrowedScene.Glyphs);
        double defaultDistance = Math.Abs(defaultScene.Notes[1].X - defaultScene.Notes[0].X);
        double narrowedDistance = Math.Abs(narrowedScene.Notes[1].X - narrowedScene.Notes[0].X);
        Assert.True(narrowedDistance > defaultDistance);
    }

    [Fact]
    public void BuildScore_ShowFingeringsChanges_RebuildsNotesWithoutFingering()
    {
        var cache = new GrandStaffSceneCache();
        var sourceNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(2));
        var score = new Score(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([sourceNote], [])]);

        var shown = cache.BuildScore(score, firstVisibleMeasure: 0, showFingerings: true);
        var hidden = cache.BuildScore(score, firstVisibleMeasure: 0, showFingerings: false);

        Assert.NotSame(shown.Notes, hidden.Notes);
        Assert.NotNull(Assert.Single(shown.Notes).Fingering);
        Assert.Null(Assert.Single(hidden.Notes).Fingering);
    }

    private static Score SingleNoteScore(ScoreNote note) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure([note], [])]);

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
