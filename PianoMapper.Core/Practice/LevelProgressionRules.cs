namespace PianoMapper.Practice;

/// <summary>
/// Every threshold of the ladder in one place: how a level is passed and how the tempo moves. The defaults are the
/// beginner values; none of them locks anything, they only decide what is recommended.
/// </summary>
/// <param name="RequiredSessions">A level is passed when its latest this-many matching sessions all pass.</param>
/// <param name="MinimumPitchFirstTryPercent">A session passes with at least this share of prompts right with the first key.</param>
/// <param name="MinimumTimingCleanPercent">A timed session also needs this share of prompts on time (and held right).</param>
/// <param name="TempoStepPulses">How far the recommended tempo moves per step.</param>
/// <param name="StruggleBelowPercent">A session below this share of clean prompts counts as a struggle.</param>
/// <param name="StruggleSessionCount">This many struggling sessions in a row step the tempo down.</param>
public sealed record LevelProgressionRules(
    int RequiredSessions = 3,
    double MinimumPitchFirstTryPercent = 90,
    double MinimumTimingCleanPercent = 80,
    int TempoStepPulses = 5,
    double StruggleBelowPercent = 60,
    int StruggleSessionCount = 2)
{
    public static LevelProgressionRules Default { get; } = new();
}
