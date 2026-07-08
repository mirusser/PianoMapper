# Verification Report: OpenTK Synced Visualization — Phase 2 (Note Timeline & Piano-Roll)

Plan: `docs/plans/opentk-synced-visualization.md` (Task 2.1, Task 2.2)
Scope: read-only verification of the implementation completed in the prior session. No code was edited as part of this verification.

## Completeness

| Plan item | Status | Evidence |
|---|---|---|
| Task 2.1 AC1: tracking carries note name, frequency, start time, duration, buffer reference | Done | `PianoMapper/NoteInstance.cs:6-12`, `PianoMapper/NoteTimeline.cs:30-50` |
| Task 2.1 AC2: thread-safe snapshot without blocking audio thread | Done | `PianoMapper/NoteTimeline.cs:52-59` (lock scope is a fast list copy, distinct from `activeNotesLock`) |
| Task 2.1 AC3: existing playback/cleanup still functions | Done | `PianoMapper/PianoMapperWindow.cs:134-153` — AL cleanup logic unchanged; only note construction now goes through `noteTimeline.Add(...)` |
| Task 2.1 Verification: `dotnet build` succeeds | Done | Re-ran fresh: `dotnet build PianoMapper.slnx` → 3 projects, 0 errors, 0 warnings |
| Task 2.1 Verification: manual keypress check | Not run | No display/audio device in this sandbox (documented in plan). Automated coverage via `NoteTimelineTests` (4 tests, all passing). |
| Task 2.2 AC1: bar drawn at correct pitch row, grows with duration | Partial | Logic implemented and unit-tested (`PianoRollLayoutTests.GetBarRect_NoteStillPlaying_WidthGrowsWithElapsedTime`, `..._HigherFrequency_RendersAboveLowerFrequency`), but on-screen behavior never observed |
| Task 2.2 AC2: concurrent notes render as separate bars | Partial | `NoteTimeline` supports concurrent entries (`Add_MultipleConcurrentNotes_AllAppearAsSeparateEntries`); `PianoRollRenderer.Render` iterates the full snapshot (`PianoRollRenderer.cs:42-51`); visual not observed |
| Task 2.2 AC3: old notes scroll off after rolling window | Partial | Culling logic unit-tested (`GetBarRect_NoteEndedBeforeVisibleWindow_ReturnsNull`); visual not observed. **See Blocker below** — this AC does not hold for notes cleared via Spacebar. |
| Task 2.2 Verification: manual play-and-observe check | Not run | No display available. |

**Scope drift:** none. `docs/remote-access.md` shows as modified in the working tree but `git log -p -- docs/remote-access.md` confirms it was changed in a prior, unrelated commit (`ca44a1b`) — not touched by this implementation.

## Findings

### Blockers

- **Spacebar-clear desyncs the piano-roll from actual audio state** — `PianoMapper/PianoMapperWindow.cs:51` / `PianoMapper/AudioDispatcher.cs:73-89`
  > `audioDispatcher.ClearActiveNotes(activeNotes, activeNotesLock);`
  `ClearActiveNotes` stops/deletes the AL source+buffer and clears `activeNotes`, but never touches `noteTimeline`. `PianoRollLayout.GetBarRect` (`PianoRollLayout.cs:24`) computes the bar's end from the note's original fixed `Duration`, so a cleared note's bar keeps rendering and growing until its original duration would have elapsed — even though the sound stopped instantly. This directly contradicts Task 2.2's own verification goal ("piano-roll visually matches what's heard, with no visible drift") for the one case (manual clear) where audio and visual state are supposed to change together but currently can't, since nothing tells `noteTimeline` a clear happened.

### Important

- **`NoteInstance` should be `sealed` and arguably a `record`** — `PianoMapper/NoteInstance.cs:4`
  > `public class NoteInstance`
  Per code-standards, a type with only required init-only properties and no behavior fits the "Records and DTOs" guidance (record), and separately, "classes are sealed by default unless subclassing is intentional" — no subclassing occurs anywhere in the codebase.

- **No test coverage for `frequency <= 0` in `PianoRollLayout.MapFrequencyToY`** — `PianoMapper/Rendering/PianoRollLayout.cs:47-52`
  > `var semitoneOffset = 12.0 * Math.Log2(frequency / ReferenceFrequency);`
  Zero or negative frequency produces `NaN`/`-Infinity` through `Math.Log2`, which `Math.Clamp` does not sanitize. This feeds directly into GL vertex data via `PianoRollRenderer`. Not reachable through the current key-based input path (all frequencies come from `Consts.GenerateKeyToFrequencyMapping`, which only emits positive values), but the function has no guard and no test pins the assumption.

- **`MapTimeToX` does not clamp `x0`, and no test covers a note that started before the visible window while still playing** — `PianoMapper/Rendering/PianoRollLayout.cs:40-45`
  A held note whose `StartTime` is more than `RollingWindowSeconds` in the past would compute an `x0` below `-1` NDC (only `x1`, driven by `now`, stays in range). Untested; behavior under this real scenario (holding a note past 8s) is unconfirmed.

- **`Add_MultipleConcurrentNotes_AllAppearAsSeparateEntries` doesn't test concurrency** — `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs:31-47`
  All three `Add` calls run sequentially on the test thread. `NoteTimeline`'s entire raison d'être (per its own doc comment and `Lock notesLock`) is safe concurrent access from the audio thread and render thread simultaneously — that path is never exercised by any test.

- **Cross-class magic-number coupling has no enforcement or shared source of truth** — `PianoMapper/NoteTimeline.cs:9-11` vs `PianoMapper/Rendering/PianoRollLayout.cs:9`
  `NoteTimeline.RetentionSeconds = 15.0` is sized relative to `PianoRollLayout.RollingWindowSeconds = 8.0` only via a comment ("Task 2.2 uses an 8s window"). If either constant changes independently, notes could be pruned mid-scroll with no compiler error and no failing test, since each class's tests only exercise that class in isolation.

