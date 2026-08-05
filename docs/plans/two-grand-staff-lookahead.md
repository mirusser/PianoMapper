# Implementation Plan: Two-Grand-Staff Look-Ahead Playback

**Date:** 2026-08-02

**Status:** Approved

**Scope:** Blazor WebAssembly imported-score view

## Goal

Replace the imported score's single, replace-in-place grand staff with two persistent physical grand-staff rows. The current page and the next five-measure page remain visible together. As playback or Practice crosses a page boundary, the cursor moves to the already-rendered other row and the row just completed is recycled with the following page.

The result should let a pianist read ahead without notation changing underneath the line they are currently playing.

## Repository Context

- `GrandStaffLayout.VisibleMeasureCount` fixes one rendered score window at five measures.
- `PianoMapper.Web/Pages/Piano.razor` currently owns one `canvasScene`, one `firstVisibleMeasure`, one `GrandStaffSceneCache`, and one `ScoreCursorPlaybackState`.
- `PianoMapper.Web/Components/PianoCanvas.razor` renders one notation canvas plus the shared waveform/spectrum controls.
- `GrandStaffSceneCache` is intentionally instance-scoped and documents the intended ownership as one cache per grand-staff view. The paired view should therefore use two cache instances rather than turning the cache into shared global state.
- Automated score playback schedules the entire selected score once. Its JavaScript cursor maps absolute audio-clock beats into the current five-measure window; it currently disappears after leaving that window.
- Practice rebuilds the dynamic scene every 16 ms but still composes against the same single `firstVisibleMeasure`.
- The automatic rollover the user currently observes is the free-play window derived by `GrandStaffLayout.GetLiveFirstVisibleMeasure`; imported-score playback itself has no automatic page rotation today.
- The JavaScript animation loop currently schedules frames only when an analyser is visible. The paired cursor feature must make cursor animation independent of waveform/spectrum visibility.

## Restated User Experience

For a score with at least four logical five-measure pages, the physical rows should behave as follows:

| Playback page | Upper row | Lower row | Cursor location |
|---|---|---|---|
| Measures 1–5 | Measures 1–5 | Measures 6–10, preloaded | Upper |
| Measures 6–10 | Measures 11–15, preloaded | Measures 6–10 | Lower |
| Measures 11–15 | Measures 11–15 | Measures 16–20, preloaded | Upper |
| Measures 16–20 | Measures 11–15, retained if no later page exists | Measures 16–20 | Lower |

The logical page changes every five selected-score measures. The physical row alternates by logical page parity. A row is recycled only after the cursor has moved to the other, already-rendered row.

## Assumptions and Scope Boundaries

- Implement this in the Blazor web client. The desktop OpenGL client has a separate `PianoMapperWindow`/`StaffRenderer` path and is not included in this plan.
- Apply the paired behavior to both **Play / restart** and **Practice**. Otherwise the main user-playing flow would retain the single-window problem.
- Keep five measures per logical page; do not change score spacing or `GrandStaffLayout.VisibleMeasureCount`.
- Keep free play and piano-roll behavior single-canvas and unchanged.
- For a selected range of five or fewer measures, show one populated grand staff and omit a redundant empty second row.
- When there is no later look-ahead page, keep the immediately preceding page in the inactive row so the ending retains useful context. Never duplicate or clamp an out-of-range page into the second row.
- While automated playback or Practice is following the score, disable manual Previous/Next navigation so manual and automatic page ownership cannot fight each other.
- When playback is inactive, Previous/Next advances the active logical page by five measures and uses the same upper/lower alternation as playback.
- “Both visible” means both notation rows are present together in the notation panel without replacing one another. Narrow screens may require normal document scrolling; readability takes precedence over forcing both rows into a small physical viewport.

## Acceptance Criteria

