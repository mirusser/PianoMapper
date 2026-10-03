using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Web.Components;

/// <summary>One learner intention from the note-reading exercise presentation module.</summary>
public abstract record SightReadingExerciseAction
{
    public sealed record SelectStaff(Staff Value) : SightReadingExerciseAction;

    public sealed record SelectPreset(SightReadingPresetId Value) : SightReadingExerciseAction;

    public sealed record SelectPromptCount(int Value) : SightReadingExerciseAction;

    public sealed record SelectMotion(SightReadingMotion Value) : SightReadingExerciseAction;

    public sealed record SelectIntervalSteps(int Value) : SightReadingExerciseAction;

    public sealed record SelectGrandStaff(bool Value) : SightReadingExerciseAction;

    public sealed record SetHandsTogether(bool Value) : SightReadingExerciseAction;

    public sealed record SelectRhythmPreset(SightReadingRhythmPreset Value) : SightReadingExerciseAction;

    public sealed record SelectMode(NoteReadingMode Value) : SightReadingExerciseAction;

    public sealed record SelectTempo(int Value) : SightReadingExerciseAction;

    public sealed record SelectPacing(ExercisePacing Value) : SightReadingExerciseAction;

    public sealed record SetRevealNoteNames(bool Value) : SightReadingExerciseAction;

    public sealed record SetRevealFingering(bool Value) : SightReadingExerciseAction;

    public sealed record SetRevealKeys(bool Value) : SightReadingExerciseAction;

    public sealed record SetClickWhilePlaying(bool Value) : SightReadingExerciseAction;

    public sealed record SetCoachHints(bool Value) : SightReadingExerciseAction;

    public sealed record Generate : SightReadingExerciseAction;

    public sealed record DrillWeakNotes : SightReadingExerciseAction;

    public sealed record StartRecommended : SightReadingExerciseAction;

    public sealed record Retry : SightReadingExerciseAction;

    public sealed record RetryMissed : SightReadingExerciseAction;

    public sealed record End : SightReadingExerciseAction;
}
