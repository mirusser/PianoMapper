# Implementation Plan: OpenTK-Backed Synced Visualization

## Overview

PianoMapper is currently a console app: a key press generates a PCM buffer (`PCM.GeneratePianoWave`), plays it via OpenAL on a dedicated thread (`AudioDispatcher`), and draws one static ASCII plot of the first 10ms (`PCM.VisualizeWave`) — a snapshot at trigger time, not a live view. The goal is a real window (OpenTK, already a transitive dependency via the `OpenTK` package) showing a piano-roll and an oscilloscope/spectrum that stay in sync with what's actually playing, as a learning tool for music theory and electronic-music sound design.

## Architecture Decisions

- **Render surface: OpenTK window**, not the terminal. `OpenTK.Windowing.Desktop`/`.Graphics`/`.Mathematics` are already present in `bin/` as transitive deps of the `OpenTK` package — no new NuGet packages needed for windowing.
- **Audio thread stays as-is.** `AudioDispatcher`'s dedicated OpenAL-owning thread is untouched; the GL window's context and the OpenAL context run independently.
- **Input moves from `Console.ReadKey` to OpenTK's `KeyboardState`**, since a blocking console read loop can't coexist with a windowed render loop. Same QWERTY note layout is preserved.
- **FFT is hand-rolled** (radix-2 Cooley-Tukey), not a new dependency (e.g. MathNet.Numerics) — consistent with the existing hand-rolled PCM/DSP style in `PCM.cs`.
- **Scope boundary:** this plan covers piano-roll + oscilloscope + spectrum (the "both together" visualization slice). Music-theory labeling (scale/chord/interval detection on top of the piano-roll) is intentionally deferred — see Open Questions.

## Task List

### Phase 1: Foundation — Window & Input Loop

#### Task 1.1: Add OpenTK GameWindow scaffold
**Description:** Create a `GameWindow` subclass that opens a window and runs an update/render loop at a fixed rate, replacing `Program.cs`'s `while(true)` console-polling loop. Console logging (`Console.WriteLine`) can stay for now — window and console coexist fine.

**Acceptance criteria:**
- [ ] Running the app opens a window (e.g. titled "PianoMapper") that stays open until closed or Escape is pressed.
- [ ] The existing note pipeline (`AudioDispatcher` → OpenAL playback) still fires from key input, now read via OpenTK instead of `Console.ReadKey`.
- [ ] Octave switching still works via the new input path.

**Verification:**
- [ ] `dotnet build` succeeds.
- [ ] Manual check: press piano keys with the window focused, hear notes play as before.

**Dependencies:** None

**Files likely touched:** `PianoMapper/Program.cs`, new `PianoMapper/PianoMapperWindow.cs`

**Estimated scope:** M

#### Task 1.2: Migrate key-to-note handling into the window loop
**Description:** Port the `ConsoleKey` → note lookup and octave-switch handling into OpenTK's `Keys` enum via `KeyboardState`, with one-shot "just pressed" detection so holding a key doesn't repeat-fire (matching old `Console.ReadKey` per-press behavior).

**Acceptance criteria:**
- [ ] Each physical key press triggers exactly one note; holding a key does not stream repeated notes.
- [ ] Same QWERTY layout (A,W,S,E,D,F,R,J,U,K,I,L) maps to the same notes as before.
- [ ] Spacebar clears active notes; Q exits the window and disposes `AudioDispatcher` cleanly (no hang on `thread.Join()`).

**Verification:**
- [ ] Manual check: hold a key — only one note fires.
- [ ] Manual check: Q closes the window and the process exits without hanging.

**Dependencies:** Task 1.1

**Files likely touched:** `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Consts.cs` (if the key type changes from `ConsoleKey`)

**Estimated scope:** S

### Checkpoint: After Phase 1
- [ ] App runs as a window; audio playback behavior unchanged from the user's perspective.
- [ ] Octave switching, clear, and exit all still work.
- [ ] Review with human before proceeding to rendering work.

