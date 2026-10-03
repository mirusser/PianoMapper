namespace PianoMapper.Web.Practice;

/// <summary>How quickly the learner finds notes when they set the pace (self-paced sessions only).</summary>
/// <param name="MedianResponseTime">The median of the sessions' average time to find a note.</param>
/// <param name="NotesPerMinute">Timed notes divided by the time spent finding them, scaled to a minute.</param>
/// <param name="SessionCount">How many self-paced sessions the figures come from.</param>
public sealed record SpeedInsight(TimeSpan MedianResponseTime, double NotesPerMinute, int SessionCount);
