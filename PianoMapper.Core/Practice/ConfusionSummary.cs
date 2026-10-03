using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// A wrong key played for an expected note, and how often it happened in one session: "asked for C4 on the bass
/// staff, played D4, three times".
/// </summary>
public sealed record ConfusionSummary(Pitch Expected, Pitch Played, Staff Staff, int Count);
