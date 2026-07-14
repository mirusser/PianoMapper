using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Audio;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Rendering;

namespace PianoMapper;

internal sealed class PianoMapperWindow : GameWindow
{
    private readonly NoteTimeline noteTimeline;
    private readonly Instrument instrument;
    private readonly Score? loadedScore;
    private readonly OpenNoteTracker openNotes = new();
    private IReadOnlyList<ScheduledScoreEvent> scoreSchedule = Array.Empty<ScheduledScoreEvent>();
    private PracticeSession? practiceSession;
    private GradingResult? practiceResult;

    private static readonly float[] LabelColor = [1f, 1f, 1f];
    private const float OctaveLabelGlyphWidth = 0.018f;
    private const float OctaveLabelGlyphHeight = 0.05f;
    private const float PanelLabelGlyphWidth = 0.016f;
    private const float PanelLabelGlyphHeight = 0.045f;

    // Sits just below the octave label (0.90 + OctaveLabelGlyphHeight) with a small gap.
    private const float TempoLabelY = 0.83f;

    private int octave = 1;
    private int firstVisibleMeasure;
    private int nextScoreEventIndex;
    private TimeSpan? scorePlaybackAnchor;
    private bool scorePlaybackActive;
    private int countInTicksPlayed;

    private static readonly Pitch CountInPitch = new(NoteLetter.C, 0, 6);
    private static readonly TimeSpan CountInClickDuration = TimeSpan.FromMilliseconds(50);

    // Reflects the time signature/tempo the M-key random-measure feature will use (or last
    // used); PlayRandomMeasureAsync updates these once it rolls its random values.
    private int measureNumerator = 4;
    private int measureBeatNoteValue = 4;
    private int measureBpm = 60;

    private IReadOnlyDictionary<Keys, Pitch> keyToPitchMap = Consts.GenerateKeyToPitchMapping(1);
    private bool showGrandStaff = true;
    private PianoRollRenderer? pianoRollRenderer;
    private StaffRenderer? staffRenderer;
    private OscilloscopeRenderer? oscilloscopeRenderer;
    private SpectrumRenderer? spectrumRenderer;
    private TextRenderer? textRenderer;

