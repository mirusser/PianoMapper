# Implementation Plan: Tuplet (Triplet-First) Support

**Date:** 2026-09-23

**Status:** Complete

## Goal

Let PianoMapper import, persist, play back, and render MusicXML notes carrying a `<time-modification>` tuplet ratio (starting with eighth-note triplets) instead of rejecting the whole score with `NotSupportedException: Unsupported MusicXML element <time-modification>`.

## Context

This surfaced while manually testing the score-image import feature end-to-end (upload `gama-C-major.jpg`, a scanned C-major-scale exercise, through the running app at `http://localhost:5080`, driven via headless Firefox BiDi). Two sequential import blockers were found and diagnosed by inspecting the raw Audiveris MusicXML output directly (bypassing the app, running `.tools/audiveris-5.10.2/opt/audiveris/bin/Audiveris` standalone):

1. `<articulations>` inside `<notations>` — Audiveris hallucinated `<tenuto>`/`<staccato>`/`<staccatissimo>` marks on ~45 of ~55 notes in the first system (almost certainly misreading the phrase-grouping curves under each beamed group; the source page has no such marks). **Already fixed, uncommitted**, in `PianoMapper.Core/Music/MusicXmlScoreReader.cs`: added an `ArticulationsElementName` const and included it alongside `TechnicalElementName`/`FermataElementName` in the "recognized but ignored" branch inside `HasTieStart`'s `<notations>` loop. This fix is orthogonal to this plan and not part of it.
2. `<time-modification>` — **the subject of this plan; now implemented.** After the articulations fix, import advanced further and hit a genuine triplet-shaped note group: three consecutive beamed eighth notes (`C6`, `D5`, `E5` — each with a real, distinct pitch), tagged `<time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>` plus a `<tuplet type="start"/">`/`type="stop"` pair in `<notations>`. `MusicXmlScoreReader` has no representation for a tuplet ratio anywhere: `<time-modification>` is not in `SupportedNoteElements`, so the note-child loop throws before the ratio is ever read; separately, `<tuplet>` inside `<notations>` is not in the recognized set either, so it would throw there too if reached first.

   Note the melodic shape (`C6 → D5` is a downward major-7th leap mid-scale-run) strongly suggests this specific triplet is itself an Audiveris rhythm-segmentation misread of three straight eighth notes, not a real notated triplet on the source page. This plan does **not** claim `gama-C-major.jpg` contains a real triplet — see Task 8 and the Risks table.

   There is already a *different*, narrower triplet-shaped cleanup in `PianoMapper.Server/Omr/AudiverisMusicXmlNormalizer.cs` (`NormalizeFingeringTuplets`): it strips out unbeamed **quarter**-note triplets where **only the middle note has a pitch** (Audiveris misreading a single fingering-digit glyph as three notes) and converts them back into one plain note plus a `<fingering>`. That heuristic correctly leaves the triplet described above alone, because this one is beamed eighths with a real pitch on all three notes — it is a structurally different case. Do not conflate the two; `NormalizeFingeringTuplets` stays as-is.

- Root domain gap: `PianoMapper.Core/Music/NoteValue.cs` is `readonly record struct NoteValue(int Denominator, int Dots)`, denominator restricted to `{1, 2, 4, 8, 16}`. There is no tuplet ratio field. `CONTEXT.md`'s `NoteValue` glossary row explicitly scopes it to "a denominator ... and optional dots," excluding everything else.
- Confirmed chokepoint via codegraph blast-radius query: `MusicalTime.GetBeats(NoteValue, TimeSignature)` → `GetWholeNoteFraction(NoteValue)` (`PianoMapper.Core/Music/MusicalTime.cs`) is the **single** place duration turns into beats. It is used, directly or transitively, by `ScoreNote.BeatOffset` accumulation during import, `GrandStaffLayout` rendering/spacing, `PianoMapper.Core/Music/ScorePlayback.cs`, and `PianoMapper.Core/Music/MetronomeGrid.cs`. Extending the ratio math there — rather than threading a new parameter through every caller — is the lowest-blast-radius way to make beat math, spacing, and playback all tuplet-aware at once.
- Persistence: `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`'s private `NoteValueDocument(int Denominator, int Dots)` mirrors `NoteValue` for saved-score JSON; it needs the same extension for round-trip fidelity.
- Rendering: `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs` (and `PianoMapper.Core/Rendering/GrandStaffLayout.cs` / `ScoreNoteLayout`) already compute horizontal spacing from cumulative beats (`MapScoreNotationBeatToX` and friends), so spacing should fall out correctly for free once the beat math is ratio-aware — this plan's rendering task is mostly *additive* (drawing a tuplet numeral), not corrective. `PianoMapper/Rendering/StaffRenderer.cs` is a second, desktop consumer of `ScoreNoteLayout` found by the same blast-radius query — see Open Questions.
- `RandomMeasureComposer.PossibleNoteValues`, `SightReadingExerciseComposer.QuarterNote`, and `GrandStaffSceneBuilder.supportedLiveNoteValues` all construct `NoteValue` directly and must keep compiling unchanged (precedent: the prior `ScoreStemDirection` plan added its new field as an optional trailing member for exactly this reason).
- Style/format precedent: `docs/plans/musicxml-stem-direction.md` did the same shape of work (add an optional MusicXML-imported attribute end-to-end through domain → reader → renderer → docs) and is the template this plan follows.
- Test convention: `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs` loads fixtures from `PianoMapper.Tests/UnitTests/Fixtures/*.musicxml` by filename (see `Fixture(name)` helper), not inline XML strings.

