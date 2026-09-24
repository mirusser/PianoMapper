using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// A printed-score slur: a thin stroked arc from a slur-start note to its matching slur-stop
/// note, resolved by <see cref="GrandStaffSceneBuilder"/> from <see cref="PianoMapper.Music.ScoreSlur"/>
/// pairing data. Shares <see cref="GrandStaffTie"/>'s X0/Y0/X1/Y1/direction shape, but is drawn
/// with its own thin-stroke curve math (<c>drawSlur</c> in canvas.js) rather than a tie's tapered,
/// filled bezier — a slur typically spans many notes and reads as a single arc over the whole
/// phrase, not a short tie shape.
/// </summary>
public sealed record GrandStaffSlur(
    double X0,
    double Y0,
    double X1,
    double Y1,
    StemDirection CurveDirection);
