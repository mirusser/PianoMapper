namespace PianoMapper;

/// <summary>
/// Pure sample-position math for the oscilloscope/spectrum: estimates where playback
/// is within a note's buffer from elapsed wall-clock time (used before the audio
/// thread's first live OpenAL offset arrives, or once the source has been torn down),
/// and extracts a fixed-size sample window around a given offset.
/// </summary>
internal static class PlaybackPosition
{
    public static bool IsNoteStillPlaying(PerformedNote note, TimeSpan now) =>
        !note.ReleaseTime.HasValue || now <= note.ReleaseTime.Value;

    public static int EstimateSampleOffset(PerformedNote note, TimeSpan now, int sampleCount)
    {
        var elapsedSeconds = now.TotalSeconds - note.StartTime.TotalSeconds;
        var estimated = (int)(elapsedSeconds * Consts.SampleRate);
        return Math.Clamp(estimated, 0, Math.Max(0, sampleCount - 1));
    }

    public static short[] ExtractWindow(IReadOnlyList<short> samples, int centerOffset, int windowSize)
    {
        var window = new short[windowSize];
        var half = windowSize / 2;
        var start = centerOffset - half;

        for (var i = 0; i < windowSize; i++)
        {
            var sourceIndex = start + i;
            if (sourceIndex >= 0 && sourceIndex < samples.Count)
            {
                window[i] = samples[sourceIndex];
            }
        }

        return window;
    }
}
