namespace PianoMapper.Music;

public enum ScoreBarlineStyle
{
    Regular,
    /// <summary>Two thin lines (MusicXML <c>light-light</c>), the usual mark of a section end.</summary>
    Double,
    /// <summary>A thin then a heavy line (MusicXML <c>light-heavy</c>), the final barline.</summary>
    Final,
}
