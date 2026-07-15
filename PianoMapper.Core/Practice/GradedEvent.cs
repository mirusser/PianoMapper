using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed record GradedEvent(
    ScoreEvent? Expected,
    PerformedNote? Performed,
    Verdict Verdict);
