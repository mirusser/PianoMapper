namespace PianoMapper.Music;

/// <summary>
/// A <see cref="Score"/> read from MusicXML together with the presentation-only constructs that
/// were ignored, so a file is not rejected for them yet nothing is dropped without being reported.
/// </summary>
public sealed record MusicXmlReadResult(Score Score, IReadOnlyList<MusicXmlImportWarning> Warnings);
