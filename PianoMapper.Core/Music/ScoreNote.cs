namespace PianoMapper.Music;

public sealed record ScoreNote(
    Pitch Pitch,
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff,
    bool TiesToNext = false,
    bool IsChordContinuation = false,
    BeamState BeamState = BeamState.None,
    ScoreStemDirection? StemDirection = null,
    ScoreFingering? Fingering = null,
    ScoreAccidental? Accidental = null,
    ScoreFermata? Fermata = null,
    ScoreArticulation? Articulation = null,
    ScoreOrnament? Ornament = null,
    ScoreAccidental? AccidentalMark = null,
    ScoreSlur? Slur = null,
    ScoreArpeggio? Arpeggio = null,
    ScoreGlissando? Glissando = null,
    int SoundingOctavesAboveNotated = 0)
{
    private ScoreBeamList beams = ScoreBeamList.Empty;

    /// <summary>
    /// All visual beam levels imported from MusicXML. <see cref="BeamState"/> remains the
    /// compatibility representation of the primary, full beam level.
    /// </summary>
    public IReadOnlyList<ScoreBeam> Beams
    {
        get => beams;
        init => beams = new ScoreBeamList(value ?? []);
    }

    /// <summary>
    /// The MusicXML <c>stem</c> element's <c>default-y</c> endpoint, in tenths of an interline
    /// space measured from the top staff line. Null means the renderer chooses the endpoint.
    /// </summary>
    public double? StemEndYInTenths { get; init; }
}

internal sealed class ScoreBeamList : IReadOnlyList<ScoreBeam>, IEquatable<ScoreBeamList>
{
    private readonly ScoreBeam[] values;

    public static ScoreBeamList Empty { get; } = new([]);

    public ScoreBeamList(IEnumerable<ScoreBeam> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values.ToArray();
    }

    public int Count => values.Length;

    public ScoreBeam this[int index] => values[index];

    public bool Equals(ScoreBeamList? other) =>
        ReferenceEquals(this, other) || (other is not null && values.SequenceEqual(other.values));

    public override bool Equals(object? obj) => Equals(obj as ScoreBeamList);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ScoreBeam beam in values)
        {
            hash.Add(beam);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<ScoreBeam> GetEnumerator() => ((IEnumerable<ScoreBeam>)values).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => values.GetEnumerator();
}
