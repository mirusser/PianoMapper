namespace PianoMapper.Music;

public enum ScoreEndingType
{
    /// <summary>The volta bracket opens, on a measure's left barline.</summary>
    Start,
    /// <summary>The bracket closes with a downward hook, on a measure's right barline.</summary>
    Stop,
    /// <summary>The bracket ends without a hook, on a measure's right barline.</summary>
    Discontinue,
}
