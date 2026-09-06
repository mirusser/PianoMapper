using PianoMapper.Music;

namespace PianoMapper.Web.Rendering;

internal sealed record PianoKeyboardKey(
    Pitch Pitch,
    bool IsBlack,
    double LeftPercent,
    double WidthPercent,
    bool IsActive);
