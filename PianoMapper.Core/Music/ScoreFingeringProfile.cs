namespace PianoMapper.Music;

/// <summary>Independent right- and left-hand reach profiles used by fingering generation.</summary>
public sealed class ScoreFingeringProfile
{
    private const double DefaultComfortableSpan = 5;
    private const double DefaultMaximumSpan = 7;

    public ScoreFingeringProfile(FingeringHandProfile rightHand, FingeringHandProfile leftHand)
    {
        ArgumentNullException.ThrowIfNull(rightHand);
        ArgumentNullException.ThrowIfNull(leftHand);
        RightHand = rightHand;
        LeftHand = leftHand;
    }

    /// <summary>
    /// The generic thumb-to-little-finger reach of <see cref="Default"/>: comfortable up to 6.5 and at most 9
    /// white-key centre spacings (a tenth, C4 to E5; C4 to D5 is 8). The maximum was chosen so that the W3C
    /// <c>accidentals</c> sample's four-note G3 to B4 chord generates. It is not a personal measurement.
    /// </summary>
    public static FingeringReach DefaultThumbToLittleFingerReach { get; } = new(6.5, 9);

    /// <summary>
    /// Generic, unmeasured defaults for both hands, used when a caller supplies no profile. Every other finger pair
    /// is limited to 5 comfortable and 7 maximum white-key spacings: an octave, so that a chord such as the W3C
    /// <c>accidentals</c> sample's G3 F4 G4 B4 can put its octave between the thumb and a middle or ring finger.
    /// </summary>
    public static ScoreFingeringProfile Default { get; } = new(
        CreateHandWithThumbToLittleFingerReach(DefaultThumbToLittleFingerReach),
        CreateHandWithThumbToLittleFingerReach(DefaultThumbToLittleFingerReach));

    public FingeringHandProfile RightHand { get; }

    public FingeringHandProfile LeftHand { get; }

    /// <summary>
    /// A profile that sets only the thumb-to-little-finger reach of each hand. Every other finger pair keeps the
    /// generic limits, but never exceeds the thumb-to-little-finger reach, since a smaller span cannot be wider than
    /// the hand's widest one.
    /// </summary>
    public static ScoreFingeringProfile CreateWithThumbToLittleFingerReach(
        FingeringReach rightHandReach,
        FingeringReach leftHandReach)
    {
        ArgumentNullException.ThrowIfNull(rightHandReach);
        ArgumentNullException.ThrowIfNull(leftHandReach);
        return new ScoreFingeringProfile(
            CreateHandWithThumbToLittleFingerReach(rightHandReach),
            CreateHandWithThumbToLittleFingerReach(leftHandReach));
    }

    public FingeringHandProfile GetHand(Staff hand) => hand switch
    {
        Staff.Treble => RightHand,
        Staff.Bass => LeftHand,
        _ => throw new ArgumentOutOfRangeException(nameof(hand)),
    };

    private static FingeringHandProfile CreateHandWithThumbToLittleFingerReach(FingeringReach thumbToLittleFingerReach) =>
        new(
            new FingeringReach(
                Math.Min(DefaultComfortableSpan, thumbToLittleFingerReach.ComfortableWhiteKeySpan),
                Math.Min(DefaultMaximumSpan, thumbToLittleFingerReach.MaximumWhiteKeySpan)),
            [new FingeringFingerPairLimit(1, 5, thumbToLittleFingerReach)]);
}
