namespace PianoMapper.Rendering;

public readonly record struct StaffPlacement(
    StaffPosition DiatonicPosition,
    float Y,
    IReadOnlyList<float> LedgerLineYs,
    bool NeedsAccidental)
{
    public Music.Staff Staff => DiatonicPosition.Staff;
}
