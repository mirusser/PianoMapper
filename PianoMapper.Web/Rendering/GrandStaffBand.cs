namespace PianoMapper.Web.Rendering;

/// <summary>
/// A background band drawn behind a staff's note-label/fingering rows, so they read as a
/// distinct annotation strip belonging to that staff rather than floating notation.
/// </summary>
public sealed record GrandStaffBand(double X0, double Y0, double X1, double Y1);
