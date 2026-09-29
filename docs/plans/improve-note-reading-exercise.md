# Plan: Improve the Note-Reading Exercise

**Date:** 2026-09-25

**Goal:** Turn the current generated pitch drill into a reliable, reviewable, progressively harder, hardware-accessible, and adaptive note-reading learning loop without regressing imported-score idle checking or practice grading.

## Context

The current browser exercise is a coherent first slice:

- `SightReadingExercisePanel.razor` selects treble/bass, a five-note or one-octave range, and 8 or 16 notes.
- `SightReadingExerciseComposer` creates balanced natural-note shuffle bags, avoids adjacent repeats, and emits fixed quarter notes in 4/4 at 120 BPM.
- `Piano.razor` always runs generated exercises in `NoteReadingMode.PitchAndOrder`, routes USB MIDI note events into `NoteReadingSession`, hides labels and fingerings, and highlights the next expected note.
- `NoteReadingSession` already supports chords, holds, ties, overlapping voices, and rhythm verdicts, but the generated exercise does not expose those capabilities.
- The completion UI reports only aggregate first-try accuracy, total mistakes, and elapsed time. It does not preserve wrong played pitches, per-prompt attempts, or a reviewable mistake map.
- Browser note input is USB MIDI-only. The rendered 88-key piano is display-only.
- Exercise fields and lifecycle methods are spread through the 2,400-line `Piano.razor` page.

The focused baseline is healthy: 30 `NoteReadingSessionTests` and 4 `SightReadingExerciseComposerTests` pass. The plan preserves that behavior while adding vertical, independently reviewable slices.

## Request and success criteria

Implement the full improvement roadmap identified in the note-reading audit:

1. Clean exercise transitions and harden session edge cases.
2. Preserve per-prompt attempt information and provide a meaningful completion review.
3. Add retry-missed, next-exercise, history, mastery, and adaptive generation.
4. Add staged pitch, accidental, key-signature, chord, grand-staff, duration, rest, and rhythm curricula.
5. Expose hold and rhythm modes with an explicit audio-clock count-in anchor.
6. Make the exercise usable without USB MIDI through the on-screen piano and optional computer-key input.
7. Extract exercise orchestration from `Piano.razor` and cover the workflow with focused tests.

The completed feature must keep active-exercise answers hidden, reveal useful review information only after completion, remain deterministic when a seeded `Random` is supplied, and preserve imported-score idle note checking.

## Scope assumptions

These are the default decisions for implementation. Change them during plan review rather than silently choosing different behavior mid-implementation.

- Browser-local history is sufficient. Accounts, PostgreSQL history, and cross-device synchronization are out of scope.
- Keep the current 8- and 16-prompt choices, but model them as prompt counts rather than measure counts.
- Exercise elapsed time begins with the first played attempt, not when the score is generated. It freezes on completion and resets on retry.
- Generate/retry/end transitions release tracked browser notes and clear browser audio so stale held notes cannot leak across sessions.
- Generate suggested fingerings with the existing `ScoreFingeringGenerator`; hide them during the active exercise and reveal them only in review.
- The on-screen piano is the primary no-hardware fallback. Computer-key note input is included as an opt-in follow-up so existing browser shortcuts retain priority.
- Use local storage with a versioned schema and retain the most recent 100 completed sessions.
- Do not introduce a component-test package initially. Put workflow logic in a testable coordinator, cover JavaScript modules with Node tests, and keep a concise manual browser checklist. Reconsider bUnit only if meaningful behavior remains trapped in Razor markup.
- Desktop exercise parity, sustain-pedal semantics, mobile-specific layout redesign, and MusicXML import changes are out of scope.

## Recommended curriculum defaults

These defaults make the curriculum tasks implementable while keeping the exact catalog easy to revise during review.

| Preset | Treble material | Bass material | Motion/content rule |
|---|---|---|---|
| Five note | C4-G4 | C3-G3 | Naturals; steps and thirds; preserve balanced coverage |
| One octave | C4-C5 | C3-C4 | Naturals; steps, thirds, and occasional wider skips |
| Ledger lines | A3-C6 | C2-E4 | Include at least one prompt above and below the normal staff in every complete exercise |
| G major | G4-G5 | G2-G3 | One sharp through key signature; no redundant written accidental |
| F major | F4-F5 | F2-F3 | One flat through key signature; no redundant written accidental |
| Grand staff | Treble and bass one-octave palettes | Both staves | Alternate single-note prompts before introducing simultaneous two-note prompts |
| Chords | I, IV, and V triads in the selected key | Selected or grand staff | At most three distinct pitches per prompt in the first chord preset |
| Basic rhythm | Current pitch palette | Current staff selection | 4/4 using half, quarter, paired eighth notes, and quarter rests |
| Compound rhythm | Current pitch palette | Current staff selection | 6/8 using dotted-quarter and grouped eighth-note patterns |

## Architecture decisions

- Keep `NoteReadingSession` as the shared score-event checker used by generated exercises and imported-score idle checking. Add generic prompt-result snapshots there; do not create a competing grading engine.
- Add a web-layer `SightReadingExerciseCoordinator` that owns exercise options, generated score, phase, retry/review state, and session summaries. It must not own Web Audio or DOM/JS interop.
- Keep audio, metronome, MIDI connection, rendering, and browser lifecycle in `Piano.razor`, but reduce the page to translating browser events into coordinator operations and rendering derived state.
- Introduce stable preset identifiers before persistence. Persist preset IDs as strings with a schema version; never persist enum ordinals.
- Keep `SightReadingExerciseComposer` deterministic through its supplied `Random`. Adaptive weights are explicit inputs, not hidden global state.
- Route MIDI, pointer, and computer-key notes through one source-neutral note start/release path so all inputs receive the same audio, timeline, exercise-checking, and cleanup behavior.
- Use the existing audio clock and `MetronomeGrid` for rhythm anchors. Wall-clock timestamps must not be mixed with Web Audio event times.

## Dependency graph

```text
Session edge behavior
    -> prompt result snapshots
        -> stable preset/options contract
            -> exercise coordinator
                -> clean lifecycle
                -> completion review -> retry missed
                -> session summary -> local history -> mastery/adaptation
                -> expanded pitch/key/chord/rhythm presets
                                      -> explicit rhythm anchor -> count-in UI

Source-neutral browser note input
    -> on-screen pointer/touch piano
    -> opt-in computer-key piano
    -> input readiness/status copy

All feature slices
    -> README + browser test matrix + full verification
```

## Implementation protocol

For every implementation task:

1. Load `code-standards`, `writing-tests`, and `tdd`; load `run-tests` before executing tests.
2. Use codegraph exploration/impact for the named symbols before editing.
3. Add or update a focused failing test before implementation when behavior changes.
4. Keep each task independently buildable and do not combine adjacent cleanup.
5. Prefix shell commands with `rtk` as required by `AGENTS.md`.

## Task summary

### Phase 1: Session and orchestration foundation

- [x] Task 1: Harden note-reading event boundaries.
- [x] Task 2: Add immutable per-prompt result snapshots and fair elapsed timing.
- [x] Task 3: Introduce the stable exercise preset/options contract.
- [x] Task 4: Extract the sight-reading exercise coordinator.
- [x] Task 5: Make generate, retry, and end transitions clean.

### Phase 2: Review and progress history

- [ ] Task 6: Add completion review and answer reveal. (fingering reveal + review data done; canvas review-color rendering deferred, see task notes)
- [x] Task 7: Add retry-missed and next-exercise actions.
- [x] Task 8: Define versioned session summaries and bounded history.
- [x] Task 9: Persist history in browser local storage.
- [x] Task 10: Display recent sessions and pitch mastery.

### Phase 3: Curriculum and rhythm

- [x] Task 11: Add constrained pitch-motion and ledger-line presets.
- [x] Task 12: Add accidental and key-signature presets.
- [x] Task 13: Add grand-staff and chord presets.
- [x] Task 14: Add duration and rest composition.
- [x] Task 15: Add an explicit rhythm-anchor contract.
- [x] Task 16: Expose hold/rhythm modes with count-in.

### Phase 4: Input accessibility

- [x] Task 17: Generalize browser note input behind a source-neutral API.
- [x] Task 18: Make the 88-key piano playable by pointer and touch.
- [x] Task 19: Add opt-in computer-key note input.
- [x] Task 20: Add clear input-readiness and MIDI connection guidance.

### Phase 5: Adaptation and handoff

- [x] Task 21: Weight new exercises from local mastery history.
- [x] Task 22: Update documentation and complete full verification.

## Detailed tasks

### Task 1: Harden note-reading event boundaries

**Description:** Lock down numerical and lifecycle edge behavior before extending the session. Replace exact onset grouping with the same explicit musical-time tolerance already used for overlapping pitches, while preserving truly adjacent events. Add the missing boundary cases from the audit.

**Acceptance criteria:**

- [x] Score events whose onsets differ by no more than the chosen `1e-9` beat tolerance form one prompt; events beyond it remain separate.
- [x] Exact onset-tolerance boundaries, adjacent same-pitch notes whose second onset equals the first end, 6/8, dotted values, and tuplets are covered by tests.
- [x] Invalid mode/tolerance, null or empty score, post-completion input, unknown release, idempotent `ReleaseAll`, and chord release/re-attack behavior have explicit expected tests.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests`
- [x] Confirm the original 30 session tests still pass unchanged unless a test is deliberately tightened.

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** S

### Task 2: Add immutable per-prompt result snapshots and fair elapsed timing

**Description:** Record enough information to review mistakes and build mastery without coupling the session to UI or persistence. Publish immutable snapshots in the same style as `Verdicts` and `ExpectedNotes`.

**Acceptance criteria:**

- [x] Each prompt result exposes prompt index/onset, expected source notes and pitches, wrong played pitches, wrong-attempt count, first-try outcome, and completion state/times.
- [x] Results work for single notes, chords, enharmonic input, hold/rhythm mistakes, ties, and overlapping voices without exposing mutable internal collections.
- [x] `ElapsedTime` remains zero until the first played attempt, advances while active, freezes at completion, and resets on retry; in-progress first-try metrics include a currently attempted prompt.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests`
- [x] Add a test proving callers cannot mutate published result state.

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Core/Practice/NoteReadingPromptResult.cs` (new)
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** M

### Task 3: Introduce the stable exercise preset/options contract

**Description:** Replace the narrow `SightReadingDifficulty`/measure-count contract with stable preset IDs and prompt counts before coordinator extraction or persistence. Preserve current behavior as `FiveNote` and `OneOctave`.

**Acceptance criteria:**

- [x] `SightReadingExerciseOptions` contains staff selection, a stable string-serializable preset ID, prompt count, and note-reading mode; the initial catalog contains behavior-equivalent five-note and one-octave presets.
- [x] Composition produces exactly the requested supported prompt count, validates invalid values, stays deterministic for a seeded `Random`, remains balanced, and avoids adjacent repetitions.
- [x] The old difficulty type is removed only after all callers/tests migrate; no enum ordinal becomes a persisted contract.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

**Dependencies:** Task 2

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Core/Music/SightReadingPresetId.cs` (new)
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Core/Music/SightReadingDifficulty.cs` (remove after migration)
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

### Checkpoint A: Domain contracts

- [x] Tasks 1-3 focused tests pass.
- [x] The solution builds in Release.
- [x] Existing five-note and one-octave seeded sequences retain their documented invariants.
- [ ] Review the prompt-result and preset contracts before web-layer work continues. (pending human/reviewer sign-off)

### Task 4: Extract the sight-reading exercise coordinator

**Description:** Move exercise-only options, score/session state, phase, metrics, and generate/retry/end decisions out of `Piano.razor`. Keep browser audio and rendering calls in the page.

**Acceptance criteria:**

- [x] A `SightReadingExerciseCoordinator` owns `Inactive`, `Active`, and `Review` phases, options, the generated score, `NoteReadingSession`, and all derived review data.
- [x] Generate uses an injected/supplied `Random`, Retry preserves the same score, and End clears exercise state; each transition has unit coverage.
- [x] `Piano.razor` uses coordinator state instead of separate sight-reading fields and does not duplicate phase/metric calculations.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseCoordinatorTests`
- [x] Run the composer and session focused tests from Tasks 1-3.

