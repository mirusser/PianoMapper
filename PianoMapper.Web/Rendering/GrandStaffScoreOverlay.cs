namespace PianoMapper.Web.Rendering;

/// <summary>
/// The moving ink drawn over an otherwise static score scene: the practice cursor, held notes,
/// and the held notes' ledger lines. Sent independently so cursor ticks do not re-send notation.
/// </summary>
public sealed record GrandStaffScoreOverlay(
    GrandStaffLine? Cursor,
    IReadOnlyList<GrandStaffNote> Notes,
    IReadOnlyList<GrandStaffLine> LedgerLines)
{
    internal static GrandStaffScoreOverlay Empty { get; } = new(null, [], []);
}
