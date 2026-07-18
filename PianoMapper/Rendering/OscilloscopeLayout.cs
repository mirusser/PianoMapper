using OpenTK.Mathematics;

namespace PianoMapper.Rendering;

/// <summary>
/// Pure sample-window -> screen-space math for the oscilloscope, confined to an inset
/// panel in NDC space so it doesn't overlap the piano-roll or the spectrum panel. Kept
/// free of any GL dependency so it can be unit tested without a live rendering context.
/// </summary>
public static class OscilloscopeLayout
{
    public const float PanelX0 = -0.98f;
    public const float PanelX1 = 0.98f;
    public const float PanelY0 = -0.62f;
    public const float PanelY1 = -0.40f;

    public static IReadOnlyList<Vector2> BuildPolyline(IReadOnlyList<short> window)
    {
        if (window.Count == 0)
        {
            return [];
        }

        var points = new List<Vector2>(window.Count);
        var panelHeight = PanelY1 - PanelY0;
        var centerY = (PanelY0 + PanelY1) / 2f;

        for (int i = 0; i < window.Count; i++)
        {
            var x = window.Count == 1
                ? PanelX0
                : PanelX0 + (PanelX1 - PanelX0) * i / (window.Count - 1);
            float normalizedAmplitude = Math.Clamp(window[i] / (float)short.MaxValue, -1f, 1f);
            var y = centerY + normalizedAmplitude * (panelHeight / 2f);
            points.Add(new Vector2(x, y));
        }

        return points;
    }
}
