# Plan: Allow PC Piano Without MIDI

**Date:** 2026-09-17
**Goal:** Let browser audio initialize with PC Piano when a saved FP-10 preference cannot be satisfied because no MIDI output is connected.

## Context

The sound-source controls remain disabled until Web Audio initializes. `audio.js` restores the persisted source during initialization and currently rejects a saved FP-10 source when its MIDI output is absent, preventing the user from reaching the enabled PC sound controls.

## Request

Audio initialization must remain usable without MIDI. A disconnected saved FP-10 preference should fall back to PC Piano, while explicitly selecting FP-10 without its MIDI output must continue to fail.

## Plan

### Phase 1: Reproduce the lockout
- [ ] Add a JavaScript regression test for initialization with a saved FP-10 source and no MIDI output.
- [ ] Verify the test also preserves the validation on an explicit FP-10 selection.

### Phase 2: Apply the fallback
- [ ] Reconcile an unavailable saved FP-10 source to the default PC Piano source during audio initialization.
- [ ] Persist the fallback so later status reads and reloads agree with the active source.

### Checkpoint: Verification
- [ ] Run the focused JavaScript audio test.
- [ ] Run the complete JavaScript test suite.
- [ ] Build the solution in Release configuration.

## Risks and open questions

- A disconnected FP-10 preference will be replaced with PC Piano until the user reconnects MIDI and selects FP-10 again; this matches the available controls and avoids an unusable saved state.
