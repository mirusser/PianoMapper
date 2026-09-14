# Plan: Edit fingering hand

**Date:** 2026-09-14
**Goal:** Let users assign a selected score note to the right or left hand while editing fingerings.

## Context

The score model stores a note's hand assignment in `ScoreNote.Staff`: treble represents right hand and bass represents left hand for fingering labels. Grand-staff rendering already chooses the visual notation staff from pitch, so changing the hand does not move the notehead. Saved-score JSON already persists `Staff`, so no API, schema, or document-version change is required.

## Request

- Add right- and left-hand choices to the fingering editor.
- Update only the selected score note.
- Refresh the displayed `R`/`L` fingering label immediately.
- Persist the chosen hand through the existing **Save changes** flow.

## Plan

### Phase 1: Immutable score edit

- [x] Add a tested score-editor operation that changes the selected note's staff/hand while preserving all other notes and fingering data.
- [x] Reject invalid note ordinals and invalid staff values consistently with fingering-number edits.

### Checkpoint: Core behavior

- [x] Focused score-editor tests pass.

### Phase 2: Browser editor

- [x] Add **Right hand** and **Left hand** controls for the selected note.
- [x] Mark the current hand as selected and disable both controls until a note is selected.
- [x] Rebuild the current score and canvas through the same path used by fingering-number changes.

### Phase 3: Persistence and verification

- [x] Document the hand editor and browser regression case.
- [x] Run the complete .NET and JavaScript test suites, formatting checks, and Release build.
- [x] Verify hand selection and database save/reload in the hosted browser.

## Risks and open questions

- `Staff` is the existing stored hand signal for fingering labels; adding a separate hand field would create an unnecessary document migration and conflicting sources of truth.
- The hand edit must not change pitch-derived notehead placement on the grand staff.
