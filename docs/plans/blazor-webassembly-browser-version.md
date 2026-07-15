# Implementation Plan: Blazor WebAssembly Browser Version

**Date:** 2026-07-15
**Status:** Implementation complete; manual browser, Desktop GUI, and hosting review pending
**Target:** Preserve the existing OpenTK/OpenAL desktop application and add a standalone Blazor WebAssembly browser application.

## Goal

Deliver a browser-hosted PianoMapper that covers the current interactive feature set using client-side WebAssembly and browser-native APIs, while keeping the desktop application working and avoiding a shared lowest-common-denominator UI. The browser application should run as static assets over HTTPS, synthesize audio on the user's device, and, after browser parity stabilizes, support offline use as a PWA.

## Context

The current `PianoMapper` executable targets .NET 10 and combines OpenTK windowing/input, OpenGL rendering, OpenAL audio, score playback, and practice orchestration in `PianoMapperWindow`. Music, score, timing, grading, and much of the layout math are platform-neutral but currently live in the same executable assembly and are mostly internal. The browser host cannot use OpenTK or OpenAL directly, so it needs browser adapters for input, audio, rendering, file selection, and lifecycle.

The implementation should fail fast on the highest-risk path: keyboard event -> accurate timestamp -> local Web Audio note-on/note-off -> live canvas note. Large-scale extraction and secondary feature parity should happen only after that path meets the latency checkpoint.

## Restated Request

- Keep the existing desktop application and its current desktop shortcuts.
- Add a separate browser version using standalone Blazor WebAssembly.
- Reuse the platform-neutral C# domain, practice, scheduling, and layout logic.
- Replace OpenAL with Web Audio and OpenGL with browser canvas rendering.
- Do not globally capture or repurpose browser/navigation keys such as Space, Tab, Enter, Escape, Page Up/Down, or the arrow keys.
- Give the browser host visible controls and a distinct set of ordinary, non-reserved shortcuts.

## Scope

### In scope

- Computer-keyboard piano input with sustained note-on/note-off behavior.
- Octave selection, clearing notes, view switching, score playback, measure navigation, random-measure playback, and practice controls.
- MusicXML file selection from the browser.
- Live grand staff, score notation, playback cursor, piano roll, verdict colors, and practice summary.
- Browser-local piano-style synthesis, oscilloscope, and spectrum.
- Standalone static deployment, HTTPS compatibility, and published PWA/offline behavior.
- Incremental extraction of a `PianoMapper.Core` library shared by desktop and web.

### Deliberately out of scope for the first browser release

- Web MIDI or physical MIDI-device integration. This is an additive feature, not current parity.
- A server backend, accounts, cloud synchronization, or remote audio streaming.
- New MusicXML capabilities such as compressed `.mxl`, multipart scores, tuplets, or grace notes.
- A touch-screen piano keyboard or mobile-specific redesign.
- Bit-for-bit reproduction of the desktop PCM output; the initial requirement is equivalent piano-style multi-harmonic behavior and note lifecycle.
- Replacing or redesigning the desktop OpenTK/OpenAL host.

## Overall Acceptance Criteria

- The existing desktop app still builds, runs, and passes all pre-existing tests throughout the work.
- The browser app loads without a server-side .NET runtime and can be published as static files.
- Pressing and releasing the existing piano note keys produces local browser audio and matching `PerformedNote` timing without stuck notes.
- Browser-reserved/navigation keys retain their native behavior; the app does not register global handlers for them.
- A user can select a supported MusicXML file, view it, play it, navigate measures, complete a practice session, and see grading feedback.
- Grand-staff, piano-roll, oscilloscope, and spectrum views operate from browser-local state/audio.
- The published PWA works offline after its first successful online load.
- Browser-specific unsupported states, especially blocked audio startup, are visible and recoverable.

## Architecture Decisions

1. **Use a standalone Blazor WebAssembly host.** No Blazor Server or mandatory backend is needed for current features; input, timing, rendering, and audio stay on the user's device.
2. **Keep `PianoMapper` as the desktop composition root.** It continues to own OpenTK/OpenAL code and preserves current behavior.
3. **Introduce `PianoMapper.Core` incrementally.** Move only code required by the next vertical browser slice, retain existing namespaces where practical, and expose the smallest intentional public API between assemblies. Keep implementation types internal and grant test access explicitly rather than making types public only for tests.
4. **Keep platform adapters separate.** Desktop `Keys`, OpenAL handles, OpenGL buffers, browser key codes, `AudioContext`, and canvas state do not enter the core project.
5. **Use coarse-grained JavaScript interop.** Audio and canvas modules receive complete commands, schedules, or scene data rather than one interop call per audio sample, vertex, or FFT bin.
6. **Use one browser timing domain.** Capture `KeyboardEvent.timeStamp` at the JavaScript boundary and convert it from the `performance.now()` origin to the Web Audio clock using an anchor pair captured in the same JavaScript module. Audio commands, `PerformedNote` timestamps, cursors, and grading consume that mapped time rather than the later .NET callback time.
7. **Render with Canvas 2D first.** Existing layout math remains in C#; web-specific scene builders create batched primitives for a small canvas module. WebGL is considered only if profiling shows Canvas is insufficient.
8. **Use browser-native audio analysis.** `AnalyserNode` supplies time-domain and frequency-domain data; the browser host does not reproduce OpenAL sample-offset tracking.
9. **Do not force the existing `IAudioDispatcher` abstraction onto the web.** Its dedicated-thread/action-queue contract is OpenAL-shaped. The browser gets a small Web Audio session module and JavaScript adapter while sharing note, score, and practice state.

## Dependency Order

