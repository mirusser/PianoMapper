# Plan: Regenerate All Fingering Numbers

**Date:** 2026-09-14
**Goal:** Add a deterministic option that recomputes every piano fingering number while preserving each note's current hand assignment.

## Context

PianoMapper stores fingering on immutable `ScoreNote` records and already refreshes edited scores through the existing fingering-edit flow. Saved scores persist the current score document through the existing **Save changes** action.

## Request

Add one operation that replaces every existing fingering number and fills every unnumbered note. The operation must preserve right/left-hand assignments. Filling only missing numbers and automatic hand reassignment are outside this change.

## Plan

### Phase 1: Fingering optimizer

- [x] Add a score-level, cost-based optimizer in `PianoMapper.Core`.
- [x] Optimize each hand independently across the whole chronological note sequence.
- [x] Treat simultaneous pitches as chords, assign distinct fingers within playable chords, and reject same-hand chords wider than five distinct keys.
- [x] Return a new immutable score with every finger number replaced.

### Checkpoint: Core behavior

- [x] Verify conventional ascending right- and left-hand scale fingerings.
- [x] Verify conventional triad fingering and hand preservation.
- [x] Verify existing numbers do not constrain or survive generation.

### Phase 2: Browser action

- [x] Add a clearly destructive **Regenerate all fingerings** action to the notation controls.
- [x] Confirm before replacing numbers, refresh the open score, and report failures without modifying it.
- [x] Keep database writes explicit through the existing **Save changes** action.

### Checkpoint: Complete

- [x] Focused and full .NET tests pass.
- [x] JavaScript tests and Release build pass.
- [x] Documentation describes generation and persistence behavior.

## Risks and open questions

- Fingering is subjective; this first version optimizes ergonomic movement and offers one deterministic suggestion.
- Hand redistribution, finger substitution on a held key, and performer-specific hand-size settings remain outside scope.
- A same-hand chord with more than five distinct keys cannot be fingered without redistribution and will produce a readable error.
