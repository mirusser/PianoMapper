# Implementation Plan: OpenTK Synced Visualization — Phase 2 Remediation

## Overview

Verification of Phase 2 (`docs/plans/opentk-synced-visualization.md`, Tasks 2.1/2.2) found one Blocker and six Important issues, recorded in `.agents/Plans/verification-findings-opentk-phase-2.md`. This plan fixes those before the Phase 2 checkpoint. Nice-to-have findings from that report are intentionally out of scope — see Open Questions.

## Architecture Decisions

- **Spacebar-clear removes the corresponding entries from `NoteTimeline` outright** (bar disappears immediately) rather than freezing them at their actual stop time. Matches the existing "space stops everything" intent and avoids adding mutable stop-time state to the otherwise-immutable `NoteInstance`.
- **`PianoMapperWindow` orchestrates the fix**, not `AudioDispatcher`: `AudioDispatcher` stays unaware of `NoteTimeline` (existing, deliberate decoupling from Phase 2). The window already holds references to both `activeNotes`/`activeNotesLock` and `noteTimeline`, so it snapshots the notes being cleared (under `activeNotesLock`) and tells `noteTimeline` to drop them, synchronously — no need to route this through the audio thread's `Enqueue`.
- **`NoteInstance` becomes a `sealed record`** (still non-positional, same required `init` properties) rather than a plain class — mechanical, low-risk, matches code-standards' DTO guidance. Reviewed for equality-semantics risk: see Risks.

## Task List

### Phase 1: Code Fixes

#### Task 1: Fix Spacebar-clear leaving stale piano-roll bars (Blocker)
**Description:** Pressing Spacebar stops audio via `AudioDispatcher.ClearActiveNotes` but never updates `NoteTimeline`, so cleared notes' bars keep growing on the piano-roll until their original (now-irrelevant) duration elapses. Add a way to remove specific notes from `NoteTimeline`, and call it from the Spacebar handler using a snapshot of what's about to be cleared.

**Acceptance criteria:**
- [x] `NoteTimeline` exposes a thread-safe way to remove a given set of notes (e.g. `Remove(IReadOnlyCollection<NoteInstance>)`), guarded by its existing lock.
- [x] Pressing Spacebar removes exactly the notes that were in `activeNotes` at that moment from `noteTimeline` — no lingering growth after a clear, and no unrelated (already-finished) notes are affected. *(Verified at the `NoteTimeline.Remove` logic level via `Remove_ActiveNote_NoLongerAppearsInSnapshot` and `Remove_OneOfMultipleNotes_OnlyRemovesSpecifiedNote`.)*
- [x] Existing audio-cleanup behavior (AL source/buffer stop + delete) is unchanged.

**Verification:**
- [x] New test: `NoteTimelineTests` — add a note, call `Remove` with it, assert `Snapshot()` no longer contains it.
- [x] `dotnet build` succeeds; `dotnet test` passes.
- [ ] Manual check: hold a note, press Space, confirm its bar disappears immediately instead of continuing to grow. *(Not run — no display available in this sandbox, same limitation as Phase 2.)*

**Dependencies:** None

**Files likely touched:** `PianoMapper/NoteTimeline.cs`, `PianoMapper/PianoMapperWindow.cs`, `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs`

**Estimated scope:** S

#### Task 2: Make `NoteInstance` `sealed` and a `record`
**Description:** `NoteInstance` has only required init-only properties and no behavior — per code-standards it should be a `sealed record`, not a plain `class`.