**Implementation note (2026-09-25, updated):** `SightReadingExerciseCoordinator` takes an externally-owned `NoteReadingSession` (constructor-injected, not created internally) because `Piano.razor`'s single `noteReadingSession` field is also reused for imported-score idle-note checking (`NotationScore => ... ?? selectedScore`), per this plan's architecture decision to keep `NoteReadingSession` a shared, generic grading engine — the fork identified in the first pass was resolved by keeping one shared session instance and having the coordinator drive it, rather than splitting into two sessions. `Piano.razor` now owns a `sightReadingExerciseCoordinator` field (constructed in the `Piano()` constructor, right after `noteReadingSession`) and every former `sightReadingStaff`/`sightReadingPresetId`/`sightReadingPromptCount`/`sightReadingExerciseScore` field was removed; `NotationScore`, `IsSightReadingExerciseActive`, the `<SightReadingExercisePanel>` bindings, the three `SetSightReading*` setters, and all 6 "exit exercise mode as a side effect" sites (`HandleScoreLoadedAsync`, `HandleScoreUnloadedAsync`, `ApplySelectedMeasureRangeAsync`, `StartScorePlaybackAsync`, `PlayRandomMeasureAsync`, `StartPracticeAsync`) now read/call the coordinator instead. The other ~40 read-only references to `IsSightReadingExerciseActive`/`noteReadingSession.*` needed no changes since they already went through those same computed properties. Verified with a fresh `dotnet clean` + `dotnet build` (0 errors) and the full test suite (766 passing) — no bUnit component tests were added, per this plan's explicit scope assumption to defer that.

**Dependencies:** Task 3

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs` (new)
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs` (new)

**Estimated scope:** M

### Task 5: Make generate, retry, and end transitions clean

**Description:** Ensure an exercise transition cannot inherit sounding audio, tracked MIDI notes, a partial chord, wrong-note overlays, playback, practice, or metronome state from the previous activity.

**Acceptance criteria:**

