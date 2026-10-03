using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>The words for an insight; the choice of what to say lives in <see cref="SightReadingInsights"/>.</summary>
internal static class SightReadingInsightText
{
    internal static string DescribeTrend(TrendDirection direction) => direction switch
    {
        TrendDirection.Improving => "improving",
        TrendDirection.Declining => "slipping",
        TrendDirection.Steady => "steady",
        _ => "too few sessions to see a trend",
    };

    /// <summary>E.g. "often plays a step higher than written on bass notes".</summary>
    internal static string DescribeHabit(ConfusionHabit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);
        string staff = habit.Staff == Staff.Bass ? "bass" : "treble";
        return $"often plays {PitchDistance.Describe(habit.Difference)} than written on {staff} notes";
    }
}
