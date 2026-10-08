namespace PianoMapper.Music;

/// <summary>Identifies a note in one score instance for a fingering lock or diagnostic.</summary>
public readonly record struct ScoreFingeringNoteAddress(int MeasureIndex, int NoteIndex);
