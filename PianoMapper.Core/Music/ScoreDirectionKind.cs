namespace PianoMapper.Music;

public enum ScoreDirectionKind
{
    /// <summary>A dynamic mark such as p, mf or sfz; <see cref="ScoreDirection.Text"/> is its name.</summary>
    Dynamics,
    /// <summary>Printed text: an expression or tempo word, "D.C. al Coda", "cresc.".</summary>
    Words,
    /// <summary>A printed tempo mark such as "♩ = 120".</summary>
    Metronome,
    Rehearsal,
    Segno,
    Coda,
    /// <summary>The opening of a crescendo hairpin; it closes at the <see cref="WedgeStop"/> with the same number.</summary>
    CrescendoStart,
    /// <summary>The opening of a diminuendo hairpin; it closes at the <see cref="WedgeStop"/> with the same number.</summary>
    DiminuendoStart,
    WedgeStop,
    PedalStart,
    PedalStop,
    PedalChange,
    /// <summary>A chord name above the staff, such as "Cmaj7/E".</summary>
    ChordSymbol,
}