```text
Project scaffolding
    -> shared live-note types
        -> Web Audio prototype
            -> browser keyboard path
                -> minimal canvas note path
                    -> latency go/no-go checkpoint
                        -> shared score/import types
                            -> shared rolling-window and staff layout
                                -> staff rendering and score playback
                                    -> shared practice/grading
                                        -> remaining visual parity
                                            -> PWA, browser hardening, and documentation
```

## Browser Key Policy

The note keys remain physically equivalent to the desktop layout and use `KeyboardEvent.code`, so keyboard language/layout does not change the piano geometry.

| Function | Browser binding | Notes |
|---|---|---|
| Piano notes | `A W S E D F R J U K I L ;` | Same physical layout as desktop |
| Clear notes / abort practice | `C` | Visible button is always available |
| Toggle staff/piano roll | `V` | Replaces desktop Tab |
| Start/restart score playback | `P` | Retains the existing non-reserved shortcut |
| Start/retry practice | `T` | Replaces desktop Enter |
| Previous/next measure group | `[` / `]` | Replaces Page Up/Down |
| Octave down/up | `Z` / `X` | Replaces arrow keys |
| Select octave | `1` through `8` | Unmodified digit keys only |
| Random measure | `M` | Retains the existing non-reserved shortcut |
| Exit | None | A browser page is not closed programmatically |

Keyboard listeners are scoped to the focused play surface, ignore input/textarea/select/content-editable targets, ignore repeated `keydown` events, and release active notes on blur or visibility loss. Only a recognized app binding may call `preventDefault`; the reserved/navigation keys listed above are neither registered nor suppressed. Native button keyboard behavior remains available through normal browser accessibility; it is not replaced by global shortcuts.

## Phased Task Checklist

### Phase 1: Preserve the Desktop Baseline and Create Project Boundaries

#### Task 1: Record the baseline and add the shared core project

**Description:** Run the existing unit suite, add an empty `PianoMapper.Core` .NET 10 class library, wire solution/project references, and leave all production types in their current assembly initially.

**Recorded baseline (2026-07-15):** `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` passed 223 tests with 0 warnings before structural changes.

**Acceptance criteria:**

- [x] The baseline test count and result are recorded before structural changes.
- [x] `PianoMapper`, `PianoMapper.Core`, and `PianoMapper.Tests` build in the solution.
- [x] `PianoMapper.Tests` references `PianoMapper.Core` directly, and `PianoMapper.Core` grants `PianoMapper.Tests` access to internal types without widening the production API.
- [x] The desktop executable still starts through its current project path.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`
- [ ] Manual smoke: launch the desktop app and play/release one mapped note.

- **Dependencies:** None
- **Files likely touched:** `PianoMapper.slnx`, `PianoMapper.Core/PianoMapper.Core.csproj`, `PianoMapper/PianoMapper.csproj`, `PianoMapper.Tests/PianoMapper.Tests.csproj`
- **Estimated scope:** Medium

#### Task 2: Extract the live-note domain slice

**Description:** Move only the types needed to describe and retain a browser key performance into the core assembly while preserving namespaces and desktop behavior. Add explicit-time note-start and snapshot/pruning paths so the browser can retain and age notes using the mapped Web Audio time; keep the existing desktop convenience paths backed by `TimeProvider`.

**Acceptance criteria:**

- [x] `Pitch`, `NoteLetter`, `PerformedNote`, and `NoteTimeline` are usable by both hosts without referencing OpenTK/OpenAL or rendering code.
- [x] Browser callers can start, complete, snapshot, and prune notes using mapped `TimeSpan` values without mixing in the Core timeline's `TimeProvider` origin; desktop callers retain current `TimeProvider` behavior.
- [x] `NotePlayback` remains in the desktop audio module because only `Instrument` consumes its completion contract.
- [x] Public accessibility is limited to cross-host contracts; implementation details remain internal where possible.
- [x] Existing pitch, timeline, playback-position, and instrument tests continue to compile and pass.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~PitchTests|FullyQualifiedName~NoteTimelineTests|FullyQualifiedName~PlaybackPositionTests|FullyQualifiedName~InstrumentTests"`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

- **Dependencies:** Task 1
- **Files likely touched:** `PianoMapper/Music/NoteLetter.cs`, `PianoMapper/Music/Pitch.cs`, `PianoMapper/PerformedNote.cs`, `PianoMapper/NoteTimeline.cs` and their destinations under `PianoMapper.Core/`, plus `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs`
- **Estimated scope:** Medium

#### Task 3: Separate physical-key offsets from OpenTK keys

**Description:** Introduce a core helper that maps an octave plus semitone offset to `Pitch`; keep the OpenTK key-to-offset table in the desktop host and use the same helper from browser bindings later.

**Acceptance criteria:**

- [x] Core pitch mapping contains no `OpenTK.Keys` references.
- [x] The desktop piano note mapping is unchanged for every existing key and octave.
- [x] Mapping tests cover the first note, chromatic offsets, and the octave boundary.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ConstsTests|FullyQualifiedName~PianoKeyboardLayoutTests"`
- [ ] Manual desktop check: `A` and `;` still map to the expected boundary pitches.

- **Dependencies:** Task 2
- **Files likely touched:** `PianoMapper.Core/Input/PianoKeyboardLayout.cs`, `PianoMapper/Consts.cs`, `PianoMapper.Tests/UnitTests/ConstsTests.cs`, `PianoMapper.Tests/UnitTests/PianoKeyboardLayoutTests.cs`
- **Estimated scope:** Medium

#### Task 4: Add the minimal standalone Blazor WebAssembly shell

**Description:** Add a minimal .NET 10 standalone Blazor WebAssembly project with a single focused play page. Do not add PWA/offline assets or sample template features yet.

**Acceptance criteria:**

