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
    int SoundingOctavesAboveNotated = 0);
