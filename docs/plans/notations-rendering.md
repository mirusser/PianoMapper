# Implementation Plan: Render Tolerated `<notations>` Elements on the Grand Staff

**Date:** 2026-09-23

**Status:** Complete

## Goal

For every MusicXML `<notations>` child this session made import-tolerant but that is currently parsed-and-discarded — `articulations`, `slur`, `arpeggiate`, `non-arpeggiate`, `glissando`, `slide`, `ornaments`, `accidental-mark` — carry the data into the score domain model and draw it on the web grand staff, the same way `<technical><fingering>` and `<notations><fermata>` already are. `other-notation` gets an explicit, justified "stays unrendered" decision rather than an invented generic visual.

## Context

This follows directly from `docs/plans/tuplet-support.md` (Complete). While re-verifying that plan's fix against a real scanned image (`gama-C-major.jpg`) through Audiveris OMR, the reader kept rejecting the score on one `<notations>` child at a time: first `articulations`, then (after that fix) `slur`, then (after that fix) `arpeggiate`/`non-arpeggiate`/`glissando`/`slide`/`ornaments`/`accidental-mark`/`other-notation` in the same pass. Rather than keep whack-a-moling the reader, `PianoMapper.Core/Music/MusicXmlScoreReader.cs`'s `HasTieStart` notations loop was broadened into one `IgnoredNotationsElements` `FrozenSet<string>`:

```csharp
private static readonly FrozenSet<string> IgnoredNotationsElements = new[]
{
    TechnicalElementName, FermataElementName, ArticulationsElementName, TupletElementName,
    SlurElementName, "arpeggiate", "non-arpeggiate", "glissando", "slide", "ornaments",
    "accidental-mark", "other-notation",
}.ToFrozenSet(StringComparer.Ordinal);
```

That only stops the reader from throwing. None of this data reaches `ScoreNote` or the renderer — confirmed by grepping `ScoreNote.cs`/`GrandStaffSceneBuilder.cs`/`GrandStaffGlyphKind.cs` for every one of these names: zero matches outside the parser. The user asked for all of them to actually render.

`tuplet` is **not** in this plan's scope: its ratio already renders correctly via `<time-modification>` → `NoteValue` → the triplet numeral built in the tuplet-support plan. The bare `<tuplet>` element is just a decorative bracket/visibility marker with no data of its own — nothing to add.

### What's already there to build on

