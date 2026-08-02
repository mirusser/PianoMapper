namespace PianoMapper.Rendering;

public readonly record struct LiveNoteSegmentLayout(
    float X,
    float DurationEndX,
    double StartBeat,
    double EndBeat,
    StaffPlacement Position,
    bool HasIncomingTie,
    bool HasOutgoingTie);