- [x] Loading a selected score range longer than five measures shows the current five-measure page and the following page in two labeled grand-staff rows.
- [x] Loading a range of five or fewer measures shows one row with no duplicate or empty look-ahead row.
- [x] Play / restart begins at the selected range's first logical page regardless of prior manual browsing.
- [x] The playback cursor animates when waveform and spectrum panels are hidden.
- [x] At an exact five-measure boundary, the cursor is owned by the new row only; it never appears on both rows or disappears between them.
- [x] After the cursor moves upper → lower, the upper row is replaced with the next page; after lower → upper, the lower row is replaced.
- [x] Window state is derived from absolute cursor position, so a delayed/background refresh can jump directly to the correct row pair rather than replaying missed transitions.
- [x] Practice uses the same alternating rows, and verdict colors plus held-note indicators appear only in their owning window.
- [x] A partial final page remains visible and stable at completion, with the previous page retained in the inactive row.
- [x] Score selection changes, unload, timing changes, restart, abort, completion, and piano-roll toggling leave no stale secondary scene or cursor.
- [x] Manual Previous/Next and `[`/`]` follow the paired-page model while idle and cannot interfere while playback or Practice is active.
- [x] Existing free-play grand staff, piano roll, audio analysis panels, score scheduling, and MusicXML parsing behavior do not change.
- [x] Both rows remain readable at wide, tablet, and minimum supported narrow widths; the page retains its current full-width score layout.

## Architecture Decisions

### Stable physical rows, rotating logical pages

Introduce a small, pure paired-window calculation in the web rendering layer. Given the selected score's measure count and an active logical page index (or absolute cursor beats), it returns:

- the clamped active page index;
- the upper row's first measure;
- the optional lower row's first measure;
- which physical row is active.

Derive the state directly from the absolute page rather than mutating “next page” counters. This handles exact boundaries, restarts, coarse polling, and tab throttling with one deterministic rule.

Recommended type: `ScoreGrandStaffWindowPair`, with a nested immutable state value, in `PianoMapper.Web/Rendering/ScoreGrandStaffWindowPair.cs`.

### Two scenes and two caches, one score model

Keep `selectedScore` as the single score source. Build one `GrandStaffScene` per present physical row using separate `GrandStaffSceneCache` instances. Reuse the current builder and five-measure layout without adding a combined scene type or changing core rendering geometry.

### One visualization component, two score canvases

Extend `PianoCanvas` with an optional secondary score scene/cursor and row labels. Keep the panel header, legend, waveform, spectrum, and help content single-instance. Add a score-only JavaScript initializer for the second canvas so it does not bind or observe the shared analyser canvases.

### Cursor handoff remains audio-clock driven

Give both physical score canvases the same audio-clock anchor but their own `FirstVisibleMeasure`. Each canvas maps the same absolute beat and draws only if that beat belongs to its half-open interval:

`[window start beat, window end beat)`

The upcoming row is already initialized before the boundary, so JavaScript can place the cursor there on the exact animation frame. The existing 250 ms C# completion poll only needs to notice the new logical page and recycle the now-inactive row before the following boundary.

### No audio or import changes

Do not reschedule playback at a row transition. Do not modify `BrowserScorePlayback`, `ScorePlayback`, `Score`, `MusicXmlScoreReader`, or the five-measure layout contract unless implementation evidence reveals a defect outside this plan.

## Dependency Order

```text
Paired-window calculation and boundary contract
        |
        +--> Two-canvas component lifecycle and responsive layout
        |         |
        |         +--> Automated playback window rotation
        |                    |
        |                    +--> Practice and view-state integration
        |
        +--> Cursor animation and exact-boundary ownership
                              |
                              +--> End-to-end validation and docs
```

## Phased Task Checklist

### Phase 1: Deterministic paging foundation

#### Task 1: Add the paired grand-staff window calculation

**Description:** Add a pure internal helper that converts measure count plus active page/cursor position into the upper/lower physical row assignments. Use `GrandStaffLayout.VisibleMeasureCount`; do not repeat a new magic value of five. The helper must calculate from absolute state so it can resynchronize after skipped polls.

**Test-first cases:**

- initial upper page plus lower look-ahead page;
- exact boundary selects the new page;
- upper → lower → upper alternation;
- direct jump across multiple pages;
- one-page score has no lower row;
- partial last page retains the previous physical row;
- previous/next requests clamp to valid logical pages;
- zero/negative cursor beats resolve to the first page.

**Acceptance criteria:**

- [x] Every valid measure index appears in at most one physical row state.
- [x] A look-ahead page, when one exists, occupies the inactive row.
- [x] No returned row start is outside the selected score.
- [x] The calculation has no audio, JavaScript, component, or mutable global dependency.

