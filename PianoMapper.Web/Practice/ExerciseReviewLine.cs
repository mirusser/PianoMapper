using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>One prompt the learner did not get cleanly on the first try, in review terms.</summary>
/// <param name="BarNumber">The one-based bar the prompt is in.</param>
/// <param name="BeatNumber">The one-based beat within the bar, in the score's beat unit (fractions for off-beats).</param>
/// <param name="PlayedPitch">The last wrong key played for this prompt, if any.</param>
/// <param name="PitchDifference">How <paramref name="PlayedPitch"/> differs from the nearest expected pitch.</param>
/// <param name="WasMissed">Whether the prompt was never played (a play-along run moved past it).</param>
/// <param name="OnsetVerdict">Early or late; null when the start was fine or not graded.</param>
/// <param name="OnsetDeviation">The signed deviation that goes with <paramref name="OnsetVerdict"/> (positive is late).</param>
/// <param name="DurationVerdict">Too short or too long; null when the release was fine or not graded.</param>
public sealed record ExerciseReviewLine(
    int BarNumber,
    double BeatNumber,
    IReadOnlyList<Pitch> ExpectedPitches,
    Pitch? PlayedPitch,
    PitchDifference? PitchDifference,
    bool WasMissed,
    Verdict? OnsetVerdict,
    TimeSpan? OnsetDeviation,
    Verdict? DurationVerdict);
