namespace PianoMapper.Music;

/// <summary>
/// Counts the presentation-only constructs one <see cref="MusicXmlScoreReader"/> read ignored, in
/// order of first occurrence.
/// </summary>
internal sealed class MusicXmlImportWarnings
{
    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private readonly List<string> order = [];

    internal void Add(string construct)
    {
        if (counts.TryGetValue(construct, out int count))
        {
            counts[construct] = count + 1;
            return;
        }

        counts[construct] = 1;
        order.Add(construct);
    }

    internal IReadOnlyList<MusicXmlImportWarning> ToList() =>
        order.Select(construct => new MusicXmlImportWarning(construct, counts[construct])).ToArray();
}
