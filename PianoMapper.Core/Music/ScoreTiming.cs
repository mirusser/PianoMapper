namespace PianoMapper.Music;

public static class ScoreTiming
{
    private const double BeatComparisonTolerance = 1e-9;

    public static Score Apply(Score source, TimeSignature timeSignature, Tempo tempo)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TimeSignature == timeSignature)
        {
            return source with { Tempo = tempo };
        }

        double targetBeatsPerSourceBeat = MusicalTime.GetBeats(
            source.TimeSignature.BeatNoteValue,
            timeSignature);
        ScoreNote[] notes = source.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => MapNote(note, source, timeSignature, targetBeatsPerSourceBeat))
            .ToArray();
        ScoreRest[] rests = source.Measures
            .SelectMany(measure => measure.Rests)
            .Select(rest => MapRest(rest, source, timeSignature, targetBeatsPerSourceBeat))
            .ToArray();

        double targetBeatCount = ScoreDerivation.GetMeasureStartBeats(source, source.Measures.Count) *
            targetBeatsPerSourceBeat;
        int measureCount = source.Measures.Count == 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(
                (targetBeatCount - BeatComparisonTolerance) / timeSignature.Numerator));
        int eventMeasureCount = notes.Select(note => note.MeasureIndex)
            .Concat(rests.Select(rest => rest.MeasureIndex))
            .DefaultIfEmpty(-1)
            .Max() + 1;
        measureCount = Math.Max(measureCount, eventMeasureCount);

        var marks = MapMeasureMarks(source, timeSignature, targetBeatsPerSourceBeat, measureCount);
        int initialKeyFifths = marks[0].KeyFifths ?? source.KeyFifths;
        ScoreMeasure[] measures = Enumerable.Range(0, measureCount)
            .Select(measureIndex => new ScoreMeasure(
                notes.Where(note => note.MeasureIndex == measureIndex).ToArray(),
                rests.Where(rest => rest.MeasureIndex == measureIndex).ToArray(),
                measureIndex > 0 ? marks[measureIndex].KeyFifths : null,
                marks[measureIndex].LeftBarline,
                marks[measureIndex].RightBarline,
                marks[measureIndex].Directions.Count == 0 ? null : marks[measureIndex].Directions))
            .ToArray();
        return source with
        {
            TimeSignature = timeSignature,
            Tempo = tempo,
            KeyFifths = initialKeyFifths,
            Measures = measures,
        };
    }

    private sealed class MeasureMarks
    {
        internal int? KeyFifths { get; set; }

        internal ScoreBarline? LeftBarline { get; set; }

        internal ScoreBarline? RightBarline { get; set; }

        internal List<ScoreDirection> Directions { get; } = [];
    }

    // Key changes, barline signs and directions belong to where they sit in the source; under the new bar lines
    // they go to the measure that holds that musical position, which is the same measure when the meter is unchanged.
    private static MeasureMarks[] MapMeasureMarks(
        Score source,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat,
        int targetMeasureCount)
    {
        MeasureMarks[] marks = Enumerable.Range(0, targetMeasureCount).Select(_ => new MeasureMarks()).ToArray();
        for (int sourceIndex = 0; sourceIndex < source.Measures.Count; sourceIndex++)
        {
            ScoreMeasure measure = source.Measures[sourceIndex];
            double startBeats = ScoreDerivation.GetMeasureStartBeats(source, sourceIndex);
            double endBeats = ScoreDerivation.GetMeasureStartBeats(source, sourceIndex + 1);
            (int startMeasure, _) = MapPosition(startBeats, targetTimeSignature, targetBeatsPerSourceBeat);
            (int endMeasure, _) = MapPosition(
                endBeats - BeatComparisonTolerance * 10,
                targetTimeSignature,
                targetBeatsPerSourceBeat);
            startMeasure = Math.Min(startMeasure, targetMeasureCount - 1);
            endMeasure = Math.Min(endMeasure, targetMeasureCount - 1);
            if (measure.KeyFifths is { } keyFifths)
            {
                marks[startMeasure].KeyFifths = keyFifths;
            }

            if (measure.LeftBarline is { } leftBarline)
            {
                marks[startMeasure].LeftBarline = leftBarline;
            }

            if (measure.RightBarline is { } rightBarline)
            {
                marks[endMeasure].RightBarline = rightBarline;
            }

            foreach (ScoreDirection direction in measure.Directions ?? [])
            {
                (int directionMeasure, double directionOffset) = MapPosition(
                    startBeats + direction.BeatOffset,
                    targetTimeSignature,
                    targetBeatsPerSourceBeat);
                marks[Math.Min(directionMeasure, targetMeasureCount - 1)].Directions.Add(
                    direction with { BeatOffset = directionOffset });
            }
        }

        return marks;
    }

    private static ScoreNote MapNote(
        ScoreNote note,
        Score source,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat)
    {
        (int MeasureIndex, double BeatOffset) position = MapPosition(
            ScoreDerivation.GetOnsetBeats(source, note),
            targetTimeSignature,
            targetBeatsPerSourceBeat);
        return note with
        {
            MeasureIndex = position.MeasureIndex,
            BeatOffset = position.BeatOffset,
        };
    }

    private static ScoreRest MapRest(
        ScoreRest rest,
        Score source,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat)
    {
        double sourceAbsoluteBeat =
            ScoreDerivation.GetMeasureStartBeats(source, rest.MeasureIndex) + rest.BeatOffset;
        (int MeasureIndex, double BeatOffset) position = MapPosition(
            sourceAbsoluteBeat,
            targetTimeSignature,
            targetBeatsPerSourceBeat);
        return rest with
        {
            MeasureIndex = position.MeasureIndex,
            BeatOffset = position.BeatOffset,
        };
    }

    private static (int MeasureIndex, double BeatOffset) MapPosition(
        double sourceAbsoluteBeat,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat)
    {
        double targetAbsoluteBeat = sourceAbsoluteBeat * targetBeatsPerSourceBeat;
        int measureIndex = (int)Math.Floor(targetAbsoluteBeat / targetTimeSignature.Numerator);
        double beatOffset = targetAbsoluteBeat - (measureIndex * targetTimeSignature.Numerator);
        if (Math.Abs(beatOffset) <= BeatComparisonTolerance)
        {
            beatOffset = 0;
        }
        else if (Math.Abs(targetTimeSignature.Numerator - beatOffset) <= BeatComparisonTolerance)
        {
            measureIndex++;
            beatOffset = 0;
        }

        return (measureIndex, beatOffset);
    }
}
