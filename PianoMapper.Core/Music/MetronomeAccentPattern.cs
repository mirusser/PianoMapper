namespace PianoMapper.Music;

/// <summary>
/// The beat indices within one measure that receive an accent. The first beat is always the downbeat; subsequent
/// entries are the starts of felt beat groups, such as beat four in 6/8 or beat three in 5/8 grouped 2+3.
/// </summary>
public sealed class MetronomeAccentPattern
{
    private MetronomeAccentPattern(int[] groupStartBeatIndices) =>
        GroupStartBeatIndices = groupStartBeatIndices;

    public IReadOnlyList<int> GroupStartBeatIndices { get; }

    /// <summary>
    /// Resolves either the meter's natural grouping or explicit group lengths. Explicit lengths must cover exactly
    /// one measure, which prevents the audible accent cycle from drifting away from bar lines.
    /// </summary>
    public static MetronomeAccentPattern Create(MetronomeGrid grid, IReadOnlyList<int>? groupLengths = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        IReadOnlyList<int> resolvedGroupLengths = groupLengths ?? GetNaturalGroupLengths(grid);
        if (resolvedGroupLengths.Count == 0 || resolvedGroupLengths.Any(length => length <= 0) ||
            resolvedGroupLengths.Sum() != grid.TimeSignature.Numerator)
        {
            throw new ArgumentException(
                "Metronome beat groups must contain positive lengths that fill exactly one measure.",
                nameof(groupLengths));
        }

        var groupStarts = new int[resolvedGroupLengths.Count];
        int beatIndex = 0;
        for (int groupIndex = 0; groupIndex < resolvedGroupLengths.Count; groupIndex++)
        {
            groupStarts[groupIndex] = beatIndex;
            beatIndex += resolvedGroupLengths[groupIndex];
        }

        return new MetronomeAccentPattern(groupStarts);
    }

    private static IReadOnlyList<int> GetNaturalGroupLengths(MetronomeGrid grid)
    {
        if (grid.BeatsPerGroup == 1)
        {
            return [grid.TimeSignature.Numerator];
        }

        return Enumerable.Repeat(grid.BeatsPerGroup, grid.TimeSignature.Numerator / grid.BeatsPerGroup).ToArray();
    }
}
