# Implementation Plan: Metronome + On-Tempo Indicator (Web)

## Overview

Add an optional metronome (default **off**) to the Blazor web app so the user hears the beat for the
tempo/meter already selectable in the Timing card, and an on-tempo indicator that tells the user —
per played note — whether they hit the beat, were early, or were late, with a user-settable
tolerance so feedback is forgiving rather than millisecond-perfect.

Everything runs on the Web Audio `AudioContext` clock, which the app already uses end-to-end:
`WebAudioSession.GetCurrentTimeAsync()` reads it, scheduled events are placed on it, and keyboard
presses arrive as `BrowserInputCommand.EventTime` already mapped onto it. Comparing a keypress
against the beat grid is therefore exact arithmetic.

## Architecture Decisions

- **A3 — Hybrid grid ownership.** C# owns the beat grid as a pure Core type (`MetronomeGrid`:
  anchor time + tempo + time signature). JavaScript (`audio.js`) owns click emission using the
  standard lookahead pattern (short `setInterval` scheduling clicks ~0.2 s ahead on the audio
  clock), so audio is immune to Blazor/GC pauses. The same grid drives both the clicks and the
  on-tempo math, so what the user hears and what gets graded cannot drift apart.
- **B — Tolerance dead zone with UI presets.** A note is "on time" when
  `|deviation| <= OnTimeTolerance`. Exposed in the UI as presets: **Strict 30 ms / Normal 60 ms /
  Relaxed 100 ms** (default Normal). The same dead zone is added to `Grader.Classify`, fixing an
  existing quirk where only an *exactly* equal onset yields `Verdict.Correct`
  (PianoMapper.Core/Practice/Grader.cs:135-143) — real input is currently always Early/Late.
  One shared setting is used by both the free-play indicator and practice grading.
- **C3 — Phased indicator.** HTML first (beat pulse + last-note deviation chip + rolling stats in
  the Timing area); canvas rendering (beat grid lines, note coloring in piano roll) is a listed
  follow-up, not in scope.
- **Verdict reuse.** Beat alignment classifies into the existing `Verdict` enum
  (`Correct`/`Early`/`Late` subset) instead of a new enum, so the existing verdict color scheme and
  future canvas coloring work unchanged.
- **Web-first, logic in Core.** Grid math and classification live in `PianoMapper.Core` so the
  desktop OpenTK app can adopt them later. Click emission, interop, and UI are web-only.
