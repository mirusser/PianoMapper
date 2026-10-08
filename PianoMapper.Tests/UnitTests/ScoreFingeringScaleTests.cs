using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// Whole-keyboard scale runs. The rule under test is the one every scale fingering follows, not a particular
/// string of numbers: moving away from the thumb the fingers climb one at a time (1 2 3 4 5) and the thumb passes
/// under finger 3 or 4; moving toward the thumb the fingers fall one at a time and finger 3 or 4 crosses over it.
/// Because every step is legal, the thumbs also land three or four notes apart. The runs are long (15 and 22 notes)
/// because an 8-note scale hides the bug where a long scale came out as 1 2 1 2 1 2; they cover both hands and
/// directions, a slow and a fast tempo, and C major (white keys) and F major (a black-key thumb).
/// </summary>
public sealed class ScoreFingeringScaleTests
{
    private static readonly IReadOnlyDictionary<string, (NoteLetter Tonic, NoteLetter[] Sharpened, NoteLetter[] Flattened)> KeySignatures =
        new Dictionary<string, (NoteLetter, NoteLetter[], NoteLetter[])>
        {
            ["C major"] = (NoteLetter.C, [], []),
            ["F major"] = (NoteLetter.F, [], [NoteLetter.B]),
        };

    public static TheoryData<string, int, Staff, bool, double> Scales()
    {
        var data = new TheoryData<string, int, Staff, bool, double>();
        foreach (string key in KeySignatures.Keys)
        {
            foreach (int noteCount in new[] { 15, 22 })
            {
                foreach (Staff hand in new[] { Staff.Treble, Staff.Bass })
                {
                    // A scale is fingered the same way at a slow and at a fast tempo.
                    foreach (double beatsPerMinute in new[] { 60.0, 200.0 })
                    {
                        data.Add(key, noteCount, hand, false, beatsPerMinute);
                        data.Add(key, noteCount, hand, true, beatsPerMinute);
                    }
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Generate_ScaleRun_PassesTheThumbOnlyUnderOrOverFingerThreeOrFour(
        string key,
        int noteCount,
        Staff hand,
        bool descending,
        double beatsPerMinute)
    {
        Score scale = CreateScale(key, noteCount, hand, descending, beatsPerMinute);

        int[] fingers = FingeringNumbers(ScoreFingeringGenerator.Generate(scale));

        string description = $"{key}, {noteCount} notes, {hand}, {(descending ? "descending" : "ascending")}, {beatsPerMinute} bpm: {string.Join(' ', fingers)}";
        for (int index = 1; index < fingers.Length; index++)
        {
            Assert.True(
                IsLegalScaleStep(hand, descending, fingers[index - 1], fingers[index]),
                $"Step {index} to {index + 1} is not a scale step ({description}).");
        }
    }

    /// <summary>
    /// Whether <paramref name="next"/> may follow <paramref name="previous"/> on the next scale degree. Pitch moves away
    /// from the thumb for the right hand going up and the left hand going down.
    /// </summary>
    private static bool IsLegalScaleStep(Staff hand, bool descending, int previous, int next)
    {
        bool awayFromThumb = (hand == Staff.Treble) != descending;
        return awayFromThumb
            ? next == previous + 1 || (next == 1 && previous is 3 or 4)
            : next == previous - 1 || (previous == 1 && next is 3 or 4);
    }

    /// <summary>A one-note-per-beat scale of <paramref name="noteCount"/> degrees from the key's tonic, written in the key signature.</summary>
    private static Score CreateScale(string key, int noteCount, Staff hand, bool descending, double beatsPerMinute)
    {
        (NoteLetter tonic, NoteLetter[] sharpened, NoteLetter[] flattened) = KeySignatures[key];
        int tonicOctave = hand == Staff.Treble ? 4 : 2;
        ScoreNote[] notes = Enumerable.Range(0, noteCount)
            .Select(index =>
            {
                int degree = descending ? noteCount - 1 - index : index;
                int diatonic = (int)tonic + degree;
                var letter = (NoteLetter)(diatonic % 7);
                int alter = sharpened.Contains(letter) ? 1 : flattened.Contains(letter) ? -1 : 0;
                return CreateNote(letter, tonicOctave + (diatonic / 7), hand, beatOffset: index, alter: alter);
            })
            .ToArray();
        return CreateScore(64, beatsPerMinute, notes);
    }
}
