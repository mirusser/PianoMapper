namespace PianoMapper.Practice;

/// <summary>Where the learner stands on a level. Deliberately has no "locked": every level stays playable.</summary>
public enum LevelStatus
{
    /// <summary>No matching session yet.</summary>
    NotTried,

    /// <summary>Some matching sessions, not passed, and not the next step.</summary>
    InProgress,

    /// <summary>The next step: the first level not yet passed.</summary>
    Recommended,

    Passed,
}
