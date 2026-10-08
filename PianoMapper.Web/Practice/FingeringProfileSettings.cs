using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The thumb-to-little-finger reach the learner chose for each hand, in white-key centre spacings. The browser
/// storage format lives in <see cref="FingeringProfileSettingsJson"/>.
/// </summary>
internal sealed record FingeringProfileSettings(
    double RightComfortable,
    double RightMaximum,
    double LeftComfortable,
    double LeftMaximum)
{
    internal const double MinimumReach = 0;
    internal const double MaximumReach = 12;

    internal static FingeringProfileSettings Default { get; } = new(
        ScoreFingeringProfile.DefaultThumbToLittleFingerReach.ComfortableWhiteKeySpan,
        ScoreFingeringProfile.DefaultThumbToLittleFingerReach.MaximumWhiteKeySpan,
        ScoreFingeringProfile.DefaultThumbToLittleFingerReach.ComfortableWhiteKeySpan,
        ScoreFingeringProfile.DefaultThumbToLittleFingerReach.MaximumWhiteKeySpan);

    /// <summary>Whether both hands have a supported reach whose comfortable span does not exceed its maximum.</summary>
    internal bool IsValid => IsValidHand(RightComfortable, RightMaximum) && IsValidHand(LeftComfortable, LeftMaximum);

    internal static bool TryParseReach(string? text, out double reach) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out reach) && IsSupportedReach(reach);

    /// <summary>
    /// Applies the text typed into one reach box. A rejected edit returns the current settings unchanged with the reason,
    /// so the caller keeps showing the last accepted values.
    /// </summary>
    internal bool TryEditReach(
        Staff hand,
        bool isMaximum,
        string? text,
        out FingeringProfileSettings edited,
        [NotNullWhen(false)] out string? rejection)
    {
        edited = this;
        if (!TryParseReach(text, out double reach))
        {
            rejection = string.Create(
                CultureInfo.InvariantCulture,
                $"Enter a fingering reach between {MinimumReach} and {MaximumReach} white-key spacings.");
            return false;
        }

        FingeringProfileSettings candidate = WithReach(hand, isMaximum, reach);
        if (!candidate.IsValid)
        {
            rejection = "A comfortable reach cannot be wider than its maximum reach.";
            return false;
        }

        edited = candidate;
        rejection = null;
        return true;
    }

    internal FingeringProfileSettings WithReach(Staff hand, bool isMaximum, double reach) => (hand, isMaximum) switch
    {
        (Staff.Treble, false) => this with { RightComfortable = reach },
        (Staff.Treble, true) => this with { RightMaximum = reach },
        (Staff.Bass, false) => this with { LeftComfortable = reach },
        (Staff.Bass, true) => this with { LeftMaximum = reach },
        _ => throw new ArgumentOutOfRangeException(nameof(hand)),
    };

    internal ScoreFingeringProfile ToProfile() => ScoreFingeringProfile.CreateWithThumbToLittleFingerReach(
        new FingeringReach(RightComfortable, RightMaximum),
        new FingeringReach(LeftComfortable, LeftMaximum));

    internal static bool IsValidHand(double comfortable, double maximum) =>
        IsSupportedReach(comfortable) && IsSupportedReach(maximum) && comfortable <= maximum;

    private static bool IsSupportedReach(double reach) =>
        double.IsFinite(reach) && reach is >= MinimumReach and <= MaximumReach;
}
