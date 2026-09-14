using PianoMapper.Music;
using PianoMapper.Web.Playback;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreNoteAddressResolverTests
{
    [Fact]
    public void ResolveOrdinal_LaterSelectedRange_ReturnsFullScoreNoteOrdinal()
    {
        var score = CreateScore(
            [
                CreateMeasure(0, NoteLetter.C, NoteLetter.D),
                new ScoreMeasure([], []),
                CreateMeasure(2, NoteLetter.E),
                CreateMeasure(3, NoteLetter.F, NoteLetter.G),
            ]);

        int ordinal = ScoreNoteAddressResolver.ResolveOrdinal(
            score,
            selectedFirstMeasure: 2,
            new ScoreNoteAddress(1, 1));

        Assert.Equal(4, ordinal);
    }

    [Fact]
    public void ResolveOrdinal_RebarredSelectedNote_MapsBackToOriginalScoreOrder()
    {
        var source = CreateScore(
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 3, Staff.Treble)],
                    []),
                new ScoreMeasure(
                    [
                        new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 1, 0, Staff.Treble),
                        new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 1, 1, Staff.Treble),
                    ],
                    []),
            ]);
        var targetTimeSignature = new TimeSignature(3, new NoteValue(4));
        Score loaded = ScoreTiming.Apply(source, targetTimeSignature, source.Tempo);
        Score selected = ScoreMeasureRange.Create(loaded, firstMeasureIndex: 1, lastMeasureIndex: 1);
        var selectedAddress = new ScoreNoteAddress(0, 1);

        int ordinal = ScoreNoteAddressResolver.ResolveOrdinal(loaded, selectedFirstMeasure: 1, selectedAddress);
        Score editedSource = ScoreFingeringEditor.SetFingering(source, ordinal, fingerNumber: 5);
        Score editedLoaded = ScoreTiming.Apply(editedSource, targetTimeSignature, source.Tempo);

        Assert.Equal(NoteLetter.D, selected.Measures[0].Notes[selectedAddress.NoteIndex].Pitch.Letter);
        Assert.Equal(new ScoreFingering(5), editedLoaded.Measures[1].Notes[1].Fingering);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    public void ResolveOrdinal_InvalidAddress_Throws(int measureIndex, int noteIndex)
    {
        var score = CreateScore([CreateMeasure(0, NoteLetter.C)]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScoreNoteAddressResolver.ResolveOrdinal(
                score,
                selectedFirstMeasure: 0,
                new ScoreNoteAddress(measureIndex, noteIndex)));
    }

    private static Score CreateScore(IReadOnlyList<ScoreMeasure> measures) =>
        new(
            "Addresses",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            measures);

    private static ScoreMeasure CreateMeasure(int measureIndex, params NoteLetter[] letters) =>
        new(
            letters
                .Select((letter, noteIndex) => new ScoreNote(
                    new Pitch(letter, 0, 4),
                    new NoteValue(4),
                    measureIndex,
                    noteIndex,
                    Staff.Treble))
                .ToArray(),
            []);
}
