# Plan: Chord Identity, Notehead Displacement, Shared Stems, and Onset Spacing

**Date:** 2026-09-22
**Goal:** Render simultaneous notes on the grand staff the way conventional notation does — displaced noteheads and a shared stem for a true chord, correct horizontal spacing that reflects what's actually drawn at each onset — without merging genuinely independent voices into one stem.

## Context

This continues a prior session's handoff. Its finding still holds against current code:

- `PianoMapper.Core/Rendering/GrandStaffLayout.cs:223` (`GetStaffPosition`) already places pitches correctly by diatonic staff step — **out of scope, don't touch.**
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs:128` (`BuildStaticScoreParts`) computes each note's `X` independently via `MapScoreNotationBeatToX` → `GrandStaffLayout.MapScoreOnsetToX`, a fixed, time-proportional mapping from `(measureIndex, beatOffset)` to X. It never measures what's actually drawn at an onset (chord width, accidentals, dots), and it emits one `GrandStaffNote` per `ScoreNote` with no notion of "these two noteheads belong to one stem."
- `PianoMapper.Web/wwwroot/js/canvas.js:736` (`drawNote`) draws each notehead + stem completely independently. No displacement, no shared stems.
- `PianoMapper.Core/Music/MusicXmlScoreReader.cs:375` already computes `bool isChord = FindChild(noteElement, ChordElementName) is not null;` to get the right `beatOffset` for a chord note, then **discards** that flag — `ParseNote` (line 361) never stores it on `ScoreNote`.
- `PianoMapper.Core/Music/ScoreNote.cs:3` is a record with no chord-membership field. It has 67 callers (`MusicXmlScoreReader`, `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`, `PianoMapper.Core/Practice/NoteReadingSession.cs`, `GrandStaffLayout.cs`, plus test files).
- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs:9` defines `CurrentVersion = 1` and `Deserialize` throws on any other version — there is **no migration mechanism**, only additive-field tolerance via `System.Text.Json` defaulting missing JSON properties to the CLR default. Every field added to `ScoreNoteDocument`/`ScoreNote` since v1 (`Accidental`, `Fermata`, `Fingering`) has relied on exactly this: old saved JSON deserializes fine, the new field just comes back as `null`/`false`.

