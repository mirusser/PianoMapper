using System.Collections.Frozen;
using System.Xml.Linq;

namespace PianoMapper.Music;

// The note-level marks (articulations, ornaments, slurs). A note may carry several of each; the ones this app has no
// glyph for are reported as warnings instead of rejecting the file.
public sealed partial class MusicXmlScoreReader
{
    private static readonly FrozenDictionary<string, ScoreArticulation> ArticulationsByElement =
        new Dictionary<string, ScoreArticulation>(StringComparer.Ordinal)
        {
            ["staccato"] = ScoreArticulation.Staccato,
            ["tenuto"] = ScoreArticulation.Tenuto,
            ["accent"] = ScoreArticulation.Accent,
            ["staccatissimo"] = ScoreArticulation.Staccatissimo,
            ["spiccato"] = ScoreArticulation.Staccatissimo,
            ["detached-legato"] = ScoreArticulation.Staccato | ScoreArticulation.Tenuto,
            ["strong-accent"] = ScoreArticulation.StrongAccent,
            ["breath-mark"] = ScoreArticulation.BreathMark,
            ["caesura"] = ScoreArticulation.Caesura,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, ScoreOrnament> OrnamentsByElement =
        new Dictionary<string, ScoreOrnament>(StringComparer.Ordinal)
        {
            ["trill-mark"] = ScoreOrnament.TrillMark,
            ["turn"] = ScoreOrnament.Turn,
            ["inverted-turn"] = ScoreOrnament.InvertedTurn,
            ["mordent"] = ScoreOrnament.Mordent,
            ["inverted-mordent"] = ScoreOrnament.InvertedMordent,
            ["shake"] = ScoreOrnament.Shake,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static ScoreArticulation? ParseArticulation(XElement noteElement, MusicXmlImportWarnings warnings)
    {
        ScoreArticulation? articulation = null;
        foreach (var child in GetNotationChildren(noteElement, ArticulationsElementName))
        {
            string name = child.Name.LocalName;
            if (ArticulationsByElement.TryGetValue(name, out var mapped))
            {
                articulation = (articulation ?? 0) | mapped;
            }
            else
            {
                warnings.Add(name);
            }
        }

        return articulation;
    }

    private static ScoreOrnament? ParseOrnament(XElement noteElement, MusicXmlImportWarnings warnings)
    {
        ScoreOrnament? ornament = null;
        foreach (var child in GetNotationChildren(noteElement, OrnamentsElementName))
        {
            string name = child.Name.LocalName;
            if (OrnamentsByElement.TryGetValue(name, out var mapped))
            {
                ornament = (ornament ?? 0) | mapped;
            }
            else
            {
                warnings.Add(name);
            }
        }

        return ornament;
    }

    // Every child of every <articulations>/<ornaments> (a note may repeat the container) in document order.
    private static IEnumerable<XElement> GetNotationChildren(XElement noteElement, string containerName) =>
        (FindChild(noteElement, NotationsElementName)?.Elements() ?? [])
            .Where(element => element.Name.LocalName == containerName)
            .SelectMany(container => container.Elements());

    // Slur ends are chained in document order; "continue" only marks a slur running on across a system break, which the
    // start and stop already say, so it adds nothing.
    private static ScoreSlur? ParseSlur(XElement noteElement)
    {
        ScoreSlur? chain = null;
        var slurs = (FindChild(noteElement, NotationsElementName)?.Elements() ?? [])
            .Where(element => element.Name.LocalName == SlurElementName && element.Attribute("type")?.Value != "continue")
            .Reverse();
        foreach (var slur in slurs)
        {
            chain = new ScoreSlur(
                ParsePairingType(slur, SlurElementName),
                ParsePairingNumber(slur, SlurElementName),
                chain);
        }

        return chain;
    }
}
