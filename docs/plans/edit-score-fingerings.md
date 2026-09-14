# Plan: Edit Fingerings on the Grand Staff

**Date:** 2026-09-13  
**Goal:** Let a user select a note in an imported-score grand staff, assign or remove its piano fingering, see the change immediately, and persist it through the existing saved-score workflow.

## Context

- Imported fingerings already live on the immutable `ScoreNote` record as an optional `ScoreFingering` containing a number and optional MusicXML placement.
- `GrandStaffSceneBuilder` includes the fingering label when `showFingerings` is enabled, and `canvas.js` renders it in the grand-staff annotation lane.
- Saved score document version 1 already serializes and deserializes fingering number and placement. The existing **Save score** / **Save changes** flow sends the current `Score`, so this feature does not require an API, database, or document-version migration.
- The browser displays two five-measure score canvases. A selected note therefore needs an explicit address; pitch and onset are not sufficient because chord members and repeated/unison notes can otherwise be ambiguous.
- `Piano.razor` keeps an original `importedScore`, a timing-adjusted `loadedScore`, and a rebased `selectedScore` measure range. Fingering edits must update the original score and then rebuild the derived score, or a later tempo/meter change can discard the edit.

## Request and acceptance criteria

The proposed first version edits only fingering numbers. It does not edit pitch, rhythm, score staff/hand, or MusicXML placement, and it does not add MusicXML export.

- [x] When an imported or saved score is shown as a grand staff, the user can enter a clearly labeled fingering edit mode.
- [x] Edit mode keeps fingerings visible and lets the user select an exact note in either visible score row, including an individual chord member.
- [x] The user can set finger 1 through 5 or remove the selected note's fingering; the grand staff updates immediately.
- [x] Changing a number preserves an existing `ScoreFingering.Placement`; adding a new fingering uses no placement override.
- [x] Edits survive score-page navigation, measure-range changes, and subsequent tempo or meter changes.
- [x] **Save score** and **Save changes** persist edited fingerings, and reloading the saved score reproduces them.
- [x] Playback and Practice cannot accidentally edit notes; entering edit mode stops active playback/practice, and starting those flows exits edit mode.
- [x] A keyboard-accessible note selector is available alongside canvas clicking, rather than making a pointer-only canvas the sole way to select a note.

## Architecture decisions

1. **Address rendered notes by measure and note index.** Add a small `ScoreNoteAddress` value containing the measure index and note index within the currently selected score. Attach it only to rendered score notes; live performed notes have no address. This distinguishes chord members without changing the persisted score contract.
2. **Mutate scores immutably by global note ordinal.** Add a focused score fingering operation that validates 1–5 (or `null` to remove), replaces one `ScoreNote`, one `ScoreMeasure`, and the containing `Score`, and leaves all other values unchanged. `Piano.razor` converts the range-relative address to a global note ordinal in `loadedScore`, applies it to `importedScore`, then rebuilds `loadedScore` with `ScoreTiming.Apply`. Note order is preserved by the current timing transformation; a regression test will make that dependency explicit.
3. **Keep hit-testing in the canvas module.** `canvas.js` already owns the scene and the CSS-pixel mapping. Export a deterministic hit-test function that returns the closest addressed notehead within a touch-friendly tolerance. `PianoCanvas.razor` invokes it from each canvas click and raises a typed Blazor callback.
4. **Render selection as a lightweight overlay.** Pass the selected address to the canvas render call and draw a selection ring after the cached score layer. Selection changes then do not invalidate `GrandStaffSceneCache` or rebuild notation geometry.
5. **Reuse explicit persistence.** Do not auto-save and do not alter the saved-score API or serializer. Editing changes the current `loadedScore`; the existing save buttons remain the persistence boundary.

## Plan

### Phase 1: Score edit foundation

#### Task 1: Add a validated immutable fingering edit operation

**Description:** Add a focused core operation that assigns or removes a fingering by global note ordinal. It will preserve every non-fingering field, retain placement when changing an existing number, and reject invalid finger numbers or note ordinals.

**Acceptance criteria:**

- [x] Assigning 1–5 returns a new score with only the addressed note changed.
- [x] Removing a fingering sets only that note's `Fingering` to `null`.
- [x] Existing placement is preserved when its number changes, and invalid inputs fail predictably.

**Verification:**

