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
                            Fermata: ScoreFermata.Upright),
                        new ScoreNote(
                            new Pitch(NoteLetter.B, -1, 2),
                            new NoteValue(16),
                            0,
                            1.25,
                            Staff.Bass,
                            BeamState: BeamState.End),
                    ],
                    [new ScoreRest(new NoteValue(4, 1), 0, 2.0, Staff.Bass)]),
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
    }

    [Fact]
    public void Deserialize_UnknownDocumentVersion_Throws()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            ScoreDocumentSerializer.Deserialize("Title", "{}", ScoreDocumentSerializer.CurrentVersion + 1));

        Assert.Contains("document version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
