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
            .Select(note => MapNote(note, source.TimeSignature, timeSignature, targetBeatsPerSourceBeat))
            .ToArray();
        ScoreRest[] rests = source.Measures
            .SelectMany(measure => measure.Rests)
            .Select(rest => MapRest(rest, source.TimeSignature, timeSignature, targetBeatsPerSourceBeat))
            .ToArray();

        double targetBeatCount = source.Measures.Count *
            source.TimeSignature.Numerator *
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

        ScoreMeasure[] measures = Enumerable.Range(0, measureCount)
            .Select(measureIndex => new ScoreMeasure(
                notes.Where(note => note.MeasureIndex == measureIndex).ToArray(),
                rests.Where(rest => rest.MeasureIndex == measureIndex).ToArray()))
            .ToArray();
        return source with
        {
            TimeSignature = timeSignature,
            Tempo = tempo,
            Measures = measures,
        };
    }

    private static ScoreNote MapNote(
        ScoreNote note,
        TimeSignature sourceTimeSignature,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat)
    {
        (int MeasureIndex, double BeatOffset) position = MapPosition(
            ScoreDerivation.GetOnsetBeats(note, sourceTimeSignature),
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
        TimeSignature sourceTimeSignature,
        TimeSignature targetTimeSignature,
        double targetBeatsPerSourceBeat)
    {
        double sourceAbsoluteBeat =
            (rest.MeasureIndex * sourceTimeSignature.Numerator) + rest.BeatOffset;
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