- [x] Generate, Retry, and End use one ordered transition helper: stop conflicting playback/practice/metronome activity, obtain the audio-clock time when available, clear tracked input/audio, clear overlays, then mutate coordinator state and redraw.
- [x] A held MIDI note before Generate/Retry cannot remain sounding or suppress the same pitch in the new exercise; its later stale note-off is harmless.
- [x] Transitions are safe before audio initialization and after MIDI disconnection.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~BrowserMidiInputStateTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`
- [ ] Manual: hold a MIDI key during Generate, Retry, and End; verify no stuck sound, stale highlight, or blocked re-attack. (NOT performed — no browser access in this session; needs human/browser-capable verification)

**Implementation note (2026-09-25):** Added `PrepareSightReadingExerciseTransitionAsync()` as the shared ordered helper in `Piano.razor`, called by `GenerateSightReadingExerciseAsync`, `RetrySightReadingExerciseAsync`, and `EndSightReadingExerciseAsync` before they mutate coordinator state. It mirrors the existing "full stop" idiom already used by `StartPracticeAsync` and `HandleClearCommandAsync` elsewhere in the page: `ExitFingeringEditMode()` → `AbortPracticeAsync` → `StopScorePlaybackAsync` → `visualizationTicker.StopAsync()` → conditional `StopMetronomeAsync()` → resolve the audio-clock time (`TimeSpan.Zero` when audio isn't initialized yet) → `midiInputState.Clear(currentTime)` → conditional `AudioSession.ClearAsync(currentTime)` (guarded on `AudioSession.IsInitialized`, since `WebAudioSession.ClearAsync` throws `InvalidOperationException` when called before init) → `wrongScoreInputNotes.Clear()`. Previously only Generate did any of this cleanup at all; Retry and End did nothing but reset the session, which was the actual bug (a held MIDI note survived Retry/End). `AbortPracticeAsync` and the metronome-stop calls are pre-existing no-ops when nothing is running, so calling them unconditionally from Retry/End for the first time carries no behavior risk. The "held note's later stale note-off is harmless" property comes from `BrowserMidiInputState.Clear` emptying its tracked-notes dictionary, so a subsequent genuine note-off for that key is looked up, not found, and treated as a no-op by `BrowserMidiInputState.Handle`.

**Dependencies:** Task 4

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Tests/UnitTests/BrowserMidiInputStateTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

### Checkpoint B: Reliable current exercise

- [x] Full .NET tests pass. (766 passing, fresh `dotnet clean` + `dotnet build` confirmed 0 errors/warnings)
- [ ] Current five-note/one-octave Generate, Retry, and End flow works in the browser. (NOT verified — no browser access in this session; code review + automated tests only)
- [x] Labels and fingerings remain hidden during active exercises. (behavior-preserving; `IsSightReadingExerciseActive` unchanged in meaning)
- [ ] No held-input state survives a transition. (fixed in code per Task 5; NOT manually verified in a browser)

### Task 6: Add completion review and answer reveal

**Description:** Turn completion into a learning step. Generate deterministic suggested fingerings, keep answers hidden during `Active`, then reveal names, fingerings, and a first-try mistake map during `Review`.

**Acceptance criteria:**

- [x] Active exercises still hide note names and fingerings; entering Review reveals both without regenerating or changing the score.
- [ ] Review colors/annotations distinguish first-try-correct prompts from prompts that needed retries, even though the final played pitch was correct. (data done, canvas-color rendering deferred — see note below)
- [x] The summary shows `first-try correct / attempted`, percentage, wrong attempts, and active elapsed time with unambiguous labels.

**Verification:**

- [x] Coordinator tests cover all-correct, wrong-then-correct, and chord prompt review maps. (chord case deferred to Task 13 — no chord preset exists yet to test against; `BuildReviewFirstTryMap` is chord-agnostic by construction, see note)
- [x] Existing `GrandStaffSceneBuilderTests` pass. (134 passing)
- [ ] Manual: verify label/fingering lanes at the smallest supported canvas size and with ledger-line presets when those become available. (NOT performed — no browser access)

**Implementation note (2026-09-25):** Split this task into a safe/tested part and a deferred design fork.

Done: `SightReadingExerciseCoordinator.Generate` now runs the composed score through the existing `ScoreFingeringGenerator.Generate` before storing it, so `Score` always carries fingerings from the moment it's generated — Review doesn't regenerate or mutate the score, it only changes what's rendered. Added `Piano.razor`'s `IsSightReadingExerciseInProgress` (true only in the `Active` phase, unlike `IsSightReadingExerciseActive` which stays true through `Review`) and rewired the two `BuildScore` call sites in `RefreshCanvasScene` to gate `showNoteLabels`/`showFingerings` on it instead, so labels and fingerings reveal automatically once the session completes. Added `SightReadingExerciseCoordinator.BuildReviewFirstTryMap()` — a `ScoreNote -> bool` map built from `NoteReadingSession.PromptResults` (from Task 2), fully unit tested for all-correct, wrong-then-correct, and in-progress cases. It is chord-agnostic by construction (every note in a prompt's `ExpectedSourceNotes` gets that prompt's single `IsFirstTryCorrect` value), so it will handle chords correctly once Task 13 adds a chord preset — no chord-specific test exists yet because there's nothing chord-shaped to generate against, and adding one now would be a speculative test against behavior that doesn't exist. Panel summary text now reads "`X of Y first-try correct (Z%)`" using the session's own `FirstTryCorrectCount`.

Deferred: making the review distinction (first-try-correct vs. needed-a-retry) visible as a *canvas color/annotation* on the notation itself. The existing rendering pipeline colors notes via an `IReadOnlyDictionary<ScoreNote, Verdict>` passed into `GrandStaffSceneBuilder`/the scene caches, where `Verdict` values (`Correct`, `WrongPitch`, `Early`, `Late`, `TooShort`, `TooLong`, ...) already carry established meanings tied to *live* performance grading, several of which also drive human-readable status text elsewhere in `Piano.razor` (`GetNoteReadingStatus`/`GetNoteReadingReleaseStatus`). None of the existing verdicts mean "correct, but took more than one attempt," and reusing one (e.g. `Late`) purely for its color would risk surfacing timing-specific copy or styling for what is actually a pitch-only exercise — a real correctness/UX bug I can't rule out without seeing it render. The right fix is either a new `Verdict`-like concept scoped to review-only rendering, or a parallel annotation channel in the scene builder, and either one is a rendering-pipeline design decision I'm not comfortable making blind, especially given how many past lessons in `.agents/lessons.md` are about subtle rendering/annotation placement bugs that were only caught by looking at the actual canvas. The data needed to build it (`BuildReviewFirstTryMap`) is ready and tested; recommend a follow-up pass with browser access to design and verify the actual visual treatment.

**Dependencies:** Task 5

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

### Task 7: Add retry-missed and next-exercise actions

**Description:** Let a learner immediately act on the review instead of manually rebuilding an exercise. Retry-missed creates an exercise only from prompts that were not first-try correct; Next generates a fresh score with the same options.

**Acceptance criteria:**

- [x] `Retry missed notes` is enabled only when Review contains missed prompts and preserves prompt/chord membership while compacting them into a valid score.
- [x] `Next exercise` keeps current options but uses a fresh random sequence; `Retry same notes` remains exact.
- [x] All three actions start from the clean transition defined in Task 5 and reset attempt metrics.

**Verification:**

- [x] Composer tests cover review-score composition for single notes and chords. (chord grouping covered via multi-note prompt groups sharing one onset; no chord *preset* exists yet — see Task 6 note, same reasoning applies)
- [x] Coordinator tests cover button availability and score identity/difference semantics.
- [ ] Manual: complete an exercise with mixed results and exercise all three follow-up paths. (NOT performed — no browser access)

**Implementation note (2026-09-25):** `SightReadingExerciseComposer.ComposeFromMissedPrompts(originalScore, missedPromptGroups)` compacts missed prompts (each an `IReadOnlyList<ScoreNote>`, so a chord's notes travel together) into sequential fixed quarter notes reusing the same measure-packing scheme as `Compose`; it deliberately ignores the original notes' stale `MeasureIndex`/`BeatOffset`/`TiesToNext` and recomputes them, verified by a test that seeds those fields with wrong values to prove they're discarded, not copied. The last measure may end up short (e.g. 3 notes in a 4/4 measure) when the missed-prompt count isn't a multiple of 4 — rests to pad it are explicitly out of scope until Task 14, and nothing in the grading or derivation pipeline requires full measures, so this is a data-correctness non-issue, only a possible cosmetic one I can't see without a browser. `SightReadingExerciseCoordinator.HasMissedPrompts` and `RetryMissed(tolerance)` build on `BuildReviewFirstTryMap`'s underlying data (`NoteReadingSession.PromptResults`) and regenerate fingerings for the compacted score exactly like `Generate` does. For "Next exercise," I reused the existing `Generate` action/button rather than adding a second, functionally-identical button — the panel's primary button now reads "Next exercise" once the session is complete and "Generate new exercise" otherwise, since both cases call the exact same coordinator method with the exact same semantics (fresh random sequence, current options); a separate button doing the same thing seemed like avoidable UI clutter rather than a meaningfully different action.

**Dependencies:** Task 6

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

### Task 8: Define versioned session summaries and bounded history

**Description:** Convert live prompt results into a small serializable record suitable for local history and adaptation, without serializing full `Score` object graphs or mutable session objects.

**Acceptance criteria:**

- [x] A summary stores schema version, completion timestamp, preset ID, staff/mode, prompt count, elapsed time, aggregate metrics, and per-pitch correct/attempt counts.
- [x] A pure bounded-history type orders newest first, retains at most 100 valid summaries, and computes mastery without dividing by zero.
- [x] Unknown future preset IDs or malformed entries can be skipped without losing valid history entries.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingHistoryTests` (also added `SightReadingSessionSummaryTests` for the summary type itself, per the `writing-tests` skill's "one test class per production class" convention — run together via `FullyQualifiedName~SightReading` if needed)
- [x] Round-trip representative summaries through `System.Text.Json` in tests.

**Implementation note (2026-09-25):** `SightReadingSessionSummary` (`PianoMapper.Core/Practice/`) stores `PresetId` as a plain `string` (the enum member name, e.g. `"FiveNote"`) rather than the `SightReadingPresetId` enum — this is deliberate and matches the acceptance criterion: a `string` field can always be parsed even when it holds a preset id from a future schema version, whereas an enum-typed field cannot represent an unrecognized value at all. `Staff`/`NoteReadingMode` stayed as their native enums since they're stable core types, not the extensible catalog the plan calls out; both serialize as camelCase strings (never ordinals) via a shared `JsonStringEnumConverter`. `SightReadingHistory.FromJson` parses the JSON array one `JsonElement` at a time and try/catches each entry's deserialization individually (plus rejects unrecognized `SchemaVersion` values) so one corrupt or future-versioned array element is skipped without discarding the rest of the array — a single `JsonSerializer.Deserialize<List<T>>` call would have failed the whole array on one bad element, which is why it's implemented this way instead. `SightReadingSessionSummary.Create(...)` converts live `NoteReadingPromptResult`s (from Task 2) into a summary; nothing yet calls it from the coordinator — that wiring is Task 9/10's job once local storage exists to write to.

**Dependencies:** Tasks 3 and 6

**Files likely touched:**

- `PianoMapper.Core/Practice/SightReadingSessionSummary.cs` (new)
- `PianoMapper.Core/Practice/SightReadingHistory.cs` (new)
- `PianoMapper.Tests/UnitTests/SightReadingHistoryTests.cs` (new)

**Estimated scope:** M

### Task 9: Persist history in browser local storage

**Description:** Add a narrow web adapter and JavaScript module that loads/saves the versioned bounded history. Storage failure must not prevent practicing.

**Acceptance criteria:**

- [x] The adapter loads once, saves after a newly completed exercise exactly once, and uses a namespaced/versioned local-storage key.
- [x] Missing, corrupt, disabled, or quota-limited local storage yields an empty/in-memory history plus a non-fatal status; exercise completion still succeeds.
- [x] JavaScript tests cover read, write, missing value, corrupt JSON, and storage exceptions.

**Verification:**

- [x] `rtk node --test PianoMapper.Tests/JavaScript/sight-reading-history.test.mjs` (7 tests)
- [x] Run `SightReadingHistoryTests` and coordinator tests.

**Implementation note (2026-09-25):** `sight-reading-history.js` deliberately does *no* JSON parsing/validation of its own — it's a thin get/set/remove wrapper around a bare `localStorage` reference (matching the existing `keyboard.js`/`document` convention: reference the global directly, let tests stub `globalThis.localStorage`), returning `{ json, isAvailable }`/`{ isAvailable }` so a thrown access error (disabled storage, quota) degrades to `isAvailable: false` instead of propagating. All JSON validation (including "corrupt/malformed entry" tolerance) lives in `SightReadingHistory.FromJson` from Task 8, which already handles it per-entry. `BrowserSightReadingHistoryStore` (`PianoMapper.Web/Practice/`) wraps the module the same way `WebAudioSession` wraps `audio.js` (lazy `IJSObjectReference` import, `catch (JSException)` around each call) and has no unit tests of its own — consistent with `WebAudioSession` itself having none; this codebase's established boundary is JS-behavior tests in Node and adapter plumbing verified by code review plus the manual browser matrix, not by mocking `IJSRuntime`. "Save exactly once per completion" required a stateful gate: `SightReadingExerciseCoordinator.ConsumeCompletionSummary()` returns a summary only on the first call after the session reaches `Review`, and `null` on every call after that until the next Generate/Retry/RetryMissed resets the gate (this needed a `TimeProvider` added to the coordinator's constructor, for the summary's real wall-clock `CompletedAt`, since `NoteReadingSession`'s own `TimeProvider` is audio/exercise-relative, not a calendar timestamp — kept optional with a `TimeProvider.System` default, matching `NoteReadingSession`'s own constructor convention). `Piano.razor` calls it unconditionally after every `Check`/`Release` that idle-checking handles, in both `HandleNoteOnCommandAsync` and `HandleNoteOffCommandAsync` — harmless when nothing just completed, and correct for a future hold/rhythm mode (Task 16) where completion can happen on release rather than on-check.

**Dependencies:** Task 8

**Files likely touched:**

- `PianoMapper.Web/Practice/BrowserSightReadingHistoryStore.cs` (new)
- `PianoMapper.Web/wwwroot/js/sight-reading-history.js` (new)
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/JavaScript/sight-reading-history.test.mjs` (new)

**Estimated scope:** M

### Task 10: Display recent sessions and pitch mastery

**Description:** Provide a compact progress view without turning the main page into a dashboard. Show recent outcomes and weak pitches derived from the bounded history.

**Acceptance criteria:**

- [x] The history panel shows the most recent sessions with date, preset/staff, first-try fraction, mistakes, and elapsed time.
- [x] A mastery section shows pitches with enough attempts, sorted weakest first, and clearly distinguishes insufficient data.
- [x] The panel has an explicit, confirmed `Clear local history` action that does not affect saved scores or the current exercise.

**Verification:**

- [x] Pure history aggregation tests pass. (`ComputeMasteryWeakestFirst` — 3 new tests in `SightReadingHistoryTests`)
- [ ] Manual: verify empty, one-session, many-session, and cleared-history states with keyboard navigation. (NOT performed — no browser access)

**Implementation note (2026-09-25):** Added `SightReadingHistory.MinimumMasteryAttempts` (5) and `ComputeMasteryWeakestFirst()` (filters to pitches meeting that threshold, sorts by accuracy ascending then by MIDI number for deterministic tie-breaking) on top of Task 8's `ComputeMastery()`. New `SightReadingHistoryPanel.razor` is presentational like `SightReadingExercisePanel.razor` (`RecentSessions`, `WeakestPitches`, `InsufficientDataPitchCount`, `ClearHistory` parameters), added to `Piano.razor` right after the exercise panel; `RecentSightReadingSessions` caps the display to the 10 newest entries (history itself still holds up to 100), and `InsufficientMasteryDataPitchCount` is `ComputeMastery()` minus what clears the threshold, so the panel can say "N more pitches need more attempts" without a third Core method. For the confirmed clear action, the panel itself calls the browser's native `confirm()` (`IJSRuntime.InvokeAsync<bool>("confirm", message)`, a global JS function, not a new module) before invoking the `ClearHistory` callback — kept the confirm step inside the panel rather than in `Piano.razor` since it's a self-contained interaction with no dependency on the page's audio/MIDI state. `ClearSightReadingHistoryAsync` in `Piano.razor` only touches `sightReadingHistory`/`sightReadingHistoryStore`, never `selectedScore`/saved scores/the active coordinator, so it can't affect a running exercise or the score library. One thing I deliberately did NOT do: the two new list elements (`history-session-list`, `history-mastery-list`) have no CSS rules of their own — they inherit the existing `.control-card`/`.control-note` styling for everything else, but will render as plain default-bulleted lists until someone adds matching CSS. I didn't guess at styling blind; recommend a quick visual pass alongside the other manual checks.

**Dependencies:** Task 9

**Files likely touched:**

- `PianoMapper.Web/Components/SightReadingHistoryPanel.razor` (new)
- `PianoMapper.Web/Practice/BrowserSightReadingHistoryStore.cs`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingHistoryTests.cs`

**Estimated scope:** M

### Checkpoint C: Complete learning loop

- [x] Tasks 6-10 focused tests and JavaScript tests pass. (801 .NET tests, 65 JavaScript tests, all passing; Release build 0 errors/0 warnings)
- [x] A session can be completed, reviewed, retried, persisted, reloaded, displayed, and cleared. (verified by code path/unit tests; NOT exercised end-to-end in a real browser — see outstanding manual items below)
- [x] Active answers remain hidden; Review answers are legible and accurately reflect first-try outcomes. (data/state verified by tests; the review *color* distinction on the canvas was explicitly deferred in Task 6's notes — legibility of labels/fingerings text itself relies on existing, already-shipped rendering)
- [ ] Review the summary schema before curriculum fields expand. (pending human/reviewer sign-off — same as Checkpoint A's equivalent bullet)

**Outstanding manual-only items carried out of Phase 2** (none of these were performed — no browser access in this session): hold a MIDI key during Generate/Retry/End (Task 5); label/fingering lanes at smallest canvas size (Task 6); all three follow-up actions after a mixed-result exercise (Task 7); empty/one/many/cleared history states with keyboard navigation (Task 10); the review-color/annotation distinction deferred in Task 6 needs a design decision plus visual verification; the history panel's two new list elements need CSS.

### Task 11: Add constrained pitch-motion and ledger-line presets

**Description:** Extend the preset catalog with musically staged pitch ranges rather than merely increasing the bag size. Keep coverage balanced while constraining leaps.

**Acceptance criteria:**

- [x] Add the recommended five-note, one-octave, and ledger-line ranges for both staves, with explicit maximum diatonic-leap rules per preset.
- [x] Every complete ledger exercise contains prompts outside the normal staff on both sides while avoiding adjacent duplicates and impossible constraint loops.
- [x] Seeded composition remains deterministic and every palette pitch receives balanced coverage before adaptive weighting.

**Verification:**

- [x] Composer tests assert pitch bounds, required ledger coverage, leap limits, balance, and determinism over representative seeds. (30 composer tests; ledger below/above coverage checked across 5 seeds per staff)
- [ ] Manual: visually inspect treble/bass ledger notes and annotation lanes at narrow and wide viewport sizes. (NOT performed — no browser access)

**Implementation note (2026-09-25):** Replaced the full-bag-shuffle composition algorithm with a constrained sequential builder (`ComposePitches` in `SightReadingExerciseComposer.cs`) because leap limits and bag-shuffling are fundamentally incompatible — a shuffled bag has no notion of "how far is the next note from this one." At each prompt position it computes the palette pitches that are (a) not equal to the previous prompt and (b) within the preset's max diatonic leap of it; if that set is ever empty (theoretically possible only with a pathologically tight leap limit on a tiny palette, not the case for any current preset) it falls back to "not equal to previous" only, so the loop always completes in exactly `pitchCount` steps — no retry loop, no possibility of hanging, which is what "avoiding... impossible constraint loops" asked for. Required-coverage groups (ledger's below-staff and above-staff subsets) are guaranteed the same way: once too few remaining slots exist to still fit an unmet group, that position is force-picked from it (preferring a leap-valid member of the group if one exists) rather than left to chance. Chose the numeric leap limits myself since the plan names the *behavior* ("steps and thirds," "occasional wider skips") but not exact numbers: FiveNote = 2 diatonic steps (a third), OneOctave = 4 (a fifth), LedgerLines = 7 (an octave, needed to actually reach from the ledger extremes back toward the staff). These are easy to retune later — flagging them as the one place in this task I made an explicit numeric judgment call rather than following a spec. Re-ran the *existing* pre-Task-11 balance test (`Compose_FiveNoteTrebleExercise_CreatesBalancedQuarterNoteScore`, tight max-min diff ≤ 1) unchanged against the new algorithm and it still passes, so I didn't need to loosen any prior invariant. Also added `LedgerLines` to the `SightReadingExercisePanel.razor` range dropdown.

**Dependencies:** Task 3

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingPresetId.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

### Task 12: Add accidental and key-signature presets

**Description:** Add G-major and F-major reading without confusing sounding pitch, spelling, key signature, and rendered accidental policy.

**Acceptance criteria:**

- [x] G-major and F-major presets set `Score.KeyFifths` correctly and compose correctly spelled pitches from the selected key.
- [x] Notes implied by the key signature do not receive redundant accidentals; any deliberately chromatic future note must use the existing accidental policy.
- [x] Review and per-pitch mastery preserve pitch spelling while correctness continues to use MIDI sounding equality as currently documented.

**Verification:**

- [x] Composer tests assert key fifths, pitch spellings, and deterministic balanced coverage. (12 new tests)
- [x] Existing accidental rendering tests pass. (`GrandStaffSceneBuilderTests` — 134 passing, untouched by this task)
- [ ] Manual: verify F-sharp in G major and B-flat in F major on both staves. (NOT performed — no browser access)

**Implementation note (2026-09-25):** Read `GrandStaffSceneBuilder.GetScoreAccidentalGlyph` (Web rendering) before writing this task's Core code: it already suppresses a note's accidental glyph exactly when `pitch.Alter` matches what `score.KeyFifths` implies for that letter (via a circle-of-fifths lookup), and already renders one otherwise. So "no redundant accidental" needed zero rendering changes — it falls out automatically as long as the composer spells key-signature notes with the *correct* `Alter` (F# as `Pitch(F, +1, octave)`, not `Pitch(F, 0, octave)` plus a hoped-for key signature) rather than plain naturals. That correct spelling is also what makes grading automatically correct too, since `Pitch.MidiNumber` includes `Alter` directly — no separate "key-aware grading" logic was needed. I deliberately duplicated the small circle-of-fifths lookup (`SharpKeyOrder`/`FlatKeyOrder`, `GetKeySignatureAlter`) into the composer instead of extracting a shared Core utility that both it and `GrandStaffSceneBuilder` would use — DRY would be cleaner, but it would mean editing an already-shipped, visually-critical rendering file (with many past documented rendering bugs in `.agents/lessons.md`) for a refactor my task didn't strictly require, and I can't visually verify the result. Judgment call: correctness now over avoiding ~10 lines of duplication. One real bug I caught via a failing test before it shipped: I first reused the *other* presets' bass base octave (3), which would have produced G3-G4/F3-F4 instead of the curriculum table's G2-G3/F2-F3 — key-signature presets use a different bass octave than FiveNote/OneOctave, so they get their own octave logic rather than sharing `GetStaffBaseOctave`. "Preserve pitch spelling" for review/mastery required no changes — `PitchAttemptSummary`/`NoteReadingPromptResult` already store the actual written `Pitch` (Task 2/8), so F#4 and Gb4 already group as distinct mastery entries by construction, while `NoteReadingSession.Check` already grades by `MidiNumber` (sounding) equality, unchanged.

**Dependencies:** Task 11

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingPresetId.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

### Task 13: Add grand-staff and chord presets

**Description:** Reuse the session's existing grouped-prompt behavior to introduce both-staff reading and simple chords without changing generic grading semantics.

**Acceptance criteria:**

- [x] Staff selection supports treble, bass, and grand staff; grand-staff single-note exercises give both staves meaningful, balanced representation.
- [x] The first chord preset uses deterministic I/IV/V triads with no more than three distinct pitches and completes only after all chord tones are played in any order.
- [x] Prompt counts and UI labels refer to prompts, not raw `ScoreNote` count, and retry-missed preserves chord membership.

**Verification:**

- [x] Composer/coordinator tests cover cross-staff ordering, chord grouping, balance, and retry-missed. (11 new composer tests, 3 new coordinator tests including a real chord-preset retry-missed test)
- [x] Existing chord and grand-staff scene tests pass. (`GrandStaffSceneBuilderTests` — 134 passing, untouched by this task)
- [ ] Manual: verify chord highlighting, wrong-note feedback, and review colors on both score rows. (NOT performed — no browser access)

**Implementation note (2026-09-25):** Genuine architectural fork here, resolved deliberately rather than guessed: `Staff` (`PianoMapper.Music.Staff` — Treble/Bass) is a per-*note* fact used pervasively (`ScoreNote.Staff`, fingering generation, rendering, MIDI mapping); adding a third `Grand` value to it would be wrong (a note is drawn on exactly one physical staff even within a grand-staff piece) and would silently break every exhaustive `switch` over `Staff` throughout the app if a `Grand` value ever leaked into a `ScoreNote`. Instead added `SightReadingExerciseOptions.IsGrandStaff` as an *additive* `bool` with a default of `false`, so every existing call site (dozens of tests, the coordinator, the panel) needed zero changes — only new code paths engage it. `Staff` stays meaningful as "which staff when not grand" and is simply ignored when `IsGrandStaff` is true (documented on the record).

Grand-staff composition does **not** treat the two staves as one continuous leap-constrained sequence — I tried that mentally first and it doesn't work: a diatonic-index leap from a high treble note to a low bass note is huge by construction (they're written an octave-plus apart), so a uniform leap constraint would make the algorithm avoid crossing staves almost entirely, the opposite of "alternate." Instead `ComposeSingleNotePrompts` composes each staff's own sub-sequence independently (each with its own leap constraint applied only within that staff, reusing the *unchanged* single-staff `ComposePitches` from Tasks 11/12 with zero modification) and interleaves them treble-bass-treble-bass. This directly satisfies the curriculum table's "alternate single-note prompts" and trivially guarantees balance (an exact half-half split for even prompt counts) without needing a "required groups" forcing mechanism at all.

Chords are a new preset (`SightReadingPresetId.Chords`, its own composition path in `Compose`, not routed through `BuildPresetPalette`/`ComposePitches`) building I/IV/V triads of C major on the selected single staff (least-used-triad selection, never repeating the same triad immediately) — "the selected key" isn't wired to GMajor/FMajor yet since nothing in this task's acceptance criteria requires a selectable chord key and doing so would have meant deciding how key selection and chord selection compose in the UI, which the plan doesn't specify; C major is a simple, defensible default I'm flagging as a scope choice, not an oversight. Deliberately did **not** support `IsGrandStaff` for the `Chords` preset (documented on the options record) — splitting triads across two staves or interleaving whole-chord prompts is a materially different composition problem I didn't want to bolt on without being able to see whether the result reads sensibly on a real grand staff. `Compose()` was restructured around a unified `(Pitch, Staff)[]`-per-prompt model instead of the old fixed one-note-per-beat array, which is what let single-note and chord/grand-staff prompts share one measure-building loop — "prompt counts refer to prompts, not raw ScoreNote count" holds by construction now (a chord prompt is one array entry with 3 notes, not 3 array entries). Retry-missed chord-membership preservation needed zero new code: `ComposeFromMissedPrompts` (Task 7) already groups by original prompt, and `NoteReadingPromptResult.ExpectedSourceNotes` (Task 2) already contains all of a chord's source notes together — proved this with a coordinator test using a real `Chords`-preset exercise rather than trusting the Task 7 synthetic-chord test alone. Added the `Chords` option and a "Grand staff" checkbox (disables the Staff dropdown when checked) to `SightReadingExercisePanel.razor`.

**Dependencies:** Tasks 7 and 12

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

### Checkpoint D: Pitch curriculum

- [x] Tasks 11-13 tests pass with several deterministic seeds. (845 .NET tests passing; ledger and key-signature tests run across 5 seeds per staff)
- [ ] All pitch/key/chord presets render and complete end to end. (verified by composer/coordinator/session tests, which exercise real generated scores through `NoteReadingSession.Check` end to end in-process; NOT verified in an actual browser — no access this session)
- [ ] Review the proposed rhythm patterns and tempo defaults before continuing. (pending human/reviewer sign-off, same as Checkpoints A/C)

### Task 14: Add duration and rest composition

**Description:** Extend composition from fixed quarter notes to measure-balanced rhythmic patterns. Keep composition separate from live timing so the score is valid before enabling rhythm grading.

**Acceptance criteria:**

- [x] Add `Basic rhythm` in 4/4 and `Compound rhythm` in 6/8 using the recommended note/rest patterns; every measure has the exact required beat total.
- [x] Notes/rests use existing `NoteValue`, `TimeSignature`, `ScoreMeasure`, and score-derivation semantics; do not add a parallel duration model.
- [x] Seeded composition is deterministic, avoids malformed beams/onsets, and generates at least the requested prompt count without splitting a chord or rhythmic group incorrectly.

**Verification:**

- [x] Composer tests assert measure totals, onsets, note values, rests, 6/8 beat-unit behavior, and determinism. (12 new tests)
- [x] Existing `MusicalTime`, score derivation, rendering, and playback tests pass. (159 passing — `MusicalTime`, `ScoreDerivation`, `ScorePlayback`, `GrandStaffSceneBuilder` — none touched by this task)
- [ ] Manual: visually inspect beaming and spacing for 4/4 and 6/8 exercises. (NOT performed — no browser access)

**Implementation note (2026-09-25):** `SightReadingRhythmPreset` (`Fixed`/`Basic`/`Compound`) is another additive, default-valued field on `SightReadingExerciseOptions` (`= SightReadingRhythmPreset.Fixed`), same pattern as `IsGrandStaff` — zero ripple into existing tests. Measure patterns are a small fixed catalog per meter (7 for Basic, 4 for Compound) of explicit `(IsRest, NoteValue, BeamState)` event lists, each hand-verified to sum to its meter's exact beat total (checked by a test using `MusicalTime.GetBeats`, not just by eye). Beam states are written directly into each pattern literal rather than derived from note-value adjacency — I chose explicit over clever here on purpose: `.agents/lessons.md` documents several past bugs from inferring rendering-adjacent facts (beam/ledger/annotation placement) instead of stating them directly, and beam state is exactly that kind of fact. "At least the requested prompt count" (not exactly) is real, deliberate behavior: since measure patterns have 2-6 note events each depending on which pattern gets picked, hitting an *exact* prompt count would sometimes require ending a measure mid-pattern, which would violate the beat-total requirement — the composer instead adds whole measures until the count is met or exceeded, then stops. Reused the same "least-used pattern, no immediate repeat" balancing algorithm from Tasks 11-13 for pattern selection, so variety/balance falls out for free. Deliberately restricted variable rhythm to single-staff, non-chord exercises (`Compose` silently falls back to the fixed-rhythm path when `RhythmPreset != Fixed` is combined with `IsGrandStaff` or the `Chords` preset, documented on the options record) — aligning two staves' independently-varying measure lengths, or giving a triad a duration other than a quarter note, are materially different problems this task doesn't attempt to solve blind. Hit the same record-array-reference-equality gotcha as Task 8's `SightReadingSessionSummary` test in the new determinism test (`Assert.Equal` on `ScoreMeasure[]` compares array references inside the records, not content) — fixed by comparing flattened note/rest tuples instead, same resolution as before.

**Dependencies:** Task 13

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingRhythmPreset.cs` (new)
- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`

**Estimated scope:** M

### Task 15: Add an explicit rhythm-anchor contract

**Description:** Make rhythm grading accept a supplied audio-clock anchor so the first onset can be early, correct, or late. Preserve the current first-valid-onset fallback for imported-score idle checking.

**Acceptance criteria:**

- [x] `NoteReadingSession` can be reset/started with an optional explicit anchor in the same time domain as incoming browser events.
- [x] With an explicit anchor, the first prompt is graded against its score onset; without one, existing lazy anchoring remains unchanged.
- [x] Exact early/late tolerance boundaries, nonzero first onsets, 6/8, and reset behavior are covered.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests` (64 passing, 10 new)

**Implementation note (2026-09-25):** Small, surgical change: `Reset(Score?, NoteReadingMode, TimeSpan, TimeSpan? explicitRhythmAnchor = null)` — a 4th optional parameter on the existing 3-arg overload, so every existing caller (dozens across the test suite and `Piano.razor`) needed zero changes. Internally this needed almost no new logic: `ClassifyOnset`'s existing `rhythmAnchor ??= eventTime - ...` line already only *sets* the anchor when it's null, so pre-populating `rhythmAnchor` from `explicitRhythmAnchor` in `Reset` makes that line a no-op and the very first prompt gets graded against the supplied anchor exactly like every later prompt — no branching needed in the grading path itself. `Reset` still unconditionally assigns `rhythmAnchor = explicitRhythmAnchor` (not `??=`), so a later `Reset` call without an explicit anchor correctly clears any anchor from a previous exercise rather than leaking it in, which is what the "reset behavior" acceptance bullet and its dedicated test check. Task 16 (not yet implemented) is the intended caller — this task only adds the contract and proves it works in isolation.

**Dependencies:** Tasks 1 and 14

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** S

### Task 16: Expose hold/rhythm modes with count-in

**Description:** Add Pitch only, Pitch + hold, and Rhythm modes to the exercise panel. Rhythm mode starts from a count-in based on `BrowserMetronome.Grid` and passes the post-count-in audio-clock anchor to the session.

**Acceptance criteria:**

- [x] Pitch-only remains self-paced; hold mode grades written duration; rhythm mode requires initialized audio, performs one full-measure count-in, then grades the first onset against the explicit anchor.
- [x] Count-in/running/review status is visible, duplicate Start commands are ignored, and Retry/End/visibility loss cleanly stop the exercise-owned metronome.
- [x] The exercise uses the score's `TimeSignature` and `Tempo`; quarter-note tempo is not incorrectly reused as the beat unit in compound meter.

**Verification:**

- [x] Coordinator and `BrowserMetronomeTests` cover anchor calculation, state transitions, and cancellation. (13 new coordinator tests for the count-in state machine; existing `BrowserMetronomeTests` untouched and still passing since `BrowserMetronome` itself wasn't changed)
- [ ] Manual: play first notes early/on time/late in 4/4 and 6/8 and verify verdicts and metronome cleanup. (NOT performed — no browser access this session; this is the one item in the whole plan I'd flag as needing verification before trusting the live audio timing)

**Dependencies:** Tasks 5, 14, and 15

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`
- `PianoMapper.Tests/UnitTests/BrowserMetronomeTests.cs`

**Estimated scope:** M

**Implementation note (2026-09-26):** Modeled the count-in as a small state machine directly on `SightReadingExerciseCoordinator` (`IsCountingIn`, `StartCountIn`, `CountInTicksDue`, `TryCompleteCountIn`, `CancelCountIn`) rather than inventing a new mechanism, because this exact problem is already solved and shipping in this codebase: `PracticeSession` (`PianoMapper.Core/Practice/PracticeSession.cs`) already does "one full measure count-in, `TimeProvider`-driven, audio-clock anchor" for practice mode. I read it before writing anything and copied its core trick deliberately — snapshot the audio clock *once* (`AudioSession.GetCurrentTimeAsync()`, via `Metronome.Grid!.Anchor` after starting the metronome) and derive all subsequent "how much time has passed" checks from `TimeProvider.GetElapsedTime` (wall clock) rather than re-awaiting the audio clock every poll. This is not mixing audio-clock and wall-clock values incorrectly (the architecture decision this plan warns about); it's using wall-clock deltas to advance a single audio-clock snapshot, which only requires that both clocks tick at the same rate — true by construction. Every part of this is unit-tested with `FakeTimeProvider`, including a dedicated test proving the 6/8 case does *not* reuse the 4/4 duration (one 6/8 measure at "120" (eighth notes)/minute is 3s, not the 2s a 4/4 measure at 120 quarter-notes/minute would be) — the exact bug the acceptance criterion warns about.

The `Piano.razor` wiring mirrors the existing practice-mode ticker pattern as closely as possible: a new `AsyncTicker` (`sightReadingCountInTicker`), reusing the *same* `AsyncTicker` type and `CanvasRefreshInterval` the page already uses for practice/visualization polling, calling `TryCompleteCountIn` every tick until it succeeds. `PrepareSightReadingExerciseTransitionAsync` (Task 5's shared transition helper, already called by Generate/Retry/RetryMissed) now also stops this ticker and cancels any in-progress count-in first, so a held-note-style stale-state bug can't happen for count-ins either. Extended `HandleFocusLostAsync` (the existing visibility/focus-loss handler that already aborts practice mode) to do the same for an in-progress count-in, and `DisposeAsync` to stop the ticker. Rhythm mode requires `AudioSession.IsInitialized` before starting a count-in (falls back to a "enable audio" status message otherwise, never crashes). The metronome started for a count-in is stopped again the instant the count-in completes — it's a count-in only, not a continued click-track through the whole exercise, since nothing in the acceptance criteria asks for the latter and it would have meant deciding a whole separate "does clicking continue through Play" question the plan doesn't raise.

Found and fixed a real, pre-existing latent bug while wiring this in: `Piano.razor`'s `ActiveNoteReadingMode` computed property hard-coded `NoteReadingMode.PitchAndOrder` whenever a sight-reading exercise was active, on the (previously true) assumption that generated exercises were always pitch-only. That assumption became false the moment Task 16 added Hold/Rhythm modes to generated exercises. Left as-is, it would have silently broken two things: (1) changing the on-time-tolerance dropdown during an active Hold/Rhythm sight-reading exercise would never rebuild the session with the new tolerance (`SelectOnTimeToleranceAsync`'s guard checks `ActiveNoteReadingMode != PitchAndOrder`, which was unreachable for sight-reading), and (2) `GetNoteReadingStatus`'s early/late and hold-duration status text branches were entirely unreachable for sight-reading exercises (an early return at the top of the method always took the plain "Correct."/"not the highlighted note" path regardless of mode), so a Rhythm-mode exercise would have graded early/late notes correctly but never told the player so. Fixed both by making `ActiveNoteReadingMode` read `sightReadingExerciseCoordinator.Mode` when a sight-reading exercise is active, and restructuring `GetNoteReadingStatus` so its already-correct, already-shared hold/rhythm/chord status branches (previously reachable only for idle-score checking) also apply to sight-reading exercises. Verified line-by-line that every previously-reachable case produces byte-for-byte the same text as before (no observable behavior change for Pitch-only sight-reading or for any existing idle-checking case) — the only new reachable behavior is Hold/Rhythm sight-reading status text, which didn't exist as a code path before this task.

### Checkpoint E: Rhythm curriculum

- [x] Tasks 14-16 focused and full tests pass. (877 .NET tests passing, 0 warnings; Release build clean)
- [ ] Pitch-only, hold, 4/4 rhythm, and 6/8 rhythm exercises work end to end. (verified via coordinator/session unit tests exercising the real composition + grading + count-in state machine in-process; NOT run in an actual browser)
- [x] First-onset timing is demonstrably graded from the count-in anchor. (`TryCompleteCountIn_AfterOneMeasureElapses_ResetsSessionWithComputedAnchor` and the compound-meter variant both play the first note and assert the resulting `Verdict` against the computed anchor)
- [ ] Metronome/audio/session state is clean after retry, end, disconnect, and visibility loss. (code paths added and reasoned through for all four; NOT observed running in a real browser)

### Task 17: Generalize browser note input behind a source-neutral API

**Description:** Separate note lifecycle tracking from MIDI event parsing so MIDI, pointer, and computer-key sources can use the same command pipeline without pretending to be MIDI devices.

**Acceptance criteria:**

- [x] A source-neutral note event/state API starts and releases notes by stable source-specific ID, pitch, velocity, and audio-clock time; the MIDI adapter maps existing events into it.
- [x] Duplicate start, unknown release, clear, release-all, disconnect, and simultaneous identical pitches from different sources have explicit semantics and tests.
- [x] Existing MIDI behavior, 88-key range, velocity handling, timeline updates, and browser input commands are unchanged.

**Verification:**

- [x] Run all browser input state/binding tests. (7 existing `BrowserMidiInputStateTests` unchanged and passing, 12 new `BrowserNoteInputStateTests`)
- [ ] Manual: verify USB MIDI still produces one note-on/off and correct exercise grading. (NOT performed — no browser/MIDI hardware access this session)

**Implementation note (2026-09-26):** `BrowserMidiInputState` was already close to source-neutral internally — its `Dictionary<string, PerformedNote>` was always keyed by an opaque string ID, never anything MIDI-specific. The only genuinely MIDI-specific pieces were its `Handle(BrowserMidiEvent, ...)` entry point (MIDI number validation, the "velocity 0 means note-off" convention) and `CreatePitch(midiNumber)`. Extracted the actual note-tracking (start/release/clear/release-all, duplicate-start and unknown-release semantics) into a new `BrowserNoteInputState` (`PianoMapper.Web/Input/`, named to match this project's existing `BrowserMidiInputState`/`BrowserKeyboardState` convention rather than the plan's suggested `BrowserNoteInputEvent.cs` — it's a stateful tracker with methods, not a data record, so "State" reads more accurately than "Event"). `BrowserMidiInputState` is now a thin adapter: it does the MIDI-specific validation and the velocity-0 check itself (that's a MIDI protocol quirk, not generic input semantics), then delegates to `BrowserNoteInputState` for everything else. Verified byte-for-byte behavior preservation by running the *original, unmodified* `BrowserMidiInputStateTests` (all 7) against the refactored adapter with zero changes to that test file — they still pass, which is the strongest evidence I have (short of a running browser) that MIDI behavior is unchanged. "Simultaneous identical pitches from different sources" is handled for free by the existing design: notes are keyed by source ID, not pitch, so `"midi:roland:0:60"` and `"pointer:1"` both playing middle C are tracked as two independent entries — added an explicit test proving releasing one doesn't affect the other. This task only touches `PianoMapper.Web/Input/`; `Piano.razor` needed no changes since `BrowserMidiInputState`'s public method signatures didn't change. Tasks 18/19 (pointer/touch and computer-key input) are the intended future callers of `BrowserNoteInputState` directly.

**Dependencies:** Task 5

**Files likely touched:**

- `PianoMapper.Web/Input/BrowserMidiInputState.cs`
- `PianoMapper.Web/Input/BrowserNoteInputState.cs` (new — see implementation note for naming)
- `PianoMapper.Web/Pages/Piano.razor` (not touched — no public API change needed)
- `PianoMapper.Tests/UnitTests/BrowserMidiInputStateTests.cs` (unchanged, still passing)
- `PianoMapper.Tests/UnitTests/BrowserNoteInputStateTests.cs` (new)

**Estimated scope:** M

### Task 18: Make the 88-key piano playable by pointer and touch

**Description:** Convert the display-only keyboard into an accessible playable surface using pointer IDs and pointer capture so mouse, pen, and multitouch receive reliable note-off/cancel events.

**Acceptance criteria:**

- [x] Pointer down starts the selected pitch through the source-neutral path; pointer up, cancel, lost capture, focus loss, and visibility loss release it exactly once.
- [x] Multitouch can hold different pitches simultaneously, glissando does not leave stuck notes, and active styling remains driven by the shared active-MIDI-number set.
- [x] Keys are keyboard-focusable controls with useful labels, and touch interaction does not trigger page scrolling/selection inside the keyboard surface.

**Verification:**

- [x] Node tests cover pointer capture/cancel/lost-focus behavior in the keyboard module. (9 new tests: down/up/cancel/lost-capture/untracked-release/glissando-crossing/same-key-no-retrigger/multitouch/dispose)
- [x] Browser input tests cover synthetic pointer note IDs and cleanup. (`BrowserNoteInputStateTests` — same-pitch-different-source tests double as this; a MIDI+pointer coexistence test added to `BrowserMidiInputStateTests`)
- [ ] Manual: mouse, touch, and two-finger input can complete an exercise without stuck notes. (NOT performed — no browser/touch device access this session; this and Task 16's audio-timing check are the two items in this plan I'd most want verified before trusting them)

**Implementation note (2026-09-26):** Restructured note-input ownership while designing this: `BrowserMidiInputState` previously constructed its own private `BrowserNoteInputState`, but Task 18 needs pointer input to see and coexist with MIDI-held notes in the *same* tracker (so `ReleaseAll`, active-note counts, etc. cover every source together — the whole point of Task 17's "simultaneous identical pitches from different sources" contract). Changed `BrowserMidiInputState`'s constructor to take a `BrowserNoteInputState` by dependency injection instead of building its own; `Piano.razor` now constructs one `BrowserNoteInputState` in its constructor (right after `keyboardState`, before `midiInputState`, since C# field initializers can't reference other instance fields — learned this the hard way via a `CS0236` build error and moved the assignment into the constructor body) and hands it to both `BrowserMidiInputState` and the new pointer handlers. Updated the three existing `BrowserMidiInputState` constructor calls in its test file to match and added a test proving MIDI and pointer notes now share one tracker.

The pointer/touch feature itself: `piano-keyboard-input.js` uses standard Pointer Events (`setPointerCapture` on down, `pointermove` with `document.elementFromPoint` to detect gliding onto a different key since a captured pointer's own `event.target` doesn't update as it moves, `pointerup`/`pointercancel`/`lostpointercapture` to release) — multitouch works for free since every pointer gets its own `pointerId`-keyed tracking, both in the JS module and in `BrowserNoteInputState`'s per-source-ID map. `FullPianoKeyboard.razor` now owns its own JS module lifecycle (attach on first render, dispose on component disposal) and talks to `Piano.razor` through ordinary `EventCallback<BrowserPointerNoteOnEvent>`/`EventCallback<BrowserPointerNoteOffEvent>` parameters — threaded through the existing `PianoCanvas.razor` wrapper component — rather than handing `Piano.razor`'s own `DotNetObjectReference` down into the child component, matching how every other sub-component in this codebase (e.g. `SightReadingExercisePanel`) talks to the page via plain callbacks, not raw JS-interop coupling. Each key is now a real `<button>` with `data-pitch="C4"`-style attributes (round-tripped through the existing `Pitch.ToString()`/`Pitch.TryParse`) and an `aria-label`, replacing the previous `aria-hidden` decorative spans; added `touch-action: none`/`user-select: none` on the keyboard surface and a visible `:focus-visible` outline for keyboard navigation. Judgment call: I also made focus/visibility loss release *every* held note (all sources, via `noteInputState.ReleaseAll`) rather than only pointer-held ones — pointer input has no hardware-independent note-off guarantee the way MIDI does, so it genuinely needs this safety net, and applying it uniformly is simpler than filtering by source prefix; the tradeoff is a MIDI note held through a tab-switch now gets released a little early instead of waiting for its real note-off, which I judged a smaller problem than the stuck-note failure mode the acceptance criteria are explicitly worried about.

**Dependencies:** Task 17

**Files likely touched:**

- `PianoMapper.Web/Components/FullPianoKeyboard.razor`
- `PianoMapper.Web/Components/PianoCanvas.razor` (not in the original list — needed to thread the new callbacks down to `FullPianoKeyboard`)
- `PianoMapper.Web/wwwroot/js/piano-keyboard-input.js` (new)
- `PianoMapper.Web/wwwroot/css/app.css`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Input/BrowserMidiInputState.cs`, `BrowserPointerNoteOnEvent.cs` (new), `BrowserPointerNoteOffEvent.cs` (new)
- `PianoMapper.Tests/JavaScript/piano-keyboard-input.test.mjs` (new)
- `PianoMapper.Tests/UnitTests/BrowserMidiInputStateTests.cs` (constructor signature update + new shared-state test)

**Estimated scope:** M

### Task 19: Add opt-in computer-key note input

**Description:** Add a dedicated computer-piano state rather than mixing piano mappings into the control-shortcut table. Extend the keyboard module with key-up events and track the pitch chosen at key-down so octave changes cannot alter note-off.

**Acceptance criteria:**

- [x] An explicit toggle enables the documented desktop-style note layout; when disabled, current browser shortcuts and unmapped-key behavior remain unchanged.
- [x] Key repeat does not retrigger, key-up releases the originally started pitch, and focus/visibility loss releases all computer-key notes.
- [x] Editable text/select targets remain protected while checkbox/file controls continue to allow global shortcuts per the repository lesson. (Unchanged: `isEditableTarget`'s existing carve-out logic in `keyboard.js` is shared by both `keydown` and the new `keyup` listener — no piano-specific bypass was added, so the existing repository lesson still holds for both events.)

**Verification:**

- [x] C# tests cover mapping, repeat, octave changes, toggle state, and release-all. (`BrowserComputerPianoBindingsTests`: 8 tests — mapping, unmapped code, octave shift, distinct-codes invariant. `BrowserComputerPianoStateTests`: 10 tests — disabled/enabled/unmapped key-down, key repeat, same-code-held-twice, key-up with an octave change mid-hold, key-up on an unheld code, release-all with multiple held keys, release-all with nothing held, toggle-off leaving already-held notes alone.)
- [x] Existing and new `keyboard.test.mjs` cases cover down/up/focus behavior and editable targets. (4 tests total: 1 pre-existing keydown/editable-target test + 3 new keyup tests — handled code, unmapped code, and the editable-target carve-out including the checkbox/file-input exception.)
- [ ] Manual: use computer keys to complete an exercise while all control shortcuts still work. (NOT performed — no browser/keyboard-hardware access this session. Same category as Task 18's manual pointer/touch check: this is the item I'd most want a human to verify before trusting it, specifically the intentional code overlaps between piano notes and shortcuts, e.g. `KeyC`/`KeyV`/`KeyM`.)

**Dependencies:** Task 17

**Files likely touched:**

- `PianoMapper.Web/Input/BrowserComputerPianoState.cs` (new)
- `PianoMapper.Web/Input/BrowserComputerPianoBindings.cs` (new)
- `PianoMapper.Web/Input/MidiPitchMapping.cs` (new — not in the original list; extracted the semitone→`Pitch` mapping shared by `BrowserMidiInputState` and `BrowserComputerPianoBindings` rather than duplicating it)
- `PianoMapper.Web/wwwroot/js/keyboard.js`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/BrowserComputerPianoStateTests.cs` (new)
- `PianoMapper.Tests/UnitTests/BrowserComputerPianoBindingsTests.cs` (new — not in the original list)
- `PianoMapper.Tests/JavaScript/keyboard.test.mjs` (new keyup tests)

**Estimated scope:** M

**Implementation note (2026-09-26):** Kept the computer-piano mapping and held-key bookkeeping as their own types (`BrowserComputerPianoBindings` — a pure code→pitch lookup covering roughly one octave in the classic "Z row = white keys, S/D/G/H/J row = black keys" DAW layout, plus `BrowserComputerPianoState` — held-key tracking) rather than folding either into `BrowserKeyboardState`'s shortcut table, per the task description. Both route note starts/releases through the same source-neutral `BrowserNoteInputState` that MIDI and pointer input already share (source IDs of the form `"key:KeyZ"`), so `ActiveNoteCount`, mixed-source coexistence, etc. all continue to work exactly as Task 17 established.

`Piano.razor`'s `HandleKeyDownAsync` now tries `computerPianoState.HandleKeyDown(...)` first and only falls through to the existing shortcut table (`keyboardState.HandleKeyDown(...)`) when that call is *unhandled*. Getting the unhandled/handled boundary right took an extra iteration: my first draft treated "disabled", "unmapped code", "key repeat", and "already held" as all equally unhandled (so all four would fall through to shortcuts). That's correct for the first two — when the computer piano can't or won't claim the code, shortcuts must still work — but wrong for the latter two: a piano key that's genuinely repeating or already held is still a *claimed* piano code, and letting it fall through to the shortcut table risked re-triggering an overlapping shortcut (e.g. holding `KeyC` down, whose repeat events would otherwise re-match `BrowserKeyBindings`' "Clear notes" binding on that same code) on every OS-level key-repeat tick. Fixed by returning a *handled* no-op (`Kind: None, IsHandled: true`) for repeat/already-held instead of an unhandled one, and added explicit `IsHandled` assertions (not just `Kind`) to the two tests covering those paths so this distinction can't silently regress. `HandleKeyUpAsync` is new and routes only through `computerPianoState.HandleKeyUp(...)` — there's no shortcut-table fallback needed since `BrowserKeyboardState` has no key-up handling at all (shortcuts are momentary keydown actions).

Fixed a related bug found by design review before it ever ran: `HandleFocusLostAsync` already called the shared `noteInputState.ReleaseAll(eventTime)`, which *does* correctly stop the audio/timeline for computer-key-held notes too (they live in the same underlying dictionary as MIDI/pointer notes). But it never told `BrowserComputerPianoState` about it, so `heldPitchesByCode` would go stale — and since a key released while the tab is unfocused never gets its `keyup` event once focus returns, that code would stay "held" in local bookkeeping forever, silently no-op'ing every future press of that key (via the new handled-no-op path above). Fixed by adding a `computerPianoState.ReleaseAll(eventTime)` loop before the existing `noteInputState.ReleaseAll(eventTime)` call, which clears both the local bookkeeping and (redundantly but harmlessly) the shared tracker before the aggregate release runs.

Added a toggle checkbox ("Play notes with the keyboard (Z row = white keys)") in the existing "Play & MIDI tools" card, bound directly to `computerPianoState.IsEnabled`/`SetEnabled`. Turning it off mid-hold releases any currently-held computer-key notes immediately (same stuck-note-safety philosophy as Task 18) rather than leaving them to be silently orphaned until the next focus-loss event.

`keyboard.js`'s `attach()` now receives the *union* of `BrowserKeyBindings.HandledCodes` and `BrowserComputerPianoBindings.HandledCodes` as its prevented-default code set, computed once per attach — always including piano codes even while the toggle is off, since a disabled `BrowserComputerPianoState` simply no-ops those codes on the C# side (returns unhandled, falls through to shortcuts normally) and this avoids needing to re-attach the JS listener with a different code list whenever the toggle flips at runtime.

Extracted `MidiPitchMapping.CreatePitch(int midiNumber)` as a small static helper shared by `BrowserMidiInputState` and `BrowserComputerPianoBindings`, since both need the identical semitone-offset-from-MIDI-number → `Pitch` conversion and duplicating it risked the two mappings silently drifting apart.

Not done, deliberately, and worth a human decision before Task 20 or ship: no manual verification of real keyboard hardware, and no verification that the chosen code overlaps (`KeyC`, `KeyV`, `KeyM` are each both a shortcut and a piano note) feel right in practice — the design makes piano notes win those codes whenever the toggle is on, which is a reasonable default but an opinionated one, and it's the kind of interaction detail that's much easier to sanity-check by actually typing on a keyboard than by reading code.

### Task 20: Add clear input-readiness and MIDI connection guidance

**Description:** Make available input methods and connection requirements obvious at the exercise controls. Generating a score must no longer lead to a dead end when MIDI is unavailable.

**Acceptance criteria:**

- [x] The panel states whether MIDI is connected and always names the available on-screen fallback; it exposes the existing Connect MIDI action when appropriate.
- [x] The optional computer-key toggle includes its active mapping/range and does not imply MIDI is required.
- [x] Status and error copy is announced accessibly without revealing the expected answer.

**Verification:**

- [ ] Manual: verify disconnected, permission denied, connected, on-screen-only, and computer-key-enabled states. (NOT performed — no browser/MIDI-hardware access this session; same category as the other manual-only items in this plan.)
- [ ] Confirm an exercise can be completed in every advertised input state. (NOT performed, same reason. All the underlying grading/input plumbing this depends on is already covered by Task 17-19's automated tests — MIDI, pointer/touch, and computer-key input all drive the same `BrowserNoteInputState`/grading path — so this is specifically about confirming the *UI* guidance is accurate and usable, which needs a real browser.)

**Dependencies:** Tasks 18 and 19

**Files likely touched:**

- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`

**Estimated scope:** S

**Implementation note (2026-09-26):** No automated test coverage was added for this task specifically — the repo has no Blazor component-rendering test infrastructure (no bUnit or equivalent is referenced anywhere in `PianoMapper.Tests`), and the plan's own verification section for this task lists only manual checks, consistent with that. This is a markup/copy change over state that's already fully covered by other tasks' automated tests (MIDI connection state from Task 17's `BrowserMidiInputState`/existing `midiInputNames`/`isMidiConnecting` fields, computer-piano enabled state from Task 19's `BrowserComputerPianoState.IsEnabled`), so the risk surface is narrow, but the actual rendered wording/layout is unverified by anything other than a successful build.

Added `IsMidiInputConnected`, `IsMidiConnecting`, `IsComputerPianoEnabled` (bool) and `ConnectMidi` (`EventCallback`) parameters to `SightReadingExercisePanel.razor`. Replaced the previous static "Play each highlighted note on your MIDI piano" copy (which read as MIDI-required and never mentioned any fallback) with a computed `InputReadinessMessage` that always names the on-screen 88-key piano regardless of MIDI state, adds the computer keyboard to that list only when it's enabled, and leads with an explicit MIDI-connected/not-connected statement — e.g. "No MIDI piano connected — play each highlighted note on the on-screen piano below." Added a small "Connect MIDI piano" button inside the panel itself, shown only while `!IsMidiInputConnected` (reusing the page's existing `ConnectMidiAsync`/`isMidiConnecting` state so there's a single source of truth for MIDI connection, not a second parallel one) — a deliberately narrower affordance than the primary Audio card's always-visible Connect/Reconnect button, since re-prompting to reconnect from inside this panel once already connected didn't seem worth the extra visual noise. `IsMidiInputConnected` is wired from the page as `midiInputNames.Count > 0` specifically (not the existing broader `HasMidiConnection`, which also counts a MIDI *output*-only connection) since what the exercise actually needs is something that can send it note-on/off events, not just an output device.

For the computer-key toggle's own mapping/range disclosure (the other half of this task, living in the "Play & MIDI tools" card added in Task 19, not in the exercise panel itself), added a `<p class="control-note">` under the checkbox: "Optional — MIDI and the on-screen keys both work without it. When on, maps Z through / to white and black keys starting at C@(keyboardState.CurrentOctave), following the octave selected above." This explicitly disclaims MIDI-required framing and tells the user the mapping follows the existing octave picker rather than being a fixed range, which is accurate to `BrowserComputerPianoBindings`' behavior (the base octave is passed in from `keyboardState.CurrentOctave` on every key event).

The new status paragraph keeps its existing `role="status"` (already accessible via the pre-existing progress-status pattern used elsewhere in this panel) and contains no reference to which pitch is expected — it only ever describes *how* to play, never *what* to play, so it can't leak the answer.

### Checkpoint F: Hardware-independent exercise

- [x] MIDI regression tests, computer-key tests, and JavaScript input tests pass. (908/908 .NET tests, 77/77 JS tests, `rtk dotnet build`: 6 projects, 0 errors, 0 warnings — full suite re-run after Tasks 19 and 20.)
- [ ] MIDI, mouse, touch, and optional computer keyboard all traverse the same grading path. (Verified structurally, not end-to-end: every source (`midiInputState`, pointer handlers, `computerPianoState`) drives the one shared `BrowserNoteInputState`, and every JSInvokable/event handler that starts or releases a note funnels through the single `ApplyInputCommandAsync` method before reaching grading. Not confirmed live in a browser with real hardware — leaving unchecked pending manual verification.)
- [x] No input source leaves stuck audio after focus loss, visibility loss, retry, end, or device disconnect. (Verified by code inspection: `HandleFocusLostAsync`/visibility-loss share one handler and now also clear `BrowserComputerPianoState`'s own bookkeeping (Task 19 fix) in addition to the shared tracker; Generate/Retry/RetryMissed/End all route through `PrepareSightReadingExerciseTransitionAsync`, which calls the shared tracker's `Clear`; MIDI disconnect calls the shared tracker's `ReleaseAll`. All of these operate on the one dictionary every source shares, so none can leave a note *sounding*. Known minor non-audio quirk, not fixed here as out of scope: unlike focus/visibility loss, Retry/End/MIDI-disconnect don't also clear `BrowserComputerPianoState.heldPitchesByCode` — but that's harmless because a normal key-up still follows in those cases (the tab stays focused), so it self-corrects on the next physical release with nothing sounding in the meantime. Only the focus/visibility-loss path needed the explicit fix, because that's the one case where no key-up ever follows.)
- [x] The exercise panel never instructs a disconnected user to perform an impossible action. (Task 20's `InputReadinessMessage` always names the on-screen piano as playable regardless of MIDI state; the previous MIDI-only "Play each highlighted note on your MIDI piano" copy is gone. Confirmed by code review, not a live render.)

### Task 21: Weight new exercises from local mastery history

**Description:** Use local per-pitch mastery to repeat weak material while retaining coverage, seeded determinism, and the preset's musical constraints.

**Acceptance criteria:**

- [x] The composer accepts explicit optional mastery weights; no history/global service is accessed from Core.
- [x] Every allowed pitch still appears before adaptive repeats dominate, weak pitches receive measurably higher later frequency, adjacent duplicates remain forbidden, and leap/key/chord constraints remain valid.
- [x] Empty/insufficient history produces the existing balanced generator, and Retry same notes never recomposes adaptively.

**Verification:**

- [x] Seeded tests compare neutral versus deliberately weak-pitch histories across several prompt counts without probabilistic assertions. (5 new tests in `SightReadingExerciseComposerTests`, all fixed-seed: initial-coverage-holds-under-weighting, weak-pitch-frequency comparison — actual observed counts for the chosen seed were 8 neutral vs. 15 weighted out of 40 prompts, comfortably clear of the assertion's threshold, confirmed by a throwaway diagnostic run before writing the real assertion — adjacent/leap constraints hold under weighting, key-signature alterations stay correct under weighting, and empty-weights-equals-no-weights.)
- [x] Coordinator tests prove Generate Next uses the latest history while Retry same/missed preserves its selected material. (3 new tests in `SightReadingExerciseCoordinatorTests`: mastery-weighted Generate favors the weak pitch over a neutral Generate call with the same seed; empty mastery produces an identical score to omitting the parameter; and — the "latest, not cached" property — calling `Generate` twice on the *same* coordinator instance with the same seed but different mastery on the second call changes its output, proving nothing is cached between calls. Retry/RetryMissed needed no new test: neither method's signature gained a mastery parameter at all, so "never recomposes adaptively" is a compile-time guarantee, not just a runtime one — `Retry` still just calls `session.Reset` on the existing `Score` reference, and `RetryMissed` still only calls `ComposeFromMissedPrompts`, unrelated to `Compose`/weights.)

**Dependencies:** Tasks 8, 10, and 11

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Pages/Piano.razor` (not in the original list — needed to thread `sightReadingHistory.ComputeMasteryWeakestFirst()` into the existing `Generate` call site)
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-09-26):** `PianoMapper.Core/Practice/SightReadingHistory.cs` ended up untouched — its `ComputeMasteryWeakestFirst()` method (already built in Task 10, already gated on `MinimumMasteryAttempts`) was exactly the right shape to reuse as-is, so there was nothing to add there.

Core's `SightReadingExerciseComposer.Compose` gained a third, optional `IReadOnlyDictionary<Pitch, double>? pitchWeights = null` parameter, threaded through `ComposeSingleNotePrompts`/`ComposeVariableRhythmScore`/`ComposePitches` down to a new `ChooseWeightedLeastUsedCandidate` selection helper that sits alongside the existing `ChooseLeastUsedCandidate` (kept untouched, still used verbatim whenever `pitchWeights` is null, so the pre-existing integer-based tie-breaking and every pre-existing test's exact seeded output is completely unaffected by this change). The weighting scheme: compare `usageCount / weight` instead of raw `usageCount` (missing-from-the-dictionary defaults to weight 1.0, i.e. neutral). This was chosen specifically because every candidate starts at weighted-usage exactly 0 regardless of its weight — so the full-palette coverage guarantee (every pitch appears once before any repeat) falls out for free from the *existing* algorithm structure, rather than needing a second, separate coverage mechanism bolted on. Only once every pitch has appeared at least once do the weights start to differentiate picks, which is what produces the "adaptive repeats" phase the acceptance criteria describe. Deliberately scoped out: `SightReadingPresetId.Chords` triad selection (a structurally different per-triad-index algorithm, not per-pitch — weighting individual notes within a triad by mastery is a different design problem this task doesn't attempt) — chord constraints remain valid simply because that code path is untouched.

The coordinator's `Generate` gained a fourth (from the caller's perspective, third positional) optional `IReadOnlyList<PitchMastery>? mastery = null` parameter. It converts mastery to weights itself via a new private `BuildPitchWeights`/linear `1.0` (perfect/no-data) to `5.0` (0% accuracy) formula, so Core's public API only ever sees a plain pitch→double dictionary and never references `PitchMastery` or `SightReadingHistory` at all — satisfies "no history/global service is accessed from Core" as a literal type-level fact, not just a convention. `Piano.razor`'s single `Generate` call site (in `GenerateSightReadingExerciseAsync`) now passes `sightReadingHistory.ComputeMasteryWeakestFirst()` live at the call site; since `sightReadingHistory` is a plain field re-read on every call (not snapshotted anywhere upstream) and is already updated synchronously right after a session completes (`sightReadingHistory = sightReadingHistory.WithCompletedSession(summary)` before the save), the next "Generate new exercise" click after finishing an exercise already sees that exercise's own results factored in.

Not done, out of scope for this task: no manual/browser verification that the resulting adaptive difficulty *feels* reasonable in practice (the weight curve — up to 5x for a completely missed pitch — was chosen by reasoning about the algorithm's math, not tuned against real practice sessions). This is a product/UX tuning question, not a correctness one, and would need actual usage data or a human trying it to calibrate well.

### Task 22: Update documentation and complete full verification

**Description:** Align user-facing documentation and the manual browser matrix with the implemented behavior, then run the complete repository checks. Use `verify-readme-docs` for the documentation audit and `run-tests` for final validation.

**Acceptance criteria:**

- [x] README features, controls, requirements, and current limits accurately describe presets, review/history, rhythm/count-in, MIDI, on-screen input, and optional computer keys.
- [x] The browser test matrix covers clean transitions, hidden/revealed answers, each input source, local-storage failure, representative presets, 4/4 and 6/8 rhythm, and adaptive generation.
- [x] No stale claim remains that browser notes require USB MIDI or that touch piano is outside the release.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` — 916 tests passed, 0 warnings.
- [x] `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs` — 77 tests passed, 0 failed.
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release` — 6 projects, 0 errors, 0 warnings.
- [ ] Complete and record the relevant rows in `docs/browser-test-matrix.md` on a MIDI-capable browser and a no-MIDI browser/touch device. (NOT performed — no browser/MIDI-hardware/touch-device access this session. Added the rows themselves — a new "Note-reading exercise" section with 10 scenarios covering exactly the six categories this criterion names — all marked Pending, matching this file's existing convention for not-yet-manually-verified checks. This is the single biggest outstanding item in the whole plan: everything the automated suite can prove is proven, but nobody has yet actually played a note-reading exercise in a real browser.)

**Dependencies:** Tasks 6-21

**Files likely touched:**

- `README.md`
- `docs/browser-test-matrix.md`

**Estimated scope:** S

**Implementation note (2026-09-26):** Used the `verify-readme-docs` skill's workflow for the README pass: read the current file fully, cross-checked its claims against the actual implemented code (not against my own memory of the plan) for every claim I touched, and kept edits local to the stale claims rather than rewriting surrounding prose or style. Changes: (1) the note-reading exercise feature bullet was still describing the Task-1-era version (treble/bass only, no presets, no rhythm, no review, no history) — rewrote it to name the actual presets, rhythm options, count-in, review/retry-missed, and history-based adaptive weighting; (2) the 88-key browser piano bullet and the computer-key-input bullet both undersold the browser app relative to the desktop app before Tasks 18-19 — updated both to state the browser now has equivalent pointer/touch and computer-key input; (3) the Controls table's browser "Notes" row said only "Connected USB MIDI piano," which is now actively misleading since MIDI was never required even before this plan and is even less central now — updated to list all three input sources and added two short paragraphs on stuck-note-release-on-focus-loss (now covering all three sources, not just MIDI) and the computer-key toggle's intentional shortcut-code overlap; (4) "Current limits" still said "a touch piano... outside the current browser release," which Task 18 made flatly false — corrected while deliberately leaving the adjacent "accounts, backend synchronization, and mobile-specific layout" claims alone, since none of those were touched by this plan and rewriting a claim I didn't verify would violate the skill's own guardrail against drive-by cleanup.

For `docs/browser-test-matrix.md`, added the new "Note-reading exercise" section listing 10 concrete manual scenarios (one or more per acceptance-criteria category: clean transitions, hidden/revealed answers, one row per input source, local-storage failure, representative presets across staff/grand-staff/key/chords, 4/4 basic rhythm, 6/8 compound rhythm, and adaptive generation from repeated misses), all marked Pending — consistent with this file's own existing convention (most rows in every other section are also Pending; only physically-tested rows carry a browser/date). Did not restructure the file's top-level "Current evidence" summary table to add a note-reading-exercise column, since two of the file's other major sections ("Free Play measure-boundary ties", "Imported-score paired grand staff") already exist as their own standalone sections without a corresponding summary-table column, so a new standalone section matches established precedent rather than deviating from it.

Ran all three of this task's named verification commands directly (not through the generic `run-tests` skill, which is written for a different, trait/category-based multi-tier convention — `Category=Integration`/`Category=LiveApi` — that doesn't exist anywhere in this repo's test suite; every `PianoMapper.Tests` test is a plain fast unit test, matching how every prior task in this plan was verified). All three passed clean: 916 .NET tests, 77 JavaScript tests, and a 6-project Release build with 0 errors and 0 warnings.

## Final definition of done

- [ ] All 22 tasks and phase checkpoints are complete. (All 22 tasks are done. The checkpoints are not fully done: each checkpoint has one or more items still unchecked, and every one of those is either a human/reviewer sign-off item (Checkpoints A, C, D) or a real-browser/hardware manual-verification item this session could not perform (Checkpoints B, D, E, F) — none are unfinished implementation work.)
- [x] Full .NET, JavaScript, and Release-build verification succeeds. (916 .NET tests, 77 JavaScript tests, `dotnet build PianoMapper.slnx --configuration Release`: 6 projects, 0 errors, 0 warnings — the exact three commands Task 22 names, all re-run at the very end of this session.)
- [x] Current imported-score playback, practice, idle note checking, MIDI input, and notation rendering remain functional. (No automated test for any of these regressed across the whole plan — every pre-existing test in these areas is still in the 916-test green suite, and every task that touched shared code (Tasks 4-5's coordinator extraction, Tasks 17-18's MIDI adapter refactor) was explicitly designed and tested to be behavior-preserving. Not re-confirmed by hand in a live browser.)
- [x] Active exercise answers are hidden; Review exposes accurate per-prompt feedback and useful next actions. (Built in Task 6-7, covered by coordinator/session unit tests for the underlying state (`BuildReviewFirstTryMap`, hidden-during-`Active` flags, Retry/Retry-missed/End availability); the actual on-screen hide/reveal rendering was not visually confirmed in a browser.)
- [x] History and adaptation degrade safely when local storage is unavailable. (Directly covered by automated tests: `sight-reading-history.test.mjs`'s "reports storage unavailable" cases for read/write/clear, and Task 21's `BuildPitchWeights` returning `null` — the exact same code path as "no history at all" — for empty/insufficient mastery data.)
- [ ] Pitch, key, chord, hold, and rhythm presets work with deterministic tests and documented manual evidence. (Deterministic tests: yes, extensively, across Tasks 11-16 and Task 21. Documented manual evidence: no — this is the one piece of this criterion nothing in this session could produce, since it explicitly asks for manual evidence and no browser was available. Left unchecked rather than checked on partial credit.)
- [x] MIDI, pointer/touch, and optional computer-key input share cleanup and grading semantics. (True at the code/architecture level, which is what "share... semantics" asks: all three route through the one shared `BrowserNoteInputState` and the one `ApplyInputCommandAsync` funnel, covered by adapter-level unit tests for each source plus cross-source coexistence tests. Checkpoint F's separate, stricter bullet about confirming this live in a browser is intentionally left unchecked there — this bullet is about the semantics being shared, not about a live hardware confirmation.)
- [x] README and browser test documentation match the shipped behavior. (README verified claim-by-claim against actual code in Task 22 and corrected where stale. The browser test matrix's scenarios and expected results were added/updated to accurately describe shipped behavior; its Pending rows track that the manual runs themselves haven't happened, which is a separate, explicitly-tracked gap — not a documentation-accuracy problem.)

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The roadmap becomes one oversized change | High | Treat every checkpoint as a releasable milestone; do not begin the next phase with a failing checkpoint. |
| `NoteReadingSession` changes regress imported-score idle checking | High | Keep its API generic, preserve lazy rhythm anchoring as fallback, and run all existing session/scene tests after every domain change. |
| Exercise orchestration continues accumulating in `Piano.razor` | High | Complete coordinator extraction before review, history, curriculum, or input UI work. |
| Audio-clock and wall-clock timestamps are mixed | High | Use `WebAudioSession.GetCurrentTimeAsync`, MIDI mapped event times, and `MetronomeGrid.Anchor` exclusively for browser grading. |
| Exact `double` onset equality splits a chord | Medium | Centralize a documented beat tolerance in Task 1; consider rational musical time only as a separately approved refactor. |
| Touch/pointer cancellation leaves stuck notes | High | Use pointer capture plus cancel/lost-capture/focus/visibility cleanup and test each path. |
| Local-storage corruption or quota failure blocks practice | Medium | Version and bound the payload; catch storage failures at the adapter boundary and retain in-memory operation. |
| Adaptive weighting destroys balanced coverage | Medium | Keep baseline palette coverage mandatory, inject weights explicitly, and use deterministic invariant tests. |
| Key-signature presets confuse spelling with MIDI equality | Medium | Preserve `Pitch` spelling in generation/history and keep correctness based on sounding MIDI number unless separately changed. |
| Generated fingerings or annotation lanes collide with notation | Medium | Reuse `ScoreFingeringGenerator` and existing fixed annotation lanes; visually test ledger/chord extremes per repository lessons. |
| Parallel agents conflict in `Piano.razor` | Medium | Merge the coordinator first; serialize tasks that touch the page, or assign one integration owner while other agents work in Core/JS. |

## Parallelization guidance

- Tasks 1-5 are foundational and should remain sequential.
- After Checkpoint B, Tasks 8-10 (history) may run in parallel with Tasks 11-15 (Core curriculum) once the preset and prompt-result contracts are frozen.
- Task 17 can begin after Checkpoint B in parallel with Core curriculum work, but Tasks 18-20 should have one web-integration owner because they share `Piano.razor` and keyboard JavaScript.
- Task 21 depends on the history and pitch-preset slices but not on rhythm or alternate input.
- Task 22 is always last.

## Review decisions

Unless changed during review, implementation should use the assumptions above. The reviewer should explicitly confirm:

- [ ] The recommended curriculum ranges and G/F-major starter keys are acceptable.
- [ ] Local-only, 100-session history is acceptable.
- [ ] Timer-on-first-attempt semantics are acceptable.
- [ ] Suggested fingerings should be generated automatically and revealed in Review.
- [ ] Pointer/touch input is required; computer-key note input is included but opt-in.
- [ ] The work may ship checkpoint by checkpoint rather than as one release.