- [x] Focused unit tests cover assignment, replacement, removal, chord/unison disambiguation by ordinal, immutability, and invalid input.
- [x] Tests follow `Method_State_ExpectedResult` naming and assert observable record values.

**Dependencies:** None  
**Files likely touched:**

- `PianoMapper.Core/Music/ScoreFingeringEditor.cs` (new)
- `PianoMapper.Tests/UnitTests/ScoreFingeringEditorTests.cs` (new)

**Estimated scope:** Small

#### Task 2: Carry exact score-note addresses into rendered scenes

**Description:** Define the transient score-note address and have `GrandStaffSceneBuilder` enumerate measures and their note lists with indexes when creating `GrandStaffNote` values. Existing live-note construction remains unaddressed.

**Acceptance criteria:**

- [x] Every visible imported-score note has its correct range-relative measure and note index.
- [x] Two chord members at the same X coordinate have distinct addresses.
- [x] Live/performed notes have no editable address.

**Verification:**

- [x] `GrandStaffSceneBuilderTests` covers ordinary notes, chord members, and live-note exclusion.
- [x] Existing scene-builder and scene-cache tests remain green.

**Dependencies:** Task 1  
**Files likely touched:**

- `PianoMapper.Web/Rendering/ScoreNoteAddress.cs` (new)
- `PianoMapper.Web/Rendering/GrandStaffNote.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Medium

### Checkpoint: Model and render contract

- [x] A unit test can select one member of a chord and change only that note's fingering.
- [x] Scene JSON has a stable, nullable address for score notes without changing saved-score JSON.
- [x] `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreFingeringEditorTests|FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~GrandStaffSceneCacheTests"` passes.

### Phase 2: Selection and editing interaction

#### Task 3: Add canvas hit-testing and selection feedback

**Description:** Add an exported canvas hit-test that maps click coordinates to the closest addressed score note. Extend canvas render state with the selected address and draw a high-contrast ring around the selected notehead without modifying the cached notation layer.

**Acceptance criteria:**

- [x] Clicking near a notehead returns its address; clicking empty canvas returns no selection.
- [x] Vertically separated chord members select independently even when their X positions match.
- [x] Hit tolerance remains usable after responsive canvas resizing, and the selected ring follows the same mapping.

**Verification:**

- [x] JavaScript tests cover hits, misses, chord disambiguation, scaling, and selected-ring drawing.
- [x] `node --test PianoMapper.Tests/JavaScript/canvas.test.mjs` passes.

**Dependencies:** Task 2  
**Files likely touched:**

- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Estimated scope:** Small

#### Task 4: Bridge both canvases to typed Blazor selection

**Description:** Give `PianoCanvas` editability, selected-address, and selection-callback parameters. Handle clicks on the primary and secondary canvases through the JavaScript hit-test, update the cursor/visual affordance only in edit mode, and expose clear instructional/accessible text.

**Acceptance criteria:**

- [x] Either visible score row can report a typed `ScoreNoteAddress` to the page.
- [x] Score canvases do not select notes outside edit mode.
- [x] Edit mode is visually obvious and does not affect live-staff or piano-roll behavior.

**Verification:**

- [x] Component behavior is exercised through the narrowest existing test seam; coordinate behavior remains in JavaScript tests.
- [ ] Manual keyboard and pointer checks confirm normal piano input still works when edit controls are focused.

**Dependencies:** Task 3  
**Files likely touched:**

- `PianoMapper.Web/Components/PianoCanvas.razor`
- `PianoMapper.Web/wwwroot/css/app.css`

**Estimated scope:** Small

#### Task 5: Add the editor state and controls to the score page

**Description:** Add an **Edit fingering** mode near the existing Fingering visibility checkbox. In edit mode, show the selected note (absolute measure, beat, pitch, and L/R hand), a keyboard-accessible visible-note selector, buttons 1–5, and **Remove**. Resolve the range-relative address to a full-score ordinal, update `importedScore`, derive `loadedScore` again using the current timing, rebuild `selectedScore`, reset practice-derived state, and refresh both canvas scenes.

**Acceptance criteria:**

- [x] Entering edit mode stops playback/practice and forces fingering visibility on; starting playback/practice exits edit mode.
- [x] Assign/remove updates the exact selected note immediately and keeps it selected when still visible.
- [x] Loading/unloading another score, changing the selected measure range, leaving staff view, or navigating beyond the selected note clears stale selection safely.
- [x] A timing-change regression test proves that an edited note remains edited after `ScoreTiming.Apply` rebars the score.

