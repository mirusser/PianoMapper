using System.Text.Json;
using System.Text.Json.Serialization;
using PianoMapper.Music;

namespace PianoMapper.Server.Persistence;

internal static class ScoreDocumentSerializer
{
    internal const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static string Serialize(Score score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var document = new ScoreDocument(
            score.TimeSignature.Numerator,
            ToDocument(score.TimeSignature.BeatNoteValue),
            score.Tempo.BeatsPerMinute,
            score.KeyFifths,
            score.Measures.Select(ToDocument).ToArray());
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    internal static Score Deserialize(string title, string json, int documentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (documentVersion != CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported saved-score document version {documentVersion}.");
        }

        ScoreDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ScoreDocument>(json, JsonOptions) ??
                throw new InvalidDataException("The saved-score document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The saved-score document is invalid.", exception);
        }

        return new Score(
            title,
            new TimeSignature(document.TimeSignatureNumerator, FromDocument(document.BeatNoteValue)),
            new Tempo(document.TempoBeatsPerMinute),
            document.KeyFifths,
            document.Measures.Select(FromDocument).ToArray());
    }

    private static ScoreMeasureDocument ToDocument(ScoreMeasure measure) =>
        new(
            measure.Notes.Select(ToDocument).ToArray(),
            measure.Rests.Select(ToDocument).ToArray());

    private static ScoreNoteDocument ToDocument(ScoreNote note) =>
        new(
            new PitchDocument(note.Pitch.Letter, note.Pitch.Alter, note.Pitch.Octave),
            ToDocument(note.NoteValue),
            note.MeasureIndex,
            note.BeatOffset,
            note.Staff,
            note.TiesToNext,
            note.IsChordContinuation,
            note.BeamState,
            note.StemDirection,
            note.Fingering is null
                ? null
                : new ScoreFingeringDocument(note.Fingering.Number, note.Fingering.Placement),
            note.Accidental,
            note.Fermata,
            note.Articulation,
            note.Ornament,
            note.AccidentalMark,
            note.Slur is null ? null : new ScoreSlurDocument(note.Slur.IsStart, note.Slur.Number),
            note.Arpeggio,
            note.Glissando is null
                ? null
                : new ScoreGlissandoDocument(note.Glissando.IsStart, note.Glissando.Number, note.Glissando.Kind),
            note.SoundingOctavesAboveNotated);

    private static ScoreRestDocument ToDocument(ScoreRest rest) =>
        new(
            ToDocument(rest.NoteValue),
            rest.MeasureIndex,
            rest.BeatOffset,
            rest.Staff);

    private static ScoreMeasure FromDocument(ScoreMeasureDocument measure) =>
        new(
            measure.Notes.Select(FromDocument).ToArray(),
            measure.Rests.Select(FromDocument).ToArray());

    private static ScoreNote FromDocument(ScoreNoteDocument note) =>
        new(
            new Pitch(note.Pitch.Letter, note.Pitch.Alter, note.Pitch.Octave),
            FromDocument(note.NoteValue),
            note.MeasureIndex,
            note.BeatOffset,
            note.Staff,
            note.TiesToNext,
            note.IsChordContinuation,
            note.BeamState,
            note.StemDirection,
            note.Fingering is null
                ? null
                : new ScoreFingering(note.Fingering.Number, note.Fingering.Placement),
            note.Accidental,
            note.Fermata,
            note.Articulation,
            note.Ornament,
            note.AccidentalMark,
            note.Slur is null ? null : new ScoreSlur(note.Slur.IsStart, note.Slur.Number),
            note.Arpeggio,
            note.Glissando is null
                ? null
                : new ScoreGlissando(note.Glissando.IsStart, note.Glissando.Number, note.Glissando.Kind),
            note.SoundingOctavesAboveNotated);

    private static ScoreRest FromDocument(ScoreRestDocument rest) =>
        new(
            FromDocument(rest.NoteValue),
            rest.MeasureIndex,
            rest.BeatOffset,
            rest.Staff);

    private static NoteValue FromDocument(NoteValueDocument noteValue) =>
        new(noteValue.Denominator, noteValue.Dots, noteValue.TupletActualNotes, noteValue.TupletNormalNotes);

    private static NoteValueDocument ToDocument(NoteValue noteValue) =>
        new(noteValue.Denominator, noteValue.Dots, noteValue.TupletActualNotes, noteValue.TupletNormalNotes);

    private sealed record ScoreDocument(
        int TimeSignatureNumerator,
        NoteValueDocument BeatNoteValue,
        double TempoBeatsPerMinute,
        int KeyFifths,
        IReadOnlyList<ScoreMeasureDocument> Measures);

    private sealed record ScoreMeasureDocument(
        IReadOnlyList<ScoreNoteDocument> Notes,
        IReadOnlyList<ScoreRestDocument> Rests);

    private sealed record ScoreNoteDocument(
        PitchDocument Pitch,
        NoteValueDocument NoteValue,
        int MeasureIndex,
        double BeatOffset,
        Staff Staff,
        bool TiesToNext,
        bool IsChordContinuation,
        BeamState BeamState,
        ScoreStemDirection? StemDirection,
        ScoreFingeringDocument? Fingering,
        ScoreAccidental? Accidental,
        ScoreFermata? Fermata,
        ScoreArticulation? Articulation = null,
        ScoreOrnament? Ornament = null,
        ScoreAccidental? AccidentalMark = null,
        ScoreSlurDocument? Slur = null,
        ScoreArpeggio? Arpeggio = null,
        ScoreGlissandoDocument? Glissando = null,
        int SoundingOctavesAboveNotated = 0);

    private sealed record ScoreRestDocument(
        NoteValueDocument NoteValue,
        int MeasureIndex,
        double BeatOffset,
        Staff Staff);

    private sealed record PitchDocument(NoteLetter Letter, int Alter, int Octave);

    private sealed record NoteValueDocument(
        int Denominator,
        int Dots,
        int TupletActualNotes = 1,
        int TupletNormalNotes = 1);

    private sealed record ScoreFingeringDocument(
        int Number,
        ScoreFingeringPlacement? Placement);

    private sealed record ScoreSlurDocument(bool IsStart, int Number);

    private sealed record ScoreGlissandoDocument(bool IsStart, int Number, ScoreGlissandoKind Kind);
}