### Phase 2: Core Features — Note Timeline & Piano-Roll

#### Task 2.1: Extend note tracking with timing/pitch metadata
**Description:** Extend `NoteInstance` (or introduce `NoteEvent`) to carry `Frequency`, `NoteName`, `StartTime` (from a shared `TimeProvider`-based clock started at launch), `Duration`, and the generated `short[]` sample buffer (needed by Phase 3's oscilloscope). Add a thread-safe snapshot method so the render loop can read active/recent notes without holding the audio lock during rendering.

**Acceptance criteria:**
- [x] Note tracking carries note name, frequency, start time, duration, and buffer reference.
- [x] A snapshot method returns a copy of current + recently-finished notes without blocking the audio thread.
- [x] Existing playback/cleanup (removal after duration) still functions.

**Verification:**
- [x] `dotnet build` succeeds.
- [ ] Manual check: press a note, confirm the snapshot contains it with correct start time and frequency. *(Timing/metadata behavior covered by automated `NoteTimelineTests` with a `FakeTimeProvider`; the literal in-app keypress check needs a machine with a real display/audio device — not available in this sandbox.)*

**Dependencies:** Phase 1 checkpoint

**Files likely touched:** `PianoMapper/NoteInstance.cs`, `PianoMapper/NoteTimeline.cs`, `PianoMapper/PianoMapperWindow.cs`

**Estimated scope:** S

#### Task 2.2: Render a scrolling piano-roll from the note timeline
**Description:** In the render loop, draw a scrolling grid — x axis = time (rolling window, e.g. last 8s), y axis = pitch/note name — with a bar per note event spanning `StartTime` to `StartTime + Duration`.

**Acceptance criteria:**
- [ ] Pressing a key draws a bar at the correct pitch row, growing over time to match its actual duration.
- [ ] Concurrently-held notes render as separate, correctly-positioned bars (visual chord).
- [ ] Old notes scroll off-screen after the rolling window passes.

*(Implemented: `PianoRollLayout` — pure time/pitch → NDC math, unit-tested (`PianoRollLayoutTests`) for right-edge growth while playing, fixed width after note-end, pitch ordering, and window-based culling — feeds `PianoRollRenderer`, a GL shader/VBO renderer wired into `PianoMapperWindow.OnRenderFrame`. None of the three boxes above are checked because they describe on-screen appearance, which needs an actual GL context; this sandbox has no display (`GLFWException: Failed to detect any supported platform`). Check these off after a manual run.)*

**Verification:**
- [ ] Manual check: play notes/chords and confirm the piano-roll visually matches what's heard, with no visible drift after ~30s of play. *(Not run — no display available in this environment.)*

**Dependencies:** Task 2.1

**Files likely touched:** new `PianoMapper/Rendering/PianoRollLayout.cs`, new `PianoMapper/Rendering/BarRect.cs`, new `PianoMapper/Rendering/PianoRollRenderer.cs`, `PianoMapper/PianoMapperWindow.cs`

**Estimated scope:** M

### Checkpoint: After Phase 2
- [ ] Piano-roll works end-to-end: notes appear/scroll/disappear in sync with playback.
- [ ] Review with human before proceeding to oscilloscope work.

### Phase 3: Core Features — Live Oscilloscope

#### Task 3.1: Query real playback position from OpenAL
**Description:** Each frame, for the relevant active source(s), call `AL.GetSource(sourceId, ALGetSourcei.SampleOffset)` to get the true current sample position within its buffer.

**Acceptance criteria:**
- [x] A method returns the live sample offset for a given active note's source.
- [ ] Offset advances correctly during playback and behaves sanely once a note ends.

*(Implemented: `AudioDispatcher.RequestSampleOffsetRefresh`/`TryGetSampleOffset` — the render loop enqueues an `AL.GetSource(sourceId, ALGetSourcei.SampleOffset)` query onto the audio thread (the only thread allowed to touch the AL context) each frame, and reads the cached result via a `ConcurrentDictionary` without blocking; `ForgetSampleOffset` clears the entry when a source is deleted. `PlaybackPosition.EstimateSampleOffset` (pure, unit-tested with `PlaybackPositionTests`) is the elapsed-time fallback used before the first live query lands or once a note has ended, matching the plan's risk mitigation. The second box needs a live audio device streaming real playback to confirm the offset tracks linearly — not available in this sandbox.)*

**Verification:**
- [ ] Manual check: log the offset for a held note over its lifetime; confirm it increases roughly linearly and matches the note's known duration/sample rate. *(Not run — no audio device available in this environment.)*

**Dependencies:** Phase 2 checkpoint

**Files likely touched:** `PianoMapper/AudioDispatcher.cs` or new `PianoMapper/PlaybackPosition.cs`

**Estimated scope:** S

#### Task 3.2: Render a scrolling oscilloscope around the live position
**Description:** Using the buffer retained in Task 2.1 and the live offset from Task 3.1, extract a small window of samples (e.g. ±10ms) and render it as a line strip each frame. `PCM.VisualizeWave` can remain for ad-hoc debugging but is no longer called from the main loop.

**Acceptance criteria:**
- [ ] While a note plays, the oscilloscope line updates each frame and reflects the note's actual current position (not a static snapshot).
- [ ] Higher-pitched notes visibly show a tighter/faster waveform than lower-pitched ones.

*(Implemented: `PlaybackPosition.ExtractWindow` — pure sample-window extraction (zero-padded at buffer edges), unit-tested — feeds `OscilloscopeLayout` (pure sample-window → NDC polyline math, confined to a bottom-left inset panel so it doesn't overlap the piano-roll; unit-tested for panel-edge placement and amplitude-to-height mapping in `OscilloscopeLayoutTests`), which in turn feeds `OscilloscopeRenderer`, a GL line-strip renderer wired into `PianoMapperWindow.OnRenderFrame` against the "primary" (most-recently-triggered) note per the Open Questions recommendation. Neither box is checked because they describe on-screen appearance over live audio, which needs an actual GL context and audio device; this sandbox has neither. Check these off after a manual run.)*

**Verification:**
- [ ] Manual check: play a low note then a high note; confirm the waveform's visual wavelength changes accordingly and tracks in real time. *(Not run — no display/audio device available in this environment.)*

**Dependencies:** Task 3.1

**Files likely touched:** new `PianoMapper/Rendering/OscilloscopeRenderer.cs`, new `PianoMapper/Rendering/OscilloscopeLayout.cs`, `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Consts.cs`

**Estimated scope:** M

### Checkpoint: After Phase 3 — Vertical Slice 1 Complete
- [ ] Piano-roll + oscilloscope both run live and in sync with actual audio playback. *(Code complete and unit-tested where testable without a display/audio device; needs a manual run to confirm visually.)*
- [ ] Pause here for review before spectrum work.

### Phase 4: Spectrum (Harmonic Content) View

#### Task 4.1: Hand-rolled radix-2 FFT
**Description:** Add a small, self-contained FFT (power-of-two window → magnitude bins), consistent with the existing hand-rolled DSP style in `PCM.cs`. No new NuGet dependency.

**Acceptance criteria:**
- [x] FFT of a known pure sine wave (e.g. 440Hz via `PCM.GenerateSineWave`) shows a clear single peak at the correct bin/frequency.
- [x] Handles the sample window size(s) used by the oscilloscope.

*(Implemented: `PianoMapper/Audio/Fft.cs` — iterative radix-2 Cooley-Tukey over a Hann-windowed input, `ComputeMagnitudes` (throws on non-power-of-two/empty input) and `BinToFrequency`. The oscilloscope and FFT share `Consts.ScopeWindowSize` (1024, a power of two) as the sample window size, so the same window feeds both. Fully unit-tested in `FftTests` — no GL/AL context needed.)*

**Verification:**
- [x] Test: generate a 440Hz sine buffer, run FFT, assert peak bin corresponds to ~440Hz within tolerance. `FftTests.ComputeMagnitudes_PureSineWave_PeaksAtBinMatchingFrequency` passes (peak within 1.5 bin-widths of 440Hz at a 2048-sample window).

**Dependencies:** Phase 3 checkpoint

**Files likely touched:** new `PianoMapper/Audio/Fft.cs`, new test file

**Estimated scope:** S-M

#### Task 4.2: Render spectrum bars alongside the oscilloscope
**Description:** Feed the same live sample window through the FFT each frame (or at a lower refresh rate for performance) and draw magnitude bars.

**Acceptance criteria:**
- [ ] While a note plays, spectrum bars show a peak at the fundamental and smaller peaks at harmonics, consistent with `PCM.GeneratePianoWave`'s harmonic content.
- [ ] No visible audio glitches/stutter from FFT cost (kept off the audio thread).

*(Implemented: `SpectrumLayout` — pure magnitude-bins → bar-rects math, confined to a bottom-right inset panel distinct from the oscilloscope's; unit-tested in `SpectrumLayoutTests` for empty/all-zero input, panel-edge placement, and proportional bar height — feeds `SpectrumRenderer`, a GL bar renderer wired into `PianoMapperWindow.OnRenderFrame`, which runs `Fft.ComputeMagnitudes` on the same window the oscilloscope uses. Extracted a shared `ShaderProgram.CreateSolidColorProgram()` helper (used by all three renderers) while adding this, replacing the shader-compile code `PianoRollRenderer` previously duplicated inline. The FFT/render work runs entirely on the render thread and never touches `AudioDispatcher`'s queue, so the "kept off the audio thread" half of the second box is true by construction; the "no visible stutter" observation and the first box's on-screen harmonic-structure check both need a manual run to confirm, which this no-display sandbox can't do.)*

**Verification:**
- [ ] Manual check: play a note, confirm a visible fundamental peak and harmonic structure roughly matching what `PCM.GeneratePianoWave` synthesizes. *(Not run — no display available in this environment.)*

**Dependencies:** Task 4.1

**Files likely touched:** new `PianoMapper/Rendering/SpectrumRenderer.cs`, `PianoMapper/PianoMapperWindow.cs`

**Estimated scope:** M

### Checkpoint: Complete
- [ ] Piano-roll, oscilloscope, and spectrum are all live, synced, and readable together in one window. *(Code complete, wired together, and covered by unit tests for every pure-math piece; the live/visual acceptance criteria across Phases 3-4 all need a manual run on a machine with a display and audio device, which this sandbox lacks.)*
- [ ] All acceptance criteria met; ready for review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| OpenAL `SampleOffset` precision may be coarse or platform-dependent on Linux | Med | Fall back to estimating position from a per-note `Stopwatch` if the OpenAL offset proves unreliable |
| Windowed GL app may hit context-creation issues in this Linux dev environment | Med | Validate `GameWindow` opens cleanly in Phase 1 before investing in rendering logic |
| Per-frame rendering/FFT work competes with the audio thread for CPU | Low-Med | Keep FFT/render off the audio thread (already dedicated); throttle spectrum recompute rate if needed |
| Scope creep beyond the agreed vertical slice | Low | Theory/chord layer explicitly deferred (see Open Questions) |

## Open Questions

- Should the oscilloscope/spectrum track a single "primary" note (e.g. most recently pressed) or overlay all concurrently active notes? Recommend starting with "most recent" for simplicity in Task 3.2/4.2, revisit once working.
- Music-theory layer (scale/chord/interval labeling on the piano-roll) is deferred to a future plan once this vertical slice is validated.
- `Alpha/Scratchboard.cs` has several unused alternate wave-generation experiments (ADSR, inharmonicity) sitting adjacent to what this plan builds on — not required here; flagging for a future decision on whether to fold any of it into `PCM.GeneratePianoWave`.
