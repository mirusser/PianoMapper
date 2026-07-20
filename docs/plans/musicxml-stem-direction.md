# Implementation Plan: Preserve MusicXML Stem Direction

**Date:** 2026-07-19

**Status:** Awaiting approval

## Goal

Load MusicXML scores such as `Hot-Cross-Buns.mxl` without discarding supported stem-direction intent. Preserve `<stem>up</stem>` and `<stem>down</stem>` in the score model, apply that direction in desktop and web rendering, retain the existing automatic direction when `<stem>` is absent, and reject stem values the current renderer cannot express.

## Context

- `Hot-Cross-Buns.mxl` is a valid compressed MusicXML archive whose notes include `<stem>up</stem>`.
- The current worktree contains a partial compatibility fix in `MusicXmlScoreReader`: `stem` was added to `IgnoredPresentationElements`, and the compressed-reader test currently verifies that it is ignored. Implementation must replace that behavior rather than layer another workaround on top.
- `ScoreNote` currently preserves beam state but has no stem-direction field.
- `GrandStaffLayout.GetScoreNoteLayout` derives direction solely from pitch position. The desktop renderer consumes that resolved layout directly.
- The web renderer initially consumes the same layout, but `GrandStaffSceneBuilder.AddBeam` recomputes one direction for a beam group from its average vertical position. Both locations must honor imported intent.
- The worktree also contains unrelated staged web header/CSS changes and untracked screenshots/score files. They must remain untouched; the local `Hot-Cross-Buns.mxl` may be used for manual verification but must not become a committed test dependency.

## Restated Request

Replace the accept-and-discard handling of MusicXML `<stem>` with bounded, test-driven support that carries stem direction from import through the shared score model into both rendering paths.

## Acceptance Criteria

- MusicXML `up` and `down` values are represented on the corresponding `ScoreNote` and affect rendered stem direction.
- A note without `<stem>` continues to use the existing below-middle-line/up and middle-line-or-above/down heuristic.
- A web beam group with one consistent explicit direction uses that direction rather than the average-pitch heuristic.
- MusicXML `none`, `double`, invalid values, and conflicting explicit directions within one supported beam group fail at import with a readable error; they are not silently normalized.
- Existing callers constructing `ScoreNote` do not require edits solely because the new value is optional.
- The exact local `Hot-Cross-Buns.mxl` loads successfully and reports four measures; all automated tests and the Release build pass.

## Assumptions and Architecture Decisions

- Add a music-domain `ScoreStemDirection` enum (`Up`, `Down`) and an optional `StemDirection` value at the end of `ScoreNote`'s positional record. Do not make `PianoMapper.Music` depend on `PianoMapper.Rendering`, and do not move the existing public rendering enum.
- `ScoreStemDirection` is imported notation intent; `PianoMapper.Rendering.StemDirection` remains the resolved render value. `GrandStaffLayout` is the single mapping boundary between them, using an exhaustive switch and the existing heuristic for `null`.
- For a beam group, no explicit directions means the existing average-position rule. One distinct explicit direction, even if some group members omit `<stem>`, controls the group. Both `Up` and `Down` in the same group are outside the supported subset and are rejected by the MusicXML reader.
- Valid MusicXML values `none` and `double` remain unsupported because they require stem suppression or dual-stem geometry, not just direction selection. Arbitrary values are invalid input. Both cases must name `<stem>` and the value in the user-facing error.
- This change preserves engraving intent; it does not attempt MusicXML round-tripping or preservation of unrelated layout metadata.

## Phase 1: Domain Contract and Import

### Task 1: Add optional score-level stem direction

**Description:** Introduce the notation value in the music domain without changing existing constructor call sites or coupling the domain to rendering types.

**Acceptance criteria:**

- [ ] `ScoreStemDirection` contains only `Up` and `Down`.
- [ ] `ScoreNote.StemDirection` is nullable and defaults to `null` as the final positional parameter.
- [ ] Existing `ScoreNote` construction and `with` expressions compile unchanged and preserve the new value naturally.

**Verification:**

- [ ] Build succeeds: `rtk dotnet build PianoMapper.Core/PianoMapper.Core.csproj`

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Core/Music/ScoreStemDirection.cs`
- `PianoMapper.Core/Music/ScoreNote.cs`

**Estimated scope:** Small

### Task 2: Parse and validate MusicXML stem values

**Description:** Convert `<stem>` from globally ignored presentation metadata into an explicitly recognized note element. Write the failing reader tests first, then parse `up`/`down` and reject unsupported, invalid, or conflicting beam-group values at the import boundary.

**Acceptance criteria:**

- [ ] The existing compressed MusicXML regression is renamed from “ignores stem” to “preserves stem” and asserts the imported `ScoreStemDirection.Up` value.
- [ ] Tests cover `down`, missing `<stem>`, `none`, `double`, an invalid value, and a beam group containing conflicting explicit directions.
- [ ] `stem` is removed from `IgnoredPresentationElements`; all failures include the element and offending value in a readable message.

**Verification:**

- [ ] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`

**Estimated scope:** Medium

## Checkpoint: Imported Contract

