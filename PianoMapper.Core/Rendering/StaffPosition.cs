using PianoMapper.Music;

namespace PianoMapper.Rendering;

public readonly record struct StaffPosition(
    Staff Staff,
    int DiatonicOffset,
    IReadOnlyList<int> LedgerLineOffsets);