## Restated Request

Add tuplet-ratio support (`actual-notes`/`normal-notes`, general at the model/import level, triplet-only for v1 rendering) to the score domain model, MusicXML import, persistence, playback timing, and web rendering, so a genuinely tuplet-notated MusicXML score imports and plays back with correct timing instead of being rejected outright.

## Acceptance Criteria

- A MusicXML note with `<time-modification>` (`actual-notes`/`normal-notes`) imports successfully with beat-accurate duration, instead of throwing `NotSupportedException`.
- The note immediately following an imported tuplet group has the correct `BeatOffset` (proves beat-accumulation, not just the tuplet note's own duration, is ratio-aware).
- A saved score containing a tuplet round-trips through `ScoreDocumentSerializer` (serialize → deserialize) with the ratio intact.
- Playback/metronome scheduling (`ScorePlayback`, `MetronomeGrid`) produces the correct wall-clock duration for a tuplet note, verified by a test, not assumed from the shared beat-math change.
- A beamed eighth-note triplet renders in the web grand staff with correct horizontal spacing relative to its neighbors and a visible "3" indicating the tuplet.
- Existing callers that construct `NoteValue` without a ratio continue to compile and behave exactly as before (ratio defaults to the 1:1 identity — no tuplet).
- `CONTEXT.md` and `README.md` describe the supported subset (see Out of Scope) accurately.
- Re-uploading `gama-C-major.jpg` (with the already-applied articulations fix) through the running app either imports successfully or fails with a *different, unrelated* error — confirming this plan closes the `<time-modification>` blocker specifically.

## Assumptions and Architecture Decisions

- **Anchor the ratio on `NoteValue` itself**, not on `ScoreNote` or a sibling type. `MusicalTime.GetBeats` takes a bare `NoteValue`, and `NoteValue` is also independently used for `ScoreRest`, `TimeSignature.BeatNoteValue`, and free-play/random/sight-reading note values that will never carry a tuplet ratio. Two new `int` fields defaulting to `1`/`1` (meaning "no tuplet," a no-op multiplier) keep every non-tuplet call site source-compatible, matching the precedent set when `ScoreStemDirection` was added to `ScoreNote` as an optional trailing member.
- **Name the fields after the MusicXML vocabulary already used elsewhere in this codebase**: `TupletActualNotes` / `TupletNormalNotes`, mirroring `<time-modification><actual-notes>`/`<normal-notes>` and the identical naming already chosen in `AudiverisMusicXmlNormalizer`'s `IsFingeringTupletSequence`/`IsUnbeamedQuarterTriplet` helpers. Avoids inventing a second vocabulary (e.g. "ratio numerator/denominator") for the same concept.
- **Support the general ratio at the model/reader level**, since storing two extra ints costs nothing and `<time-modification>` is not triplet-specific in MusicXML. **Scope rendering (the tuplet numeral) to triplets (3:2) for v1** — the common case — and require beaming for the numeral to have an unambiguous group to anchor to (see Open Questions for the unbeamed case).
- **The `<tuplet>` marker inside `<notations>` carries no ratio data in MusicXML** (the ratio lives entirely in `<time-modification>`); treat it the same as `<technical>`/`<fermata>`/`<articulations>` — recognized and skipped by the reader's notations loop — rather than parsed for now, since v1 rendering derives its tuplet group from beaming + a shared non-identity ratio, not from `<tuplet>` start/stop markers. Revisit if a later need (e.g. unbeamed tuplet grouping) requires the explicit start/stop boundary.
- **`ValidateDuration` gets a ratio multiplier**, not a new code path: `expectedQuarterNotes = 4.0 * dotMultiplier * (normalNotes / actualNotes) / denominator`, keeping the existing epsilon-based comparison.
- This plan does not attempt to determine whether `gama-C-major.jpg`'s specific triplet is a real notated triplet or an OMR misread — it only makes the reader capable of accepting either. Task 8 re-tests the real image but treats "imports successfully" and "matches the printed page" as separate questions.

## Phase 1: Domain Contract and Import

### Task 1: Add a tuplet ratio to `NoteValue` and thread it through beat math

**Description:** Add `TupletActualNotes`/`TupletNormalNotes` (default `1`/`1`) to `NoteValue`, validate both are positive, and multiply them into `MusicalTime.GetWholeNoteFraction` so `GetBeats`/`ToDuration`/`DurationToBeats` all become ratio-aware from this single change.

**Acceptance criteria:**

- [x] `NoteValue(4)` and `new NoteValue(4, dots: 0)` remain valid and equal to their current behavior (ratio defaults to 1:1).
- [x] `new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2)` throws for non-positive `actualNotes`/`normalNotes`, and otherwise constructs successfully.
- [x] `MusicalTime.GetBeats` for an eighth-note triplet value returns 2/3 of a plain eighth note's beats under a 4/4 time signature.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicalValueTypesTests"`
- [x] Build succeeds: `rtk dotnet build PianoMapper.Core/PianoMapper.Core.csproj`

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Core/Music/NoteValue.cs`
- `PianoMapper.Core/Music/MusicalTime.cs`
- `PianoMapper.Tests/UnitTests/MusicalValueTypesTests.cs`

**Estimated scope:** Small

### Task 2: Parse and validate `<time-modification>` in MusicXML import

**Description:** Write failing reader tests first (a new `eighth-note-triplet.musicxml` fixture under `PianoMapper.Tests/UnitTests/Fixtures/`), then add `<time-modification>` to `SupportedNoteElements`, parse `<actual-notes>`/`<normal-notes>` in `ParseNoteValue`, add `<tuplet>` to the recognized-and-ignored branch in `HasTieStart`'s notations loop (alongside `TechnicalElementName`/`FermataElementName`/`ArticulationsElementName`), and extend `ValidateDuration` with the ratio multiplier.

**Acceptance criteria:**

- [x] The new fixture (three beamed eighth-note triplet notes, `actual-notes=3`/`normal-notes=2`, followed by a plain note) imports without throwing.
- [x] The imported triplet notes have `NoteValue.TupletActualNotes == 3` and `TupletNormalNotes == 2`.
- [x] The plain note immediately after the triplet has the correct `BeatOffset` (proves accumulated beat math, not just the triplet notes' own duration).
- [x] `<time-modification>` present without a matching, plausible `<tuplet>` pairing still imports (the `<tuplet>` marker is decorative only per the architecture decision above) — add a regression asserting this rather than assuming it.
- [x] A malformed `<time-modification>` (missing `<actual-notes>` or `<normal-notes>`, or a non-positive value) fails at import with a readable error naming the element.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`
- `PianoMapper.Tests/UnitTests/Fixtures/eighth-note-triplet.musicxml` (new)

**Estimated scope:** Medium

## Checkpoint: Imported Contract

- [x] A genuinely tuplet-notated MusicXML fixture imports successfully with beat-accurate offsets.
- [x] Non-tuplet scores are unaffected (full existing `MusicXmlScoreReaderTests` suite still passes).
- [x] Core build and all Task 1–2 tests pass together.

## Phase 2: Persistence and Playback Verification

### Task 3: Round-trip the tuplet ratio through saved-score persistence

**Description:** Extend the private `NoteValueDocument` record in `ScoreDocumentSerializer` with the two new fields (default `1`/`1`) and thread them through `ToDocument`/`FromDocument` for both `ScoreNote.NoteValue` and `ScoreRest.NoteValue`.

**Acceptance criteria:**

- [x] Serializing then deserializing a `Score` containing a tuplet note preserves `TupletActualNotes`/`TupletNormalNotes` exactly.
- [x] Serializing then deserializing a `Score` with no tuplets is byte-for-byte/structurally unchanged from before this task (no accidental JSON shape change for the common case).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreDocumentSerializerTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`
- `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs`

**Estimated scope:** Small

### Task 4: Verify playback and metronome timing for a tuplet note

**Description:** The working hypothesis (from the Context section) is that `ScorePlayback` and `MetronomeGrid` need **no code changes**, because both consume `MusicalTime`'s beat/duration conversions that Task 1 already made ratio-aware. Prove this with a regression rather than assuming it; fix whichever call site breaks the hypothesis if one does.

**Acceptance criteria:**

- [x] A test constructs a score/measure containing a tuplet note and asserts the scheduled wall-clock duration `ScorePlayback` produces for it matches the ratio-adjusted expectation (e.g. an eighth-note triplet at a known tempo lasts 2/3 of a plain eighth note).
- [x] A test asserts `MetronomeGrid`'s nearest-beat/deviation math is unaffected by a tuplet note occupying the same time window (a tuplet doesn't shift the underlying beat grid).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScorePlaybackTests|FullyQualifiedName~MetronomeGridTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Music/ScorePlayback.cs` (only if the hypothesis is wrong)
- `PianoMapper.Core/Music/MetronomeGrid.cs` (only if the hypothesis is wrong)
- `PianoMapper.Tests/UnitTests/ScorePlaybackTests.cs`
- `PianoMapper.Tests/UnitTests/MetronomeGridTests.cs`

**Estimated scope:** Small

## Checkpoint: Data and Timing

- [x] Saved scores with tuplets round-trip correctly.
- [x] Playback and metronome timing are proven correct for a tuplet note by a passing test, not assumed.
- [x] All Phase 1–2 tests pass together.

## Phase 3: Rendering

### Task 5: Render a triplet numeral in the web grand staff

**Description:** In `GrandStaffSceneBuilder`'s beam-building path (`BuildBeams` and friends in `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`), detect a beam group whose members share a single non-identity tuplet ratio (`TupletActualNotes != TupletNormalNotes`) and add one small numeral glyph (e.g. "3") at the group's horizontal centroid, above or below the beam by stem direction — following the existing minimal-glyph precedent set by fermata rendering rather than a full bracket-line implementation.

**Acceptance criteria:**

- [x] A beamed eighth-note triplet fixture renders exactly one "3" glyph positioned at the group's centroid.
- [x] Horizontal spacing between the triplet notes and their neighbors reflects the ratio-adjusted beat math from Task 1 (i.e., three triplet eighths occupy the same horizontal span as two plain eighths) — assert this directly on rendered `X` values, don't just trust the beat math transitively.
- [x] A score with no tuplets renders no numeral and is otherwise pixel-identical to before this task (no incidental layout shift).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] Manual screenshot check: load the Task 2 fixture in the running app (headless Firefox via WebDriver BiDi, `--remote-debugging-port`, per this session's working recipe) and visually confirm the "3" and spacing.

**Dependencies:** Tasks 1–2

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Medium

## Checkpoint: Rendering Behavior

- [x] A beamed triplet renders with correct spacing and a visible numeral in the web client.
- [x] Non-tuplet rendering is provably unchanged (existing scene-builder tests still pass).
- [x] Manual screenshot confirms the above in the actual running app, not just unit assertions.

## Phase 4: Documentation and End-to-End Verification

### Task 6: Document the supported subset

**Description:** Update `CONTEXT.md`'s `NoteValue` glossary row to mention the tuplet ratio and what it excludes, and update `README.md`'s MusicXML-import capability description to state that triplets are supported and what isn't (see Out of Scope).

**Acceptance criteria:**

- [x] `CONTEXT.md` `NoteValue` row reflects the new field without implying full general-tuplet engraving fidelity.
- [x] `README.md` states triplet support and names the explicit exclusions below.

**Verification:**

- [x] Manual read-through diff against the Out of Scope list.

**Dependencies:** Tasks 1–5

**Files likely touched:**

- `CONTEXT.md`
- `README.md`

**Estimated scope:** Small

### Task 7: Re-verify the originating real-world case

**Description:** With the articulations fix (already applied) and this plan's changes in place, re-run the exact repro from this debugging session: upload `gama-C-major.jpg` through the running app's file picker and observe the result.

**Acceptance criteria:**

- [x] The import either succeeds, or fails with an error that is demonstrably *not* `<time-modification>`-related (proving this plan's scope is closed, independent of whatever else this specific noisy scan may still trip on).
- [ ] N/A — import did not succeed (see Actual result below), so there was nothing to screenshot; the ottava passage was not reached. The note about the ottava sign never being recognized as an `<octave-shift>` (a separate, unresolved OMR-recognition gap, not a PianoMapper defect) still stands from the original diagnosis and remains true whenever import does get that far.

**Verification:**

- [x] Manual browser check via the app's file picker, screenshotted.

**Actual result:** Re-uploading `gama-C-major.jpg` through the running app (after rebuilding with all of Tasks 1–6) now fails with `422 Unsupported MusicXML element <slur>` instead of `<time-modification>` — a different, unrelated notation element (Audiveris again reading the source's phrase-grouping curves, this time as `<slur>` rather than `<articulations>`, now that the earlier blocker is past). This satisfies the acceptance criterion: the `<time-modification>` blocker is conclusively closed. `<slur>` support is a new, separate gap, out of scope here — the import still does not fully succeed for this specific noisy scan, and the ottava passage was never reached to inspect.

**Dependencies:** Tasks 1–6

**Files likely touched:** None (verification only)

**Estimated scope:** Small

## Final Checkpoint

- [x] Every acceptance criterion above is satisfied with recorded command output.
- [x] Fast suite passes: `rtk dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"`
- [x] Release build succeeds: `rtk dotnet build PianoMapper.slnx --configuration Release`
- [x] The final diff contains only tuplet-support model/import/persistence/playback/rendering/tests/docs changes, plus the already-separately-applied `<articulations>` fix.
- [x] Ready for review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Changing `MusicalTime.GetWholeNoteFraction`, a single chokepoint used everywhere duration matters, could silently shift timing for every existing score. | Widespread regression across import, rendering, and playback. | Default ratio is 1:1 (multiplier of exactly 1.0, a mathematical no-op); run the full existing suite, not just new tuplet tests, before considering Task 1 done. |
| Unbeamed tuplets have no unambiguous group to anchor a numeral to. | Task 5 could ship an inconsistent or missing indicator for a valid import. | v1 explicitly scopes numeral rendering to beamed tuplets only (see Out of Scope); the note still imports and plays back correctly either way, since Tasks 1–4 don't require beaming. |
| Floating-point comparison in `ValidateDuration` with an added ratio multiplier could introduce new tolerance edge cases for uncommon ratios (e.g. 5:4, 7:8). | False-positive rejection of a valid tuplet, or false-positive acceptance of an invalid one. | Keep the existing epsilon-based comparison; add explicit test cases beyond 3:2 if general-ratio import is exercised. |
| The originating `gama-C-major.jpg` triplet may be OMR noise, not a real notated triplet. | This plan could be mistaken for "fixing" that specific image's fidelity, when it only makes the reader accept the ratio Audiveris already produced. | Task 7 explicitly separates "imports without throwing" from "matches the printed page" and records both outcomes distinctly. |
| Two rendering targets (`PianoMapper.Web` and desktop `PianoMapper/Rendering/StaffRenderer.cs`) consume the same `ScoreNoteLayout`. | Shipping the numeral only in the web client could be an intentional scope cut or an accidental gap, depending on whether the desktop renderer is still actively used. | See Open Questions — confirm desktop renderer status before or during Task 5 rather than silently skipping or silently duplicating the work. |

## Out of Scope

- Nested tuplets, tuplets spanning multiple voices or crossing staves.
- A full tuplet bracket line — v1 draws a numeral only, matching the existing minimal-glyph precedent (fermata).
- A numeral for an unbeamed tuplet.
- Any visual/rendering polish for non-3:2 ratios — the model and reader accept a general `actual:normal` ratio, but v1 rendering work targets triplets specifically.
- Changes to Audiveris/OMR recognition itself. The ottava (8va) sign not being recognized, and the specific triplet misread found in `gama-C-major.jpg`, are OMR-quality issues upstream of this app and are not addressed by this plan.

## Open Questions

- Is the desktop `PianoMapper` project (via `PianoMapper/Rendering/StaffRenderer.cs`) still an actively maintained rendering target, or has `PianoMapper.Web` fully superseded it? This determines whether Task 5 needs a desktop counterpart or whether triplet-numeral rendering is intentionally web-only for now. **Not resolved** — implementation took the default (web-only; `StaffRenderer.cs` was not touched). The desktop client still imports, persists, and plays back tuplets correctly (all of that is in shared `PianoMapper.Core`); it just never draws a numeral.
- Should an unbeamed tuplet be rejected at import time with a readable error (consistent with how the prior stem-direction plan rejected MusicXML values the renderer couldn't express), or accepted silently with no visual indicator until a later plan adds unbeamed-numeral placement? **Resolved by default in this implementation:** accepted silently, no numeral (`AddTupletGlyph` only fires from within `AddBeam`, which already requires `group.Count >= 2`). Revisit if that turns out to be the wrong call.
