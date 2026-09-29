using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed record PitchAttemptSummary(
    Pitch Pitch,
    int CorrectFirstTryCount,
    int AttemptCount);
