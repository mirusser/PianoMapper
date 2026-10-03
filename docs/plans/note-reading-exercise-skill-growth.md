# Plan: Grow Real Playing Skill from the Note-Reading Exercise

**Date:** 2026-10-01

**Goal:** Turn the generated note-reading exercise from a pitch-name drill into a guided practice loop that trains reading, rhythm/meter, and steady playing for a beginner with a MIDI keyboard — by making grading honest, timing usable, rhythm trainable on its own, mistakes visible and explained, adaptation real, and progression guided.

This plan builds on `docs/plans/improve-note-reading-exercise.md` (all 22 tasks shipped on 2026-09-25) and carries over its one unfinished item (canvas review marks, Tasks 20–21 here).

## Context

An investigation of the shipped exercise on 2026-10-01 (code reading only; the app was not run) found these gaps. File references are to the current tree.

| # | Finding | Evidence |
|---|---|---|
| F1 | Rhythm/duration mistakes are scored as pitch-reading mistakes. A right pitch played 70 ms late sets `IsFirstTryCorrect=false`, counts as a missed pitch in history, and makes the next exercise over-serve that pitch. | `NoteReadingSession.cs:161-165`, `:246-249`; `SightReadingSessionSummary.cs:36-45`; `SightReadingHistory.ComputeMastery` ignores `Mode` |
| F2 | Timed modes have no usable pulse. Tempo is hard-coded to 120 BPM, the metronome stops the moment the count-in ends, Pitch + hold has no metronome at all, and the 60 ms tolerance applies to onsets *and* releases. | `SightReadingExerciseComposer.cs:6,94,289`; `Piano.razor:1369-1374, 1404`; `NoteReadingSession.ClassifyDuration` |
| F3 | Rhythm is welded to pitch (no rhythm-only mode), the catalog is small (7 patterns in 4/4, 4 in 6/8, quarter rest only), rhythm presets silently fall back to quarter notes on grand staff/chords, and the 6/8 metronome accents only beat 1 of 6. | `NoteReadingMode.cs`; `Composer.cs:49-55,168-204`; `audio.js:253` |
| F4 | **Rests are never drawn.** `ScoreRest` feeds horizontal spacing only (`GrandStaffSceneBuilder.cs:898`); there is no rest glyph kind and no rest drawing in `canvas.js`. The existing Basic rhythm preset's quarter rest renders as an unexplained gap. | `GrandStaffGlyphKind.cs`, `canvas.js`, `StaffRenderer.cs` (no rest drawing found) |
| F5 | **Per-prompt review marks are not rendered.** `BuildReviewFirstTryMap` has no UI consumer; the canvas review treatment was deferred in the earlier plan's Task 6. The README's "per-prompt review" wording overstates what ships (names/fingerings reveal and the summary line only). | `SightReadingExerciseCoordinator.cs:263` has no callers outside tests; old plan Task 6 note |
| F6 | Adaptation is weaker than documented. Every palette pitch must appear once before any repeat, so with 8/16 prompts weights barely matter (the weighting test needs 40 prompts). Mastery is keyed by pitch only (merges treble/bass, all modes), needs ≥5 attempts per pitch, and drops two useful signals that already exist per prompt: `WrongPlayedPitches` and `CompletedAt`. | `Composer.cs:443-469`; `ComposerTests.cs:111-131`; `SightReadingHistory.cs:19`; `NoteReadingPromptResult.cs` |
| F7 | The generator trains note naming, not reading: pitches never repeat, constrained random walk, no step/skip/repeat patterns, only C/G/F keys, no accidentals, grand staff alternates one hand per beat (never together), chords are root-position I/IV/V in C on one staff. | `Composer.cs:99-162` |
| F8 | Weak feedback at the moment of error and in progress: wrong note → "Try again" forever; progress is a raw list of the last 10 sessions with enum names, no trend, goals, or next-level guidance. | `Piano.razor:2617`; `SightReadingHistoryPanel.razor` |
| F9 | (Hypothesis from code reading, to be measured first.) Input timestamps are mapped to the audio clock with no output-latency compensation (`BrowserAudioClock.MapEventTimestamp`). A player in sync with the *heard* click may be graded systematically late by the audio output latency, which matters at a 60 ms tolerance. | `BrowserAudioClock.cs`; no `outputLatency` use found |

Two grading engines already exist and both are kept: `NoteReadingSession` (pitch-gated, waits for you) and `PracticeSession` + `Grader` (time-driven, with Missed/Extra and a moving cursor, currently only for loaded scores). The learner wants both styles for rhythm.

## Request and success criteria

Implement the full improvement set from the investigation, sequenced for a beginner learner:

1. Grading tells the truth: pitch, onset, and duration are separate outcomes and only pitch feeds pitch mastery.
2. Timing is usable: adjustable slow tempo, a click that keeps going, a visible beat, live early/late feedback, fair release tolerance, correct 6/8 accents, and (if measured as needed) latency calibration.
3. Rhythm can be practised on its own (any key), then with pitch, then with holds — and visibly includes rests.
4. Both pacing styles exist for rhythm: **Wait for me** (today's pitch-gated behavior) and **Play along** (time-driven, keeps going, grades Missed/Extra).
5. Mistakes are visible and explained: a review list with direction/interval wording, opt-in in-exercise coaching hints, and review marks on the notation.
6. Adaptation is real: speed/confusion/staff data captured, a mastery model that uses it, and an explicit weak-note drill.
7. Progression is guided: a level ladder and tempo ladder that recommend, never lock.
8. The reading generator and rhythm catalog grow in independent, à-la-carte steps.

The finished feature must keep active-exercise answers hidden (opt-in training wheels unchanged), remain deterministic for a seeded `Random`, preserve imported-score idle checking and practice, and never lose a learner's saved history.

## Scope assumptions

Default decisions for implementation. Change them during plan review rather than silently choosing differently mid-implementation.

- Learner: a beginner with a MIDI keyboard (FP-10 class). Defaults favor slow tempo and forgiving release grading; nothing is locked behind levels.
- Browser-local history stays the only persistence. History schema changes are additive (see D2); existing sessions must keep working.
- Play-along reuses `PracticeSession`/`Grader`/`BrowserPracticeCoordinator`; it is not a third grading engine.
- New modes (`PitchAndRhythm`, `RhythmOnly`) and Play-along are exercise-only. The idle imported-score checking selector is unchanged.
- Rests are first drawn for generated exercise scores only. Drawing them for imported scores is a separate decision after visual verification against real saved scores.
- Every aid that reveals the answer is opt-in and off by default: note names, fingering, next-key highlight (existing) and coach hints (Task 20, decided 2026-10-01). Nothing in this plan turns one on automatically.
- Manual verification uses a real browser and the user's MIDI keyboard. The earlier plan's manual items were never executed; where a task below says "Manual", it is a gate for that task, not a nice-to-have.

## Architecture decisions

- **D1 — Outcomes are separate; `IsFirstTryCorrect` keeps its meaning.** Per prompt: pitch first-try, onset verdict, duration verdict, signed onset deviation, response time, missed flag. `IsFirstTryCorrect` stays "everything clean" so Retry-missed and existing tests are unaffected. Pitch mastery reads only the pitch outcome. New fields are `init` properties with defaults so existing constructors compile.
- **D2 — History schema v2 is additive.** `SchemaVersion` becomes 2 once (Task 3); v1 entries still parse (today `TryParseEntry` drops any entry whose version isn't current — that must change first). All later fields (speed, confusions, staff) are optional additive members with no further bump. Unknown newer versions are still skipped. The localStorage key (`pianomapper-sight-reading-history-v1`) is a storage key, not the schema version, and does not change. v1 entries recorded in Hold/Rhythm modes are excluded from pitch mastery (their pitch outcome was conflated) but stay visible in the session list.
- **D3 — Enums are append-only.** `NoteReadingMode`, `SightReadingPresetId`, `SightReadingRhythmPreset` names are persisted as strings; add members, never rename. `Verdict` ordinals index `canvas.js`'s `verdictColors` (guarded by `scene-contract.test.mjs`): do not extend `Verdict` for review marks (Task 21 uses a separate channel).
- **D4 — Graded axes, not scattered mode checks.** `NoteReadingMode` maps once to three axes (Pitch, Onset, Duration) via a public helper used by `NoteReadingSession` and the play-along mapper, so both engines agree on what a mode grades.
- **D5 — Tempo is stored in the score's written-beat unit, exposed to the learner in pulses.** 4/4 pulse = quarter; 6/8 pulse = dotted quarter (×3 into eighth-beat BPM). Reuses the conversion lesson from `.agents/lessons.md` #15.
- **D6 — Two engines, one result type.** Wait-for-me and Play-along both produce `NoteReadingPromptResult` lists; review, history, mastery, and the ladder consume only that. A contract test runs equivalent synthetic performances through both engines.
- **D7 — Audio-clock discipline continues.** All timing uses the audio clock and `MetronomeGrid` anchors; never wall-clock mixed with Web Audio times. Calibration shifts *grading anchors*, not event times, the click, or the visual cursor.
- **D8 — Adaptation never silently breaks coverage.** Default generation keeps coverage-first. A weakness-first drill is an explicit action with its own invariants.
- **D9 — The ladder recommends; it never locks.** Pure Core function over history plus a static catalog; the UI only displays and applies it.
- **D10 — Rendering changes are pixel-verified.** Every task that changes what is drawn (rests, review marks, new keys, accidentals, hands-together, new rhythm patterns) is verified in real pixels at the smallest clamped canvas height (lessons #18–#19, #23–#26) and asks before overriding a documented lessons trade-off.

## Dependency graph

```text
Separate outcomes (T1)
    -> proportional hold tolerance (T2)
    -> summary v2 + migration + pitch-only mastery (T3) -> readable labels/pitch-vs-timing UI (T4)
    -> grading axes + new modes (T11) -> compose rhythm-only + mode ladder (T12) -> retry keeps rhythm (T13)
    -> play-along mapper (T14) -> coordinator pacing (T15) -> controller (T16) -> integration (T17a -> T17b) -> click/cursor (T18)

Exercise tempo (T5) -> click stays on (T6) -> beat indicator + chip (T7) -> calibration (T9, measurement-gated)
Beat-group accents (T8)            [independent]
Draw rests (T10)                   [independent; needed before rest-heavy catalog growth]

T1 + T4 -> mistake list (T19) -> coach hints (T20)
T1 -> review-mark spike (T21) -> review-mark data channel (T22a) -> drawing (T22b)

T3 + T1 -> capture speed/confusions/staff (T23) -> mastery model (T24) -> weighted/drill generation (T25) -> insights + drill UI (T26)
T3 + T5 + T12 + T23 -> level + tempo ladder core (T27) -> ladder UI (T28)

Generator growth (T29-T33) and catalog growth (T34-T37): independent of each other; serialize edits to SightReadingExerciseComposer.cs

All feature slices -> README + glossary + test matrix + full verification (T38)
```

## Implementation protocol

For every implementation task:

1. Load `code-standards`, `writing-tests`, and `tdd`; load `run-tests` before executing tests. Read `.agents/lessons.md` first.
2. Use codegraph exploration/impact for the named symbols before editing (`codegraph_impact` on any public Core type).
3. Write the failing test first when behavior changes; keep each task independently buildable; do not combine adjacent cleanup.
4. Prefix shell commands with `rtk`. Standard commands:
   - `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~<TestClass>`
   - `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`
   - `rtk dotnet build PianoMapper.slnx --configuration Release`
5. Manual checks: launch with `make` (or `dotnet run` with `ASPNETCORE_ENVIRONMENT=Development` — lessons #20), play on the MIDI keyboard, and use the Firefox BiDi headless screenshot recipe for pixel checks.
6. On any correction from the user, append a one-line rule to `.agents/lessons.md`.

## Task summary

**Implementation status (2026-10-02, updated at the end of the third run):** Tasks 1-8 and 10-38 are implemented with their automated tests and (where possible) real-browser checks in headless Firefox using mocked Web MIDI and real-pixel checks at the smallest clamped canvas height. The user picked the halo for the review marks on 2026-10-02 (Task 21), and the third run built Tasks 22a and 22b, ran Checkpoint E, brought the README, glossary, browser test matrix and the exercise panel's subtitle up to date (Task 38 follow-up), and checked the two shared-renderer changes against imported scores (see Checkpoint I). Still open: **Task 9** (needs the user's measurement on their hardware). Every "Manual" step that needs the physical MIDI keyboard or listening by ear is left unticked with a note, namely Tasks 2, 6, 7 (play along to the click), 12, 17a, 18 (play along), 32, 36, Checkpoint E's reading of the marks on the physical keyboard, plus Checkpoint G's "feels right for a real beginner" item. The headless runs exercise the real page, audio clock and grading but are not the keyboard. Checkpoint sign-offs by the user are not recorded. Phases 8-9 (Tasks 29-37) were scheduled à la carte after Checkpoint G and are done; each task records the decisions, the pre-existing tests it changed and its pixel evidence.

### Phase 1: Truthful grading data

- [x] Task 1: Separate pitch, onset, and duration outcomes in `NoteReadingSession`.
- [x] Task 2: Make hold-duration tolerance proportional to the written value.
- [x] Task 3: History schema v2, v1 migration, and pitch-only mastery.
- [x] Task 4: Show pitch vs timing results and readable history labels.

### Phase 2: Timing a beginner can use

- [x] Task 5: Exercise tempo (slow beginner default).
- [x] Task 6: Keep the click running through timed exercises.
- [x] Task 7: Beat indicator and live early/late feedback in the exercise panel.
- [x] Task 8: Beat-group-aware metronome accents (6/8).
- [ ] Task 9: Timing-offset calibration (measurement-gated).

### Phase 3: Rhythm apart from pitch

- [x] Task 10: Draw quarter rests in exercise scores.
- [x] Task 11: Grading axes plus `PitchAndRhythm` and `RhythmOnly` modes.
- [x] Task 12: Compose rhythm-only exercises and add the mode ladder UI.
- [x] Task 13: Retry-missed keeps the rhythm in timed modes.

### Phase 4: Time-driven play-along

- [x] Task 14: `Grader` ignore-pitch option and `GradingResult` → prompt-result mapper.
- [x] Task 15: Coordinator pacing and unified results.
- [x] Task 16: Exercise play-along controller.
- [x] Task 17a: Pacing select and Play-along start/abort/finish transitions.
- [x] Task 17b: Completion summary with per-verdict counts and shared verdict labels.
- [x] Task 18: Click alignment and cursor following.

### Phase 5: Mistakes you can see and understand

- [x] Task 19: Review mistake list with direction/interval wording.
- [x] Task 20: Coach hints while playing (opt-in, off by default).
- [x] Task 21: Review-mark design spike (throwaway). The user picked A (halo) on 2026-10-02.
- [x] Task 22a: Review-mark data channel, scene cache, and contract tests (no visual change).
- [x] Task 22b: Draw review marks and pixel-verify.

### Phase 6: Diagnosis and adaptation

- [x] Task 23: Capture speed, confusions, and staff in summaries.
- [x] Task 24: Staff- and speed-aware note mastery model.
- [x] Task 25: Staff-aware weights and weakness-first drill generation.
- [x] Task 26: Progress insights and drill entry point.

### Phase 7: Guided path

- [x] Task 27: Level ladder and tempo ladder (Core).
- [x] Task 28: Ladder UI and "Start recommended exercise".

### Phase 8: Reading generator growth (à la carte)

- [x] Task 29: Melodic and intervallic motion.
- [x] Task 30: D major, B♭ major, A minor.
- [x] Task 31: Accidentals preset.
- [x] Task 32: Hands-together grand staff.
- [x] Task 33: Chord inversions.

### Phase 9: Rhythm catalog growth (à la carte)

- [x] Task 34: Extended 4/4 vocabulary (dotted, whole, eighth rest).
- [x] Task 35: 3/4 and 2/4.
- [x] Task 36: Syncopation and ties.
- [x] Task 37: More 6/8 patterns.

### Phase 10: Handoff

- [x] Task 38: README, glossary, browser test matrix, and full verification.

## Suggested release slices

| Release | Tasks | What the learner feels |
|---|---|---|
| R1 — Honest and usable timing | 1–10 | Right pitch late no longer counts as a wrong pitch; slow tempo, steady click, visible beat and ms early/late; rests visible; fair release grading. |
| R2 — Rhythm on its own | 11–13 | Tap a rhythm on any key, then add pitch, then holds; Retry-missed keeps the rhythm. |
| R3 — Play along | 14–18 | A moving cursor, no waiting, Missed/Extra graded, same history. |
| R4 — Learn from mistakes | 19–22 | "You played D, a step higher"; optional coaching after repeated misses (off unless you turn it on); marks on the staff. |
| R5 — Adapt and guide | 23–28 | Weak-note drill, speed/confusion insights, recommended next level and tempo. |
| R6/R7 — Grow the catalog | 29–37 | New keys, accidentals, hands together, richer rhythm — schedule à la carte. |

For a beginner, the highest-value cut is **R1 + R2 + Tasks 19–20**.

## Detailed tasks

### Task 1: Separate pitch, onset, and duration outcomes in `NoteReadingSession`

**Description:** Today any onset or duration miss calls `RecordWrongAttempt`, so a prompt has one fused first-try flag. Track the three outcomes separately per step and publish them in the prompt result, plus the two numbers later tasks need (signed onset deviation and response time). `IsFirstTryCorrect` keeps its meaning ("all clean"); `WrongAttemptCount` keeps its current total.

**Acceptance criteria:**

- [x] `NoteReadingPromptResult` gains `init` properties: `IsPitchFirstTryCorrect`, `OnsetVerdict` (null when onset is not graded), `DurationVerdict` (null when duration is not graded), `OnsetDeviation` (signed `TimeSpan?`), and `ResponseTime` (`TimeSpan?`: time from the previous prompt's attack completion to this prompt's first correct attack; null for prompt 0 and for onset-graded modes, where the clock — not the learner — sets the time).
- [x] A right pitch played late yields pitch ✔, onset `Late`, `IsFirstTryCorrect=false`; a wrong key followed by the right key on time yields pitch ✘, onset `Correct`. The session also exposes `PitchMistakeCount` and `TimingMistakeCount` partitioning today's `WrongAttemptCount`.
- [x] All existing `NoteReadingSessionTests` pass unchanged unless deliberately tightened with a stated reason.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests`
- [x] New tests cover Pitch-only (timing fields null), Hold (duration only), Rhythm (onset + duration), chord steps, and the late-but-right / wrong-then-right cases above.

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Core/Practice/NoteReadingPromptResult.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `IsPitchFirstTryCorrect` falls back to `IsFirstTryCorrect` when a result does not set it (hand-built pre-split results keep their fused meaning, so no existing summary/history test needed editing). A chord prompt reports its *worst* onset (largest absolute deviation) and its first duration mistake. `DurationVerdict` stays null until the first key of the prompt is released. `ResponseTime` uses the `eventTime` the caller passes to `Check` (the page always passes the audio clock).

### Task 2: Make hold-duration tolerance proportional to the written value

**Description:** Releases are graded with the same fixed ±60 ms as onsets, which is stricter than normal playing (notes are often lifted slightly early). Replace the release window with `max(onset tolerance, 25% of the written duration)`, with the ratio as one named constant. This is the shared engine, so idle checking of imported scores gets the same fairer rule.

**Acceptance criteria:**

- [x] A note released at 80% of its written duration grades Correct; at 60% grades TooShort; at 140% grades TooLong; very short notes never get a window smaller than the onset tolerance.
- [x] Onset classification is unchanged. The ratio lives in one named constant with a doc comment explaining the choice.
- [x] Existing boundary tests that asserted the exact ±tolerance release edge are updated deliberately and each states why.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests`
- [ ] Manual (MIDI keyboard): in Pitch + hold at a slow tempo, a normal slightly-early lift is not flagged "released too soon".

**Dependencies:** Task 1 (same file; avoids conflicts)

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** S

**Implementation note (2026-10-02):** The only pre-existing test changed is `Release_PitchAndHoldAtToleranceBoundary_ClassifiesInclusively`: its inclusive edges moved from 440/560 ms to 375/625 ms because a 500 ms quarter note now has a 125 ms release window (the test carries a comment stating this). Manual MIDI-keyboard check: **not performed** (needs the physical keyboard).

### Task 3: History schema v2, v1 migration, and pitch-only mastery

**Description:** Fix F1 end to end. Bump the summary to schema v2 with additive optional members, accept v1 entries on read, compute new sessions' per-pitch counts from the pitch outcome only, and exclude v1 Hold/Rhythm-mode entries from pitch mastery (their pitch outcome is unrecoverable). Later tasks add further optional fields without another bump (D2).

**Acceptance criteria:**

- [x] v2 adds optional members: `RhythmPreset` (string), `IsGrandStaff`, `TempoBeatsPerMinute`, `Pacing` (string; null means wait-for-me), `PitchFirstTryCorrectCount`, `TimingMistakeCount`, and early/late/short/long counts. `PitchAttemptSummary.CorrectFirstTryCount` now derives from `IsPitchFirstTryCorrect`.
- [x] `TryParseEntry` accepts v1 and v2, still skips unknown future versions and malformed elements. A v1 PitchAndOrder entry contributes to mastery identically to today; a v1 PitchAndHold/PitchHoldAndRhythm entry appears in `Entries` but not in mastery.
- [x] A new late-but-right-pitch prompt counts as pitch-correct for mastery. JSON round-trip (`ToJson`/`FromJson`) is lossless.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingHistoryTests|FullyQualifiedName~SightReadingSessionSummaryTests"`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/sight-reading-history.test.mjs` (storage key unchanged).
- [x] A checked-in v1 JSON fixture (taken from a real saved history) loads without losing any entry.

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Practice/SightReadingSessionSummary.cs`
- `PianoMapper.Core/Practice/SightReadingHistory.cs`
- `PianoMapper.Core/Practice/PitchAttemptSummary.cs`
- `PianoMapper.Tests/UnitTests/SightReadingHistoryTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingSessionSummaryTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** New members are `init` properties on the existing positional record (existing constructors and tests compile unchanged); the serializer now omits nulls so v2 entries without a pacing/tempo stay small. `IsGrandStaff` is `bool?` (null = recorded before it was tracked; a v1 grand-staff session stored an arbitrary `Staff`). `TimingMistakeCount` and the early/late/short/long counts are *prompt* counts derived from the prompt results (engine-independent per D6), not the session's per-attempt `TimingMistakeCount`. The v1 fixture `PianoMapper.Tests/Fixtures/sight-reading-history-v1.json` was produced by running the **unmodified v1 serializer** over three sessions (one per pitch/hold/rhythm mode); the user's real browser history lives in their Firefox profile and was deliberately not read or touched. The coordinator stamps `RhythmPreset`/`IsGrandStaff`; tempo and pacing are stamped by Tasks 5 and 15. `PianoMapper.Tests.csproj` now also copies `Fixtures\*.json`.

### Task 4: Show pitch vs timing results and readable history labels

**Description:** Make the new data visible. The completion/progress lines report pitch and timing separately when timing is graded; the history list uses human-readable preset, staff, mode, rhythm, and tempo labels instead of enum names. One shared label helper feeds both the panel options and the history list so they cannot drift.

**Acceptance criteria:**

- [x] In Pitch-only mode the summary line is identical to today. In timed modes it adds a timing part (e.g., "pitch 7 of 8 first-try · 3 timing mistakes").
- [x] History rows read like "Five notes · Treble · Pitch only" / "…· Rhythm · ♩ = 60"; v1 rows lacking new fields render gracefully ("older session").
- [x] Label helper is unit-tested for every `SightReadingPresetId`, `NoteReadingMode`, and `SightReadingRhythmPreset` member (a new enum member without a label fails a test).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~SightReadingLabels"`
- [x] Manual: complete one exercise in Pitch-only and one in Rhythm; compare the lines. (Done with mocked Web MIDI in headless Firefox, not the physical keyboard.)

**Dependencies:** Task 3

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingLabels.cs` (new)
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Components/SightReadingHistoryPanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** The Mode label for `PitchHoldAndRhythm` is "Rhythm" for now (matching the plan's example row); Task 12 renames it to its ladder wording. The panel's selects now render their text from `SightReadingLabels` (presets and rhythm presets by looping the enum, so a member without a label throws instead of silently vanishing); the visible option wording changed slightly as a result (e.g. "Starter — five notes" is now "Five notes (starter)"). `NoteReadingPromptResult.HasTimingMistake` is the one shared definition used by the summary and the coordinator's `TimingMistakeCount`. Mode-to-"is timing graded" lives in `SightReadingLabels.IsTimingGraded` until Task 11 introduces the Core axes helper. Manual comparison of the Pitch-only and Rhythm summary lines is recorded under Checkpoint A.

### Checkpoint A: Truthful grading data

- [x] Tasks 1–4 focused tests pass; Release build is clean.
- [x] Mastery no longer penalizes pitches for timing mistakes (covered by a test, not just a claim).
- [x] An existing real v1 history loads intact.
- [ ] Review with the user before Phase 2. (Status reported in the implementer's hand-off; the user has not signed off.)

**Checkpoint A status (2026-10-02):** Focused tests, the full .NET suite (987 passed), the JS suite (77 passed) and the Release build (0 warnings) are clean. "Real v1 history" is covered two ways: the fixture test above, and a headless-Firefox run where a throwaway profile's localStorage was seeded with that fixture — the Progress panel listed all three sessions ("… · older session"), then a new Pitch-only and two Rhythm sessions were appended in the v2 shape. The user's own browser history was not read or touched. Manual runs used **mocked Web MIDI** (the scratchpad harness presses the app's highlighted keys through a fake MIDI input), so they exercise the real page, audio clock and grading but are **not** the physical MIDI keyboard. Observed: Pitch-only summary line unchanged ("Complete: 7 of 8 first-try correct (87.5%), 1 mistake(s), …"); Rhythm adds "(pitch 8 of 8 first-try · 0 timing mistake(s))" for an on-time run, and "(pitch 8 of 8 first-try · 2 timing mistake(s))" for a run with two pitch-correct late notes (one deliberately 150 ms late, one made late by the driver playing notes sequentially): first-try fell to 6 of 8 while pitch stayed 8 of 8 — the F1 fix, seen in the real page.

### Task 5: Exercise tempo (slow beginner default)

**Description:** Add a learner-chosen tempo to the exercise instead of the hard-coded 120 BPM. The option is in pulses (quarter in 4/4, dotted quarter in 6/8) and converted to the score's written-beat unit (D5). While an exercise is active, the Timing card mirrors the exercise tempo and time signature (as loaded scores already do) and its tempo/time-signature inputs are disabled with a short note, so the manual metronome and the exercise cannot disagree.

**Acceptance criteria:**

- [x] `SightReadingExerciseOptions` carries a tempo in pulses; defaults are 60 (4/4) and 40 (dotted-quarter pulses in 6/8). Out-of-range values (outside 30–200 pulses) throw `ArgumentOutOfRangeException`; tempo never consumes `Random`, so seeded pitch sequences are unchanged.
- [x] 4/4 at 60 → `Score.Tempo` 60; 6/8 at 40 pulses → `Score.Tempo` 120 eighth-beats/min (a measure lasts 3.0 s). Retry and Retry-missed preserve the tempo; the count-in length follows it.
- [x] The panel shows a tempo input only for timed modes with a unit label (♩ = 60 / ♩. = 40); the Timing card mirrors it during an exercise and its inputs are disabled.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`
- [x] Manual: Rhythm mode at 60 vs 90 — count-in lasts one measure at the chosen tempo; manual metronome shows the same tempo.

**Dependencies:** Task 3 (summary records tempo)

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`

**Estimated scope:** M

**Implementation note (2026-10-02):** Deliberate changes to existing tests: `SightReadingExerciseComposerTests` now expects the default `Tempo(60)` (was 120), and three count-in tests in `SightReadingExerciseCoordinatorTests` (`CountInTicksDue_AfterOneBeatElapses_IsTwo`, `TryCompleteCountIn_BeforeOneMeasureElapses_…`, `TryCompleteCountIn_AfterOneMeasureElapses_…`) pin `SetTempoPulsesPerMinute(120)` because their timelines were written for 120 BPM; their assertions are unchanged. Decision: choosing a rhythm preset that changes the pulse unit (quarter <-> dotted quarter) drops a chosen tempo for the new default, because "60" means something else in 6/8. The summary records `TempoBeatsPerMinute` only for timing-graded modes. While an exercise is active the Timing card mirrors the exercise's meter/tempo and its beats, beat-note and tempo inputs are disabled (the on-time tolerance select stays enabled); the pre-exercise values are restored when the exercise ends or is replaced by a score/playback. Manual (headless Firefox, mocked MIDI): count-in measured 3.96 s at 60 and 2.64 s at 90 (expected 4.00 s / 2.67 s); the Timing card read 4/4 · 90 BPM then 4/4 · 60 BPM with the three inputs disabled, and 4/4 · 120 BPM with inputs enabled again after End.

### Task 6: Keep the click running through timed exercises

**Description:** Add a durable `ClickWhilePlaying` exercise setting (default on). In Rhythm mode the count-in metronome keeps running past the anchor instead of stopping at `TryCompleteCountIn`. In Pitch + hold mode the click starts with the exercise (tempo reference only — no anchor or count-in needed). The click stops on completion, End, Retry, Generate, and focus loss.

**Acceptance criteria:**

- [x] With the setting on in Rhythm mode, the metronome grid and its anchor persist after the count-in and the rhythm anchor equals the grid anchor plus one measure. With it off, behavior is identical to today.
- [x] In Pitch + hold mode the click starts at generation using the score's time signature and tempo, and stops at completion; no click survives Generate/Retry/Retry-missed/End or a focus loss.
- [x] `ClickWhilePlaying` is a durable setting like the reveal flags: Generate/Retry/Retry-missed/End do not reset it.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~BrowserMetronomeTests"`
- [ ] Manual (MIDI keyboard): click continues through a whole Rhythm exercise and stops cleanly at Review; End mid-exercise leaves no click. **Physical MIDI keyboard / by-ear check NOT performed**; it was simulated with mocked Web MIDI in headless Firefox (see this task's implementation note).

**Dependencies:** Task 5

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** The click is owned by a small `ExerciseClick` (Web/Practice, registered in DI) that remembers the beat grid it started, so stopping it never silences a metronome the learner started themselves and a manual restart takes ownership away; the page's separate `StopExerciseClickAsync` also resets the tempo-feedback tracker. `SightReadingExerciseCoordinator.ShouldClickSound` is the single policy (always during the count-in, then only while a timing-graded exercise is active with `ClickWhilePlaying` on). The setting appears as "Keep the click going while playing" in the "While playing" group, only for timing-graded modes. The page stops the click when the exercise reaches review (checked wherever history is saved), on End/Retry/Retry-missed/Generate (existing transition code), when a score/playback replaces the exercise, and on focus loss (status: "Click stopped (focus lost)…"; the exercise itself keeps running). Manual (headless Firefox, mocked MIDI, "Start/Stop metronome" button text as the indicator): Rhythm click ran through the whole exercise and was off at Review; End mid-exercise left no click; focus loss stopped it; with the setting off the click stopped when the count-in ended; Pitch + hold started the click at generation, kept it through Retry, and End stopped it.

### Task 7: Beat indicator and live early/late feedback in the exercise panel

**Description:** The beat flash (`[data-metronome-pulse]`) and the early/late chip live inside the collapsed Timing card. Extract that block into a reusable `TempoFeedback` component, show it in the exercise panel while the click runs, and make the JS pulse update every matching element. Reset the tempo-feedback tracker at exercise start so its stats describe the current attempt.

**Acceptance criteria:**

- [x] While the click runs, the exercise panel shows the beat pulse and "N ms early/late · median …"; it is hidden when the click is off. The Timing card's display behaves as before.
- [x] `pulseMetronome`/`clearMetronomePulse` use `querySelectorAll`, so both indicators pulse together.
- [x] Generate/Retry reset the tracker; stats never carry over from a previous exercise.

**Verification:**

- [x] `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs` (add a pulse test using the existing `FakeAudioContext` pattern in `external-midi-audio.test.mjs`).
- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~TempoFeedbackTrackerTests`
- [ ] Manual: play along to the click and watch the chip. **Physical MIDI keyboard / by-ear check NOT performed**; it was simulated with mocked Web MIDI in headless Firefox (see this task's implementation note).

**Dependencies:** Task 6

**Files likely touched:**

- `PianoMapper.Web/Components/TempoFeedback.razor` (new)
- `PianoMapper.Web/Components/MetronomeControls.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/wwwroot/js/audio.js`
- `PianoMapper.Web/wwwroot/css/app.css`

**Estimated scope:** M

**Implementation note (2026-10-02):** The beat indicator and chip live in the new `TempoFeedback` component, used by `MetronomeControls` (unchanged behavior) and by the exercise panel (shown only while the exercise's own click runs). `audio.js` `pulseMetronome`/`clearMetronomePulse` now use `querySelectorAll`. **Deliberate changes to existing JS tests:** the fake `document` objects in `external-midi-audio.test.mjs` (two) and `canvas.test.mjs` gained `querySelectorAll: () => []` because audio teardown now calls it; the new pulse test extends the file's `FakeAudioContext` with oscillator/gain methods. The tracker is reset in `PrepareSightReadingExerciseTransitionAsync` (Generate/Retry/Retry-missed/End). `TempoFeedbackTrackerTests` needed no change. Manual (headless Firefox, mocked MIDI): both `[data-metronome-pulse]` elements were active together in every sampled frame (never one without the other); the panel chip read "Play a note to check your timing." after Generate, then "On time · 3/3 on time · median +1 ms" after three notes at 60 pulses, and was absent with the click off.

### Task 8: Beat-group-aware metronome accents (6/8)

**Description:** The metronome accents only the first beat of a measure, so 6/8 is counted as six equal pulses. Expose the beat grouping from Core (3 for x/8 meters with a numerator that is a multiple of 3 and ≥ 6; otherwise 1) and add a mid-strength accent on each group start. The count-in remains one full measure.

**Acceptance criteria:**

- [x] `MetronomeGrid` (or `TimeSignature`) reports `BeatsPerGroup`; 6/8, 9/8, 12/8 → 3; 4/4, 3/4, 2/4 → 1.
- [x] `startMetronome` takes the group size; in 6/8, clicks 0 and 3 are accented (downbeat strongest, group start medium); other meters are byte-for-byte unchanged in behavior.
- [x] Benefits the manual metronome for loaded 6/8 scores too.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MetronomeGridTests|FullyQualifiedName~BrowserMetronomeTests"`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`
- [ ] Manual: 6/8 click sounds as "ONE two three TWO two three".

**Dependencies:** None (parallel with Tasks 5–7)

**Files likely touched:**

- `PianoMapper.Core/Music/MetronomeGrid.cs`
- `PianoMapper.Web/Playback/IBrowserMetronomeAudio.cs`
- `PianoMapper.Web/Audio/WebAudioSession.cs`
- `PianoMapper.Web/Audio/BrowserMetronome.cs`
- `PianoMapper.Web/wwwroot/js/audio.js`

**Estimated scope:** M

**Implementation note (2026-10-02):** `MetronomeGrid.BeatsPerGroup` is 3 only for an undotted eighth-note beat with a numerator that is a multiple of 3 and at least 6 (so 3/8 and a dotted-eighth beat stay 1). `IBrowserMetronomeAudio.StartMetronomeAsync` gained a required `beatsPerGroup` parameter; `audio.js` `startMetronome` defaults it to 1, so any other meter schedules exactly the same clicks as before. Group starts use 1540 Hz at gain 0.45 between the downbeat (1760 Hz, 0.55) and the plain beat (1320 Hz, 0.35). **Deliberate change to an existing test:** `WebAudioSessionTests.MetronomeCommands_InitializedSession_StartsAndStopsWithGridValues` now passes and expects the fourth `beatsPerGroup` argument; the two fake `IBrowserMetronomeAudio` implementations in the tests gained the parameter. Manual (headless Firefox, hooked oscillator frequencies instead of listening): the real page's manual metronome at 6/8 produced 1760, 1320, 1320, 1540, 1320, 1320, 1760 Hz — ONE two three TWO two three ONE. Not verified by ear.

### Task 9: Timing-offset calibration (measurement-gated)

**Description:** (Addition from the investigation, F9; not one of the original seven proposals.) Input timestamps are mapped to the audio clock without output-latency compensation, so a player in sync with the *heard* click can be graded systematically late. **Step 1 is a measurement, not a build:** on the user's actual setup (MIDI keyboard + browser piano sound, and + FP-10 external sound), tap along to 16 clicks and record the median deviation. **Decision gate:** if |median| < 25 ms, close this task as "not needed" and record the measurement. Otherwise add a "Calibrate timing" action that taps 8 clicks, stores the median as an offset in localStorage, and applies it to grading *anchors* (not event times, the click, or the cursor) via an anchor offset in the Rhythm-mode explicit anchor and `GradingOptions`.

**Acceptance criteria:**

- [ ] The measurement and the go/no-go decision are written into this plan's task notes with the numbers.
- [ ] If built: the offset persists across reloads, degrades to 0 when storage is unavailable, can be reset, and shifts only the grading anchors (D7); a unit test proves a constant simulated offset grades as on-time after calibration and as late before it.
- [ ] If built: the existing visual cursor and click timing are unaffected.

**Verification:**

- [ ] Manual: measurement protocol above, with the numbers recorded.
- [ ] If built: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~NoteReadingSessionTests|FullyQualifiedName~GraderTests|FullyQualifiedName~TempoFeedbackTrackerTests"` and `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`.

**Dependencies:** Task 7 (uses the click and feedback chip)

**Files likely touched (if built):**

- `PianoMapper.Web/wwwroot/js/timing-calibration.js` (new) and its test
- `PianoMapper.Web/Practice/BrowserTimingCalibrationStore.cs` (new)
- `PianoMapper.Core/Practice/GradingOptions.cs`
- `PianoMapper.Web/Pages/Piano.razor`

**Estimated scope:** M (S if closed by the gate)

**Status (2026-10-02): PENDING — measurement gate not taken, nothing built.** Step 1 needs the user's own MIDI keyboard and speakers (tap along to 16 clicks, record the median deviation, both with the browser piano sound and with the FP-10's own sound); an implementer cannot take it, and a mocked MIDI input has no output latency to measure. Per the decision gate the build is skipped until that number exists: if |median| < 25 ms close this task as "not needed", otherwise build "Calibrate timing" as described. Tasks 5-8 already give a usable way to take the measurement: choose Rhythm at a slow tempo with "Keep the click going while playing" on, tap along, and read the median in the beat/feedback chip ("N ms early/late · median …") in the exercise panel. Record the numbers here when measured. Task 16 and Task 18 reference the calibration offset only "if Task 9 shipped", so nothing downstream is blocked.

### Checkpoint B: Usable timing

- [x] Tasks 5–8 tests pass; Release build and JS tests are clean. (Task 9 has no code or tests: it is pending its measurement gate.)
- [ ] Manual with the MIDI keyboard: a complete Rhythm exercise at a slow tempo with click, beat flash, and live ms feedback. **Not performed on the physical keyboard.** A mocked-MIDI run in headless Firefox did complete a Rhythm exercise at 60 pulses/min with the click running throughout, both beat indicators pulsing together, and the chip reading "On time · 3/3 on time · median +1 ms".
- [ ] Task 9 decision recorded. **Pending the user's measurement** (see Task 9 status). Review with the user before Phase 3.

### Task 10: Draw quarter rests in exercise scores

**Description:** Fix F4 with the smallest vertical slice: draw the one rest value the shipped presets already emit (the Basic preset's quarter rest). The scene builder uses rests only for spacing and no rest glyph exists. Add a `Rest` glyph kind (append-only; its ordinal is mirrored in `canvas.js` and guarded by `scene-contract.test.mjs`) drawn with the same Unicode music-glyph approach as the fermata (`'Noto Music'`/`'Bravura Text'`), centered on the staff. Build the glyph mapping so other values slot in later; whole/half/eighth rests are added in Task 34 when patterns first need them. Gate behind a scene option that is on for generated exercise scores only, so imported-score rendering cannot regress (decided in scope assumptions).

**Acceptance criteria:**

- [x] A Basic-rhythm exercise draws a recognizable quarter rest at the right beat position on the correct staff; imported scores and exercises without rests render exactly as before.
- [x] `GrandStaffGlyphKind.Rest` is appended (ordinals stable) and mirrored in `canvas.js`; the contract and scene builder/cache tests cover it, including cache invalidation when the option toggles.
- [x] Verified in real pixels at the smallest clamped canvas height with a Basic 4/4 exercise (rest at beat 2 and at the end of a measure); no collision with noteheads, beams, or the annotation strip (lessons #23–#26).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~GrandStaffSceneCacheTests|FullyQualifiedName~GrandStaffSceneContractTests"`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/canvas.test.mjs PianoMapper.Tests/JavaScript/scene-contract.test.mjs`
- [x] Manual: before/after screenshots of a Basic-rhythm exercise showing the quarter rest.

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffGlyphKind.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneCache.cs`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/scene-contract.test.mjs`

**Estimated scope:** M

**Implementation note (2026-10-02):** Rests are drawn through the same height-scaled glyph path as the fermata: `GrandStaffGlyphKind.Rest` (ordinal 10, appended; mirrored as `restGlyphKind` in `canvas.js` and pinned in both contract tests), quarter-rest glyph U+1D13D, three staff spaces tall, centered on the staff's middle line at the rest's beat X. `GetRestGlyph` maps only the quarter value; other values are skipped (spacing only, as before) until Task 34 adds them. The option is `drawRests` on `BuildScore`/`BuildStaticScoreParts`/`GrandStaffSceneCache` (part of the cache key); the page passes it only while the exercise coordinator has a score, so imported scores are untouched (a test pins the default to no rest glyphs). **Pixel evidence** (headless Firefox, viewport 1000 px so the canvas sits at its smallest clamped height of 240 px, 16-note Basic 4/4 exercise): `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/t10-before-after.png` (top: same exercise with the rest glyph suppressed = the old unexplained gap; bottom: the quarter rest between the two quarter notes of measure 3, on the middle line, clear of noteheads/beams and the annotation strip). Limit: the only shipped pattern with a rest puts it on beat 2 (the second beat) — no preset places a rest at a measure end yet, so that case is not pixel-checked (it arrives with Task 34's eighth/half/whole rests).

### Task 11: Grading axes plus `PitchAndRhythm` and `RhythmOnly` modes

**Description:** Replace the scattered `mode is …`/`RequiresHoldValidation` checks with three axes derived once from the mode (D4) and expose that mapping publicly. Append two modes: `PitchAndRhythm` (pitch + onset, no release grading) and `RhythmOnly` (onset only; any key matches the next pending event, so a step with N events needs N distinct presses). Persisted names of existing modes do not change.

**Acceptance criteria:**

- [x] `NoteReadingMode` has a public axes helper: PitchAndOrder = Pitch; PitchAndHold = Pitch+Duration; PitchAndRhythm = Pitch+Onset; PitchHoldAndRhythm = all three; RhythmOnly = Onset; Off = none. `NoteReadingSession` uses it; the hold-tracking machinery runs only when Duration is graded.
- [x] `PitchAndRhythm` records onset verdicts without hold tracking; `RhythmOnly` accepts any pitch, never records a wrong pitch, and completes chord steps by press count. Release of an unfinished step un-matches it, as in Pitch-only.
- [x] All existing `NoteReadingSessionTests` pass unchanged; the idle-checking selector in `PracticePanel` is unchanged (new modes are exercise-only).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~NoteReadingSessionTests`
- [x] New tests: onset verdicts with an explicit anchor per new mode, any-key acceptance, chord steps in RhythmOnly, invalid-enum still throws.

**Dependencies:** Tasks 1, 2

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteReadingMode.cs`
- `PianoMapper.Core/Practice/NoteReadingSession.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `GradedAxes` (flags: Pitch, Onset, Duration) and `NoteReadingModeExtensions.GetGradedAxes` live in Core; `NoteReadingSession` now reads `RequiresHoldValidation`/`GradesOnset`/`GradesPitch` from them, and `SightReadingLabels.IsTimingGraded` derives from the same helper (it returns false for an undefined mode rather than throwing, because history can hold a numeric mode). `NoteReadingMode` gained `PitchAndRhythm` (4) and `RhythmOnly` (5) appended. In `RhythmOnly` a press consumes the step's next pending event and the session remembers which event each *pressed key* consumed (`matchedEventMidiByPressedKey`) so releasing an unfinished chord key un-matches exactly that event. In the non-hold path the stored verdict is now the onset verdict instead of always `Correct` (identical for pitch-only, where onset is always `Correct`), so `PitchAndRhythm` colors a late note. The exercise panel still lists only the three original modes (Task 12), the idle-checking selector is untouched, and the page keeps treating only `PitchHoldAndRhythm` as the count-in mode until Task 12 generalizes that. The only edit to an existing test is the labels test list gaining the two new modes (the labels test failing on an unlabeled member is exactly its purpose).

### Task 12: Compose rhythm-only exercises and add the mode ladder UI

**Description:** `RhythmOnly` exercises use one repeated centre-line pitch per staff (treble B4, bass D3 — verify stem/beam rendering), any rhythm preset including Fixed, no grand staff/chords/palette. Reorder the Mode select as a learning ladder with short descriptions: Pitch only → Rhythm only (tap on any key) → Pitch + rhythm → Pitch + hold → Pitch + hold + rhythm. Fix the existing silent fallback: choosing a variable rhythm with grand staff or chords currently produces quarter notes with no explanation; disable that control with a note instead.

**Acceptance criteria:**

- [x] Every `RhythmOnly` score contains a single pitch per staff, is deterministic for a seed, and rejects grand staff/chords with a clear exception (UI disables those controls and the Range select in this mode).
- [x] The panel lists the five modes in ladder order with one-line descriptions and shows "Tap this rhythm on any key" for Rhythm only; the Rhythm select is disabled with an explanation when grand staff or Chords is selected.
- [x] Verified in real pixels: rhythm-only Basic and Compound exercises render cleanly (noteheads, beams, rests from Task 10) at the smallest clamped canvas height.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~SightReadingLabels"`
- [ ] Manual (MIDI keyboard): tap a Basic rhythm on arbitrary keys and get onset verdicts only. **Physical MIDI keyboard / by-ear check NOT performed**; it was simulated with mocked Web MIDI in headless Firefox (see this task's implementation note).

**Dependencies:** Tasks 4, 10, 11

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** The composer rejects a grand-staff or Chords rhythm-only request with a plain `ArgumentException`; the *coordinator* (not the user) keeps that from happening: for `RhythmOnly` it generates with `FiveNote`, no grand staff, and exposes `IsPitchSetupIgnored` so the panel disables Range and Grand staff and says why. The silent quarter-note fallback is now explicit: `IsRhythmPresetLocked` (grand staff or Chords, except in rhythm only) disables the Rhythm select, the select shows the `EffectiveRhythmPreset` (Fixed), the panel explains it, and the history row/tempo default use the rhythm actually composed. `UsesCountIn` (onset-graded modes) replaces the page's hard-coded "is PitchHoldAndRhythm" count-in check; the page's status wording and tolerance-change reset were generalized to the grading axes (hold wording only when duration is graded). Mode labels were renamed to the ladder wording: the three-skill mode is now "Pitch + hold + rhythm" (was "Rhythm"), and one existing test was updated for it (`DescribeSession_RhythmEntryWithTempo_…`). History rows for rhythm only omit the unused pitch range. **Pixel evidence** (headless Firefox, 1000 px viewport so the canvas is at its smallest clamped height): `…/scratchpad/t12-rhythmonly-both.png` (treble B4: Basic with a quarter rest and beamed eighths, and Compound 6/8 beams) and `t12-rhythmonly-bass.png` (bass D3 with a rest) — noteheads, stems, beams and rests are clean and clear of the annotation strip; stems hang down from the middle line as engraving expects. Manual (mocked MIDI): a Fixed rhythm-only exercise tapped on one arbitrary key (E2) with one note 200 ms late completed as "pitch 8 of 8 first-try · 1 timing mistake(s)" and the history row read "Bass · Rhythm only · ♩ = 120". Not done on the physical keyboard.

### Task 13: Retry-missed keeps the rhythm in timed modes

**Description:** `ComposeFromMissedPrompts` flattens missed prompts into fixed quarter notes, so a rhythm mistake is retried without its rhythm. In onset-graded modes, retry-missed instead replays every *measure* that contains a miss (rhythm intact, rests included); pitch-only behavior is unchanged.

**Acceptance criteria:**

- [x] In PitchAndRhythm/PitchHoldAndRhythm/RhythmOnly, Retry-missed produces a score of the missed measures with their original note values, rests, beams, and key; tempo is preserved.
- [x] In Pitch-only/Pitch+hold, output is identical to today (existing tests unchanged).
- [x] Prompt/chord membership is preserved; recomputed measure indices and beat offsets (never copied from stale values).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`
- [x] Manual: miss a note in measure 2 of a Basic rhythm exercise, Retry missed, and see measure 2 with its rhythm.

**Dependencies:** Tasks 11, 12

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** New `SightReadingExerciseComposer.ComposeFromMissedMeasures(score, missedNotes)` replays each measure containing a missed note, once and in order, with measure indexes renumbered from zero (beat offsets are measure-relative so they stay valid), keeping note values, rests, beams, chord members, key, time signature and tempo; it throws for no missed notes or a note whose measure is not in the score. The coordinator's `RetryMissed` uses it when the mode is onset-graded (`UsesCountIn`) and the old flatten-to-quarters path otherwise (a test pins that). Manual (headless Firefox, mocked MIDI, Pitch + rhythm, Fixed quarter notes, 16 notes at 120): one note in measure 2 played 250 ms late; Review colored it orange (Late) and "Retry missed notes" drew the whole of measure 2 (four notes) rather than the single missed note (screenshot `…/scratchpad/t13-retry.png`). Basic-rhythm measure replay is covered by the composer and coordinator tests, not by a browser run, because the driver cannot read a Basic exercise's onsets. Observation for Task 9: the very first exercise after a fresh page load in headless Firefox was graded late throughout (16 of 16 timing mistakes at a +0 ms offset) while later exercises in the same page were on time (median +5 ms); headless audio start-up is the suspected cause, and it is the same mapping the Task 9 measurement should check on the real setup.

### Checkpoint C: Rhythm on its own

- [x] Tasks 10–13 tests pass; Release build clean. (Full .NET suite 1102 passed, JS 80 passed, Release build 0 warnings.)
- [ ] Manual: the ladder Pitch only → Rhythm only → Pitch + rhythm on the MIDI keyboard; rests visible; Retry-missed keeps rhythm. **Not performed on the physical keyboard.** Mocked-MIDI runs in headless Firefox covered Pitch only, Rhythm only (tapped on an arbitrary key), Pitch + rhythm and Pitch + hold + rhythm, the quarter rest in pixels, and Retry-missed replaying the whole measure (see Tasks 10, 12, 13).
- [ ] Review with the user before Phase 4. (Status is in the implementer's hand-off; the user has not signed off.)

### Task 14: `Grader` ignore-pitch option and `GradingResult` → prompt-result mapper

**Description:** Prepare the time-driven path. Add `GradingOptions.IgnorePitch` (any key matches; for RhythmOnly) and a Core mapper that turns a `GradingResult` plus the score's events into `NoteReadingPromptResult` lists (D6): group events into prompts with the same onset tolerance `NoteReadingSession` uses (extract that grouping into one shared helper rather than duplicating it), and keep only the outcomes the mode grades (D4). Wrong pitch captures the played pitch; Missed sets the missed flag; Extra notes are counted at session level.

**Acceptance criteria:**

- [x] `Grader` with `IgnorePitch` accepts any pitch within the onset window and never emits WrongPitch; existing `GraderTests` pass unchanged.
- [x] `NoteReadingPromptResult` gains `WasMissed` (`init`, default false). The mapper yields per prompt: pitch outcome (WrongPitch/Missed ⇒ miss), onset verdict, duration verdict (only if the mode grades duration), played wrong pitches, and response time null.
- [x] A contract test runs equivalent synthetic performances (on-time, late, wrong pitch, chord) through `NoteReadingSession` and through `Grader` + mapper and asserts equal pitch/onset outcomes.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GraderTests|FullyQualifiedName~PlayAlongResultMapperTests|FullyQualifiedName~NoteReadingSessionTests"`

**Dependencies:** Tasks 1, 11

**Files likely touched:**

- `PianoMapper.Core/Practice/GradingOptions.cs`
- `PianoMapper.Core/Practice/Grader.cs`
- `PianoMapper.Core/Practice/PlayAlongResultMapper.cs` (new)
- `PianoMapper.Core/Music/ScoreDerivation.cs` (shared onset grouping)
- `PianoMapper.Tests/UnitTests/GraderTests.cs`, `PianoMapper.Tests/UnitTests/PlayAlongResultMapperTests.cs` (new)

**Estimated scope:** M

**Implementation note (2026-10-02):** Shared grouping: `ScoreDerivation.GroupByOnset` is now the one place a prompt is formed (1e-9 beat tolerance); `NoteReadingSession` builds its steps from it and the mapper uses it. `PlayAlongResultMapper.Map(result, mode, tempo, anchor, options)` returns a `PlayAlongOutcome` (prompt results, extra-note count, per-verdict counts). Decisions: a prompt is "clean" only on the axes its mode grades (a too-short hold does not spoil a Pitch + rhythm prompt); `Missed` is a pitch miss in pitch-graded modes and an onset miss (`OnsetVerdict = Missed`, null deviation) in rhythm-only; `WrongAttemptCount` counts the prompt's events that were wrong on a graded axis, a missed event counting as one; a chord reports its worst onset; `DurationVerdict` is null when the grader never reached the duration check (early/late/wrong-pitch/missed notes), because `Grader.Classify` short-circuits before duration. The mapper groups the flattened events the practice session uses, so the session's hold-mode merging of overlapping same-pitch events is not mirrored (generated exercises have none). Cross-engine contract tests run on-time, late (+120 ms), early (-120 ms), a never-corrected wrong key and an on-time chord through both engines and assert equal pitch, onset verdict and deviation, and first-try outcome. A wrong key *followed by* the right key is deliberately not an equivalence case: the pitch-gated engine counts the wrong press as a mistake while the time-driven grader sees an extra note (that is the design: extras are session-level).

### Task 15: Coordinator pacing and unified results

**Description:** Add an `ExercisePacing` setting (WaitForMe default, PlayAlong) to the exercise coordinator. `Phase` becomes Review when the session completes (WaitForMe) or when `CompletePlayAlong(results, extraNotes, elapsed)` is called (PlayAlong). Prompt results, accuracy, review map, `HasMissedPrompts`, and `ConsumeCompletionSummary` read from one outcome view so history/review/retry work identically for both pacings. PlayAlong is valid only for onset-graded modes.

**Acceptance criteria:**

- [x] Pacing is a durable setting (not reset by Generate/Retry/Retry-missed/End); PlayAlong with a non-onset mode is rejected by the coordinator.
- [x] Phase transitions, completion summary (exactly once, with `Pacing = "playAlong"`), and retry/review behave the same for both pacings in tests.
- [x] Existing `SightReadingExerciseCoordinatorTests` pass unchanged.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseCoordinatorTests`

**Dependencies:** Tasks 3, 14

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Practice/ExercisePacing.cs` (new)
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** Interpretation of "PlayAlong with a non-onset mode is rejected": `Pacing` is the learner's durable preference and accepts any value; `EffectivePacing` is what runs and falls back to wait-for-me for a non-onset-graded mode (so choosing play-along, switching to Pitch only and back loses nothing); `CompletePlayAlong` throws `InvalidOperationException` unless a play-along exercise (onset-graded mode) is currently Active, so a non-onset mode or a second completion is rejected. The coordinator now owns one outcome view (`PromptResults`, `PromptCount`, `CompletedPromptCount`, `FirstTryCorrectCount`, `WrongAttemptCount`, `FirstTryAccuracyPercent`, `ElapsedTime`) that delegates to the session for wait-for-me and to the mapped outcome for play-along; `Phase`, `HasMissedPrompts`, `RetryMissed`, `BuildReviewFirstTryMap`, the pitch/timing counts and `ConsumeCompletionSummary` all read from it, and `Piano.razor` binds the exercise panel to it (same values in wait-for-me; `IsComplete` is now `Phase != Active`, identical for wait-for-me including the no-exercise state). `CompletePlayAlong(PlayAlongOutcome, elapsed)` takes the outcome record instead of the plan's three loose arguments (results + extras + counts travel together). The summary's `Pacing` is `"playAlong"` or null. No existing coordinator test changed.

### Task 16: Exercise play-along controller

**Description:** A testable `ExercisePlayAlongController` in `PianoMapper.Web/Practice` composes `BrowserPracticeCoordinator` and the exercise coordinator: start (count-in → running), update each tick, on Finished run the mapper and call `CompletePlayAlong`, abort without completing. `BrowserPracticeCoordinator.StartAsync` gains an option to skip its own count-in beeps (used when the metronome owns the count-in in Task 18). Grading options carry the tolerance, `IgnorePitch` for RhythmOnly, and the calibration anchor offset if Task 9 shipped.

**Acceptance criteria:**

- [x] Using the fake audio from `BrowserPracticeCoordinatorTests`: start → CountingIn → Running → Finished produces prompt results via the mapper and moves the coordinator to Review; abort returns to Idle without completing and leaves the exercise Active.
- [x] Next-key hints (`GetNextPitches`) are exposed for the opt-in amber keys while running.
- [x] No Razor/DOM/JS dependency in the controller (logic stays testable).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~BrowserPracticeCoordinatorTests|FullyQualifiedName~ExercisePlayAlongControllerTests"`

**Dependencies:** Task 15

**Files likely touched:**

- `PianoMapper.Web/Practice/ExercisePlayAlongController.cs` (new)
- `PianoMapper.Web/Practice/BrowserPracticeCoordinator.cs`
- `PianoMapper.Tests/UnitTests/ExercisePlayAlongControllerTests.cs` (new)
- `PianoMapper.Tests/UnitTests/BrowserPracticeCoordinatorTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `ExercisePlayAlongController(practice, exercise)` has `StartAsync(tolerance, playCountInClicks)`, `UpdateAsync` (returns whether the run is still going; on Finished it grades, maps via `PlayAlongResultMapper`, and calls `CompletePlayAlong` exactly once with the score's musical duration as elapsed time), `AbortAsync`, `GetNextPitches`, `IsRunning`, `CursorBeats`; it throws `InvalidOperationException` unless a play-along exercise is active. Grading options: the page's on-time tolerance, `IgnorePitch` for rhythm only; no calibration offset (Task 9 pending). `BrowserPracticeCoordinator.StartAsync` gained an overload taking a `PracticeRunOptions` (count-in clicks on/off, retain performed notes); the original overloads behave exactly as before. **Finding while building this (pre-existing, not changed):** `NoteTimeline` forgets *finished* notes 15 s after their release, and `BrowserPracticeCoordinator` regrades from that snapshot on every tick, so in loaded-score Practice a piece longer than about 15 s is graded with its early notes missing. Verified with a throwaway test (perfect performance of 20 quarter notes at 60 BPM, then deleted): the final result was 10 Correct and 10 Missed. A 16-note exercise at the 60-pulse default lasts 20 s, so play-along would have hit it; `RetainPerformedNotes` (on for exercise runs only, covered by a 20-note test and a slow 16-prompt controller test) keeps every note performed during the run. Loaded-score Practice is left as is; you may want a separate fix for it. **Superseded (2026-10-03):** the working tree no longer matches this note. `PracticeRunOptions` has only `ScheduleCountInClicks`, and `BrowserPracticeCoordinator` retains every performed note it has seen during a run for *every* run, loaded-score Practice included, so the 15 s loss described here no longer happens (see Follow-up bug fixes, Bug 1).

### Task 17a: Pacing select and Play-along start/abort/finish transitions

**Description:** Wire Play-along into the page without bloating `Piano.razor` (logic stays in the controller/coordinator). Add the Pacing select (Wait for me — default / Play along), disabled with a note for non-timed modes. Generalize the practice start/tick path to the exercise score without calling `sightReadingExerciseCoordinator.End()`; a finished run saves history through the existing completion hook; abort and focus loss return to an Active exercise with "Play-along stopped. Retry to try again."

**Acceptance criteria:**

- [x] Wait-for-me behavior is unchanged. Play-along runs count-in → moving cursor → Review without needing a correct key to advance; a skipped note is graded Missed and the run continues.
- [x] Generate/Retry/Retry-missed/End while running, and focus loss, leave no stuck notes, ticker, or click; history gets exactly one entry per finished run, tagged with pacing.
- [x] Loaded-score Practice behaves exactly as before (existing practice tests unchanged).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ExercisePlayAlongControllerTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~PracticeSessionTests"`
- [ ] Manual (MIDI keyboard): complete a 4/4 Basic play-along at 60 BPM; deliberately miss one note and confirm Missed + continuation + one saved history row. **Physical MIDI keyboard / by-ear check NOT performed**; it was simulated with mocked Web MIDI in headless Firefox (see this task's implementation note).

**Dependencies:** Tasks 4, 16

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** The page only wires and ticks: `StartSightReadingPlayAlongAsync` starts `ExercisePlayAlongController` from the existing Generate/Retry/Retry-missed transition when `EffectivePacing` is play-along, `TickSightReadingPlayAlongAsync` (on the existing practice ticker) updates it, redraws with the cursor and the practice engine's live verdicts, and saves history through the existing completion hook when the run finishes; `End()` is never called by the run itself. A finished run keeps the practice engine's verdict colors in review (`RefreshCanvasScene` falls back to `practiceCoordinator.GetVisibleVerdicts()` in a play-along review). Aborts reuse `AbortPracticeAsync`, whose status is "Play-along stopped. Retry to try again." for an exercise; Generate/Retry/Retry-missed/End already abort a running practice, and focus loss already aborted practice. The Pacing select is disabled with a note for modes not graded on the beat and shows the *effective* pacing; `ExercisePacing` is public only because component parameters must use public types. History rows add "Play along" for play-along sessions. Loaded-score Practice code paths are untouched (existing practice tests unchanged). Manual (headless Firefox, mocked MIDI, Pitch + rhythm, Fixed, 120 pulses, 8 notes): count-in then cursor then Review with no key needed to advance; the skipped 4th note was drawn grey (Missed) in review and the summary read "7 of 8 first-try correct · pitch 7 of 8 first-try"; the stored history grew by exactly one entry per finished run (13 -> 14) and not at all for a focus-loss abort, a Generate mid-run, or End mid-run; focus loss showed "Play-along stopped. Retry to try again." with the exercise still Active, and Retry then ran and completed normally. Not done on the physical keyboard.

### Task 17b: Completion summary with per-verdict counts and shared verdict labels

**Description:** The Play-along completion summary lists correct/early/late/too short/too long/missed/extra counts. Extract the verdict label helper that `PracticePanel` already defines into one shared place so the practice panel and the exercise summary cannot drift.

**Acceptance criteria:**

- [x] The exercise summary after a Play-along run shows each non-zero verdict count with the same labels as `PracticePanel`; Wait-for-me summaries are unchanged.
- [x] `PracticePanel` renders exactly as before using the shared helper.
- [x] The helper is unit-tested for every `Verdict` member (a new member without a label fails a test).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingLabels|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`
- [x] Manual: finish a Play-along run with one late, one wrong, and one skipped note; compare counts with what you did.

**Dependencies:** Task 17a

**Files likely touched:**

- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Components/PracticePanel.razor`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** S

**Implementation note (2026-10-02):** `SightReadingLabels.Verdict` replaces `PracticePanel`'s private `GetVerdictLabel` (wording identical for all eight verdicts, pinned member by member; an undefined verdict throws instead of falling back to `ToString()`); the exercise summary appends "Run: Correct 5 · Wrong pitch 1 · Late 1 · Missed 1" built by `DescribeVerdictCounts` (non-zero counts, verdict order) from `SightReadingExerciseCoordinator.PlayAlongVerdictCounts`, which is null for a wait-for-me run so those summaries are unchanged. **Bug found and fixed by the manual run:** the grader always judges duration, so the first version of the counts showed "Too short 5" for a *Pitch + rhythm* run that does not grade duration; the mapper now builds the run's counts from what the mode grades (a short note counts as Correct there; a test covers both modes). Manual (headless Firefox, mocked MIDI, play-along, Pitch + rhythm, 8 notes): one note 150 ms late, one wrong key, one never played — the summary read "5 of 8 first-try correct … 3 mistake(s) … pitch 6 of 8 first-try · 1 timing mistake(s) Run: Correct 5 · Wrong pitch 1 · Late 1 · Missed 1.", matching what was played. The Practice panel's own rendering was not re-inspected in the browser; its labels come from the same helper and are pinned by tests.

### Task 18: Click alignment and cursor following

**Description:** With `ClickWhilePlaying`, start the metronome with an explicit anchor equal to the play-along start (new `BrowserMetronome.StartAsync` overload) and disable the practice coordinator's own count-in beeps, so there is one click track. Verify the practice cursor drives the page window across the exercise's two-row layout.

**Acceptance criteria:**

- [x] The click grid anchor equals the practice start; clicks and the graded anchor never drift (tested with the fake clock), and with the setting off the practice's own count-in beeps are used as today.
- [x] The cursor sweeps the exercise score, the page window follows it, and the amber next-key hints (if opted in) track the upcoming notes.
- [x] Verified in real pixels at the smallest clamped canvas height with a 16-note two-row exercise.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~BrowserMetronomeTests|FullyQualifiedName~ExercisePlayAlongControllerTests"`
- [ ] Manual: play along to a click that starts exactly with the count-in; screenshot the moving cursor. **Physical MIDI keyboard / by-ear check NOT performed**; it was simulated with mocked Web MIDI in headless Firefox (see this task's implementation note).

**Dependencies:** Tasks 6, 17a

**Files likely touched:**

- `PianoMapper.Web/Audio/BrowserMetronome.cs`
- `PianoMapper.Web/Practice/ExercisePlayAlongController.cs`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/BrowserMetronomeTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `BrowserMetronome.StartAsync` and `ExerciseClick.StartAsync` gained overloads taking an explicit audio-clock anchor (used verbatim, no scheduling lead). `ExercisePlayAlongController` now takes the `ExerciseClick`: with `ClickWhilePlaying` on, the practice engine starts without its own beeps and the click starts at *practice anchor minus the one-measure count-in* (the run start the practice engine already computed, not a second clock read), so click beat *k* and graded beat *k* are the same instant (a test checks deviation < 1 microsecond over 12 beats, the only slack being tick rounding at 90 pulses); with it off the practice beeps its count-in as before. The controller stops the click when the run finishes or is aborted, and never stops a manual metronome the learner started meanwhile. The Task 16 controller tests were deliberately adapted: the fixture builds the controller with a click and a fake metronome, the "plays the practice count-in clicks" test now sets `ClickWhilePlaying(false)`, and the controller's `playCountInClicks` argument was removed because the setting decides. Manual/pixels (headless Firefox, 1000 px viewport so each canvas is at its smallest clamped height of 240 px, "measures per row" set to 3 so a 16-note Basic exercise spans two rows): `…/scratchpad/t18-sweep.png` shows four moments of one play-along run — the red cursor sweeps the upper row, the active row then switches to the lower row (the CURRENT/LOOK AHEAD tags swap) and the final review keeps the per-note colors and labels; the amber next-key highlight followed the upcoming notes (D4, then G4, then none after the run). Audio check by hooked oscillators: zero 880 Hz practice beeps and the metronome pattern 1760/1320/1320/1320/1760… from the first count-in beat, running through the run and off at review. The notes in this sweep were deliberately not played, so the grey notes are Missed. Not done by a human playing along to the click.

### Checkpoint D: Play-along

- [x] Tasks 14–18 tests pass; Release build and JS tests clean. (Full .NET suite 1181 passed, JS 80 passed, Release build 0 warnings.)
- [ ] Manual: a full play-along and a full wait-for-me run on the MIDI keyboard, both appearing correctly in history. **Not performed on the physical keyboard.** In headless Firefox with mocked MIDI, full play-along and wait-for-me runs both completed and appeared in history (play-along rows carry "Play along"; the stored history grew by exactly one entry per finished run and none for aborted runs); see Tasks 17a, 17b and 18.
- [ ] Review with the user before Phase 5. (Status is in the implementer's hand-off; the user has not signed off.)

### Task 19: Review mistake list with direction/interval wording

**Description:** Add a pure Core `PitchDistance.Describe(expected, played)` ("a step higher", "a third lower", "same note, an octave higher", "same note, sharp") from `DiatonicIndex` and alteration. In Review, list non-clean prompts: "Bar 2 · beat 3 — expected C4, played D4 (a step higher) · late by 85 ms". Names appear only in Review, never while Active. Cap the list (first 8 plus "and N more").

**Acceptance criteria:**

- [x] `PitchDistance` is covered for steps/skips/octaves, accidentals, enharmonic spellings, and treble/bass-agnostic pitches.
- [x] The review builder produces a line for wrong-pitch, late/early, too-short/long, and missed prompts, using bar/beat derived from the score's time signature; nothing is shown while Active.
- [x] Wording is covered by tests only at the stable-field level (distance category), not by asserting prose where avoidable.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~PitchDistanceTests|FullyQualifiedName~ExerciseReviewBuilderTests"`
- [x] Manual: finish an exercise with one wrong note and one late note; read the list.

**Dependencies:** Tasks 1, 4

**Files likely touched:**

- `PianoMapper.Core/Music/PitchDistance.cs` (new)
- `PianoMapper.Web/Practice/ExerciseReviewBuilder.cs` (new)
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/PitchDistanceTests.cs`, `ExerciseReviewBuilderTests.cs` (new)

**Estimated scope:** M

**Implementation note (2026-10-02):** `PitchDistance.Measure` returns a structured `PitchDifference` (kind: SameKey / Accidental / Interval / Octaves, direction, letter-position steps, accidental delta; `Octaves` and `IntervalSteps` derived) and `Describe` words it; the tests pin the structure and only the three phrases the plan quotes. Distances count staff positions (C4 to D#4 is a step), not semitones, and enharmonic respellings (C# vs Db, B#3 vs C4) are "the same key". Treble/bass independence is a test: the input has no staff. `ExerciseReviewBuilder` (pure) builds `ExerciseReview` (first 8 mistakes in prompt order plus a "more" count) from the unified prompt results; bar and beat come from each prompt's `OnsetBeats` and the time signature (6/8 counts eighth-note beats), a wrong key is described against the *nearest* expected pitch of the prompt (so a chord works), and a prompt that was never played reads "not played". `SightReadingExerciseCoordinator.ReviewMistakes` is null unless the phase is Review, which is what keeps note names out of an active exercise (tested, including after a mistake). The panel lists them under "Mistakes to look at". `ExerciseReview`/`ExerciseReviewLine` are public only because a component parameter must use public types. Manual (headless Firefox, mocked MIDI, Pitch + rhythm): no list during the run; after it, "Bar 1 · beat 3 — expected E4, played F4 (a step higher) · late by 209 ms" and "Bar 2 · beat 2 — expected D4 · late by 188 ms" (the late-by figures include the harness playing the corrected key after the wrong one). Screenshot `…/scratchpad/t19-review.png`.

### Task 20: Coach hints while playing

**Description:** An opt-in, durable `CoachHints` setting, **off by default** like the other answer-revealing aids (note names, fingering, next-key highlight), exposed as a "Coach hints" checkbox in the exercise panel's "While playing" group. When enabled, in Wait-for-me, after 2 wrong attempts on the current prompt the status line adds a direction hint from `PitchDistance` ("the note is a third lower than D4"); after 4 it names the note for that prompt in the status text ("It's C4 — find it and play it"). Hints never light keys (that remains its own opt-in), never persist as a reveal, and the prompt still counts as a first-try miss. Not shown in Play-along. Thresholds are named constants.

**Acceptance criteria:**

- [x] The setting defaults to off and is toggled by a "Coach hints" checkbox in the "While playing" group; with it off the status line is identical to today, however many wrong attempts occur. It is durable across Generate/Retry/Retry-missed/End.
- [x] With it on, thresholds and wording selection are tested at the coordinator level from `PromptResults` (`WrongAttemptCount`, `ExpectedPitches`, last wrong pitch).
- [x] Using a hint never changes first-try accuracy or mastery (the prompt was already a miss).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseCoordinatorTests`
- [x] Manual: with the checkbox off, repeated misses show no hint; with it on, miss the same prompt 2 and 4 times and read the status.

**Dependencies:** Task 19

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`

**Estimated scope:** S

**Implementation note (2026-10-02):** `SightReadingExerciseCoordinator.CoachHints` (default false, durable; "Coach hints" checkbox in the "While playing" group, disabled while play-along pacing applies) and `GetCoachHint()` return a structured `ExerciseCoachHint` (level None/Direction/Name, expected pitches, last wrong key, `PitchDifference`); `ExerciseCoachHintText` words it; thresholds are the constants `DirectionHintWrongKeyCount = 2` and `NameHintWrongKeyCount = 4`. One deliberate deviation from the plan's wording: the count is the number of wrong *keys* on the prompt (`WrongPlayedPitches`), not `WrongAttemptCount`, because that also counts late notes and a hint about which key to press is irrelevant to a timing miss (a test pins that timing-only mistakes never hint). The hint is null unless hints are on, the exercise is wait-for-me and Active, so it never appears in Review or play-along; it is only appended to the existing "… Try again." status text, lights no key, and reading it changes nothing (a test plays four wrong keys, then the right one, and checks first-try and pitch accuracy stay a miss). Manual (headless Firefox, mocked MIDI, Pitch only): with the checkbox off, five wrong keys in a row all read "E5 is not the highlighted exercise note. Try again." with nothing appended; with it on the second wrong key added "D5 is the right note in the wrong octave (same note, an octave lower)." and the fourth read "… It's D4 — find it and play it.".

### Task 21: Review-mark design spike (throwaway)

**Description:** The earlier plan deferred the canvas review treatment because reusing `Verdict` colors would surface live-grading meanings (and `verdictColors` is indexed by `Verdict` ordinal — D3). Build a throwaway prototype of 2–3 visual treatments (e.g., small ring/underline per notehead: clean / timing / pitch / missed), screenshot each on: single notes, a beamed eighth pair, a chord, ledger lines, and a rest-adjacent measure at the smallest clamped canvas height. No production code merges from this task. Use the `matt-prototype` skill.

**Acceptance criteria:**

- [x] Screenshots of each option on all five cases are saved under the scratchpad and summarized with a recommendation and its risks (lessons #23–#26 lane/annotation rules).
- [x] The user picks a treatment before Task 22a starts; the decision is recorded in this plan. **Decision (2026-10-02): the user picked A, the halo** (their words, after seeing the spike screenshots: "go with the halo for review marks"). See the decision note under Task 21's status below.
- [x] The prototype branch/files are discarded or clearly marked throwaway. (Scratchpad only, with `README-THROWAWAY.md`; nothing in the repo.)

**Verification:**

- [x] Manual: user review of the screenshots. (Done by the user on 2026-10-02: they looked at the Task 21 spike and chose the halo.)

**Dependencies:** Task 1

**Files likely touched:** Throwaway only (no production files)

**Estimated scope:** S

**Status (2026-10-02): spike DONE, decision MADE (A, halo, picked by the user on 2026-10-02); Tasks 22a and 22b follow.**

Question answered by the prototype: *what should a per-note review mark (Clean / Timing / Pitch / Missed) look like on the canvas?* Method: a throwaway script (marked `README-THROWAWAY.md`, scratchpad only, no production file touched) injected into the real running app records notehead positions from the canvas's own `ellipse` calls and draws the marks on a transparent canvas laid over each score canvas, so the real renderer, fonts and the smallest clamped canvas height (240 px, viewport 1000 px) are what is judged. Marks are assigned round-robin (clean, timing, pitch, missed, …) so every state appears in every case. Each case was played to Review in Pitch only, so the notes underneath carry their real review colors and the note-name strip is showing.

Screenshots (each image stacks: no marks / A / B / C), under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/proto-review-marks/`:

| Case | Image | What it contains |
|---|---|---|
| Single notes | `case1-singles-all.png` | two measures of quarter notes (F4–D4) |
| Chord | `case3-chord-all.png` | I/IV/V triads, including displaced seconds |
| Ledger lines | `case4-ledger-all.png` | C6 down to D4, notes above and below the staff |
| Rest-adjacent measure + beamed eighth pair | `case5-rest-and-beams-all.png` | Basic 4/4: a quarter rest between notes in measure 3 and a beamed eighth pair (with a ledger C4) in measure 2 |

(The beamed pair and the rest were captured in one exercise; the individual per-variant files `caseN-…-{off,A,B,C}.png` are in the same folder.)

The three treatments:

- **A — Halo.** A ring around the notehead. Shape-coded: solid orange = timing, thicker solid red = pitch, dashed grey = missed, no ring = clean.
- **B — Badge.** A small filled circle at the notehead's upper right holding an icon: check (clean), clock (timing), cross (pitch), dash (missed).
- **C — Tint.** The notehead itself is recolored (green / orange / red / grey), with a dark dash through a missed head.

Findings at the smallest height:

| | Readability | Collisions | Accessibility | Verdict |
|---|---|---|---|---|
| A halo | Clear at a glance; clean notes stay uncluttered | Ring crosses the stem base slightly; in chords per-note rings overlap into a tangle; rings of low ledger notes come close to the note-name strip | Shape (solid/thick/dashed) plus color | Best single-voice reading |
| B badge | Icons are ~12 px and barely legible; the most explicit when readable | Sits on the stem of stem-up notes and on beams; badges pile up on chords; high notes (C6) push badges toward the top edge of the canvas (lesson #35 headroom) | Icon plus color | Too small and too collision-prone |
| C tint | Lowest clutter; works on chords and beams | None (it is the notehead) | Color only, with one shape cue (the dash) for missed; it also hides the notehead's own review color | Safest rendering, weakest meaning |

**Recommendation: A (halo), with two rules for the production task.** (1) A chord gets one halo around the whole stack when its members share their prompt's mark (the plan already makes them share it), instead of a ring per head; (2) clean notes get no ring. Risks to carry into Task 22b: the ring radius must be clamped so it never reaches the annotation strip (lessons #23–#26) or crosses a beam; a dashed ring is only 3 px dashes at this size, so verify it still reads as dashed rather than dotted; the mark colors should be their own constants, not `verdictColors` (D3), and drawn only in Review. If a color-only treatment is preferred for simplicity, C is the fallback; B is not recommended.

Not decided here: which treatment ships (see the decision note that follows).

**Decision note (2026-10-02):** the user picked **A (halo)** after seeing these screenshots. Treatment carried into Tasks 22a/22b exactly as recommended above: one halo per chord (a chord's heads share their prompt's mark, so one ring encloses the stack), no ring for a clean note, mark precedence Pitch > Missed > Timing > Clean, drawn in Review only (never while Active), its own data channel and its own color constants (never `Verdict` or `verdictColors`, D3), and both pacings (Wait-for-me and Play-along) produce marks. Re-checked against the saved screenshots before starting: nothing in them contradicts these details. `case3-chord-all.png` shows the per-head rings of a chord overlapping into the tangle that rule 1 (one halo per chord) removes; `case1`, `case4` and `case5` show clean notes ringless and the solid orange (timing), thicker solid red (pitch) and dashed grey (missed) rings readable at 240 px. The spike did not cover accidentals, ties, or a hands-together prompt on two staves; Tasks 22a/22b handle those and record how (see their notes).

### Task 22a: Review-mark data channel, scene cache, and contract tests (no visual change)

**Description:** Add the separate review-mark channel decided in Task 21 (never a new `Verdict` — D3): an additive per-note mark (Clean / Timing / Pitch / Missed, precedence Pitch > Missed > Timing > Clean) derived from prompt results and carried on the scene note, populated only in the Review phase. The scene cache key includes the marks. Nothing is drawn yet, so this is safe to land and review on its own.

**Acceptance criteria:**

- [x] In Review each scene note carries the mark matching its prompt result for both pacings (chord members share their prompt's mark); in Active no marks are present.
- [x] `verdictColors` and `Verdict` ordinals are untouched; `scene-contract.test.mjs`, scene builder tests, and cache tests cover the new field, including cache invalidation when marks change.
- [x] Rendering output is pixel-identical to before for scenes without marks (existing `GrandStaffSceneBuilderTests` pass unchanged).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~GrandStaffSceneCacheTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/scene-contract.test.mjs`

**Dependencies:** Tasks 1, 21 (and Task 17a for the Play-along path)

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffNote.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneCache.cs`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02, third run):** The treatment is the user's pick from Task 21 (A, the halo). **Data model:** `ReviewMark` (`PianoMapper.Web/Rendering`, appended-only enum: Clean 0, Timing 1, Pitch 2, Missed 3) is a channel of its own on the scene note; `Verdict`, `verdictColors` and their contract test are untouched (D3). `ExerciseReviewMarks` (`PianoMapper.Web/Practice`, pure) turns the unified `PromptResults` into one mark per source note, so both pacings read the same input and chord members and tied source notes share their prompt's mark. **Precedence Pitch > Missed > Timing > Clean, and how "Pitch" is detected:** by the wrong keys played (`WrongPlayedPitches`), not by `IsPitchFirstTryCorrect`, because the play-along mapper also reports an *unplayed* prompt as a failed pitch (nothing was found) and that must read as Missed; a result that states no cause at all (the pre-split fused flag, `IsFirstTryCorrect = false` only) reads as Pitch, which is what it always meant; a prompt still in progress has no mark. `SightReadingExerciseCoordinator.BuildReviewMarks()` returns null unless the phase is Review (so never while Active; tested for wait-for-me and play-along, and after Retry). `GrandStaffNote` gained `ReviewMark` and `ReviewMarkGroup` (appended, default null): notes of one prompt on one staff share a group number, assigned in `GrandStaffSceneBuilder` from (notation staff, onset), so a chord gets one ring and a hands-together prompt one ring per hand, and the canvas never has to guess staves. `BuildScore`/`BuildStaticScoreParts` and `GrandStaffSceneCache.BuildScore` take `reviewMarks`; the cache compares marks by content (a new dictionary with the same marks reuses the geometry, a change or a return to none rebuilds it), and `Piano.razor` passes `BuildReviewMarks()` to both score rows. The cache's dictionary comparison was generalised from `VerdictsEqual` to a private `MapsEqual<TValue>` so verdicts and marks share it (behavior of verdicts unchanged). `BuildReviewFirstTryMap` (the old precursor, only used by tests) was left alone. **Tests added (30 .NET, 1 JS; no pre-existing test changed or deleted):** `ExerciseReviewMarksTests` (13: every precedence pair, the unplayed-prompt case, chord and tie sharing, in-progress prompt), 5 in `SightReadingExerciseCoordinatorTests` (null while active and before any exercise, wait-for-me wrong key, play-along skipped prompt, null again after Retry), 7 in `GrandStaffSceneBuilderTests` (mark carried and verdict untouched, none without marks, unmarked neighbour, chord group, one group per staff, **all geometry identical with and without marks**, marks outside the window ignored), 3 in `GrandStaffSceneCacheTests`, 2 in `GrandStaffSceneContractTests` (ordinals append-only; the verdict channel is not extended), and the ordinal constants in `scene-contract.test.mjs`. Red was seen first for each slice (compile error or failing assertion), then green. The page wiring was verified in the real page in Task 22b (a ring only appears if the marks reach the canvas).

### Task 22b: Draw review marks and pixel-verify

**Description:** Draw the treatment the user chose in Task 21 from the Task 22a channel in `canvas.js`, only in Review, staying clear of the annotation strip, stems, beams, and rests.

**Acceptance criteria:**

- [x] Review shows a visible mark per note matching its prompt result; Active shows none. (A clean note deliberately has no ring, as chosen in Task 21.)
- [x] Verified in real pixels on the Task 21 cases (single notes, beamed pair, chord, ledger lines, rest-adjacent measure) and, added by the user's instruction, an exercise with ties and accidentals, at the smallest clamped canvas height; no mark crosses the annotation strip or collides with stems/beams/rests. (Collisions that cannot be avoided are listed in the note below; a note's own stem, its own ledger line and a tie's first pixels meet its ring.)
- [x] `canvas.test.mjs` covers the mark drawing contract.

**Verification:**

- [x] `rtk node --test PianoMapper.Tests/JavaScript/canvas.test.mjs PianoMapper.Tests/JavaScript/scene-contract.test.mjs`
- [x] Manual: screenshots of a completed mixed-result exercise, saved and described. (Headless Firefox with mocked Web MIDI; see the note.)

**Dependencies:** Task 22a

**Files likely touched:**

- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`
- `PianoMapper.Web/wwwroot/css/app.css`

**Estimated scope:** M

**Implementation note (2026-10-02, third run):** `canvas.js` draws the halo in `drawGrandStaff` (the cached score layer, inside the clef clip), after the staff, glyphs and rests and **before** ties, noteheads, stems and beams, so a ring is always under the notation it surrounds (ledger lines are drawn last, as before). Treatment exactly as decided in Task 21: Timing = solid orange `#fb923c`, 2 px; Pitch = solid red `#f87171`, 3 px (thicker); Missed = dashed grey `#94a3b8`, 2 px, dash 6 / gap 4 (the first try, 3 / 4, read as dots on a ring this small, which was the risk the spike named; 6 / 4 reads as dashes); Clean has no style so no ring. These are the canvas's own constants (`reviewMarkStyles`, ordinals mirrored as `reviewMarkClean/Timing/Pitch/Missed`), never `verdictColors`. The ring is a rounded rectangle whose radius is half its short side (about a circle around one head, a pill around a chord stack), with `max(3 px, 0.4 staff space)` of clearance. **Geometry rules, each found by looking at real pixels:** (1) a note's own accidental is part of the ring (the ring wraps the glyph's box instead of cutting through it); (2) the annotation strip is a hard boundary: a ring never reaches into one (nor above the canvas top), keeping 1.5 px of air where there is room; the lowest treble note is only 3.8 px above its strip at 240 px, so there the ring hugs the head's lower edge and keeps 0.76 px (0.25 px minimum) instead, and never draws over the head itself; (3) beside another note the ring stays off that note's head and accidental, and shares the free space with a neighbour's own ring (two beamed eighths are only about 6.7 px apart at 916 px); (4) barlines and rests are obstacles too (a first or last note is about 4 px from a barline). The ring list is drawn per `reviewMarkGroup`, so a chord has one ring and a hands-together prompt one per hand. **Tests (11 JS, no pre-existing assertion changed):** one ring per marked note and none for a clean/unmarked one; scenes without marks or with only clean marks give identical canvas operations; shape coding (thick pitch, dashed missed, plain timing, three colors); a chord is one ring around all its heads; two staves at one beat get a ring each; the accidental is wrapped; the strip is respected and the head never cut; a neighbouring head is kept off and two neighbouring rings share the space; barlines and rests are kept off; rings are drawn before noteheads and ties; the ring uses the mark's color, not the verdict's. The fake canvas context in `canvas.test.mjs` gained a recording `roundRect` (and marks a round rectangle as stroked when it is stroked, since the annotation bands also use `roundRect` filled) and the file imports `verdictColors`; both are test-infrastructure additions only. Red was seen (10 of the 11 fail with the draw call removed), then green. **Pixel evidence** (all under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/`, headless Firefox, viewport 1000 px so the canvas is 916 x 240, the smallest clamped height, mocked Web MIDI, real app on `http://127.0.0.1:5199`): Wait for me: `t22b-wm-singles.png` (Pitch only, five wrong keys: five red rings that match the five lines of **Mistakes to look at**; clean notes ringless), `t22b-active-no-marks.png` (three mistakes made and the exercise still active: no ring), `t22b-wm-rhythm.png` (Pitch + rhythm, Fixed: orange timing rings and red pitch rings, clean ones ringless), `t22b-wm-chords.png`, `t22b-wm-ledger-bass.png` and `t22b-wm-hands.png` (montage `t22b-wm-montage.png`: chord stacks wrapped by one ring each, bass ledger-line notes, grand staff), `t22b-wm-hands-together.png` (one ring per hand on a hands-together prompt), `t22b-checkpointE.png` (Coach hints on, see Checkpoint E). Play along: `t22b-pa-basic.png` (Basic 4/4 with beamed pairs and a rest; one dashed grey ring per unplayed note, orange for late, red for wrong, matching "Run: Correct 8 · Wrong pitch 3 · Late 3 · Missed 3" and the list), `t22b-pa-rest.png` (every note marked beside a quarter rest; no ring touches the rest), `t22b-pa-ties-acc.png` and `t22b-m2.png` (Accidentals + Syncopated on one staff: rings that wrap flats and sharps, both ends of a tie ringed with the tie running between the rings). Zoomed crops are `*-z*.png` beside them. **Machine check of 23 real scenes** (`check.py`, which renders scenes dumped from the real builder, with a spread of mark patterns, in the real canvas module and measures each ring from the actual `roundRect` calls): ring count equals group count in every scene; at 916 x 240 no ring crosses a strip or the canvas edge or cuts into a head; the same holds at 1320 x 304 and 1800 x 304. **Limits found, not removable without breaking another rule, and recorded rather than hidden:** (a) a note's own stem leaves its own ring (the stem is drawn over the ring, so it stays intact), and its own ledger line crosses the ring (ledger lines are drawn last); (b) two *adjacent marked* eighth notes (6.7 px between heads at 916 px) can only have rings that touch each other; (c) a ring beside a barline touches it when the note is the last of a measure or a measure-leading accidental (the existing layout leaves the accidental about 2 px from the barline); (d) a tie starts about 1.7 px outside the head, so its first pixels meet the ring beside it (the tie is drawn over the ring and stays readable, see `t22b-pa-ties-acc.png`); (e) the lowest treble note with an accidental (C#4) has a sharp that already extends into the strip, so the ring, which must stop at the strip, crosses the tails of the sharp; (f) in the existing Accidentals + eighth layout a note's sharp can already overlap the previous note's stem, so that ring meets it too. The check also found that below about a 800 px wide canvas the notes and barlines are close enough that many rings touch barlines and stems; the 1000 px viewport is the narrowest checked as the plan asks. **Observation, outside this task, not changed:** the exercise settings (Mode, Pacing, ...) are not reset by choosing them while a review is showing, but `EffectivePacing` is derived from them, so changing the pacing select after a wait-for-me run flips the displayed exercise to Active/0 results while the canvas keeps its old rings until the next redraw; the live verdict colors behave the same way. **Superseded (2026-10-03):** fixed, see Follow-up bug fixes, Bug 2. Limit (f) above (a sharp or flat overlapping the previous note's stem in Accidentals + eighths) is fixed too, see Bug 3.

### Checkpoint E: Mistakes you can see and understand

- [x] Tasks 19–22 tests pass; Release build and JS tests clean. (Full .NET suite 1570 passed, JS 93 of 93, Release build 0 errors and 0 warnings, formatter clean.)
- [x] Manual: an exercise with a wrong note, a late note, and a missed note shows the list, hints, and marks consistently. **Mocked-MIDI pass, not the physical keyboard, and split across the two pacings by design:** hints exist only in Wait for me and a missed note only in Play along, so no single exercise can show all three. Wait for me with Coach hints on (`t22b-checkpointE.png`): after 2 wrong keys on one prompt the status read "… The note is a step higher than B3." and after 4 on another "… It's F4 — find it and play it."; the list named exactly those two prompts and exactly those two notes carry a red ring. Wait for me with notes played late (`t22b-wm-rhythm.png`) and Play along with wrong, late and skipped notes (`t22b-pa-basic.png`, `t22b-pa-rest.png`): every line of the list has a ring of the matching kind, and every ring has a list line (the list is capped at 8 lines, the rings are not).
- [ ] Reading the marks on the physical keyboard with a real beginner's sense of "at a glance". **NOT performed** (needs a person and the physical keyboard).
- [ ] Review with the user before Phase 6. (Status is in the implementer's hand-off; the user has not signed off.)

### Task 23: Capture speed, confusions, and staff in summaries

**Description:** Fill the additive v2 fields from prompt results (D2, no version bump): per note a `NoteAttemptSummary` (Pitch, Staff, attempts, pitch first-try correct, response samples, total response milliseconds) and a capped list of `ConfusionSummary` (expected, played, count; top 20 per session). `PitchAttempts` stays populated for older readers. Staff comes from the prompt's source notes (explicit in generated scores).

**Acceptance criteria:**

- [x] Summaries from synthetic prompt results contain staff, response-time totals (null in onset-graded modes) and confusions; v1 entries and v2 entries lacking these fields still parse.
- [x] A payload-size test bounds a worst-case session so 100 sessions stay well under localStorage limits.
- [x] JSON round-trip is lossless.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingSessionSummaryTests|FullyQualifiedName~SightReadingHistoryTests"`

**Dependencies:** Tasks 1, 3

**Files likely touched:**

- `PianoMapper.Core/Practice/SightReadingSessionSummary.cs`
- `PianoMapper.Core/Practice/NoteAttemptSummary.cs` (new)
- `PianoMapper.Core/Practice/ConfusionSummary.cs` (new)
- `PianoMapper.Tests/UnitTests/SightReadingSessionSummaryTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `NoteAttemptSummary(Pitch, Staff, AttemptCount, PitchFirstTryCorrectCount, ResponseSampleCount, TotalResponseMilliseconds?)` and `ConfusionSummary(Expected, Played, Staff, Count)` are additive nullable lists on the v2 summary (`NoteAttempts`, `Confusions`, no version bump; older entries parse with null). **Deviation:** `ConfusionSummary` also carries the *staff* of the expected note (the plan listed expected, played, count) because Task 26's "on bass notes" wording needs it. A chord's members each get their own staff (from the prompt's source note with that pitch) and the prompt's shared response time; the response total is null when no prompt had a response time (onset-graded modes). A wrong key is filed against the *nearest expected pitch* of its prompt. Confusions are most-frequent-first (ties by expected, played, staff), capped at `MaximumConfusions` = 20. **Payload:** the worst case (16 all-distinct 3-note chord prompts, 20 distinct confusions, 100 sessions) first serialized to 2.37 MB because every stored pitch also wrote its derived MIDI number, frequency and staff position; history storage now writes only letter/alter/octave (a `TypeInfoResolver` modifier on the history's own `JsonSerializerOptions`; the score wire format is untouched, and older entries with the extra members still read). It now measures about 1.50 MB for 100 worst-case sessions (a typical single-note session is roughly a tenth of that) and the test bounds it at 2 MB, about 40% of a 5 MB origin quota. Round trip of the new members and of the checked-in v1 fixture is covered.

### Task 24: Staff- and speed-aware note mastery model

**Description:** Add a `NoteMastery` model (Pitch, optional Staff, attempts, pitch accuracy, median response time) and a documented `WeaknessScore` with named constants: lower accuracy ⇒ higher weakness; slower than the learner's own median ⇒ higher weakness; fewer than `MinimumMasteryAttempts` ⇒ excluded. Entries with unknown staff (v1) contribute to a staff-agnostic bucket merged on lookup; legacy conflated entries stay excluded (D2). Keep `PitchMastery` as a thin compatibility wrapper until callers migrate in Tasks 25–26.

**Acceptance criteria:**

- [x] Tests assert monotonicity (accuracy down ⇒ weakness up; slower ⇒ weakness up), exclusion below the attempt threshold, and staff separation (C4 weak on bass, strong on treble).
- [x] v1-only history yields the same pitch ordering as the current `ComputeMasteryWeakestFirst`.
- [x] No behavior change to generation yet.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingHistoryTests`

**Dependencies:** Task 23

**Files likely touched:**

- `PianoMapper.Core/Practice/NoteMastery.cs` (new)
- `PianoMapper.Core/Practice/SightReadingHistory.cs`
- `PianoMapper.Core/Practice/PitchMastery.cs`
- `PianoMapper.Tests/UnitTests/SightReadingHistoryTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `NoteMastery(Pitch, Staff?, AttemptCount, CorrectFirstTryCount, MedianResponseTime?, WeaknessScore)` with `NoteMastery.CalculateWeakness`: weakness = the fraction of first-try misses + a capped slowness part, `min(MaximumSlownessWeight = 0.5, max(0, typicalMultiple - 1) * SlownessWeightPerExtraMultiple = 0.25)`, where the multiple is the note's median response over the learner's own typical time; being faster than typical earns nothing, so speed can raise weakness but never hide a miss, and without response times the score is accuracy alone. Because summaries store per-note response *totals* and sample counts (not the individual samples), a note's `MedianResponseTime` is the median of its per-session averages, and "the learner's own median" is the median over notes of those medians (each note counts once). `SightReadingHistory.ComputeNoteMastery` / `ComputeNoteMasteryWeakestFirst` (excludes notes under `MinimumMasteryAttempts`, highest weakness first, ties by pitch then staff) / `GetNoteMastery(pitch, staff)`: v1 and staff-less data is a staff-agnostic bucket merged into each staff of that pitch (and returned alone, with a null staff, when a pitch has no staff-specific data); legacy timed-mode entries stay excluded. `ComputeMastery`/`ComputeMasteryWeakestFirst`/`PitchMastery` are untouched (the thin staff-merged view for the callers Tasks 25–26 migrate); a test checks a v1-only history orders pitches exactly as before. No generation behavior changed.

### Task 25: Staff-aware weights and weakness-first drill generation

**Description:** The composer takes weights per (pitch, staff) so a pitch weak on one clef is not boosted on the other. Add an explicit generation strategy: `CoverageFirst` (today's default, unchanged) and `WeaknessFirst` (weighted sampling without the coverage-first rule, no immediate repeat, leap limit respected, and the weakest three pitches guaranteed ≥ 2 appearances). The drill only draws from the current preset's palette.

**Acceptance criteria:**

- [x] `CoverageFirst` with the same seed reproduces today's sequences (existing composer tests unchanged apart from the weights parameter type).
- [x] `WeaknessFirst` meets its invariants over many seeds: palette bounds, no adjacent repeat, leap limit, weakest-three minimum appearances, and measurably more weak-pitch appearances than `CoverageFirst` at 8 and 16 prompts (the original shortfall, F6).
- [x] Grand-staff weights reach the right palette (a bass-only weak C4 does not inflate treble C4).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingExerciseCoordinatorTests"`

**Dependencies:** Task 24

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `Compose` now takes `IReadOnlyDictionary<(Pitch, Staff), double>` and a new `SightReadingExerciseOptions.Strategy` (`CoverageFirst` default, `WeaknessFirst`). **Deliberate changes to existing tests:** the five composer weight tests (and one empty-weights case) only changed the dictionary key type to `(pitch, Staff.Treble)`; two coordinator mastery tests build `NoteMastery` instead of `PitchMastery` (weakness 1.0, the same weight of 5.0 as before). A golden test pins four CoverageFirst sequences captured from the composer *before* this change (single staff, bass one-octave, grand staff, and a weighted one), so default generation is demonstrably unchanged. `WeaknessFirst`: each prompt is drawn from the leap- and no-repeat-legal candidates in proportion to `weight^2` (`DrillWeightEmphasis`), then the weakest three notes (weighted above neutral; only as many as fit two appearances each) are raised to at least two appearances by swapping other prompts where both neighbours stay legal; ranges with required groups (ledger lines) retry until the groups are present, and after 200 attempts the drill falls back to the always-valid coverage-first sequence. Measured over 300 seeds with one weak note (G4, weight 5): 8 prompts 1.98 -> 3.75 (five-note) and 1.31 -> 3.80 (one-octave) appearances, 16 prompts 5.32 -> 7.38 and 5.10 -> 7.48, which is why the emphasis exists: with plain proportional sampling the 16-prompt gain was only about 3%. Invariants (palette bounds, no repeat, leap limit, weakest-three minimums) are tested over 300 seeds at 8 and 16 prompts. Grand-staff weights reach the right palette (a bass-only weight leaves the treble prompts identical to unweighted generation for the same seed). `SightReadingExerciseCoordinator.Generate` takes `NoteMastery` (staff-less data weights both staves, a staff-specific entry wins; weight = 1 + weakness x 4) and `GenerateWeaknessDrill(random, tolerance, mastery)` is the explicit drill; the next ordinary `Generate` is coverage-first again. The page now passes `ComputeNoteMasteryWeakestFirst()`.

### Architecture implementation note (2026-10-02): Exercise presentation module

The user selected the architecture review's top recommendation and it is now implemented without changing learner-facing behavior. `SightReadingExercisePanel` has one render-state interface, `SightReadingExercisePresentation`, and one typed learner-intent interface, `EventCallback<SightReadingExerciseAction> OnAction`; this replaced the former 29 state parameters and 17 callback parameters. `Piano.razor` is the page adapter: it builds the presentation from the coordinator plus browser-owned MIDI, audio-click, and tempo-feedback state, and `HandleSightReadingExerciseActionAsync` routes each typed action to the existing coordinator or to the existing asynchronous transition. The panel owns input parsing/validation and rendering only; audio, tickers, canvas refresh, score-window changes, and history persistence remain at the page seam.

When extending the exercise panel (notably Task 26), do **not** add a standalone parameter/callback pair. Add an explicit property to `SightReadingExercisePresentation` for new render state and/or a nested action record to `SightReadingExerciseAction`, then handle that action in `Piano.razor`. The record's `required` properties make omitted state a compile-time error. The former thin `SetSightReading...` page methods were deliberately removed so action routing stays local. Verification after this refactor: full .NET tests, JavaScript tests, Release build, and formatter check.

### Task 26: Progress insights and drill entry point

**Description:** Surface the new data. The Progress panel shows weak spots by (pitch, staff), the commonest confusions in plain words ("often plays a step higher than written on bass notes" via `PitchDistance`), speed (median response and notes/min for self-paced sessions), and a simple trend of recent pitch accuracy and timing-clean rate. The exercise panel gets a **Drill my weak notes** button, enabled when the current range contains weak notes.

**Acceptance criteria:**

- [x] Insights render from history only (no new persistence); empty/insufficient data shows the existing "not enough attempts" copy.
- [x] The drill button generates a `WeaknessFirst` exercise for the current options; disabled with a reason when there is nothing weak.
- [x] Insight text builders are unit-tested at the data level (which pitches/confusions are selected), not by asserting prose.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~SightReadingHistoryTests"`
- [x] Manual: after several sessions with deliberate errors, the weak spots and drill reflect them.

**Dependencies:** Tasks 19, 24, 25

**Files likely touched:**

- `PianoMapper.Web/Components/SightReadingHistoryPanel.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Practice/SightReadingInsights.cs` (new)

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingInsights.Build(history)` (pure, history-only, in `PianoMapper.Web/Practice`) returns a `SightReadingInsightReport`: **weak spots** = notes by pitch and staff with weakness at least `WeakNoteThreshold` (0.2), worst first, at most 5; **habits** = wrong-key patterns grouped by staff and written-to-played distance (so "a step higher on bass" pools across different notes and sessions), at least 3 occurrences, top 3; **speed** = median of the self-paced sessions' average response times plus notes per minute (clock-paced sessions are ignored); **trend** = the last 10 sessions' pitch accuracy (and timing-clean rate for timed ones) oldest first, with a direction from comparing the newer half with the older half (5-point threshold, 4 sessions minimum); legacy timed-mode entries with fused outcomes are left out. All of it is unit-tested at the data level; the words live in `SightReadingInsightText` (e.g. "often plays a step higher than written on bass notes", built from `PitchDistance.Describe(PitchDifference)`, a new overload). The Progress panel shows a "Weak spots and habits" section and keeps the existing "Not enough attempts yet to rank individual pitches." copy when there is nothing to report. **Drill:** `SightReadingExerciseCoordinator.GetDrillAvailability(weakNotes)` (not chords, not rhythm only, at least one weak note inside the current range on a staff the exercise uses, using the new public `SightReadingExerciseComposer.GetRangePitches`) feeds the "Drill my weak notes" button, which is disabled with the reason shown next to it; the button runs `GenerateWeaknessDrill`. History-derived values are recomputed once per history change, not per render. **Found in the working tree when resuming (not part of the plan):** the exercise panel's roughly 35 parameters and callbacks had been regrouped into one `SightReadingExercisePresentation` record plus a `SightReadingExerciseAction` stream handled by `Piano.razor` (new files `Components/SightReadingExercisePresentation.cs` and `SightReadingExerciseAction.cs`, plus the panel and page wiring); behavior is unchanged, the build and all tests are green and the panel was re-verified in the browser, but it is beyond "surgical" and can be backed out separately. Manual (headless Firefox, accumulated headless-profile history, not the physical keyboard): the Progress panel listed the weak notes D4/G4/E4 with first-try percentages and the pitch-accuracy trend; the drill button was enabled for the five-note treble range, disabled with "Chord exercises cannot be drilled note by note." for Chords, and with "No weak notes in this range yet…" for the bass staff; the generated drill played as "G4 E4 D4 E4 G4 E4 D4 E4" (only the three weak notes, each at least twice, no repeats) against an ordinary exercise "F4 E4 G4 E4 C4 D4 F4 D4".

### Checkpoint F: Adaptation is real

- [x] Tasks 23–26 tests pass; Release build and JS tests clean. (Full .NET suite 1314 passed, JS 80 passed, Release build 0 warnings.)
- [x] Manual: a deliberately weak pitch is over-served in the drill and not in the default exercise. (Headless Firefox with the headless profile's real accumulated history: the drill played only the three weak notes, each at least twice; an ordinary exercise played all five notes. Unit tests also show the drill serving a weak note 1.4-1.9x as often as coverage-first at 8 and 16 prompts. Not done on the physical keyboard.)
- [ ] Review with the user before Phase 7. (Status is in the implementer's hand-off; the user has not signed off.)

### Task 27: Level ladder and tempo ladder (Core)

**Description:** A static, data-driven `ExerciseLevelCatalog` plus a pure `LevelProgression` over history (D9). Initial beginner ladder: (1) Five notes · Treble · Pitch only → (2) Bass → (3) Grand staff → (4) One octave treble → (5) One octave bass → (6) Rhythm only · quarter notes @60 → (7) Rhythm only · Basic 4/4 → (8) Five notes · Pitch + rhythm · Basic @60 → (9) G major → (10) F major → (11) Ledger lines → (12) Chords → (13) 6/8 rhythm-only then pitch + rhythm → (14) Pitch + hold + rhythm @70. New presets from Phases 8–9 append levels when they ship. Pass rule (constants in one record): last 3 completed matching sessions each ≥ 90% pitch first-try (and ≥ 80% timing-clean for timed levels). Tempo ladder: for timed levels, recommend last-passed tempo + 5 (capped by the level's maximum) or −5 after two sessions below 60%.

**Acceptance criteria:**

- [x] No history ⇒ Level 1 recommended; three passing matching sessions ⇒ next level; non-matching sessions are ignored; tempo steps up and down as specified.
- [x] A catalog test composes every level's options through `Compose` so an invalid combination cannot ship.
- [x] Pure Core, no UI or storage dependency.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~LevelProgressionTests`

**Dependencies:** Tasks 3, 5, 12, 23

**Files likely touched:**

- `PianoMapper.Core/Practice/ExerciseLevel.cs` (new)
- `PianoMapper.Core/Practice/ExerciseLevelCatalog.cs` (new)
- `PianoMapper.Core/Practice/LevelProgression.cs` (new)
- `PianoMapper.Tests/UnitTests/LevelProgressionTests.cs` (new)

**Estimated scope:** M

**Implementation note (2026-10-02):** Core types: `ExerciseLevel` (options plus start/maximum tempo for timed levels), `ExerciseLevelCatalog.Levels`, `LevelProgressionRules` (the one record holding every threshold: 3 sessions, 90% pitch, 80% timing-clean, tempo step 5, struggle below 60% twice), `LevelProgression.Evaluate(history, rules?)` returning a `LevelProgressionReport` (per-level `LevelProgress` with `LevelStatus` Passed / Recommended / InProgress / NotTried, window size, averages, recommended tempo; no "locked" state). **Decisions:** (1) the plan's level 13 ("6/8 rhythm-only then pitch + rhythm") is two levels, so the catalog has **15** levels, not 14: 13 = Rhythm only 6/8, 14 = Pitch + rhythm 6/8, 15 = Pitch + hold + rhythm @70; (2) levels 9-12 (G major, F major, ledger lines, chords) are Pitch only, and the three rhythm levels before them use the Treble staff; (3) only schema-version-2 sessions with a known layout, rhythm and pitch outcome can match a level — v1 history is ignored (a v1 grand-staff session stored an arbitrary staff, so matching it would be a guess); (4) the *recommendation* is the first level not yet passed, while a level the learner passed out of order still shows Passed; (5) the "timing clean" rate is prompts minus timing mistakes, except in rhythm-only where it is the share of fully clean prompts (a skipped note is not a stored timing mistake but is not clean either); (6) tempo: struggle (the two latest tempo-recorded sessions both under 60% clean) steps down 5 from the latest tempo and never below 30; otherwise one step above the latest *passing* session's tempo, capped at the level maximum; with no pass yet it stays at the latest tempo, and with no session it is the level's start tempo (maximums: 100 for 4/4 levels, 70 dotted-quarter pulses in 6/8, 110 for the hold level). A catalog test composes every level at its start and maximum tempo through the real composer. Pure Core, no UI or storage.

### Task 28: Ladder UI and "Start recommended exercise"

**Description:** Show the ladder in the Progress panel (passed / recommended / not yet tried — never "locked") with the reason ("Passed Five notes · Treble at 94% over 3 sessions"). The exercise panel gets **Start recommended exercise**, which applies the level's options (including the recommended tempo) via a coordinator method and generates. All presets remain freely selectable.

**Acceptance criteria:**

- [x] The recommended exercise matches `LevelProgression` output in a coordinator test; applying it sets staff/preset/mode/rhythm/tempo and generates.
- [x] No control is disabled by level state; manual selection is never overridden.
- [x] The ladder renders with and without history.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~LevelProgressionTests"`
- [x] Manual: clear history ⇒ recommended is Level 1; complete Level 1 three times with ≥ 90% ⇒ Level 2 is recommended.

**Dependencies:** Tasks 4, 27

**Files likely touched:**

- `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`
- `PianoMapper.Web/Components/SightReadingHistoryPanel.razor`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Pages/Piano.razor`

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingExerciseCoordinator.GenerateRecommended(random, tolerance, recommendation, mastery)` sets only the settings that define a level (staff, grand staff, range, length, mode, rhythm and, for a timed level, the recommended tempo) and then generates; pacing, the reveal and hint options and the click are left as the learner set them, and every setting stays changeable afterwards (tests cover both, plus grand staff, 6/8 dotted-quarter tempo, an untimed level not touching a chosen tempo, and that the applied level equals what `LevelProgression` recommends for a three-session history). No control is disabled by level state: the Progress panel's "Guided path" lists all 15 levels (passed / next step / in progress / not tried, with a reason line such as "Passed Five notes · Treble · Pitch only at 100% over 3 sessions" or "Next: … · try ♩ = 65"), and the exercise panel's "Start recommended exercise" button plus a one-line description of the next step sit beside the ordinary Generate. `aria-current="step"` marks the next step. Manual (headless Firefox, mocked MIDI; this headless profile's history was cleared first): with no history Level 1 was "Next step" and the other 14 "not tried yet"; after "Start recommended exercise" was played perfectly three times, Level 1 read "Passed … at 100% over 3 sessions", Level 2 became the next step, and the next "Start recommended exercise" set Staff = Bass. Not done on the physical keyboard and not judged by the user ("does the recommendation feel right for a real beginner session" is Checkpoint G's open item).

### Checkpoint G: Guided path

- [x] Tasks 27–28 tests pass; Release build clean. (Full .NET suite 1353 passed, JS 80 passed, Release build 0 warnings.)
- [ ] Manual: the recommendation feels right for a real beginner session (the user's). **Pending the user.** What was checked mechanically in headless Firefox: no history recommends Level 1; three perfect Level 1 runs pass it and recommend Level 2; "Start recommended exercise" applies the level's settings.
- [ ] Review with the user. Phases 8–9 are à la carte after this. (Not started, as instructed; status is in the implementer's hand-off.)

### Task 29: Melodic and intervallic motion

**Description:** Add a `Motion` option: `Random` (today), `Melodic` (stepwise runs with direction persistence, ~25% repeated notes, occasional skips ≤ a third), and `Intervallic` (each note exactly ±N diatonic steps from the previous, N = 1–4, direction chosen to stay in the palette). Coverage-first and mastery weights apply to `Random` only (documented). UI: a "Pattern" select.

**Acceptance criteria:**

- [x] Invariants over many seeds: `Melodic` contains repeats and respects palette bounds; `Intervallic` leaps are constant at N; seeded determinism holds; `Random` output is unchanged.
- [x] Palette/preset/key-signature spelling rules still hold (key presets stay correctly altered).
- [x] History records the motion; the ladder ignores it unless a level specifies it.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`

**Dependencies:** Task 25 (avoid composer conflicts)

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingMotion.cs` (new)
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingMotion` (Random, Melodic, Intervallic; persisted by name in history) and two options appended to `SightReadingExerciseOptions` with defaults (`Motion`, `IntervalSteps` = 2, allowed 1-4; the composer throws for any other size when the motion is intervallic). The plan says "a Pattern select" but intervallic motion needs N, so the panel also shows an "Interval" select (Seconds/Thirds/Fourths/Fifths) only while Pattern = Same interval; this is the one UI addition beyond the plan's wording. `Compose` now dispatches per staff: Random is the old generator unchanged (the existing generator was only renamed `ComposeRandomPitches`; the golden-sequence test still passes untouched), Melodic is a walk with a 25% repeat chance, a 25% chance to turn instead of keeping direction, a 20% chance of a skip of a third and a bounce at either end of the palette, Intervallic starts on a note that has a legal move and then picks at random among the directions that stay inside the palette (the way it came is always a legal way back, so it cannot get stuck). Both ignore mastery weights and the weakness strategy (the plan's "Random only" rule). **Scope decision (recorded, not in the plan's wording):** motion only applies to ranges that pick plain notes, `SightReadingExerciseComposer.SupportsMotion`: not Chords (whole triads) and not Ledger lines (its required below/above-the-staff coverage cannot be guaranteed by a run of steps), and not rhythm only. The coordinator makes that explicit like the rhythm lock: `IsMotionAvailable`/`EffectiveMotion`, the Pattern select is disabled with a note, and history records the *effective* motion (`Motion` is an additive v2 member, no version bump; the history row names a non-random motion). The weak-note drill is unavailable with a reason while a non-random pattern applies, because a drill picks notes by miss rate. `GenerateRecommended` leaves the learner's motion alone, and the ladder ignores motion (a Melodic session counts toward the level of its range; pinned by a test). No pre-existing test was changed. Invariants are tested over 100-300 seeds: palette bounds, melodic moves of at most a third with about a quarter repeats and some skips and a majority of direction-keeping moves, intervallic leaps exactly N in both directions, determinism, key-signature spelling for G and F major, grand-staff palettes per staff, and the variable-rhythm path. Not rendering-affecting (same glyphs as before), so no pixel check was needed beyond the Pattern select in the page (see the Task 30 browser run).


### Task 30: D major, B♭ major, A minor

**Description:** Append `DMajor`, `BFlatMajor`, and `AMinor` presets using the existing circle-of-fifths helper. A minor is natural minor with no accidentals (harmonic/melodic variants are out of scope). Choose each preset's range so every pitch sits within the staff plus at most two ledger lines.

**Acceptance criteria:**

- [x] Palettes are correctly spelled against 2 sharps / 2 flats / 0 and never print a redundant accidental; new `SightReadingPresetId` members are appended.
- [x] Labels exist for each (the Task 4 label test enforces it).
- [x] Verified in real pixels: key signatures and notes render correctly on both staves at the smallest clamped canvas height (lessons #5–#7).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingLabels"`
- [x] Manual: screenshots of each key on treble and bass.

**Dependencies:** Task 4

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingPresetId.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `DMajor`, `BFlatMajor`, `AMinor` are appended to `SightReadingPresetId` and reuse `BuildKeySignaturePalette` (so G/F major are untouched): D major = 2 sharps, B-flat major = 2 flats, A minor = natural minor with no key signature and no accidentals (the same eight notes as C major from A). Ranges follow the existing key presets (tonic to tonic, octave 4 on treble and octave 2 on bass): D4-D5 / D2-D3, Bb4-Bb5 / Bb2-Bb3, A4-A5 / A2-A3; the most any note needs is one ledger line (D2/E2 on the bass for D major, A5/Bb5 on the treble for B-flat major), checked by a test. Labels: "D major (two sharps)", "B♭ major (two flats)", "A minor (natural minor)"; the score title uses "B-flat major" in ASCII. **Ladder (Task 27's rule "new presets append levels when they ship"):** levels 16-18 (`D major`, `B♭ major`, `A minor`, all Treble, Pitch only) were appended after level 15, which keeps every existing level number and therefore every recommendation for existing history. **Deliberate change to one pre-existing test:** `Catalog_FollowsTheBeginnerLadder` asserted the *last* catalog entry was the Pitch + hold + rhythm @70 level; it now asserts `Levels[14]` (level 15) because the ladder no longer ends there, and a new test pins levels 16-18. The catalog-composes-validly test stays green and now covers them. **Pixel evidence** (headless Firefox, viewport 1000 px so each canvas is at its smallest clamped height of 240 px, note names switched on so the picture shows what was generated): `.../scratchpad/t30-keys.png` stacks D major treble and bass, B-flat major treble and bass, A minor treble and bass; `t30-zoom.png` is a 3x crop of the bass D2/E2 ledger lines and the B-flat key signature. Both new key signatures draw on both staves in the standard positions (F#/C# top line and third space on treble, B-flat middle line and E-flat top space), notes carry no accidentals, A minor has none, and the time signature keeps a clear gap after the last accidental (lessons #5-#7). Observation, not changed: with three-character note names the label strip reads "F#2C#3" / "Eb3Bb3" where two names sit either side of a barline; the same crowding applies to the existing G/F major presets. **Superseded (2026-10-03, second round):** fixed, see Follow-up bug fixes, Second round, item 3. Also viewed in this run (Task 29): the Pattern and Interval selects and a "Thirds" and a Melodic exercise (`t29-pattern-select.png`, `t29-motion.png`).


### Task 31: Accidentals preset

**Description:** An `Accidentals` preset in C major adding sharps/flats to the natural palette (e.g., F♯, B♭, C♯, E♭) with required coverage of at least two sharps and two flats. The renderer prints an accidental only when the pitch's alteration differs from the key signature (it has no cautionary naturals), so the generator must never emit a natural of a letter that was altered earlier in the same measure, and must not alter one letter two different ways in a measure.

**Acceptance criteria:**

- [x] Invariants over many seeds: the same-measure accidental rules above, required coverage, leap/adjacent rules.
- [x] Sessions still match on sounding MIDI number (spelling preserved in generation/history, per the earlier plan's risk note).
- [x] Verified in real pixels with a seed that produces an accidental then a later same-letter note.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`
- [x] Manual: screenshot of an accidental-heavy exercise.

**Dependencies:** Task 30

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingPresetId.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingPresetId.Accidentals` (C major, one octave: C4-C5 on treble, C3-C4 on bass) adds C sharp, E flat, F sharp and B flat to the natural notes, so any of those four letters can be plain or altered (never more than one ledger line, tested). It has its own generator, `ComposeAccidentalPitches`: each draw follows the range's leap limit, never repeats a staff position even in another form (C then C sharp), and obeys a per-measure spelling rule (`MeasureSpelling`, the plan's rule: once a letter is altered in a measure on a staff, every later note of that letter in that measure keeps the same alteration, so a plain note can never be misread for the altered one; a plain note *before* the alteration is fine), then keeps the draw only if it holds at least two sharps and two flats (one of each when there are fewer than eight notes; redrawn up to 200 times, each draw succeeding roughly one time in six or better, so running out is practically impossible and the fallback is the last draw, which still obeys every other rule). Measure boundaries come from the composer: 4 prompts per measure on a single staff, 2 per staff on a grand staff, and the actual non-rest notes per pattern for a variable rhythm. Selection is the usual least-used pick (mastery-weighted when weights are given, keyed by the altered pitch); **scope decisions:** the weak-note drill is unavailable for this preset (a drill's "weakest three twice" guarantee is not implemented here) and the Pattern select does not apply (`SupportsMotion` is false), both stated in the panel/coordinator like the other ranges that pick their own notes. **Two rendering gaps found by the pixel check and fixed:** (1) exercise notes had no explicit accidental, and the notation spacing only reserves room for an accidental when the note states one (`ScoreNote.Accidental`, as an imported score does); the composer now states the printed accidental on every note whose alteration differs from the key (`GetPrintedAccidental`; the renderer draws the same glyph it always derived from the pitch, so nothing else changes, and the key-signature presets and plain ranges still state none); (2) an accidental on the first note of a measure was drawn on top of the barline, because the barline-to-first-note gap (0.02 scene-X) is smaller than the accidental's reach (0.019 centre offset + ~0.009 half glyph). `GrandStaffSceneBuilder` now adds `LeadingAccidentalClearance` (0.015) to the start of a measure whose first onset states an accidental. **This is a shared-renderer change**: an imported score whose measure begins with an explicit accidental now starts its notes 0.015 scene-X (about 7 px at 458 px per unit) further from that barline; no existing builder test pinned the old position, and the new tests pin the clearance (and that a measure with an accidental only later is unchanged). **Retry missed (Pitch only / Pitch + hold flatten):** compacting missed prompts into new measures could put a plain F after an F sharp in the same new measure, so `ComposeFromMissedPrompts` now starts a new measure when the next prompt's spelling would be misread (the measure then ends short, as the last one always could) and copies each source note's printed accidental; the onset-graded retry replays whole measures and was already safe. Sessions match on the sounding MIDI number whatever the spelling (tested by playing the enharmonic spelling of every key), and the spelling survives `Create`/history JSON for both `PitchAttempts` and `NoteAttempts`. Ladder: level 19 `Accidentals · Treble · Pitch only` appended (the append test now covers levels 16-19). **Pixel evidence** (headless Firefox, 1000 px viewport so the canvas is 240 px high): `.../scratchpad/t31-acc-all.png` (before the fixes: accidentals jammed against the previous note and on the barline), `t31b-zoom.png` (3x crops of the first-note and measure-initial cases showing the overlap), `t31c-acc-all.png` (after: six 16-note exercises, including F#/F# in one measure, a plain F in the next measure after an F#, and leading accidentals on the first note and at measure starts, all clear of the barline) and `t31c-wide.png` (same preset at a 1400 px viewport). Observation, not changed: at the narrow 1000 px width, runs of three-character note names (F#4 Eb4 C#4 F#4) touch each other in the label strip; at 1400 px they are clearly separated, and the same crowding already applies to three-character names in the G/F major presets. **Superseded (2026-10-03, second round):** fixed, see Follow-up bug fixes, Second round, item 3.


### Task 32: Hands-together grand staff

**Description:** A `HandsTogether` option (grand staff only): each prompt is one treble note plus one bass note sounded together (five-note palettes first). The session already completes cross-staff chords; rhythm presets stay Fixed on grand staff (existing guard).

**Acceptance criteria:**

- [x] Every prompt has exactly two notes, one per staff; seeded determinism; coordinator rejects the option without grand staff.
- [x] Session tests cover cross-staff chord steps in Pitch-only and onset-graded modes; fingering generation still returns a valid fingering for each note.
- [x] Verified in real pixels, including middle-C-area pairs (lessons #27, #32, #38).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~NoteReadingSessionTests|FullyQualifiedName~ScoreFingeringGeneratorTests"`
- [ ] Manual (MIDI keyboard): play both hands together through an exercise. **Physical MIDI keyboard NOT performed**; a mocked-Web-MIDI run in headless Firefox pressed both highlighted keys of each of 8 prompts and finished "Complete: 8 of 8 first-try correct (100.0%)".

**Dependencies:** Task 25

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseOptions.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Components/SightReadingExercisePanel.razor`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `IsHandsTogether` is appended to `SightReadingExerciseOptions` (default false). With a grand staff each prompt is one treble and one bass note at the same onset, each hand drawn from its own five-note palette with the five-note rules (no repeat, leap of at most a third; both hands honour mastery weights, and the Pattern option, per staff); the treble hand is composed first, then the bass hand, so a seed is deterministic and the alternating mode is untouched. **Scope (the plan's "five-note palettes first"):** the composer throws `ArgumentException` without a grand staff and for any range other than five notes; this is also the reason it is limited, because in the five-note ranges (treble C4-G4, bass C3-G3) the hands never share a pitch, whereas the one-octave and ledger-line ranges overlap around middle C. **"Coordinator rejects the option without grand staff":** interpreted as the pattern used for Pacing, the rhythm lock and Pattern: `HandsTogether` is the learner's durable preference, `EffectiveHandsTogether` is what runs (grand staff + five-note range + not rhythm only), the panel disables the checkbox otherwise and says why, and generation never throws (a test pins that a chosen option is ignored without a grand staff). Rhythm presets stay fixed on the grand staff through the existing lock. History does **not** record hands together and the ladder ignores it, so a hands-together session counts toward level 3 (Grand staff) like an alternating one; recording it was not in the plan and is left as an open choice. Tests: composer invariants over 50 seeds (exactly two notes per prompt, one per staff, palette per staff, 8 notes per measure), determinism, rejection cases, fingering (`ScoreFingeringGenerator` gives every note a finger 1-5), a Pitch-only session that needs both hands on each prompt (first key does not advance), an onset-graded session where the later hand sets the onset deviation, and coordinator tests (availability, durable, ignored without grand staff, Retry-missed keeps both hands). **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas): `.../scratchpad/t32-ht-all.png` (three 16-prompt hands-together exercises with note names on) and `t32-zoom.png` (3x crop of the middle-C-area pairs: treble C4 on its ledger line over bass G3/F3/E3, with each staff's label lane cleanly separate): no collisions between the hands, the ledger line, the lanes or the beams.


### Task 33: Chord inversions

**Description:** Extend the Chords preset from root-position I/IV/V to all three inversions of each (nine voicings within an octave), balanced by least-used selection with no immediate repeat. Single staff.

**Acceptance criteria:**

- [x] All nine voicings appear in a long exercise; each voicing is the correct triad inversion with notes sorted on one staff; no adjacent repeat.
- [x] Existing chord tests pass or are tightened with stated reasons.
- [x] Verified in real pixels for inversions with seconds (displaced noteheads).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`

**Dependencies:** Task 25

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** S

**Implementation note (2026-10-02):** The Chords preset now draws from nine voicings (`ChordVoicingScaleOffsets`): root position, first and second inversion of I, IV and V, in close position, each with its lowest note in the octave above the staff's base C (C4 on treble, C3 on bass), so the three root positions are exactly the ones the preset always had. Treble: C4 E4 G4, E4 G4 C5, G4 C5 E5, F4 A4 C5, A4 C5 F5, C4 F4 A4, G4 B4 D5, B4 D5 G5, D4 G4 B4 (bass is the same shapes an octave lower). The selection code is unchanged: least-used voicing, never the previous one; a 16-prompt exercise therefore uses all nine voicings (the first nine prompts are nine different ones, then each repeats at most once), pinned over 20-30 seeds against voicings written out as literals. **Deliberate change to one pre-existing test:** `Compose_Chords_UsesIivAndVTriadsOfCMajor` asserted that every prompt equals one of the three root-position pitch sets, which is exactly what this task changes; it is now `Compose_Chords_UsesIivAndVTriadsOfCMajorInAnyInversion` and asserts the prompt's note *letters* are one of the three triads (still the I, IV, V chords). The other chord tests (three distinct pitches, no immediate repeat, all notes on the chosen staff, chord count, determinism) passed unchanged. Seeded chord output changes (more voicings), which no test pinned. **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas, note names on): `.../scratchpad/t33-chords-all.png` (16-prompt chord exercises, two on treble and two on bass) and `t33-zoom.png` (3x crops). The chords draw cleanly on both staves. Two findings: (1) the plan asks for inversions "with seconds (displaced noteheads)", but a close-position triad inversion has intervals of a third and a fourth only, so no adjacent seconds and no displaced noteheads occur in these nine voicings; the displaced-notehead path is therefore not exercised by this task. (2) On the bass staff the highest voicings reach further above the staff than the root positions did: first-inversion V (B3 D4 G4) tops out on the third ledger line, second-inversion I (G3 C4 E4) and first-inversion IV (A3 C4 F4) on the second; all render cleanly, but if three ledger lines is too much for a beginner's bass chords, lowering those bass voicings by an octave is a small table change and was left for the user to decide. The treble three-row label stack is as small as it already was for root-position chords (the existing severe-stack shrink). **Superseded (2026-10-03, second round):** finding (2) is fixed: the bass staff's first-inversion V is now voiced an octave lower (B2 D3 G3), see Follow-up bug fixes, Second round, item 5.


### Checkpoint H: Reading generator growth

- [x] Tasks 29–33 tests pass; Release build clean; screenshots recorded for every rendering-affecting task.
- [x] Ladder catalog extended for any shipped preset (Task 27's catalog test still green).

**Checkpoint H status (2026-10-02):** full .NET suite 1485 passed (0 failed), JS suite 80 of 80 passed, Release build of `PianoMapper.slnx` 6 projects, 0 errors, 0 warnings. Screenshots for the rendering-affecting tasks are under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/` (Task 30 `t30-keys.png`, `t30-zoom.png`; Task 31 `t31-acc-all.png`, `t31b-zoom.png`, `t31c-acc-all.png`, `t31c-wide.png`; Task 32 `t32-ht-all.png`, `t32-zoom.png`; Task 33 `t33-chords-all.png`, `t33-zoom.png`; plus the Task 29 pattern UI `t29-pattern-select.png`, `t29-motion.png`). **Ladder catalog, what was and was not extended:** levels 16-19 were appended for the four new `SightReadingPresetId` members (`D major`, `B♭ major`, `A minor`, `Accidentals`; Treble, Pitch only) as each shipped; levels 1-15 and their numbers are unchanged. No level was added for motion (Task 29), hands together (Task 32) or chord inversions (Task 33): they are options or a widening of an existing preset rather than new presets, the ladder matches on preset, staff, grand staff, mode and rhythm only, and the plan says the ladder ignores motion. The consequence, stated rather than hidden, is that a Melodic or hands-together session counts toward the level of its range and staff. Cross-cutting decisions made in this phase: the Pattern select does not apply to ranges that pick their own notes (Chords, Ledger lines, Accidentals) or to rhythm only; the weak-note drill is unavailable for a non-random pattern and for Accidentals; the leading-accidental clearance in `GrandStaffSceneBuilder` is a shared-renderer change (see Task 31). Not reviewed by the user (no sign-off recorded).

### Task 34: Extended 4/4 vocabulary

**Description:** Append `SightReadingRhythmPreset.Extended` (4/4): dotted half + quarter, quarter + dotted half, whole, dotted quarter + eighth + half, and an eighth-rest pattern. Beam states are written explicitly per pattern (the file's own rule); beat totals are verified by tests. This is the first task that needs more than the quarter rest, so it extends Task 10's glyph mapping with eighth (and, if a pattern uses them, half/whole) rests.

**Acceptance criteria:**

- [x] Every pattern sums to exactly 4 beats via `MusicalTime.GetBeats`; beams are explicit; every pattern is used by the least-used rule across a long seeded exercise.
- [x] Each rest value used by a pattern renders at its conventional staff position (Task 10's mechanism); dotted and whole notes render correctly.
- [x] Verified in real pixels for each pattern at the smallest clamped canvas height.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`
- [x] Manual: screenshots of each pattern.

**Dependencies:** Tasks 10, 12

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingRhythmPreset.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingRhythmPreset.Extended` is appended. Its five 4/4 patterns are: dotted half + quarter, quarter + dotted half, a whole note, dotted quarter + eighth + half, and quarter + eighth rest + eighth + half. None of them beams two notes (the lone eighths are flagged), so every beam state is explicitly `None`; tests pin beat totals via `MusicalTime.GetBeats`, the exact onsets of the two eighth patterns, usage balance over a 60-note exercise (all five patterns used, counts differing by at most one, never the same pattern twice in a row), and the slow default tempo (60 pulses, pulse = quarter). To keep the next rhythm tasks to data, the composer's per-preset choices (time signature, patterns, score-title wording, compound pulse) now live in one `GetRhythmCatalog`; `ComposeVariableRhythmScore` no longer branches on `Compound`. **Rests:** the eighth rest (U+1D13E, two staff spaces tall, centered on the middle line, so its head sits in the third space and its stem reaches the second line: the conventional position, checked in pixels) joins the quarter rest in `GetRestGlyph`; no pattern uses a half or whole rest, so those stay spacing only (a test pins that). Labels: "Extended 4/4 (dotted notes, whole note, eighth rest)". **Ladder:** level 20 `Rhythm only · Extended 4/4` (start 60, maximum 100) appended; I chose Rhythm only for the new rhythm vocabularies so the new notation is learned without pitch, as levels 6-7 do. Existing theories gained an `Extended` row (determinism, requested prompt count, tempo conversion); no pre-existing assertion changed. **Found by the pixel check and fixed (shared renderer change):** a dotted note with its stem up (the usual case for notes below the middle line) had its dot drawn 0.5 px from the stem (the stem stands where the dot goes), so a dotted half read as a plain half. `canvas.js` now places the dot 0.3 staff space clear of the stem for stem-up notes (`dotClearanceFromStemInStaffSpaces`); stem-down notes and notes without a stem are unchanged. This also moves the dots of stem-up dotted notes in imported scores and in the existing 6/8 Compound patterns by about 3 px; `canvas.test.mjs` pins the clearance (its fake context now records `arc` calls). A dot on a staff line is still drawn on the line rather than moved into the space above (existing behavior, left alone). **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas; `.../scratchpad/`): `t34-ext-all.png` and `t34-zoom.png` (before the dot fix: dots fused with stems), `t34b-rows.png` (after: three exercises showing the whole note, dotted halves and quarters with visible dots, the eighth + half figure and the eighth-rest figure) and `zr.png` (8x crop of the eighth rest between two quarter/eighth notes). Observation, not changed: three-character note-name labels of adjacent eighth-note onsets touch at this width (E4G4, F4G4), as for the accidentals preset. **Superseded (2026-10-03, second round):** fixed, see Follow-up bug fixes, Second round, item 3.


### Task 35: 3/4 and 2/4

**Description:** Append `ThreeFour` and `TwoFour` presets with their own pattern catalogs (quarters, half + quarter, dotted half in 3/4, eighth pairs, a quarter rest). Count-in is one measure; metronome accents are unchanged.

**Acceptance criteria:**

- [x] Patterns sum to the meter's beats; the score's time signature and count-in follow the preset; labels exist.
- [x] Hold/rhythm grading and tempo conversion work for both meters (pulse = quarter).
- [x] Verified in real pixels, including the time signature glyph spacing (lessons #5–#7).

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~SightReadingLabels"`

**Dependencies:** Task 34 (shared catalog structure)

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingRhythmPreset.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Web/Practice/SightReadingLabels.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `ThreeFour` and `TwoFour` are appended to `SightReadingRhythmPreset` and added to `GetRhythmCatalog` (their own time signature, pulse = quarter, same slow default of 60 pulses). 3/4 patterns: Q Q Q, H Q, Q H, a dotted half, two beamed eighths + Q Q, Q + beamed eighths + Q, and Q + quarter rest + Q. 2/4 patterns: Q Q, H, beamed eighths + Q, Q + beamed eighths, Q + quarter rest. Beam states are explicit per pattern; the only rest is the quarter rest, which the renderer already draws. Tests: exact beat totals per meter, all patterns used and balanced over a 60-note exercise, validly formed beam groups, tempo pulse = quarter, a 3/4 onset-graded session (the second measure starts at 3.0 s at 60 pulses: on time at 3.0 s, early at 2.0 s), a hold-graded session (a dotted half held 3 s is correct and lifted after 1 s is too short), coordinator count-ins lasting exactly one measure (3 s and 2 s at 60 pulses) and a chosen tempo surviving a switch between quarter-pulse presets, and a review-builder test for bar/beat in 3/4 and 2/4. Labels "3/4 time (dotted half, quarter rest)" and "2/4 time (short bars)". The metronome accents need no change (`BeatsPerGroup` is 1 for both, covered by Task 8's tests). **Ladder:** levels 21 `Rhythm only · 3/4 time` and 22 `Rhythm only · 2/4 time` (start 60, maximum 100) appended. The rhythm select now lists presets in enum order (Fixed, Basic, Compound 6/8, Extended 4/4, 3/4, 2/4); each label names its meter, so I did not add a separate display order. **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas; `.../scratchpad/t35-rows.png`, from `t35-34-*.png` and `t35-24-*.png`): the 3 over 4 and 2 over 4 time signatures draw at the same size and gap after the clef as the 4/4 and 6/8 ones, notes start clear of them, dotted halves show their dot (after the Task 34 dot fix), beamed pairs, quarter rests and bass-staff exercises are clean. Not observed in these particular exercises: a 3/4 `Q rQ Q` measure (the quarter-rest glyph itself was verified in Task 10 and in the 2/4 samples).


### Task 36: Syncopation and ties

**Description:** Append `Syncopated` (4/4): off-beat eighths, notes tied across the beat and across the barline. Uses `ScoreNote.TiesToNext`; confirm the session merges tied continuations for onset/hold grading (the existing next-key hint behavior already does) and that the Review/Retry paths treat a tied pair as one prompt.

**Acceptance criteria:**

- [x] Tied pairs are one prompt in `NoteReadingSession` and in the play-along mapper; durations sum across the tie; measure-boundary ties render using the existing tie renderer.
- [x] Beat totals, explicit beams, and balanced pattern usage are tested; labels exist.
- [x] Verified in real pixels for within-measure and cross-barline ties.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~SightReadingExerciseComposerTests|FullyQualifiedName~NoteReadingSessionTests|FullyQualifiedName~PlayAlongResultMapperTests"`
- [x] Manual: play a syncopated exercise on the MIDI keyboard in Rhythm mode. **Physical MIDI keyboard NOT performed.** A mocked-Web-MIDI run in headless Firefox played a Syncopated Pitch-only exercise (8 requested, 10 prompts because patterns overshoot) by pressing the highlighted keys and finished "Complete: 10 of 10 first-try correct (100.0%)", i.e. tied pairs were one key press each; the onset-graded paths are covered by the session and mapper tests below.

**Dependencies:** Tasks 34, 14

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingRhythmPreset.cs`
- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/NoteReadingSessionTests.cs`
- `PianoMapper.Tests/UnitTests/PlayAlongResultMapperTests.cs`

**Estimated scope:** M

**Implementation note (2026-10-02):** `SightReadingRhythmPreset.Syncopated` is appended. Eight 4/4 patterns: Q 8 Q 8 Q and 8 Q 8 Q Q (off-beat eighths, flagged), Q Q~ Q Q (tie across the middle of the bar), Q 8 8~ Q Q (a beamed pair whose second eighth is tied into the next quarter), and two measure pairs that tie across the barline: Q Q Q 8 8~ or H Q Q~ ends tied and must be followed by Q Q H (continuation first) or H Q Q (continuation first). `RhythmEvent` gained `TiesToNext` and `IsTiedContinuation`; a continuation repeats its predecessor's pitch and is not a prompt (pitch picking, the prompt count and the accidentals generator's measure boundaries all skip it, and the accidentals generator records a tied note at the start of the next measure so the spelling rule still holds). The pattern selection only changes for catalogs with ties: a pattern that ends tied can only be followed by one that starts with a continuation (and no other pattern may), and the exercise never stops mid-tie; the other catalogs see exactly the candidates they always did. Usage stays balanced (tested: eight patterns, counts within one, no immediate repeat, over 100 notes), every tie is finished by a note of the same staff and pitch starting where it ends, the last measure never ends tied, beams stay explicit. **The plan's premise "measure-boundary ties render using the existing tie renderer" was not true for the printed score view:** `GrandStaffSceneBuilder.BuildScore` never drew `ScoreNote.TiesToNext` at all (an existing test comment says so; the tie renderer in `canvas.js` was only fed by the live view), so a tied pair would have shown two noteheads and read as two attacks. I added score-view ties, feeding the existing `GrandStaffTie` / `drawTie` renderer: a new `drawTies` scene option (default off, part of the static-parts cache key, passed by the page only while an exercise is shown, exactly like `drawRests`), so **imported scores still draw no ties** (turning them on for imported scores is a separate decision, as it was for rests). A tie joins a note to its continuation (`ScoreDerivation.FindTieContinuation`, the same rule `Flatten` merges by, now public) on the side opposite the stem; a tie that leaves the visible measures is a half tie to the right edge and one that arrives from before them is a half tie from the left edge (tests cover within-measure, across the barline, both window edges, the option off, a dangling flag, and cache invalidation). **Sessions, mapper, review and retry:** `Flatten` already merges a tied pair, so `NoteReadingSession` and `PlayAlongResultMapper` see one prompt with both source notes and the summed duration (tested in both, including a hold-graded dotted-tie pair: held 1.0 s correct, lifted at 0.5 s too short, and a perfect mapped run of a syncopated exercise is all clean). Review uses the first note's onset for bar/beat. Retry-missed in an onset-graded mode replays whole measures and now clears a tie whose finishing measure is not part of the replay (so no curve hangs off a note), and the flatten retry (Pitch only / Pitch + hold) draws a tied pair as one quarter note instead of two stacked ones. **Ladder:** level 23 `Rhythm only · Syncopated 4/4` (start 60, maximum 90) appended. Labels "Syncopated 4/4 (off-beats and ties)". **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas; `.../scratchpad/`): `t36-sync-all.png` (four 16-note exercises: ties inside a measure, across the middle of the bar and across barlines), `t36-zoom.png` (5x crops of a cross-barline tie and a beamed-pair tie), `t36-row-all.png` and `t36-row-zoom.png` (three exercises with 3 measures per page, so a tie can cross from one row to the next). Observations, not changed because they touch the existing tie geometry that lesson 13 tuned: (1) at this size the tie is faint, its centre thickness being 0.08 staff space (about 0.6 px on a 240 px canvas), and for a note in the bottom space it runs along the bottom staff line; (2) the half tie that arrives at the start of the second row is only the 9 px between the opening barline and the first note, so it is practically invisible, whereas the half tie leaving the first row is clear. If either bothers you a minimum thickness for small canvases would be a small change in `drawTie`. **Superseded (2026-10-03, second round):** both are fixed, see Follow-up bug fixes, Second round, item 4 (the arriving half tie turned out not to be drawn at all, not just faint).


### Task 37: More 6/8 patterns

**Description:** Extend the Compound catalog (currently four patterns) with quarter + eighth groupings, an eighth-rest pattern, and a long note across a group. Extending `Compound` changes seeded output, so any test asserting pattern sequences is updated deliberately.

**Acceptance criteria:**

- [x] Every pattern sums to 6 eighth-beats with explicit beams that respect the 3 + 3 grouping; usage stays balanced.
- [x] Tempo conversion, count-in, and the Task 8 accents work unchanged.
- [x] Verified in real pixels for each new pattern.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter FullyQualifiedName~SightReadingExerciseComposerTests`
- [x] Manual: screenshots of each pattern.

**Dependencies:** Tasks 8, 10

**Files likely touched:**

- `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`
- `PianoMapper.Tests/UnitTests/SightReadingExerciseComposerTests.cs`

**Estimated scope:** S

**Implementation note (2026-10-02):** The Compound catalog grows from four to nine patterns (in eighth-note beats, grouped 3 + 3): the original dotted quarter pairs and eighth groups, plus Q 8 Q 8 and Q 8 Q. and Q. Q 8 (quarter + eighth groupings; the lone eighth is flagged, never beamed to a quarter), r8 8 8 Q. (an eighth rest then a beamed pair then a dotted quarter) and a dotted half (one note held across both groups). Tests: beat totals of 6 for every pattern, all nine used and balanced over 100 notes with no immediate repeat, only the dotted half crosses the middle of the bar, every beamed group lies inside one group of three, the quarter + eighth measure has no beam, the rest measure has its own beamed pair, and tempo (50 pulses = 150 eighth-note beats per minute, a 2.4 s measure) and `MetronomeGrid.BeatsPerGroup` = 3 are unchanged. No new rest glyph was needed (the eighth rest comes from Task 34). **Deliberate changes to pre-existing tests (extending `Compound` changes what it may contain, as the plan anticipated):** `Compose_CompoundRhythm_OnlyUsesDottedQuarterAndEighthNoteValues` is now `Compose_CompoundRhythm_OnlyUsesTheCompoundNoteAndRestValues` (allows dotted half, dotted quarter, quarter and eighth notes and only eighth rests); `Compose_CompoundRhythm_BeamedEighthGroupsHaveConsistentBeamState` now calls the shared beam helper with `allowLoneEighths: true` (the helper's default still requires every eighth to be beamed, which the Basic test keeps relying on). No other test pinned Compound's seeded output. The Compound rhythm in the ladder (levels 13-14) keeps its levels; no level was added because no new preset shipped. **Pixel evidence** (headless Firefox, 1000 px viewport, 240 px canvas): `.../scratchpad/t37-c-all.png` (five 16-note 6/8 exercises showing every new pattern: the dotted half with its dot, quarter + eighth with flagged eighths, the eighth rest with the beamed pair, and the old patterns beside them); dotted quarters show their dot clear of the stem (Task 34 fix). Three-character note-name labels again touch at this width.


### Checkpoint I: Rhythm catalog growth

- [x] Tasks 34–37 tests pass; Release build clean; screenshots recorded for every pattern.
- [x] Ladder catalog extended for shipped presets.

**Checkpoint I status (2026-10-02):** full .NET suite 1540 passed (0 failed), JS suite 81 of 81 passed (one test added for the dot clearance), Release build of `PianoMapper.slnx` 6 projects, 0 errors, 0 warnings, and `dotnet format PianoMapper.slnx --verify-no-changes` exits 0. Screenshots for every pattern are under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/` (Task 34 `t34b-rows.png`, `zr.png`; Task 35 `t35-rows.png`; Task 36 `t36-sync-all.png`, `t36-zoom.png`, `t36-row-all.png`, `t36-row-zoom.png`; Task 37 `t37-c-all.png`). **Ladder catalog, what was and was not extended:** levels 20-23 were appended for the four new `SightReadingRhythmPreset` members (`Extended`, `ThreeFour`, `TwoFour`, `Syncopated`), each as a Rhythm-only level (start 60 pulses, maximum 100, Syncopated 90), as the preset shipped; the Compound extension of Task 37 adds patterns to an existing preset and so adds no level. The ladder now has 23 levels (1-15 original, 16-19 reading presets, 20-23 rhythm presets). **Shared-renderer changes made in these tasks (each gated or minimal, each recorded in its task):** eighth rest glyph (exercise scores only), stem-up dot clearance in `canvas.js` (affects imported scores and Compound too), score-view ties behind the exercise-only `drawTies` option, and the leading-accidental clearance (Task 31). Not reviewed by the user (no sign-off recorded).

**Imported-score regression check of the two shared-renderer changes (2026-10-02, third run):** the leading-accidental clearance (`LeadingAccidentalClearance`, Task 31) and the stem-up dot clearance (`dotClearanceFromStemInStaffSpaces`, Task 34) also touch imported scores. Method: `git archive HEAD` (771dd75, nothing of the earlier runs) was extracted into the scratchpad (`head-src/`, no index, stash or checkout used), built and served on port 5198 as the "before"; the working tree ran on 5199 as the "after"; the same files were loaded through the real file input of each, at a 1000 px viewport (240 px canvas), and screenshotted and pixel-compared (`compare -metric AE`). Files: `docs/mxml` and `PianoMapper.Tests/Fixtures` have no file with an explicit `<accidental>` that starts a measure (only `mia_sebastians_theme_ivanovskaya_transcription.musicxml` has `<accidental>` elements at all, all in mid-measure, and `dotted-double-accidental.musicxml` has dots and a double sharp without an explicit accidental), so two scratch scores were written for the leading-accidental case (not added to the repository): `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/scratch-acc-dots-grand.musicxml` (grand staff, 4 bars, explicit accidentals on the first note of bars 1-4 on both staves, dotted quarters and dotted halves with stems up and down) and `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/scratch-acc-dots-single.musicxml` (single staff, same idea with eighths). Results (before / after / pixel difference images are `ba-*-before.png`, `ba-*-after.png`, `ba-*-diff.png`; zooms `ba-grand-zoom.png`, `ba-mia-zoom.png`, `ba-mia11-zoom.png`): **grand scratch score**, 2,475 pixels differ: before, a measure-leading sharp straddled the barline (bar 3, seen at 5x) and the dots of stem-up dotted notes (the bass F#2, for one) were fused with the stem so a dotted half read as a plain half; after, the accidental sits clear of the barline and every dot is visible beside its stem. **Single-staff scratch score**, 1,572 pixels differ, the same two improvements (the F#4 that starts bar 3 no longer touches the barline, the G#4, A4 and B♭4 dots show). **`mia_sebastians_theme_ivanovskaya_transcription.musicxml`** (a real score; measures 1-10, 11-20 and 16-24 viewed): 23, 799 and 23 pixels differ; the only differences are the dots of the dotted notes (seen at 5x: the dotted half D4 in bar 2, the dotted half C#4 in bar 4, the dotted note in bar 12), which were invisible before and are now visible; the key signature, time signature, clefs, labels and spacing are pixel-identical (lesson #5). **`dotted-double-accidental.musicxml`**: 5 pixels differ (its dot). Conclusion: both changes are better or neutral on every imported score checked, none is visibly worse, so **no gating to exercise scores was added**. Not checked: scores with a measure-leading accidental *and* beams or chords at the same spot (none exists in the repository).

### Task 38: README, glossary, browser test matrix, and full verification

**Description:** Bring docs in line with shipped behavior. Correct the README's "per-prompt review" claim (or delete it if Tasks 22a–22b are not shipped), document the new modes, pacing, tempo, click, hints, drill, and ladder, add glossary terms to `CONTEXT.md` (pacing, graded axes, prompt outcome, review mark, level), and update `docs/browser-test-matrix.md` with the new manual scenarios (and mark which were actually run). Use the `verify-readme-docs` skill.

**Acceptance criteria:**

- [x] README claims verified against code; no stale "per-prompt review" overstatement.
- [x] `CONTEXT.md` terms match code names; the browser test matrix has rows for each new scenario with run status.
- [x] Full verification passes (below) and the results are pasted into the task notes.

**Verification:**

- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`
- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`

**Dependencies:** All shipped feature tasks

**Files likely touched:**

- `README.md`
- `CONTEXT.md`
- `docs/browser-test-matrix.md`

**Estimated scope:** S

**Implementation note (2026-10-02, second run; its statements about review marks and the panel subtitle are superseded by the follow-up below):** Done with the `verify-readme-docs` workflow (code and tests as the source of truth). **README:** the Features bullet that said "self-paced" and "per-prompt review" is replaced by four bullets that match what ships: the reading presets (keys, accidentals, chord inversions, Pattern, Hands together) and rhythm presets, the five modes, tempo, count-in, click, beat indicator and live early/late feedback, Wait for me versus Play along, separate pitch/onset/duration grading, opt-in answers and Coach hints, the **Mistakes to look at** review list, Retry missed notes, browser-local history, the Progress panel, the Guided path, Start recommended exercise and Drill my weak notes. **Review marks are not shipped** (Tasks 22a and 22b are pending the user's choice of treatment, see Task 21), so the README says plainly that "Review does not draw marks on the staff"; the old "per-prompt review" wording is gone. Two limits were added because the code makes them true: timed exercises have no output-latency compensation (Task 9's calibration is not built, its measurement is pending), and rests and ties are drawn only in generated exercises (imported scores use rests for spacing and timing only and show tied notes without a curve). **CONTEXT.md:** added `ExercisePacing`, `GradedAxes`, **Prompt outcome** (`NoteReadingPromptResult`) and **Level** (`ExerciseLevel`), each named as in the code. The plan's "review mark" term was deliberately **not** added, because nothing by that name exists in the code yet; add it when Tasks 22a/22b ship. **docs/browser-test-matrix.md:** the Note-reading exercise section no longer says "self-paced", its "Hidden until review" row describes the mistake list instead of per-prompt feedback, and 19 rows were added for the new scenarios (mode ladder and tempo, click and feedback, 6/8 accents, rhythm only, pacings, pitch/timing reporting, coach hints, retry missed in timed modes, history upgrade, progress/drill/guided path, hands together, Pattern select, new keys and accidentals, chord inversions, the extended rhythm presets, syncopated ties, and the timing-offset measurement). Each row is marked passed only for what was actually run: "Mocked-MIDI pass" (headless Firefox, fake Web MIDI input, 2026-10-02: the real page, audio clock and grading but not the physical keyboard or any listening), "Pixel pass" (screenshots at the 240 px canvas), or Pending; none of the physical-keyboard or by-ear checks is marked done. **Stale text found and not changed (outside Task 38's files):** the exercise panel's subtitle in `SightReadingExercisePanel.razor` still reads "Self-paced pitch practice", which is no longer accurate now that rhythm modes and Play along exist. **Full verification (2026-10-02, after the last code change):** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` 1540 passed, 0 failed; `node --test PianoMapper.Tests/JavaScript/*.test.mjs` 81 of 81 passed; `dotnet build PianoMapper.slnx --configuration Release` 6 projects, 0 errors, 0 warnings; `dotnet format PianoMapper.slnx --verify-no-changes` exit 0; `git diff --check` clean.

**Follow-up (2026-10-02, third run), after the user picked the halo and Tasks 22a/22b shipped:** done with the `verify-readme-docs` workflow against the code and tests. **README:** the Features bullet that said "Review does not draw marks on the staff" now describes what is drawn and no more: a halo ring per not-clean note after completion (one ring around a chord, one per hand of a hands-together prompt), solid orange for a timing mistake, thicker solid red when a wrong key was played, dashed grey for a note never played in Play along, none for a clean note, the precedence wrong key over missed over timing, both pacings, nothing while the exercise is active. **CONTEXT.md:** added **Review mark** (`ReviewMark`, `ExerciseReviewMarks`), the term Task 38 had held back because no such name existed in the code. **docs/browser-test-matrix.md:** the sentence that no review-mark scenario exists is gone, and five rows were added (Wait for me, Play along, clear of the notation, only for exercises, and reading them at a glance on the physical keyboard); each is marked only for what was run (mocked-MIDI pass and pixel pass, the by-hand physical-keyboard reading Pending, and the stale-canvas observation recorded in the row). **Stale subtitle fixed:** `SightReadingExercisePanel.razor` read "Self-paced pitch practice" and now reads "Pitch and rhythm practice" (the exercise now has rhythm modes and Play along); `SightReadingHistoryPanel.razor`'s "Self-paced speed" is still accurate (it describes sessions that waited for the learner) and was left alone. **Full verification (2026-10-02, after the last change):** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` 1570 passed, 0 failed; `node --test PianoMapper.Tests/JavaScript/*.test.mjs` 93 of 93 passed; `dotnet build PianoMapper.slnx --configuration Release --no-incremental` 6 projects, 0 errors, 0 warnings; `dotnet format PianoMapper.slnx --verify-no-changes` exit 0; `git diff --check` clean.


## Final definition of done

- [ ] Tasks for each shipped release slice are complete and their checkpoints signed off by the user; Phases 8–9 are done only for the items the user schedules.
- [x] A right pitch played late no longer lowers pitch mastery (tested), and an existing v1 history loads intact (fixture-tested).
- [x] Full .NET, JavaScript, and Release-build verification passes. (1570 .NET, 93 of 93 JS, Release build 0 errors and 0 warnings, formatter clean, 2026-10-02, third run.)
- [x] Every rendering-affecting task has pixel evidence at the smallest clamped canvas height. (Rests, tie and accidental fixes, new keys, hands together, chord inversions, the rhythm presets, and the review marks in Task 22b; each task's note names its screenshots.)
- [ ] Manual MIDI-keyboard runs are recorded for Wait-for-me and Play-along (the earlier plan's manual items were never run; this plan records real results).
- [x] README, `CONTEXT.md`, and the browser test matrix match shipped behavior. (Review marks are shipped as a halo and the docs describe it; see Task 38's follow-up.)

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `Piano.razor` (2,400+ lines) keeps growing; Tasks 6, 7, 9, 17, 18, 22 all touch it | High | Keep logic in the coordinator/controller (tested); one integration owner for page edits; no combined cleanup. |
| Two grading engines drift (pitch-gated vs time-driven) | High | D4 shared axes helper, shared onset-grouping helper, and the Task 14 cross-engine contract test. |
| History schema change loses a learner's saved sessions | High | D2: version once, parse v1 and v2, fixture from a real history, additive-only afterwards. |
| Rendering regressions (rests, review marks, new keys/accidentals, hands together, new patterns) | High | D10 pixel verification; rests gated to exercises first; review marks via separate channel; lessons #18–#26 and "ask before overriding a documented trade-off". |
| Calibration misattributes hardware offset to the player, or is built needlessly | Medium | Task 9 is measurement-gated with a 25 ms go/no-go; shifts only grading anchors (D7). |
| Overly strict grading discourages a beginner | Medium | Proportional release tolerance (Task 2), slow default tempo (Task 5), open decision on a gentler onset tolerance (below). |
| Compound-meter tempo/count-in math wrong | Medium | D5 pulse-to-beat conversion tested in Task 5; lesson #15. |
| Adaptive drill breaks coverage or becomes monotone | Medium | D8: drill is explicit; invariants tested over many seeds, incl. weakest-three minimums and no adjacent repeat. |
| `Verdict`/enum ordinal contracts broken (`verdictColors`, persisted names) | Medium | D3 append-only; contract tests in Tasks 10 and 22. |
| localStorage payload growth from richer summaries | Low | Task 23 caps confusions and tests a worst-case size. |
| Scope creep from Phases 8–9 | Medium | À la carte after Checkpoint G; each task independent and ≤ M. |

## Parallelization guidance

- Tasks 1–4 are foundational; keep them sequential (Tasks 1→2→3→4).
- After Checkpoint A: Task 8 (accents), Task 10 (rests), and Tasks 5→6→7 can proceed in parallel; Task 9 waits for Task 7.
- Tasks 11–13 follow Task 1/2; Phase 4 follows Tasks 11–12. Tasks 19–22 can run in parallel with Phase 4 once Tasks 1 and 4 are done (Task 22a needs Task 17a for the play-along path).
- Phase 6 follows Tasks 3 and 23; Phase 7 follows Phase 6 and Task 12.
- Phases 8–9 are independent of everything after Task 25, but all of them edit `SightReadingExerciseComposer.cs` — serialize those edits or assign one owner.
- Task 38 is always last.

## Out of scope / deferred

- Sixteenth notes and triplets (high beaming/rendering risk; not beginner-critical).
- Hands-together rhythm exercises (rhythm presets remain Fixed on grand staff, as today).
- Drawing rests for imported scores (separate decision after visual verification against real saved scores).
- Harmonic/melodic minor, landmark-note and interval-naming quizzes, ear training, sustain pedal, accounts/cross-device sync, mobile layout redesign.
- Adding the new modes to the idle imported-score checking selector.

## Review decisions

Reviewed with the user on 2026-10-01: the recommendations below were accepted as written, with one change — coach hints are opt-in and off by default (Task 20). The one item left open is chosen later, from the Task 21 spike.

- [x] Default tempos (60 pulses/min in 4/4; 40 dotted-quarter pulses/min in 6/8) are right for a beginner.
- [x] Hold-release window of `max(onset tolerance, 25% of written duration)`.
- [x] Include Task 9 (timing calibration), behind the measurement gate (it is an addition beyond the original seven proposals).
- [x] No gentler "Beginner (150 ms)" onset-tolerance task is added; the default stays Normal 60 ms. Revisit if the R1 manual runs on the MIDI keyboard show 60 ms is too strict.
- [x] Rests drawn for exercise scores only at first (Task 10).
- [x] Play-along offered only for onset-graded modes; Wait-for-me stays the default.
- [x] Coach hints are **opt-in, off by default** (Task 20), with thresholds of 2 and 4 wrong attempts once enabled.
- [x] New modes are exercise-only (idle checking selector unchanged).
- [x] The ladder only recommends (never locks); pass rule of 3 sessions at ≥ 90% pitch (≥ 80% timing-clean for timed levels).
- [x] The review-mark visual treatment is chosen by the user from the Task 21 spike: **A, the halo, picked by the user on 2026-10-02.**
- [x] Phases 8–9 are scheduled à la carte after Checkpoint G.
- [x] Release order R1 → R2 → (Tasks 19–20) → R3 → rest.

## Follow-up bug fixes

**2026-10-03** (the work started on the evening of 2026-10-02). Three bugs listed as "found but not fixed" in the last status report, done in this order with the same loop each time: reproduce first (a failing test, and real pixels where the bug is visual), smallest fix, proof. Screenshots and scratch tools are under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/` (headless Firefox, mocked Web MIDI, viewport 1000 px so each canvas is its smallest clamped 240 px high). The "before" builds were `git archive HEAD` (771dd75, port 5198) and a copy of the working tree taken before any of these edits (`cur-before/`, port 5197); no git state was touched.

- [x] **Bug 1: loaded-score Practice loses notes older than 15 s and grades them Missed. Already fixed in the working tree; not caused or fixed by this run.**
  - **Root cause (at HEAD):** `NoteTimeline` forgets finished notes 15 s after their release and `BrowserPracticeCoordinator.UpdateAsync` regraded from `timeline.Snapshot(currentTime)` on every tick, so a note performed more than 15 s earlier dropped out of the grading input and its expected event flipped to Missed.
  - **State found:** the working tree (staged since an earlier run) already has the fix. `BrowserPracticeCoordinator.GetPerformedNotes` copies every note it sees in the snapshot into a per-run list (cleared by `StartAsync` and `AbortAsync`) and grades that list. It is unconditional, so loaded-score Practice has it too; the `RetainPerformedNotes` option that the Task 16 note describes no longer exists (`PracticeRunOptions` only has `ScheduleCountInClicks`). Free-play and the visualisation keep the 15 s window (`NoteTimeline` is untouched).
  - **Reproduction of the bug itself:** (1) real page, 32-note loaded score at 60 BPM (32 s), perfect mocked-MIDI performance: the HEAD build ended with **15 Correct and 17 Missed (46.9%)**, the working tree with **32 Correct (100.0%)**; the same run with notes 4, 5, 13 and 20 not played gave 28 Correct and 4 Missed (87.5%) on the working tree. (2) `UpdateAsync_LongRun_GradesNotesTheTimelineHasPruned` (already in the tree) goes red (13 of 20 Correct) in a scratch copy with the old one-line `session.Grade(timeline.Snapshot(currentTime))`, and is green in the tree.
  - **Added by this run (no production change):** `UpdateAsync_LongRunWithUnplayedNotes_StillGradesThoseNotesMissed` (20-note run with a two-note gap and a lone missed note: 17 Correct, 3 Missed, 0 Extra; red in the same scratch copy with 12 Correct, green in the tree), plus an optional `skippedNoteIndexes` parameter on the test helper `PlayPerfectly` (existing callers unchanged). Because the fix was already there this test was green on arrival; its red state is only the scratch-copy one.
  - **Pre-existing tests changed:** none.
- [x] **Bug 2: changing Mode or Pacing while a review (or a run) is showing flips the displayed exercise. Fixed.**
  - **Root cause:** `Mode` and `Pacing` are durable settings like Staff, Preset and Range, but unlike those they were also read live by the code that describes the exercise on screen: `EffectivePacing` (derived from them) decided `Phase`, `PromptResults`, the counts, `PlayAlongVerdictCounts`, `CompletePlayAlong`'s guard and the summary's `Pacing`; `Mode` decided `ShouldClickSound`, the summary's mode, the page's `ActiveNoteReadingMode`, the play-along controller's grading mode, the count-in's session reset and the panel's timing breakdown. Staff, Preset, Range and the other settings are only read by the next Generate (the exercise's `Score` is its own), so changing them never touched the displayed exercise.
  - **Fix:** the coordinator remembers the mode and effective pacing when a run starts (`StartRun`, used by Generate, Retry and Retry missed) and exposes them as `RunMode` and `RunPacing` (the plain settings when there is no exercise). Everything that describes or grades the exercise on screen reads those; `Mode`, `Pacing` and `EffectivePacing` keep meaning "what the next Generate, Retry or Retry missed will use" and still drive the selects. `TryCompleteCountIn` resets the session with the run's mode. `ExercisePlayAlongController` and `Piano.razor` read `RunPacing`/`RunMode` where they describe or grade the run on screen, and the panel's pitch/timing breakdown follows a new `RunMode` presentation field. No other setting's semantics changed.
  - **Proof, failing first:** in `SightReadingExerciseCoordinatorTests`, `SetModeAndPacing_WaitForMeRunInReview_KeepsPhaseResultsAndMarks`, `SetModeAndPacing_PlayAlongRunInReview_KeepsPhaseResultsMarksAndSummary`, `SetPacing_PlayAlongRunInProgress_StillCompletesAsAPlayAlongRun`, `SetMode_TimedWaitForMeRunInProgress_KeepsTheRunsClickPolicy`, `TryCompleteCountIn_ModeChangedDuringTheCountIn_GradesWithTheModeTheExerciseWasGeneratedWith` and the `RetryMissed` case of `GenerateRetryAndRetryMissed_AfterModeAndPacingChanged_UseTheNewValues` were red (6 failures: Phase flipped Review to Active, the run could not complete, a late note graded Correct), then green; the `Generate` and `Retry` cases of that theory were green before and after (they guard "the next Generate/Retry picks up the new values"). `RunModeAndRunPacing_WithoutAnExercise_FollowTheSettings` and `RunModeAndRunPacing_WithAnExercise_ChangeOnlyWhenItIsGeneratedOrRetried` pin the new members. In `ExercisePlayAlongControllerTests`, `UpdateAsync_ModeChangedMidRun_GradesTheRunWithTheModeItStartedWith` was red (the skipped note was graded as a pitch miss in rhythm only) then green; `UpdateAsync_PacingChangedMidRun_StillCompletesTheRunAsPlayAlong` is a guard (the coordinator fix already satisfied it).
  - **Proof in the real page** (`bug2.sh`, `bug2pa.sh`; headless Firefox, mocked MIDI, 1000 px viewport): before (copy of the pre-fix working tree), after finishing a wait-for-me exercise with two wrong keys and choosing Mode = Pitch + rhythm and Pacing = Play along, the panel read "Progress: 0 of 8 notes", "Retry missed notes" was disabled and the button read "Generate new exercise" (Active, 0 results); the canvas kept its halo and verdict colours until a redraw and then showed an unmarked Active exercise (`bug2-montage.png`, rows 1-3). After the fix the panel, the buttons, the halo rings and the verdict colours were identical before the change, after it and after a redraw (`bug2-montage.png`, rows 4-6), and "Retry same notes" then started the play-along count-in (the new pacing). The same for a finished play-along run with Pacing = Wait for me and Mode = Pitch only (`bug2pa-montage.png`): the old version flipped to Active, the fixed one kept its "Run: Correct 18 · Late 1 · Missed 1" line, its timing breakdown, the mistake list and the canvas, and Retry then restarted in the new wait-for-me mode.
  - **Pre-existing tests changed:** none. `docs/browser-test-matrix.md`'s "Review marks only for exercises" row said this behaviour was observed and not changed; that clause now says it is fixed. Observation, not changed: `ConsumeCompletionSummary` still reads Staff, Preset, Rhythm and the grand-staff flag live (its Mode and Pacing now come from the run); the page consumes the summary in the same tick the run completes, so nothing can change in between.
- [x] **Bug 3: in the Accidentals preset with eighth notes a note's sharp or flat is drawn on the previous note's stem or head. Reproduced and fixed.**
  - **Reproduction first:** the real app (Accidentals, Treble, Basic rhythm, 16 notes) at 1000 px / 240 px canvas showed it in several of 6 random exercises (`acc-before-basic-all.png`, 3x crops `zc-acc-b-all.png`). For a reproducible measurement the real builder's scenes for the Accidentals preset were dumped for 25 seeds x 7 rhythm presets x treble/bass (526 five-measure windows, 2,090 accidentals), rendered on real 916 x 240 canvases of the live page and measured from the real font metrics (`accmeasure.py`: each accidental's ink box against the other notes' stems and heads). **Before: 11 accidentals overlapped another note's stem and 18 another note's head**, all in the presets with eighth notes (Basic 7 stems, Syncopated 3, Extended 1; 3/4, 2/4 and the quarter-note presets had none), the closest at 0 px; 42 were under 1 px from the note before them. Seeds reproduce it deterministically, e.g. Basic treble seed 1 (`b3-before-zoom.png`).
  - **Root cause:** an accidental sits 0.019 scene-X left of its note and its ink reaches 3.3 px further, while the previous note's stem stands half a notehead (4.5 px) right of its centre, so the note needs to sit about 17 px (0.039 scene-X) right of the one before it. `BuildNotationSpacingAnchors` only adds `ChordNoteheadDisplacement` (0.016) of extra gap on both sides of an accidental onset, caps all such extras at 20% of the measure and then rescales the measure to fit, so in a measure with several accidentals the gap before an accidental eighth (about 14 px of natural spacing) ended near 15 px, short of the 17 px it needs.
  - **Fix (smallest spacing change):** `GrandStaffSceneBuilder.EnsureRoomBeforeAccidentals` runs after the existing spacing and gives a note that prints an accidental at least `MinimumGapBeforeAccidentalNote` (0.04 scene-X, measured as above) before it, taking what is missing from the other gaps of the same measure so barlines and the last note do not move; a measure too crowded to hold every minimum shrinks all its gaps alike. Gaps already wide enough are untouched, `GrandStaffLayout` is unchanged and the Task 31 leading-accidental clearance is untouched.
  - **Proof, failing first:** in `GrandStaffSceneBuilderTests`, `BuildScore_AccidentalsOnCrowdedEighths_KeepEachGlyphClearOfThePreviousNote` (a measure shaped like the real seed-1 one; the sharp was 0.037 right of the note before it, it needs 0.039) and five cases of `BuildScore_AccidentalsExercisesWithEighths_KeepEveryGlyphClearOfThePreviousNote` (Basic treble and bass, Extended treble, Syncopated treble and bass, each over 25 seeds; its Compound case was green before and is a guard) were red, then green; `BuildScore_AccidentalsOnCrowdedEighths_KeepEveryNoteInsideItsMeasure` guards that every note stays between its own barlines (green before and after). After the fix the same 526-window measurement gives **0 stem overlaps, 0 head overlaps and a smallest gap of 1.4 px** (`accmeasure-acc-scenes-after.json-916x240.json`), and the real app at 240 px shows the accidentals clear (`acc-after-basic-all.png`, crops `acc-after-zoom.png`; before/after pairs `b3-compare.png`). Remaining, not changed: in the densest measures (four accidentals within a beat and a half) the glyphs sit only 1.4 px from the stems and a few accidentals sit 1-2 px from a beam; the measurement's beam test is crude and no beam overlap was seen in the crops.
  - **Imported scores before/after (shared renderer):** the scenes the real builder produces for every first-measure window of every score below were dumped with the pre-edit tree and with the fixed one and compared as JSON; the first three scores were also screenshotted at 240 px on both builds and pixel-compared. `mia_sebastians_theme_ivanovskaya_transcription.musicxml` (all 24 windows): **identical scenes, and 0 differing pixels** in the windows screenshotted (from measure 1, 11 and 16); `dotted-double-accidental.musicxml`, `grand-staff-demo.musicxml`, `single-staff.musicxml`, `musescore-export.musicxml`, `eighth-note-triplet.musicxml` and the two earlier scratch scores with measure-leading accidentals: identical. Only a new scratch score with crowded mid-measure accidentals after eighths (`scratch-mid-acc.musicxml`, scratchpad only, not added to the repository) changes: notes move by at most 3.1 px and the accidentals sit clearer of the notes before them (`b3imp-midacc-zoom.png`). So the change is neutral on every imported score checked and better on the one it applies to, and **no gating to exercise scores was needed**. Not checked: a real score with crowded explicit accidentals on consecutive eighths (none exists in the repository).
  - **Pre-existing tests changed:** none (all 168 `GrandStaffSceneBuilderTests` passed before and after, so no earlier spacing test needed updating).
- **Full verification (2026-10-03, after the last change):** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` 1591 passed, 0 failed (baseline 1570 plus 21 new: 1 for Bug 1, 10 coordinator and 2 controller for Bug 2, 8 builder for Bug 3); `node --test PianoMapper.Tests/JavaScript/*.test.mjs` 93 of 93 passed (no JS change); `dotnet build PianoMapper.slnx --configuration Release --no-incremental` 6 projects, 0 errors, 0 warnings; `dotnet format PianoMapper.slnx --verify-no-changes` exit 0; `git diff --check` and `git diff --cached --check` exit 0. Nothing was committed, staged, stashed, reset or checked out.
- **Found, outside scope, not changed:** (1) a stem-down note's own accidental sits about 0.2 px from its own stem (323 of 2,090 accidentals in the measurement, in every rhythm preset, so it predates this work and is the measured `AccidentalHorizontalOffset`); (2) imported scores reserve room only for explicit accidentals (`ScoreNote.Accidental`), not for accidentals the renderer derives from the key signature, so a derived accidental can still land near the previous stem; (3) the out-of-scope items (label crowding, faint ties, bass voicings, amber versus orange, the red cursor line, Task 9) were not touched. **Superseded (2026-10-03, second round, after the user said to fix them):** items (1) and (2) and every item of (3) except Task 9 are fixed, see Second round below; Task 9 stays pending.

### Second round (2026-10-03)

The user said "ok, implement those 'follow-up big fixes' and fix them", which replaces the earlier "out of scope, report only" instructions for the seven findings above. Done strictly in this order, each with the same loop (reproduce with a failing test and real pixels, smallest fix, proof). Task 9 and the physical-keyboard steps stay out of scope and unticked. Method: the "before" is `r2-before/`, a copy of the working tree taken before the first edit of this round (no git state touched, port 5197); the "after" is the live tree (port 5199); the real builder's scenes were dumped for 25 seeds x 7 rhythm presets x treble/bass of the Accidentals preset (526 five-measure windows, 2,090 accidentals), for G, F, D and B-flat major, one-octave and Accidentals exercises in Fixed/Basic/Extended (288 windows, 3,612 labels), chords, Syncopated, and every first-measure window of nine imported scores, and each was rendered on real 916 x 240 canvases of the live page (headless Firefox, 1000 px viewport) with both builds' `canvas.js`. Screenshots are under `/tmp/claude-1000/-home-mirusser-MyRepos-PianoMapper/b46a9c4d-ef57-4803-b57f-ee9fe98c2ac1/scratchpad/`. The real app was also driven with mocked Web MIDI for items 6 and 7 and shot for items 1-5.

- [x] **Item 1: a stem-down note's own accidental sat about 0.2 px from its own stem. Fixed.**
  - **Root cause:** the accidental is placed `AccidentalHorizontalOffset` (0.019 scene-X, 8.4 px) left of its note for every note, but a stem-down stem stands on the left of its head (4.5 px from the centre, 2 px wide). Measured edge to edge on the real canvas, a sharp's ink overlapped the stem by 0.97 px and a flat's was 0.14 px from it: **all 1,040 stem-down accidentals of the 2,090 had at most 0.14 px, and the 323 sharps overlapped**.
  - **Fix:** a stem-down note (the stem direction the scene reports, so beam and chord overrides count) puts its accidental `StemDownAccidentalExtraOffset` = 0.005 scene-X (2.2 px) further left; `MinimumGapBeforeAccidentalNote` rose from 0.04 to 0.045 by the same amount because the spacing does not know the stem direction. Stem-up notes are untouched.
  - **Proof, failing first:** `BuildScore_StemDownNoteWithAnAccidental_KeepsTheGlyphClearOfItsOwnStem` (sharp and flat), `BuildScore_BeamedStemDownPairWithAccidentals_KeepsEachGlyphClearOfItsOwnStem` and the seven cases of `BuildScore_AccidentalsExercises_KeepEveryGlyphClearOfItsOwnStemDownStem` were red (-0.97 px and 0.14 px, wanted 1 px), then green; `BuildScore_StemUpNoteWithAnAccidental_KeepsTheGlyphWhereItAlwaysWas` is a guard. Setting the minimum gap back to 0.04 turns six of the Bug 3 spacing tests red again, so the bump is needed. Real measurement after: **0 own-stem overlaps, stem-down edge gap 1.23 to 2.34 px (before -0.97 to 0.14)**, still 0 overlaps with other stems or heads and a smallest gap to the note before of 1.4 px (`accmeasure2-acc-scenes.json-916x240.json` before, `accmeasure2-final-916x240.json` after).
  - **Pixel evidence:** `r2-i1-before.png` and `r2-i1-after.png` (three real exercises), `r2-i1-compare.png` (7x crops, before on the left, after on the right: the sharp no longer merges with its stem), and an imported score, `r2-i1-mia8-zoom.png` (Mia window 8, before above and after below, a sharp that touched its stem is clear).
  - **Imported scores:** Mia: 5 of 24 windows change, **only the accidental glyph of a stem-down note moves 2.2 px left, no note moves**; the other four fixtures are identical; only `scratch-mid-acc.musicxml` (crowded accidentals after eighths) moves notes (at most 5.2 px) and only for the better. Not gated.
- [x] **Item 2: accidentals derived from the key signature had no spacing reserve. Fixed.**
  - **Root cause:** the renderer draws a glyph when a note states an accidental **or** when its alteration differs from the key signature (`GetScoreAccidentalGlyph`), but the spacing (`RequiresExtraNotationWidth`, `EnsureRoomBeforeAccidentals`, `HasLeadingAccidental`) only looked at `ScoreNote.Accidental`. MusicXML omits `<accidental>` for a repeated accidental in a measure, so every such note was drawn crowded against the previous note's stem and a measure-leading one on the barline.
  - **Fix:** one helper, `GetNoteAccidentalGlyph(note, keyFifths)`, now decides both the drawn glyph and `PrintsAccidental`, which the three spacing predicates use, so room is reserved for exactly the glyphs that are drawn. `keyFifths` is threaded through `MapScoreNotationBeatToX` and the spacing chain (a private signature change only).
  - **Proof, failing first:** `BuildScore_AccidentalsDerivedFromTheKeySignature_ReserveTheSameRoomAsExplicitOnes`, `BuildScore_DerivedNaturalInASharpKey_ReservesRoomLikeAnExplicitNatural` and both cases of `BuildScore_MeasureLedByADerivedAccidental_KeepsTheGlyphClearOfTheBarlineBeforeIt` were red, then green; `BuildScore_NoteThatMatchesTheKeySignature_ReservesNoExtraRoom` guards that a sharp the key already has prints no glyph and gets no room. Generated exercises state their accidentals explicitly (Task 31), so every exercise scene dump is identical before and after this item.
  - **Pixel evidence:** `r2-i2-keyderived-compare.png` (scratch score with no `<accidental>` elements, before above, after below), `r2-i2-dotdouble-compare.png` (the double sharp of `dotted-double-accidental.musicxml`, drawn on the opening barline before, clear after), real UI `r2imp-keyderived-*.png` and `r2imp-dotdouble-*.png`.
  - **Imported scores (mandatory check):** of the nine scores, **identical scenes after this item for Mia (all 24 windows), `grand-staff-demo`, `musescore-export`, `single-staff`, `eighth-note-triplet`, both earlier scratch scores and `scratch-mid-acc`**; `dotted-double-accidental.musicxml` moves its first note and the rest 6.6 px right (its derived double sharp now clears the barline, better); the new `scratch-key-derived.musicxml` moves notes up to 14.7 px and the accidentals are clear of the stems (better). No score got visibly worse, so **no gating to exercise scores was needed**.
- [x] **Item 3: note-name labels crowded at narrow widths. Fixed (floor chosen from measurements).**
  - **Root cause:** the label is 16 px text centred under its note and nothing compared it with its neighbour. Neighbouring notes in an eighth run are 17.6 to 20 px apart at 916 px, a three-character name is about 28 px wide, and two-character names overlap in eighth runs too. Measured on real canvases at 916 px: **422 of 3,324 neighbouring label pairs (key presets, Accidentals and one-octave, Fixed/Basic/Extended) touched or overlapped (worst -14 px), 1,080 of 5,596 in the Accidentals exercises (-9.2 px), 138 of 620 in Syncopated (-8.7 px); at 1316 px (the 1400 px viewport) 130 of 3,324 (-7.4 px)**, so it was not only the three-character names and not only at 1000 px.
  - **Fix (smaller change of the two offered, so no stagger):** `canvas.js` `getNoteLabelFitScales` (exported, pure, measured with the real font) compares labels of one row (same `labelY`, so a chord's stacked names never compare with each other) in x order; for each neighbouring pair both get the widest common factor that keeps 2 px of air between them, a label takes the smaller factor of its two pairs, never below 0.5 (8 px), and a label with no crowded neighbour is not touched. It multiplies the scene's own `labelFontScale` (the severe-stack shrink). It is computed once per scene and width in the cached score layer and reused by the playback highlight, which redraws the highlighted note's label. Pairs closer than 4 px are left alone (the same moment drawn a little apart, such as a live note over another one, which shrinking cannot separate). Labels stay in their strip, top-anchored, so lessons 14, 18, 19, 21-26 and 31 are untouched: nothing moves, text only gets smaller.
  - **Choice of the 0.5 floor (the user can change `noteLabelMinimumFitScale`):** residual touching pairs at 916 px (keys / Accidentals / Syncopated): floor 0.6 left 106 / 74 / 8 (worst -3.3 px), 0.55 left 28 / 18 / 2, **0.5 leaves 6 / 10 / 0 (worst -0.25 px)** and 0 at 1316 px (smallest ink gap 1.39 px). The price is 8 px text in the densest eighth runs (818 of 3,612 labels in the key presets get smaller, 484 at 1316 px, never below 0.67 there).
  - **Proof, failing first (JS):** "grand staff shrinks labels that would touch their neighbour so they keep a gap", "grand staff never shrinks a label below its floor, however close the neighbour", "grand staff fits a label by its own neighbours' distance, so only the crowded one shrinks" and "score playback highlight redraws a crowded label at the same fitted size" were red, then green; guards: a label with room stays 16 px, labels in different rows are independent, labels of the same moment are untouched, the fit composes with the severe-stack scale. `FakeCanvasContext.measureText` gained a proportional `width` (test infrastructure only).
  - **Pixel evidence:** `r2-i3-strips.png` (label strips, before above and after below, for G major, F major and Accidentals exercises), real app with note names on `r2-ui-names-montage.png` (G major, Accidentals, F major bass Extended), imported `r2-i3-mia12.png`, `r2-final-mia0-22.png`, `r2-i3-imp-others.png`, real UI import `r2imp-mia-pair.png`.
  - **Imported scores:** the same fit applies to imported scores: Mia changes in 23 of 24 windows (the only pixel differences are smaller labels where the old ones overlapped, e.g. "C#4F#4G#4A4G#4F#4" now reads as six separate names), `eighth-note-triplet` 1 window, the scratch scores; `grand-staff-demo`, `musescore-export` and `single-staff` are pixel-identical (0 differing pixels on real canvases; `grand-staff-demo` and `musescore-export` also through the real file input). None got worse. Still touching even at 8 px: the four labels of a triplet, whose notes are about 11 px apart.
- [x] **Item 4: faint ties. Fixed, and the arriving tie turned out to be not drawn at all.**
  - **Root cause (two parts):** (a) `drawTie`'s thickest point is 0.08 staff space, 0.6 px on the 7.5 px staff space of the smallest score canvas, a hairline. (b) The half tie that arrives at the start of a row is `GrandStaffLayout.ScoreX0` to the first note, and the first note of a row is placed exactly at `ScoreX0`, so the tie had **zero length** (`BuildScore_RowStartingWithATiedContinuation_LeavesRoomForTheArrivingTieToBeSeen` reported -6 px of visible length): it was not "practically invisible", it was missing, and a thicker stroke alone could not have fixed it. (Related finding, not changed: scene X values are float-derived doubles, `-0.56f` arrives as -0.5600000023841858, so `isScoreEdgeX`'s `Number.EPSILON` test never matches a real scene and edge half ties always get the 0.8 staff-space end inset.)
  - **Fix (a):** the thickest point is never less than 1.2 px (`tieMinimumCenterThicknessPixels`), a floor in pixels so taller canvases keep exactly 0.08 staff space. The tapered lens shape (zero thickness at both ends) and the 0.8 staff-space endpoint gaps are untouched.
  - **Fix (b):** a row whose first note finishes a tie from the previous row (`HasArrivingTie`, exercise scores with ties drawn only) gets `ArrivingTieClearance` 0.02 scene-X (8.8 px) more room after the opening barline, applied in the note, rest and cursor mappings alike; the half tie now starts at the opening barline (`ScoreX0 - OpeningBarlineLead`) and `isScoreEdgeX` recognises that x (with a tolerance) so it has no gap before it. Visible length: about 11.6 px.
  - **Free play:** the same renderer draws free-play measure-boundary ties. Their thickness is 0.08 x 15.6 px = 1.25 px at the 460 px canvas of a 1000 px viewport and 1.5 px at the largest, so **unchanged** (pinned by a test); only the smallest free-play height (400 px, 13.4 px staff space) goes from 1.07 px to 1.2 px (+0.13 px). Free-play layout does not use the arriving-tie room. Real free play was shot with held notes across barlines, `r2-i4-fp-5199b.png`.
  - **Proof, failing first (JS):** "grand staff ties stay at least 1.2 px thick on the smallest score canvas" and "grand staff ties arriving from the left edge are at least 1.2 px thick too" were red, then green; "grand staff starts the tie that arrives at a row at the opening barline, with no gap before it" fails when the `isScoreEdgeX` condition is removed (mutation checked); guards: the lens shape and endpoint gaps at 240 and 900 px, and free-play thickness unchanged. **(C#):** `BuildScore_RowStartingWithATiedContinuation_LeavesRoomForTheArrivingTieToBeSeen` red then green; guards: no change without the ties option (imported scores), none for a row that starts untied, the cursor stays on its note.
  - **Pixel evidence:** `r2-i4-z-top.png` and `r2-i4-z-inrow.png` (8x crops, before above and after below: the arriving half tie, the leaving half tie, a cross-barline tie), `r2-i4m.png` (the review-mark halos of Task 22b with ties, before above and after below: the tie is drawn over the rings and still reads), real exercises `r2-ui-sync-*.png`.
  - **Imported scores:** unchanged by construction (they draw no ties and do not reserve room): scenes identical, 0 changed windows.
- [x] **Item 5: chord voicings reached too high on the bass staff. Fixed, all nine voicings kept.**
  - **Root cause:** the nine voicings are one table of scale offsets used for both staves; on the bass staff first-inversion V (B3 D4 G4) puts G4 on the third ledger line (the other eight need at most two; the treble staff's highest is G5, no ledger line).
  - **Fix:** `FitChordVoicingToStaff` voices a voicing an octave lower when any of its notes needs more than `MaximumChordLedgerLines` = 2 ledger lines on the staff (a small `CountLedgerLines` in the composer, the same rule as the tests use for the key presets). Only the bass staff's first-inversion V changes, to **B2 D3 G3**; the treble staff and the seeded structure (least-used pick, no immediate repeat, determinism) are unchanged.
  - **Design choice (user can adjust):** I kept all nine by lowering that one voicing an octave instead of dropping it. If you would rather have eight voicings on the bass staff, remove it from `ChordVoicingScaleOffsets` for bass instead.
  - **Proof, failing first:** `Compose_Chords_NeedAtMostTwoLedgerLinesOnTheirStaff` (Bass: G4 needed three) and the bass case of `Compose_Chords_LongExerciseUsesAllNineVoicingsEachSortedOnTheStaff` were red, then green (treble green throughout). **Pre-existing test changed deliberately:** `Compose_Chords_LongExerciseUsesAllNineVoicingsEachSortedOnTheStaff` derived its literals from `octave + offset` for both staves, which cannot express one lowered voicing; it now spells out both staves' nine voicings as literals (bass `B2 D3 G3` in place of `B3 D4 G4`) and its signature lost the `octave` parameter. The other chord tests passed unchanged.
  - **Pixel evidence:** `r2-i5-chords-bass.png` (bass chord exercise, before above and after below: the B3 D4 G4 chord with G4 high above the staff is now B2 D3 G3 in the staff), real app `r2-ui-chords-bass-*.png`. Scene dumps: only the bass chord scenes changed; treble chords, key presets, Accidentals, Syncopated and every imported score are identical.
- [x] **Item 6: clean notes in the Play-along review were not the Wait-for-me green. Fixed.**
  - **Root cause:** in Play along the review colors each note with the practice engine's own verdict. The engine judges every release, so a key let go early is `TooShort` (`#facc15`, yellow) even in modes that do not grade holds, where the exercise itself calls the prompt clean. The clean notes therefore drew yellow/amber, close to the orange `#fb923c` of a late note and its halo (the earlier note called this "default amber"; the real colors were read from the canvas, `#facc15` x 16 for 16 clean notes, 2 x `#fb923c`, 1 x `#f87171`, 1 x `#94a3b8`). Only an exact-length hold gave green.
  - **Fix:** `SightReadingExerciseCoordinator.BuildPlayAlongReviewVerdicts` returns the engine's verdicts with `Verdict.Correct` for every note of a prompt the exercise graded clean (`ExerciseReviewMarks.Classify` is `Clean`), taken from the exercise's own prompt results so both pacings agree; the page applies it for a play-along review (also in the final tick of the run). No `Verdict` member and no `verdictColors` ordinal changed. Late, early, wrong and missed notes keep the engine's colors, and so does a too-short note in a mode that grades holds (as Wait for me shows TooShort there).
  - **Proof, failing first:** `BuildPlayAlongReviewVerdicts_CleanPromptsReleasedEarlyWhereHoldsAreNotGraded_AreCorrectLikeInWaitForMe` was red (the engine says TooShort, asserted first as the premise), then green; guards: `..._PromptsThatWereNotClean_KeepTheEnginesColors` (hold-graded mode), `..._PerfectRun_IsAllCorrect...` and `..._WhileTheRunIsStillGoing_ReturnsTheEnginesVerdictsUnchanged`.
  - **Pixel evidence (real app, mocked MIDI, 1000 px viewport, same plan of on-time, late, early, wrong and skipped notes):** `r2-i67-compare.png` (before above: yellow clean notes; after below: green clean notes, orange late, red wrong, grey missed) and `r2-i6-before-pa.png`.
- [x] **Item 7: the red cursor line stayed at the end of the score in the Play-along review. Fixed.**
  - **Root cause:** the page's play-along tick drew the cursor for `Running` **or `Finished`**, and the tick that finishes the run is also the one that moves the exercise to Review; nothing redrew afterwards unless an event came, so the line stayed at the end of the score (whether it shows depends on the end of the exercise lying inside the visible window: the cursor at the window's right edge is not drawn).
  - **Fix:** `ExercisePlayAlongController.VisibleCursorBeats` is the cursor only while the state is `Running` (null in the count-in, as before, and once finished); the page uses it. The JS-driven cursor already stops at the run's completion.
  - **Proof, failing first:** `VisibleCursorBeats_OnceTheRunHasFinishedAndTheExerciseIsInReview_HasNoCursorLine` was red, then green; guards: `..._CountIn_HasNoCursorLine` and `..._WhileTheRunIsGoing_FollowsThePracticeCursor`.
  - **Pixel evidence:** `r2-i7-compare.png` (a finished 2-measure play-along; rows 1 and 2 are the before build immediately after the run and 2 s later: the red line sits after the last note both times; rows 3 and 4 are the after build at the same moments: no line), `r2-i7-keep.png` (after build: no line in the count-in, a line while running).
- **Tests that existed before and were changed, and why:** (1) `GrandStaffSceneBuilderTests.AssertAccidentalsClearOfPreviousNote` (used by the Bug 3 spacing tests): it hard-coded the glyph 0.019 left of its note and a 0.039 minimum gap, both wrong for a stem-down note; it now finds the note by proximity and requires the real glyph-to-note distance plus 0.020 (its constant became `ReachLeftOfAnAccidentalGlyph`), which is the same pixel requirement (item 1). (2) `GrandStaffSceneBuilderTests.BuildScore_TieThatLeavesTheVisibleMeasures_DrawsAHalfTieToTheRightEdgeAndTheNextWindowOneFromTheLeftEdge`: the arriving half tie now starts at the opening barline, not at `ScoreX0` (item 4). (3) `SightReadingExerciseComposerTests.Compose_Chords_LongExerciseUsesAllNineVoicingsEachSortedOnTheStaff` (item 5, above). (4) `canvas.test.mjs`, "grand staff tie height and tapered thickness derive from staff spacing": it rendered at 240 px, where the 1.2 px floor now applies, so it renders at 600 px to keep pinning the 0.08 staff-space rule above the floor (item 4); and the fake context's default `measureText` gained a `width`. No other assertion changed.
- **Design choices the user could adjust:** item 4 uses a pixel floor of 1.2 px (`tieMinimumCenterThicknessPixels`) and 0.02 scene-X of room for an arriving tie (`ArrivingTieClearance`); item 5 lowers one bass voicing an octave rather than dropping it; item 3 uses a 0.5 floor and 2 px of air. Nothing contradicts a lesson in `.agents/lessons.md`, so no item was stopped; no lesson was added because no correction was needed.
- **Full verification (2026-10-03, after the last change):** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` 1620 passed, 0 failed (baseline 1591 plus 29 new: 11 for item 1, 5 for item 2, 4 for item 4, 2 for item 5, 4 for item 6, 3 for item 7); `node --test PianoMapper.Tests/JavaScript/*.test.mjs` 106 of 106 passed (baseline 93 plus 8 for item 3 and 5 for item 4); `dotnet build PianoMapper.slnx --configuration Release --no-incremental` 6 projects, 0 errors, 0 warnings; `dotnet format PianoMapper.slnx --verify-no-changes` exit 0; `git diff --check` and `git diff --cached --check` exit 0. Nothing was committed, pushed, staged, stashed, reset or checked out, and no file was added to the repository (the scratch scene-dump test was copied in only for single runs and removed each time; the only changes are edits to existing files).
- **Found, outside scope, not changed:** (1) scene X values are float-derived, so `isScoreEdgeX` never matches `ScoreX0`/`ScoreX1` as a real scene sends them (see item 4); (2) the JS cursor (`mapScoreNotationBeatToX`) is a plain beat mapping that does not know the accidental, tie or chord spacing, so the cursor line is a few pixels off its note in measures that add room (it already was for accidentals); (3) a tie in the bottom staff space still runs along the bottom staff line (Task 36's observation); (4) in the densest measures (four accidentals within a beat and a half) a few accidentals still sit 1-2 px from a beam (the measurement's beam test is crude, 11 of 2,090, unchanged by this round); (5) in the Play-along review a note the engine judged TooShort or TooLong keeps that yellow in a mode that grades holds, and a wrong key draws red while Wait for me draws nothing on the staff for it; (6) labels of notes about 11 px apart (triplets) still touch even at 8 px; (7) Task 9 and the manual physical-keyboard steps stay pending ("NOT performed").
