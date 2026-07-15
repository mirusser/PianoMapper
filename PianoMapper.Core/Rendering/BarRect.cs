namespace PianoMapper.Rendering;

/// <summary>
/// A note bar's extent in normalized device coordinates ([-1, 1] on both axes).
/// </summary>
public readonly record struct BarRect(float X0, float X1, float Y0, float Y1);
