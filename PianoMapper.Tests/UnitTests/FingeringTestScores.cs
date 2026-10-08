using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

/// <summary>Small score builders shared by the fingering-generator test classes.</summary>
internal static class FingeringTestScores
{
    internal static ScoreNote CreateNote(
        NoteLetter letter,
        int octave,
        Staff staff,
        double beatOffset,
        ScoreFingering? fingering = null,
        NoteValue? noteValue = null,
        bool tiesToNext = false,
        int measureIndex = 0,
        int alter = 0) =>
        new(
            new Pitch(letter, alter, octave),
            noteValue ?? new NoteValue(4),
            measureIndex,
            beatOffset,
            staff,
            TiesToNext: tiesToNext,
            Fingering: fingering);

    /// <summary>One twelve-beat measure, so a test can place notes at any early beat.</summary>
    internal static Score CreateScore(IReadOnlyList<ScoreNote> notes) => CreateScore(12, 120, notes);

    /// <summary>
    /// One measure per argument; every note's <see cref="ScoreNote.MeasureIndex"/> must match its measure.
    /// </summary>
    internal static Score CreateScore(
        int beatsPerMeasure,
        double beatsPerMinute,
        params IReadOnlyList<ScoreNote>[] measures) =>
        new(
            "Generated fingerings",
            new TimeSignature(beatsPerMeasure, new NoteValue(4)),
            new Tempo(beatsPerMinute),
            0,
            measures.Select(notes => new ScoreMeasure(notes, [])).ToArray());

    internal static int[] FingeringNumbers(Score score) =>
        score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => Assert.IsType<ScoreFingering>(note.Fingering).Number)
            .ToArray();

    /// <summary>A reach profile whose 1-5 pair spans <paramref name="whiteKeySpan"/> and every other pair is no wider.</summary>
    internal static ScoreFingeringProfile CreateProfile(double whiteKeySpan, double? leftHandWhiteKeySpan = null) =>
        ScoreFingeringProfile.CreateWithThumbToLittleFingerReach(
            new FingeringReach(whiteKeySpan, whiteKeySpan),
            new FingeringReach(leftHandWhiteKeySpan ?? whiteKeySpan, leftHandWhiteKeySpan ?? whiteKeySpan));
}
