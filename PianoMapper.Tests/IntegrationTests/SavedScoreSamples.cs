using PianoMapper.Music;

namespace PianoMapper.Tests.IntegrationTests;

internal static class SavedScoreSamples
{
    internal static Score Create(string title, int measureCount = 1) =>
        new(
            title,
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(90),
            0,
            [
                .. Enumerable.Range(0, measureCount).Select(index => new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4 + index), new NoteValue(4), index, 0, Staff.Treble)],
                    [])),
            ]);
}
