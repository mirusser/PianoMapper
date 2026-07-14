namespace PianoMapper.Music;

internal static class RandomMeasureComposer
{
    private static readonly NoteValue[] PossibleNoteValues =
    [
        new(1),
        new(2),
        new(4),
        new(8),
        new(16),
    ];

    public static RandomMeasure Compose(
        IReadOnlyList<Pitch> palette,
        int minNumerator,
        int maxNumerator,
        int minBeatsPerMinute,
        int maxBeatsPerMinute,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(random);
        if (palette.Count == 0)
        {
            throw new ArgumentException("The pitch palette cannot be empty.", nameof(palette));
        }

        int numerator = random.Next(minNumerator, maxNumerator + 1);
        var timeSignature = new TimeSignature(numerator, new NoteValue(4));
        var tempo = new Tempo(random.Next(minBeatsPerMinute, maxBeatsPerMinute + 1));
        var events = new List<RandomMeasureEvent>();
        double beatsRemaining = numerator;

        while (beatsRemaining > 0)
        {
            var fittingValues = PossibleNoteValues
                .Where(noteValue => MusicalTime.GetBeats(noteValue, timeSignature) <= beatsRemaining)
                .ToArray();
            var noteValue = fittingValues[random.Next(fittingValues.Length)];
            double beats = MusicalTime.GetBeats(noteValue, timeSignature);
            var pitch = palette[random.Next(palette.Count)];
            events.Add(new RandomMeasureEvent(pitch, noteValue, beats));
            beatsRemaining -= beats;
        }

        return new RandomMeasure(timeSignature, tempo, events);
    }
}