- [x] `PianoMapper.Web` references `PianoMapper.Core` but not the desktop project or OpenTK packages.
- [x] `PianoMapper.Tests` can reference internal browser orchestration types through an explicit project reference and `InternalsVisibleTo`; no browser type is made public only for testing.
- [x] The web project loads a minimal PianoMapper page and reports that audio is not yet initialized.
- [x] The play surface is focusable, visibly labelled, and does not install a document-global keyboard listener.
- [x] The solution builds without changing desktop startup behavior.

**Verification:**

- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`
- [x] `rtk dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj`
- [ ] Manual browser check: the play page renders with no console exceptions.

- **Dependencies:** Task 1
- **Files likely touched:** `PianoMapper.slnx`, `PianoMapper.Web/PianoMapper.Web.csproj`, `PianoMapper.Web/Program.cs`, `PianoMapper.Web/App.razor`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/PianoMapper.Tests.csproj`
- **Estimated scope:** Medium

### Checkpoint: Desktop Baseline and Project Boundaries

- [x] The recorded baseline tests still pass after the empty Core and Web projects are added.
- [x] A Release solution build includes Desktop, Core, Web, and Tests.
- [ ] The desktop app still launches and plays/releases a note.
- [x] Core has no OpenTK/OpenAL/browser dependency, and Web has no desktop project reference.

### Phase 2: Prove the High-Risk Browser Note Path

#### Task 5: Implement an explicit Web Audio initialization and note prototype

**Description:** Add a small JavaScript audio module and a C# `WebAudioSession` that imports it through the existing `IJSRuntime` seam, creates/resumes an `AudioContext` from an explicit user action, starts one piano-style note, stops it, and clears all active nodes. Do not add a second public audio interface for the single browser adapter.

**Acceptance criteria:**

- [x] Audio initialization succeeds only after a visible user action and exposes blocked/failure state in the UI.
- [x] Initialization returns the Web Audio/performance clock anchor needed to map subsequent browser event timestamps.
- [x] Note-on, note-off, and clear commands are batched at command level rather than sample level.
- [x] Repeated note cycles release browser audio nodes and do not accumulate active state.

**Verification:**

- [x] Unit tests with a hand-written `IJSRuntime` fake verify module/command ordering and clear behavior.
- [ ] Manual check in current Chrome and Firefox: enable audio, play, release, and replay a note.
- [x] Browser console contains no unhandled promise rejections.

- **Dependencies:** Tasks 2 and 4
- **Files likely touched:** `PianoMapper.Web/Audio/WebAudioSession.cs`, `PianoMapper.Web/wwwroot/js/audio.js`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/WebAudioSessionTests.cs`
- **Estimated scope:** Medium

#### Task 6: Complete keyboard note-on/note-off with browser-safe controls

**Description:** Route scoped browser keyboard events into the shared pitch mapping and Web Audio session module, record `PerformedNote` values with the mapped event timestamp, and implement only the clear/octave bindings needed by this slice. Later feature tasks add their own binding when the corresponding visible control and behavior exist.

**Acceptance criteria:**

- [x] All 13 piano keys support independent concurrent note-on/note-off with key-repeat filtering.
- [x] Note start/release timestamps come from `KeyboardEvent.timeStamp` mapped to the audio clock, not from callback arrival time in .NET.
- [x] Blur and `visibilitychange` release every active note and prevent stuck audio.
- [x] Changing octave while a key is held still releases the originally started note; clearing notes empties both audio and key-tracking state so a later keyup is harmless.
- [x] `C`, `Z`/`X`, and `1`-`8` work while the play surface is focused and have visible-control equivalents.
- [x] Space, Tab, Enter, Escape, Page Up/Down, and arrow keys are not registered as app shortcuts and are never globally suppressed.

**Verification:**

- [x] Unit tests cover note-key mapping, timestamp conversion, current control mappings, repeats, and unknown/reserved keys.
- [ ] Manual browser check confirms browser Tab navigation, Space/Enter button activation, page scrolling, and Escape behavior remain native.
- [ ] Measure event-to-audio-schedule delay for 100 presses on the target laptop against the approved Checkpoint A limits (provisional target: median <= 20 ms and p95 <= 50 ms), excluding device output latency.
- [ ] If the JS -> .NET -> JS path misses the approved target, keep the latency-sensitive key-to-audio dispatch inside the JavaScript audio module, install or refresh the C#-generated code-to-pitch table at initialization and octave changes, and report the same mapped event to .NET; do not maintain a second handwritten pitch map.

- **Dependencies:** Tasks 3 and 5
- **Files likely touched:** `PianoMapper.Web/Input/BrowserKeyBindings.cs`, `PianoMapper.Web/Audio/WebAudioSession.cs`, `PianoMapper.Web/wwwroot/js/keyboard.js`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/BrowserKeyBindingsTests.cs`
- **Estimated scope:** Medium

#### Task 7: Complete the minimal keyboard-to-audio-to-canvas slice

**Description:** Add a reusable canvas component and an initial live-note scene builder, then display the active/released note state produced by Task 6. This is intentionally a minimal marker scene; Task 14 deepens and renames the same builder when shared grand-staff layout becomes available.

**Acceptance criteria:**

- [x] A piano-key press updates browser audio and a matching canvas note marker from the same mapped event; release stops audio and closes the same `PerformedNote`.
- [x] One render crosses JS interop as a complete scene batch rather than one call per primitive.
- [x] Resize and disposal do not duplicate listeners, animation frames, or canvas ownership.

**Verification:**

- [x] Scene-builder unit tests cover active and released notes plus empty state.
- [ ] Manual check confirms a 13-key sequence and a chord produce matching audible and visible state.
- [x] Browser profiling records event-to-audio scheduling and event-to-canvas presentation measurements used by Checkpoint A.

