using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>
/// A hint for the prompt the learner is stuck on. <see cref="Difference"/> is how the last wrong key differs from
/// the nearest expected pitch (measured from the key pressed to the one wanted), and is only set for a
/// direction-level hint.
/// </summary>
internal sealed record ExerciseCoachHint(
    ExerciseCoachHintLevel Level,
    IReadOnlyList<Pitch> ExpectedPitches,
    Pitch? PressedPitch,
    PitchDifference? Difference);
