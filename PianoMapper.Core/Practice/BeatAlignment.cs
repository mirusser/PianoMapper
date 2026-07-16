using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed record BeatAlignment(
    long BeatIndex,
    TimeSpan Deviation,
    Verdict Verdict,
    bool IsDownbeat)
{
    public static BeatAlignment Classify(
        TimeSpan onset,
        MetronomeGrid grid,
        TimeSpan onTimeTolerance)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfLessThan(onTimeTolerance, TimeSpan.Zero);

        long beatIndex = grid.GetNearestBeatIndex(onset);
        TimeSpan deviation = onset - grid.GetBeatTime(beatIndex);
        Verdict verdict = deviation.Duration() <= onTimeTolerance
            ? Verdict.Correct
            : deviation < TimeSpan.Zero
                ? Verdict.Early
                : Verdict.Late;
        bool isDownbeat = beatIndex % grid.TimeSignature.Numerator == 0;
        return new BeatAlignment(beatIndex, deviation, verdict, isDownbeat);
    }
}
