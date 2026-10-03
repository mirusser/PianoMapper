namespace PianoMapper.Music;

/// <summary>
/// A presentation-only MusicXML construct the reader accepted but did not use, counted across the
/// whole file — for example "dynamics" with a count of 12. Constructs that change pitch or timing
/// never appear here: they are either supported or fail the import with a readable error.
/// </summary>
public sealed record MusicXmlImportWarning(string Construct, int Count);
