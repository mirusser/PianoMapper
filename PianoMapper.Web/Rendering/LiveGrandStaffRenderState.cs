namespace PianoMapper.Web.Rendering;

/// <summary>
/// What the live grand staff hands the browser on one tick: each row's scene without its cursor line, and the cursor
/// state of the row that holds the cursor (the other row has none).
/// </summary>
internal readonly record struct LiveGrandStaffRenderState(
    GrandStaffScene UpperScene,
    LiveGrandStaffCursorState? UpperCursor,
    GrandStaffScene LowerScene,
    LiveGrandStaffCursorState? LowerCursor);
