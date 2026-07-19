using PianoMapper.Rendering;
using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffNote(
    string Label,
    double X,
    double Y,
    double DurationSeconds,
    bool IsActive,
    bool IsFilled = true,
    bool HasStem = false,
    StemDirection StemDirection = StemDirection.Up,
    bool HasDot = false,
    int FlagCount = 0,
    Verdict? Verdict = null,
    double? DurationEndX = null,
    double? StemEndY = null);