**Verification:**

- [x] `rtk test dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreGrandStaffWindowPairTests"`

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Web/Rendering/ScoreGrandStaffWindowPair.cs` (new)
- `PianoMapper.Tests/UnitTests/ScoreGrandStaffWindowPairTests.cs` (new)

**Estimated scope:** Small

#### Task 2: Make cursor ownership and animation safe for paired rows

**Description:** Establish one half-open visibility rule in both C# scene composition and JavaScript cursor drawing. Update the JavaScript animation scheduling so an active score cursor requests animation frames independently of waveform/spectrum visibility and stops cleanly when the cursor completes or is removed.

**Acceptance criteria:**

- [x] At a shared page boundary, the outgoing window rejects the cursor and the incoming window accepts it.
- [x] Practice cursor and held-note overlays obey the same ownership rule as automated playback.
- [x] An active score cursor animates with both analysis panels hidden.
- [x] Removing/finishing a cursor does not leave a perpetual animation loop.

**Verification:**

- [x] `rtk test dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] `rtk test node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Dependencies:** Task 1 defines the boundary behavior to match.

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Estimated scope:** Medium

### Checkpoint 1: Paging and cursor foundation

- [x] Focused C# and JavaScript tests pass.
- [x] Exact-boundary tests prove there is one cursor owner.
- [x] The window calculation proves delayed-jump recovery and final-page behavior.
- [x] No core measure-count, score, audio-scheduling, or MusicXML contract changed.

### Phase 2: Two persistent score rows

#### Task 3: Extend `PianoCanvas` with an optional second score canvas

**Description:** Render two score canvases inside the existing visualization panel only when a second score window exists. Add independent scene/cursor state and measure-range labels for each physical row, but keep the header, legend, analyser controls, analyser canvases, and help content single-instance. Give the secondary canvas a score-only JS initialization/disposal path.

**Acceptance criteria:**

- [x] Each physical canvas has independent scene, static-layer cache, cursor, resize, and disposal state in JavaScript.
- [x] Showing/hiding the optional row after score load, range change, unload, or view toggle does not use an invalid `ElementReference` or leak a `ResizeObserver`/animation frame.
- [x] The waveform and spectrum canvases are initialized and observed only once.
- [x] Each row exposes an accurate measure range in visible text and its accessible label.
- [x] Live grand staff and piano roll still render a single canvas.

**Verification:**

- [x] `rtk test node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`
- [x] Manual component lifecycle check: no console errors across load → unload → reload and staff → piano roll → staff.

**Dependencies:** Tasks 1–2

**Files likely touched:**

- `PianoMapper.Web/Components/PianoCanvas.razor`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Web/wwwroot/css/app.css`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Estimated scope:** Medium

#### Task 4: Wire paired rows into score loading, browsing, and automated playback

**Description:** Replace the page's single imported-score window state with the paired state. Own two score scenes, two cache instances, and two optional JS cursor states. Reset to page zero on load, selected-range change, and Play / restart. During automated playback, capture the absolute cursor beats in the existing completion loop; when the derived logical page changes, rebuild only the row whose first measure changed and push updated cursor state without rescheduling audio.

Manual Previous/Next should move one logical five-measure page while idle, update the current-row label, and be disabled during automated playback or Practice.

**Acceptance criteria:**

- [x] Initial load renders pages 0 and 1 when both exist.
- [x] Play / restart returns to logical page 0 and supplies the same anchor to both row cursors.
- [x] Crossing page 0 → 1 recycles only the upper row to page 2; crossing 1 → 2 recycles only the lower row to page 3.
- [x] The existing audio schedule starts once and is uninterrupted by visual row transitions.
- [x] Selected-range labels add `selectedFirstMeasure` to the range-relative row indexes.
- [x] Load, unload, range selection, timing changes, stop, restart, and completion clear or rebuild both scenes/cursors consistently.
- [x] A background/throttled poll derives the correct current pair directly from current cursor beats.

**Verification:**

