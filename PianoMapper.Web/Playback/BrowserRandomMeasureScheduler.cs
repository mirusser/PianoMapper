using PianoMapper.Music;

namespace PianoMapper.Web.Playback;

internal static class BrowserRandomMeasureScheduler
{
    internal static IReadOnlyList<BrowserScoreAudioEvent> CreateEvents(
        RandomMeasure measure,
        TimeSpan anchor)
    {
        var events = new List<BrowserScoreAudioEvent>(measure.Events.Count);
        TimeSpan startTime = anchor;
        for (int index = 0; index < measure.Events.Count; index++)
        {
            var randomEvent = measure.Events[index];
            TimeSpan duration = MusicalTime.BeatsToDuration(randomEvent.Beats, measure.Tempo);
            events.Add(new BrowserScoreAudioEvent(
                $"random-measure-{index}",
                randomEvent.Pitch.Frequency,
                startTime,
                duration));
            startTime += duration;
        }

        return events;
    }
}
