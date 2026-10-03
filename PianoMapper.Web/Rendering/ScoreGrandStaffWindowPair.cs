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
