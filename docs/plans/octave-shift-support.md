# Implementation Plan: Native Octave-Shift (8va/8vb) Support

**Date:** 2026-09-24

**Status:** Complete

## Goal

Parse MusicXML's `<octave-shift>` direction (ottava: 8va/8vb/15ma/15mb/22a/22b) and render it correctly on the imported-score grand staff — notes stay at their compact, ledger-line-free *notated* position with a dashed bracket and numeral, while everything that isn't rendering (grading, audio, tie-matching, auto-fingering) sees the *sounding* pitch — instead of requiring pre-shifted, ledger-line-heavy note data as the workaround does today.

## Context

### Why this is needed

While debugging a real OMR import (`gama-C-major.jpg`, a scanned piano scale exercise) in this session, Audiveris misread a passage's octave because the source page uses standard ottava notation (a dashed "8" bracket meaning "play an octave higher than written"). Audiveris did not recognize the bracket as a structural element at all — zero `<direction>` elements appeared in that measure's raw output — it just silently misjudged notehead position. That specific import stays hand-corrected in the dev database as a one-off fix; `docs/plans/tuplet-support.md`'s Out of Scope section already stated the same thing for the same image ("The ottava (8va) sign not being recognized... [is an] OMR-quality issue upstream of this app"). This plan is entirely about the reader/renderer's own capability, not about fixing that import or Audiveris.

### Verified against the MusicXML 4.1 primary source (not just the request's paraphrase)

Fetched directly from `musicxml.formats.music` (the current home of the W3C MusicXML reference after a redirect chain from both URLs in the request):

> "The `<octave-shift>` element indicates where notes are shifted up or down from their performed values because of printing difficulty. Thus a treble clef line noted with 8va will be indicated with an `<octave-shift>` down from the pitch data indicated in the notes."
>
> `type` (up-down-stop-continue): "Indicates if this is the start, stop, or continuation of the octave shift. The start is specified as a shift up or down from their performed values."
>
> `size`: "8 indicates one octave; 15 indicates two octaves; 22 indicates 3 octaves. The default value is 8."
>
> `number` (number-level): "Distinguishes multiple octave shifts when they overlap in MusicXML document order."

This **confirms the request's reading, not the inverted one**: `type="down"` is the 8va-alta case (bracket above the staff, sounds *higher* than written — "a treble clef line noted with 8va... down"); `type="up"` is the 8va-bassa case (bracket below the staff, sounds *lower* than written). The name describes the direction the *notated* pitch sits relative to the *sounding* pitch, not the direction the sound moves. Getting this backwards would silently invert every rendered ottava, so Task 3 below requires **positive test coverage of both directions**, not just the 8va case.

`<octave-shift>` lives in `<direction><direction-type>`, a sibling of `<note>` elements in `<measure>` — **not** a `<notations>` child of a specific note, unlike every other per-note mark this reader already supports (`ParseSlur`, `ParseGlissando`, `ParseFermata`, etc.). It marks a *span*, matched start-to-stop by `number` (same `number-level` datatype and same start/stop-by-number matching convention as `ScoreSlur`/`ScoreGlissando`), but its `type` vocabulary is `up`/`down`/`stop`/`continue` — **not** `start`/`stop`/`continue` like slur/tie/glissando. `up`/`down` each mean "start, in this direction" simultaneously, so `MusicXmlScoreReader.ParsePairingType` (which only recognizes literal `"start"`/`"stop"`) is **not** directly reusable for octave-shift's `type` attribute — a new switch is needed. `ParsePairingNumber` (default-1 `number` parsing) **is** directly reusable as-is; the `number` attribute means the same thing here.

### Confirmed: what happens to `<octave-shift>` today

`MusicXmlScoreReader.cs`'s `"direction"` case only calls `ParseTempo`, which scans `direction.Elements()` and treats `"direction-type"` as an ignorable element name (`IgnoredDirectionElements`, line ~74-81) — it `continue`s straight past it **without ever looking inside**. So a `<direction><direction-type><octave-shift .../></direction-type></direction>` parses today with **zero effect and zero error**: the content is invisible, not rejected. This confirms the request's suspicion directly from the source, not by assumption.

### The core design tension, resolved

`ScoreNote.Pitch` is currently the single value used for **everything**: grand-staff vertical position, MIDI number, frequency, and grading. An octave-shifted passage needs the *sounding* pitch for playback/grading/audio, but the *notated* (written) pitch for staff position, so the note renders compactly with a bracket instead of stacked ledger lines.

**Decision: keep `ScoreNote.Pitch` as the sounding pitch everywhere it already is used, and add one new field, `int SoundingOctavesAboveNotated = 0`, that only rendering consults to recover the notated pitch.** No new domain type, no dual-pitch API.

This was checked against **every actual consumer of `ScoreNote.Pitch`** in the codebase (via `codegraph_explore` + a full-repo grep for `.Pitch`), not assumed:

| Consumer | File | Uses sounding pitch correctly with **zero changes**? |
|---|---|---|
| Grading (`WrongPitch` MIDI match) | `PianoMapper.Core/Practice/Grader.cs:105,129` | Yes — the student physically presses the key that *sounds*, which is exactly what grading must match. |
| Tie-continuation matching (`candidate.Pitch == tiedNote.Pitch`) | `PianoMapper.Core/Music/ScoreDerivation.cs:61` | Yes — a real tie never crosses an octave-shift start/stop boundary inconsistently; sounding-pitch equality is exactly right. |
| Note-reading-session pitch matching | `PianoMapper.Core/Practice/NoteReadingSession.cs` (many `Pitch.MidiNumber` uses) | Yes — same reasoning as grading. |
| Audio scheduling (`Pitch.Frequency`) | `PianoMapper.Web/Playback/BrowserScorePlayback.cs`, `BrowserRandomMeasureScheduler.cs`, desktop `Instrument`/`AudioDispatcher` | Yes — the frequency that should actually sound. |
| Auto-fingering generator (`Pitch.MidiNumber` grouping/ordering) | `PianoMapper.Core/Music/ScoreFingeringGenerator.cs:152,164` | Yes — fingering describes which physical finger plays which physical key = sounding pitch. |
| Note-name label text (`note.Pitch.ToString()`) | `GrandStaffSceneBuilder.cs:250` (Score path) | Yes, and for free — the label already shows what the student should actually play, consistent with what grading rewards. |
| Accidental glyph choice (`GetAccidentalGlyph`/`GetScoreAccidentalGlyph`) | `GrandStaffSceneBuilder.cs:954,283,1691-1703` | Yes, unaffected either way — both only read `Pitch.Letter`/`Pitch.Alter`, never `Octave`. |
| Chord-second collision check (`Pitch.DiatonicIndex` difference) | `GrandStaffSceneBuilder.cs:827` | Yes, unaffected either way — a uniform octave shift adds a constant to both compared notes' `DiatonicIndex`, so their *difference* (what's tested) doesn't change. |
| Label-row ordering for simultaneous notes (`OrderByDescending(Pitch.MidiNumber)`) | `GrandStaffLayout.cs:426` | Yes, unaffected either way — same "uniform shift preserves relative order" reasoning. |

