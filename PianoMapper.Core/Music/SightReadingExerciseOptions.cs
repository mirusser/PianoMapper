using PianoMapper.Practice;

namespace PianoMapper.Music;

/// <param name="Staff">
/// The staff to compose for when <paramref name="IsGrandStaff"/> is <see langword="false"/>. Ignored (any value is
/// fine) when <paramref name="IsGrandStaff"/> is <see langword="true"/>.
/// </param>
/// <param name="IsGrandStaff">
/// When <see langword="true"/>, prompts alternate between the treble and bass staves (or, with
/// <paramref name="IsHandsTogether"/>, sound both together) instead of using a single staff. Not currently supported together with <see cref="SightReadingPresetId.Chords"/>.
/// </param>
/// <param name="RhythmPreset">
/// The measure/rest vocabulary to compose with. <see cref="SightReadingRhythmPreset.Fixed"/> (the default)
/// preserves the original fixed-quarter-note, 4/4 behavior exactly. Not currently supported together with
/// <see cref="SightReadingPresetId.Chords"/>.
/// </param>
/// <param name="TempoPulsesPerMinute">
/// The learner-facing tempo in pulses per minute: a quarter note in 4/4, a dotted quarter in 6/8. The composer
/// converts it to the score's written-beat unit. <see langword="null"/> (the default) picks the beginner default for
/// the exercise's meter, see <see cref="GetDefaultTempoPulsesPerMinute"/>. Values outside
/// <see cref="MinimumTempoPulsesPerMinute"/>..<see cref="MaximumTempoPulsesPerMinute"/> are rejected by the composer.
/// </param>
/// <param name="Strategy">
/// <see cref="SightReadingGenerationStrategy.CoverageFirst"/> (the default) or the explicit weak-note drill. Only
/// applies to single-note exercises; chords are chosen as whole triads.
/// </param>
/// <param name="Motion">
/// How notes follow each other, see <see cref="SightReadingMotion"/>. <see cref="SightReadingMotion.Random"/> (the
/// default) is the original behavior. Ignored by presets that choose their own notes, see
/// <see cref="SightReadingExerciseComposer.SupportsMotion"/>, and by rhythm-only exercises.
/// </param>
/// <param name="IntervalSteps">
/// The interval of <see cref="SightReadingMotion.Intervallic"/> motion in diatonic steps: 1 is a step (second), 2 a
/// third, 3 a fourth, 4 a fifth. Only read for intervallic motion, where values outside
/// <see cref="MinimumIntervalSteps"/>..<see cref="MaximumIntervalSteps"/> are rejected by the composer.
/// </param>
/// <param name="IsHandsTogether">
/// With a grand staff, every prompt is one treble note and one bass note sounded together instead of the staves
/// alternating. Only supported for the five-note range (so the two hands never share a pitch); the composer rejects
/// it without a grand staff or with another range.
/// </param>
public sealed record SightReadingExerciseOptions(
    Staff Staff,
    SightReadingPresetId PresetId,
    int PromptCount,
    NoteReadingMode Mode,
    bool IsGrandStaff = false,
    SightReadingRhythmPreset RhythmPreset = SightReadingRhythmPreset.Fixed,
    int? TempoPulsesPerMinute = null,
    SightReadingGenerationStrategy Strategy = SightReadingGenerationStrategy.CoverageFirst,
    SightReadingMotion Motion = SightReadingMotion.Random,
    int IntervalSteps = SightReadingExerciseOptions.DefaultIntervalSteps,
    bool IsHandsTogether = false)
{
    public const int MinimumTempoPulsesPerMinute = 30;

    public const int MaximumTempoPulsesPerMinute = 200;

    public const int MinimumIntervalSteps = 1;

    public const int MaximumIntervalSteps = 4;

    public const int DefaultIntervalSteps = 2;

    private const int DefaultSimpleMeterPulsesPerMinute = 60;

    private const int DefaultCompoundMeterPulsesPerMinute = 40;

    /// <summary>
    /// The slow beginner default for a rhythm preset's pulse unit: 60 quarter-note pulses in 4/4, 40 dotted-quarter
    /// pulses in 6/8 (both a measure of roughly 3 to 4 seconds).
    /// </summary>
    public static int GetDefaultTempoPulsesPerMinute(SightReadingRhythmPreset rhythmPreset) =>
        rhythmPreset == SightReadingRhythmPreset.Compound
            ? DefaultCompoundMeterPulsesPerMinute
            : DefaultSimpleMeterPulsesPerMinute;
}
