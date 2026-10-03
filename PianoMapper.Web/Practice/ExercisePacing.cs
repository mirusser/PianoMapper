namespace PianoMapper.Web.Practice;

/// <summary>
/// How an exercise is paced. <see cref="WaitForMe"/> waits for the right key before moving on (the original
/// behavior). <see cref="PlayAlong"/> follows the clock, moves on whether or not a note was played, and grades what
/// was missed or extra. Play-along needs a beat to follow, so it applies only to onset-graded modes.
/// </summary>
public enum ExercisePacing
{
    WaitForMe,
    PlayAlong,
}
