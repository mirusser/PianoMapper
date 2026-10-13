using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Caller-owned memo of the live grand staff's <see cref="LiveGrandStaffRenderState"/>. The live staff is refreshed
/// every ~16 ms for as long as audio is on, yet once its cursor line is separated out and the browser grows a held
/// note's tail from the audio clock, the scene depends on the clock only through the visible page and, while a note is
/// held, the measure the clock is in (a held note splits at each barline). Every tick between two key events, two
/// measures or two pages would rebuild and compare exactly the same scenes, at a cost that grows with the notes the
/// timeline still retains. This hands back the previous state for such ticks.
/// </summary>
internal sealed class LiveGrandStaffRenderMemo
{
    // How far past a barline, and past a held note's start, the clock has to be for a scene built then to hold for the
    // rest of the measure. Far above the comparison tolerance the live layout splits notes at.
    private const double SettledBeats = 0.000001;

    private Inputs? rememberedInputs;
    private LiveGrandStaffRenderState rememberedState;

    /// <summary>
    /// Returns the state remembered for these inputs, or calls <paramref name="build"/>, which must build exactly the
    /// state those inputs describe, and remembers its result.
    /// </summary>
    internal LiveGrandStaffRenderState GetOrBuild(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo,
        int? selectedOctave,
        bool showNoteLabels,
        Func<LiveGrandStaffRenderState> build)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(build);

        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, timeSignature, tempo);
        int? heldNoteMeasure = notes.Any(note => note.ReleaseTime is null)
            ? GrandStaffLayout.GetLiveCurrentMeasure(currentTime, timeSignature, tempo)
            : null;
        if (rememberedInputs is { } remembered &&
            remembered.Matches(
                notes,
                firstVisibleMeasure,
                heldNoteMeasure,
                timeSignature,
                tempo,
                selectedOctave,
                showNoteLabels))
        {
            return rememberedState;
        }

        LiveGrandStaffRenderState state = build();
        // A held note is drawn from the clock only once the clock is strictly inside a measure and past the note's
        // start: built on a barline, or before the note's start, the scene lacks the note's head in that measure, and
        // would keep lacking it for the rest of the measure.
        rememberedInputs = heldNoteMeasure is null || IsSettled(notes, currentTime, timeSignature, tempo)
            ? new Inputs(
                notes.Select(note => (note.Pitch, note.StartTime, note.ReleaseTime)).ToArray(),
                firstVisibleMeasure,
                heldNoteMeasure,
                timeSignature,
                tempo,
                selectedOctave,
                showNoteLabels)
            : null;
        rememberedState = state;
        return state;
    }

    private static bool IsSettled(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        double currentBeat = MusicalTime.DurationToBeats(currentTime, tempo);
        if (currentBeat % timeSignature.Numerator <= SettledBeats)
        {
            return false;
        }

        return notes.All(note =>
            note.ReleaseTime is not null ||
            currentBeat - MusicalTime.DurationToBeats(note.StartTime, tempo) > SettledBeats);
    }

    private sealed class Inputs(
        (Pitch Pitch, TimeSpan StartTime, TimeSpan? ReleaseTime)[] notes,
        int firstVisibleMeasure,
        int? heldNoteMeasure,
        TimeSignature timeSignature,
        Tempo tempo,
        int? selectedOctave,
        bool showNoteLabels)
    {
        internal bool Matches(
            IReadOnlyList<PerformedNote> otherNotes,
            int otherFirstVisibleMeasure,
            int? otherHeldNoteMeasure,
            TimeSignature otherTimeSignature,
            Tempo otherTempo,
            int? otherSelectedOctave,
            bool otherShowNoteLabels)
        {
            if (firstVisibleMeasure != otherFirstVisibleMeasure ||
                heldNoteMeasure != otherHeldNoteMeasure ||
                timeSignature != otherTimeSignature ||
                tempo != otherTempo ||
                selectedOctave != otherSelectedOctave ||
                showNoteLabels != otherShowNoteLabels ||
                notes.Length != otherNotes.Count)
            {
                return false;
            }

            for (int index = 0; index < notes.Length; index++)
            {
                PerformedNote note = otherNotes[index];
                if (notes[index] != (note.Pitch, note.StartTime, note.ReleaseTime))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
