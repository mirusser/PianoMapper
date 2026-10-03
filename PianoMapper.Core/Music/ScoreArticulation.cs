namespace PianoMapper.Music;

/// <summary>
/// The articulation marks on one note. A note can carry several (a staccato under an accent), so the values
/// combine as flags; the persisted form is the camelCase name list, which keeps older single-name documents readable.
/// </summary>
[Flags]
public enum ScoreArticulation
{
    Staccato = 1,
    Tenuto = 2,
    Accent = 4,
    Staccatissimo = 8,
    StrongAccent = 16,
    BreathMark = 32,
    Caesura = 64,
}
