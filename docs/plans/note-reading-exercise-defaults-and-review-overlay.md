# Plan: Note-reading exercise defaults and review overlay

**Date:** 2026-10-04  
**Goal:** Make melodic motion the initial note-reading pattern, keep the 88-key piano directly below the grand staff, and give completed auto-next exercises a clear staff-level countdown whose colors are accurately explained.

## Context

- `SightReadingExerciseCoordinator` owns durable exercise settings and currently initializes `Motion` to `SightReadingMotion.Random`.
- The ten-second automatic-next countdown is already correctly tracked by `SightReadingExerciseCoordinator`, refreshed by `Piano.razor`, and exposed through `SightReadingExercisePresentation`. It is currently rendered as text in `SightReadingExercisePanel` rather than on the score.
- `PianoCanvas` renders the grand staff, then practice controls and analysis panels, and only later renders the optional `FullPianoKeyboard`.
- The score canvas uses `verdictColors` for noteheads and separate colored review halos. Its current legend instead describes generic live-note states (pressed/released), so it does not describe the colors visible on a completed exercise score.

## Request and acceptance criteria

- The initial Pattern selection for a note-reading exercise is Melodic; unavailable patterns retain their existing fallback behavior.
- When shown, the 88-key piano sits directly below the grand-staff view.
- With automatic-next enabled, completing an eligible exercise shows a visible 10-second progress/countdown overlay on the grand staff, updates as time elapses, and preserves the existing “Stay here” cancellation action.
- The grand-staff legend accurately distinguishes live note states from score verdict colors and review marks, with labels matching the rendered colors.

## Plan

### Task 1: Set and cover the melodic default

**Description:** Change the exercise coordinator’s initial motion from Random to Melodic and update its documentation. Keep `EffectiveMotion`’s existing Random fallback for modes/presets where motion cannot apply.

**Acceptance criteria:**

- [x] A new coordinator reports `SightReadingMotion.Melodic` initially.
- [x] Supported, newly generated exercises use melodic motion without a user selection.
- [x] Unsupported patterns still use the existing fallback.

**Verification:**

- [x] Focused coordinator/composer tests pass.

**Dependencies:** None.  
**Files likely touched:** `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`, coordinator/composer tests.

### Task 2: Place the 88-key piano beneath the staff

**Description:** Reorder the conditional `FullPianoKeyboard` in `PianoCanvas` so it is immediately after the visualization panel that contains the grand staff, before practice and analysis content. Preserve its existing toggle and pointer-note callbacks.

**Acceptance criteria:**

- [ ] Toggling “Show 88-key piano” presents the keyboard below the grand staff.
- [ ] The keyboard still supports active/next-key states and pointer input.

**Verification:**

- [x] Component markup/build succeeds.
- [ ] Manual browser check confirms staff → piano ordering at desktop and narrow widths.

**Dependencies:** None.  
**Files likely touched:** `PianoMapper.Web/Components/PianoCanvas.razor` (and CSS only if the resulting spacing needs a targeted adjustment).

### Task 3: Surface automatic-next progress on the grand staff

**Description:** Pass the existing remaining-seconds presentation state to `PianoCanvas` and render an accessible countdown/progress overlay inside the grand-staff canvas stack only while it is active. Keep the existing coordinator/ticker timing behavior and retain cancellation in the exercise panel, removing the duplicate countdown sentence there.

**Acceptance criteria:**

- [ ] An eligible completed exercise displays an overlay beginning at 10 seconds on the grand staff.
- [ ] The display updates until automatic generation begins; disabling or cancelling the countdown removes it.
- [ ] No countdown appears during an active exercise or when auto-next is not armed.

**Verification:**

- [x] Existing `SightReadingExerciseAutoNextTests` remain green.
- [ ] Focused rendering/component checks and a manual completion flow confirm the overlay’s lifecycle.

**Dependencies:** Task 2 (both change `PianoCanvas` structure).  
**Files likely touched:** `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Web/Components/PianoCanvas.razor`, `PianoMapper.Web/Components/SightReadingExercisePanel.razor`, `PianoMapper.Web/wwwroot/css/app.css`.

### Task 4: Correct the grand-staff color legend

**Description:** Replace the score-view legend with a legend for the colors actually used by `canvas.js`: green correct, red wrong pitch, orange timing, yellow hold duration, gray missed, purple extra, and the separate review-halo meanings. Leave the current pressed/released legend intact for the live (non-score) visualization.

**Acceptance criteria:**

- [ ] Score/exercise grand-staff legend labels agree with `verdictColors` and review-mark styles.
- [ ] The live visualization continues to explain pressed and released notes.
- [ ] The legend does not imply a review halo is the same thing as a live verdict.

**Verification:**

- [x] JavaScript canvas contract tests pass.
- [ ] Manual review of an active score and a completed exercise confirms labels and colors agree.

**Dependencies:** Task 3 (both modify score-view presentation).  
**Files likely touched:** `PianoMapper.Web/Components/PianoCanvas.razor`, `PianoMapper.Web/wwwroot/css/app.css`, potentially a focused component/rendering test.

## Checkpoint: Complete

- [x] Run the targeted .NET and JavaScript tests, then the appropriate project test suite.
- [x] Build the solution without warnings introduced by these changes.
- [ ] Verify the complete exercise flow manually: default Melodic → generate → complete with auto-next → staff countdown → new exercise, including the 88-key piano and color legend.

## Risks and open questions

- The phrase “under the grand staff” is interpreted as immediately following the staff visualization and before practice/analysis panels. This is the least invasive layout change and keeps the keyboard near the notation it supports.
- Score verdict colors and review marks intentionally communicate different things. The corrected legend must explicitly separate them rather than merge their meanings.
