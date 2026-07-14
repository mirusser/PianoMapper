namespace PianoMapper.Music;

internal static class MusicalTime
{
    public static double GetBeats(NoteValue noteValue, TimeSignature timeSignature) =>
        GetWholeNoteFraction(noteValue) / GetWholeNoteFraction(timeSignature.BeatNoteValue);

    public static TimeSpan ToDuration(NoteValue noteValue, TimeSignature timeSignature, Tempo tempo) =>
        BeatsToDuration(GetBeats(noteValue, timeSignature), tempo);

    public static TimeSpan BeatsToDuration(double beats, Tempo tempo) =>
        TimeSpan.FromMinutes(beats / tempo.BeatsPerMinute);

    public static double DurationToBeats(TimeSpan duration, Tempo tempo) =>
        duration.TotalMinutes * tempo.BeatsPerMinute;

    private static double GetWholeNoteFraction(NoteValue noteValue)
    {
        double dotMultiplier = 2.0 - (1.0 / Math.Pow(2.0, noteValue.Dots));
        return dotMultiplier / noteValue.Denominator;
    }
}