- **Dependencies:** Task 6
- **Files likely touched:** `PianoMapper.Web/Rendering/LiveNoteSceneBuilder.cs`, `PianoMapper.Web/Components/PianoCanvas.razor`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/LiveNoteSceneBuilderTests.cs`
- **Estimated scope:** Medium

### Checkpoint A: Browser Feasibility Gate

**Automated feasibility evidence (2026-07-15):** Headless Chromium completed 100 keyboard-to-audio scheduling samples with median 0.9 ms and p95 1.1 ms; the automated canvas presentation probe completed in 59 ms. Polyphony, release, blur cleanup, native reserved-key behavior, and zero console/page errors passed. These measurements do not replace the primary-laptop audio, sound-quality, Firefox, or human-review checks below.

- [x] Desktop tests and Release build remain green.
- [x] Keyboard polyphony, releases, blur cleanup, and visible controls work end to end.
- [x] Keyboard -> mapped `PerformedNote` -> Web Audio -> canvas works end to end.
- [ ] The measured timing target is met on the primary laptop/browser.
- [ ] The sound is acceptable as a piano-style prototype, or exact timbre work is explicitly approved before continuing.
- [x] Browser-reserved keys retain native behavior.
- [ ] Human review approves proceeding with the remaining extraction and parity work.

### Phase 3: Share Scores and Render the Primary Browser Experience

#### Task 8: Extract rhythmic primitives and conversions

**Description:** Move note values, tempo, time signatures, and musical-time conversion into the core assembly without changing behavior.

**Acceptance criteria:**

- [x] Rhythm types have no desktop or rendering dependency.
- [x] Existing validation and conversion behavior remains identical.
- [x] Tests continue to use deterministic values and no wall-clock sleeps.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~NoteValueTests|FullyQualifiedName~TempoTests|FullyQualifiedName~TimeSignatureTests|FullyQualifiedName~MusicalTimeTests"`

- **Dependencies:** Task 2
- **Files likely touched:** `PianoMapper/Music/NoteValue.cs`, `PianoMapper/Music/Tempo.cs`, `PianoMapper/Music/TimeSignature.cs`, `PianoMapper/Music/MusicalTime.cs` and their destinations under `PianoMapper.Core/`
- **Estimated scope:** Medium

#### Task 9: Extract the score aggregate

**Description:** Move the platform-neutral score, measure, note, rest, and staff model into the core assembly.

**Acceptance criteria:**

- [x] The `Score` aggregate retains the terminology and exclusions defined in `CONTEXT.md`.
- [x] No MusicXML, rendered geometry, performed-note, or audio state leaks into the aggregate.
- [x] Existing score validation tests remain unchanged in behavior.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreTests"`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

- **Dependencies:** Task 8
- **Files likely touched:** `PianoMapper/Music/Staff.cs`, `PianoMapper/Music/Score.cs`, `PianoMapper/Music/ScoreMeasure.cs`, `PianoMapper/Music/ScoreNote.cs`, `PianoMapper/Music/ScoreRest.cs` and their destinations under `PianoMapper.Core/`
- **Estimated scope:** Medium

#### Task 10: Extract score derivation and playback scheduling

**Description:** Move flattened score events and deterministic schedule calculations into core; keep actual audio scheduling in each platform host.

**Acceptance criteria:**

- [x] Core produces the same event ordering, due times, durations, and cursor beats as the desktop implementation.
- [x] Core scheduling has no timer, OpenAL, Web Audio, or render-loop dependency.
- [x] Desktop score playback continues to use the extracted schedule without behavior changes.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScorePlaybackTests"`
- [ ] Manual desktop check: load a fixture and start score playback.

- **Dependencies:** Task 9
- **Files likely touched:** `PianoMapper/Music/ScoreEvent.cs`, `PianoMapper/Music/ScheduledScoreEvent.cs`, `PianoMapper/Music/ScoreDerivation.cs`, `PianoMapper/Music/ScorePlayback.cs` and their destinations under `PianoMapper.Core/`
- **Estimated scope:** Medium

#### Task 11: Add stream-based MusicXML import and browser file selection

**Description:** Move `MusicXmlScoreReader` into core, add a stream-based entry point, retain the desktop path overload as a thin adapter, and expose browser file selection with readable errors.

**Recorded browser file limit (2026-07-15):** 10 MiB per selected MusicXML file. The picker rejects larger files before opening the browser stream and displays the limit before selection.

**Acceptance criteria:**

- [x] Path and stream imports produce equivalent `Score` values for every existing fixture.
- [x] The browser reads only the selected file and enforces the size limit approved before this task; the UI states that limit before selection.
- [x] Unsupported/malformed files show the existing meaningful error rather than leaving partial score state.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests|FullyQualifiedName~ScoreCommandLineTests"`
- [ ] Manual browser check: load a supported fixture, then a malformed and unsupported fixture.

- **Dependencies:** Task 9
- **Files likely touched:** `PianoMapper/Music/MusicXmlScoreReader.cs` and its destination under `PianoMapper.Core/`, `PianoMapper/ScoreCommandLine.cs`, `PianoMapper.Web/Components/ScoreFilePicker.razor`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`
- **Estimated scope:** Medium

### Checkpoint: Shared Score Model and Import

- [x] Rhythm, Score, scheduling, and MusicXML tests pass from `PianoMapper.Core`.
- [x] The desktop loads every supported fixture and rejects existing unsupported fixtures with unchanged error categories.
- [ ] The browser selects a supported fixture and reports malformed/unsupported input without retaining partial score state.
- [x] A Release solution build remains green before rendering extraction continues.

#### Task 12: Extract the rolling-window layout dependency

**Description:** Move `BarRect` and the pure `PianoRollLayout` calculations into core before moving `GrandStaffLayout`, which currently consumes `PianoRollLayout.BandY0`, `BandY1`, `RollingWindowSeconds`, and `MapTimeToX`.

**Acceptance criteria:**

- [x] The shared rolling-window layout contains no OpenGL, OpenTK, browser canvas, or audio dependency.
- [x] Open, released, expired, low, and high note rectangle behavior remains unchanged.
- [x] Desktop rendering continues to consume the same normalized rectangle values.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~PianoRollLayoutTests"`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

