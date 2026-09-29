using PianoMapper.Practice;

namespace PianoMapper.Music;

/// <param name="Staff">
/// The staff to compose for when <paramref name="IsGrandStaff"/> is <see langword="false"/>. Ignored (any value is
/// fine) when <paramref name="IsGrandStaff"/> is <see langword="true"/>.
/// </param>
/// <param name="IsGrandStaff">
/// When <see langword="true"/>, prompts alternate between the treble and bass staves instead of using a single
/// staff. Not currently supported together with <see cref="SightReadingPresetId.Chords"/>.
/// </param>
/// <param name="RhythmPreset">
/// The measure/rest vocabulary to compose with. <see cref="SightReadingRhythmPreset.Fixed"/> (the default)
/// preserves the original fixed-quarter-note, 4/4 behavior exactly. Not currently supported together with
/// <see cref="SightReadingPresetId.Chords"/>.
/// </param>
public sealed record SightReadingExerciseOptions(
    Staff Staff,
    SightReadingPresetId PresetId,
    int PromptCount,
    NoteReadingMode Mode,
    bool IsGrandStaff = false,
    SightReadingRhythmPreset RhythmPreset = SightReadingRhythmPreset.Fixed);
