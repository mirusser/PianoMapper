using System.Xml.Linq;

namespace PianoMapper.Music;

// One part is the score; two single-staff parts are a piano's right and left hand and become its upper and lower staff.
public sealed partial class MusicXmlScoreReader
{
    private const int GrandStaffStaveCount = 2;

    // A single part checks its own <staves> while its attributes are read; several parts are checked together here,
    // before any of them is read, because their staves add up to the one grand staff.
    private static void ValidateStaffCount(IReadOnlyList<XElement> parts)
    {
        if (parts.Count == 1)
        {
            return;
        }

        int staves = parts.Sum(CountStaves);
        if (staves > GrandStaffStaveCount)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML element <part>: {parts.Count} parts add up to {staves} staves, " +
                $"and the grand staff has {GrandStaffStaveCount}.");
        }
    }

    private static int CountStaves(XElement part) =>
        part.Elements()
            .Where(measure => measure.Name.LocalName == "measure")
            .SelectMany(measure => measure.Elements())
            .Where(element => element.Name.LocalName == "attributes")
            .SelectMany(attributes => attributes.Elements())
            .Where(element => element.Name.LocalName == "staves")
            .Select(element => ParsePositiveInt(element, "staves"))
            .DefaultIfEmpty(1)
            .First();

    private static Score BuildScore(string title, IReadOnlyList<PartResult> parts)
    {
        PartResult upper = parts[0];
        var state = upper.State;
        IReadOnlyList<ScoreMeasure> measures = upper.Measures;
        if (parts.Count > 1)
        {
            // Hands are listed right then left; a score listing the bass-clef part first has the order the other way.
            PartResult lower = parts[1];
            if (IsBassPart(upper) && !IsBassPart(lower))
            {
                (upper, lower) = (lower, upper);
            }

            state = upper.State;
            ValidatePartsAgree(upper, lower);
            measures = MergeMeasures(upper.Measures, lower.Measures);
            if (!upper.State.TempoSpecified)
            {
                state.QuarterNotesPerMinute = lower.State.QuarterNotesPerMinute;
            }
        }

        var tempo = new Tempo(state.QuarterNotesPerMinute * MusicalTime.GetBeats(new NoteValue(4), state.TimeSignature));
        return new Score(title, state.TimeSignature, tempo, state.InitialKeyFifths, measures);
    }

    private static bool IsBassPart(PartResult part) =>
        part.State.Clefs.TryGetValue(1, out var clef) && clef.Sign == "F";

    // Two hands share one time signature and one key signature per measure; a score where they differ cannot be drawn on
    // one grand staff.
    private static void ValidatePartsAgree(PartResult upper, PartResult lower)
    {
        if (upper.Measures.Count != lower.Measures.Count)
        {
            throw new InvalidDataException(
                $"The MusicXML parts have different numbers of measures ({upper.Measures.Count} and {lower.Measures.Count}).");
        }

        if (upper.State.TimeSignature != lower.State.TimeSignature)
        {
            throw new NotSupportedException("Unsupported MusicXML element <time>: the two parts have different time signatures.");
        }

        bool keysAgree = upper.State.InitialKeyFifths == lower.State.InitialKeyFifths &&
            upper.Measures.Zip(lower.Measures).All(pair => pair.First.KeyFifths == pair.Second.KeyFifths);
        if (!keysAgree)
        {
            throw new NotSupportedException("Unsupported MusicXML element <key>: the two parts have different key signatures.");
        }

        bool lengthsAgree = upper.Measures.Zip(lower.Measures).All(pair =>
            pair.First.LengthInBeats == pair.Second.LengthInBeats);
        if (!lengthsAgree)
        {
            throw new NotSupportedException(
                "Unsupported MusicXML element <measure>: the two parts have different implicit measure lengths.");
        }
    }

    private static List<ScoreMeasure> MergeMeasures(IReadOnlyList<ScoreMeasure> upper, IReadOnlyList<ScoreMeasure> lower) =>
        upper.Zip(lower, (first, second) => new ScoreMeasure(
                first.Notes.Select(note => note with { Staff = Staff.Treble })
                    .Concat(second.Notes.Select(note => note with { Staff = Staff.Bass }))
                    .ToArray(),
                first.Rests.Select(rest => rest with { Staff = Staff.Treble })
                    .Concat(second.Rests.Select(rest => rest with { Staff = Staff.Bass }))
                    .ToArray(),
                first.KeyFifths,
                MergeOptionalBarlines(first.LeftBarline, second.LeftBarline),
                MergeOptionalBarlines(first.RightBarline, second.RightBarline),
                MergeDirections(first.Directions, second.Directions),
                first.LengthInBeats))
            .ToList();

    private static ScoreBarline? MergeOptionalBarlines(ScoreBarline? upper, ScoreBarline? lower) =>
        lower is null ? upper : MergeBarlines(upper, lower);

    private static IReadOnlyList<ScoreDirection>? MergeDirections(
        IReadOnlyList<ScoreDirection>? upper,
        IReadOnlyList<ScoreDirection>? lower)
    {
        var merged = (upper ?? []).Select(direction => direction with { Staff = Staff.Treble })
            .Concat((lower ?? []).Select(direction => direction with { Staff = Staff.Bass }))
            .ToArray();
        return merged.Length == 0 ? null : merged;
    }
}
