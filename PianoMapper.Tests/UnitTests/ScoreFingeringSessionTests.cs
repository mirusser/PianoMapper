using PianoMapper.Music;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringSessionTests
{
    private static readonly ScoreFingeringNoteAddress FirstNote = new(0, 0);

    [Fact]
    public void ToggleLock_SameNoteTwice_LocksItToTheGivenFingerThenUnlocksIt()
    {
        var session = new ScoreFingeringSession();

        bool lockedFirst = session.ToggleLock(FirstNote, 3);

        Assert.True(lockedFirst);
        Assert.True(session.IsLocked(FirstNote));
        ScoreFingeringLock fingeringLock = Assert.Single(session.CreateLocks());
        Assert.Equal(FirstNote, fingeringLock.Address);
        Assert.Equal(3, fingeringLock.FingerNumber);

        bool lockedSecond = session.ToggleLock(FirstNote, 3);

        Assert.False(lockedSecond);
        Assert.False(session.IsLocked(FirstNote));
        Assert.False(session.HasLocks);
    }

    [Fact]
    public void FollowEdit_LockedNote_MovesWithItsNewFingerAndGoesWhenTheFingerIsRemoved()
    {
        var session = new ScoreFingeringSession();
        session.ToggleLock(FirstNote, 3);

        session.FollowEdit(FirstNote, 5);

        Assert.Equal(5, Assert.Single(session.CreateLocks()).FingerNumber);

        session.FollowEdit(FirstNote, null);

        Assert.False(session.HasLocks);
    }

    [Fact]
    public void FollowEdit_UnlockedNote_DoesNotCreateALock()
    {
        var session = new ScoreFingeringSession();

        session.FollowEdit(FirstNote, 2);

        Assert.False(session.HasLocks);
    }

    [Fact]
    public void SelectAlternative_AfterShowAlternatives_StartsOnTheFirstAndReturnsTheChosenOne()
    {
        var session = new ScoreFingeringSession();
        IReadOnlyList<ScoreFingeringAlternative> alternatives = CreateAlternatives(3);

        session.ShowAlternatives(alternatives);

        Assert.Equal(0, session.SelectedAlternativeIndex);
        Assert.Same(alternatives[2], session.SelectAlternative(2));
        Assert.Equal(2, session.SelectedAlternativeIndex);
    }

    [Fact]
    public void ClearAlternatives_DropsThemButKeepsTheLocks()
    {
        var session = new ScoreFingeringSession();
        session.ToggleLock(FirstNote, 3);
        session.ShowAlternatives(CreateAlternatives(3));
        session.SelectAlternative(1);

        session.ClearAlternatives();

        Assert.Empty(session.Alternatives);
        Assert.Equal(0, session.SelectedAlternativeIndex);
        Assert.True(session.HasLocks);
    }

    [Fact]
    public void Clear_DropsLocksAndAlternativesSoTheyNeverReachAnotherScore()
    {
        var session = new ScoreFingeringSession();
        session.ToggleLock(FirstNote, 3);
        session.ShowAlternatives(CreateAlternatives(3));

        session.Clear();

        Assert.False(session.HasLocks);
        Assert.Empty(session.Alternatives);
    }

    private static IReadOnlyList<ScoreFingeringAlternative> CreateAlternatives(int count)
    {
        var score = new Score("Alternatives", new TimeSignature(4, new NoteValue(4)), new Tempo(120), 0, []);
        return Enumerable.Range(0, count)
            .Select(index => new ScoreFingeringAlternative(score, index, $"Alternative {index + 1}"))
            .ToArray();
    }
}