**Key finding from this planning pass** (changes the original handoff's scope for the better): the codebase does not currently need MusicXML `<voice>` numbers at all to solve this. `MusicXmlScoreReader.ParseNote` already advances/holds `cursorDivisions` correctly for both cases:
- A true chord note (`<chord/>` present) attaches to `lastNoteOnsetDivisions` and does **not** advance the cursor.
- An independent voice sharing the same onset only lands there because the MusicXML explicitly rewound the cursor with `<backup>` — and that note has **no** `<chord/>` element.

So the raw per-note `isChord` boolean *is* the chord-membership signal: a note starts a new chord group when `isChord` is `false`, and every immediately-following note with `isChord == true` joins that same group. `ScoreMeasure.Notes` already preserves MusicXML document order (`ParseNote` appends in element order), so chord grouping can be reconstructed later from one boolean per note plus list order — no group IDs, no voice numbers, no new parsing of `<voice>` needed for this feature.

**Also found**: the app already has an informal, coarser notion of "chord" — grouped by shared `(MeasureIndex, BeatOffset)`, not by the MusicXML `<chord/>` flag — in `ScoreFingeringEditor`/`ScoreFingeringGenerator` ("chord member ordinal"), `GraderTests` (`Grade_ChordWithOneWrongMember`), and `ScorePlayback.GetDueEvents` (fires every simultaneous event together). `GrandStaffLayout.GetLabelRowIndexes` groups the same way to stack note-name labels. None of these currently distinguish a real chord from two independent voices sharing an onset — they don't need to for their own purposes (grading/fingering/playback fire together either way; label stacking just needs *some* row per note). This plan does not change that grading/fingering/playback behavior. It only makes the *rendering* layer chord-aware. See open questions below for the one place this could matter later.

Test baseline before any change in this plan: `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj` (includes `GrandStaffSceneBuilderTests`, 96/96) and `node --test PianoMapper.Tests/JavaScript/canvas.test.mjs` (19/19), both green with zero implementation.

## Request / acceptance criteria

1. A true MusicXML chord (second note has `<chord/>`) renders with correctly displaced noteheads (standard "notehead on the other side" convention for a 2nd/adjacent interval) and **one shared stem** per chord per staff.
2. Two independent voices that happen to share an onset (reached via `<backup>`, no `<chord/>`) keep **separate** stems and are not merged into one notehead cluster.
3. Existing saved scores (persisted under document version 1) still load correctly with no migration step.
4. Horizontal spacing between onsets reflects what's actually drawn (chords, accidentals, dots) closely enough that dense onsets no longer visually collide with their neighbors — verified visually, not just by unit test.
5. All existing tests stay green; new tests cover the new behavior.

## Architecture decisions

- **No new persisted concept beyond one additive boolean.** Add `bool IsChordContinuation` (exact name TBD at implementation time, default `false`) to `ScoreNote` and `ScoreNoteDocument`. This preserves exactly the MusicXML `<chord/>` signal, nothing more. Old saved JSON without the property deserializes with `false` for every note — the safe "no grouping known" default, i.e. old saved scores render exactly as they do today (no merged stems) until re-imported from MusicXML.
- **No document-version bump.** Consistent with how `Accidental`/`Fermata`/`Fingering` were added previously.
- **Chord grouping is derived, not stored as a group ID.** A small helper partitions a staff's notes-in-order into chord groups by scanning `IsChordContinuation` runs. This avoids threading a group identifier through 67 call sites.
- **Follow the existing beam-override pattern for shared stems.** `GrandStaffSceneBuilder.BuildBeams` already produces a `Dictionary<ScoreNote, (StemDirection, StemEndY, BeamCount)>` of per-note overrides computed from a cross-note grouping. Shared chord stems should follow the same shape (a `Dictionary<ScoreNote, ...>` of overrides computed once from chord groups), not a new parallel rendering path.
- **Displacement math lives in C#, not canvas.js.** `GrandStaffLayout`/`GrandStaffSceneBuilder` already own all note geometry (`ScoreNoteLayout.X`, `.Y`); canvas.js only draws what it's given (`drawNote` at `canvas.js:736`). Chord displacement should compute a per-note `X` offset in `GrandStaffSceneBuilder` (same place `renderedX` is already computed per note) so canvas.js needs no chord-awareness at all — it already draws independent noteheads/stems correctly, it just needs the right `X`/stem data per note.

## Task list

### Phase 1: Chord identity (parse → model → persistence)

- [ ] **Task 1 — Add a MusicXML fixture/test that pins the chord-vs-voice distinction.**
  **Description:** In `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`, add two small MusicXML fixtures: (a) a measure with two notes on one staff, the second carrying `<chord/>`; (b) a measure with two notes on one staff at the same onset reached via `<backup>`, neither carrying `<chord/>`. Assert the parsed `ScoreNote`s' new chord field (added in Task 2) is `true` for the second note in (a) and `false` for both notes in (b). Since the field doesn't exist yet, this test is written first and will not compile/pass until Task 2 lands (red step).
  **Acceptance criteria:**
  - [ ] Both fixtures parse without throwing and produce notes at the same `(MeasureIndex, BeatOffset, Staff)`.
  - [ ] Test asserts the field distinguishes (a) from (b).
  **Verification:** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter MusicXmlScoreReaderTests` (expected to fail/not compile until Task 2).
  **Dependencies:** None.
  **Files:** `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`.
  **Size:** S.

- [ ] **Task 2 — Preserve chord membership through the model and persistence.**
  **Description:** Add the boolean field to `ScoreNote` (`PianoMapper.Core/Music/ScoreNote.cs`), set it from the existing `isChord` local in `MusicXmlScoreReader.ParseNote` (`MusicXmlScoreReader.cs:375`), and thread it through `ScoreDocumentSerializer`'s `ScoreNoteDocument`/`ToDocument`/`FromDocument` (`PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs:64,92,129`). Check the other listed `ScoreNote` construction sites (`PianoMapper.Core/Practice/NoteReadingSession.cs`, `PianoMapper.Core/Music/SightReadingExerciseComposer.cs`, `ScoreFingeringEditor.cs`'s `note with { ... }`) — most only need a default `false`/unchanged value via `with`-expressions or a trailing optional parameter; confirm none need to *set* it. Specifically check whether `SightReadingExerciseComposer` ever synthesizes two notes at one onset that are meant to be a chord (e.g. hands-together interval drills) — if so, it should set the new field so generated content benefits from Phase 3/4 too.
  **Acceptance criteria:**
  - [ ] Task 1's test passes.
  - [ ] `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs` covers: serialize → deserialize round-trip preserves the field; deserializing a JSON payload captured *before* this change (missing the property) still succeeds and yields `false` for every note.
  - [ ] `ScoreTests.cs`, `NoteReadingSessionTests.cs`, `GrandStaffLayoutTests.cs`, `ScorePlaybackTests.cs` still compile and pass unmodified (or with only trivial constructor-argument additions if `ScoreNote` isn't a `with`-friendly call site).
  **Verification:** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`.
  **Dependencies:** Task 1.
  **Files:** `ScoreNote.cs`, `MusicXmlScoreReader.cs`, `ScoreDocumentSerializer.cs`, `ScoreDocumentSerializerTests.cs`, possibly `SightReadingExerciseComposer.cs`.
  **Size:** M.

### Checkpoint: Phase 1

- [ ] Full `dotnet test` suite green.
- [ ] Confirm with a manual read: does `SightReadingExerciseComposer` need to set the new field? Record the answer (even if "no, it never emits simultaneous notes on one staff").
- [ ] Review with user before proceeding to rendering changes.

### Phase 2: Chord notehead displacement

- [ ] **Task 3 — Derive chord groups and compute displacement in `GrandStaffSceneBuilder`.**
  **Description:** Add a helper (in `GrandStaffLayout.cs` or a new small type alongside it, matching where `GetLabelRowIndexes` already lives) that partitions each staff's notes-in-order into chord groups using the Phase 1 field. For each group with 2+ notes, apply standard notehead-displacement rules (adjacent-step members alternate sides of the stem) and adjust each member's `X` in `BuildStaticScoreParts` (`GrandStaffSceneBuilder.cs:128`, where `renderedX` is currently assigned uniformly) instead of the shared onset X.
  **Acceptance criteria:**
  - [ ] A 2-note chord a 2nd apart renders with one notehead offset from the other (verified by a `GrandStaffSceneBuilderTests` assertion on `GrandStaffNote.X`).
  - [ ] A chord with no adjacent-step collision (e.g. a 3rd or wider) renders both noteheads at the same X, unchanged from today.
  - [ ] Two independent-voice notes at the same onset (Task 1's fixture (b) shape) are *not* displaced relative to each other by this logic.
  **Verification:** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter GrandStaffSceneBuilderTests`.
  **Dependencies:** Phase 1.
  **Files:** `GrandStaffLayout.cs`, `GrandStaffSceneBuilder.cs`, `GrandStaffSceneBuilderTests.cs`.
  **Size:** M.

### Checkpoint: Phase 2

- [ ] `GrandStaffSceneBuilderTests` and `canvas.test.mjs` both green.
- [ ] Visual check per [[pianomapper-visual-verification-workflow]]: screenshot a chord-containing score at the smallest clamped canvas height, confirm displaced noteheads don't collide with ledger lines/accidentals.

### Phase 3: Shared stems for chords

- [ ] **Task 4 — Merge chord members onto one stem, mirroring `BuildBeams`'s override pattern.**
  **Description:** Extend (or add a sibling to) the `beamOverrides` dictionary in `GrandStaffSceneBuilder.BuildStaticScoreParts` so a chord group produces one shared `StemDirection`/`StemEndY` for all its members (stem direction from the extreme note, length covering the full chord span), while non-chord notes at a shared onset (independent voices) keep computing their stem independently as today.
  **Acceptance criteria:**
  - [ ] A 2-note chord's rendered notes share identical `StemDirection` and `StemEndY`.
  - [ ] Two independent voices sharing an onset each keep their own, potentially different, stem direction/length.
  - [ ] Beamed chords (a chord where the top or bottom note is also part of a beam group) still resolve to one consistent stem per chord — check interaction with existing `ValidateBeamStemDirections`/`BuildBeams` logic rather than assuming no conflict.
  **Verification:** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter GrandStaffSceneBuilderTests`.
  **Dependencies:** Phase 2 (shares the per-note override plumbing).
  **Files:** `GrandStaffSceneBuilder.cs`, `GrandStaffSceneBuilderTests.cs`.
  **Size:** M.

### Checkpoint: Phase 3

- [ ] Full `dotnet test` + `node --test PianoMapper.Tests/JavaScript/*.test.mjs` green.
- [ ] Visual check: a beamed passage containing at least one chord renders one stem per chord, correct beam angle.
- [ ] Review with user before the spacing pass — it's the highest-risk, most visible phase.

### Phase 4: Onset spacing pass

- [ ] **Task 5 — Measure per-onset rendered width and replace fixed time-proportional X.**
  **Description:** Replace `GrandStaffLayout.MapScoreOnsetToX`'s pure `beatOffset / timeSignature.Numerator` proportion (used at `GrandStaffSceneBuilder.cs:128` via `MapScoreNotationBeatToX`) with a spacing model that accounts for what's actually drawn at each onset across both staves — chord width from Phase 2, accidental glyphs, dots. Keep barlines and the playback cursor (`GrandStaffLayout.GetScoreBarlineXs`, `MapAbsoluteBeatToScoreX`, used by the live/rolling piano-roll view too) consistent with whatever onset-to-X mapping results — the live view's `MapTimeToX`/`GetLiveNoteLayout` path (`GrandStaffLayout.cs:40-138`) is a *different*, playback-time-based mapping and must stay untouched; only the static score-notation mapping changes.
  **Acceptance criteria:**
  - [ ] A measure containing one dense chord/accidental cluster and mostly single notes still fits in the same barline-to-barline span, but the dense onset no longer visually overlaps its neighbor.
  - [ ] Barline positions and the playback cursor line still align with note onsets (existing `GrandStaffSceneBuilderTests` cursor/barline assertions still pass or are updated deliberately).
  - [ ] Live/rolling piano-roll rendering (non-score-notation view) is unaffected — its own tests (`ScorePlaybackTests`, whatever covers `GetLiveNoteLayout`) stay green untouched.
  **Verification:** `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`; `node --test PianoMapper.Tests/JavaScript/*.test.mjs`.
  **Dependencies:** Phase 2 (needs chord width) and Phase 3 (stems don't affect width but should be re-verified once X changes).
  **Files:** `GrandStaffLayout.cs`, `GrandStaffSceneBuilder.cs`, `GrandStaffSceneBuilderTests.cs`.
  **Size:** L — likely worth breaking into sub-tasks once the spacing model is chosen (e.g. "compute per-onset width" as one task, "redistribute X across a measure" as another). Do that breakdown at the start of this phase, informed by what Phases 2-3 actually produced.

### Checkpoint: Phase 4 / Complete

- [ ] Full test suite green.
- [ ] Visual verification per [[pianomapper-visual-verification-workflow]] on at least one real imported score with chords, at the smallest clamped canvas height.
- [ ] Re-run the desktop/browser manual check for anything in `docs/browser-test-matrix.md` that touches score rendering, if the checklist calls for it.
- [ ] All acceptance criteria from the Request section met.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| A chord group spans a beam boundary in an unexpected way (e.g. only the top note of a chord is beamed) | Med | Task 4 explicitly checks interaction with `BuildBeams`/`ValidateBeamStemDirections` before merging stem logic |
| Spacing pass (Phase 4) regresses barline/cursor alignment used by the live piano-roll cursor line | High | `GetCursorLineYBounds`/live mapping (`MapTimeToX`) is explicitly called out as untouched; only the static notation X mapping changes |
| `SightReadingExerciseComposer` silently generates simultaneous notes without chord marking, so generated content never benefits from Phases 2-3 | Low | Explicit check added to Task 2's acceptance criteria |
| Phase 4 is underestimated — spacing algorithms are easy to get "mostly right" and hard to get fully right | Med | Sized as L/needs-further-breakdown up front; checkpoint before starting it; visual verification required, not just unit tests |

## Open questions

- **Is true multi-voice rendering (independent rhythmic streams sharing a staff, each with its own stem, at any beat offset, not just at a shared onset) an actual goal?** This plan only guarantees that two voices *sharing one onset* don't get incorrectly merged into one chord. It does not add general multi-voice layout (e.g. voice 1 stems up, voice 2 stems down, overlapping note spans, cross-voice collision avoidance). Given PianoMapper's target content is beginner/practice piano scores, confirm whether that's out of scope entirely, or whether a future phase should read and preserve `<voice>` numbers properly (this plan's finding is that Phase 1-4 don't need them, but a real multi-voice feature would).
- **Displacement convention specifics**: which side does the displaced notehead go on for an upward vs. downward stem, and what's the exact interval threshold (a 2nd only, or seconds and unisons)? Task 3 should pin this down with a cited convention (e.g. matching the VexFlow `stavenote.ts` behavior referenced in the original handoff) before implementing, rather than inventing one.
- **Phase 4's spacing model**: proportional-with-minimums, or a true tick-based formatter (VexFlow's `formatter.ts` approach)? Recommend deciding this at the start of Phase 4 with a quick prototype/comparison rather than committing now.
