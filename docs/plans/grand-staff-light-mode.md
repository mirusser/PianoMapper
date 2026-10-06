# Plan: Grand-staff light mode

**Date:** 2026-10-06
**Goal:** Add a dark-by-default light presentation mode for normal, score, and note-reading grand staffs.

## Context

`PianoCanvas` owns the header shared by normal live grand staff, imported scores, and note-reading exercises. Its JavaScript canvas renderer currently uses a dark-only grand-staff palette.

## Request

Add a checkbox immediately before **Reset highlights** where that action is present, and expose it in normal grand-staff mode too. It should switch the displayed staff to a conventional white-paper, black-notation appearance, while preserving readable, differentiated state feedback. Dark mode remains the default.

## Plan

### Phase 1: Render palette and control

- [x] Add local light-mode state and a checkbox in the shared score toolbar.
- [x] Pass the state to both rendered score canvases and invalidate their cached static layers when it changes.
- [x] Define dark and light grand-staff palettes; make light static notation black and use darker accessible feedback colors.

### Phase 2: Presentation and verification

- [x] Give light canvases a white surface and keep the legend swatches aligned with the active renderer palette.
- [x] Add a focused JavaScript canvas test for light notation and active-note colors.
- [x] Run the focused JavaScript test suite and the Web project build.

## Checkpoint

- [x] The checkbox appears before Reset highlights in score and exercise views, and in normal grand-staff mode.
- [x] A new component instance opens in dark mode.
- [x] Both score rows repaint in light mode with black notation and visible feedback states.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Cached score layers retain the old theme | Set the score layer dirty when the mode changes. |
| Pale feedback colors lose contrast on white | Use a dedicated darker light-mode palette and test its active-note color. |
