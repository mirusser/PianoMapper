namespace PianoMapper.Rendering;

public readonly record struct LiveNoteLayout(
    float X,
    float DurationEndX,
    StaffPlacement Position);
