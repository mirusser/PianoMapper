namespace PianoMapper.Music;

public static class SightReadingExerciseComposer
{
    private const int BeatsPerMeasure = 4;
    private const int DefaultTempoBeatsPerMinute = 120;
    private static readonly NoteValue QuarterNote = new(4);

    public static Score Compose(SightReadingExerciseOptions options, Random random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MeasureCount);

        Pitch[] palette = BuildPalette(options.Staff, options.Difficulty);
        Pitch[] pitches = ComposePitches(palette, options.MeasureCount * BeatsPerMeasure, random);
        var measures = new ScoreMeasure[options.MeasureCount];
        for (int measureIndex = 0; measureIndex < measures.Length; measureIndex++)
        {
            var notes = new ScoreNote[BeatsPerMeasure];
            for (int beatIndex = 0; beatIndex < BeatsPerMeasure; beatIndex++)
            {
                notes[beatIndex] = new ScoreNote(
                    pitches[(measureIndex * BeatsPerMeasure) + beatIndex],
                    QuarterNote,
                    measureIndex,
                    beatIndex,
                    options.Staff);
            }

            measures[measureIndex] = new ScoreMeasure(notes, []);
        }

        return new Score(
            $"{GetStaffName(options.Staff)} {GetDifficultyName(options.Difficulty)} note reading",
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            new Tempo(DefaultTempoBeatsPerMinute),
            KeyFifths: 0,
            measures);
    }

    private static Pitch[] BuildPalette(Staff staff, SightReadingDifficulty difficulty)
    {
        int octave = staff switch
        {
            Staff.Treble => 4,
            Staff.Bass => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
        };
        int pitchCount = difficulty switch
        {
            SightReadingDifficulty.Starter => 5,
            SightReadingDifficulty.OneOctave => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unsupported difficulty."),
        };

        return Enumerable.Range(0, pitchCount)
            .Select(index => new Pitch((NoteLetter)(index % 7), 0, octave + (index / 7)))
            .ToArray();
    }

    private static Pitch[] ComposePitches(IReadOnlyList<Pitch> palette, int pitchCount, Random random)
    {
        var pitches = new List<Pitch>(pitchCount);
        while (pitches.Count < pitchCount)
        {
            Pitch[] bag = palette.ToArray();
            Shuffle(bag, random);
            if (pitches.Count > 0 && bag.Length > 1 && bag[0] == pitches[^1])
            {
                (bag[0], bag[1]) = (bag[1], bag[0]);
            }

            int remaining = pitchCount - pitches.Count;
            pitches.AddRange(bag.Take(remaining));
        }

        return pitches.ToArray();
    }

    private static void Shuffle(Pitch[] pitches, Random random)
    {
        for (int index = pitches.Length - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (pitches[index], pitches[swapIndex]) = (pitches[swapIndex], pitches[index]);
        }
    }

    private static string GetStaffName(Staff staff) => staff switch
    {
        Staff.Treble => "Treble",
        Staff.Bass => "Bass",
        _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
    };

    private static string GetDifficultyName(SightReadingDifficulty difficulty) => difficulty switch
    {
        SightReadingDifficulty.Starter => "starter",
        SightReadingDifficulty.OneOctave => "one-octave",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unsupported difficulty."),
    };
}