- **Plan's Task 2.2 "Files likely touched" is internally inconsistent** — `docs/plans/opentk-synced-visualization.md:95`
  Lists only `PianoRollRenderer.cs` and `PianoMapperWindow.cs`, while the inline implementation note two lines above (line 88) credits `PianoRollLayout` (and its test file) as doing the actual math — `PianoRollLayout.cs` and the supporting `BarRect.cs` are absent from the same section's file list.

- **README.md no longer describes the app** — `README.md:3,10,13`
  Still says "a small C# console app," "Console waveform plotting via `ConsolePlot`," and ".NET SDK 9.0." The app is now a windowed `GameWindow` app on `net10.0` with a GL piano-roll, and there's no mention of the new `PianoMapper.Tests` project or how to run it. This predates Phase 2 (stale since the earlier OpenTK-window commit) but Phase 2 widens the gap further by adding a whole rendering feature the README doesn't acknowledge.

### Nice-to-have

- `NoteTimeline.Add` takes 6 positional params including two adjacent same-typed `int`s (`sourceId`, `bufferId`) the compiler can't catch if transposed — `PianoMapper/NoteTimeline.cs:30`. Single call site currently passes them correctly; flagged per Simplicity First, not proposing a parameter object for one call site.
- `BarHalfHeight = 0.02f` has no rationale comment, unlike its sibling constants in the same file — `PianoMapper/Rendering/PianoRollLayout.cs:13`.
- `var` used for `double` primitives from arithmetic/property access (e.g. `nowSeconds`, `semitoneOffset`) — code-standards says never use `var` for primitives — `PianoMapper/Rendering/PianoRollLayout.cs:21,49` (representative).
- `activeNotesLock` (`PianoMapperWindow.cs:16`) is still a plain `object` passed by reference into `AudioDispatcher.ClearActiveNotes`'s public signature, inconsistent with the new, correctly-typed `Lock notesLock` sitting right next to it.
- `PianoRollRenderer.NoteColor` (`PianoRollRenderer.cs:12`) is a single flat constant — every bar renders identically; fine for now, worth a decision if per-pitch/velocity styling is ever wanted.
- Missing fencepost tests: exact retention boundary (`elapsed == duration + retention`), exact window-start boundary (`noteEnd == windowStart`), and a zero-duration note — none currently would catch an off-by-one in the `>` vs `>=` comparisons.
- Plan's Task 2.1 "Files likely touched" is stale — lists `AudioDispatcher.cs`/`Program.cs` (untouched), omits `NoteTimeline.cs`/`PianoMapperWindow.cs` (`docs/plans/opentk-synced-visualization.md:76`).
- Plan's Task 2.1 description still says "from a shared `Stopwatch` started at launch" (line 63) while the implementation uses an injectable `TimeProvider` instead (better for testability, per `NoteTimelineTests`'s use of `FakeTimeProvider`) — the doc text wasn't updated to reflect the actual (arguably improved) design choice.

## Codegraph impact

- `PlayNoteAsync` — 2 callers (`OnUpdateFrame` at `PianoMapperWindow.cs:35`, `PlayRandomMeasureAsync` at `PianoMapperWindow.cs:163`), both updated to the new 3-arg signature. No stale callers.
- `NoteInstance` — 7 references (`NoteTimeline.Add`/`notes`/`Snapshot`, `PianoRollLayoutTests.CreateNote`, `PianoRollLayout.GetBarRect`, `PianoRollRenderer.Render`, `PianoMapperWindow.activeNotes`), all consistent with the extended required-property shape. No unupdated callers.

## Tests

- Ran: `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` → **10/10 passed**, 0 failed, 0 skipped, 35ms.
- Ran: `dotnet build PianoMapper.slnx` → 3 projects, **0 errors, 0 warnings**.
- No integration-tier tests exist in this repo; unit tests are the only automated tier.

## Behavioral check

Not run. This sandbox has no accessible display server (confirmed in the prior implementation session: `GLFWException: Failed to detect any supported platform` when attempting `dotnet run`), so the primary Task 2.2 acceptance criterion (visually confirm the piano-roll matches playback) could not be exercised. This gap was already disclosed in the plan file itself; re-confirmed here rather than silently skipped.

## Acceptance criteria

- [x] Task 2.1 AC1 — note metadata carried — `NoteInstance.cs:6-12`
- [x] Task 2.1 AC2 — thread-safe snapshot — `NoteTimeline.cs:52-59`
- [x] Task 2.1 AC3 — existing cleanup unaffected — `PianoMapperWindow.cs:134-153`
- [ ] Task 2.2 AC1/AC2/AC3 — implemented and unit-tested at the logic layer; on-screen behavior unverified, and AC3 ("old notes scroll off") does not hold for manually-cleared notes per the Blocker above

## Recommendation

**Remediation needed before the Phase 2 checkpoint.** The Blocker (Spacebar-clear leaves stale bars on the piano-roll) is a real, code-confirmed contradiction of Task 2.2's stated goal, not a hypothetical — it should be fixed (e.g., have `ClearActiveNotes`/`Add` also truncate or remove the corresponding `NoteTimeline` entries) before checking off Task 2.2's acceptance criteria or advancing to Phase 3. The Important findings (NaN-prone `MapFrequencyToY`, unclamped `x0` for long-held notes, the mistitled "concurrent" test, the constant coupling, and the two doc-drift items) are worth addressing but don't block a checkpoint review by themselves. The manual visual/audio checks remain genuinely unverifiable until run on a machine with a display and audio device.
