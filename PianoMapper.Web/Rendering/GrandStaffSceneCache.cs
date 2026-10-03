using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Caller-owned memoization for <see cref="GrandStaffSceneBuilder.BuildScore"/>. A hot loop that
/// re-renders every tick purely because the playback cursor moved (e.g. practice mode) can reuse
/// this cache across calls instead of rebuilding barlines, ledger lines, and note glyphs from
/// scratch every 16ms. Verdict, review-mark and expected-note changes invalidate the cached notation.
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
    private int cachedVisibleMeasureCount;
    private bool cachedDrawRests;
    private bool cachedDrawTies;
    private IReadOnlyDictionary<ScoreNote, ReviewMark>? cachedReviewMarks;
    private GrandStaffStaticScoreParts? cachedStaticParts;
    private GrandStaffScene? cachedStaticScene;

    internal GrandStaffScene BuildScore(
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null,
        IReadOnlyList<PerformedNote>? performedNotes = null,
        double? performedNoteBeats = null,
        bool showNoteLabels = true,
        bool showFingerings = true,
        IReadOnlySet<ScoreNote>? expectedNotes = null,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount,
        bool drawRests = false,
        bool drawTies = false,
        IReadOnlyDictionary<ScoreNote, ReviewMark>? reviewMarks = null)
    {
        GrandStaffScoreRenderState renderState = BuildScoreRenderState(
            score,
            firstVisibleMeasure,
            cursorBeats,
            verdicts,
            performedNotes,
            performedNoteBeats,
            showNoteLabels,
            showFingerings,
            expectedNotes,
            visibleMeasureCount,
            drawRests,
            drawTies,
            reviewMarks);
        return GrandStaffSceneBuilder.ComposeScore(renderState.StaticParts, renderState.Overlay);
    }

    internal GrandStaffScoreRenderState BuildScoreRenderState(
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null,
        IReadOnlyList<PerformedNote>? performedNotes = null,
        double? performedNoteBeats = null,
        bool showNoteLabels = true,
        bool showFingerings = true,
        IReadOnlySet<ScoreNote>? expectedNotes = null,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount,
        bool drawRests = false,
        bool drawTies = false,
        IReadOnlyDictionary<ScoreNote, ReviewMark>? reviewMarks = null)
    {
        ArgumentNullException.ThrowIfNull(score);

        if (cachedStaticParts is not { } staticParts ||
            !ReferenceEquals(cachedScore, score) ||
            cachedFirstVisibleMeasure != firstVisibleMeasure ||
            !MapsEqual(cachedVerdicts, verdicts) ||
            !ScoreNotesEqual(cachedExpectedNotes, expectedNotes) ||
            cachedShowNoteLabels != showNoteLabels ||
            cachedShowFingerings != showFingerings ||
            cachedVisibleMeasureCount != visibleMeasureCount ||
            cachedDrawRests != drawRests ||
            cachedDrawTies != drawTies ||
            !MapsEqual(cachedReviewMarks, reviewMarks))
        {
            staticParts = GrandStaffSceneBuilder.BuildStaticScoreParts(
                score,
                firstVisibleMeasure,
                verdicts,
                showNoteLabels,
                showFingerings,
                expectedNotes,
                visibleMeasureCount,
                drawRests,
                drawTies,
                reviewMarks);
            cachedStaticParts = staticParts;
            cachedStaticScene = GrandStaffSceneBuilder.CreateStaticScoreScene(staticParts);
            cachedScore = score;
            cachedFirstVisibleMeasure = firstVisibleMeasure;
            cachedVerdicts = verdicts;
            cachedExpectedNotes = expectedNotes;
            cachedShowNoteLabels = showNoteLabels;
            cachedShowFingerings = showFingerings;
            cachedVisibleMeasureCount = visibleMeasureCount;
            cachedDrawRests = drawRests;
            cachedDrawTies = drawTies;
            cachedReviewMarks = reviewMarks;
        }

        return new GrandStaffScoreRenderState(
            cachedStaticScene!,
            GrandStaffSceneBuilder.BuildScoreOverlay(
                staticParts,
                score,
                firstVisibleMeasure,
                cursorBeats,
                performedNotes,
                performedNoteBeats,
                showNoteLabels,
                visibleMeasureCount,
                drawTies),
            staticParts);
    }

    private static bool MapsEqual<TValue>(
        IReadOnlyDictionary<ScoreNote, TValue>? left,
        IReadOnlyDictionary<ScoreNote, TValue>? right)
        where TValue : struct
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var (note, value) in left)
        {
            if (!right.TryGetValue(note, out var otherValue) || !EqualityComparer<TValue>.Default.Equals(otherValue, value))
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
