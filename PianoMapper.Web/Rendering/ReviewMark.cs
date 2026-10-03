namespace PianoMapper.Web.Rendering;

/// <summary>
/// How one note of a finished exercise is marked on the staff: its prompt's outcome, as a separate channel from the
/// live-grading <c>Verdict</c> (whose ordinals index <c>verdictColors</c> in <c>canvas.js</c>). The ordinals cross the
/// JS interop seam by hand, so members are only ever appended; see <c>GrandStaffSceneContractTests</c>.
/// </summary>
public enum ReviewMark
{
    /// <summary>First-try correct on every graded axis. Nothing is drawn for it.</summary>
    Clean,

    /// <summary>The right pitch, but early, late, released too soon or held too long.</summary>
    Timing,

    /// <summary>A wrong key was pressed before the right one.</summary>
    Pitch,

    /// <summary>The prompt was never played (a play-along run moved past it).</summary>
    Missed,
}