Three places, and **only** three, need the *notated* pitch instead — all three are the Web **Score** path, none is the Live/free-play path:

1. `GrandStaffLayout.GetScoreNoteLayout` (`GrandStaffLayout.cs:189`) — `GetPosition(note.Pitch, note.Staff)`, the actual staff-line/ledger-line vertical placement.
2. `GrandStaffSceneBuilder.BuildStaticScoreParts`, twice (`GrandStaffSceneBuilder.cs:156,162`) — `GrandStaffLayout.GetLivePosition(candidate.Pitch)` / `(note.Pitch)`, a **pitch-based treble/bass auto-selection heuristic** (the fix behind lessons.md's "choose treble or bass notation from pitch, preserve the original hand only for R/L fingering" entry). This is easy to miss because it calls a function named `GetLivePosition` from *Score*-path code — it's reused there purely as a "which staff does this pitch naturally belong to" rule, not because this is the Live path.

All three should derive from one new small helper, `GrandStaffLayout.GetNotatedPitch(ScoreNote note)`, rather than duplicating the "subtract `SoundingOctavesAboveNotated` octaves" arithmetic three times. `Pitch` has no `init`/settable properties (its constructor is the only way to build one — `pitch with { Octave = ... }` does **not** compile), so the helper must call `new Pitch(note.Pitch.Letter, note.Pitch.Alter, note.Pitch.Octave - note.SoundingOctavesAboveNotated)`.

### Confirmed scoping: Score path only, and a nuance the prior two "desktop vs. web" plans didn't have

Per the request and matching `docs/plans/tuplet-support.md`/`docs/plans/configurable-measures-per-page.md`'s identical fork: the **Live/free-play** grand staff (`GrandStaffLayout.GetLivePosition`, `GrandStaffSceneBuilder.Build(IReadOnlyList<PerformedNote>, ...)`) is untouched. A live-played note has no "notated" position — the performer is playing an actual key, which is exactly what `GetLivePosition` already shows.

The **desktop client** (`PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Rendering/StaffRenderer.cs`) gets a genuinely different outcome than in the prior two plans, confirmed by reading it directly: `StaffRenderer.cs:100` calls `GrandStaffLayout.GetScoreNoteLayout(note, ...)` — the same **shared, Core** function this plan fixes. Desktop has no pitch-based staff-auto-selection heuristic of its own (it just uses `note.Staff` as imported), so it needs no separate fix for that. This means: **once `GetScoreNoteLayout` is fixed, the desktop client automatically renders octave-shifted notes at the correct, compact, notated position — for free, with zero desktop-specific code changes.** What desktop will **not** get is the dashed bracket + numeral (new code lives entirely in `GrandStaffSceneBuilder.cs`/`canvas.js`, Web-only, matching the prior plans' precedent for new visual notation). So desktop shows the right notes with no explanation of *why* they're positioned that way — flagged explicitly in Open Questions rather than assumed acceptable.

### Rendering precedent already in this codebase

- **Reuse existing primitives, don't invent a new record type.** The glissando line explicitly documents this reasoning (`GrandStaffSceneBuilder.cs:450-460`): "A straight connecting line is a much simpler shape... reuses the existing `GrandStaffLine`/`drawLine` machinery directly rather than a new record/draw-function pair." The tuplet numeral does the same with `GrandStaffGlyph`/`GrandStaffGlyphKind.Tuplet`. An ottava bracket is a horizontal line (`GrandStaffLine`, new `GrandStaffLineKind.OctaveShift`) plus a numeral (`GrandStaffGlyph`, new `GrandStaffGlyphKind.OctaveShiftNumeral`) — **no new record type is needed**, and `GrandStaffScene`/`GrandStaffStaticScoreParts`/`GrandStaffSceneCache`/the JS scene contract need no new top-level fields at all, only two new (appended, ordinal-safe) enum members.
- **`GrandStaffLineKind`/`GrandStaffGlyphKind` cross the JS interop boundary as ordinals**, pinned by `GrandStaffSceneContractTests.cs`/`scene-contract.test.mjs` (confirmed by the existing comment in `configurable-measures-per-page.md`'s Task 8 about exactly this pinning). New members must be **appended**, never inserted, and the contract test needs a new case.
- **Genuinely new precedent needed: canvas.js has no `setLineDash` usage anywhere today** (confirmed by grep). Real ottava notation is a *dashed* line — rendering it solid (like glissando does, deliberately, since MusicXML's glissando line-type distinction wasn't rendered) would read as a completely different, wrong mark (a phrase line or an underline), not as "play differently." This plan adds the first dashed-line rendering in this codebase — small, contained, but new.
- **Span grouping without cross-measure retroactive mutation.** Slur/glissando pairing works because MusicXML declares both endpoints *on the notes themselves* (`<notations><slur type="start"/>` on note A, `type="stop"` on note B) — the reader needs no bookkeeping; `BuildSlurs`/`BuildGlissandoLines` just scan the fully-parsed note list later. Octave-shift's start/stop live on `<direction>` *siblings*, not on notes, and a real ottava passage can span a measure boundary — by the time a `type="stop"` direction is seen, the last shifted note may already be sealed into a **previous**, already-completed `ScoreMeasure` (`Read()`'s `notes`/`measures` lists are per-measure and immutable once appended). Retroactively editing an already-committed note to attach a start/stop marker is real, avoidable complexity. **Decision: don't do it.** Store only the per-note `SoundingOctavesAboveNotated` value (which the reader *can* assign correctly in a single forward pass, exactly like it already threads `divisions`/`keyFifths`/`timeSignature` state across measures), and let the renderer derive each bracket's on-screen span by grouping **contiguous** same-staff notes (ordered by onset) that share the same nonzero value — precisely mirroring `BuildBeams`/`AddBeam`'s existing contiguous-run grouping over a per-note `BeamState` field. This is simpler, needs no new domain type, and correctly handles a shift spanning a measure boundary with no special-casing. The one thing it gives up: two *immediately adjacent* same-magnitude shifts (a `stop` followed instantly by a new `start` of equal size, no unshifted note between) would render as one continuous bracket instead of two — explicitly listed in Out of Scope; it does not affect the motivating `gama-C-major.jpg`-shaped fixture, and the notes' pitches are still correct either way.

### Persistence precedent (`ScoreDocumentSerializer.cs`)

`Slur`/`Glissando` were both added as **trailing, nullable/optional fields with `= null` defaults** on the private `ScoreNoteDocument` record, so previously-saved scores (with no such field in their JSON) still deserialize. `SoundingOctavesAboveNotated` follows the same shape (`int ... = 0` trailing default) in both `ScoreNoteDocument` and the `ToDocument`/`FromDocument` mapping functions.

### Test-fixture convention

Two conventions coexist: inline heredoc fixtures (`ReadNotes("""...""")`, seen throughout `MusicXmlScoreReaderTests.cs` for small single-note cases and error-path tests) and standalone files under `PianoMapper.Tests/Fixtures/*.musicxml` for larger, multi-element, realistic scenarios (e.g. `eighth-note-triplet.musicxml`, read via the `Fixture(name)` helper). Since the request specifically wants a fixture that "round-trips a real `<octave-shift type="down" size="8">...<octave-shift type="stop" size="8">` pair" spanning several notes — the true 8va case matching `gama-C-major.jpg` — this belongs in the **Fixtures file** convention, not a heredoc. Small error-path/edge-case tests (bad `size`, `type="continue"`, mismatched stop `number`) fit the existing heredoc convention instead.

### A pre-existing constraint this plan does not lift

`MusicXmlScoreReaderTests.cs` already has `Read_UnsupportedScoreSemantics_ThrowsMessageNamingElement("unsupported-direction-offset.musicxml", "<offset>")` — **any** `<direction>` containing an `<offset>` child is rejected today, unconditionally (confirmed: `"offset"` is not in `IgnoredDirectionElements`, so `ParseTempo`'s scan throws `Unsupported("offset")` for it regardless of what else is in the direction). Real-world exporters (MuseScore, Finale) commonly attach `<offset>` to a `<direction>` for fine horizontal placement — including, plausibly, on octave-shift directions. This plan's own fixture is hand-authored without `<offset>` (matching the spec's schema, which doesn't require it) and satisfies its acceptance criteria either way, but a real-world *exported* file with `<offset>` on its octave-shift directions would still fail to import after this plan, unchanged from today. Flagged in Risks and Out of Scope rather than silently left for someone to discover later.

### Attribute strictness: deliberately not following `ParseTempo`'s pattern

`ParseTempo` exhaustively validates every attribute on `<sound>` (`foreach (var attribute in sound.Attributes()) { if (attribute.Name.LocalName != "tempo") throw Unsupported(...); }`) — but this is the *outlier* in this reader, not the norm: `ParseSlur`/`ParseGlissando`/`ParseFermata` etc. all read only the attributes they care about (`type`, `number`) and silently ignore anything else a note's notation carries (e.g. `placement`). `<octave-shift>` should follow the **majority** pattern: read `type`/`size`/`number`, ignore `default-x`/`default-y`/`dash-length`/`space-length`/`color`/`font-*`/`id`/etc. This matters concretely — the MusicXML spec's *own* canonical `<octave-shift>` example includes `default-y`, `dash-length`, and `space-length`; strict rejection (mirroring `ParseTempo`) would make this feature fail on the spec's own example and on every realistic exporter output.

## Restated Request

Parse `<octave-shift>` spans in imported MusicXML and render them on the **web** grand staff's **imported-score** view: notes keep their compact notated staff position (no ledger-line pileup) with a dashed bracket and an "8"/"15"/"22" numeral, while grading, audio, tie-matching, and fingering generation all continue to work against the correct *sounding* pitch with no code changes of their own. Both ottava directions (8va-alta, `type="down"`; 8va-bassa, `type="up"`) must be demonstrably correct, not just one.

## Acceptance Criteria

- A MusicXML file containing `<direction><direction-type><octave-shift type="down" size="8" number="1"/></direction-type></direction>` ... notes ... `<direction><direction-type><octave-shift type="stop" size="8" number="1"/></direction-type></direction>` imports without error; the enclosed notes' `Pitch` is the *sounding* pitch (notated pitch + one octave), and their `SoundingOctavesAboveNotated` is `1`.
- The mirror-image case (`type="up"`, the 8va-bassa/"sounds lower" direction) is separately, explicitly tested and produces `Pitch` one octave *below* notated and `SoundingOctavesAboveNotated` of `-1` — proving the direction mapping is not backwards.
- `size="15"`/`size="22"` map to two/three octaves; an unrecognized `size`, `type="continue"`, a `type="stop"` with no active shift, and a mismatched stop `number` each fail import with a readable error naming `<octave-shift>`, matching this reader's existing error conventions.
- Grading (`Verdict.WrongPitch`/`Correct`), audio playback frequency, and note-reading-session matching all use the note's sounding `Pitch` with **no changes** to `Grader.cs`, `ScoreDerivation.cs`, `NoteReadingSession.cs`, `Instrument.cs`, or any audio-scheduling code — proven by the existing test suites for those passing unmodified plus one new end-to-end assertion that a shifted note grades correctly against the sounding MIDI number.
- On the web imported-score grand staff, a note under an active octave-shift renders at its **notated** staff position (few or no ledger lines) with a dashed horizontal bracket and an "8" numeral above (8va) or below (8va-bassa) the staff, spanning from the first to the last shifted note.
- Saved/reloaded scores (`ScoreDocumentSerializer`) round-trip `SoundingOctavesAboveNotated`; a previously-saved score JSON with no such field still deserializes (defaults to `0`).
- The desktop client renders octave-shifted notes at the correct, compact position (confirmed via the shared `GrandStaffLayout.GetScoreNoteLayout` fix) but does **not** draw the bracket — documented as an explicit, evidence-based scope decision, not silently accepted.
- README.md/CONTEXT.md describe the new supported `<direction>`-level mark, distinct from the existing `<notations>` list.
- Grand-staff spacing/rendering changes are visually verified in real pixels (headless-Firefox screenshot, per this project's established practice) before being considered done — flagged explicitly as **not performable by this planning agent**; see the Verification task and its honest caveat.

## Assumptions and Architecture Decisions

- **One new field, no new domain type.** `ScoreNote` gains `int SoundingOctavesAboveNotated = 0` (sounding octave minus notated octave; `0` = not shifted; `+1`/`+2`/`+3` = 8va/15ma/22a *alta* [sounds higher]; `-1`/`-2`/`-3` = the corresponding *bassa* [sounds lower]). No `ScoreOctaveShift` record, no start/stop boundary marker on notes — see Context for why the (IsStart, Number) precedent used by `ScoreSlur`/`ScoreGlissando` was considered and deliberately not reused here.
- **`ScoreNote.Pitch` stays the sounding pitch everywhere**, matching every existing consumer with zero changes to them (table above). Rendering alone reconstructs the notated pitch via a new `GrandStaffLayout.GetNotatedPitch(ScoreNote note)` helper.
- **Reader tracks one global active-shift state**, not per-staff, mirroring how `divisions`/`keyFifths`/`timeSignature` are already single, part-wide `ref`-threaded state in `MusicXmlScoreReader.Read()` (declared outside the per-measure loop so a shift can span a measure boundary). A second `type="down"`/`type="up"` start while one is already active throws `NotSupportedException` — see Open Questions for whether independent simultaneous per-staff shifts should be supported later.
- **Attribute parsing is lenient** (reads `type`/`size`/`number`, ignores everything else on `<octave-shift>`), matching the slur/glissando/fermata majority precedent, not `ParseTempo`'s stricter `<sound>`-only pattern (see Context).
- **`<offset>` inside `<direction>` stays unsupported**, unchanged from today; not lifted by this plan (see Context and Risks).
- **Bracket vertical clearance is a simple fixed offset** above the staff's top line (8va) or below its bottom line (8va-bassa) — matching `GetFermataY`/`GetPointGlyphY`'s single-clearance-constant pattern, not `GetNotationBottomY`'s full note/ledger/stem scan. Justified because the entire purpose of octave-shift notation is keeping notes *close* to the staff (that's why a composer/engraver uses it instead of ledger lines), so a fixed clearance should normally suffice; flagged as an Open Question pending the visual check.
- **Desktop gets correct note positions for free, no bracket.** See Context. Not re-litigated as a full exclusion like the prior two plans' identical desktop question — the nuance (partial, not total, benefit) is new here and called out explicitly in Open Questions.

## Phase 1: Domain model

### Task 1: Add `SoundingOctavesAboveNotated` to `ScoreNote`

**Description:** Add `int SoundingOctavesAboveNotated = 0` as a new trailing optional parameter on the `ScoreNote` record (`PianoMapper.Core/Music/ScoreNote.cs`), after `Glissando`. Update the `ScoreNote` row in `CONTEXT.md`'s domain glossary to mention octave-shift (a sounding-vs-notated pitch offset), matching how the row already lists every other optional per-note mark.

**Acceptance criteria:**
- [x] `ScoreNote` compiles with the new field; every existing call site that doesn't pass it keeps compiling (trailing optional parameter, matching every other optional mark already on this record).
- [x] A direct-construction unit test proves the field defaults to `0` and round-trips through record equality/`with`.
- [x] `CONTEXT.md`'s `ScoreNote` row mentions the new field.

**Files likely touched:** `PianoMapper.Core/Music/ScoreNote.cs`, `CONTEXT.md`, `PianoMapper.Tests/UnitTests/MusicalValueTypesTests.cs` (or wherever bare `ScoreNote` construction is already tested).

**Estimated scope:** Small (1-2 files)

### Checkpoint: Phase 1
- [x] `dotnet build PianoMapper.slnx` succeeds.
- [x] Existing fast test suite still passes (no behavior changed yet).

## Phase 2: MusicXML reader

### Task 2: Parse `<octave-shift>` start/stop and thread active-shift state

**Description:** In `MusicXmlScoreReader.cs`: add `OctaveShiftElementName = "octave-shift"` and `DirectionTypeElementName = "direction-type"` constants. Declare `int activeOctaveShiftOctaves = 0;` and `int activeOctaveShiftNumber = 1;` alongside `divisions`/`keyFifths`/`timeSignature` (outside the per-measure loop, so a shift can span a measure boundary). In the `"direction"` case (alongside the existing `ParseTempo` call, not replacing it — a `<direction>` won't realistically contain both `<sound>` and `<octave-shift>`, but nothing about this design requires it not to), add a call to a new `ParseOctaveShiftDirective(XElement direction)` that:
- Scans `direction.Elements()` for `direction-type` children (any name not `direction-type` here is left to `ParseTempo`'s existing handling, unaffected), and within each, for `octave-shift` children.
- Returns `null` if none found; throws `NotSupportedException` if more than one is found across all `direction-type` children ("exactly one octave-shift per direction is required", matching this reader's existing "exactly one X" precedent).
- If found, parses `type` via a **new** switch (not `ParsePairingType`, whose vocabulary doesn't match — see Context): `"down"` → start, positive octaves; `"up"` → start, negative octaves; `"stop"` → stop; `"continue"` → `NotSupportedException`; anything else → `InvalidDataException`.
- Parses `size` (present only on a start) via a new helper mapping `8`/`15`/`22` → `1`/`2`/`3` octaves; anything else → `NotSupportedException` naming `octave-shift@size` (following the `$"{ElementName}@{attribute}"` naming precedent already used elsewhere in this file for unsupported attribute values).
- Parses `number` via the **existing** `ParsePairingNumber(element, OctaveShiftElementName)`, reused as-is.
- Reads only `type`/`size`/`number`; ignores every other attribute (see Context's "attribute strictness" note).

Back in `Read()`'s `"direction"` case, apply the parsed directive to `activeOctaveShiftOctaves`/`activeOctaveShiftNumber`: a start while one is already active throws `NotSupportedException`; a stop validates `activeOctaveShiftOctaves != 0` and that its `number` matches `activeOctaveShiftNumber`, else throws `InvalidDataException`, then resets `activeOctaveShiftOctaves = 0`. An octave-shift left active at end-of-score is tolerated (not an error), matching the "valid, already-imported data" precedent already established for unmatched slur/glissando/arpeggio marks elsewhere in this codebase.

**Acceptance criteria:**
- [x] A `<direction><direction-type><octave-shift type="down" size="8" number="1"/></direction-type></direction>` heredoc fixture parses without error and does not affect notes before it.
- [x] Heredoc/fixture tests (new small `.musicxml` fixtures or `ReadNotes`-heredoc, matching this file's existing error-path test style) cover: `size="10"` (unsupported), `type="continue"` (unsupported), `type="stop"` with no active shift (invalid data), a `type="stop"` `number` that doesn't match the active start's `number` (invalid data), and a second `type="down"` start while one is already active (unsupported).
- [x] A `<direction>` with `<sound tempo="96"/>` still parses exactly as before (regression: this task doesn't touch `ParseTempo`'s own behavior).

**Files likely touched:** `PianoMapper.Core/Music/MusicXmlScoreReader.cs`, `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`, new fixtures under `PianoMapper.Tests/Fixtures/` if the heredoc pattern doesn't fit an error case cleanly.

**Estimated scope:** Medium (2-3 files, several small error-path tests)

**Dependencies:** Task 1.

### Task 3: Apply the active shift to parsed notes; the real 8va/8vb fixture

**Description:** Thread `activeOctaveShiftOctaves` (by value, not `ref` — only the `"direction"` case mutates it) into `ParseNote`. Compute the note's stored `Pitch` as the *notated* pitch (read verbatim from `<pitch>`, exactly as today) shifted by `activeOctaveShiftOctaves` octaves when nonzero (`new Pitch(letter, alter, notatedOctave + activeOctaveShiftOctaves)`), and set `SoundingOctavesAboveNotated: activeOctaveShiftOctaves` on the constructed `ScoreNote`. Add `PianoMapper.Tests/Fixtures/octave-shift-8va.musicxml`: one measure, an unshifted note, then `<octave-shift type="down" size="8" number="1"/>`, three or four shifted notes, `<octave-shift type="stop" size="8" number="1"/>`, then an unshifted closing note — directly matching what the `gama-C-major.jpg` passage needed. Add a second fixture (or a second `[Theory]` case) for the mirror-image `type="up"` (8va-bassa) direction, proving the sign is not backwards.

**Acceptance criteria:**
- [x] `octave-shift-8va.musicxml`: notes before the shift have `SoundingOctavesAboveNotated == 0` and `Pitch` equal to their written `<pitch>`; notes inside the shift have `SoundingOctavesAboveNotated == 1` and `Pitch.Octave` one **higher** than their written `<pitch>`; notes after the stop revert to `0`/written pitch.
- [x] The mirror `type="up"` fixture: shifted notes have `SoundingOctavesAboveNotated == -1` and `Pitch.Octave` one **lower** than written.
- [x] `size="15"`/`size="22"` cases (heredoc, one each) produce `SoundingOctavesAboveNotated` of `±2`/`±3`.
- [x] A new end-to-end test proves a shifted note's `Pitch.MidiNumber` matches what `Grader.Classify`/`ScoreDerivation.Flatten` would grade as correct for the physically-higher/lower key — i.e., grading logic needs zero changes and already works (satisfies this plan's "no changes to Grader.cs" acceptance criterion with evidence, not assertion).

**Files likely touched:** `PianoMapper.Core/Music/MusicXmlScoreReader.cs`, `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs`, `PianoMapper.Tests/Fixtures/octave-shift-8va.musicxml`, `PianoMapper.Tests/Fixtures/octave-shift-8vb.musicxml` (or equivalent).

**Estimated scope:** Medium (2-3 files)

**Dependencies:** Task 2.

### Checkpoint: Phase 2
- [x] `dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"` passes, including all new octave-shift reader tests.
- [x] Manual read-through: every acceptance criterion for Tasks 2-3 has a corresponding passing test, not just "should work."

## Phase 3: Persistence

### Task 4: Round-trip `SoundingOctavesAboveNotated` through saved scores

**Description:** In `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`, add `SoundingOctavesAboveNotated` as a trailing `int ... = 0` field on the private `ScoreNoteDocument` record and thread it through `ToDocument`/`FromDocument`, matching exactly how `Arpeggio` (a bare enum, not wrapped) and `Slur`/`Glissando` (wrapped records) were added — trailing, defaulted, so old saved-score JSON with no such field still deserializes.

**Acceptance criteria:**
- [x] A round-trip test (`Serialize` → `Deserialize`) proves a shifted note's `SoundingOctavesAboveNotated` survives.
- [x] A test deserializing a JSON string that predates this field (no `soundingOctavesAboveNotated` key) still succeeds and defaults to `0`, matching the existing "old saved scores still deserialize" pattern already tested for other optional fields.

**Files likely touched:** `PianoMapper.Server/Persistence/ScoreDocumentSerializer.cs`, `PianoMapper.Tests/UnitTests/ScoreDocumentSerializerTests.cs`.

**Estimated scope:** Small (1-2 files)

**Dependencies:** Task 1.

### Checkpoint: Phase 3
- [x] Full fast `dotnet test` suite passes.

## Phase 4: Rendering geometry (Score path only)

### Task 5: `GrandStaffLayout.GetNotatedPitch` and the staff-position fix

**Description:** Add `public static Pitch GetNotatedPitch(ScoreNote note)` to `GrandStaffLayout.cs`: returns `note.Pitch` unchanged when `SoundingOctavesAboveNotated == 0`, else `new Pitch(note.Pitch.Letter, note.Pitch.Alter, note.Pitch.Octave - note.SoundingOctavesAboveNotated)` (note: `Pitch` has no settable properties, so a `with` expression will not compile here — use the constructor). In `GetScoreNoteLayout` (`GrandStaffLayout.cs:189`), change `GetPosition(note.Pitch, note.Staff)` to `GetPosition(GetNotatedPitch(note), note.Staff)`. Leave `GetPosition`/`GetLivePosition` themselves unchanged (still take a raw `Pitch`) — this keeps the Live path, which never calls `GetNotatedPitch`, completely untouched.

**Acceptance criteria:**
- [x] A new `GrandStaffLayoutTests.cs` case: a note with `Pitch` = (sounding, e.g. C6) and `SoundingOctavesAboveNotated = 1` renders at the same `StaffPlacement`/ledger-line set as an otherwise-identical unshifted note at the notated pitch (C5) — i.e., proves the *position* comes from the notated pitch, not the sounding one.
- [x] A companion test proves that same shifted note's `Pitch` (used for anything *other* than `GetScoreNoteLayout`) is still the unmodified sounding pitch — the field isn't mutated, only read differently by rendering.
- [x] Every existing `GrandStaffLayoutTests.cs`/`GrandStaffSceneBuilderTests.cs` case for `SoundingOctavesAboveNotated == 0` notes is byte-identical to before (regression).

**Files likely touched:** `PianoMapper.Core/Rendering/GrandStaffLayout.cs`, `PianoMapper.Tests/UnitTests/GrandStaffLayoutTests.cs`.

**Estimated scope:** Small (1-2 files)

**Dependencies:** Task 1.

### Task 6: Fix the Score-path staff-auto-selection heuristic

**Description:** In `GrandStaffSceneBuilder.BuildStaticScoreParts` (`GrandStaffSceneBuilder.cs:154-163`), change both `GrandStaffLayout.GetLivePosition(candidate.Pitch)` and `GrandStaffLayout.GetLivePosition(note.Pitch)` to `GrandStaffLayout.GetLivePosition(GrandStaffLayout.GetNotatedPitch(candidate))` / `(note)`. This is the pitch-based treble/bass auto-selection heuristic (lessons.md: "choose treble or bass notation from pitch") — it must use the same notated pitch as the actual staff-position calculation, or a shifted note near the treble/bass boundary could be auto-assigned to the wrong staff relative to where it's actually drawn.

**Acceptance criteria:**
- [x] A new `GrandStaffSceneBuilderTests.cs` case: a note notated near the treble/bass boundary (e.g. notated B3) that sounds well into treble range under an 8va shift (sounding B4) is still auto-assigned to the staff its *notated* position would pick, not its sounding position — constructed to actually differ between the two, so the test would fail without this task's fix.
- [x] Every existing (non-shifted) staff-auto-selection test is unaffected (regression).

**Files likely touched:** `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`, `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`.

**Estimated scope:** Small (1-2 files)

**Dependencies:** Task 5.

### Checkpoint: Phase 4
- [x] Full fast `dotnet test` suite passes.
- [x] Manual confirmation (read-through, not yet visual): a shifted note's computed `ScoreNoteLayout.Position` has few/no ledger lines compared to what it would have had at its sounding pitch.

## Phase 5: Rendering — the bracket

### Task 7: New enum members and bracket-span building

**Description:** Append `OctaveShift` to `GrandStaffLineKind` and `OctaveShiftNumeral` to `GrandStaffGlyphKind` (append only — both cross the JS interop boundary as pinned ordinals; do not insert). Add a method in `GrandStaffSceneBuilder.cs` (e.g. `BuildOctaveShiftMarks`, alongside `BuildSlurs`/`BuildGlissandoLines`) that, given the same `(ScoreNote Note, ScoreNoteLayout Layout)` list and rendered notes `BuildStaticScoreParts` already has: groups notes by `Staff`, in onset order, into maximal contiguous runs sharing the same nonzero `Note.SoundingOctavesAboveNotated` (mirroring `BuildBeams`/`AddBeam`'s contiguous-run grouping over `BeamState`, not a start/stop-marker scan). For each run, emit one `GrandStaffLine` (`Kind = OctaveShift`, `X0`/`Y0` at the first note, `X1`/`Y1` at the last note, `Y` set to a fixed clearance above the staff's top line for a positive value or below its bottom line for a negative one — see Assumptions) and one `GrandStaffGlyph` (`Kind = OctaveShiftNumeral`, `Text` = `"8"`/`"15"`/`"22"` from `Math.Abs(value)`, positioned at the run's start). Append both into `staticParts.Lines`/`staticParts.Glyphs` in `BuildStaticScoreParts`, same as glissando lines and tuplet glyphs already are.

**Acceptance criteria:**
- [x] A `GrandStaffSceneBuilderTests.cs` case: the fixture from Task 3, built through `BuildScore`, produces exactly one `GrandStaffLine` with `Kind == OctaveShift` spanning the shifted notes' X range, and one `GrandStaffGlyph` with `Kind == OctaveShiftNumeral` and `Text == "8"`.
- [x] A companion case for the `type="up"` fixture proves the line/glyph Y sits **below** the staff instead of above.
- [x] `GrandStaffSceneContractTests.cs`/`scene-contract.test.mjs` gain a case for the two new ordinal values, proving the C#-to-JS ordinal mapping stays in sync (matching this project's existing pinning discipline for these two enums).
- [x] The two immediately-adjacent-same-magnitude-spans edge case (Out of Scope, below) is left unhandled and not silently "fixed" by accident — a note documenting why, next to the grouping logic.

**Files likely touched:** `PianoMapper.Web/Rendering/GrandStaffLineKind.cs`, `PianoMapper.Web/Rendering/GrandStaffGlyphKind.cs`, `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`, `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`, `PianoMapper.Tests/UnitTests/GrandStaffSceneContractTests.cs`, `PianoMapper.Tests/JavaScript/scene-contract.test.mjs`.

**Estimated scope:** Medium (4-6 files)

**Dependencies:** Task 5, Task 6.

### Task 8: canvas.js — dashed line and numeral rendering

**Description:** In `canvas.js`'s `drawLine`, add a `line.kind === octaveShiftLineKind` branch: set `context.setLineDash([...])` (a small, staff-space-scaled dash/gap pair — this codebase's first use of `setLineDash`) before stroking, and reset with `context.setLineDash([])` immediately after (every other line kind must remain solid; a forgotten reset would silently dash barlines/staff lines drawn afterward in the same canvas pass). Give it a distinct color constant, following `glissandoLineColor`'s precedent. In `drawGlyph`, no new branch should be strictly necessary if `OctaveShiftNumeral` reuses the default glyph styling (`#f8fafc`) — confirm this reads clearly against the staff in the visual check (Task 10); add a dedicated style only if it doesn't.

**Acceptance criteria:**
- [x] `canvas.test.mjs` gains a case asserting `setLineDash` is called with a non-empty pattern for an `octaveShiftLineKind` line and reset to `[]` afterward (mock-canvas-context assertion, matching this file's existing style for other `drawX` functions).
- [x] Every existing `canvas.test.mjs` case for other line kinds still passes unmodified (proves the dash reset doesn't leak into other lines).

**Files likely touched:** `PianoMapper.Web/wwwroot/js/canvas.js`, `PianoMapper.Tests/JavaScript/canvas.test.mjs`.

**Estimated scope:** Small (1-2 files)

**Dependencies:** Task 7.

### Checkpoint: Phase 5
- [x] `dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"` passes.
- [x] `node --test PianoMapper.Tests/JavaScript/*.test.mjs` passes.

## Phase 6: Docs and verification

### Task 9: Update README.md and CONTEXT.md

**Description:** README.md line ~12 currently lists the supported `<notations>` marks (fermata, articulation, ornament, accidental-mark, slur, arpeggio, glissando/slide) as a single sentence about per-note marks. Octave-shift is not a `<notations>` mark — add a **new** sentence/bullet describing the supported `<direction>`-level mark (ottava/8va-15ma-22a, both directions, rendered as a dashed bracket with a numeral) rather than folding it into the existing `<notations>` sentence, to keep the documentation structurally accurate.

**Acceptance criteria:**
- [x] README.md describes octave-shift support distinctly from the `<notations>` list, including both directions and the three sizes.
- [x] CONTEXT.md's `ScoreNote` row (Task 1) is consistent with this description.

**Files likely touched:** `README.md`, `CONTEXT.md`.

**Estimated scope:** Small (1-2 files)

**Dependencies:** Tasks 1-8.

### Task 10: Visual verification

**Description:** Per this project's established practice (project memory: "verify grand-staff spacing in real pixels at the smallest clamped canvas height" via the headless-Firefox screenshot recipe), render the `octave-shift-8va.musicxml` fixture (and ideally the corrected `gama-C-major` data, if convenient) in the running web app and screenshot it, confirming: the shifted notes sit compactly near the staff (not stacked in ledger lines), the dashed bracket is visually distinguishable from a slur/tie/glissando line, the numeral is legible and correctly positioned (above for 8va, below for 8va-bassa), and nothing overlaps the annotation band/fingering rows below the staff.

**Acceptance criteria:**
- [x] Screenshot evidence exists for both the 8va and 8va-bassa cases, at minimum at the default canvas height and the smallest CSS-clamped height (matching the project's own "smallest clamped canvas height" verification convention, since canvas text doesn't scale down with scene geometry — see lessons.md).
- [x] If the fixed-clearance bracket Y-position (Assumptions) fails to clear a note's stem/beam in the rendered fixture, this is recorded and either fixed (extending toward `GetNotationBottomY`-style scanning) or explicitly deferred with a reason.

**Verification result (2026-09-24):** Headless Firefox screenshots were captured under `test-results/octave-shift-visual/` for 8va and 8vb at 19rem/304px and 15rem/240px canvas heights. The first pass exposed the 8vb numeral and line overlapping the bass annotation band; the renderer now includes a downward octave-shift mark in the staff's notation-bottom calculation, moving labels and fingerings below it. The second pass confirmed both directions are legible, compact, distinct from solid notation lines, and clear of stems and annotation rows at both heights.

**Files likely touched:** None (verification only); may produce follow-up findings that reopen Task 7.

**Estimated scope:** Small (verification-heavy, not code-heavy)

**Dependencies:** Task 8.

**Note:** This is a planning document; the planner cannot run the browser or capture screenshots itself. This task is real, required work for the implementer, not optional polish — flagged explicitly rather than silently assumed complete, matching how `docs/plans/configurable-measures-per-page.md`'s Final Checkpoint honestly listed which visual-verification items it could and couldn't perform in that implementation pass.

### Final Checkpoint

- [x] Every acceptance criterion in this plan is satisfied with recorded command/test output, not assumed.
- [x] `dotnet test PianoMapper.slnx --filter "Category!=Integration&Category!=LiveApi"` passes in full.
- [x] `node --test PianoMapper.Tests/JavaScript/*.test.mjs` passes in full.
- [x] `dotnet build PianoMapper.slnx --configuration Release` succeeds.
- [x] Visual verification (Task 10) is either complete with screenshot evidence, or explicitly flagged as not yet done — never silently claimed.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `type="up"`/`type="down"` mapped backwards. | Every rendered ottava (and every sounding-pitch computation feeding grading/audio) would be silently inverted — a serious, hard-to-notice correctness bug. | Verified directly against the primary MusicXML source before design (Context); Task 3 requires explicit, separate positive test coverage of *both* directions, not just 8va. |
| `<offset>` inside `<direction>` is already unconditionally rejected by this reader; real-world exporters commonly pair it with octave-shift directions. | A file imported from MuseScore/Finale (as opposed to this plan's hand-authored fixture) could still fail to import even after this plan ships. | Explicitly flagged, not silently left. Out of Scope. Fixture is deliberately hand-authored without `<offset>` so acceptance criteria are checkable regardless. |
| Global (not per-staff) active-shift state can't represent two simultaneous, independent shifts on treble and bass. | A real piece with independent hand-specific ottava passages would either throw (double-start) or misattribute. | Explicit Out of Scope + Open Question; not present in the motivating fixture. |
| Contiguous-value grouping (no boundary markers) merges two immediately-adjacent same-magnitude spans into one visual bracket. | Rare, cosmetically-narrow edge case; pitches stay correct either way. | Documented in Out of Scope; a code comment at the grouping site explains why, so it isn't mistaken for a bug later. |
| Fixed bracket clearance (not a full note/ledger/stem scan like `GetNotationBottomY`) might not clear an unusually tall stem/beam in a dense shifted passage. | Bracket could visually collide with notation in an edge case. | Task 10's visual check is the actual gate; Open Question flags whether to upgrade to full scanning now or after evidence. |
| First `setLineDash` usage in this codebase — a forgotten reset could leak dashing into unrelated lines drawn afterward in the same canvas pass. | Barlines/staff lines/other marks could render dashed by accident. | Explicit test asserting the reset (Task 8); every existing `canvas.test.mjs` case for other kinds must keep passing unmodified. |
| Desktop shares the position fix (`GetScoreNoteLayout`) but not the bracket — a mismatched, only-partially-consistent experience between web and desktop. | A desktop user sees correctly-positioned but visually unexplained notes. | Explicitly documented (Context, Open Questions), not silently accepted as "desktop is out of scope" the way the prior two plans' *total* desktop exclusion was. |
| `hasBassRegisterNote`/label-row-ordering/chord-second-collision consumers of `.Pitch` were reasoned to be invariant under a uniform shift rather than empirically re-derived from scratch. | If that reasoning is wrong for some edge case not considered, a subtle rendering bug could slip through untested. | Each is called out individually in Context with its specific invariance argument; existing test suites for all of them must keep passing unmodified as a regression gate. |

## Out of Scope

- Fixing the `gama-C-major.jpg` Audiveris import itself, or improving Audiveris's/OMR's ottava recognition — that data stays hand-corrected in the dev database as a one-off, matching `docs/plans/tuplet-support.md`'s identical framing for the same image.
- `type="continue"` — matches this reader's existing "start/stop only, no continue" precedent for slur/tie/glissando.
- `<offset>` co-occurring with `<direction>` — a pre-existing constraint, not lifted by this plan.
- Two simultaneous, independent octave-shift spans on different staves (single global active-shift state, "at most one at a time," not per-staff).
- Two immediately-adjacent same-magnitude octave-shift spans rendering as two separate brackets instead of one continuous one (contiguous-value-run grouping, no boundary-marker tracking).
- A bracket end-tick (the small vertical stroke some engravings add where the bracket stops) or any other engraving polish beyond a dashed line + numeral — matches `docs/plans/tuplet-support.md`'s "a full tuplet bracket line" deferral precedent (numeral/line only, not full engraving fidelity).
- Desktop bracket rendering (`PianoMapper/Rendering/StaffRenderer.cs` draws no dashed line/numeral) — though desktop note *positioning* is fixed for free via the shared `GrandStaffLayout.GetScoreNoteLayout` change (see Context/Open Questions for the nuance).
- The Live/free-play grand staff — no "notated vs. sounding" distinction applies to a note being played right now.
- Any change to Grader.cs, ScoreDerivation.cs, NoteReadingSession.cs, audio scheduling, or fingering generation — all already correct against the sounding `Pitch` with zero changes (Context table).

## Open Questions

- **Independent per-staff octave shifts.** V1 tracks one global active shift, not per-staff — a real virtuosic passage with different simultaneous ottava markings on each hand isn't representable. Is this acceptable for now, or should the reader track shift state per `Staff` instead (a real but scoped-out possibility, not merely a theoretical one, unlike some of the other cuts above)? Recommend shipping v1 as scoped and revisiting only if a real score needs it.
- **Desktop's partial benefit.** Desktop will show correctly-positioned octave-shifted notes with no bracket at all, explaining nothing. Is silently-correct-but-unexplained acceptable, or should this plan also add a (much smaller) bracket-drawing pass to `StaffRenderer.cs`? Recommend leaving desktop bracket-free, matching this repo's repeated precedent of scoping new visual notation to the web client only (`tuplet-support.md`, `configurable-measures-per-page.md`) — but flagging since the "free position fix, no bracket" combination is a new nuance this repo's plan history hasn't hit before, not a straightforward repeat of the prior desktop cuts.
- **Bracket vertical clearance.** Recommend the simple fixed-clearance approach (Assumptions) pending Task 10's visual check; if it collides with a dense passage's stems/beams, a follow-up would extend it toward `GetNotationBottomY`'s fuller scan. Confirm this staged approach is acceptable rather than building the fuller scan up front.
- **`size` values beyond {8, 15, 22}.** MusicXML's schema technically allows any `xs:positiveInteger`, but only 8/15/22 have real engraving meaning and no known real file uses anything else. Recommend the strict three-value allowlist (`NotSupportedException` otherwise), matching this reader's general "explicitly supported subset" philosophy over silently accepting arbitrary values via formula. Confirm.
