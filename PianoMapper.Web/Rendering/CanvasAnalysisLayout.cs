namespace PianoMapper.Web.Rendering;

internal sealed record CanvasAnalysisLayout(
    double SpectrumX0,
    double SpectrumX1,
    double SpectrumY0,
    double SpectrumY1,
    int SpectrumVisibleBinCount);
