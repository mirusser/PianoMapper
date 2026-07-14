using PianoMapper.Music;

namespace PianoMapper.Practice;

internal sealed record GradedEvent(
    ScoreEvent? Expected,
    PerformedNote? Performed,
    Verdict Verdict);
