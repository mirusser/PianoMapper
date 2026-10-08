using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// An independent check of the physical rules a generated fingering must satisfy, written against the score and the
/// reach profile only, so it can verify the generator without sharing its search code.
/// </summary>
internal static class FingeringConstraintValidator
{
    private const double Tolerance = 0.000001;
    private static readonly double[] KeyOffsetsByPitchClass = [0, 0.55, 1, 1.55, 2, 3, 3.55, 4, 4.55, 5, 5.55, 6];

    /// <summary>Returns one message per broken rule; an empty list means the fingering is physically consistent.</summary>
    internal static IReadOnlyList<string> Validate(Score score, ScoreFingeringProfile profile)
    {
        var violations = new List<string>();
        foreach (Staff hand in new[] { Staff.Treble, Staff.Bass })
        {
            ValidateHand(score, profile.GetHand(hand), hand, violations);
        }

        return violations;
    }

    private static void ValidateHand(Score score, FingeringHandProfile profile, Staff hand, List<string> violations)
    {
        List<Played> notes = [];
        for (int measure = 0; measure < score.Measures.Count; measure++)
        {
            for (int index = 0; index < score.Measures[measure].Notes.Count; index++)
            {
                ScoreNote note = score.Measures[measure].Notes[index];
                if (note.Staff != hand)
                {
                    continue;
                }

                if (note.Fingering is not { Number: >= 1 and <= 5 } fingering)
                {
                    violations.Add($"{hand} measure {measure + 1} note {index + 1} has no finger from 1 to 5.");
                    continue;
                }

                double onset = ScoreDerivation.GetOnsetBeats(score, note);
                notes.Add(new Played(
                    $"{hand} measure {measure + 1} note {index + 1}",
                    note,
                    onset,
                    onset + MusicalTime.GetBeats(note.NoteValue, score.TimeSignature),
                    fingering.Number));
            }
        }

        foreach (Played tied in notes.Where(played => played.Note.TiesToNext))
        {
            Played? continuation = notes.FirstOrDefault(candidate =>
                candidate.Note.Pitch == tied.Note.Pitch && Math.Abs(candidate.Onset - tied.End) < Tolerance);
            if (continuation is not null && continuation.Finger != tied.Finger)
            {
                violations.Add($"{tied.Name} is tied to {continuation.Name} but uses a different finger.");
            }
        }

        foreach (double onset in notes.Select(played => played.Onset).Distinct())
        {
            Played[] sounding = notes
                .Where(played => played.Onset <= onset + Tolerance && played.End > onset + Tolerance)
                .ToArray();
            var keys = new List<(int Midi, double Position, int Finger)>();
            foreach (IGrouping<int, Played> key in sounding.GroupBy(played => played.Note.Pitch.MidiNumber))
            {
                if (key.Select(played => played.Finger).Distinct().Count() > 1)
                {
                    violations.Add($"{hand} key {key.First().Note.Pitch} is held by two fingers at beat {onset:0.###}.");
                    continue;
                }

                keys.Add((key.Key, Position(key.Key), key.First().Finger));
            }

            keys.Sort((first, second) => first.Midi.CompareTo(second.Midi));
            for (int first = 0; first < keys.Count; first++)
            {
                for (int second = first + 1; second < keys.Count; second++)
                {
                    int lowerFinger = keys[first].Finger;
                    int upperFinger = keys[second].Finger;
                    if (lowerFinger == upperFinger)
                    {
                        violations.Add($"{hand} finger {lowerFinger} holds two keys at beat {onset:0.###}.");
                        continue;
                    }

                    if (hand == Staff.Treble ? lowerFinger > upperFinger : lowerFinger < upperFinger)
                    {
                        violations.Add($"{hand} fingers cross at beat {onset:0.###}.");
                    }

                    double span = keys[second].Position - keys[first].Position;
                    double limit = profile.GetReach(lowerFinger, upperFinger).MaximumWhiteKeySpan;
                    if (span > limit + Tolerance)
                    {
                        violations.Add(
                            $"{hand} fingers {lowerFinger} and {upperFinger} span {span:0.##} at beat {onset:0.###}, beyond {limit:0.##}.");
                    }
                }
            }
        }
    }

    private static double Position(int midi) => (midi / 12 * 7) + KeyOffsetsByPitchClass[midi % 12];

    private sealed record Played(string Name, ScoreNote Note, double Onset, double End, int Finger);
}
