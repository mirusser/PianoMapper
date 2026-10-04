namespace PianoMapper.Web.Rendering;

/// <summary>
/// The live grand-staff cursor data handed to the browser so its audio-clock position can move
/// independently of the C# scene refresh loop.
/// </summary>
public sealed record LiveGrandStaffCursorState(
    double BeatsPerMinute,
    int BeatsPerMeasure,
    int FirstVisibleMeasure,
    double CursorY0,
    double CursorY1);