- [ ] The score model distinguishes explicit direction from automatic direction.
- [ ] Supported values survive `.mxl` decompression and parsing.
- [ ] Unsupported stem semantics fail during loading, before either renderer receives the score.
- [ ] Core build and all MusicXML reader tests pass.

## Phase 2: Shared and Beamed Rendering

### Task 3: Resolve explicit direction in shared score layout

**Description:** Teach `GrandStaffLayout.GetScoreNoteLayout` to map the optional score value to the rendering enum, falling back to the existing staff-position heuristic only when the score value is absent. This shared layout feeds both desktop score rendering and the initial web scene.

**Acceptance criteria:**

- [ ] An explicit `Up` overrides the automatic down direction for a note above the middle line.
- [ ] An explicit `Down` overrides the automatic up direction for a note below the middle line.
- [ ] Existing no-override tests continue to prove the current automatic heuristic.

**Verification:**

- [ ] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffLayoutTests"`

**Dependencies:** Tasks 1-2

**Files likely touched:**

- `PianoMapper.Core/Rendering/GrandStaffLayout.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffLayoutTests.cs`

**Estimated scope:** Small

### Task 4: Preserve explicit direction across web beam grouping

**Description:** Update web beam direction resolution so a consistent imported score direction controls the beam and all member stems. Retain the current average-position rule for groups without an imported direction and a deterministic automatic fallback for conflicting programmatically constructed scores.

**Acceptance criteria:**

- [ ] A beam group forced opposite to its automatic direction uses the imported direction for the beam and every member stem.
- [ ] A beam group without overrides retains the current average-position behavior and removes individual flags as before.
- [ ] A programmatically constructed group with conflicting overrides renders deterministically via the automatic rule rather than throwing during rendering.

**Verification:**

- [ ] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`

**Dependencies:** Task 3

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Small

## Checkpoint: Rendering Behavior

- [ ] Isolated notes honor imported direction in the shared layout used by desktop and web clients.
- [ ] Web beams do not overwrite a consistent imported direction.
- [ ] Scores without stem metadata render exactly as before.
- [ ] MusicXML, layout, and scene-builder focused tests pass together.

## Phase 3: Contract Documentation and End-to-End Verification

### Task 5: Document the supported subset and verify the exact score

**Description:** Update canonical domain language and user-facing import capability documentation, then verify the complete load and render path with the supplied archive without adding the untracked file as a repository fixture.

**Acceptance criteria:**

- [ ] `CONTEXT.md` describes optional score stem direction and distinguishes it from computed rendering geometry.
- [ ] `README.md` states that `up`/`down` stem direction is preserved and that `none`/`double` remain outside the supported MusicXML subset.
- [ ] Loading local `Hot-Cross-Buns.mxl` reports four measures without an import error; the archive remains untracked unless separately requested.

**Verification:**

- [ ] Focused suites pass together: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests|FullyQualifiedName~GrandStaffLayoutTests|FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [ ] Fast suite passes: `rtk dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"`
- [ ] Release build succeeds: `rtk dotnet build PianoMapper.slnx --configuration Release`
- [ ] Manual browser check: select `Hot-Cross-Buns.mxl`, confirm the loaded status reports four measures, and confirm the score renders without a component error.

**Dependencies:** Tasks 1-4

**Files likely touched:**

- `CONTEXT.md`
- `README.md`

**Estimated scope:** Small

## Final Checkpoint

- [ ] Every acceptance criterion above is satisfied with recorded command output.
- [ ] `rtk git diff --check` reports no whitespace errors.
- [ ] The final diff contains only stem-direction model/import/rendering/tests/docs changes.
- [ ] Existing staged `Piano.razor` and `app.css` changes and unrelated untracked files remain untouched.
- [ ] The implementation is ready for review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Adding a positional record member changes `ScoreNote` equality. | Cached or dictionary-key behavior now distinguishes notes with different engraving intent. | Treat this as desired behavior and run the full suite; keep the member optional and last to preserve source compatibility. |
| Web beam construction currently overwrites per-note layout directions. | Parser support could appear correct while beamed notes still render incorrectly. | Add a beam-specific regression forced opposite to the current average-position heuristic. |
| Two direction enums could drift. | New enum values might not be mapped and could fail at runtime. | Keep the score enum limited to `Up`/`Down` and use an exhaustive mapping switch without a catch-all arm. |
| Mixed voices or cross-staff beams may contain direction patterns outside the current model. | Overgeneralizing could claim fidelity the renderer cannot provide. | Limit support to the existing one-part/two-staff beam model and reject conflicting imported directions with a readable error. |
| The worktree is already dirty. | Implementation could overwrite unrelated user changes or accidentally include local score/screenshots. | Touch only the listed files, inspect staged and unstaged diffs separately, and never stage unrelated paths. |

## Out of Scope

- Rendering MusicXML `stem` values `none` or `double`.
- Exact MusicXML engraving or round-trip preservation.
- Lyrics, work-title import, clef changes, voice preservation, or cross-staff beaming.
- Changes to the existing page header, CSS, screenshots, or local score-file tracking.

## Open Questions

No question blocks this plan. A future engraving-fidelity effort should decide whether `none`, `double`, mixed-voice stems, and cross-staff beams belong in the score model before expanding the enum or renderer.
