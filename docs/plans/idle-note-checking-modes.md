# Plan: Idle Note-Checking Modes

**Date:** 2026-09-18
**Goal:** Add selectable pitch-only, hold-aware, and rhythm-aware idle note checking without changing count-in Practice behavior.

## Context

Idle score checking currently uses `NoteReadingSession`: it groups notes by onset and advances when the current pitches have been pressed. Written duration and onset timing are available in `ScoreEvent`, but the session does not evaluate them. Timed count-in Practice uses a separate grader and remains outside this change.

## Request

Provide three idle checking modes plus an off state:

1. Preserve the existing self-paced pitch/order behavior.
2. Add self-paced pitch/order checking that validates key holds against written duration.
3. Add tempo-based onset rhythm checking to the hold-aware behavior.

Hold-aware modes must keep long notes pending until release, classify early/late releases, and handle cross-staff overlaps and ties. Rhythm mode anchors its tempo timeline on the first accepted score onset and classifies later correct pitches as early, on time, or late.

## Plan

### Phase 1: Core behavior and tests

- [x] Add an explicit note-reading mode contract and deterministic event-time APIs.
- [x] Preserve pitch-only behavior while tracking active hold attempts in the two new modes.
- [x] Validate release duration and tempo-relative onset timing with typed verdicts.
- [x] Add unit tests for cross-staff long-note overlap, short/correct/long holds, ties, rhythm timing, release-all, and legacy compatibility.

### Checkpoint: Core

- [x] Focused `NoteReadingSessionTests` pass.
- [x] Existing pitch-only tests pass unchanged.

### Phase 2: Browser integration

- [x] Replace the idle-checking checkbox with an Off/three-mode selector.
- [x] Pass audio-clock event times and the selected timing tolerance into the core session.
- [x] Surface onset and release verdicts in status text and keep held score notes highlighted.
- [x] Update the practice help text and README feature description to distinguish idle modes from count-in Practice.

### Checkpoint: Complete

- [x] Browser project builds.
- [x] Full .NET and JavaScript test suites pass.
- [x] No behavior changes occur in count-in Practice or generated pitch-only sight-reading exercises.

## Risks and open questions

- A self-paced hold mode uses written duration converted through the score tempo while leaving note onsets user-paced; this is intentional so final notes can also be validated.
- Rhythm mode accepts the expected pitch in score order even when early or late, records the timing verdict, and advances; it does not auto-skip missed prompts.
- Sustain-pedal CC64 remains outside scope because the current MIDI input contract discards controller messages.