- **Per-note field + parse function, one flag/value at a time.** `ScoreNote` (`PianoMapper.Core/Music/ScoreNote.cs`) has exactly two notation fields today: `Fingering` (parsed by `ParseFingering`, reading `<notations><technical><fingering>`) and `Fermata` (parsed by `ParseFermata`, reading `<notations><fermata>`). Both are read in `ParseNote` and follow the same shape: find the element under `<notations>`, validate there's at most one, map its value/attributes to a small enum or record, reject anything unrecognized with a message naming the element. This is the direct template for **articulations**, **accidental-mark**, and (per-note) **ornaments**.
- **`<tie>`/`<tied>` start/stop precedent.** `HasTieStart` (same file) shows the established pattern for a `type="start"`/`type="stop"` pairing element: a boolean per note (`TiesToNext`), with the actual note-to-note pairing resolved later, at render time, by walking forward through notes in `GrandStaffSceneBuilder`. `<slur>` uses the same `type`/`number` attributes. Unlike `<tie>` (which only ever pairs with the *immediately next same-pitch* note), a slur can span an arbitrary run of *different*-pitch notes, so its start→end matching needs its own walk, not reuse of the tie-continuation search in `ScoreDerivation`/`GrandStaffSceneBuilder` (that search keys on matching pitch).
- **Point-glyph rendering.** `GrandStaffGlyphKind.Fermata` + `GetFermataY` (`PianoMapper.Core/Rendering/GrandStaffLayout.cs`) is a single text glyph pushed a fixed clearance outside the note/stem/beam. The tuplet-support plan added a second instance of the identical pattern (`GrandStaffGlyphKind.Tuplet`, `AddTupletGlyph` in `GrandStaffSceneBuilder.cs`). This is the direct template for **articulations**, **ornaments**, and **accidental-mark** — all point-glyphs above/below a single note.
- **Curve rendering exists but is tuned for a different shape.** `GrandStaffTie` (`X0,Y0,X1,Y1,CurveDirection,IsActive`) is rendered by `drawTie` in `PianoMapper.Web/wwwroot/js/canvas.js` (~line 847) as a short, *tapered, filled* bezier between two adjacent noteheads (min/max height clamped to a couple of staff spaces, sized for a 1-note gap). A slur typically spans many notes and reads as a single *thin stroked* arc above/below the whole phrase, not a tapered tie shape — reusing `drawTie`'s exact geometry for a multi-note slur span would look wrong (over-thick, over-tall). Plan for a new `GrandStaffSlur` record + `drawSlur` function with the same X0/Y0/X1/Y1/direction shape but its own (simpler, thin-stroke) curve math, rather than reusing `drawTie`.
- **Glissando/slide are a third, simpler shape.** Conceptually a straight (glissando: often wavy in print, but a straight line is the common simplified rendering) line directly between two specific noteheads — closer to a beam or ledger line (`drawLine`-style primitive) than to a curve. Likely its own small `GrandStaffGlissandoLine` record + straight-line draw, reusing the existing `GrandStaffLine`/`drawLine` machinery if the kind enum can express it, rather than inventing new bezier math.
- **No existing precedent at all: arpeggiate/non-arpeggiate.** A vertical wavy line (arpeggiate) or bracket (non-arpeggiate) to the left of a *simultaneous* chord. `BuildChordGroups`/`ApplyChordLayout` (`GrandStaffSceneBuilder.cs`) already group same-onset, same-staff notes for notehead-displacement purposes — the same grouping is the right anchor for "is this note part of a chord that has an arpeggiate mark," but the vertical-wavy-line geometry itself is new and has no reusable primitive yet.
- **Desktop renderer stays out of scope, by settled precedent, not as an open question.** `PianoMapper/Rendering/StaffRenderer.cs` (the OpenTK desktop client) does not draw ties, fermata, fingering, or the tuplet numeral today — it only draws what `ScoreNoteLayout`/`GrandStaffLayout` resolve generically (notehead, stem, accidental, barlines, cursor, stem direction). Every rich notation feature to date has shipped web-only (`PianoMapper.Web`). This plan does the same; `StaffRenderer.cs` is not touched.
- **Persistence needs new fields, same trick as before.** `ScoreDocumentSerializer.cs`'s private document records will need a field per new `ScoreNote` property. Old saved scores without them must still deserialize — the existing precedent (`NoteValueDocument`'s `TupletActualNotes = 1`/`TupletNormalNotes = 1` C# default parameter values, which `System.Text.Json`'s parameterized-record binding honors for a missing JSON property) is the template; verify it the same way (a literal legacy-JSON test), don't just assume it.
- **Test conventions**, same as `tuplet-support`: `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`'s `ReadNotes(notesXml)` helper (divisions=2, wraps a bare `<note>...</note>` snippet) for reader unit tests; `PianoMapper.Tests/UnitTests/Fixtures/*.musicxml` file fixtures for whole-score round-trip tests; `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`'s `ScoreWithNotes`/`SingleNoteScore` helpers for renderer tests; `PianoMapper.Tests/JavaScript/canvas.test.mjs` and `scene-contract.test.mjs` for the JS side and its ordinal-pinning tests (`GrandStaffSceneContractTests.cs` mirrors `Clef`/`Accidental` ordinals only — a new `GrandStaffGlyphKind`/line-kind value appended at the *end* does not need a new pinning test unless canvas.js special-cases its ordinal, exactly as `Tuplet` didn't).

### MusicXML subtype scope (why not "everything")

`<articulations>` and `<ornaments>` are themselves containers with many possible children (articulations: `staccato`, `tenuto`, `accent`, `staccatissimo`, `strong-accent`, `spiccato`, `detached-legato`, `scoop`, `plop`, `doit`, `falloff`, `breath-mark`, `caesura`, `stress`, `unstress`, and more; ornaments: `trill-mark`, `turn`/`delayed-turn`/`inverted-turn`/`vertical-turn`, `shake`, `wavy-line`, `mordent`/`inverted-mordent`, `schleifer`, `tremolo`, `haydn`, `other-ornament`). Full coverage of every subtype is not proposed here — see Assumptions for the v1 subset and Out of Scope for the rest, mirroring how `tuplet-support` scoped triplets (3:2) first instead of general n:m rendering polish.

## Restated Request

Give each of `articulations` (a defined common subset), `slur`, `arpeggiate`/`non-arpeggiate`, `glissando`/`slide`, `ornaments` (a defined common subset), and `accidental-mark` a `ScoreNote` field, MusicXML parsing, saved-score persistence, and a real visual on the web grand staff. Make an explicit, documented decision on `other-notation` (default: stays unrendered, since it's an arbitrary vendor escape hatch with no fixed visual meaning) instead of leaving it an open question.

## Acceptance Criteria

- A MusicXML note carrying a supported articulation (staccato, tenuto, or accent) imports, round-trips through saved-score persistence, and renders a distinct glyph at the correct side of the note (matching the fermata/tuplet-numeral clearance pattern).
- A MusicXML `<slur type="start">`/`type="stop">` pair spanning two or more notes imports, round-trips, and renders one thin stroked arc from the first note to the last, distinct in appearance from a tie.
- A MusicXML `<arpeggiate>` on a chord (2+ simultaneous notes) imports, round-trips, and renders one vertical wavy mark to the left of the chord; `<non-arpeggiate>` renders a distinguishable bracket variant.
- A MusicXML `<glissando>`/`<slide>` pair spanning two notes imports, round-trips, and renders a line connecting them, visually distinct from both a tie and a slur.
- A MusicXML `<ornaments><trill-mark/></ornaments>` imports, round-trips, and renders a "tr" glyph above the note.
- A MusicXML `<accidental-mark>` imports, round-trips, and renders its accidental glyph near the note it's attached to (distinguishable from the note's own `<accidental>` rendering).
- `<other-notation>` is explicitly, deliberately left unrendered — documented in `CONTEXT.md`/`README.md` as a decision, not silently absent.
- Existing callers constructing `ScoreNote` without any of these new fields continue to compile and behave exactly as before (every new field optional/defaulted).
- Old saved scores (JSON without any of these new document fields) still deserialize correctly — verified by a literal legacy-JSON test per new field, not assumed from the tuplet-support precedent.
- Re-running the full `dotnet test`/`node --test` suite and a Release build stays green throughout.

## Assumptions and Architecture Decisions

- **v1 articulation subset: `staccato`, `tenuto`, `accent` only** (the three most pedagogically common for a beginner piano-learning app, and — not coincidentally — the three Audiveris actually hallucinated on the `gama-C-major.jpg` test image, so real fixture data already exists in this session's history). A `<staccatissimo>`, `<strong-accent>`, or any other articulation child throws a readable "unsupported articulation" error at import, the same way an unsupported `<stem>` value does today — don't silently drop *unrecognized* articulations once articulations are otherwise supported, or a real notation difference goes invisible without any signal. Add more subtypes in a later pass if needed.
- **v1 ornament subset: `trill-mark` only**, same reasoning and same "throw for unrecognized ornament, don't silently drop" rule.
- **`ScoreNote` gets one new field per concept**, following the `Fermata`/`Fingering` precedent (nullable value/enum, appended at the end of the positional record so existing constructors keep compiling): `Articulation` (nullable enum: Staccato/Tenuto/Accent), `AccidentalMark` (nullable `ScoreAccidental`, reusing the existing enum since an accidental-mark is spelled identically to a regular accidental), `Ornament` (nullable enum, just `TrillMark` for v1 — modeled as an enum rather than a bool so it can grow without another boolean field later), `Arpeggio` (nullable enum: Arpeggiate/NonArpeggiate), and boolean pairing flags mirroring `TiesToNext`: `SlurStartsHere`/`SlurEndsHere`... **decide during Task 2** whether slur (and glissando/slide) need a *pair id* (MusicXML's `number` attribute) rather than a bare boolean, since — unlike ties — a staff can have multiple simultaneously-open slurs (nested or overlapping phrase marks), and a bare boolean can't disambiguate which start matches which end. Recommendation: carry the MusicXML `number` attribute through as a small int on the new field(s) (e.g. `ScoreSlur(bool IsStart, int Number)`), and match by number at render time, not by simple sequential "next note that ends" scanning.
- **Rendering additions are new `GrandStaffGlyphKind`/record types, appended at the end of existing enums** — matching exactly how `Tuplet` was added to `GrandStaffGlyphKind` in `tuplet-support` (preserves `Clef`=0/`Accidental`=1 pinning, no `canvas.js` ordinal constant needed unless JS special-cases the new kind by number, which none of these need to).
- **Glissando vs. slide render identically for v1** (a straight line between two noteheads) — MusicXML distinguishes them semantically (glissando = discrete pitch slide, slide = continuous) but a beginner-learning grand staff doesn't need visually distinct treatments yet; give them the same glyph/line with a code comment noting the simplification, not two divergent render paths.
- **`other-notation` is parsed for validity (so a plainly malformed one still errors) but never rendered.** It's MusicXML's arbitrary vendor-extension escape hatch (`type` attribute + free text, no fixed visual meaning) — inventing a generic visual for unknown vendor content would be guessing, not implementing a notation. State this plainly in `CONTEXT.md`.

## Phase 1: Point-Glyph Notations (Articulation, Ornament, Accidental-Mark)

Grouped first because they reuse the fermata/tuplet-numeral point-glyph pattern exactly, with no new rendering primitive needed — lowest risk, fastest to prove the parse→persist→render pipeline end to end before tackling the harder curve/line/chord shapes.

### Task 1: Domain fields for articulation, ornament, and accidental-mark

**Description:** Add `ScoreArticulation` (Staccato/Tenuto/Accent) and `ScoreOrnament` (TrillMark) enums, reuse `ScoreAccidental` for accidental-mark, and add three new nullable trailing fields to `ScoreNote`.

**Acceptance criteria:**

- [x] `ScoreNote.Articulation`, `.Ornament`, `.AccidentalMark` are nullable and default to `null`.
- [x] Existing `ScoreNote` construction and `with` expressions compile unchanged.

**Verification:**

- [x] Build succeeds: `rtk dotnet build PianoMapper.Core/PianoMapper.Core.csproj`

**Dependencies:** None

**Files likely touched:**

- `PianoMapper.Core/Music/ScoreArticulation.cs` (new)
- `PianoMapper.Core/Music/ScoreOrnament.cs` (new)
- `PianoMapper.Core/Music/ScoreNote.cs`

**Estimated scope:** Small

### Task 2: Parse articulation, ornament, and accidental-mark

**Description:** Write failing reader tests first, then add `ParseArticulation`/`ParseOrnament`/`ParseAccidentalMark` following the `ParseFermata` template exactly (find under `<notations>`, reject more than one, map value to enum, throw a readable "unsupported articulation/ornament" error for a recognized-but-out-of-v1-scope subtype rather than silently dropping it).

**Acceptance criteria:**

- [x] `<notations><articulations><staccato/></articulations></notations>` (and tenuto, accent) import to the matching enum value.
- [x] `<notations><articulations><staccatissimo/></articulations></notations>` throws a readable error naming `<staccatissimo>` (not silently ignored — see Assumptions).
- [x] `<notations><ornaments><trill-mark/></ornaments></notations>` imports to `ScoreOrnament.TrillMark`; any other ornament child throws a readable error.
- [x] `<notations><accidental-mark>sharp</accidental-mark></notations>` imports to `ScoreAccidental.Sharp`; reuses the same value-mapping switch as the existing `ParseAccidental` (factor out the shared switch rather than duplicating it).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`

**Estimated scope:** Medium

### Task 3: Persist articulation, ornament, and accidental-mark

**Description:** Extend `ScoreNoteDocument` with the three new nullable fields, thread through `ToDocument`/`FromDocument`, verify legacy JSON (missing these fields) still deserializes.

**Acceptance criteria:**

- [x] Round-trip test: a score with all three set preserves them through serialize→deserialize.
- [x] Legacy-JSON test: a literal JSON document without these fields deserializes with all three `null` (extend the existing `Deserialize_VersionOneWithoutNotationFields_DefaultsToAbsent` test).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreDocumentSerializerTests"`

**Dependencies:** Task 1

**Files likely touched:**

- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`
- `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs`

**Estimated scope:** Small

### Task 4: Render articulation, ornament, and accidental-mark as point glyphs

**Description:** Add `GrandStaffGlyphKind.Articulation`/`.Ornament`/`.AccidentalMark` (appended at the end, after `Tuplet`) and build each glyph in `GrandStaffSceneBuilder`'s per-note loop (`BuildStaticScoreParts`, alongside the existing fermata glyph block) using the same `GetFermataY`-style clearance-outside-the-note placement. Distinct glyph text per articulation (e.g. a filled dot for staccato, a short horizontal dash for tenuto, a `>` wedge for accent — confirm against a real screenshot, this is a visual-design detail the plan shouldn't over-specify) and `"tr"` for trill-mark.

**Acceptance criteria:**

- [x] A note with each supported articulation/ornament/accidental-mark renders exactly one matching glyph, positioned outside the note (not overlapping the notehead/stem/beam).
- [x] A note without any of these renders none of the new glyphs (no regression to existing fermata/accidental glyph placement).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] Manual screenshot check via headless Firefox (this session's working WebDriver BiDi recipe) against a small hand-authored fixture score covering all three. Done as part of the combined visual pass — see Final Checkpoint (found and fixed a real glyph-sizing bug in the process). (deferred to one combined visual pass at the end of all phases — see Final Checkpoint)

**Dependencies:** Tasks 1–2

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffGlyphKind.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Medium

## Checkpoint: Point Glyphs Complete

- [x] All three point-glyph notations import, persist, and render correctly, with the "unrecognized subtype throws" rule proven by test.
- [x] Full fast suite passes: `rtk dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"`
- [x] Manual screenshot reviewed before moving to the harder shapes. Done as part of the combined visual pass — see Final Checkpoint.

## Phase 2: Slur (Curve Spanning a Note Group)

### Task 5: Domain field and parsing for slur pairing

**Description:** Decide and implement the pairing representation (see Assumptions — likely `ScoreSlur(bool IsStart, int Number)` or two fields `SlurStartNumber`/`SlurEndNumber`), parse `<notations><slur type="start"/"stop" number="N">`, validate `type` values the same way `<tie>` does (readable error for anything but start/stop).

**Acceptance criteria:**

- [x] A note with `<slur type="start" number="1">` and a later note with `<slur type="stop" number="1">` both import with matching pairing data.
- [x] Two independent, overlapping slurs (different `number` values) on the same staff import without cross-pairing incorrectly.
- [x] An invalid `type` value throws a readable error naming `<slur>`.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests"`

**Dependencies:** None (independent of Phase 1)

**Files likely touched:**

- `PianoMapper.Core/Music/ScoreNote.cs`
- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`

**Estimated scope:** Medium

### Task 6: Persist slur pairing

**Description:** Same shape as Task 3, for the new slur field(s).

**Acceptance criteria:**

- [x] Round-trip and legacy-JSON tests, matching Task 3's pattern.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~ScoreDocumentSerializerTests"`

**Dependencies:** Task 5

**Files likely touched:**

- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`
- `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs`

**Estimated scope:** Small

### Task 7: Render slur as a thin stroked arc

**Description:** Add `GrandStaffSlur` (X0/Y0/X1/Y1/direction, mirroring `GrandStaffTie`'s shape) built in `GrandStaffSceneBuilder` by matching slur-start notes to slur-end notes by `number` within the same staff (do not reuse the tie-continuation pitch-matching search — a slur spans different pitches). Add `drawSlur` in `canvas.js`: a single thin stroked bezier (not `drawTie`'s tapered fill), positioned above/below the note group by the same up/down convention as ties and fermata.

**Acceptance criteria:**

- [x] A slur spanning 3+ notes of different pitches renders one continuous arc from the first to the last note.
- [x] The slur arc is visually distinguishable from a tie between the same two notes (thin stroke vs. tapered fill) at the code/geometry level (`strokedPathCalls` vs `filledPathCalls` in canvas.test.mjs) — real screenshot confirmation deferred to the combined visual pass (see Final Checkpoint).
- [~] A note with both a tie and a slur: **discovered during implementation that the printed grand-staff view does not currently draw a tie curve at all** — `GrandStaffTie`/`drawTie` are only populated by the live piano-roll `Build()` path (`PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`'s `BuildStaticScoreParts`/`ComposeScore`, i.e. the printed-score path, never sets `Ties`). Rendering ties in the printed score is a pre-existing gap outside this plan's stated scope (tie isn't one of the eight `<notations>` children this plan targets). Verified instead that slur rendering is unaffected by a note also carrying `TiesToNext` data (`BuildScore_NoteWithTieAndSlur_StillRendersSlurCorrectly`).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] `node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`
- [x] Manual screenshot check. Done as part of the combined visual pass — see Final Checkpoint.

**Dependencies:** Tasks 5–6

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSlur.cs` (new)
- `PianoMapper.Web/Rendering/GrandStaffScene.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Estimated scope:** Large — **break into sub-tasks during implementation** if the slur-matching + new curve-drawing work doesn't fit one focused session (e.g. split "matching logic + GrandStaffSlur data" from "canvas.js drawSlur implementation").

## Checkpoint: Slur Complete

- [x] Slur imports, persists, and renders distinctly from a tie.
- [x] Full fast suite + JS suite pass.

## Phase 3: Arpeggiate / Non-Arpeggiate (Chord Marking)

### Task 8: Domain, parsing, and persistence for arpeggio marks

**Description:** Add `ScoreArpeggio` (Arpeggiate/NonArpeggiate) nullable field to `ScoreNote`, parse `<notations><arpeggiate/>` and `<non-arpeggiate type="...">`, persist it. Combine into one task (unlike Phase 1/2's split) since this is a single simple enum with no pairing complexity.

**Acceptance criteria:**

- [x] Both variants import, persist (round-trip + legacy-JSON), and are distinguishable in the domain model.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests|FullyQualifiedName~ScoreDocumentSerializerTests"`

**Dependencies:** None (independent of Phases 1–2)

**Files likely touched:**

- `PianoMapper.Core/Music/ScoreArpeggio.cs` (new)
- `PianoMapper.Core/Music/ScoreNote.cs`
- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`
- Corresponding test files

**Estimated scope:** Medium

### Task 9: Render arpeggio mark as a vertical wavy line/bracket

**Description:** New geometry — no existing precedent. Using the same same-onset chord grouping `BuildChordGroups` already computes, add a vertical mark to the left of the lowest notehead in a chord carrying an arpeggio mark: a wavy line for `Arpeggiate`, a bracket for `NonArpeggiate`. This needs its own scene record (e.g. `GrandStaffArpeggioMark(X, Y0, Y1, IsNonArpeggiate)`) and a new `canvas.js` draw function — there's nothing to adapt from ties/fermata/beams for this shape.

**Acceptance criteria:**

- [x] A chord (2+ simultaneous notes, at least one carrying an arpeggio mark) renders one mark spanning the full chord's vertical extent, to the left of the noteheads, not overlapping them.
- [x] Arpeggiate and non-arpeggiate are visually distinguishable from each other.
- [x] A chord without an arpeggio mark, and a single non-chord note carrying one (which the MusicXML spec allows but is musically meaningless — decide: render nothing, or throw at import as an unsupported case? **Open question, resolve during this task.**), don't crash or render nonsense. **Resolved: render nothing.** It's valid, already-imported data (the MusicXML schema allows it); inventing a new import-time error for a case that isn't actually wrong would be manufacturing an error, not implementing a notation. `BuildArpeggioMarks` simply skips any chord group of size < 2, which a non-chord note always is.

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] `node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`
- [x] Manual screenshot check. Done as part of the combined visual pass — see Final Checkpoint.

**Dependencies:** Task 8

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffArpeggioMark.cs` (new)
- `PianoMapper.Web/Rendering/GrandStaffScene.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- Corresponding test files

**Estimated scope:** Large — likely needs sub-tasking, same reasoning as Task 7.

## Checkpoint: Arpeggio Marks Complete

- [x] Both variants import, persist, and render correctly on real chords.
- [x] Full fast suite + JS suite pass.

## Phase 4: Glissando / Slide (Note-to-Note Line)

### Task 10: Domain, parsing, and persistence for glissando/slide pairing

**Description:** Same pairing shape as slur (Task 5) — `type="start"/"stop"` with a `number` — but a separate field/enum distinguishing glissando from slide (even though v1 renders them identically, per Assumptions, so the domain model doesn't silently conflate two different MusicXML concepts).

**Acceptance criteria:**

- [x] Both `<glissando>` and `<slide>` start/stop pairs import and persist with correct pairing and correct kind (glissando vs. slide).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~MusicXmlScoreReaderTests|FullyQualifiedName~ScoreDocumentSerializerTests"`

**Dependencies:** None (independent; can reuse whatever pairing representation Task 5 settles on)

**Files likely touched:**

- `PianoMapper.Core/Music/ScoreGlissando.cs` (new)
- `PianoMapper.Core/Music/ScoreNote.cs`
- `PianoMapper.Core/Music/MusicXmlScoreReader.cs`
- `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`
- Corresponding test files

**Estimated scope:** Medium

### Task 11: Render glissando/slide as a straight line

**Description:** Match start/stop notes by number (same approach as Task 7's slur matching), draw a straight line directly between the two noteheads — closer to a barline/ledger-line primitive than a curve. Check whether the existing `GrandStaffLine`/`GrandStaffLineKind`/`drawLine` machinery can express this directly (a new `GrandStaffLineKind` value) before inventing a new record type.

**Acceptance criteria:**

- [x] A glissando/slide pair renders one line directly connecting the two noteheads.
- [x] Visually distinguishable from a tie/slur (straight vs. curved) — also given its own distinct stroke color/width in `drawLine` (`glissandoLineColor`), pinned via a new `GrandStaffLineKind.Glissando` = 5 ordinal in both scene-contract test files since canvas.js does special-case it by ordinal (matching the existing precedent for every other `GrandStaffLineKind` value).

**Verification:**

- [x] Focused tests pass: `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "FullyQualifiedName~GrandStaffSceneBuilderTests"`
- [x] `node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`
- [x] Manual screenshot check. Done as part of the combined visual pass — see Final Checkpoint.

**Dependencies:** Task 10

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Web/wwwroot/js/canvas.js`
- Corresponding test files

**Estimated scope:** Medium

## Checkpoint: Glissando/Slide Complete

- [x] Both import, persist, and render as a straight connecting line.
- [x] Full fast suite + JS suite pass.

## Phase 5: Documentation and `other-notation` Decision

### Task 12: Document scope and the `other-notation` decision

**Description:** Update `CONTEXT.md`'s domain glossary (new `ScoreNote` fields) and `README.md`'s MusicXML-import capability list (name the supported articulation/ornament subset explicitly, state that `other-notation` is accepted but never rendered and why).

**Acceptance criteria:**

- [x] `CONTEXT.md` and `README.md` accurately describe exactly what renders and what's explicitly out of scope (see below).

**Verification:**

- [x] Manual read-through diff against this plan's Out of Scope list.

**Dependencies:** Tasks 1–11

**Files likely touched:**

- `CONTEXT.md`
- `README.md`

**Estimated scope:** Small

## Final Checkpoint

- [x] Every acceptance criterion above is satisfied with recorded command output (see each task's Acceptance Criteria checkboxes above).
- [x] Fast suite passes: `rtk dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"` (696 tests passed).
- [x] Full JS suite passes: `node --test PianoMapper.Tests/JavaScript/*.test.mjs` (55 tests passed).
- [x] Release build succeeds: `rtk dotnet build PianoMapper.slnx --configuration Release`.
- [x] `rtk git diff --check` reports no whitespace errors.
- [x] Manual screenshot check (the deferred combined visual pass from every phase above): ran the standalone Blazor app (`dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj`), imported a hand-authored MusicXML fixture covering all six notation types plus fermata, and screenshotted the rendered grand staff via headless Firefox WebDriver BiDi. **Found and fixed a real bug this way**: the tenuto articulation glyph ("–", an en dash) has a near-zero `actualBoundingBoxAscent`/`Descent` in `canvas.js`'s `measureText`-based dynamic glyph sizing, which blew its computed font size up ~15x and rendered as a giant white blob overlapping the neighboring staccato/accent marks. Fixed with a floor on the measured-height divisor in `drawGlyph` (`PianoMapper.Web/wwwroot/js/canvas.js`) — a defensive clamp that protects any future flat/dash-shaped glyph, not just this one — plus swapping the staccato glyph from "•" to the better-behaved "●". Added a JS regression test (`grand staff clamps a flat glyph's computed font size instead of blowing it up`) after extending `FakeCanvasContext.measureText` to support per-character overrides. Re-verified with a second screenshot after the fix: staccato dot, tenuto dash, accent wedge, fermata, trill "tr", accidental-mark "♯", slur arc, arpeggiate wave, non-arpeggiate bracket, and the glissando line all render correctly, distinctly, and without overlap.
- [x] Ready for review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| This is six loosely-related features bundled into one plan; scope creep is easy. | A half-finished plan is worse than several small complete ones. | Phases are independently checkpointed and largely independent of each other (Phase 2/3/4 don't depend on each other) — implement and ship phases separately if that's more tractable; don't treat "Final Checkpoint" as all-or-nothing. |
| Slur and glissando/slide pairing by simple sequential "next note with matching state" (the tie approach) breaks for overlapping/nested marks. | Wrong notes get connected, producing a visibly incorrect render that's easy to miss in a unit test with only one slur in the fixture. | Carry the MusicXML `number` attribute through explicitly (see Assumptions) and write a test with two *overlapping* slurs/glissandi specifically, not just one at a time. |
| New curve (slur) and new geometry (arpeggio mark) drawing code in `canvas.js` has no existing primitive to adapt, unlike the point-glyph phase. | Highest risk of the whole plan for going over-scope or under-testing, since there's no template to constrain the implementation the way `GetFermataY` constrained the tuplet numeral. | Keep v1 renders deliberately minimal (thin stroke, not full engraving fidelity) and screenshot-verify before considering the render task done — this is exactly the class of change `.agents/lessons.md` already warns about getting wrong without a real render to check against. |
| Six new `ScoreNote` fields is a lot of positional-record growth. | Equality/hashing semantics shift again (as already flagged and accepted in the `tuplet-support`/stem-direction precedent), and the record gets harder to read. | Consider whether some of these (arpeggio, glissando kind, ornament) belong in a small nested notation record instead of flat fields — evaluate this during Task 1/5/8/10, don't default to "just keep appending" without a look. |
| `other-notation`'s "never render" decision could be wrong for some real, meaningful vendor content (e.g. pedal marks, which `other-notation type="single">pedal mark</other-notation>` — this exact test string — hints at). | A real pedal marking silently has no visual. | Named explicitly as a decision, not a gap, in Task 12's docs update; revisit as a separate, focused plan if pedal-mark support specifically turns out to matter (it's a distinct, well-defined MusicXML concept from generic `other-notation`, better scoped on its own). |

## Out of Scope

- Every articulation/ornament subtype beyond the v1 set (staccato/tenuto/accent; trill-mark) — see Assumptions for the "throw, don't silently drop" rule for the rest.
- `other-notation` rendering of any kind.
- The desktop OpenTK client (`PianoMapper/Rendering/StaffRenderer.cs`) — web-only, by established precedent.
- Full engraving-fidelity curve/line rendering (e.g. genuinely wavy glissando lines, slur thickness/shape variation by span length) — v1 is a minimal, readable indicator, not a music-engraving-software-grade render.
- Nested/nested-and-nested slurs beyond two independent overlapping ones (the acceptance criterion in Task 5/7 tests two, not three+).
- Any change to Audiveris/OMR recognition, or further chasing of `gama-C-major.jpg`'s remaining `<backup>` timing error — that's a separate, already-flagged issue, unrelated to whether these notation types render once they do successfully import.

## Open Questions

- Should a non-chord note carrying an arpeggio mark (musically meaningless, but not disallowed by the MusicXML schema) render nothing or throw at import? Flagged inline in Task 9 as something to resolve during that task, not upfront.
- Should some of the six new `ScoreNote` fields be grouped into a nested notation record rather than added as flat fields? Flagged in the Risks table; worth a decision before Task 1 lands, since retrofitting it later touches every downstream task.
- Is bundling six features into one plan document the right granularity, or should Phases 2–4 (slur, arpeggio marks, glissando/slide — the three with no existing rendering precedent and the highest individual risk) become their own separate plans once Phase 1 (the low-risk point-glyph batch) ships? Recommend deciding this after Phase 1's checkpoint, with real data on how long that phase actually took.
