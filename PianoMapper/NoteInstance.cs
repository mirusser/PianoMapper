namespace PianoMapper;

// A simple record representing a currently playing (or recently finished) note.
public sealed record NoteInstance
{
    public required string NoteName { get; init; }
    public required float Frequency { get; init; }
    public required TimeSpan StartTime { get; init; }
    public required float Duration { get; init; }
    public required short[] Samples { get; init; }
    public required int SourceId { get; init; }
    public required int BufferId { get; init; }
}