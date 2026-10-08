namespace PianoMapper.Music;

/// <summary>Describes an infeasible score, profile, or locked fingering without changing the source score.</summary>
public sealed class ScoreFingeringGenerationException : InvalidOperationException
{
    public ScoreFingeringGenerationException(ScoreFingeringNoteAddress? address, string message)
        : base(message)
    {
        Address = address;
    }

    public ScoreFingeringNoteAddress? Address { get; }
}