**Acceptance criteria:**
- [x] `NoteInstance` is declared as `public sealed record NoteInstance` with the same property shape (no positional-record syntax forced on callers).
- [x] `PianoMapperWindow.cs`'s `activeNotes.Remove(note)` still removes exactly the intended instance under the record's value-equality semantics (verify, don't assume — see Risks). *(Confirmed via new test `Remove_NotesWithIdenticalMetadataButDifferentSourceId_OnlyRemovesMatchingInstance`: `SourceId`/`BufferId` are unique per note since they come from `AL.GenSource()`/`AL.GenBuffer()`, so value equality never collapses two distinct notes.)*

**Verification:**
- [x] `dotnet build` succeeds.
- [x] `dotnet test` — all existing tests, including the `Assert.Same` checks in `NoteTimelineTests.cs`, pass unchanged.

**Dependencies:** None (recommend doing after Task 1 to avoid two in-flight edits to code that constructs `NoteInstance`)

**Files likely touched:** `PianoMapper/NoteInstance.cs`

**Estimated scope:** XS

#### Task 3: Harden `PianoRollLayout` against edge-case pitch/time inputs
**Description:** `MapFrequencyToY` propagates NaN/-Infinity for `frequency <= 0` (via `Math.Log2`), and `MapTimeToX` doesn't clamp its output, so a note held longer than `RollingWindowSeconds` produces an `x0` outside the [-1, 1] NDC range. Neither path is tested.

**Acceptance criteria:**
- [x] `frequency <= 0` produces a defined, clamped Y value instead of NaN/-Infinity reaching vertex data.
- [x] A note whose `StartTime` is older than the visible window (but still playing) produces `x0` clamped to the same [-1, 1] range as everything else.
- [x] All existing `PianoRollLayoutTests` continue to pass unchanged.

**Verification:**
- [x] New tests: one for non-positive frequency, one for a note started before the visible window (long-held note).
- [x] `dotnet test` passes.

**Dependencies:** None

**Files likely touched:** `PianoMapper/Rendering/PianoRollLayout.cs`, `PianoMapper.Tests/UnitTests/PianoRollLayoutTests.cs`

**Estimated scope:** S

#### Task 4: Prove `NoteTimeline` thread-safety with a real concurrent test
**Description:** `Add_MultipleConcurrentNotes_AllAppearAsSeparateEntries` calls `Add` three times sequentially on one thread — it doesn't test the concurrency `NoteTimeline` is actually built for (audio thread writes, render thread reads). Add a test that exercises `Add`/`Snapshot` from multiple real threads.

**Acceptance criteria:**
- [x] A test calls `NoteTimeline.Add` from multiple concurrent threads (e.g. several `Task.Run` calls awaited via `Task.WhenAll`) and asserts every note appears in the final snapshot with no exceptions or lost entries.
- [x] The old sequential test is renamed or removed so no test name overstates what it verifies. *(Renamed to `Add_MultipleNotes_AllAppearAsSeparateEntries`.)*

**Verification:**
- [x] `dotnet test` passes; run at least twice locally to check the new test isn't itself flaky. *(Ran 3x, consistently green.)*

**Dependencies:** None (touches the same test file as Task 1's new test — sequence after Task 1 to avoid overlapping edits)

**Files likely touched:** `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs`

**Estimated scope:** XS

#### Task 5: Enforce the `RetentionSeconds` / `RollingWindowSeconds` coupling
**Description:** `NoteTimeline.RetentionSeconds` (15s) must stay larger than `PianoRollLayout.RollingWindowSeconds` (8s), or notes get pruned mid-scroll. Today that's only a comment. Make the invariant enforced, not just documented.

**Acceptance criteria:**
- [x] The relationship between the two constants is checked by code or a test — e.g. a test asserting `NoteTimeline`'s retention exceeds `PianoRollLayout.RollingWindowSeconds`, or `NoteTimeline` derives its retention from a value passed in referencing that constant. *(`RetentionSeconds` made `internal` + `InternalsVisibleTo`; new test `RetentionSeconds_ExceedsPianoRollRollingWindow` pins the invariant.)*
- [x] Breaking the invariant (e.g., lowering retention below the rolling window) causes a build or test failure, not silent pruning.

**Verification:**
- [x] `dotnet test` passes. During implementation, temporarily break the invariant to confirm the new guard/test actually fails, then revert. *(Confirmed: setting `RetentionSeconds = 5.0` made the test fail with a clear message; reverted to `15.0`.)*

**Dependencies:** None

**Files likely touched:** `PianoMapper/NoteTimeline.cs`, `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs`

**Estimated scope:** S

### Checkpoint: After Phase 1
- [x] `dotnet build` succeeds, `dotnet test` passes (existing + new tests). *(3 projects, 0 errors/0 warnings; 18/18 tests passing.)*
- [x] Blocker and all Important code-level findings resolved.
- [ ] Review with human before Phase 2 docs cleanup / before signing off the Phase 2 checkpoint.

### Phase 2: Docs Cleanup

#### Task 6: Fix plan-file drift
**Description:** `docs/plans/opentk-synced-visualization.md`'s Task 2.1/2.2 "Files likely touched" lists don't match what was actually built, and Task 2.1's description still says "shared `Stopwatch`" though the implementation uses `TimeProvider`.

**Acceptance criteria:**
- [x] Task 2.1 "Files likely touched" lists `NoteTimeline.cs` and `PianoMapperWindow.cs`; drops `AudioDispatcher.cs`/`Program.cs` if they remain untouched after Phase 1. *(Confirmed still untouched by Phase 1 remediation.)*
- [x] Task 2.2 "Files likely touched" includes `PianoRollLayout.cs` and `BarRect.cs` alongside `PianoRollRenderer.cs`.
- [x] Task 2.1's description reflects the actual `TimeProvider`-based clock instead of "shared Stopwatch."

**Verification:**
- [x] Diff review — confirm no other plan content changed.

**Dependencies:** Phase 1 checkpoint (so the file list reflects final reality, including any new test files from Tasks 1–5)

**Files likely touched:** `docs/plans/opentk-synced-visualization.md`

**Estimated scope:** XS

#### Task 7: Refresh `README.md`
**Description:** `README.md` still describes PianoMapper as a console app with `ConsolePlot` waveform plotting and `.NET SDK 9.0` — none of which is current.

**Acceptance criteria:**
- [x] README describes the current windowed OpenTK app (GameWindow + piano-roll), not a console app.
- [x] `.NET SDK 9.0` corrected to `10.0`.
- [x] The `ConsolePlot` line is removed or corrected to reflect current behavior. *(Corrected: still present as `PCM.VisualizeWave`, noted as ad-hoc/not wired into the main loop — confirmed by grep that it's unreferenced elsewhere.)*
- [x] README documents how to run tests (`dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`).

**Verification:**
- [x] Manual proofread against the app's actual current behavior and project files.

**Dependencies:** None

**Files likely touched:** `README.md`

**Estimated scope:** XS

### Checkpoint: Complete
- [x] All Blocker + Important findings from `verification-findings-opentk-phase-2.md` resolved.
- [x] `dotnet build` + `dotnet test` green.
- [x] Docs match actual implementation.
- [ ] Ready to re-verify or proceed to the original Phase 2 checkpoint / Phase 3. *(Manual/visual checks still unverifiable in this sandbox; recommend a real re-verification pass once run on a machine with a display.)*

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Converting `NoteInstance` to a `record` switches `List<NoteInstance>.Remove` (used in `PianoMapperWindow.cs` audio cleanup) from reference to value equality | Low-Med | Task 2's acceptance criteria explicitly require confirming `Remove` still targets the correct instance; each note's `StartTime` is effectively unique in practice, but this must be verified, not assumed |
| Fixing the Blocker (Task 1) needs a consistent read of `activeNotes` before `AudioDispatcher.ClearActiveNotes` asynchronously clears it on the audio thread — a race is possible if the snapshot isn't taken correctly | Med | Task 1 must snapshot under `activeNotesLock` before enqueuing the clear; its test must assert no dropped or duplicated removals |
| Nice-to-have findings (parameter-list smell in `NoteTimeline.Add`, missing constant-rationale comments, `var`-on-primitives style, `activeNotesLock` typed as `object`, flat `NoteColor`, missing fencepost tests) are left unaddressed | Low | Explicitly deferred — see Open Questions |

## Open Questions

- Should the deferred Nice-to-have findings get their own follow-up pass, or stay backlog items? Recommend only revisiting if one of them recurs or actively blocks Phase 3 work.
- The manual/visual acceptance criteria in both this plan and the original Phase 2 plan remain unverifiable in the current sandbox (no display/audio device). Who will run that check, and when, before Phase 2 is truly signed off?
