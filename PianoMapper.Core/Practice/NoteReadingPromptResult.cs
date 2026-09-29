using System.Collections.Immutable;
using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed record NoteReadingPromptResult(
    int PromptIndex,
    double OnsetBeats,
    ImmutableArray<ScoreNote> ExpectedSourceNotes,
    ImmutableArray<Pitch> ExpectedPitches,
    ImmutableArray<Pitch> WrongPlayedPitches,
    int WrongAttemptCount,
    bool IsFirstTryCorrect,
    bool IsComplete,
    TimeSpan? CompletedAt);
