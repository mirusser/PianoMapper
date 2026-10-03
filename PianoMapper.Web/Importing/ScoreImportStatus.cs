using System.Globalization;
using PianoMapper.Music;

namespace PianoMapper.Web.Importing;

internal static class ScoreImportStatus
{
    /// <summary>
    /// The line shown after a score loads: what loaded, and, when the importer left presentation-only constructs out or
    /// simplified them, which ones and how many times, so a file that looks different from its source says why.
    /// </summary>
    internal static string Describe(MusicXmlReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        string loaded = $"Loaded {result.Score.Title}: {result.Score.Measures.Count} measure(s).";
        if (result.Warnings.Count == 0)
        {
            return loaded;
        }

        string constructs = string.Join(", ", result.Warnings.Select(warning => warning.Count == 1
            ? warning.Construct
            : string.Create(CultureInfo.InvariantCulture, $"{warning.Construct} ×{warning.Count}")));
        return $"{loaded} Simplified on import: {constructs}.";
    }
}
