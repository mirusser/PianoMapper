namespace PianoMapper.Rendering;

/// <summary>
/// Pure magnitude bins -> screen-space math for the spectrum view, confined to an
/// inset panel in NDC space distinct from the oscilloscope's panel. Kept free of any
/// GL dependency so it can be unit tested without a live rendering context.
/// </summary>
public static class SpectrumLayout
{
    public const float PanelX0 = -0.98f;
    public const float PanelX1 = 0.98f;
    public const float PanelY0 = -0.98f;
    public const float PanelY1 = -0.72f;

    // Only the low bins carry musically-relevant harmonic content for piano notes;
    // higher bins would render as imperceptibly thin bars if all were shown.
    public const int VisibleBinCount = 64;

    public static IReadOnlyList<BarRect> BuildBars(IReadOnlyList<double> magnitudes)
    {
        if (magnitudes.Count == 0)
        {
            return [];
        }

        var visibleCount = Math.Min(VisibleBinCount, magnitudes.Count);
        var maxMagnitude = 0.0;
        for (var i = 0; i < visibleCount; i++)
        {
            maxMagnitude = Math.Max(maxMagnitude, magnitudes[i]);
        }

        if (maxMagnitude <= 0.0)
        {
            return [];
        }

        var bars = new List<BarRect>(visibleCount);
        var panelWidth = PanelX1 - PanelX0;
        var barWidth = panelWidth / visibleCount;

        for (var i = 0; i < visibleCount; i++)
        {
            var x0 = PanelX0 + i * barWidth;
            var x1 = x0 + barWidth * 0.85f; // small gap between bars
            var normalizedHeight = (float)(magnitudes[i] / maxMagnitude);
            var y1 = PanelY0 + normalizedHeight * (PanelY1 - PanelY0);

            bars.Add(new BarRect(x0, x1, PanelY0, y1));
        }

        return bars;
    }
}
