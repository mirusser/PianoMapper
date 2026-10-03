namespace PianoMapper.Music;

public sealed record MetronomeGrid(TimeSpan Anchor, Tempo Tempo, TimeSignature TimeSignature)
{
    private const int CompoundBeatsPerGroup = 3;
    private const int MinimumCompoundNumerator = 6;

    /// <summary>
    /// How many beats make one felt pulse: 3 in compound eighth-note meters (6/8, 9/8, 12/8, where the click should
    /// be counted ONE two three TWO two three), otherwise 1. 3/8 stays 1 because a single group of three is not a
    /// compound meter.
    /// </summary>
    public int BeatsPerGroup =>
        TimeSignature.BeatNoteValue == new NoteValue(8) &&
        TimeSignature.Numerator >= MinimumCompoundNumerator &&
        TimeSignature.Numerator % CompoundBeatsPerGroup == 0
            ? CompoundBeatsPerGroup
            : 1;

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
