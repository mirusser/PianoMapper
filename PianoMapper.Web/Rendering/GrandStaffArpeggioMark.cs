namespace PianoMapper.Web.Rendering;

/// <summary>
/// A vertical arpeggio mark to the left of a chord: a wavy line (<see cref="IsNonArpeggiate"/>
/// false) for a MusicXML &lt;arpeggiate/&gt;, or a bracket (<see cref="IsNonArpeggiate"/> true)
/// for &lt;non-arpeggiate/&gt;. Spans <see cref="Y0"/> to <see cref="Y1"/>, the chord's full
/// vertical extent.
/// </summary>
public sealed record GrandStaffArpeggioMark(double X, double Y0, double Y1, bool IsNonArpeggiate);
