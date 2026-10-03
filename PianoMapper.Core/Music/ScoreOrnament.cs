namespace PianoMapper.Music;

/// <summary>
/// The ornaments on one note. Like <see cref="ScoreArticulation"/> they combine as flags; the persisted form is the
/// camelCase name list, which keeps older single-name documents readable.
/// </summary>
[Flags]
public enum ScoreOrnament
{
    TrillMark = 1,
    Turn = 2,
    InvertedTurn = 4,
    Mordent = 8,
    InvertedMordent = 16,
    Shake = 32,
}