- **Dependencies:** Task 2
- **Files likely touched:** `PianoMapper/Rendering/BarRect.cs`, `PianoMapper/Rendering/PianoRollLayout.cs` and their destinations under `PianoMapper.Core/`, `PianoMapper.Tests/UnitTests/PianoRollLayoutTests.cs`
- **Estimated scope:** Medium

#### Task 13: Extract grand-staff layout contracts

**Description:** Move the pure note-placement records/enums needed by both OpenGL and canvas rendering into core.

**Acceptance criteria:**

- [x] Layout contracts contain coordinates and notation state but no OpenGL or browser canvas handles.
- [x] Existing staff placement semantics and equality behavior remain unchanged.
- [x] Desktop renderer compiles against the core contracts.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffLayoutTests"`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

- **Dependencies:** Tasks 9 and 12
- **Files likely touched:** `PianoMapper/Rendering/StaffPlacement.cs`, `PianoMapper/Rendering/StaffPosition.cs`, `PianoMapper/Rendering/ScoreNoteLayout.cs`, `PianoMapper/Rendering/NoteHeadStyle.cs`, `PianoMapper/Rendering/StemDirection.cs` and their destinations under `PianoMapper.Core/`
- **Estimated scope:** Medium

#### Task 14: Render live notes on a browser grand staff

**Description:** Move `GrandStaffLayout` into core and deepen/rename the Task 7 live-note scene builder to emit staff lines, clefs, ledger lines, accidentals, and live notes. Keep glyph drawing and colors host-specific; share placement and notation-state calculations.

**Acceptance criteria:**

- [x] Live notes appear at the same normalized positions and with the same staff-selection rules as desktop.
- [x] Canvas resizes without changing pitch placement or stretching note geometry incorrectly.
- [x] One render frame crosses JS interop as a scene/typed batch, not per primitive.

**Verification:**

- [x] Existing `GrandStaffLayoutTests` pass from the core assembly.
- [x] Scene-builder unit tests cover middle C, accidentals, ledger lines, and resize mapping.
- [ ] Manual visual comparison against the desktop for the same note sequence.

- **Dependencies:** Tasks 7 and 13
- **Files likely touched:** `PianoMapper/Rendering/GrandStaffLayout.cs` and its destination under `PianoMapper.Core/`, `PianoMapper.Web/Rendering/LiveNoteSceneBuilder.cs` renamed to `GrandStaffSceneBuilder.cs`, `PianoMapper.Web/Components/PianoCanvas.razor`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`
- **Estimated scope:** Medium

### Checkpoint: Shared Layout and Live Staff

- [x] Existing `PianoRollLayoutTests` and `GrandStaffLayoutTests` pass from Core.
- [ ] The desktop live staff and piano roll remain unchanged in a manual comparison.
- [x] The browser prototype now renders the full live grand staff through one scene batch.
- [x] Core layout modules contain normalized geometry and notation state, not OpenGL/canvas handles or host colors.

#### Task 15: Render loaded scores and navigate measures

**Description:** Extend the browser staff scene for score notes, barlines, stems, dots, flags, accidentals, and measure windows; preserve imported rest/tie timing behavior and add visible Previous/Next controls plus `[`/`]` shortcuts.

**Acceptance criteria:**

- [x] The same visible-measure count and clamping rules are used as desktop.
- [x] Notes, chords, barlines, stems, dots, flags, and accidentals render consistently for existing fixtures; imported rests and ties retain their current timing behavior without adding notation that desktop does not draw.
- [x] Navigation works through buttons and non-reserved shortcuts without capturing Page Up/Down.

**Verification:**

- [x] Unit tests cover first/last measure clamping and representative score-scene primitives.
- [ ] Manual comparison using `grand-staff-demo.musicxml` and `musescore-export.musicxml`.

- **Dependencies:** Tasks 11 and 14
- **Files likely touched:** `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`, `PianoMapper.Web/Components/ScoreControls.razor`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`
- **Estimated scope:** Medium

#### Task 16: Schedule score playback on the Web Audio clock

**Description:** Convert core `ScheduledScoreEvent` values into one browser audio schedule, maintain a shared clock anchor, and derive the rendered cursor from that anchor.

**Recorded background behavior (2026-07-15):** Score audio remains scheduled on the Web Audio clock while the tab is hidden. UI rendering may throttle; on the next foreground frame the cursor is recomputed from `AudioContext.currentTime` and catches up rather than replaying delayed render-frame events.

**Acceptance criteria:**

- [x] Score events are scheduled ahead on `AudioContext.currentTime` rather than dispatched from render-frame polling.
- [x] Restart stops the prior score, clears scheduled nodes, and starts from a new anchor.
- [x] Cursor and audible events remain synchronized after normal UI frame delays.

**Verification:**

- [x] Unit tests with a fake audio clock verify event offsets, restart, completion, and cursor mapping.
- [ ] Manual browser check at slow and fast fixture tempos.
- [x] Background/foreground check documents expected behavior and confirms the cursor catches up after visibility restoration.

