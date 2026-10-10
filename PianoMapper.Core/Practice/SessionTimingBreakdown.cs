namespace PianoMapper.Practice;

/// <summary>How a timed session's prompts split between pitch and timing, as <see cref="SightReadingSessionSummaryExtensions.GetTimingBreakdown"/> reads them.</summary>
/// <param name="PitchFirstTryCorrectCount">Prompts whose pitches were found without a wrong key, whatever their timing.</param>
/// <param name="TimingMistakeCount">Prompts with at least one early, late, too-short or too-long outcome.</param>
public sealed record SessionTimingBreakdown(int PitchFirstTryCorrectCount, int TimingMistakeCount);
