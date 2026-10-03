namespace PianoMapper.Music;

/// <summary>
/// How a single-note exercise moves from one note to the next. Names are persisted in the history as strings, so
/// add members, never rename.
/// </summary>
public enum SightReadingMotion
{
    /// <summary>
    /// The original behavior: every note of the range once before any repeats, then least-used picks, with the
    /// leap limit of the range. The only motion that mastery weights and the weak-note drill apply to.
    /// </summary>
    Random,

    /// <summary>
    /// Stepwise runs that keep their direction before turning, about a quarter of the notes repeated, and the
    /// occasional skip of a third.
    /// </summary>
    Melodic,

    /// <summary>Every note exactly the chosen number of diatonic steps above or below the previous one.</summary>
    Intervallic,
}