    public PianoMapperWindow(
        GameWindowSettings gameWindowSettings,
        NativeWindowSettings nativeWindowSettings,
        Score? loadedScore = null)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        noteTimeline = new NoteTimeline();
        instrument = new Instrument(noteTimeline);
        this.loadedScore = loadedScore;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0f, 0f, 0f, 1f);
        textRenderer = new TextRenderer();
        pianoRollRenderer = new PianoRollRenderer();
        staffRenderer = new StaffRenderer();
        oscilloscopeRenderer = new OscilloscopeRenderer();
        spectrumRenderer = new SpectrumRenderer();

        Console.WriteLine("Press piano keys (A, W, S, E, D, F, R, J, U, K, I, L, ;) to play notes concurrently.");
        Console.WriteLine("Press Spacebar to clear all active notes.");
        Console.WriteLine("Press Tab to toggle the grand staff and piano-roll views.");
        if (loadedScore is not null)
        {
            Console.WriteLine($"Loaded score: {loadedScore.Title}. Press PgUp/PgDn to scroll measures.");
        }
        Console.WriteLine("Press Q to exit.");
    }

    // Without this, the GL viewport stays at its initial size while every renderer's NDC
    // math assumes it always spans the current framebuffer -- e.g. a window manager that
    // resizes the window post-creation (as headless/tiling compositors do) would leave
    // part of NDC space rendered outside the visible framebuffer. OnFramebufferResize
    // (not OnResize) is used because it reports actual pixel dimensions; OnResize reports
    // logical/client size, which differs under HiDPI/fractional-scale compositors.
    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        var input = KeyboardState;
        UpdateScorePlayback();
        UpdatePracticeSession();

        var pressedControlKeys = PianoInputRouter.ControlKeys.Where(input.IsKeyPressed).ToArray();
        var command = PianoInputRouter.Resolve(pressedControlKeys, loadedScore is not null, practiceSession?.State, octave);
        switch (command.Action)
        {
            case PianoInputAction.Exit:
                Console.WriteLine("Exiting...");
                Close();
                return;
            case PianoInputAction.AbortPractice:
                AbortPracticeSession();
                return;
            case PianoInputAction.Clear:
                Console.WriteLine("Clearing active notes...");
                StopScorePlayback();
                openNotes.Clear();
                instrument.ClearAll();
                return;
        }

        foreach (var key in openNotes.ActiveKeys)
        {
            if (input.IsKeyReleased(key) && openNotes.TryRelease(key, out var note))
            {
                instrument.NoteOff(note);
            }
        }

        switch (command.Action)
        {
            case PianoInputAction.StartPractice:
                StartPracticeSession();
                return;
            case PianoInputAction.ToggleView:
                showGrandStaff = !showGrandStaff;
                return;
            case PianoInputAction.ScrollPrevious:
                firstVisibleMeasure = Math.Max(0, firstVisibleMeasure - GrandStaffLayout.VisibleMeasureCount);
                return;
            case PianoInputAction.ScrollNext:
                int lastMeasure = Math.Max(0, loadedScore!.Measures.Count - 1);
                firstVisibleMeasure = Math.Min(lastMeasure, firstVisibleMeasure + GrandStaffLayout.VisibleMeasureCount);
                return;
            case PianoInputAction.StartScorePlayback:
                StartScorePlayback();
                return;
            case PianoInputAction.ChangeOctave:
                octave = command.Octave!.Value;
                Console.WriteLine($"Changing octave to: {octave}");
                keyToPitchMap = Consts.GenerateKeyToPitchMapping(octave);
                return;
            case PianoInputAction.PlayRandomMeasure:
                Pitch[] palette = [new Pitch(NoteLetter.A, 0, 4)];
                _ = PlayRandomMeasureAsync(palette, 4, 4, 60, 60);
                return;
        }

        foreach (var (key, note) in keyToPitchMap)
        {
            if (!input.IsKeyPressed(key))
            {
                continue;
            }

            var performedNote = instrument.NoteOn(note).Note;
            openNotes.Track(key, performedNote);
            Console.WriteLine($" Note on: {note} - Frequency: {note.Frequency:F2}Hz{Environment.NewLine}");
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit);

        var now = noteTimeline.Now;
        if (textRenderer is not null)
        {
            var notes = noteTimeline.Snapshot();
            if (showGrandStaff)
            {
                if (loadedScore is not null)
                {
                    double? cursorBeat = practiceSession?.State is PracticeSessionState.Running or PracticeSessionState.Finished
                        ? practiceSession.CursorBeats
                        : scorePlaybackActive && scorePlaybackAnchor.HasValue
                            ? ScorePlayback.GetCursorBeats(now, scorePlaybackAnchor.Value, loadedScore.Tempo)
                            : null;
                    var verdicts = BuildVisiblePracticeVerdicts();
                    IReadOnlyList<PerformedNote> practiceNotes = practiceSession?.State is not null and not PracticeSessionState.Idle
                        ? practiceSession.GetSessionNotes(notes)
                        : Array.Empty<PerformedNote>();
                    staffRenderer?.RenderScore(loadedScore, firstVisibleMeasure, practiceNotes, now, textRenderer, cursorBeat, verdicts);
                }
                else
                {
                    staffRenderer?.Render(notes, now, textRenderer);
                }
            }
            else
            {
                pianoRollRenderer?.Render(notes, now, textRenderer);
            }

            textRenderer.Render($"OCTAVE {octave}", -0.95f, 0.90f, OctaveLabelGlyphWidth, OctaveLabelGlyphHeight, LabelColor);
            textRenderer.Render($"TEMPO {measureBpm} BPM {measureNumerator}/{measureBeatNoteValue}", -0.95f, TempoLabelY, OctaveLabelGlyphWidth, OctaveLabelGlyphHeight, LabelColor);
            textRenderer.Render("OSCILLOSCOPE", -0.95f, -0.36f, PanelLabelGlyphWidth, PanelLabelGlyphHeight, LabelColor);
            textRenderer.Render("SPECTRUM", -0.95f, -0.68f, PanelLabelGlyphWidth, PanelLabelGlyphHeight, LabelColor);

            if (practiceSession?.State == PracticeSessionState.Finished && practiceResult is not null)
            {
                RenderPracticeSummary(textRenderer, practiceResult.Summary);
            }
        }

        if (instrument.TryGetPrimarySampleWindow(now, Consts.ScopeWindowSize, out var window))
        {
            oscilloscopeRenderer?.Render(window);

            var magnitudes = Fft.ComputeMagnitudes(window);
            spectrumRenderer?.Render(magnitudes);
        }

        SwapBuffers();
    }

    protected override void OnUnload()
    {
        spectrumRenderer?.Dispose();
        oscilloscopeRenderer?.Dispose();
        staffRenderer?.Dispose();
        pianoRollRenderer?.Dispose();
        textRenderer?.Dispose();
        instrument.Dispose();
        base.OnUnload();
    }

    private async Task PlayRandomMeasureAsync(
        Pitch[] palette,
        int minNumerator = 2,
        int maxNumerator = 7,
        int minBpm = 60,
        int maxBpm = 180)
    {
        var measure = RandomMeasureComposer.Compose(
            palette,
            minNumerator,
            maxNumerator,
            minBpm,
            maxBpm,
            Random.Shared);
        measureNumerator = measure.TimeSignature.Numerator;
        measureBeatNoteValue = measure.TimeSignature.BeatNoteValue.Denominator;
        measureBpm = (int)measure.Tempo.BeatsPerMinute;
        Console.WriteLine($"Playing a {measureNumerator}/{measureBeatNoteValue} bar at {measureBpm} BPM...");
        var measureTasks = new List<Task>(measure.Events.Count);

        foreach (var note in measure.Events)
        {
            TimeSpan duration = GetRandomMeasureEventDuration(note, measure.TimeSignature, measure.Tempo);
            Console.WriteLine(
                $" Note: {note.Pitch} " +
                $"- Freq: {note.Pitch.Frequency} Hz " +
                $"- Value: 1/{note.NoteValue.Denominator} ({note.Beats:F2} beats) " +
                $"- Dur: {duration.TotalSeconds:F2}s");
            measureTasks.Add(instrument.Play(note.Pitch, duration).Completion);
        }

        await Task.WhenAll(measureTasks).ConfigureAwait(false);
        Console.WriteLine("— Measure complete —\n");
    }

    internal static TimeSpan GetRandomMeasureEventDuration(
        RandomMeasureEvent note,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        ArgumentNullException.ThrowIfNull(note);
        TimeSpan rhythmicDuration = MusicalTime.ToDuration(note.NoteValue, timeSignature, tempo);
        TimeSpan naturalDuration = Instrument.GetNaturalDuration(note.Pitch);
        return rhythmicDuration <= naturalDuration ? rhythmicDuration : naturalDuration;
    }

    private void StartScorePlayback()
    {
        if (loadedScore is null)
        {
            return;
        }

        instrument.ClearAll();
        openNotes.Clear();
        scorePlaybackAnchor = noteTimeline.Now;
        scoreSchedule = ScorePlayback.CreateSchedule(loadedScore, scorePlaybackAnchor.Value);
        nextScoreEventIndex = 0;
        scorePlaybackActive = scoreSchedule.Count > 0;
        UpdateScorePlayback();
    }

    private void UpdateScorePlayback()
    {
        if (!scorePlaybackActive)
        {
            return;
        }

        var now = noteTimeline.Now;
        var dueEvents = ScorePlayback.GetDueEvents(scoreSchedule, now, nextScoreEventIndex);
        foreach (var scheduledEvent in dueEvents)
        {
            instrument.Play(scheduledEvent.Event.Pitch, scheduledEvent.Duration);
        }

        nextScoreEventIndex += dueEvents.Count;
        if (nextScoreEventIndex >= scoreSchedule.Count)
        {
            var playbackEnd = scoreSchedule.Max(item => item.DueTime + item.Duration);
            scorePlaybackActive = now < playbackEnd;
        }
    }

    private void StopScorePlayback()
    {
        scorePlaybackActive = false;
        scorePlaybackAnchor = null;
        scoreSchedule = Array.Empty<ScheduledScoreEvent>();
        nextScoreEventIndex = 0;
    }

    private void StartPracticeSession()
    {
        if (loadedScore is null)
        {
            return;
        }

        StopScorePlayback();
        instrument.ClearAll();
        openNotes.Clear();
        practiceSession = new PracticeSession(loadedScore, TimeProvider.System);
        practiceSession.Start(noteTimeline.Now);
        practiceResult = null;
        countInTicksPlayed = 0;
        UpdatePracticeSession();
    }

    private void UpdatePracticeSession()
    {
        if (practiceSession is null || practiceSession.State == PracticeSessionState.Idle)
        {
            return;
        }

        practiceSession.Update();
        while (countInTicksPlayed < practiceSession.CountInTicksDue)
        {
            instrument.Play(CountInPitch, CountInClickDuration);
            countInTicksPlayed++;
        }

        if (practiceSession.State is PracticeSessionState.Running or PracticeSessionState.Finished)
        {
            practiceResult = practiceSession.Grade(noteTimeline.Snapshot());
        }
    }

    private void AbortPracticeSession()
    {
        practiceSession?.Abort();
        practiceResult = null;
        countInTicksPlayed = 0;
        openNotes.Clear();
        instrument.ClearAll();
    }

    private IReadOnlyDictionary<ScoreNote, Verdict>? BuildVisiblePracticeVerdicts()
    {
        if (practiceSession is null || practiceResult is null)
        {
            return null;
        }

        return practiceSession.BuildVisibleVerdicts(practiceResult);
    }

    private static void RenderPracticeSummary(TextRenderer textRenderer, GradingSummary summary)
    {
        var lines = BuildPracticeSummaryLines(summary);
        textRenderer.Render(lines[0], 0.42f, 0.88f, OctaveLabelGlyphWidth, OctaveLabelGlyphHeight, LabelColor);
        textRenderer.Render(lines[1], -0.20f, 0.81f, PanelLabelGlyphWidth, PanelLabelGlyphHeight, LabelColor);
        textRenderer.Render(lines[2], -0.20f, 0.74f, PanelLabelGlyphWidth, PanelLabelGlyphHeight, LabelColor);
    }

    internal static IReadOnlyList<string> BuildPracticeSummaryLines(GradingSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return
        [
            $"SCORE {summary.AccuracyPercent:F0}",
            $"CORRECT {summary.Counts[Verdict.Correct]} WRONG {summary.Counts[Verdict.WrongPitch]} " +
            $"MISSED {summary.Counts[Verdict.Missed]} EXTRA {summary.Counts[Verdict.Extra]}",
            $"EARLY {summary.Counts[Verdict.Early]} LATE {summary.Counts[Verdict.Late]} " +
            $"SHORT {summary.Counts[Verdict.TooShort]} LONG {summary.Counts[Verdict.TooLong]}",
        ];
    }
}
