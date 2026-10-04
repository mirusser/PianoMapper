# Plan: Live grand-staff cursor compositing

**Date:** 2026-10-04
**Goal:** Make the red cursor in the audio-enabled, no-score grand staff move smoothly in Firefox and other supported browsers without rebuilding the staff on every cursor tick.

## Context

The imported-score cursor is already a DOM overlay driven by the Web Audio clock. The live grand staff takes a separate path: `TickVisualizationAsync` rebuilds `GrandStaffSceneBuilder.Build` every 16 ms, including a `GrandStaffLineKind.Cursor`, and calls `StateHasChanged` unconditionally. This serializes a full scene and repaints Canvas2D merely to move the red line.

Firefox can composite a promoted element whose `transform` changes independently of Canvas2D paint work. The existing per-canvas cursor overlay uses that browser-neutral mechanism; it should be reused rather than adding a Firefox-specific path.

## Request and acceptance criteria

When audio is enabled without an imported score or generated exercise, the live grand-staff red cursor must retain its current audio-clock position, color, dimensions, and five-measure page handoff while moving independently of Blazor scene rendering. Live held notes and live-page changes must still redraw accurately.

## Plan

### Task 1: Separate live cursor data from its staff scene

Project the cursor line out of the live grand-staff scene and expose a small cursor-state record containing the audio-clock timing, visible measure window, and post-layout vertical span. Preserve the existing scene reference when its remaining visual data is structurally unchanged.

**Acceptance criteria:**

- [x] A stable live staff no longer changes its scene identity solely because the cursor position advances.
- [x] Held-note duration growth and visible-window changes still produce a new scene.
- [x] The scene delivered to Canvas2D has no duplicate live red line.

**Verification:**

- [x] Add focused unit coverage for the live-scene/cursor separation or reference-preservation boundary.

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Web/Rendering/LiveGrandStaffCursorState.cs`
- `PianoMapper.Tests/UnitTests/PianoTests.cs`

### Task 2: Animate the live cursor in the existing DOM overlay

Extend the canvas module and component cursor handoff with a live-cursor mode. Map the Web Audio clock to the live five-measure grid and update only the overlay's `translate3d` transform between C# scene updates.

**Acceptance criteria:**

- [x] An unchanged live-cursor frame does not call `drawImage` or repaint Canvas2D.
- [x] The line remains in the correct CSS-pixel position and hides outside its current measure window.
- [x] Imported-score, practice, and piano-roll paths retain their existing behavior.

**Verification:**

- [x] Add deterministic JavaScript tests for live-cursor coordinates, hiding, and no-repaint animation frames.
- [x] Run all JavaScript canvas tests.

**Files likely touched:**

- `PianoMapper.Web/Components/PianoCanvas.razor`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

### Task 3: Gate the free-play visualization render

Have the free-play ticker request a Blazor render only when the live scene or cursor configuration changes, not merely because audio time advanced.

**Acceptance criteria:**

- [x] A no-score, no-held-note live staff does not call `StateHasChanged` each 16 ms.
- [x] Input-driven visual changes still render immediately.
- [x] Page handoff updates the staff and cursor configuration.

**Verification:**

- [x] Run focused `PianoTests` and the fast .NET suite.
- [x] Run `git diff --check`.
- [ ] In Firefox Performance tools, confirm cursor-only frames show a composited transform without a main-canvas repaint.

**Dependencies:** Tasks 1-2.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Active notes grow while held | Keep their scene rebuild path; only suppress identical no-note/no-window scenes. |
| Cursor drifts from the live staff grid | Derive timing and vertical bounds from the same scene/current audio clock used by the existing live renderer. |
| Firefox layer promotion costs memory | Reuse exactly one transform-hinted overlay per visible canvas and hide it when inactive. |
| Score cursor behavior regresses | Keep the score and live cursor states distinct and cover both paths in JavaScript tests. |

## Checkpoint

- [x] JavaScript tests pass.
- [x] Focused and fast .NET tests pass.
- [ ] Firefox visual/profile check confirms no Canvas2D repaint for cursor-only frames.
