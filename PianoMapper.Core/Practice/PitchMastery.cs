using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed record PitchMastery(Pitch Pitch, int CorrectFirstTryCount, int AttemptCount)
{
    public double AccuracyPercent => AttemptCount == 0
        ? 0
        : 100.0 * CorrectFirstTryCount / AttemptCount;
}
