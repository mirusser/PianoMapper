using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

internal static class ScoreGrandStaffWindowPair
{
    internal static State FromPageIndex(int measureCount, int activePageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measureCount);

        int pageCount = (measureCount + GrandStaffLayout.VisibleMeasureCount - 1)
            / GrandStaffLayout.VisibleMeasureCount;
        int clampedPageIndex = Math.Clamp(activePageIndex, 0, pageCount - 1);
        bool isUpperActive = clampedPageIndex % 2 == 0;
        int activeFirstMeasure = clampedPageIndex * GrandStaffLayout.VisibleMeasureCount;

        if (pageCount == 1)
        {
            return new State(clampedPageIndex, activeFirstMeasure, null, PhysicalRow.Upper);
        }

        int adjacentPageIndex = clampedPageIndex + 1 < pageCount
            ? clampedPageIndex + 1
            : clampedPageIndex - 1;
        int adjacentFirstMeasure = adjacentPageIndex * GrandStaffLayout.VisibleMeasureCount;

        return isUpperActive
            ? new State(clampedPageIndex, activeFirstMeasure, adjacentFirstMeasure, PhysicalRow.Upper)
            : new State(clampedPageIndex, adjacentFirstMeasure, activeFirstMeasure, PhysicalRow.Lower);
    }

    internal static State FromCursorBeats(
        int measureCount,
        double cursorBeats,
        int beatsPerMeasure)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatsPerMeasure);

        double nonNegativeBeats = Math.Max(0, cursorBeats);
        int measureIndex = (int)Math.Floor(nonNegativeBeats / beatsPerMeasure);
        int pageIndex = measureIndex / GrandStaffLayout.VisibleMeasureCount;
        return FromPageIndex(measureCount, pageIndex);
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
