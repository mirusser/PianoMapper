using System.Diagnostics;
using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// A small representative corpus run through the generator with several reach profiles. The assertions are the
/// physical rules (checked by an independent validator), not agreement with any published fingering.
/// </summary>
public sealed class ScoreFingeringCorpusTests
{
    private static readonly NoteValue Eighth = new(8);

    public static TheoryData<string> PassageNames => [.. Passages.Keys];

    private static IReadOnlyDictionary<string, Func<Score>> Passages { get; } = new Dictionary<string, Func<Score>>
    {
        ["Repeated notes, right hand"] = () => Line(Staff.Treble, NoteLetter.C, 4, [0, 0, 0, 1, 1, 2, 2, 2]),
        ["C major arpeggio, right hand"] = () => Line(Staff.Treble, NoteLetter.C, 4, [0, 2, 4, 7, 9, 11, 14, 11, 9, 7, 4, 2, 0]),
        ["Alberti bass, left hand"] = () => Line(Staff.Bass, NoteLetter.C, 2, [0, 4, 2, 4, 0, 4, 2, 4]),
        ["Black-key run, right hand"] = () => Line(Staff.Treble, NoteLetter.C, 4, [0, 1, 3, 4, 5, 7], alterAll: 1),
        ["Triads in both hands"] = Triads,
        ["Octaves, right hand"] = Octaves,
    };

    [Theory]
    [MemberData(nameof(PassageNames))]
    public void GenerateAlternatives_RepresentativePassage_ReturnsFeasibleDistinctPathsInCostOrder(string name)
    {
        Score passage = Passages[name]();

        foreach (ScoreFingeringProfile profile in Profiles())
        {
            ScoreFingeringGenerationResult result = ScoreFingeringGenerator.GenerateAlternatives(
                passage,
                new ScoreFingeringGenerationOptions(profile, maximumAlternatives: 3));

            Assert.InRange(result.Alternatives.Count, 1, 3);
            Assert.Equal(
                result.Alternatives.Count,
                result.Alternatives.Select(alternative => string.Join(',', FingeringNumbers(alternative.Score))).Distinct().Count());
            Assert.Equal(
                result.Alternatives.Select(alternative => alternative.Cost).Order(),
                result.Alternatives.Select(alternative => alternative.Cost));
            Assert.All(
                result.Alternatives,
                alternative => Assert.Empty(FingeringConstraintValidator.Validate(alternative.Score, profile)));
        }
    }

    [Theory]
    [InlineData("w3c/accidentals.musicxml")]
    [InlineData("mia_sebastians_theme_ivanovskaya_transcription.musicxml")]
    public void Generate_ImportedScore_SatisfiesEveryReachAndHeldNoteRuleUnderTheDefaultProfile(string fixturePath)
    {
        Score score = new MusicXmlScoreReader().Read(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixturePath));

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_DensePassageOfThousandsOfNotes_FinishesWithinAnInteractiveBudget()
    {
        Score passage = CreateDenseTwoHandPassage(notesPerHand: 1500);
        var options = new ScoreFingeringGenerationOptions(maximumAlternatives: 3);

        var stopwatch = Stopwatch.StartNew();
        ScoreFingeringGenerationResult result = ScoreFingeringGenerator.GenerateAlternatives(passage, options);
        stopwatch.Stop();

        // Generous on purpose: the search is linear in the passage length and takes well under a second on a
        // development machine, while a quadratic search takes tens of seconds at this size.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Generating {result.BestScore.Measures.Sum(measure => measure.Notes.Count)} notes took {stopwatch.Elapsed}.");
        Assert.Empty(FingeringConstraintValidator.Validate(result.BestScore, ScoreFingeringProfile.Default));
    }

    private static IEnumerable<ScoreFingeringProfile> Profiles()
    {
        yield return ScoreFingeringProfile.Default;
        yield return CreateProfile(8);
        yield return CreateProfile(7, leftHandWhiteKeySpan: 8);
    }

    /// <summary>One quarter note per beat; each step is a white-key offset from <paramref name="tonic"/> in octave <paramref name="octave"/>.</summary>
    private static Score Line(
        Staff staff,
        NoteLetter tonic,
        int octave,
        int[] steps,
        int alterAll = 0)
    {
        ScoreNote[] notes = steps
            .Select((step, index) =>
            {
                int diatonic = (int)tonic + step;
                return CreateNote(
                    (NoteLetter)(diatonic % 7),
                    octave + (diatonic / 7),
                    staff,
                    beatOffset: index,
                    alter: alterAll);
            })
            .ToArray();
        return CreateScore(64, 120, notes);
    }

    private static Score Triads() => CreateScore(
    [
        CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 2),
        CreateNote(NoteLetter.B, 4, Staff.Treble, beatOffset: 2),
        CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 2),
        CreateNote(NoteLetter.C, 3, Staff.Bass, beatOffset: 0),
        CreateNote(NoteLetter.E, 3, Staff.Bass, beatOffset: 0),
        CreateNote(NoteLetter.G, 3, Staff.Bass, beatOffset: 0),
        CreateNote(NoteLetter.F, 2, Staff.Bass, beatOffset: 1),
        CreateNote(NoteLetter.A, 2, Staff.Bass, beatOffset: 1),
        CreateNote(NoteLetter.C, 3, Staff.Bass, beatOffset: 1),
    ]);

    private static Score Octaves() => CreateScore(
    [
        CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 2),
        CreateNote(NoteLetter.E, 5, Staff.Treble, beatOffset: 2),
    ]);

    private static Score CreateDenseTwoHandPassage(int notesPerHand)
    {
        const int beatsPerMeasure = 4;
        NoteLetter[] letters = [NoteLetter.C, NoteLetter.D, NoteLetter.E, NoteLetter.F, NoteLetter.G, NoteLetter.A, NoteLetter.B];
        int[] pattern = [0, 1, 2, 3, 4, 3, 2, 1, 0, 2, 4, 2, 5, 4, 2, 0];
        var measures = new List<List<ScoreNote>>();
        for (int index = 0; index < notesPerHand; index++)
        {
            double beat = index * 0.5;
            int measureIndex = (int)(beat / beatsPerMeasure);
            double offset = beat - (measureIndex * beatsPerMeasure);
            while (measures.Count <= measureIndex)
            {
                measures.Add([]);
            }

            int degree = pattern[index % pattern.Length];
            measures[measureIndex].Add(CreateNote(letters[degree % 7], 4 + (degree / 7), Staff.Treble, offset, noteValue: Eighth, measureIndex: measureIndex));
            measures[measureIndex].Add(CreateNote(letters[(degree + 2) % 7], 3 + ((degree + 2) / 7), Staff.Bass, offset, noteValue: Eighth, measureIndex: measureIndex));
            if (index % 4 == 0)
            {
                measures[measureIndex].Add(CreateNote(letters[(degree + 2) % 7], 4 + ((degree + 2) / 7), Staff.Treble, offset, noteValue: Eighth, measureIndex: measureIndex));
                measures[measureIndex].Add(CreateNote(letters[(degree + 4) % 7], 4 + ((degree + 4) / 7), Staff.Treble, offset, noteValue: Eighth, measureIndex: measureIndex));
            }
        }

        return CreateScore(beatsPerMeasure, 120, [.. measures.Select(notes => (IReadOnlyList<ScoreNote>)notes)]);
    }
}