**Verification:**

- [x] Unit tests cover range-address-to-ordinal mapping and preservation across timing derivation.
- [ ] Manual checks cover both physical score rows, later selected measure ranges, chords, page navigation, edit/remove, and interaction with Play/Practice.

**Dependencies:** Tasks 1 and 4  
**Files likely touched:**

- `PianoMapper.Web/Pages/Piano.razor`
- `PianoMapper.Tests/UnitTests/PianoTests.cs`
- A small Web helper and matching unit test only if extracting address-to-ordinal mapping is necessary for deterministic testing

**Estimated scope:** Medium

### Checkpoint: End-to-end editing

- [ ] Imported score: select a note, assign 1–5, change it, remove it, and see each update in both the canvas and editor controls.
- [ ] A chord member can be edited without changing its siblings.
- [ ] Edits remain after navigation, range changes, and tempo/meter changes.
- [ ] Playback, Practice, live MIDI input, and notation visibility retain their existing behavior.

### Phase 3: Persistence, documentation, and release verification

#### Task 6: Verify saved-score round-trip and update user documentation

**Description:** Exercise the existing save/update/load path with an edited score. Update user-facing documentation to describe the editor and narrow the current limitation that all OMR corrections require an external editor: pitch/rhythm corrections still do, but fingering corrections no longer do.

**Acceptance criteria:**

- [ ] Saving a new imported score and updating an existing saved score both retain edited and removed fingerings after reload.
- [x] No saved-score document version or database schema change is introduced.
- [x] README instructions and the browser test matrix describe the new workflow accurately.

**Verification:**

- [x] Existing `ScoreDocumentSerializerTests` continue to prove fingering round-trip; add a case only if edited/removal behavior exposes an uncovered contract.
- [ ] Hosted manual test: edit, **Save changes**, reload, and confirm the same fingerings.
- [ ] Standalone manual test: edit an imported file and confirm **Save score** remains unavailable only where the existing server limitation applies.

**Dependencies:** Task 5  
**Files likely touched:**

- `README.md`
- `docs/browser-test-matrix.md`
- `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs` only if a persistence gap is found

**Estimated scope:** Small

### Checkpoint: Complete

- [x] `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` passes.
- [x] `node --test PianoMapper.Tests/JavaScript/*.test.mjs` passes.
- [x] `dotnet build PianoMapper.slnx --configuration Release` succeeds without warnings introduced by the change.
- [ ] Hosted and standalone browser checks pass at narrow and wide canvas sizes.
- [x] README and manual browser test documentation match the implemented behavior.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Mapping a selected derived note back to the imported source becomes ambiguous after meter changes | High | Use global note order, which `ScoreTiming.Apply` currently preserves, and lock that invariant with a re-barring regression test. Do not match by pitch/onset. |
| Chords or unison notes select the wrong member | High | Carry measure/note indexes in the scene and choose the closest notehead in two dimensions. Test same-X chord notes explicitly. |
| Selection invalidates the static score cache or causes excessive interop | Medium | Keep selected state outside `GrandStaffSceneCache`, draw one overlay after the cached layer, and perform interop only on clicks/renders. |
| Edit gestures interfere with playback, Practice, or MIDI keyboard input | Medium | Make edit mode explicit and mutually exclusive with playback/practice; retain the existing rule that checkbox/file controls do not suppress piano shortcuts. |
| Canvas-only editing is inaccessible | Medium | Provide a normal HTML note selector and numbered buttons in addition to canvas clicking, with labels that include measure, beat, pitch, and hand. |
| Existing MusicXML placement is accidentally lost | Low | Preserve placement when replacing a number; document that newly assigned fingerings use the renderer's established annotation lane. |

## Open questions for review

- The plan recommends **select a note, then press 1–5 or Remove**. A paint-style workflow (choose a number first, then click many notes) could be added later if annotation speed proves more important, but it is intentionally outside this first slice.
- The plan keeps saving explicit through **Save score** / **Save changes** and does not add dirty-state prompts or autosave. That matches current score timing edits and avoids expanding the persistence behavior.
- The hand prefix remains derived from `ScoreNote.Staff`; this plan does not allow changing right/left-hand assignment while editing a fingering.
