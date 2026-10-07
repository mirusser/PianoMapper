# Implementation Plan: Unified Metronome Practice Clock

**Date:** 2026-10-06  
**Status:** Implemented except for measurement-gated calibration (Tasks 6–7)

## Goal

Make the browser metronome the single authoritative practice clock wherever timing is graded or score playback is accompanied. Eliminate phase changes during an exercise, use one correctly-accented count-in path, synchronize optional score-playback clicks, make compound-meter tempo understandable, and improve the manual metronome without weakening audio-clock accuracy.

## Restated request

Implement the recommended improvements from the metronome investigation, including its connection to note-reading exercises:

- prevent manual metronome actions from de-synchronizing a graded exercise;
- make the exercise click setting truthful and predictable;
- unify count-ins behind the existing metronome grid;
- use the audio clock for count-in transitions;
- synchronize an optional click with score playback;
- pursue latency calibration only when measurement proves it is needed; and
- improve manual metronome controls, grouping, and visual clarity.

## Context and current state

- `MetronomeGrid` already owns the musical timing model: audio-clock anchor, tempo, meter, compound grouping, nearest-beat lookup, and signed deviation.
- Browser audio schedules short oscillator clicks with a look-ahead window. `BrowserMetronome` creates the grid with a 50 ms lead.
- Wait-for-me and Play Along exercises already reuse `BrowserMetronome`; Play Along starts its click at the same anchor as `PracticeSession`.
- The page still exposes the manual metronome button during a graded exercise, while the exercise’s grading anchor remains unchanged. The exercise click checkbox can also change state without changing live audio.
- Standard score practice and some Play Along count-ins use separately scheduled uniform beeps rather than metronome accents. Wait-for-me count-in uses an audio anchor but completes based on a `TimeProvider` elapsed duration.
- Score playback and an existing manual click have independent anchors. Compound exercises present dotted-quarter tempo in their panel but written eighth-note BPM in the Timing card.

## Architecture decisions

1. **Keep `MetronomeGrid` as the source of truth.** Do not introduce a second practice-clock calculation. Cursor, click, count-in, and grading receive the same explicit audio-clock anchor.
2. **Lock click ownership during active timing-graded exercise runs.** The central manual Start/Stop control and the exercise’s continuing-click choice cannot replace the grid mid-run. The continuing-click choice is selected before Generate/Retry and remains fixed until review or end.
3. **Use the metronome for every count-in.** The existing one-measure count-in remains, but it receives the same accent pattern and scheduler whether it starts loaded-score practice, Play Along, or Wait-for-me.
4. **Drive count-in progress from `AudioContext.currentTime`.** `TimeProvider` remains suitable for non-audio UI concerns such as auto-next, but it is not the authority for an audible count-in or the first graded downbeat.
5. **Make score click opt-in and synchronized.** A learner explicitly selects “Play with click”; score events and the click start from one supplied anchor. No unsynchronized manual click may be started during score playback.
6. **Make calibration measurement-gated.** Never silently compensate. First measure the learner’s actual hardware; only build a stored, visible offset when the median bias warrants it. A calibration shifts grading anchors only—not click audio, visual pulse, event timestamps, or cursor positions.
7. **Grow manual features through explicit options.** Use a small metronome-options model for volume/timbre and accent groups instead of adding unrelated booleans to page state. Preserve today’s 4/4 and compound-meter behavior as defaults.

## Assumptions and trade-offs

- This plan is browser-only. The desktop client does not currently have a comparable metronome implementation; adding cross-client parity is a separate product slice.
- Locking a click setting for an active exercise is deliberately preferred to live reconfiguration. It avoids a new, potentially phase-shifted click track and makes the grading contract clear.
- Timing-card controls stay mirrored and disabled while an exercise is active as they are today. The new ownership guard extends that rule to the central click control.
- Manual metronome enhancements follow correctness and synchronization work. They must not delay fixes that affect fair grading.

## Acceptance criteria for the complete change

- A learner cannot cause the heard click to diverge from a timing exercise’s grading anchor through any supported control path.
- Every count-in uses the same `MetronomeGrid` beat/accent rules, including the 6/8 `ONE two three TWO two three` pattern.
- The first graded exercise beat occurs at the audio-clock downbeat, including at Strict (30 ms) tolerance.
- When score playback is configured to use a click, its first score event and click downbeat share one anchor.
- A 6/8 exercise explains both the learner-facing dotted-quarter pulse and the written eighth-note BPM.
- Calibration is absent unless a documented real-device measurement meets the implementation threshold.

## Dependency map

```text
Grid ownership + locked exercise settings
                 │
                 ├── shared metronome count-in lifecycle
                 │        └── audio-clock count-in transition
                 │                 └── score-playback synchronized click
                 │
                 └── compound-tempo copy and manual controls
                           └── measurement-gated calibration
```

