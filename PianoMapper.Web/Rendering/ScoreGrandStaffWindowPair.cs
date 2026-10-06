using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

internal static class ScoreGrandStaffWindowPair
{
    /// <summary>How many pages of <paramref name="visibleMeasureCount"/> measures a score of <paramref name="measureCount"/> spans.</summary>
    internal static int GetPageCount(
        int measureCount,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(measureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleMeasureCount);

        return (measureCount + visibleMeasureCount - 1) / visibleMeasureCount;
    }

    internal static State FromPageIndex(
        int measureCount,
        int activePageIndex,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleMeasureCount);

        int pageCount = (measureCount + visibleMeasureCount - 1) / visibleMeasureCount;
        int clampedPageIndex = Math.Clamp(activePageIndex, 0, pageCount - 1);
        bool isUpperActive = clampedPageIndex % 2 == 0;
        int activeFirstMeasure = clampedPageIndex * visibleMeasureCount;

        if (pageCount == 1)
        {
            return new State(clampedPageIndex, activeFirstMeasure, null, PhysicalRow.Upper);
        }

        int adjacentPageIndex = clampedPageIndex + 1 < pageCount
            ? clampedPageIndex + 1
            : clampedPageIndex - 1;
        int adjacentFirstMeasure = adjacentPageIndex * visibleMeasureCount;

        return isUpperActive
            ? new State(clampedPageIndex, activeFirstMeasure, adjacentFirstMeasure, PhysicalRow.Upper)
            : new State(clampedPageIndex, adjacentFirstMeasure, activeFirstMeasure, PhysicalRow.Lower);
    }

    /// <summary>
    /// Pairs the pages of an endless live grand staff, which has no final page to bound a look-ahead. The active page
    /// keeps its row by parity (even pages upper, odd pages lower) and the other row keeps the page played just
    /// before it, so the notes just played stay on screen until the cursor wraps back to that row. The first page's
    /// other row is the still-empty second page.
    /// </summary>
    internal static State FromLivePageIndex(
        int activePageIndex,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(activePageIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleMeasureCount);

        int otherPageIndex = activePageIndex == 0 ? 1 : activePageIndex - 1;
        int activeFirstMeasure = activePageIndex * visibleMeasureCount;
        int otherFirstMeasure = otherPageIndex * visibleMeasureCount;

        return activePageIndex % 2 == 0
            ? new State(activePageIndex, activeFirstMeasure, otherFirstMeasure, PhysicalRow.Upper)
            : new State(activePageIndex, otherFirstMeasure, activeFirstMeasure, PhysicalRow.Lower);
    }

    internal static State FromCursorBeats(
        int measureCount,
        double cursorBeats,
        int beatsPerMeasure,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatsPerMeasure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleMeasureCount);

        double nonNegativeBeats = Math.Max(0, cursorBeats);
        int measureIndex = (int)Math.Floor(nonNegativeBeats / beatsPerMeasure);
        int pageIndex = measureIndex / visibleMeasureCount;
        return FromPageIndex(measureCount, pageIndex, visibleMeasureCount);
    }

    internal static State FromCursorBeats(
        Score score,
        double cursorBeats,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleMeasureCount);
        if (score.Measures.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A score must contain at least one measure.");
        }

        double nonNegativeBeats = Math.Max(0, cursorBeats);
        double measureEndBeats = 0;
        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            measureEndBeats += score.Measures[measureIndex].LengthInBeats ?? score.TimeSignature.Numerator;
            if (nonNegativeBeats < measureEndBeats)
            {
                return FromPageIndex(score.Measures.Count, measureIndex / visibleMeasureCount, visibleMeasureCount);
            }
        }

        return FromPageIndex(
            score.Measures.Count,
            (score.Measures.Count - 1) / visibleMeasureCount,
            visibleMeasureCount);
    }

    internal enum PhysicalRow
    {
        Upper,
        Lower,
    }

    internal readonly record struct State(
        int ActivePageIndex,
        int UpperFirstMeasure,
        int? LowerFirstMeasure,
        PhysicalRow ActiveRow);
}