- [x] `rtk test dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~PianoTests|FullyQualifiedName~ScoreGrandStaffWindowPairTests|FullyQualifiedName~GrandStaffSceneCacheTests|FullyQualifiedName~BrowserScorePlaybackTests"`
- [ ] Manual Play / restart check with a score longer than 15 measures confirms upper → lower → upper → lower behavior without an audible restart.

**Dependencies:** Tasks 1–3

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/PianoTests.cs`
- `PianoMapper.Web/Components/PianoCanvas.razor` only if parameter naming needs final alignment

**Estimated scope:** Medium

### Checkpoint 2: Automated playback slice

- [x] Two labeled rows are visible for a long imported score.
- [x] Cursor handoff is exact and visual row recycling happens only after handoff.
- [x] Restart and selected-range changes return to the first logical page.
- [x] Audio playback remains one continuous pre-scheduled run.
- [x] Free play, piano roll, and analysis controls still work.

### Phase 3: Practice, responsive usability, and handoff

#### Task 5: Apply the same window rotation to Practice and view transitions

**Description:** Before composing each Practice frame, derive the paired state from `practiceCoordinator.CursorBeats`. Compose both present rows from their own cache using the same verdict dictionary and performed-note snapshot; rely on the half-open visibility contract so only the owning row gets the cursor/held-note overlay. Restore both score rows correctly after piano-roll toggling and retain the ending pair after finish/abort.

**Acceptance criteria:**

- [x] Practice count-in begins with the initial current/look-ahead pair and no premature cursor.
- [x] Running Practice alternates rows at the same boundaries as score playback.
- [x] Verdict colors persist when an inactive physical row is recycled and rebuilt.
- [x] Held notes and cursor geometry do not appear in both rows at a boundary.
- [x] Abort, retry, finish, focus loss, and staff/piano-roll toggles leave consistent paired state.
- [x] Free-play rollover remains the existing single-staff behavior.

**Verification:**

- [x] `rtk test dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~BrowserPracticeCoordinatorTests|FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~PianoTests"`
- [x] Manual Practice check covers count-in, two boundary crossings, verdicts, held notes, abort, retry, and finish.