## Task list

### Phase 1 — protect the grading clock

#### Task 1: Lock metronome ownership for active timing exercises

**Description:** Add a presentation-level indication that a timing-graded exercise owns the click. Disable the central manual metronome action and “Keep the click going while playing” checkbox while a generated run is active or counting in; re-enable configuration in review and after End. Defensively reject a manual toggle request while this lock applies, so keyboard, automation, or future UI changes cannot bypass the disabled control. Explain in the Timing card that the run owns its clock.

**Acceptance criteria:**

- [x] An active onset- or duration/timing-graded exercise cannot start, stop, or re-anchor the manual metronome.
- [x] The continuing-click checkbox is selectable before a run and after review/end, but not while the run is active or counting in.
- [x] Pitch-only exercises retain normal manual-metronome access, and the existing exercise-end cleanup still releases ownership.

**Likely files:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Components/MetronomeControls.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Components/SightReadingExercisePresentation.cs`
- relevant `PianoMapper.Tests/UnitTests/*Exercise*Tests.cs`

**Verification:**

- [x] Unit tests cover the lock decision for active, counting-in, review, and pitch-only states.
- [ ] Manual browser check: attempt both central click actions and the exercise checkbox during a running timed exercise; the grid anchor and click state do not change.
- [x] Run the focused exercise/metronome test filter and the browser JavaScript tests.

**Dependencies:** None.  
**Estimated scope:** M

#### Task 2: Introduce one reusable metronome count-in lifecycle

**Description:** Replace the separate fixed-frequency count-in scheduling in `BrowserPracticeCoordinator` with a small browser-practice click lifecycle backed by `BrowserMetronome`/`MetronomeGrid`. It starts from the exact practice-run start, stops after the count-in when the learner chose no continuing click, and continues otherwise. Use it for standard loaded-score practice and Play Along; retain the ownership semantics that prevent an exercise cleanup from silencing an unrelated click.

**Acceptance criteria:**

- [x] Loaded-score practice, Play Along, and Wait-for-me all use metronome accents for their count-ins.
- [x] Play Along with continuing click off sounds the count-in but no click after its first graded downbeat; with it on, one click track continues without duplicate beeps.
- [x] Standard practice preserves its existing count-in duration, grading anchor, abort behavior, and score scheduling behavior.

**Likely files:**

- `PianoMapper.Web/Practice/BrowserPracticeCoordinator.cs`
- `PianoMapper.Web/Practice/ExercisePlayAlongController.cs`
- `PianoMapper.Web/Practice/ExerciseClick.cs` (rename or extract only if the shared role is clearer)
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/BrowserPracticeCoordinatorTests.cs`
- `PianoMapper.Tests/UnitTests/ExercisePlayAlongControllerTests.cs`

**Verification:**

- [x] Unit tests prove one audio click track only and verify stop/continue behavior at the count-in-to-running transition.
- [x] JavaScript test asserts 6/8 count-in oscillator accents match the regular metronome.
- [ ] Manual browser check covers standard practice and both Play Along click choices in 4/4 and 6/8.

**Dependencies:** Task 1.  
**Estimated scope:** L — split the shared lifecycle extraction from call-site adoption if either becomes difficult to review.

#### Task 3: Make Wait-for-me count-in completion audio-clock driven

**Description:** Change `SightReadingExerciseCoordinator`’s count-in API so the page supplies the current audio-clock time on each tick. Derive both the count-in beat indicator and completion from the grid/start anchor, then reset the note-reading session exactly on the next downbeat. Remove the mixed `TimeProvider` elapsed-time decision from the audio count-in path.

**Acceptance criteria:**

- [x] Count-in status and first graded beat derive from the same audio-clock anchor as the audible click.
- [x] At a 50 ms scheduling lead and Strict tolerance, the coordinator never reports “running” before the first graded downbeat.
- [x] Retry, End, focus loss, and audio-unavailable paths retain their present cancellation behavior.

**Likely files:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Verification:**

- [x] Unit tests use a fake audio-clock sequence around the anchor and prove no early transition.
- [x] Existing count-in/retry/focus-loss tests still pass.
- [ ] Manual browser check at 30 ms tolerance confirms the visual state changes on the audible downbeat.

**Dependencies:** Task 2.  
**Estimated scope:** M

### Checkpoint: fair, coherent exercise timing

- [x] Focused .NET unit tests for metronome, practice, Play Along, and exercise coordination pass.
- [x] JavaScript audio tests pass, including 6/8 accent behavior.
- [ ] In a browser, no supported exercise control can replace the grading grid mid-run.
- [ ] Review the interaction wording before moving to score-playback and convenience features.

### Phase 2 — synchronize score playback and clarify tempo

#### Task 4: Add opt-in, anchor-synchronized score playback click

**Description:** Add a “Play with click” control to score playback. Have the page establish one explicit future audio-clock anchor and pass it to both `BrowserScorePlayback` and `BrowserMetronome`. When the option is off, stop an existing manual click before playback rather than leaving it unsynchronized; while score playback is active, prevent a new manual click from starting mid-piece.

**Acceptance criteria:**

- [x] With “Play with click” enabled, playback’s first score event and metronome downbeat have identical anchors.
- [x] With it disabled, playback is silent with respect to the click and a pre-existing manual click cannot remain out of phase.
- [x] Playback restart, Stop/Clear, timing edits, and end-of-score cleanup release the synchronized click correctly.

**Likely files:**

- `PianoMapper.Web/Playback/BrowserScorePlayback.cs`
- `PianoMapper.Web/Audio/BrowserMetronome.cs`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Components/*Playback*.razor` or the existing playback markup
- `PianoMapper.Tests/UnitTests/BrowserScorePlaybackTests.cs`
- `PianoMapper.Tests/UnitTests/BrowserMetronomeTests.cs`

**Verification:**

- [ ] Unit test supplies an explicit anchor and asserts identical score/click anchors.
- [ ] Browser audio test observes a single downbeat click at score start and no lingering clicks after stop.
- [ ] Manual 4/4 and 6/8 playback checks verify start, restart, and end behavior.

**Dependencies:** Tasks 1–3.  
**Estimated scope:** M

#### Task 5: Clarify compound-meter tempo and clock ownership copy

**Description:** Make 6/8 exercise timing show both equivalent values—dotted-quarter pulses per minute and written eighth-note BPM—and make the Timing card identify whether it is controlled by exercise, playback, or manual practice. Keep current tempo math unchanged.

**Acceptance criteria:**

- [x] A 40 dotted-quarter-pulse 6/8 exercise visibly explains its equivalent 120 eighth-note BPM.
- [x] Ownership/disabled-control text accurately describes active exercise and score-playback conditions.
- [x] 4/4 and other simple meters retain their current concise BPM display.

**Likely files:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`
- UI/component test files if present

**Verification:**

- [x] Unit tests cover simple versus compound label formatting.
- [ ] Browser visual check at compact and normal layouts confirms copy remains readable and controls do not wrap unexpectedly.

**Dependencies:** Task 4.  
**Estimated scope:** S

### Phase 3 — calibration, then manual metronome capabilities

#### Task 6: Run the latency measurement gate before implementing compensation

**Description:** Perform the existing real-device procedure: tap along to 16 clicks using the browser piano sound and again using the external piano sound, recording signed median deviation with the current feedback chip. Record the device/browser/output configuration and result in the browser test matrix.

**Acceptance criteria:**

- [ ] Measurements use a physical MIDI keyboard and audible output, not mocked MIDI.
- [ ] Each output path has a recorded signed median from 16 taps.
- [ ] If the absolute median is below 25 ms for a path, no compensation is built for that path; if it exceeds 25 ms, Task 7 is approved for that path.

**Likely files:**

- `docs/browser-test-matrix.md`

**Verification:**

- [ ] A reviewer can reproduce the setup and read the raw result and go/no-go decision.

**Dependencies:** Tasks 1–5.  
**Estimated scope:** S, but requires the user’s physical hardware.

#### Task 7: Add visible per-output calibration only if Task 6 passes the gate

**Description:** If measurement warrants it, add a short guided calibration action in Timing & metronome, store an explicit signed offset per selected browser sound source, and apply the offset solely to grading anchors for timing exercises and practice grading. Provide reset and a visible current value. Do not alter event timestamps, generated audio times, pulse animation, or cursor positions.

**Acceptance criteria:**

- [ ] Calibration is opt-in, visible, resettable, and scoped to the active sound-output path.
- [ ] A constant simulated audio-output bias grades late before calibration and on-time after the expected anchor shift.
- [ ] Click timing, score cursor timing, and raw MIDI/browser event timestamps are unchanged by calibration.

**Likely files:**

- `PianoMapper.Core/Practice/GradingOptions.cs`
- `PianoMapper.Core/Practice/PracticeSession.cs` and/or `NoteReadingSession.cs`
- `PianoMapper.Web/Practice/*TimingCalibration*.cs` (new)
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/wwwroot/js/*timing-calibration*.js` (new, if browser storage interop is needed)
- corresponding .NET and JavaScript tests

**Verification:**

- [ ] Unit tests cover zero, positive, and negative offsets for both Wait-for-me and Play Along grading anchors.
- [ ] JavaScript tests cover durable per-output storage and unavailable storage fallback.
- [ ] Manual real-device retest meets the intended fairness threshold without changing audible synchronization.

**Dependencies:** Task 6, and only if its measurement gate passes.  
**Estimated scope:** L — retain as a separate reviewed change from the synchronization work.

#### Task 8: Add configurable manual metronome sound and grouping foundations

**Description:** Define an explicit browser metronome-options model with click volume/timbre and a beat-group pattern. Preserve default 4/4 and existing 6/8 accents; add selectable common 5/8 and 7/8 groupings (for example 2+3, 3+2, 2+2+3) instead of treating every non-compound meter as one ungrouped sequence. Pass only the resolved accent pattern to JavaScript scheduling.

**Acceptance criteria:**

- [x] Existing 4/4 and 6/8 frequencies/accents are unchanged by default.
- [x] A selected irregular grouping accents the first beat of each configured group and only those beats.
- [x] Volume and timbre apply to future scheduled clicks without changing grid timing or grading.

**Likely files:**

- `PianoMapper.Core/Music/MetronomeGrid.cs` or a focused adjacent grouping type
- `PianoMapper.Web/Audio/BrowserMetronome.cs`
- `PianoMapper.Web/Playback/IBrowserMetronomeAudio.cs`
- `PianoMapper.Web/Audio/WebAudioSession.cs`
- `PianoMapper.Web/wwwroot/js/audio.js`
- metronome .NET and JavaScript test files

**Verification:**

- [x] Core tests cover default and irregular accent indices.
- [ ] JavaScript oscillator tests cover every accent strength/timbre combination and verify exact scheduled times.
- [ ] By-ear browser checks cover 4/4, 6/8, 5/8, and 7/8.

**Dependencies:** Tasks 1–4.  
**Estimated scope:** L — implement the grouping contract first, then the audio options in a follow-up commit if review size becomes excessive.

#### Task 9: Add manual practice controls and visual beat feedback

**Description:** Build on Task 8 with a tap-tempo action and a textual current-beat/count display beside the existing pulse. Tap tempo uses already audio-clock-mapped input timestamps and only updates tempo between runs; visual feedback reads the active grid/pattern without becoming a second clock. Persist non-sensitive manual preferences through the existing browser-preference convention where appropriate.

**Acceptance criteria:**

- [x] Four or more taps produce a bounded, musically sensible tempo and restart the manual grid only when it is safe to do so.
- [x] The displayed beat count agrees with the audible accent and resets on each configured group/measure boundary.
- [x] The controls are unavailable during locked exercise and score-playback timing states.

**Likely files:**

- `PianoMapper.Web/Components/MetronomeControls.razor`
- `PianoMapper.Web/Components/TempoFeedback.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/wwwroot/js/audio.js`
- `PianoMapper.Web/wwwroot/js/*preferences*.js`
- component/.NET/JavaScript tests

**Verification:**

- [x] Unit tests cover tap-tempo median/outlier behavior and safe-state checks.
- [x] Browser tests assert beat-number transitions against scheduled clicks.
- [ ] Manual keyboard, pointer, MIDI, and compact-layout checks confirm normal piano input remains unaffected.

**Dependencies:** Tasks 1–5 and 8.  
**Estimated scope:** M

### Completion checkpoint

- [x] Run the repository’s documented .NET test suite, JavaScript test suite, and Release build.
- [x] Update `README.md` and `docs/browser-test-matrix.md` for every shipped control, timing constraint, and physical verification result.
- [ ] Verify the web app manually with browser synth/piano audio and external MIDI output, including 4/4, 6/8, standard practice, Wait-for-me, Play Along, and score playback.
- [x] Confirm no implementation changes were made to the desktop app unless separately approved.

## Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| A late-added click creates a second or phase-shifted audio track | High | Lock exercise click selection per run; give every click an explicit grid anchor; test one-track behavior. |
| Refactoring count-ins changes existing practice grading | High | Preserve `PracticeAnchor` semantics with fake-audio tests before replacing the scheduler. |
| Browser timer throttling affects a visual beat indicator | Medium | Keep audio scheduling in Web Audio; treat UI animation as observational and stop/recover safely on focus loss. |
| Calibration hides a learner’s rhythmic tendency | Medium | Require a real-device measurement gate, make calibration explicit/resettable, and preserve signed feedback. |
| Irregular meter options make the UI hard to understand | Medium | Offer only common group patterns after a meter is selected; keep automatic simple/compound defaults. |
| Feature scope obscures the fairness fixes | Medium | Land Phase 1 as a reviewable change before optional calibration and manual-control enhancements. |

## Open questions

- Which browser/audio-device combinations should be treated as supported targets for the physical calibration measurement?
- Should “Play with click” be remembered as a browser preference or reset for each score session? The recommended initial behavior is session-only until real usage validates persistence.
- Which initial timbres should the manual metronome offer beyond the existing sine click? The recommended first release is a small fixed set, not arbitrary synthesis controls.

## Approval gate

Do not start implementation until this plan is approved. Begin with Tasks 1–3; they protect grading fairness and provide the stable foundation for the remaining slices.
