using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Caller-owned memoization for <see cref="GrandStaffSceneBuilder.BuildScore"/>. A hot loop that
/// re-renders every tick purely because the playback cursor moved (e.g. practice mode) can reuse
/// this cache across calls instead of rebuilding barlines, ledger lines, and note glyphs from
/// scratch every 16ms. Verdict and expected-note changes invalidate the cached notation.
/// </summary>
/// <remarks>
/// This is deliberately an explicit object the caller creates and owns (one per grand-staff view
/// in <c>Piano.razor</c>), not a static field on <see cref="GrandStaffSceneBuilder"/>. A static
/// cache would turn a currently pure, stateless builder into hidden global state shared by every
/// caller and every test in the process — including unrelated <c>GrandStaffSceneBuilderTests</c>
/// cases that call <c>BuildScore</c>/<c>Build</c> directly and expect a fresh computation every
/// time. Keeping the cache instance-scoped preserves that test isolation and keeps
/// <see cref="GrandStaffSceneBuilder"/> itself unchanged in behavior.
/// </remarks>
internal sealed class GrandStaffSceneCache
{
    private Score? cachedScore;
    private int cachedFirstVisibleMeasure;
    private IReadOnlyDictionary<ScoreNote, Verdict>? cachedVerdicts;
    private IReadOnlySet<ScoreNote>? cachedExpectedNotes;
    private bool cachedShowNoteLabels;
    private bool cachedShowFingerings;
    private GrandStaffStaticScoreParts? cachedStaticParts;

    internal GrandStaffScene BuildScore(
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null,
        IReadOnlyList<PerformedNote>? performedNotes = null,
        double? performedNoteBeats = null,
        bool showNoteLabels = true,
        bool showFingerings = true,
        IReadOnlySet<ScoreNote>? expectedNotes = null)
    {
        ArgumentNullException.ThrowIfNull(score);

        if (cachedStaticParts is not { } staticParts ||
            !ReferenceEquals(cachedScore, score) ||
            cachedFirstVisibleMeasure != firstVisibleMeasure ||
            !VerdictsEqual(cachedVerdicts, verdicts) ||
            !ScoreNotesEqual(cachedExpectedNotes, expectedNotes) ||
            cachedShowNoteLabels != showNoteLabels ||
            cachedShowFingerings != showFingerings)
        {
            staticParts = GrandStaffSceneBuilder.BuildStaticScoreParts(
                score,
                firstVisibleMeasure,
                verdicts,
                showNoteLabels,
                showFingerings,
                expectedNotes);
            cachedStaticParts = staticParts;
            cachedScore = score;
            cachedFirstVisibleMeasure = firstVisibleMeasure;
            cachedVerdicts = verdicts;
            cachedExpectedNotes = expectedNotes;
            cachedShowNoteLabels = showNoteLabels;
            cachedShowFingerings = showFingerings;
        }

        return GrandStaffSceneBuilder.ComposeScore(
            staticParts,
            score,
            firstVisibleMeasure,
            cursorBeats,
            performedNotes,
            performedNoteBeats,
            showNoteLabels);
    }

    private static bool VerdictsEqual(
        IReadOnlyDictionary<ScoreNote, Verdict>? left,
        IReadOnlyDictionary<ScoreNote, Verdict>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var (note, verdict) in left)
        {
            if (!right.TryGetValue(note, out var otherVerdict) || otherVerdict != verdict)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ScoreNotesEqual(
        IReadOnlySet<ScoreNote>? left,
        IReadOnlySet<ScoreNote>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null && right is not null && left.SetEquals(right);
    }
}
