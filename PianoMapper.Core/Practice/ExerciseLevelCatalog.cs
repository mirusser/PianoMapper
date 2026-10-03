using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// The static beginner ladder, in order. New presets and rhythms append levels here when they ship (levels 1-15 are the
/// original ladder; later ones follow it in the order their presets shipped). Data only: a
/// catalog test composes every level through the exercise composer, so an invalid combination cannot be added.
/// </summary>
public static class ExerciseLevelCatalog
{
    private const int PromptCount = 8;

    public static IReadOnlyList<ExerciseLevel> Levels { get; } =
    [
        Untimed(1, "Five notes · Treble · Pitch only", Staff.Treble, SightReadingPresetId.FiveNote),
        Untimed(2, "Five notes · Bass · Pitch only", Staff.Bass, SightReadingPresetId.FiveNote),
        Untimed(3, "Five notes · Grand staff · Pitch only", Staff.Treble, SightReadingPresetId.FiveNote, isGrandStaff: true),
        Untimed(4, "One octave · Treble · Pitch only", Staff.Treble, SightReadingPresetId.OneOctave),
        Untimed(5, "One octave · Bass · Pitch only", Staff.Bass, SightReadingPresetId.OneOctave),
        Timed(
            6,
            "Rhythm only · Quarter notes",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.Fixed,
            start: 60,
            maximum: 100),
        Timed(
            7,
            "Rhythm only · Basic 4/4",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.Basic,
            start: 60,
            maximum: 100),
        Timed(
            8,
            "Five notes · Pitch + rhythm · Basic 4/4",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.PitchAndRhythm,
            SightReadingRhythmPreset.Basic,
            start: 60,
            maximum: 100),
        Untimed(9, "G major · Treble · Pitch only", Staff.Treble, SightReadingPresetId.GMajor),
        Untimed(10, "F major · Treble · Pitch only", Staff.Treble, SightReadingPresetId.FMajor),
        Untimed(11, "Ledger lines · Treble · Pitch only", Staff.Treble, SightReadingPresetId.LedgerLines),
        Untimed(12, "Chords · Treble · Pitch only", Staff.Treble, SightReadingPresetId.Chords),
        Timed(
            13,
            "Rhythm only · 6/8",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.Compound,
            start: 40,
            maximum: 70),
        Timed(
            14,
            "Five notes · Pitch + rhythm · 6/8",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.PitchAndRhythm,
            SightReadingRhythmPreset.Compound,
            start: 40,
            maximum: 70),
        Timed(
            15,
            "Five notes · Pitch + hold + rhythm · Basic 4/4",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.PitchHoldAndRhythm,
            SightReadingRhythmPreset.Basic,
            start: 70,
            maximum: 110),
        Untimed(16, "D major · Treble · Pitch only", Staff.Treble, SightReadingPresetId.DMajor),
        Untimed(17, "B♭ major · Treble · Pitch only", Staff.Treble, SightReadingPresetId.BFlatMajor),
        Untimed(18, "A minor · Treble · Pitch only", Staff.Treble, SightReadingPresetId.AMinor),
        Untimed(19, "Accidentals · Treble · Pitch only", Staff.Treble, SightReadingPresetId.Accidentals),
        Timed(
            20,
            "Rhythm only · Extended 4/4",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.Extended,
            start: 60,
            maximum: 100),
        Timed(
            21,
            "Rhythm only · 3/4 time",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.ThreeFour,
            start: 60,
            maximum: 100),
        Timed(
            22,
            "Rhythm only · 2/4 time",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.TwoFour,
            start: 60,
            maximum: 100),
        Timed(
            23,
            "Rhythm only · Syncopated 4/4",
            SightReadingPresetId.FiveNote,
            NoteReadingMode.RhythmOnly,
            SightReadingRhythmPreset.Syncopated,
            start: 60,
            maximum: 90),
    ];

    private static ExerciseLevel Untimed(
        int number,
        string name,
        Staff staff,
        SightReadingPresetId presetId,
        bool isGrandStaff = false) =>
        new(
            number,
            name,
            staff,
            isGrandStaff,
            presetId,
            NoteReadingMode.PitchAndOrder,
            SightReadingRhythmPreset.Fixed,
            PromptCount,
            StartTempoPulsesPerMinute: null,
            MaximumTempoPulsesPerMinute: null);

    private static ExerciseLevel Timed(
        int number,
        string name,
        SightReadingPresetId presetId,
        NoteReadingMode mode,
        SightReadingRhythmPreset rhythmPreset,
        int start,
        int maximum) =>
        new(number, name, Staff.Treble, false, presetId, mode, rhythmPreset, PromptCount, start, maximum);
}
