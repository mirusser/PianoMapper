# Plan: Refine the browser controls and learning copy

**Date:** 2026-07-19
**Goal:** Make the browser controls easier to scan and replace generic music-theory copy with concise, app-specific explanations and reputable follow-up links.

## Context

The browser dashboard is implemented in `PianoMapper.Web/Pages/Piano.razor` and styled globally in `PianoMapper.Web/wwwroot/css/app.css`. The timing card currently mixes four fields, a grid-stretched metronome button, feedback, operational notes, and a term glossary. `PianoMapper.Web/Components/PianoCanvas.razor` contains the larger notation glossary. MusicTheory.net provides focused lessons on staff notation, note duration, measures, and time signatures; PianoMapper should link to those lessons without copying their text.

## Request and assumptions

- Rename `Input timing` to `Show input latency` without changing its diagnostic behavior.
- Reduce visual crowding while preserving every existing control and keyboard shortcut.
- Give the metronome button its natural content width instead of a full grid cell.
- Keep help collapsed by default, rewrite it around what users see and control in PianoMapper, and link to relevant MusicTheory.net lessons for illustrated follow-up material.
- Preserve runtime behavior, public contracts, and the dark visual theme.

## Plan

### Phase 1: Clarify and reorganize the control dashboard

- [ ] Rename the latency diagnostic button.
- [ ] Separate timing fields from the metronome action and live feedback.
- [ ] Adjust dashboard proportions and responsive behavior so control groups have clearer spacing.

**Acceptance criteria:**

- [ ] Every existing control remains present and wired to the same handler.
- [ ] The metronome button uses its content width on desktop and narrow layouts.
- [ ] The dashboard collapses to one column without an implicit extra grid column.

### Phase 2: Improve learning content

- [ ] Rewrite timing definitions with concrete examples and PianoMapper-specific behavior.
- [ ] Rewrite notation and audio-view explanations to describe what users can identify on screen.
- [ ] Add clearly labelled links to relevant MusicTheory.net lessons, opening in a new tab.
- [ ] Simplify nested glossary styling so expanded help reads as a definition list rather than another dashboard of cards.

**Acceptance criteria:**

- [ ] Explanations distinguish note value, beat, measure, tempo, and timing tolerance.
- [ ] Notation explanations cover clef reference points, ledger lines, duration symbols, and the app's live-note trails.
- [ ] External links point to the current MusicTheory.net lessons index and numbered lesson pages.

### Checkpoint: Verify the browser UI

- [ ] Build the web project with no errors or warnings introduced by the changes.
- [ ] Run the relevant repository test suite.
- [ ] Inspect desktop and narrow viewport renders for overflow, stretched controls, and readable help content.

## Risks and mitigations

- **Theory wording could conflict with app semantics:** describe PianoMapper's selected beat unit explicitly, especially for time signatures.
- **Global glossary CSS affects practice help:** preserve its semantic markup and verify that the simpler styling remains readable there.
- **External lesson content can change:** link to canonical MusicTheory.net URLs and keep all essential explanations inside PianoMapper.

## Open questions

None. The requested changes are limited to presentation and explanatory copy.
