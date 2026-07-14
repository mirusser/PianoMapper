using PianoMapper.Music;

namespace PianoMapper.Rendering;

internal readonly record struct StaffPosition(
    Staff Staff,
    int DiatonicOffset,
    IReadOnlyList<int> LedgerLineOffsets);