- **Dependencies:** Tasks 10, 11, and 15
- **Files likely touched:** `PianoMapper.Web/Playback/BrowserScorePlayback.cs`, `PianoMapper.Web/Audio/WebAudioSession.cs`, `PianoMapper.Web/wwwroot/js/audio.js`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/BrowserScorePlaybackTests.cs`
- **Estimated scope:** Medium

### Checkpoint B: Score Slice Complete

- [x] Existing desktop tests, build, MusicXML loading, and score playback remain green.
- [x] Browser file selection -> score rendering -> Web Audio playback -> cursor works end to end.
- [x] Measure navigation uses visible controls and `[`/`]`, not browser navigation keys.
- [x] Published browser build succeeds before practice work begins.
- [ ] Human review confirms score rendering and timing are sufficiently close to desktop behavior.

### Phase 4: Practice and Remaining Current Features

#### Task 17: Extract grading contracts

**Description:** Move the grading result/value types into core without changing their semantics or presentation-independent responsibilities.

**Acceptance criteria:**

- [x] Verdict values and typed summaries remain unchanged.
- [x] Core grading contracts contain no rendering colors or UI text.
- [x] Existing grading tests compile against core.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GraderTests"`

- **Dependencies:** Tasks 2 and 10
- **Files likely touched:** `PianoMapper/Practice/Verdict.cs`, `PianoMapper/Practice/GradingOptions.cs`, `PianoMapper/Practice/GradedEvent.cs`, `PianoMapper/Practice/GradingResult.cs`, `PianoMapper/Practice/GradingSummary.cs` and their destinations under `PianoMapper.Core/`
- **Estimated scope:** Medium

#### Task 18: Extract the grader and practice-session state machine

**Description:** Move `Grader`, `PracticeSessionState`, and `PracticeSession` into core while retaining `TimeProvider` injection and deterministic tests.

**Acceptance criteria:**

- [x] Count-in, running, finished, abort, timing tolerance, and visible-verdict behavior remain identical.
- [x] Tests use `FakeTimeProvider`; no sleep or browser clock is required by core tests.
- [x] Desktop practice mode continues to consume the same state machine.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GraderTests|FullyQualifiedName~PracticeSessionTests"`
- [ ] Manual desktop practice smoke using a supported fixture.

- **Dependencies:** Tasks 8, 10, and 17
- **Files likely touched:** `PianoMapper/Practice/Grader.cs`, `PianoMapper/Practice/PracticeSessionState.cs`, `PianoMapper/Practice/PracticeSession.cs`, `PianoMapper.Tests/UnitTests/GraderTests.cs`, `PianoMapper.Tests/UnitTests/PracticeSessionTests.cs` and corresponding core destinations
- **Estimated scope:** Medium

#### Task 19: Add the complete browser practice flow

**Description:** Coordinate count-in clicks, performed-note capture, grading updates, verdict coloring, abort/retry controls, and the final summary in the browser host.

**Recorded visibility behavior (2026-07-15):** Active practice is foreground-only. Blur or `document.hidden` aborts the session and releases all sounding/scheduled notes; retry starts from a fresh mapped Web Audio anchor.

**Acceptance criteria:**

- [x] Practice start/retry uses the visible button or `T`; abort uses the visible button or `C`.
- [x] Count-in and grading use the same mapped browser/audio clock as note input.
- [x] Correct, wrong, early/late, duration, missed, and extra results appear with the current summary counts.
- [x] Visibility loss follows the foreground-practice decision recorded before this task and never leaves sounding notes or silently continues with an undefined clock state.

**Verification:**

- [x] Coordinator unit tests use fake time and fake audio, including abort and retry.
- [ ] Manual practice run verifies one correct note and representative incorrect timing/pitch cases.
- [x] Reserved-key regression check from Task 6 still passes.

- **Dependencies:** Tasks 6, 16, and 18
- **Files likely touched:** `PianoMapper.Web/Practice/BrowserPracticeCoordinator.cs`, `PianoMapper.Web/Components/PracticePanel.razor`, `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/BrowserPracticeCoordinatorTests.cs`
- **Estimated scope:** Medium

### Checkpoint: Practice Slice Complete

- [ ] Desktop grading and practice tests plus a desktop practice smoke remain green.
- [x] Browser count-in -> performed notes -> verdicts -> summary works end to end with fake-clock coverage for abort and retry.
- [x] `T` and `C` apply only while the play surface is focused; native Enter/Escape/Space behavior remains unchanged.
- [ ] The approved visibility-loss behavior is exercised manually.

#### Task 20: Add the scrolling browser piano roll

**Description:** Render the already-shared `PianoRollLayout` values from Task 12 through the batched canvas module and add the view control plus `V` shortcut.

**Acceptance criteria:**

- [x] Open notes grow, released notes stop growing, and expired notes leave the visible window identically to desktop layout rules.
- [x] `V` and the visible control switch between views without using Tab.
- [x] View switching does not interrupt active notes, playback, or practice state.

**Verification:**

- [x] Existing `PianoRollLayoutTests` pass from core.
- [x] Browser scene tests cover open, released, expired, low, and high notes.
- [ ] Manual view-switch check during a sustained chord and score playback.

- **Dependencies:** Tasks 6, 12, and 14
- **Files likely touched:** `PianoMapper.Web/Rendering/PianoRollSceneBuilder.cs`, `PianoMapper.Web/Components/PianoCanvas.razor`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Tests/UnitTests/PianoRollLayoutTests.cs`
- **Estimated scope:** Medium

#### Task 21: Add random-measure playback

**Description:** Move random-measure composition into core and expose it through the visible UI and `M` shortcut using the browser audio scheduler. Keep desktop PCM natural-decay clamping in the desktop audio host; the browser schedules the same rhythmic duration and applies its own Web Audio envelope without moving `Instrument` or PCM policy into Core.

**Recorded browser envelope policy (2026-07-15):** Random-measure events retain their full rhythmic duration. The Web Audio adapter applies its existing 80 ms release envelope; it does not import Desktop PCM natural-decay clamping into Core.

**Acceptance criteria:**

- [x] Generated meter, tempo, pitch selection, and note values match current deterministic behavior.
- [x] Core owns rhythmic duration only; desktop `GetRandomMeasureEventDuration` behavior remains covered, and the browser's audible cap is defined by its approved Web Audio envelope.
- [x] Starting a generated measure does not corrupt loaded-score or practice state.
- [x] The displayed meter/tempo updates to the generated measure.

**Verification:**

- [x] Existing deterministic `RandomMeasureComposerTests` pass against core.
- [ ] Manual browser check: generate and play multiple measures, then resume loaded-score playback.

- **Dependencies:** Tasks 8 and 16
- **Files likely touched:** `PianoMapper/Music/RandomMeasure.cs`, `PianoMapper/Music/RandomMeasureEvent.cs`, `PianoMapper/Music/RandomMeasureComposer.cs`, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Tests/UnitTests/RandomMeasureComposerTests.cs` and corresponding core destinations
- **Estimated scope:** Medium

