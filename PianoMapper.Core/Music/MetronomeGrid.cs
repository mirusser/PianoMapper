namespace PianoMapper.Music;

public sealed record MetronomeGrid(TimeSpan Anchor, Tempo Tempo, TimeSignature TimeSignature)
{
    public TimeSpan BeatDuration => MusicalTime.BeatsToDuration(1, Tempo);

    public double GetBeatsElapsed(TimeSpan time) =>
        MusicalTime.DurationToBeats(time - Anchor, Tempo);

    public long GetNearestBeatIndex(TimeSpan time) =>
        checked((long)Math.Round(GetBeatsElapsed(time), MidpointRounding.AwayFromZero));

    public TimeSpan GetBeatTime(long beatIndex) =>
        Anchor + MusicalTime.BeatsToDuration(beatIndex, Tempo);

    public TimeSpan GetDeviation(TimeSpan time) =>
        time - GetBeatTime(GetNearestBeatIndex(time));
}