**Dependencies:** Task 4

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/PianoTests.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs` if an additional integration-level ownership case is needed

**Estimated scope:** Medium

#### Task 6: Tune responsive presentation and document the browser regression matrix

**Description:** Add score-mode-specific row spacing and heights instead of duplicating the current 25–34rem generic canvas height. Preserve the full-width layout and note readability. Update current documentation with the two-row look-ahead behavior and a reproducible manual regression sequence.

**Acceptance criteria:**

- [x] At wide desktop size, both score rows can be read together without an internal scroll container.
- [x] At tablet and narrow widths, clefs, signatures, notes, ledger lines, ties, barlines, row labels, and cursor remain legible and do not overlap.
- [x] Live grand-staff and piano-roll heights remain unchanged.
- [x] The browser test matrix covers initial preload, both handoff directions, final partial page, short selected range, manual navigation, restart, Practice, resize, and view toggling.
- [x] README feature text accurately describes the paired imported-score view without implying desktop parity.

**Verification:**

- [x] Inspect at representative widths: 1440 px, 1024 px, 768 px, and the supported 320 px minimum.
- [x] Run the full manual cycle with the local 16-measure `Humpty-Dumpty.mxl` when available; do not modify or commit that untracked user file.
- [x] Confirm the browser console has no JS interop, resize, or disposal errors.

**Dependencies:** Tasks 3–5

**Files likely touched:**

- `PianoMapper.Web/wwwroot/css/app.css`
- `PianoMapper.Web/Components/PianoCanvas.razor`
- `docs/browser-test-matrix.md`
- `README.md`

**Estimated scope:** Small

### Final Checkpoint

- [x] Focused paired-window, renderer, cursor, playback, and Practice tests pass.
- [x] `rtk test node --test PianoMapper.Tests/JavaScript/*.test.mjs`
- [x] `rtk test dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- [x] `rtk summary dotnet build PianoMapper.slnx --configuration Release`
- [ ] Manual long-score playback completes multiple upper/lower handoffs with continuous audio.
- [x] Manual short-score and partial-final-page cases match the documented behavior.
- [x] Manual responsive and accessibility checks pass with no console errors.
- [x] `rtk git diff --check`
- [x] Only files directly required by this feature are changed; untracked screenshots and MusicXML files remain untouched.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Both row cursors draw at the exact boundary because current X bounds are inclusive | High | Define and test half-open ownership in both C# and JS before wiring the second row. |
| The score cursor does not animate when analysers are hidden | High | Make score-cursor presence/completion an independent animation-loop condition and add a JS regression test. |
| The 250 ms completion poll notices a page transition late | Medium | Preload the other row and let both JS cursors use the audio clock; derive C# recycling state from absolute beats rather than incremental events. |
| A throttled/background tab skips multiple page transitions | Medium | Calculate the target pair directly from the current logical page and test a multi-page jump. |
| Two full-height canvases make the notation unusably tall | High | Add imported-score-only row sizing; validate wide and narrow widths without shrinking the live view. |
| Reusing one cache causes alternating rows to invalidate each other | Medium | Keep one `GrandStaffSceneCache` per physical row, as its existing ownership documentation recommends. |
| A short/final score is clamped into a duplicated row | Medium | Represent the secondary row as optional and never call the builder for an absent window. Retain the prior valid row at the ending when useful. |
| Practice verdict dictionaries cause excessive static rebuilds on both rows | Medium | Preserve the per-row cache and existing verdict-content equality; rebuild only when verdict content/window truly changes. |
| Conditional second-canvas lifecycle leaks JS state or observes analyser canvases twice | Medium | Use a score-only initializer, explicit per-canvas initialization tracking, and disposal tests. |
| Manual navigation conflicts with automatic following | Medium | Disable it while playback/Practice is active and drive both manual and automatic views through the same logical-page state. |
| Selected measure ranges display incorrect absolute labels | Low | Keep rendering indexes relative to `selectedScore` and add `selectedFirstMeasure` only when formatting user-visible labels. |

## Files Expected to Change

| File | Purpose |
|---|---|
| `PianoMapper.Web/Rendering/ScoreGrandStaffWindowPair.cs` | Pure page-to-physical-row calculation. |
| `PianoMapper.Web/Pages/Piano.razor` | Own paired state/scenes/caches/cursors and coordinate playback, Practice, navigation, and resets. |
| `PianoMapper.Web/Components/PianoCanvas.razor` | Render and manage the optional second score canvas and row labels. |
| `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs` | Enforce half-open cursor/indicator window ownership. |
| `PianoMapper.Web/wwwroot/js/canvas.js` | Score-only canvas initialization, cursor animation condition, and half-open cursor ownership. |
| `PianoMapper.Web/wwwroot/css/app.css` | Paired score-row layout and responsive heights. |
| `PianoMapper.Tests/UnitTests/ScoreGrandStaffWindowPairTests.cs` | Deterministic paging tests. |
| `PianoMapper.Tests/UnitTests/PianoTests.cs` | Page-level policy/navigation reset tests where expressible without component infrastructure. |
| `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs` | C# boundary/overlay ownership tests. |
| `PianoMapper.Tests/JavaScript/canvas.test.mjs` | Multi-canvas lifecycle, cursor ownership, and animation tests. |
| `docs/browser-test-matrix.md` | Manual long-score and responsive regression matrix. |
| `README.md` | User-visible web feature description. |

## Explicitly Unchanged Unless New Evidence Requires It

- `PianoMapper.Core/Rendering/GrandStaffLayout.cs`
- `PianoMapper.Core/Music/ScorePlayback.cs`
- `PianoMapper.Web/Playback/BrowserScorePlayback.cs`
- `PianoMapper.Web/Playback/ScoreMeasureRange.cs`
- MusicXML parsing and score model types
- Desktop `PianoMapperWindow` and `StaffRenderer`

## Confirmed Decisions

- The feature applies only to the Blazor web client; desktop parity is out of scope.
- Both automated playback and Practice use the paired grand-staff behavior.
- Selected scores of five measures or fewer show one populated staff.
- Narrow screens may use normal document scrolling to preserve notation readability.
- Manual Previous/Next navigation is disabled while automated playback or Practice is active.

## Implementation Handoff

The user approved this plan and its recommended defaults on 2026-08-02. Implementation may proceed in the documented dependency order.