#### Task 22: Add the browser oscilloscope

**Description:** Read time-domain samples from the Web Audio analyser and draw a throttled oscilloscope frame without copying full audio buffers through .NET on every animation frame.

**Acceptance criteria:**

- [x] The scope reflects the actual mixed browser output while notes are audible.
- [x] Analysis stops or idles when audio is inactive and releases animation handles on disposal.
- [x] Rendering remains responsive during polyphonic input and score playback.

**Verification:**

- [ ] Manual performance profile confirms no unbounded allocations/listeners and stable interaction responsiveness.
- [ ] Manual check across note-on, note-off, clear, score playback, and view switching.

- **Dependencies:** Tasks 5 and 14
- **Files likely touched:** `PianoMapper.Web/Audio/WebAudioSession.cs`, `PianoMapper.Web/wwwroot/js/audio.js`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Web/Components/PianoCanvas.razor`
- **Estimated scope:** Medium

#### Task 23: Add the browser spectrum

**Description:** Use browser analyser frequency data and the existing normalized spectrum-bar layout to render the spectrum panel beside the oscilloscope.

**Acceptance criteria:**

- [x] Frequency bars are bounded and normalized under silence, single notes, and chords.
- [x] The browser host does not run both `AnalyserNode` FFT and the desktop PCM FFT for the same frame.
- [x] Spectrum work shares the oscilloscope animation lifecycle and stops cleanly.

**Verification:**

- [x] Existing `SpectrumLayoutTests` pass after moving the layout to core if required.
- [ ] Manual browser comparison for low and high notes confirms the dominant band moves as expected.

- **Dependencies:** Task 22
- **Files likely touched:** `PianoMapper/Rendering/SpectrumLayout.cs` and its destination under `PianoMapper.Core/`, `PianoMapper.Web/Rendering/SpectrumSceneBuilder.cs`, `PianoMapper.Web/wwwroot/js/audio.js`, `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Tests/UnitTests/SpectrumLayoutTests.cs`
- **Estimated scope:** Medium

### Checkpoint C: Current Feature Parity

- [x] Desktop and browser builds pass together.
- [ ] Live staff, loaded score, score playback, practice, piano roll, random measure, oscilloscope, and spectrum work in one browser session.
- [x] No feature depends on OpenTK/OpenAL from `PianoMapper.Web` or browser types from `PianoMapper.Core`.
- [x] Audio/canvas profiling shows no growing node, listener, timer, or animation-frame leaks.
- [ ] Human review accepts functional parity before packaging work.

### Phase 5: Packaging, Browser Hardening, and Documentation

#### Task 24: Enable published PWA and offline static hosting

**Description:** Add the manifest, icons, service-worker assets, and published cache behavior after the feature set stabilizes.

**Acceptance criteria:**

- [x] Release publish output is self-contained static content and requires no ASP.NET server process.
- [x] HTTPS hosting enables audio/PWA requirements, and first-load failures produce a clear message.
- [x] After one successful published load, the app starts offline with its packaged assets and fixtures are still loaded only from user selection.
- [ ] The chosen host's base path and SPA fallback/direct-navigation behavior are verified; service-worker asset-manifest generation is configured in the web project.

**Verification:**

- [x] `rtk dotnet publish PianoMapper.Web/PianoMapper.Web.csproj --configuration Release`
- [x] Serve the publish directory over HTTPS and verify offline reload; verify installation only in browsers that expose PWA installation.
- [ ] Verify a new publish activates predictably after closing prior tabs.

- **Dependencies:** Checkpoint C
- **Files likely touched:** `PianoMapper.Web/PianoMapper.Web.csproj`, `PianoMapper.Web/wwwroot/manifest.webmanifest`, `PianoMapper.Web/wwwroot/service-worker.js`, `PianoMapper.Web/wwwroot/service-worker.published.js`, `PianoMapper.Web/wwwroot/index.html` and icon assets
- **Estimated scope:** Medium

#### Task 25: Harden focus, accessibility, and the supported browser matrix

**Description:** Validate keyboard focus, visible labels, error states, resizing, audio unlock, and lifecycle behavior across supported desktop browsers without adding browser-specific feature scope.

**Acceptance criteria:**

- [ ] Current stable desktop Chrome, Edge, Firefox, and macOS Safari complete the core keyboard/audio/score/practice flow, subject to the recorded Safari test-access decision.
- [x] Every visible control has a label, focus indicator, and native keyboard activation.
- [x] Reserved browser keys and form-field typing remain unaffected in every tested browser.

**Verification:**

- [ ] Run the manual browser matrix for audio unlock, polyphony, blur cleanup, file import, playback, practice, resize, and offline start.
- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

- **Dependencies:** Task 24
- **Files likely touched:** `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Web/Layout/MainLayout.razor`, `PianoMapper.Web/wwwroot/css/app.css`, `PianoMapper.Web/wwwroot/js/keyboard.js`, `docs/browser-test-matrix.md`
- **Estimated scope:** Medium

#### Task 26: Update user documentation and run final verification

**Description:** Document desktop versus browser run commands, browser key bindings, MusicXML selection, audio unlock, HTTPS/static hosting, PWA updates, offline behavior, and known exclusions.

**Acceptance criteria:**

- [x] README instructions match actual project names, commands, controls, and browser behavior.
- [x] Remote-use guidance explains that browser audio/input execute locally and no VNC/audio stream is required for the web host.
- [x] Web MIDI, touch piano, backend sync, and unsupported MusicXML features remain clearly identified as out of scope.

**Verification:**

- [ ] Execute every documented build/run/publish command exactly as written.
- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`
- [x] `rtk dotnet publish PianoMapper.Web/PianoMapper.Web.csproj --configuration Release`
- [x] `rtk git diff --check`

