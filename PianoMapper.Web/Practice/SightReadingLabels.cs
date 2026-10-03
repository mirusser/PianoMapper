using System.Globalization;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The learner-facing names for exercise settings, shared by the exercise panel's selects and the history list so
/// the two cannot drift. Every enum member must have a label; the tests enumerate the enums so a new member without
/// one fails. Persisted history stores enum member names as strings, so an unrecognized name falls back to itself.
/// </summary>
internal static class SightReadingLabels
{
    private const string Separator = " · ";

    internal static string Preset(SightReadingPresetId presetId) => presetId switch
    {
        SightReadingPresetId.FiveNote => "Five notes",
        SightReadingPresetId.OneOctave => "One octave",
        SightReadingPresetId.LedgerLines => "Ledger lines",
        SightReadingPresetId.GMajor => "G major",
        SightReadingPresetId.FMajor => "F major",
        SightReadingPresetId.Chords => "Chords",
        SightReadingPresetId.DMajor => "D major",
        SightReadingPresetId.BFlatMajor => "B♭ major",
        SightReadingPresetId.AMinor => "A minor",
        SightReadingPresetId.Accidentals => "Accidentals",
        _ => throw new ArgumentOutOfRangeException(nameof(presetId)),
    };

    internal static string PresetOption(SightReadingPresetId presetId) => WithHint(
        Preset(presetId),
        presetId switch
        {
            SightReadingPresetId.FiveNote => "starter",
            SightReadingPresetId.GMajor => "one sharp",
            SightReadingPresetId.FMajor => "one flat",
            SightReadingPresetId.DMajor => "two sharps",
            SightReadingPresetId.BFlatMajor => "two flats",
            SightReadingPresetId.AMinor => "natural minor",
            SightReadingPresetId.Accidentals => "sharps and flats",
            SightReadingPresetId.Chords => "I, IV, V",
            _ => null,
        });

    internal static string Mode(NoteReadingMode mode) => mode switch
    {
        NoteReadingMode.Off => "Off",
        NoteReadingMode.PitchAndOrder => "Pitch only",
        NoteReadingMode.PitchAndHold => "Pitch + hold",
        NoteReadingMode.PitchHoldAndRhythm => "Pitch + hold + rhythm",
        NoteReadingMode.PitchAndRhythm => "Pitch + rhythm",
        NoteReadingMode.RhythmOnly => "Rhythm only",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    internal static string ModeOption(NoteReadingMode mode) => WithHint(
        Mode(mode),
        mode switch
        {
            NoteReadingMode.PitchAndOrder => "self-paced",
            NoteReadingMode.PitchAndHold => "written duration",
            NoteReadingMode.PitchHoldAndRhythm => "timed, with count-in",
            NoteReadingMode.PitchAndRhythm => "on the beat",
            NoteReadingMode.RhythmOnly => "tap on any key",
            _ => null,
        });

    internal static string Motion(SightReadingMotion motion) => motion switch
    {
        SightReadingMotion.Random => "Random notes",
        SightReadingMotion.Melodic => "Melodic",
        SightReadingMotion.Intervallic => "Same interval",
        _ => throw new ArgumentOutOfRangeException(nameof(motion)),
    };

    internal static string MotionOption(SightReadingMotion motion) => WithHint(
        Motion(motion),
        motion switch
        {
            SightReadingMotion.Random => "every note of the range",
            SightReadingMotion.Melodic => "steps and repeats",
            SightReadingMotion.Intervallic => "jumps of one size",
            _ => null,
        });

    /// <summary>The name of an interval of <paramref name="diatonicSteps"/> steps, for the intervallic pattern.</summary>
    internal static string IntervalSize(int diatonicSteps) => diatonicSteps switch
    {
        1 => "Seconds",
        2 => "Thirds",
        3 => "Fourths",
        4 => "Fifths",
        _ => throw new ArgumentOutOfRangeException(nameof(diatonicSteps)),
    };

    /// <summary>
    /// The exercise lengths the Length select offers, shortest first. Each is a multiple of the 4-beat measure, which a
    /// fixed-rhythm exercise needs. The longer ones run past one page of measures, so the exercise pages as it goes.
    /// </summary>
    internal static IReadOnlyList<int> PromptCountChoices { get; } = [8, 16, 32, 64];

    internal static string PromptCountOption(int promptCount) => $"{promptCount} notes";

    /// <summary>The selectable modes in learning order, easiest first. <see cref="NoteReadingMode.Off"/> is not one of them.</summary>
    internal static IReadOnlyList<NoteReadingMode> LadderModes { get; } =
    [
        NoteReadingMode.PitchAndOrder,
        NoteReadingMode.RhythmOnly,
        NoteReadingMode.PitchAndRhythm,
        NoteReadingMode.PitchAndHold,
        NoteReadingMode.PitchHoldAndRhythm,
    ];

    /// <summary>One line saying what the mode asks the learner to do.</summary>
    internal static string ModeDescription(NoteReadingMode mode) => mode switch
    {
        NoteReadingMode.PitchAndOrder => "Play each note you see, at your own pace.",
        NoteReadingMode.RhythmOnly => "Tap this rhythm on any key.",
        NoteReadingMode.PitchAndRhythm => "Play the right key on the beat.",
        NoteReadingMode.PitchAndHold => "Play each note and hold it for its written length.",
        NoteReadingMode.PitchHoldAndRhythm => "Play the right key on the beat and hold it for its written length.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>
    /// The wording of a grading verdict, shared by the practice panel and the exercise summary so they cannot drift.
    /// Every <see cref="PianoMapper.Practice.Verdict"/> member needs a label (a test enumerates them).
    /// </summary>
    internal static string Verdict(Verdict verdict) => verdict switch
    {
        PianoMapper.Practice.Verdict.Correct => "Correct",
        PianoMapper.Practice.Verdict.WrongPitch => "Wrong pitch",
        PianoMapper.Practice.Verdict.Early => "Early",
        PianoMapper.Practice.Verdict.Late => "Late",
        PianoMapper.Practice.Verdict.TooShort => "Too short",
        PianoMapper.Practice.Verdict.TooLong => "Too long",
        PianoMapper.Practice.Verdict.Missed => "Missed",
        PianoMapper.Practice.Verdict.Extra => "Extra",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict)),
    };

