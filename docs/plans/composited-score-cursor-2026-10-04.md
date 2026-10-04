# Plan: Composited score cursor

**Date:** 2026-10-04
**Goal:** Make the grand-staff playback cursor smooth by decoupling its per-frame movement from Canvas2D repainting and Blazor rendering.

## Context

The score cursor position is already derived from the Web Audio clock in `canvas.js`. Recent work removed 16 ms Blazor renders when the visible score state is unchanged. However, every `requestAnimationFrame` still redraws the main canvas to move the two-pixel red line: it blits the cached static staff layer and evaluates playback highlights.

Browser guidance recommends `requestAnimationFrame` plus separate/offscreen layers for repeated animation work. Blazor guidance recommends avoiding high-frequency rendering and JS interop when client-side state can update independently.

## Request and acceptance criteria

The red vertical line should remain smooth during imported-score playback, practice, and timed sight-reading, without changing its notation-aligned position, dimensions, color, page handoff, active-note highlighting, or score-note selection behavior.

## Plan

### Task 1: Add a cursor-only overlay layer

Create one non-interactive cursor element per score canvas and hand its element reference to the existing JavaScript canvas module. Style it as a transformable two-pixel red line above the canvas without affecting hit-testing or layout.

**Acceptance criteria:**

- [x] The overlay aligns to the canvas's CSS pixels and is hidden whenever the cursor is not visible.
- [x] It cannot intercept mouse or touch input for score-note editing.
- [ ] Primary and secondary score rows retain their existing responsive sizing and scrolling behavior.

**Verification:**

- [x] Extend JavaScript canvas tests for cursor visibility and coordinates.
- [ ] Manually check the two-row score layout at desktop and narrow widths.

**Dependencies:** None.

**Files likely touched:**

- `PianoMapper.Web/Components/PianoCanvas.razor`
- `PianoMapper.Web/wwwroot/css/app.css`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

### Task 2: Split cursor animation from canvas painting

Use the animation frame to update only the cursor overlay transform while playback is continuous. Keep the Canvas2D repaint path for actual scene, selection, overlay, resize, analysis-panel, and active-note-highlight changes.

**Acceptance criteria:**

- [x] Moving the cursor alone does not call `drawImage`, redraw the static score layer, or draw a Canvas2D cursor line per frame.
- [x] Score playback highlights repaint only when their active note set changes.
- [ ] Canvas analysis views continue to update every animation frame while visible.

**Verification:**

- [x] Add deterministic request-animation-frame tests that distinguish cursor-only frames from repainting frames.
- [x] Run all JavaScript canvas tests.

**Dependencies:** Task 1.

**Files likely touched:**

- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

### Task 3: Verify the end-to-end render boundary

Confirm that the existing change-aware .NET tick gate only delivers state changes (page, cursor lifecycle, highlights, held notes, verdicts, and count-in) while JavaScript owns in-between motion.

**Acceptance criteria:**

- [ ] Imported-score playback, practice, and timed sight-reading retain their current state transitions.
- [ ] The cursor disappears at completion and shows only on the active page.
- [x] No public API, score model, or timing contract changes.

**Verification:**

- [x] Run focused `PianoTests` and the fast .NET suite.
- [x] Run `git diff --check`.
- [ ] Manually profile a dense score in browser DevTools: cursor-only frames should show no main-canvas paint work beyond the composited transform.

**Dependencies:** Tasks 1-2.

**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/PianoTests.cs`

## Checkpoint: Before completion

- [x] JavaScript tests pass.
- [x] Fast .NET tests pass.
- [ ] Browser DevTools confirms cursor-only frames do not repaint Canvas2D content.
- [ ] Visual check confirms notation mapping and two-row handoff are unchanged.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| CSS-pixel overlay drifts from a high-DPI canvas | Position it from the canvas's CSS width and height, the same coordinate space used by `mapX` and `mapY`. |
| Overlay captures score-note clicks | Use `pointer-events: none`. |
| Highlight changes no longer appear | Treat a changed active-note set as a canvas repaint trigger and cover it with deterministic tests. |
| Responsive score rows shift the overlay | Keep the overlay inside a per-canvas relative container and test both rows. |

## Sources reviewed

- [MDN Canvas optimization](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API/Tutorial/Optimizing_canvas)
- [MDN requestAnimationFrame](https://developer.mozilla.org/en-US/docs/Web/API/Window/requestAnimationFrame)
- [Blazor rendering performance guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0)
- [Blazor JS interop performance guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/javascript-interoperability?view=aspnetcore-10.0)
- [abcjs timing-callback cursor history](https://github.com/paulrosen/abcjs/issues/271)
