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
    double? StemEndY = null,
    double? LabelY = null,
    double LabelFontScale = 1.0,
    double? ScoreOnsetBeats = null,
    double? ScoreEndBeats = null,
    string? Fingering = null,
    double? FingeringY = null,
    ScoreNoteAddress? Address = null,
    ReviewMark? ReviewMark = null,
    int? ReviewMarkGroup = null,
    // A live held note whose duration trail the browser grows from the audio clock, like the cursor: the X it must not
    // grow past, the end of its measure. Such a note has no DurationEndX of its own.
    double? DurationGrowthLimitX = null);
