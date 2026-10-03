namespace PianoMapper.Music;

/// <summary>How an exercise picks its pitches. Names are persisted nowhere, but keep them stable.</summary>
public enum SightReadingGenerationStrategy
{
    /// <summary>
    /// Every pitch of the range appears once before any repeats, and weights only bias the repeats. The default.
    /// </summary>
    CoverageFirst,

    /// <summary>
    /// A drill on weak notes: pitches are drawn in proportion to their weights from the start, and the weakest
    /// three are guaranteed at least two appearances. Never the default; the learner asks for it.
    /// </summary>
    WeaknessFirst,
}
