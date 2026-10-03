using PianoMapper.Music;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreDocumentSerializerTests
{
    [Fact]
    public void SerializeDeserialize_AllSupportedFields_PreservesScore()
    {
        var expected = new Score(
            "Round trip",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(92.5),
            -3,
            [
                new ScoreMeasure(
                    [
                        new ScoreNote(
                            new Pitch(NoteLetter.C, 1, 4),
                            new NoteValue(8, 1),
                            0,
                            0.5,
                            Staff.Treble,
                            TiesToNext: true,
                            IsChordContinuation: true,
                            BeamState: BeamState.Begin,
                            StemDirection: ScoreStemDirection.Up,
                            Fingering: new ScoreFingering(3, ScoreFingeringPlacement.Above),
                            Accidental: ScoreAccidental.Sharp,
                            Fermata: ScoreFermata.Upright,
                            Articulation: ScoreArticulation.Staccato,
                            Ornament: ScoreOrnament.TrillMark,
                            AccidentalMark: ScoreAccidental.Flat,
                            Slur: new ScoreSlur(IsStart: true, Number: 2),
                            Arpeggio: ScoreArpeggio.Arpeggiate,
                            Glissando: new ScoreGlissando(IsStart: false, Number: 3, ScoreGlissandoKind.Slide),
                            SoundingOctavesAboveNotated: 1),
                        new ScoreNote(
                            new Pitch(NoteLetter.B, -1, 2),
                            new NoteValue(16),
                            0,
                            1.25,
                            Staff.Bass,
                            BeamState: BeamState.End),
                        new ScoreNote(
                            new Pitch(NoteLetter.D, 0, 4),
                            new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2),
                            0,
                            2.0,
                            Staff.Treble),
                    ],
                    [new ScoreRest(new NoteValue(4, 1), 0, 2.0, Staff.Bass)],
                    LengthInBeats: 1.5),
            ]);

        string json = ScoreDocumentSerializer.Serialize(expected);
        Score actual = ScoreDocumentSerializer.Deserialize(
            expected.Title,
            json,
            ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.TimeSignature, actual.TimeSignature);
        Assert.Equal(expected.Tempo, actual.Tempo);
        Assert.Equal(expected.KeyFifths, actual.KeyFifths);
        Assert.Equal(expected.Measures[0].Notes, actual.Measures[0].Notes);
        Assert.Equal(expected.Measures[0].Rests, actual.Measures[0].Rests);
        Assert.Equal(1.5, actual.Measures[0].LengthInBeats);
        Assert.Equal(1, actual.Measures[0].Notes[0].SoundingOctavesAboveNotated);
        Assert.DoesNotContain("midiNumber", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frequency", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_VersionOneWithoutNotationFields_DefaultsToAbsent()
    {
        const string json = """
            {
              "timeSignatureNumerator": 4,
              "beatNoteValue": { "denominator": 4, "dots": 0 },
              "tempoBeatsPerMinute": 120,
              "keyFifths": 0,
              "measures": [
                {
                  "notes": [
                    {
                      "pitch": { "letter": "c", "alter": 0, "octave": 4 },
                      "noteValue": { "denominator": 4, "dots": 0 },
                      "measureIndex": 0,
                      "beatOffset": 0,
                      "staff": "treble",
                      "tiesToNext": false,
                      "beamState": "none",
                      "stemDirection": null,
                      "fingering": null
                    }
                  ],
                  "rests": []
                }
              ]
            }
            """;

        Score score = ScoreDocumentSerializer.Deserialize(
            "Version one",
            json,
            ScoreDocumentSerializer.CurrentVersion);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);
        Assert.Null(note.Accidental);
        Assert.Null(note.Fermata);
        Assert.False(note.IsChordContinuation);
        Assert.Equal(1, note.NoteValue.TupletActualNotes);
        Assert.Equal(1, note.NoteValue.TupletNormalNotes);
        Assert.Null(note.Articulation);
        Assert.Null(note.Ornament);
        Assert.Null(note.AccidentalMark);
        Assert.Null(note.Slur);
        Assert.Null(note.Arpeggio);
        Assert.Null(note.Glissando);
        Assert.Equal(0, note.SoundingOctavesAboveNotated);
        Assert.Null(Assert.Single(score.Measures).LengthInBeats);
    }

    [Fact]
    public void Deserialize_UnknownDocumentVersion_Throws()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            ScoreDocumentSerializer.Deserialize("Title", "{}", ScoreDocumentSerializer.CurrentVersion + 1));

        Assert.Contains("document version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeDeserialize_SeveralMarksOnOneNote_PreservesThemAndNoteEquality()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Articulation: ScoreArticulation.Staccato | ScoreArticulation.Accent,
            Ornament: ScoreOrnament.TrillMark | ScoreOrnament.Mordent,
            Slur: new ScoreSlur(false, 1, new ScoreSlur(true, 1, new ScoreSlur(true, 2))));
        var expected = new Score(
            "Marks",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            0,
            [new ScoreMeasure([note], [])]);

        Score actual = ScoreDocumentSerializer.Deserialize(
            expected.Title,
            ScoreDocumentSerializer.Serialize(expected),
            ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal(note, Assert.Single(Assert.Single(actual.Measures).Notes));
    }

    [Fact]
    public void Deserialize_VersionOneDocumentWithSingleMarkNames_ReadsThemAsFlagsAndChains()
    {
        const string json = """
            {
              "timeSignatureNumerator": 4,
              "beatNoteValue": { "denominator": 4, "dots": 0 },
              "tempoBeatsPerMinute": 120,
              "keyFifths": 0,
              "measures": [
                {
                  "notes": [
                    {
                      "pitch": { "letter": "c", "alter": 0, "octave": 4 },
                      "noteValue": { "denominator": 4, "dots": 0 },
                      "measureIndex": 0,
                      "beatOffset": 0,
                      "staff": "treble",
                      "tiesToNext": false,
                      "beamState": "none",
                      "articulation": "staccatissimo",
                      "ornament": "trillMark",
                      "slur": { "isStart": true, "number": 1 }
                    }
                  ],
                  "rests": []
                }
              ]
            }
            """;

        Score score = ScoreDocumentSerializer.Deserialize(
            "Old marks",
            json,
            ScoreDocumentSerializer.CurrentVersion);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);
        Assert.Equal(ScoreArticulation.Staccatissimo, note.Articulation);
        Assert.Equal(ScoreOrnament.TrillMark, note.Ornament);
        Assert.Equal(new ScoreSlur(true, 1), note.Slur);
    }

    [Fact]
    public void SerializeDeserialize_MeasureRest_PreservesTheFlagAndOlderRestsDefaultToFalse()
    {
        var expected = new Score(
            "Rests",
            new TimeSignature(3, new NoteValue(4)),
            new Tempo(100),
            0,
            [new ScoreMeasure([], [new ScoreRest(new NoteValue(2, 1), 0, 0, Staff.Treble, IsMeasureRest: true)])]);

        string json = ScoreDocumentSerializer.Serialize(expected);
        Score actual = ScoreDocumentSerializer.Deserialize("Rests", json, ScoreDocumentSerializer.CurrentVersion);
        Score older = ScoreDocumentSerializer.Deserialize(
            "Older rests",
            json.Replace(",\"isMeasureRest\":true", string.Empty, StringComparison.Ordinal),
            ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal(expected.Measures[0].Rests, actual.Measures[0].Rests);
        Assert.False(Assert.Single(Assert.Single(older.Measures).Rests).IsMeasureRest);
    }

    [Fact]
    public void SerializeDeserialize_MeasureKeyChange_PreservesItAndOlderMeasuresHaveNone()
    {
        var expected = new Score(
            "Keys",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            1,
            [new ScoreMeasure([], []), new ScoreMeasure([], [], KeyFifths: -3)]);

        string json = ScoreDocumentSerializer.Serialize(expected);
        Score actual = ScoreDocumentSerializer.Deserialize("Keys", json, ScoreDocumentSerializer.CurrentVersion);
        Score older = ScoreDocumentSerializer.Deserialize(
            "Older keys",
            json.Replace("\"keyFifths\":-3", "\"unrelated\":0", StringComparison.Ordinal),
            ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal([null, -3], actual.Measures.Select(measure => measure.KeyFifths));
        Assert.All(older.Measures, measure => Assert.Null(measure.KeyFifths));
    }

    [Fact]
    public void SerializeDeserialize_MeasureBarlines_PreserveRepeatsEndingsAndMarks()
    {
        var left = new ScoreBarline(Repeat: ScoreRepeatDirection.Forward, Ending: new ScoreEnding("1, 2", ScoreEndingType.Start));
        var right = new ScoreBarline(
            ScoreBarlineStyle.Final,
            ScoreRepeatDirection.Backward,
            3,
            new ScoreEnding("1, 2", ScoreEndingType.Stop),
            ScoreFermata.Inverted,
            ScoreBarlineMark.Coda,
            2);
        var expected = new Score(
            "Barlines",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            0,
            [new ScoreMeasure([], [], LeftBarline: left, RightBarline: right), new ScoreMeasure([], [])]);

        string json = ScoreDocumentSerializer.Serialize(expected);
        Score actual = ScoreDocumentSerializer.Deserialize("Barlines", json, ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal(left, actual.Measures[0].LeftBarline);
        Assert.Equal(right, actual.Measures[0].RightBarline);
        Assert.Null(actual.Measures[1].LeftBarline);
        Assert.Null(actual.Measures[1].RightBarline);
    }

    [Fact]
    public void SerializeDeserialize_MeasureDirections_PreserveEveryField()
    {
        ScoreDirection[] directions =
        [
            new(ScoreDirectionKind.Dynamics, 1.5, Staff.Bass, "mf", IsBelow: true),
            new(ScoreDirectionKind.CrescendoStart, 0, Staff.Treble, IsBelow: true, Number: 2),
            new(ScoreDirectionKind.ChordSymbol, 2, Staff.Treble, "Cmaj7/E"),
        ];
        var expected = new Score(
            "Directions",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            0,
            [new ScoreMeasure([], [], Directions: directions), new ScoreMeasure([], [])]);

        string json = ScoreDocumentSerializer.Serialize(expected);
        Score actual = ScoreDocumentSerializer.Deserialize("Directions", json, ScoreDocumentSerializer.CurrentVersion);

        Assert.Equal(directions, actual.Measures[0].Directions);
        Assert.Null(actual.Measures[1].Directions);
    }
}
