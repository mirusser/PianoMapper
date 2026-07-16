namespace PianoMapper.Rendering;

public readonly record struct ScoreNoteLayout(
    float X,
    StaffPlacement Position,
    NoteHeadStyle HeadStyle,
    StemDirection StemDirection,
    bool HasStem,
    bool HasDot,
    int FlagCount);
