namespace PianoMapper.Server.Omr;

/// <summary>
/// An oemer reading made importable: compressed MusicXML plus the 1-based positions of the measures whose notes
/// do not fill their bar, which are the readings the learner should check against the image.
/// </summary>
internal sealed record OemerPostProcessResult(
    byte[] CompressedMusicXml,
    IReadOnlyList<int> SuspectMeasures);