    /// <summary>The non-zero verdict counts of a run, in verdict order, e.g. "Correct 6 · Late 1 · Missed 1".</summary>
    internal static string DescribeVerdictCounts(IReadOnlyDictionary<Verdict, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return string.Join(
            Separator,
            Enum.GetValues<Verdict>()
                .Where(verdict => counts.GetValueOrDefault(verdict) > 0)
                .Select(verdict => string.Create(CultureInfo.InvariantCulture, $"{Verdict(verdict)} {counts[verdict]}")));
    }

    /// <summary>Where the learner stands on a ladder level. Never "locked": every level stays playable.</summary>
    internal static string LevelStatus(LevelStatus status) => status switch
    {
        PianoMapper.Practice.LevelStatus.NotTried => "Not tried yet",
        PianoMapper.Practice.LevelStatus.InProgress => "In progress",
        PianoMapper.Practice.LevelStatus.Recommended => "Next step",
        PianoMapper.Practice.LevelStatus.Passed => "Passed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>
    /// The reason line under a ladder level, e.g. "Passed Five notes · Treble · Pitch only at 94% over 3 sessions".
    /// </summary>
    internal static string DescribeLevelProgress(LevelProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        string name = progress.Level.Name;
        string sessions = progress.WindowSessionCount == 1 ? "1 session" : $"{progress.WindowSessionCount} sessions";
        string percent = progress.AveragePitchPercent is { } average
            ? string.Create(CultureInfo.InvariantCulture, $"{average:F0}%")
            : string.Empty;
        string tempo = progress.RecommendedTempoPulsesPerMinute is { } pulses
            ? $"{Separator}try {Tempo(pulses, progress.Level.RhythmPreset)}"
            : string.Empty;
        return progress.Status switch
        {
            PianoMapper.Practice.LevelStatus.Passed => $"Passed {name} at {percent} over {sessions}",
            PianoMapper.Practice.LevelStatus.Recommended when progress.WindowSessionCount > 0 =>
                $"Next: {name}{Separator}{percent} over your last {sessions}{tempo}",
            PianoMapper.Practice.LevelStatus.Recommended => $"Next: {name}{tempo}",
            PianoMapper.Practice.LevelStatus.InProgress =>
                $"In progress: {name}{Separator}{percent} over your last {sessions}",
            PianoMapper.Practice.LevelStatus.NotTried => $"{name}{Separator}not tried yet",
            _ => throw new ArgumentOutOfRangeException(nameof(progress)),
        };
    }

    internal static string Pacing(ExercisePacing pacing) => pacing switch
    {
        ExercisePacing.WaitForMe => "Wait for me",
        ExercisePacing.PlayAlong => "Play along",
        _ => throw new ArgumentOutOfRangeException(nameof(pacing)),
    };

    internal static string PacingOption(ExercisePacing pacing) => WithHint(
        Pacing(pacing),
        pacing switch
        {
            ExercisePacing.WaitForMe => "waits for the right key",
            ExercisePacing.PlayAlong => "keeps going, grades missed and extra notes",
            _ => null,
        });

    internal static string RhythmPreset(SightReadingRhythmPreset rhythmPreset) => rhythmPreset switch
    {
        SightReadingRhythmPreset.Fixed => "Fixed quarter notes",
        SightReadingRhythmPreset.Basic => "Basic 4/4",
        SightReadingRhythmPreset.Compound => "Compound 6/8",
        SightReadingRhythmPreset.Extended => "Extended 4/4",
        SightReadingRhythmPreset.ThreeFour => "3/4 time",
        SightReadingRhythmPreset.TwoFour => "2/4 time",
        SightReadingRhythmPreset.Syncopated => "Syncopated 4/4",
        _ => throw new ArgumentOutOfRangeException(nameof(rhythmPreset)),
    };

    internal static string RhythmPresetOption(SightReadingRhythmPreset rhythmPreset) => WithHint(
        RhythmPreset(rhythmPreset),
        rhythmPreset switch
        {
            SightReadingRhythmPreset.Basic => "half, quarter, eighths",
            SightReadingRhythmPreset.Extended => "dotted notes, whole note, eighth rest",
            SightReadingRhythmPreset.ThreeFour => "dotted half, quarter rest",
            SightReadingRhythmPreset.TwoFour => "short bars",
            SightReadingRhythmPreset.Syncopated => "off-beats and ties",
            _ => null,
        });

    internal static string StaffName(Staff staff) => staff switch
    {
        Staff.Treble => "Treble",
        Staff.Bass => "Bass",
        _ => throw new ArgumentOutOfRangeException(nameof(staff)),
    };

    /// <summary>Whether the mode judges onset or release timing, not just pitch.</summary>
    internal static bool IsTimingGraded(NoteReadingMode mode) =>
        Enum.IsDefined(mode) &&
        (mode.GetGradedAxes() & (GradedAxes.Onset | GradedAxes.Duration)) != GradedAxes.None;

    /// <summary>The note that counts as one pulse: a quarter note, or a dotted quarter in 6/8.</summary>
    internal static string TempoUnit(SightReadingRhythmPreset rhythmPreset) =>
        rhythmPreset == SightReadingRhythmPreset.Compound ? "♩." : "♩";

    /// <summary>A tempo in the rhythm preset's pulse unit, e.g. "♩ = 60" or "♩. = 40".</summary>
    internal static string Tempo(int pulsesPerMinute, SightReadingRhythmPreset rhythmPreset) =>
        string.Create(CultureInfo.InvariantCulture, $"{TempoUnit(rhythmPreset)} = {pulsesPerMinute}");

    /// <summary>
    /// One history row's setup, e.g. "Five notes · Treble · Pitch only". Entries recorded before schema version 2
    /// lack rhythm, layout and tempo, so they are marked "older session" instead of guessing.
    /// </summary>
    internal static string DescribeSession(SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var parts = new List<string>();
        if (summary.Mode != NoteReadingMode.RhythmOnly)
        {
            // Rhythm only repeats one note on the middle line, so a stored pitch range would be misleading.
            parts.Add(Enum.TryParse(summary.PresetId, out SightReadingPresetId presetId) && Enum.IsDefined(presetId)
                ? Preset(presetId)
                : summary.PresetId);
        }

        parts.Add(summary.IsGrandStaff == true ? "Grand staff" : OrRaw(summary.Staff, StaffName));
        parts.Add(OrRaw(summary.Mode, Mode));

        SightReadingRhythmPreset? rhythmPreset = null;
        if (summary.RhythmPreset is { } rhythmName)
        {
            if (Enum.TryParse(rhythmName, out SightReadingRhythmPreset parsed) && Enum.IsDefined(parsed))
            {
                rhythmPreset = parsed;
                if (parsed != SightReadingRhythmPreset.Fixed)
                {
                    parts.Add(RhythmPreset(parsed));
                }
            }
            else
            {
                parts.Add(rhythmName);
            }
        }

        if (summary.Motion is { } motionName && motionName != nameof(SightReadingMotion.Random))
        {
            parts.Add(Enum.TryParse(motionName, out SightReadingMotion motion) && Enum.IsDefined(motion)
                ? Motion(motion)
                : motionName);
        }

        if (summary.Pacing == SightReadingExerciseCoordinator.PlayAlongPacingName)
        {
            parts.Add(Pacing(ExercisePacing.PlayAlong));
        }

        if (summary.TempoBeatsPerMinute is { } pulses)
        {
            parts.Add(Tempo(pulses, rhythmPreset ?? SightReadingRhythmPreset.Fixed));
        }

        if (summary.SchemaVersion == SightReadingSessionSummary.LegacySchemaVersion)
        {
            parts.Add("older session");
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// The pitch-versus-timing split for a timed session, or <see langword="null"/> when the mode does not grade
    /// timing or the entry predates the split (schema version 1 fused the two).
    /// </summary>
    internal static string? DescribeTimingBreakdown(SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return IsTimingGraded(summary.Mode) &&
            summary is { PitchFirstTryCorrectCount: { } pitchFirstTryCorrectCount, TimingMistakeCount: { } timingMistakeCount }
                ? DescribeTimingBreakdown(pitchFirstTryCorrectCount, summary.PromptCount, timingMistakeCount)
                : null;
    }

    internal static string DescribeTimingBreakdown(int pitchFirstTryCorrectCount, int promptCount, int timingMistakeCount) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"pitch {pitchFirstTryCorrectCount} of {promptCount} first-try{Separator}{timingMistakeCount} timing mistake(s)");

    private static string WithHint(string label, string? hint) =>
        hint is null ? label : $"{label} ({hint})";

    private static string OrRaw<TEnum>(TEnum value, Func<TEnum, string> label)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? label(value) : value.ToString();
}
