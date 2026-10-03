namespace PianoMapper.Music;

/// <summary>
/// What a measure's left or right barline carries beyond a plain line: its style, a repeat sign, a volta bracket end,
/// a fermata, and a segno or coda sign. Null on the measure means a plain barline.
/// </summary>
/// <param name="Repeat">A repeat sign; its dots and heavy line replace <paramref name="Style"/>.</param>
/// <param name="RepeatTimes">How many times a backward repeat plays the section in all (MusicXML's default is 2).</param>
/// <param name="Mark">A segno or coda sign; <paramref name="MarkCount"/> says how many are repeated side by side.</param>
public sealed record ScoreBarline(
    ScoreBarlineStyle Style = ScoreBarlineStyle.Regular,
    ScoreRepeatDirection? Repeat = null,
    int RepeatTimes = 2,
    ScoreEnding? Ending = null,
    ScoreFermata? Fermata = null,
    ScoreBarlineMark? Mark = null,
    int MarkCount = 1);
