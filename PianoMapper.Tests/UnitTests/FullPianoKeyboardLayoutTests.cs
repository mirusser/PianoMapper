using PianoMapper.Music;
using PianoMapper.Web.Pages;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class FullPianoKeyboardLayoutTests
{
    [Fact]
    public void Build_NoActiveNotes_CreatesStandardEightyEightKeyRange()
    {
        var keys = FullPianoKeyboardLayout.Build(new HashSet<int>());

        Assert.Equal(88, keys.Count);
        Assert.Equal(52, keys.Count(key => !key.IsBlack));
        Assert.Equal(36, keys.Count(key => key.IsBlack));
        Assert.Equal("A0", keys[0].Pitch.ToString());
        Assert.Equal(21, keys[0].Pitch.MidiNumber);
        Assert.Equal("C8", keys[^1].Pitch.ToString());
        Assert.Equal(108, keys[^1].Pitch.MidiNumber);
        Assert.All(keys, key => Assert.InRange(key.LeftPercent, 0, 100 - key.WidthPercent));
    }

    [Fact]
    public void Build_ActiveMidiNumbers_HighlightsOnlyMatchingKeys()
    {
        var keys = FullPianoKeyboardLayout.Build(new HashSet<int> { 21, 61, 108, 200 });

        Assert.Equal([21, 61, 108], keys.Where(key => key.IsActive).Select(key => key.Pitch.MidiNumber));
        Assert.True(keys.Single(key => key.Pitch.MidiNumber == 61).IsBlack);
    }

    [Fact]
    public void Build_NextMidiNumbers_FlagsOnlyMatchingKeysIndependentlyOfActiveKeys()
    {
        var keys = FullPianoKeyboardLayout.Build(
            new HashSet<int> { 60 },
            new HashSet<int> { 60, 64, 200 });

        Assert.Equal([60, 64], keys.Where(key => key.IsNext).Select(key => key.Pitch.MidiNumber));
        Assert.Equal([60], keys.Where(key => key.IsActive).Select(key => key.Pitch.MidiNumber));
    }

    [Fact]
    public void Build_NoNextMidiNumbers_FlagsNoKeysAsNext()
    {
        var keys = FullPianoKeyboardLayout.Build(new HashSet<int> { 60 });

        Assert.DoesNotContain(keys, key => key.IsNext);
    }

    [Fact]
    public void GetNextKeyboardMidiNumbers_HintsDisabled_ReturnsNothing()
    {
        var nextKeys = Piano.GetNextKeyboardMidiNumbers(
            isEnabled: false,
            isPracticeActive: true,
            practiceNextPitches: [new Pitch(NoteLetter.C, 0, 4)],
            isNoteCheckingActive: true,
            expectedNotes: [CreateNote(NoteLetter.D)]);

        Assert.Empty(nextKeys);
    }

    [Fact]
    public void GetNextKeyboardMidiNumbers_PracticeActive_UsesPracticePitchesNotExpectedNotes()
    {
        var nextKeys = Piano.GetNextKeyboardMidiNumbers(
            isEnabled: true,
            isPracticeActive: true,
            practiceNextPitches: [new Pitch(NoteLetter.C, 0, 4), new Pitch(NoteLetter.E, 0, 4)],
            isNoteCheckingActive: false,
            expectedNotes: [CreateNote(NoteLetter.D)]);

        Assert.Equal([60, 64], nextKeys.Order());
    }

    [Fact]
    public void GetNextKeyboardMidiNumbers_NoteCheckingActive_UsesExpectedNotePitches()
    {
        var nextKeys = Piano.GetNextKeyboardMidiNumbers(
            isEnabled: true,
            isPracticeActive: false,
            practiceNextPitches: [],
            isNoteCheckingActive: true,
            expectedNotes: [CreateNote(NoteLetter.D), CreateNote(NoteLetter.F)]);

        Assert.Equal([62, 65], nextKeys.Order());
    }

    [Fact]
    public void GetNextKeyboardMidiNumbers_NeitherPracticeNorNoteCheckingActive_ReturnsNothing()
    {
        var nextKeys = Piano.GetNextKeyboardMidiNumbers(
            isEnabled: true,
            isPracticeActive: false,
            practiceNextPitches: [new Pitch(NoteLetter.C, 0, 4)],
            isNoteCheckingActive: false,
            expectedNotes: [CreateNote(NoteLetter.D)]);

        Assert.Empty(nextKeys);
    }

    [Fact]
    public void GetActiveKeyboardMidiNumbers_LiveAndScoreNotes_ReturnsOnlyCurrentlySoundingPitches()
    {
        var liveTimeline = new NoteTimeline();
        var heldLiveNote = liveTimeline.Start(new Pitch(NoteLetter.C, 0, 4), TimeSpan.FromSeconds(1));
        var releasedLiveNote = liveTimeline.Start(new Pitch(NoteLetter.D, 0, 4), TimeSpan.FromSeconds(1));
        liveTimeline.Complete(releasedLiveNote, TimeSpan.FromSeconds(2));
        var scoreTimeline = new NoteTimeline();
        var soundingScoreNote = scoreTimeline.Start(new Pitch(NoteLetter.E, 0, 4), TimeSpan.FromSeconds(2));
        scoreTimeline.Complete(soundingScoreNote, TimeSpan.FromSeconds(4));
        var futureScoreNote = scoreTimeline.Start(new Pitch(NoteLetter.F, 0, 4), TimeSpan.FromSeconds(4));
        scoreTimeline.Complete(futureScoreNote, TimeSpan.FromSeconds(5));

        var activeNotes = Piano.GetActiveKeyboardMidiNumbers(
            liveTimeline.Snapshot(TimeSpan.FromSeconds(3)),
            scoreTimeline.Snapshot(TimeSpan.FromSeconds(3)),
            TimeSpan.FromSeconds(3),
            new Pitch(NoteLetter.A, 0, 4));

        Assert.Equal(
            [60, 64, 69],
            activeNotes.Order());
        Assert.Contains(heldLiveNote.Pitch.MidiNumber, activeNotes);
    }

    private static ScoreNote CreateNote(NoteLetter letter) =>
        new(new Pitch(letter, 0, 4), new NoteValue(4), 0, 0, Staff.Treble);
}
