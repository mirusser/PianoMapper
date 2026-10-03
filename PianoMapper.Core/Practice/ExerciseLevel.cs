using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// One step of the beginner ladder: a complete set of exercise options plus, for timed levels, the tempo the learner
/// starts at and the fastest tempo the ladder will recommend. A pure description; it knows nothing of history or UI.
/// </summary>
/// <param name="StartTempoPulsesPerMinute">Null for levels that are not graded on timing.</param>
/// <param name="MaximumTempoPulsesPerMinute">Null for levels that are not graded on timing.</param>
public sealed record ExerciseLevel(
    int Number,
    string Name,
    Staff Staff,
    bool IsGrandStaff,
    SightReadingPresetId PresetId,
    NoteReadingMode Mode,
    SightReadingRhythmPreset RhythmPreset,
    int PromptCount,
    int? StartTempoPulsesPerMinute,
    int? MaximumTempoPulsesPerMinute)
{
    public bool IsTimed => StartTempoPulsesPerMinute is not null;

    /// <summary>The exercise options for this level, at <paramref name="tempoPulsesPerMinute"/> (or the level default).</summary>
    public SightReadingExerciseOptions CreateOptions(int? tempoPulsesPerMinute = null) =>
        new(
            Staff,
            PresetId,
            PromptCount,
            Mode,
            IsGrandStaff,
            RhythmPreset,
            IsTimed ? tempoPulsesPerMinute ?? StartTempoPulsesPerMinute : null);
}
