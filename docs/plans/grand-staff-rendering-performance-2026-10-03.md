# Plan: Grand-staff rendering performance

**Date:** 2026-10-03
**Goal:** Remove avoidable full-score work from active grand-staff rendering while preserving the rendered notation and JS interop contract.

## Context

The grand staff has a cached static notation layer, but timed practice still produces and sends a new complete scene every 16 ms. Score-position calculations also repeatedly derive measure starts and notation-spacing anchors that are invariant for one visible window.

## Request

Implement the concrete rendering optimizations identified in the rendering audit. Preserve visual order, notation geometry, playback behavior, and the existing scene JSON contract.

## Plan

### Phase 1: Separate static score notation from dynamic overlays

- [x] Add an internal score-overlay DTO and builder that contains only the moving cursor and held-note ink.
- [x] Keep a reference-stable static `GrandStaffScene` in `GrandStaffSceneCache`; have `Piano.razor` supply the overlay separately.
- [x] Add JavaScript overlay update/drawing support without marking the cached score layer dirty.
- [x] Verify static scene references remain stable while overlay positions change, and overlay geometry matches current composition behavior.

### Phase 2: Reuse score-layout calculations

- [x] Introduce a per-build score timing/measure-layout context with prefix measure starts, key signatures, and notation-spacing anchors calculated once per visible measure.
- [x] Use it for note/rest X positions and dynamic cursor mapping, preserving current results for pickups, key changes, and ties.
- [x] Verify existing grand-staff geometry tests, plus focused cache/timing tests, pass unchanged.

### Phase 3: Reduce canvas frame overhead

- [x] Cache frame-derived staff spacing and selection resolution when the scene, height, or selection changes.
- [x] Batch foreground ledger strokes while preserving their post-highlight draw order.
- [x] Avoid the duplicate playback-clock lookup at the end of each frame.
- [x] Verify JavaScript contract tests and the browser-facing build pass.

## Checkpoints

- [x] Focused grand-staff cache, builder, and layout tests pass.
- [x] JavaScript canvas tests pass.
- [x] Release build succeeds and `git diff --check` reports no whitespace errors.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Overlay order changes engraving appearance | Keep held-note and ledger ink in their existing foreground order; use the existing JS cursor ordering. |
| Cached timing changes pickup/key-change placement | Test pickup, key-change, tie, and dense-spacing fixtures against existing expected scene geometry. |
| JS optimization stales after resize/selection | Invalidate only on scene, height, width, or selected address changes. |

## Explicitly deferred

Dirty-rectangle or multi-canvas compositing, glyph-metric caches, and review-halo collision rewrites are intentionally deferred: they require browser profiling and larger visual-risk changes than this focused pass.
