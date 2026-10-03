namespace PianoMapper.Web.Practice;

/// <summary>Whether the weak-note drill can start, and if not, why (shown next to the disabled button).</summary>
internal sealed record DrillAvailability(bool IsAvailable, string? Reason)
{
    internal static DrillAvailability Available { get; } = new(true, null);

    internal static DrillAvailability Unavailable(string reason) => new(false, reason);
}