- **Dependencies:** Task 25
- **Files likely touched:** `README.md`, `docs/remote-access.md`
- **Estimated scope:** Small

## Final Checkpoint

**Final automated evidence (2026-07-15):** Release tests passed 307/307 with zero warnings; the five-project Release build and Web publish completed with zero errors or warnings. Headless Chromium passed the complete input/audio/canvas score slice, analyser/view/random/practice lifecycle checks, focus-loss separation for score versus practice, and published HTTPS offline reload. `rtk git diff --check` passed.

- [ ] All overall acceptance criteria are met.
- [ ] Existing desktop behavior and shortcuts have not changed.
- [x] All automated tests pass in a clean Release build.
- [ ] Published browser output passes the supported-browser and offline checklist.
- [x] No browser-reserved key is globally intercepted.
- [x] No unrequested Web MIDI, backend, mobile keyboard, or MusicXML expansion has entered scope.
- [x] Documentation matches the verified implementation.
- [ ] Changes are ready for implementation review and normal PR review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Browser event/audio latency is too high for practice grading | High | Prove and measure the note path at Checkpoint A; timestamp in JS and schedule on the audio clock before broad extraction |
| A JS -> .NET -> JS note path adds avoidable latency | High | Measure both legs at Task 6; if the approved target is missed, install/refresh the C#-generated key map at initialization and octave changes, then dispatch audio directly inside the JS module while reporting the same mapped event to .NET |
| Audio autoplay policy leaves the app silent | High | Require an explicit Enable Audio action, expose context state, and provide a retry path |
| Core extraction accidentally changes desktop behavior | High | Move one dependency slice at a time, preserve namespaces, run focused tests plus desktop smoke checks at each checkpoint |
| Browser and audio clocks use different origins | High | Capture a `performance.now()`/`AudioContext.currentTime` anchor pair in JS, map event timestamps before interop, and unit-test the conversion with fixed anchors |
| Canvas/JS interop becomes a per-primitive bottleneck | Medium | Build complete C# scenes and send one batched render payload per frame |
| Background-tab throttling disrupts cursors or practice | Medium | Schedule audio ahead, derive UI from the shared anchor after resume, and document foreground-only practice expectations if needed |
| A selected MusicXML file consumes excessive browser memory | Medium | Set one approved `IBrowserFile.OpenReadStream` limit, show it in the picker, and reject before parsing |
| PWA caching serves an old assembly set | Medium | Enable PWA late, test published service-worker activation, and document close/reopen update behavior |
| Browser key handlers interfere with normal navigation or forms | Medium | Scope listeners to the play surface, ignore form targets, use the explicit non-reserved map, and regression-test reserved keys |
| JS audio/canvas lifecycle leaks nodes, listeners, or animation frames | Medium | Centralize ownership/disposal and profile repeated start/stop/navigation cycles before packaging |
| Browser differences cause isolated failures | Medium | Maintain a small explicit desktop-browser matrix and feature-detect recoverable capabilities |

## Checkpoint Decisions and Open Questions

These questions do not block planning, but they should be answered at the indicated checkpoint rather than silently assumed during implementation:

1. **Latency target before Task 6:** Are median <= 20 ms and p95 <= 50 ms for browser-event-to-audio-schedule delay the approved go/no-go thresholds on the primary laptop/browser, or should Checkpoint A use different measured limits?
2. **Audio fidelity at Checkpoint A:** Is equivalent piano-style synthesis acceptable, or must the web host reproduce the desktop PCM timbre closely enough for an A/B match?
3. **MusicXML size before Task 11:** Resolved provisionally at 10 MiB per selected file; the browser rejects larger files before opening the stream and shows the limit in the picker.
4. **Practice visibility before Task 19:** Resolved provisionally as abort-on-blur/visibility-loss with immediate audio cleanup and retry from a fresh clock anchor.
5. **Primary HTTPS host before Task 24:** Still open. Packaging is host-neutral and verified from local HTTPS static hosting; remote base-path and SPA-fallback behavior must be checked on the chosen host.
6. **Safari test access before Task 25:** Which macOS device or external tester will provide the Safari verification pass?
7. **Post-release scope:** Should Web MIDI and a touch piano be separate follow-up plans after browser parity is stable?

## Parallelization Guidance

- After Task 1, live-domain extraction (Tasks 2-3) and the empty Web shell (Task 4) may proceed in parallel with explicit solution/project-file ownership; Tasks 5-7 then converge sequentially at Checkpoint A.
- After Checkpoint A, rhythm/score/import work (Tasks 8-11) and rolling-window extraction (Task 12) may proceed in parallel; grand-staff extraction waits for Tasks 9 and 12.
- After Checkpoint B, grading/practice extraction (Tasks 17-19) can proceed in parallel with piano-roll work (Task 20) only if canvas/page ownership is assigned; random playback (Task 21) also depends only on the established scheduler and rhythm contracts.
- Analyzer visualizations (Tasks 22-23) remain sequential because they share one JavaScript audio/animation lifecycle.
- PWA packaging begins only after feature parity to avoid service-worker caching during active implementation.
