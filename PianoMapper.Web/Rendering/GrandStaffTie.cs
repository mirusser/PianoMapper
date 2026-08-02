using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffTie(
    double X0,
    double Y0,
    double X1,
    double Y1,
    StemDirection CurveDirection,
    bool IsActive);
