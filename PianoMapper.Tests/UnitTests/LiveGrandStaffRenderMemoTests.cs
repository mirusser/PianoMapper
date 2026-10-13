using PianoMapper.Music;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class LiveGrandStaffRenderMemoTests
{
    private static readonly TimeSignature FourFour = new(4, new NoteValue(4));
    private static readonly Tempo Bpm120 = new(120);

    // 120 BPM: one beat is half a second; a 4/4 measure is two seconds; a five-measure page is ten seconds.
    private static TimeSpan AtSeconds(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void GetOrBuild_ReleasedNotesOnTheSamePage_BuildsOnceAndReusesTheState()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [ReleasedNote(NoteLetter.C, 1, 2)];

        var first = Get(memo, builder, notes, AtSeconds(3));
        var later = Get(memo, builder, notes, AtSeconds(6));

        Assert.Equal(1, builder.Builds);
        Assert.Same(first.UpperScene, later.UpperScene);
        Assert.Same(first.LowerScene, later.LowerScene);
        Assert.Equal(first.UpperCursor, later.UpperCursor);
    }

    [Fact]
    public void GetOrBuild_NoNotes_ReusesTheStateWhileThePageStays()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();

        Get(memo, builder, [], AtSeconds(0));
        Get(memo, builder, [], AtSeconds(2));
        Get(memo, builder, [], AtSeconds(9));

        Assert.Equal(1, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteInOneMeasure_ReusesTheStateBecauseTheBrowserGrowsItsTail()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [HeldNote(NoteLetter.C, 2.2)];

        var first = Get(memo, builder, notes, AtSeconds(2.4));
        var later = Get(memo, builder, notes, AtSeconds(2.416));
        var latest = Get(memo, builder, notes, AtSeconds(3.9));

        Assert.Equal(1, builder.Builds);
        Assert.Same(first.UpperScene, later.UpperScene);
        Assert.Same(first.UpperScene, latest.UpperScene);
    }

    [Fact]
    public void GetOrBuild_HeldNoteIntoTheNextMeasure_Rebuilds()
    {
        // The held note splits at the barline into a finished head and a new active one, which only a rebuild draws.
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [HeldNote(NoteLetter.C, 1)];

        Get(memo, builder, notes, AtSeconds(1.9));
        Get(memo, builder, notes, AtSeconds(1.99));
        Get(memo, builder, notes, AtSeconds(2.01));
        Get(memo, builder, notes, AtSeconds(3.9));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteIntoTheNextPage_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [HeldNote(NoteLetter.C, 9)];

        Get(memo, builder, notes, AtSeconds(9.9));
        Get(memo, builder, notes, AtSeconds(10.1));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteBuiltExactlyOnABarline_IsNotRemembered()
    {
        // A note held across a barline is drawn as a finished head plus an active head only once the clock is strictly
        // past the barline; built on the barline itself, the scene has no active head yet (and, on a page change, an
        // empty new page). That scene must not stand in for the rest of the measure.
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [HeldNote(NoteLetter.C, 1)];

        Get(memo, builder, notes, AtSeconds(2.0));
        Get(memo, builder, notes, AtSeconds(2.016));
        Get(memo, builder, notes, AtSeconds(2.032));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteWhoseStartTheClockHasNotReached_IsNotRemembered()
    {
        // The clock the staff is drawn from can trail the clock a key's time is mapped through, and a note that has not
        // started yet has no head in the scene.
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [HeldNote(NoteLetter.C, 2.5)];

        Get(memo, builder, notes, AtSeconds(2.4));
        Get(memo, builder, notes, AtSeconds(2.5));
        Get(memo, builder, notes, AtSeconds(2.516));
        Get(memo, builder, notes, AtSeconds(2.532));

        Assert.Equal(3, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteAcrossABarlineOrPageChange_AlwaysReturnsWhatABuildAtThatTimeWouldShow()
    {
        // 16 ms ticks, including ones that land exactly on a barline and on a page change.
        var memo = new LiveGrandStaffRenderMemo();
        PerformedNote[] notes = [HeldNote(NoteLetter.G, 7.3), HeldNote(NoteLetter.E, 8.9), HeldNote(NoteLetter.C, 19.3)];
        var ticks = Enumerable.Range(0, 200).Select(index => AtSeconds(5 + (index * 0.016)))
            .Concat(Enumerable.Range(0, 100).Select(index => AtSeconds(19 + (index * 0.016))))
            .Concat([AtSeconds(6), AtSeconds(8), AtSeconds(10), AtSeconds(20), AtSeconds(20.016), AtSeconds(22)])
            .OrderBy(tick => tick)
            .ToArray();

        foreach (TimeSpan tick in ticks)
        {
            var remembered = memo.GetOrBuild(notes, tick, FourFour, Bpm120, 4, true, () => BuildFresh(notes, tick));
            var fresh = BuildFresh(notes, tick);

            Assert.True(
                remembered.UpperScene.Notes.SequenceEqual(fresh.UpperScene.Notes) &&
                remembered.LowerScene.Notes.SequenceEqual(fresh.LowerScene.Notes) &&
                remembered.UpperScene.Ties.SequenceEqual(fresh.UpperScene.Ties) &&
                remembered.LowerScene.Ties.SequenceEqual(fresh.LowerScene.Ties),
                $"The remembered state at {tick.TotalSeconds:F3} s differs from a build at that time.");
        }
    }

    [Fact]
    public void GetOrBuild_NoteHeldThenFirstKeyPressedLater_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote first = HeldNote(NoteLetter.C, 1);
        PerformedNote second = HeldNote(NoteLetter.E, 1.4);

        Get(memo, builder, [first], AtSeconds(1.2));
        Get(memo, builder, [first, second], AtSeconds(1.5));
        Get(memo, builder, [first, second], AtSeconds(1.6));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_HeldNoteReleased_BuildsOnceMoreThenReuses()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote note = HeldNote(NoteLetter.C, 1);

        Get(memo, builder, [note], AtSeconds(2));
        note.ReleaseTime = AtSeconds(2.5);
        Get(memo, builder, [note], AtSeconds(2.6));
        Get(memo, builder, [note], AtSeconds(2.7));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_NewNote_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote first = ReleasedNote(NoteLetter.C, 1, 2);
        PerformedNote second = ReleasedNote(NoteLetter.E, 3, 4);

        Get(memo, builder, [first], AtSeconds(3));
        Get(memo, builder, [first, second], AtSeconds(3.1));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_NoteExpiredFromTheTimeline_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote first = ReleasedNote(NoteLetter.C, 1, 2);
        PerformedNote second = ReleasedNote(NoteLetter.E, 3, 4);

        Get(memo, builder, [first, second], AtSeconds(5));
        Get(memo, builder, [second], AtSeconds(5.1));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_ADifferentNoteWithTheSameValues_IsTheSameInput()
    {
        // The scene depends only on what a note is, not on which instance holds it.
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();

        Get(memo, builder, [ReleasedNote(NoteLetter.C, 1, 2)], AtSeconds(3));
        Get(memo, builder, [ReleasedNote(NoteLetter.C, 1, 2)], AtSeconds(3.1));

        Assert.Equal(1, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_NextPage_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [ReleasedNote(NoteLetter.C, 1, 2)];

        Get(memo, builder, notes, AtSeconds(9.9));
        Get(memo, builder, notes, AtSeconds(10.1));

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_OctaveChanged_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [ReleasedNote(NoteLetter.C, 1, 2)];

        Get(memo, builder, notes, AtSeconds(3), selectedOctave: 4);
        Get(memo, builder, notes, AtSeconds(3.1), selectedOctave: 5);

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_NoteLabelsToggled_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [ReleasedNote(NoteLetter.C, 1, 2)];

        Get(memo, builder, notes, AtSeconds(3), showNoteLabels: true);
        Get(memo, builder, notes, AtSeconds(3.1), showNoteLabels: false);

        Assert.Equal(2, builder.Builds);
    }

    [Fact]
    public void GetOrBuild_TimingChanged_Rebuilds()
    {
        var memo = new LiveGrandStaffRenderMemo();
        var builder = new CountingBuilder();
        PerformedNote[] notes = [ReleasedNote(NoteLetter.C, 1, 2)];

        Get(memo, builder, notes, AtSeconds(3), tempo: new Tempo(120));
        Get(memo, builder, notes, AtSeconds(3.1), tempo: new Tempo(100));
        Get(memo, builder, notes, AtSeconds(3.2), tempo: new Tempo(100), timeSignature: new TimeSignature(3, new NoteValue(4)));

        Assert.Equal(3, builder.Builds);
    }

    private static LiveGrandStaffRenderState Get(
        LiveGrandStaffRenderMemo memo,
        CountingBuilder builder,
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        int? selectedOctave = 4,
        bool showNoteLabels = true,
        Tempo? tempo = null,
        TimeSignature? timeSignature = null) =>
        memo.GetOrBuild(
            notes,
            currentTime,
            timeSignature ?? FourFour,
            tempo ?? Bpm120,
            selectedOctave,
            showNoteLabels,
            builder.Build);

    private static LiveGrandStaffRenderState BuildFresh(IReadOnlyList<PerformedNote> notes, TimeSpan currentTime)
    {
        var (upper, lower) = GrandStaffSceneBuilder.BuildLivePair(
            notes,
            currentTime,
            FourFour,
            Bpm120,
            selectedOctave: 4,
            growsActiveNotesInBrowser: true);
        return new LiveGrandStaffRenderState(upper, null, lower, null);
    }

    private static PerformedNote ReleasedNote(NoteLetter letter, double startSeconds, double releaseSeconds) =>
        new()
        {
            Pitch = new Pitch(letter, 0, 4),
            StartTime = AtSeconds(startSeconds),
            ReleaseTime = AtSeconds(releaseSeconds),
        };

    private static PerformedNote HeldNote(NoteLetter letter, double startSeconds) =>
        new()
        {
            Pitch = new Pitch(letter, 0, 4),
            StartTime = AtSeconds(startSeconds),
        };

    private sealed class CountingBuilder
    {
        public int Builds { get; private set; }

        public LiveGrandStaffRenderState Build()
        {
            Builds++;
            return new LiveGrandStaffRenderState(
                new GrandStaffScene([], [], []),
                null,
                new GrandStaffScene([], [], []),
                null);
        }
    }
}
