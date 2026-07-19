using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffBeam(
    double X0,
    double Y0,
    double X1,
    double Y1,
    int Count,
    StemDirection StemDirection);
