using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Turns a finished exercise's prompt results into one <see cref="ReviewMark"/> per score note, for drawing on the
/// staff. Pure: both pacings feed it the same <see cref="NoteReadingPromptResult"/> list, so a mark means the same
/// thing whichever engine graded the run. Chord members and tied source notes share their prompt's mark.
/// </summary>
internal static class ExerciseReviewMarks
{
    /// <summary>
    /// Precedence is Pitch, then Missed, then Timing, then Clean: a wrong key is the most useful thing to look at, an
    /// unplayed prompt next, and a late or short note only when the pitch was right.
    /// </summary>
    internal static ReviewMark Classify(NoteReadingPromptResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsFirstTryCorrect)
        {
            return ReviewMark.Clean;
        }

        // A wrong key is what separates "read the wrong note" from "never played it": the play-along mapper also
        // reports an unplayed prompt as a failed pitch, so IsPitchFirstTryCorrect alone cannot tell them apart.
        if (result.WrongPlayedPitches.Length > 0)
        {
            return ReviewMark.Pitch;
        }

        if (result.WasMissed)
        {
            return ReviewMark.Missed;
        }

        // Not clean, no wrong key and not missed: timing when the result says so, otherwise the old fused flag's
        // meaning (a reading mistake), which is what a result built before pitch and timing were split reports.
        return result.HasTimingMistake ? ReviewMark.Timing : ReviewMark.Pitch;
    }

    /// <summary>
    /// The mark of every source note of every finished prompt. A prompt still in progress has no outcome yet and no
    /// mark.
    /// </summary>
    internal static IReadOnlyDictionary<ScoreNote, ReviewMark> Build(
        IReadOnlyList<NoteReadingPromptResult> promptResults)
    {
        ArgumentNullException.ThrowIfNull(promptResults);
        var marks = new Dictionary<ScoreNote, ReviewMark>();
        foreach (NoteReadingPromptResult result in promptResults.Where(result => result.IsComplete))
        {
            ReviewMark mark = Classify(result);
            foreach (ScoreNote note in result.ExpectedSourceNotes)
            {
                marks[note] = mark;
            }
        }

        return marks;
    }
}