- **Timing changes restart the grid.** Consistent with existing behavior ("a change stops
  scheduled playback"), changing tempo/meter while the metronome runs stops it and restarts it on
  a fresh anchor.

## Dependency Graph

```
MetronomeGrid + BeatAlignment (Core, pure)          Grader dead zone (Core, independent)
        │
        ├── audio.js: click synth + lookahead metronome (JS)
        │           │
        │           └── WebAudioSession interop + IBrowserMetronomeAudio
        │                       │
        │                       └── BrowserMetronome coordinator (C#)
        │                                   │
        │                                   ├── Piano.razor toggle + timing-change wiring
        │                                   │
        │                                   └── TempoFeedbackTracker (alignment capture)
        │                                               │
        │                                               └── HTML indicator UI + beat pulse
```

## Task List

### Phase 1: Core foundation (pure, TDD-friendly)

## Task 1: `MetronomeGrid` and `BeatAlignment` in Core

**Description:** Add an immutable `MetronomeGrid` record (`PianoMapper.Music`) capturing anchor
`TimeSpan`, `Tempo`, and `TimeSignature`, with beat math built on `MusicalTime`:
`BeatDuration`, beats-elapsed at a given time, nearest-beat index, and signed deviation
(`TimeSpan`, negative = early). Add `BeatAlignment` (`PianoMapper.Practice`): given a note onset,
a grid, and an `OnTimeTolerance`, returns the nearest beat index, signed deviation, and a
`Verdict` (`Correct` when `|deviation| <= tolerance`, else `Early`/`Late`). Also identifies the
downbeat (beat index modulo numerator) for later display use.

**Acceptance criteria:**
- [ ] `MetronomeGrid` computes nearest beat and signed deviation for times before/after/exactly on a beat, including times before the anchor.
- [ ] `BeatAlignment` returns `Correct` at exactly ±tolerance (inclusive boundary), `Early`/`Late` just outside it.
- [ ] Works for non-quarter beat values (e.g. 6/8 at 90 BPM) using existing `MusicalTime` semantics (a beat = `BeatNoteValue`).

**Verification:**
- [ ] Tests pass: `dotnet test --filter "MetronomeGrid|BeatAlignment"`
- [ ] Build succeeds: `dotnet build`

**Dependencies:** None

**Files likely touched:**
- `PianoMapper.Core/Music/MetronomeGrid.cs` (new)
- `PianoMapper.Core/Practice/BeatAlignment.cs` (new)
- `PianoMapper.Tests/UnitTests/MetronomeGridTests.cs` (new)
- `PianoMapper.Tests/UnitTests/BeatAlignmentTests.cs` (new)

**Estimated scope:** S–M

## Task 2: On-time dead zone in `Grader`

**Description:** Add `OnTimeTolerance` to `GradingOptions` (default 60 ms). In `Grader.Classify`,
treat `|performed.StartTime - expectedOnset| <= OnTimeTolerance` as on time (continue to duration
checks); only outside the dead zone classify `Early`/`Late`. Existing tests use exact onsets
(deviation 0), so they keep passing; add boundary tests.

**Acceptance criteria:**
- [ ] A matched note within ±`OnTimeTolerance` of the expected onset can be graded `Correct` (subject to duration checks).
- [ ] Notes outside the dead zone but within `OnsetTolerance` are still matched and graded `Early`/`Late`.
- [ ] All existing `GraderTests` and `PracticeSessionTests` pass unchanged.

**Verification:**
- [ ] Tests pass: `dotnet test --filter "Grader|PracticeSession"`

**Dependencies:** None (independent of Task 1)

**Files likely touched:**
- `PianoMapper.Core/Practice/GradingOptions.cs`
- `PianoMapper.Core/Practice/Grader.cs`
- `PianoMapper.Tests/UnitTests/GraderTests.cs`

**Estimated scope:** S

### Checkpoint: Foundation
- [ ] `dotnet build` clean, full `dotnet test` green.

### Phase 2: Metronome audio (vertical slice: toggle → click heard)

## Task 3: Click synth + lookahead metronome in `audio.js`

**Description:** Add a dedicated metronome click to `audio.js` (short envelope blip, distinct from
piano notes; accented downbeat — higher pitch and gain on beat 1, e.g. ~1760 Hz vs ~1320 Hz) and
`startMetronome(anchorSeconds, secondsPerBeat, beatsPerMeasure)` / `stopMetronome()`. Internally a
~25 ms `setInterval` schedules clicks ~0.2 s ahead on the audio clock from the given anchor —
unbounded, unlike the finite count-in. `stopMetronome()` cancels pending clicks; `dispose()` and
the metronome do not interfere with `clear`/`stopScore` (piano notes and score events unaffected).
Each scheduled click also arms a `setTimeout` at click time that pulses a CSS class on a
`data-metronome-pulse` element if present (consumed by Task 7; harmless no-op until then).

**Acceptance criteria:**
- [ ] With the metronome started, clicks are audible, evenly spaced, and beat 1 of each measure is audibly accented.
- [ ] Clicks stay steady while playing piano keys and while a score plays (no drift or stutter).
- [ ] `stopMetronome()` silences it immediately; starting again re-anchors cleanly.

**Verification:**
- [ ] Manual check via the running app (`/run`): enable audio, start metronome at 4/4 · 120 BPM, listen for accent pattern; change nothing else.

**Dependencies:** None (JS-only; interop lands in Task 4)

**Files likely touched:**
- `PianoMapper.Web/wwwroot/js/audio.js`

**Estimated scope:** S–M

## Task 4: `BrowserMetronome` coordinator + interop

**Description:** Add `IBrowserMetronomeAudio` (mirroring `IBrowserScoreAudio`) with
`StartMetronomeAsync(TimeSpan anchor, TimeSpan beatDuration, int beatsPerMeasure)` and
`StopMetronomeAsync()`, implemented by `WebAudioSession`. Add internal `BrowserMetronome`
coordinator: `StartAsync(TimeSignature, Tempo)` computes `anchor = now + lead` (reuse the 50 ms
scheduling-lead convention), builds a `MetronomeGrid`, passes derived values to JS, and exposes
`Grid` and `IsRunning`; `StopAsync()` clears the grid and stops JS. Unit-test with a fake
`IBrowserMetronomeAudio`, following the `FakePracticeAudio` pattern in
`BrowserPracticeCoordinatorTests`.

**Acceptance criteria:**
- [ ] `StartAsync` anchors the grid at audio-now + lead and forwards matching beat duration / beats-per-measure to the audio interface.
- [ ] `StopAsync` stops audio and clears `Grid`; starting again re-anchors at the new current time.
- [ ] `Grid` is `null`/inactive when not running.

**Verification:**
- [ ] Tests pass: `dotnet test --filter "BrowserMetronome"`

**Dependencies:** Tasks 1, 3

**Files likely touched:**
- `PianoMapper.Web/Playback/IBrowserMetronomeAudio.cs` (new)
- `PianoMapper.Web/Audio/WebAudioSession.cs`
- `PianoMapper.Web/Audio/BrowserMetronome.cs` (new)
- `PianoMapper.Tests/UnitTests/BrowserMetronomeTests.cs` (new)
- `PianoMapper.Tests/UnitTests/WebAudioSessionTests.cs`

**Estimated scope:** M

## Task 5: Metronome toggle in the Timing card

**Description:** Add a metronome on/off control (default off, disabled until audio is initialized)
to the Timing card in `Piano.razor`, wired to `BrowserMetronome`. `ApplyTimingAsync` restarts the
metronome on the new grid when it was running (consistent with "a change stops scheduled
playback"). The Clear command (`BrowserInputCommandKind.Clear`) and `DisposeAsync` stop the
metronome. Status chip reflects state ("Metronome on at {TimingLabel}." / "Metronome off.").

**Acceptance criteria:**
- [ ] Toggle defaults to off; turning it on produces clicks at the displayed tempo/meter, off silences them.
- [ ] Changing tempo, beats-per-measure, or beat note while running restarts the clicks at the new timing.
- [ ] Clear (`C` key or button) and page disposal stop the metronome without errors.

**Verification:**
- [ ] Build succeeds: `dotnet build`
- [ ] Manual check via the running app (`/run`): toggle on/off, change tempo mid-run, press `C`.

**Dependencies:** Task 4

**Files likely touched:**
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/wwwroot/css/app.css`

**Estimated scope:** S–M

### Checkpoint: Metronome slice
- [ ] Full `dotnet test` green; metronome audible, restartable, and cleanly stoppable in the browser.
- [ ] Review with human before Phase 3.

### Phase 3: On-tempo indicator (HTML)

## Task 6: `TempoFeedbackTracker` — alignment capture on note-on

**Description:** Add a small tracker that, while the metronome grid is active, records a
`BeatAlignment` for each free-play note-on (`ApplyInputCommandAsync`, `NoteOn` case, using
`command.EventTime` — already on the audio clock) and exposes: last alignment, rolling window of
the last 10 notes, on-time count/total, and median deviation. Pure logic, unit-tested; lives in
Core (`PianoMapper.Core/Practice/TempoFeedbackTracker.cs`) so the desktop app can reuse it.
`Reset()` on metronome stop/restart, timing change, and Clear.

**Acceptance criteria:**
- [ ] Feeding onsets produces correct last verdict, signed deviation in ms, rolling on-time ratio, and median deviation.
- [ ] Window caps at 10 notes (oldest evicted); `Reset()` empties all stats.
- [ ] No tracking occurs when the grid is inactive.

**Verification:**
- [ ] Tests pass: `dotnet test --filter "TempoFeedbackTracker"`

**Dependencies:** Tasks 1, 4

**Files likely touched:**
- `PianoMapper.Core/Practice/TempoFeedbackTracker.cs` (new)
- `PianoMapper.Tests/UnitTests/TempoFeedbackTrackerTests.cs` (new)
- `PianoMapper.Web/Pages/Piano.razor` (wiring in `ApplyInputCommandAsync`)

**Estimated scope:** M

## Task 7: Indicator UI — deviation chip, rolling stats, tolerance presets, beat pulse

**Description:** Render the feedback in/next to the Timing card, visible only while the metronome
runs: a chip for the last note ("On time" / "+42 ms late" / "−35 ms early") colored by verdict
(reuse the existing verdict color scheme), a rolling stat line ("8/10 on time · median +18 ms"),
and a tolerance preset select (Strict 30 ms / Normal 60 ms / Relaxed 100 ms, default Normal)
feeding both `TempoFeedbackTracker` and the shared `GradingOptions.OnTimeTolerance`. Add the beat
pulse element (`data-metronome-pulse`) whose CSS class is pulsed by `audio.js` at each click
(Task 3), with a stronger style on downbeats — no Blazor render loop needed for the pulse.

**Acceptance criteria:**
- [ ] With metronome on, each keypress updates the chip and stats; deliberately early/late presses show signed ms and the right color; presses on the click show "On time" at Normal tolerance.
- [ ] Switching the preset visibly changes how strict the verdicts are.
- [ ] The pulse dot blinks in sync with the audible clicks, accented on beat 1; indicator and pulse are hidden when the metronome is off.

**Verification:**
- [ ] Build succeeds: `dotnet build`; full `dotnet test` green.
- [ ] Manual check via the running app (`/run`) per the acceptance criteria above.

**Dependencies:** Tasks 3, 5, 6

**Files likely touched:**
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/wwwroot/css/app.css`
- `PianoMapper.Web/wwwroot/js/audio.js` (pulse hookup only, if not fully done in Task 3)

**Estimated scope:** M

### Checkpoint: Complete
- [ ] All acceptance criteria met; full `dotnet test` green; feature exercised end-to-end in the browser.
- [ ] Ready for review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Blazor WASM pauses (GC, interop) starve a C#-driven scheduler | High | Lookahead loop lives in JS (Task 3); C# only sets/clears the grid |
| Visual pulse drifts from audible click | Med | Pulse is armed per scheduled click in JS from the same audio-clock times — no separate animation clock |
| Grader dead zone changes practice grading semantics | Med | Existing tests use exact onsets (deviation 0) and stay green; boundary tests added; default 60 ms matches indicator default |
| Metronome interferes with `clear`/`stopScore`/dispose paths | Med | Metronome nodes tracked separately from `activeNotes`/`scheduledScoreNotes`; explicit stop wiring in Task 5 acceptance criteria |
| Working tree already has uncommitted changes (`ScoreTiming`, Razor/CSS edits) | Low | Plan builds on the current working tree; implementer should not revert or "clean up" unrelated pending changes |

## Out of Scope (agreed follow-ups)

- Canvas rendering: beat grid lines in the piano roll, coloring performed notes by alignment (C2).
- Metronome continuing through the practice **Running** phase (today only the count-in clicks); count-in reusing the new accented click.
- Indicator surfacing grader verdicts during practice-with-score.
- Desktop (OpenTK) metronome — Core types from Tasks 1/2/6 are the enabler.

## Open Questions

- None blocking. Click frequencies/gains in Task 3 are starting values to be tuned by ear during the manual check.
