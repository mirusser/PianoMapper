namespace PianoMapper.Music;

public enum PitchDifferenceKind
{
    /// <summary>The same piano key (identical pitches, or an enharmonic respelling such as C# and Db).</summary>
    SameKey,

    /// <summary>The same letter and octave with another accidental.</summary>
    Accidental,

    /// <summary>A different letter, measured in steps on the staff.</summary>
    Interval,

    /// <summary>The same letter in another octave.</summary>
    Octaves,
}
