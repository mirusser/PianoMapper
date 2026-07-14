# Implementation Plan: Piano Learning App ("Grand Plan")

## Overview

Evolve PianoMapper from a keyboard-synth toy into a piano learning app in five phases: (1) replace the stringly-typed pitch model with a spelled `Pitch` value type, (2) render played notes live on a grand staff (treble + bass clef), (3) make musical time (note values / tempo / meter) first-class and capture key releases so held duration is *measured* instead of synthesized, (4) import sheet music from MusicXML into our own `Score` model and display/play it, (5) grade a play-along performance against the score (right pitch, right time, right duration) with live visual feedback.

Each refactor phase is a **deepening** move: concentrating behavior that is currently re-derived at several call sites behind one small interface, so later phases consume one tested module instead of re-implementing pitch/time/audio logic. The repo's existing pattern — pure, unit-tested Layout classes + thin GL Renderers — is preserved for every new visual.

## Scope decisions (user-confirmed 2026-07-14)

- **Import format: MusicXML only for now.** MIDI (files and live devices) is explicitly out of scope; seams are named so a MIDI adapter can be added later without redesign, but no speculative interfaces are built for it.
- **Staff vs piano-roll: toggle.** Tab switches the upper band between grand staff and piano-roll; grand staff is the default view on launch. Both views stay maintained.
- **Practice mode v1: play-along with cursor.** Count-in clicks, cursor sweeps the score at tempo, live verdict coloring, end-of-run summary.
- **MusicXML reading: custom subset reader.** System.Xml-based, zero new NuGet dependencies, fixture-file tests. Unsupported elements fail loudly with a clear message — never silently misrender.

## Domain Language

These terms seed `CONTEXT.md` (Task 0.1) and are used consistently in code, tests, and this plan.

| Term | Meaning |
|---|---|
| **Pitch** | A spelled musical pitch: letter + accidental (alter) + octave, e.g. C♯4. Spelling matters: C♯4 ≠ D♭4 even though they sound the same. Frequency and MIDI number are *derived from* a Pitch, never stored beside it. |
| **NoteValue** | A rhythmic value: denominator (1=whole … 16=sixteenth) plus dots. Carries no seconds; seconds require a Tempo. |
| **TimeSignature** | Beats per measure + which NoteValue is one beat (e.g. 3/4, 6/8). |
| **Tempo** | Beats per minute referencing the TimeSignature's beat note. |
| **Score** | An imported piece: time signature, tempo, key signature, measures of ScoreNotes/rests. Our own model — importers adapt file formats *to* it; renderers/grader consume only it. |
| **ScoreNote** | One expected note in a Score: Pitch, NoteValue, onset (measure + beat), staff assignment (treble/bass), tie info. |
| **Staff / GrandStaff** | Treble or bass staff; the grand staff is both, joined, with middle C on the ledger line between them. |
| **StaffPosition** | The diatonic slot (line/space, incl. ledger lines) a Pitch occupies on a given staff. Pure math in GrandStaffLayout. |
| **PerformedNote** | What the player actually did: Pitch, onset time, release time (null while still sounding). No audio-engine handles, no sample buffers. |
| **Instrument** | The module that makes sound: owns synthesis, OpenAL source/buffer lifecycle, gain policy, and timeline registration behind `NoteOn`/`NoteOff`/`Play`. |
| **PracticeSession** | One play-along run: count-in, anchored start time, recorded PerformedNotes, Verdicts. |
| **Verdict** | Per-expected-note grading outcome: Correct, WrongPitch, Early, Late, TooShort, TooLong, Missed; plus Extra for unmatched performed notes. |

## Architecture Decisions

- **AD1 — `Pitch` is a spelled value type** (`readonly record struct`: NoteLetter, Alter, Octave) in `PianoMapper/Music/`. MIDI number, frequency (12-TET, A4 = 440 Hz), display name, and diatonic index are derived get-only properties; `TryParse`/`ToString` round-trip scientific names ("C#4", "Db4"). Equality is spelling equality; enharmonic comparison goes through `MidiNumber`. *Rationale:* staff placement needs spelling; today pitch identity is a string + float that can and does disagree (the M-key palette labels 440 Hz as "C"), and every consumer re-derives semantics (`NoteColors` parses strings, `PianoRollLayout` log2's frequency). One deep module ends that. Derived properties and static parse factories are the BCL value-type idiom and stay within the "records are behavior-free" standard — any mutable or stateful behavior would force a class.
- **AD2 — Musical time is first-class.** `NoteValue`, `TimeSignature`, `Tempo` value types plus one conversion module (musical duration ↔ `TimeSpan`). The two confusable `PCM.GetTimedNoteDuration` overloads are deleted; `PCM` keeps only the synthesis-domain concern (natural decay length for a frequency). *Rationale:* which overload fires today depends on C# overload-resolution tie-breakers — the interface is as complex as the implementation. Notation needs note values as data; grading needs expected duration in musical time.
- **AD3 — Layout + Renderer pattern preserved.** `GrandStaffLayout` is pure, GL-free, fully unit-tested; `StaffRenderer` is a thin GL adapter drawing lines/quads like `PianoRollRenderer`. Notation glyphs follow the existing hand-authored pixel-glyph idiom — accidentals in `BitmapFont`, clefs in a sibling fixed-size `StaffGlyphs` table (the 3×5 char grid is too coarse for clefs; see Task 2.2); a SMuFL/Bravura texture atlas is explicitly out of scope.
- **AD4 — `Score` is our model; the MusicXML reader is one concrete adapter.** `MusicXmlScoreReader` is a plain class — **no `IScoreReader` interface until a second reader exists** (one adapter = hypothetical seam; simplicity-first per AGENTS.md). The `Score` type itself is the seam every consumer depends on.
- **AD5 — Input becomes NoteOn/NoteOff events.** Key-down starts a note, key-up ends it (OpenTK `IsKeyDown`/`IsKeyReleased`), so held duration is measured. The keyboard is the only adapter for now; a future MIDI-in device would be the second. The seam is the Instrument's NoteOn/NoteOff surface, not a speculative input interface.
- **AD6 — Instrument module extracted from `PianoMapperWindow`.** Synthesis dispatch, AL buffer/source lifecycle, polyphony gain policy, cleanup scheduling, and timeline registration move behind a small interface in `PianoMapper/Audio/`. *Rationale (deletion test):* score playback (Task 4.5) and the count-in metronome (Task 5.2) need to sound notes programmatically — without the extraction, `PlayNoteAsync`'s ~80 lines get a second and third copy. The window shrinks toward composition root + input routing, the only part that genuinely can't be unit-tested.
- **AD7 — `PerformedNote` is purified.** The timeline tracks pitch + onset + release only. OpenAL handles and PCM sample buffers stay on the audio side (dispatcher/instrument-owned bookkeeping keyed by note identity); the oscilloscope gets its sample window through that audio seam instead of every timeline entry carrying its buffer. *Rationale:* the grader and both visual layouts must be testable with `FakeTimeProvider` and zero OpenAL.
- **AD8 — Time is observed only through `TimeProvider`** (existing repo pattern), tested with `FakeTimeProvider`. No `DateTime.Now`/`Stopwatch` in testable modules.
- **AD9 — Code standards applied throughout:** file-scoped namespaces; camelCase private fields without `_`; `sealed` by default; `internal` + existing `InternalsVisibleTo` for non-public surfaces; `IReadOnlyList<T>` on API surfaces; one meaningful type per file (new `Music/`, `Practice/` folders, no grab-bag files); tests one-class-per-type named `{Type}Tests` with `Method_State_ExpectedResult` naming and `[Theory]`/`[InlineData]` over duplicated facts; options for grading tolerances live in an options record.

## Dependency Graph

```
Phase 0   CONTEXT.md (glossary)
              │
Phase 1   Pitch value type ── retrofit key mapping, NoteInstance, colors, roll layout
              │
      ┌───────┴────────────────────┐
Phase 2   Grand staff live view    Phase 3   Musical time (NoteValue/TimeSignature/Tempo)
          (GrandStaffLayout,                 PerformedNote ⁄ audio-bookkeeping split
           BitmapFont glyphs,                Instrument extraction
           StaffRenderer + toggle)           NoteOn/NoteOff measured input
      └───────┬────────────────────┘
              │   (Phases 2 and 3 are independent of each other — parallelizable)
Phase 4   Score model ← Pitch + musical time
          MusicXmlScoreReader ← Score
          Score view ← GrandStaffLayout + Score
          Score playback ← Instrument + Score
              │
Phase 5   Grader ← Score + PerformedNote        Practice mode UX ← all of the above
```

**Parallelization note:** after Checkpoint 1, Phase 2 (staff view) and Phase 3 (musical time + input) are logically independent and can proceed in parallel sessions — but not in disjoint files: 2.3, 3.1, and 3.4 all edit `PianoMapperWindow.cs`, and 3.2 touches the layout files. Sequence those merges deliberately (see 3.2's dependency note and the risk table). Phase 4 needs both; Phase 5 needs Phase 4.

## Task List

Global verification for every task (AGENTS.md rule 5 — show evidence, don't claim):

- Build: `dotnet build PianoMapper.slnx`
- Tests: `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj`
- Run: `dotnet run --project PianoMapper/PianoMapper.csproj`

### Phase 0 — Domain language

#### Task 0.1: Create CONTEXT.md with the domain glossary

**Description:** Create root `CONTEXT.md` containing the Domain Language table from this plan (adapted to the CONTEXT format used by the grill-with-docs skill). This is the canonical glossary future sessions and architecture reviews key off; terms are added/sharpened as later tasks crystallize them.

**Acceptance criteria:**
- [x] `CONTEXT.md` exists at repo root defining at minimum: Pitch, NoteValue, TimeSignature, Tempo, Score, ScoreNote, StaffPosition, PerformedNote, Instrument, PracticeSession, Verdict.
- [x] Each term's entry says what it is *and* what it deliberately excludes (e.g. Pitch stores no frequency).

**Verification:**
- [x] Manual read-through; no build impact.

**Dependencies:** None
**Files likely touched:** `CONTEXT.md` (new)
**Estimated scope:** XS

### Phase 1 — Pitch (deepens investigation candidate 1)

#### Task 1.1: `Pitch` value type

**Description:** Add `PianoMapper/Music/Pitch.cs` — a `readonly record struct` with `NoteLetter` (enum C…B, new file), `Alter` (int, −2…+2), `Octave` (int, scientific pitch notation, C4 = middle C). Derived get-only members: `MidiNumber`, `Frequency` (12-TET, A4 = 440.0), `DiatonicIndex` (letter+octave → diatonic step count, the input for staff math), `ToString()` producing "C#4"/"Db4"/"C4", and `static bool TryParse(string, out Pitch)` accepting the same forms. Spelling equality is the record default; document that enharmonic comparison goes through `MidiNumber`. MusicXML `alter` can carry ±2 (double accidentals) into `Pitch` via the Phase 4 reader, so give `ToString` a documented form for them (e.g. "Cx4"/"Cbb4"); `TryParse` support for double accidentals is optional.

**Acceptance criteria:**
- [x] Known anchors hold: C4 → MIDI 60, ≈261.626 Hz; A4 → MIDI 69, 440 Hz; C#4 and Db4 → same MidiNumber, *unequal* Pitch values, different DiatonicIndex.
- [x] `ToString`/`TryParse` round-trip for all letters, alters −1/0/+1, octaves 0–8; malformed input returns false without throwing.
- [x] No consumer changes yet — purely additive.

**Verification:**
- [x] New `PitchTests` (Theory-heavy) pass; full suite green.

**Dependencies:** None (0.1 recommended first)
**Files likely touched:** `PianoMapper/Music/Pitch.cs` (new), `PianoMapper/Music/NoteLetter.cs` (new), `PianoMapper.Tests/UnitTests/PitchTests.cs` (new)
**Estimated scope:** S

#### Task 1.2: Retrofit the note flow to `Pitch`

**Description:** Replace the `Note { Name, Frequency }` class and the string+float pair in `NoteInstance` with `Pitch`. `Consts.GenerateKeyToFrequencyMapping` becomes a `Keys → Pitch` mapping built from semitone offsets (no more parallel name-string/frequency computation — delete the local noteNames array). `NoteInstance` carries `Pitch Pitch` instead of `NoteName`/`Frequency`; synthesis and labels read `note.Pitch.Frequency` / `note.Pitch.ToString()`. Fix the M-key demo palette to be spelled honestly (440 Hz is A4, not "C"). `NoteColors.GetColor(string)` keeps working this task by being fed `pitch.ToString()`; its signature changes in 1.3.

**Acceptance criteria:**
- [x] `Note` class is deleted; the deletion test holds — no complexity reappears, callers got simpler.
- [x] Key mapping frequencies match the old values within 0.05% (regression-checked for octave 4 across all 13 keys). Exact equality is deliberately not preserved: the old base (C0 = 16.35 Hz in `GenerateKeyToFrequencyMapping`) sits ≈0.17 cents flat of A440 12-TET, so deriving from `Pitch.Frequency` shifts every key by that constant, inaudible amount — and A4 becomes exactly 440 Hz.
- [x] M-key palette pitch is A4; console log, label, and roll position now agree.

**Verification:**
- [ ] Suite green; run the app — keys play the same notes with the same labels as before.

**Dependencies:** 1.1
**Files likely touched:** `PianoMapper/Consts.cs`, `PianoMapper/NoteInstance.cs`, `PianoMapper/NoteTimeline.cs`, `PianoMapper/PianoMapperWindow.cs`, `PianoMapper.Tests/UnitTests/NoteTimelineTests.cs`
**Estimated scope:** M

#### Task 1.3: Colors and roll layout consume `Pitch`

**Description:** `NoteColors.GetColor` takes a `Pitch` and colors by chromatic pitch class (`MidiNumber mod 12`) — enharmonics get the same color and the string-parsing path (`ExtractPitchClass`, sharps-only, silent fallback) dies. `PianoRollLayout` maps pitch to Y via semitone offset from A4 (`MidiNumber − 69`) instead of `log2(frequency)` — same band math, visually indistinguishable output (sub-pixel shift only, from 1.2's ≈0.17-cent retuning), no float re-derivation.

**Acceptance criteria:**
- [x] Db4 and C#4 render the same color; the FallbackColor path for malformed names is gone.
- [x] Roll Y positions match the old `log2(frequency)`-based math within 1e-3 NDC (sub-pixel) for a spread of mapped pitches — tolerance, not exact equality, because 1.2 retunes frequencies by ≈0.17 cents.

**Verification:**
- [ ] Updated `NoteColorsTests` / `PianoRollLayoutTests` pass; run the app — visuals indistinguishable from before.

**Dependencies:** 1.2
**Files likely touched:** `PianoMapper/Rendering/NoteColors.cs`, `PianoMapper/Rendering/PianoRollLayout.cs`, their test files
**Estimated scope:** S

### Checkpoint 1 — Pitch foundation
- [x] Build clean, full suite green.
- [ ] Manual run: all 13 keys play and label correctly across octave changes; M-key measure honest; visuals unchanged.
- [x] `CONTEXT.md` updated if any term shifted during implementation.

### Phase 2 — Grand staff live view (investigation candidate 6; vertical slice: "what I play appears on real notation")

#### Task 2.1: `GrandStaffLayout` — staff geometry and pitch placement

**Description:** New pure module `PianoMapper/Rendering/GrandStaffLayout.cs`: Y coordinates for the 10 staff lines (treble E4–F5, bass G2–A3) within the existing upper band (`BandY0`/`BandY1` region); `Pitch → StaffPosition` via `DiatonicIndex` relative to each staff; ledger-line computation (count and Y positions, middle C = first ledger below treble / above bass); the live-input staff-split rule (MidiNumber ≥ 60 → treble, else bass — imported scores will carry explicit staff assignment instead); time→X reusing the same rolling-window mapping as the roll (`PianoRollLayout.MapTimeToX` is currently `private` — make it `internal`, or extract a small shared time-window mapper, rather than duplicating it). Accidental need (`Alter ≠ 0`) is exposed per placed note for the renderer.

**Acceptance criteria:**
- [x] Anchor pitches place correctly in tests: E4 = bottom treble line, F5 = top treble line, A4 = second treble space, C4 = one ledger between staves (reachable from both), G2 = bottom bass line, E2 = first ledger below bass.
- [x] Ledger lines: A0 and C8 produce the right count and positions; no ledgers for on-staff pitches.
- [x] Zero GL types anywhere in the module.

**Verification:**
- [x] New `GrandStaffLayoutTests` (Theory tables of pitch → expected line/space/ledger) pass.

**Dependencies:** 1.1 (Pitch); independent of 1.2/1.3 for the math, but schedule after Checkpoint 1
**Files likely touched:** `PianoMapper/Rendering/GrandStaffLayout.cs` (new), `PianoMapper/Rendering/StaffPosition.cs` (new), `PianoMapper.Tests/UnitTests/GrandStaffLayoutTests.cs` (new)
**Estimated scope:** M

#### Task 2.2: Notation glyphs in `BitmapFont`

**Description:** Add notation glyphs. Two facts about the existing `BitmapFont` shape this task: the grid is fixed 3×5 (`GlyphWidth`/`GlyphHeight` constants; `Parse` takes exactly five rows), and `GetGlyph` case-folds via `ToUpperInvariant` — so a flat glyph keyed `'b'` would collide with the letter B. Therefore: sharp reuses the existing `'#'` glyph as-is; flat is a new glyph keyed by the real `'♭'` character (BMP, unaffected by upper-casing, falls through `GetGlyph` untouched); treble/bass clefs cannot be recognizable at 3×5, so they go in a new, separate fixed-size staff-glyph table (`StaffGlyphs`, same row-major bool-grid idiom at a larger size, e.g. 7×15) with a documented anchor row per glyph: the treble clef's curl centers on the G4 line, the bass clef's dots straddle the F3 line. Clefs may still be crude; "recognizable + correctly anchored" is the bar.

**Acceptance criteria:**
- [x] Clef glyphs expose a documented anchor row so `StaffRenderer` can align them to the correct staff line.
- [x] Existing `BitmapFont` glyphs untouched (surgical change); `GetGlyph('♭')` returns the flat glyph without colliding with 'B'; new glyph grids covered by tests (dimensions, anchor rows, lit-pixel spot checks).

**Verification:**
- [x] `BitmapFontTests` extended and new `StaffGlyphsTests` green.

**Dependencies:** None (parallel with 2.1)
**Files likely touched:** `PianoMapper/Rendering/BitmapFont.cs`, `PianoMapper/Rendering/StaffGlyphs.cs` (new), `PianoMapper.Tests/UnitTests/BitmapFontTests.cs`, `PianoMapper.Tests/UnitTests/StaffGlyphsTests.cs` (new)
**Estimated scope:** S

#### Task 2.3: `StaffRenderer` + view toggle

**Description:** New thin GL renderer drawing: 10 staff lines (thin quads), clef glyphs, per-note filled notehead quads at `StaffPosition` Y and rolling-window X, ledger-line segments under/over noteheads that need them, and accidental glyph left of the head when `Alter ≠ 0`. Wire into `PianoMapperWindow`: Tab toggles the upper band between grand staff (default on launch) and piano-roll; oscilloscope/spectrum band untouched. Same construction/disposal discipline as the other renderers (create in `OnLoad`, dispose in `OnUnload`).

**Acceptance criteria:**
- [ ] On launch the grand staff shows; playing keys places noteheads on correct lines/spaces, scrolling like the roll; sharps show ♯.
- [ ] Tab flips to the piano-roll and back; no GL errors on repeated toggling.
- [x] Renderer contains no layout math beyond quad assembly (all placement from `GrandStaffLayout`).

**Verification:**
- [ ] Manual run-through: play C4, E4, G4, A4, C#5 and low C3/G2 across octaves — verify against the anchor table from 2.1; toggle repeatedly; Spacebar clear still works in both views.

**Dependencies:** 2.1, 2.2, Checkpoint 1
**Files likely touched:** `PianoMapper/Rendering/StaffRenderer.cs` (new), `PianoMapper/PianoMapperWindow.cs`
**Estimated scope:** M

### Checkpoint 2 — Live notation
- [x] Build clean, suite green.
- [ ] Manual: the user's original near-term ask is fulfilled — played notes appear on treble/bass clefs correctly.
- [x] README features/keys updated in the same slice (Tab toggle; grand staff is now the default view).
- [ ] Review with human before Phase 3/4 (good demo moment).

### Phase 3 — Musical time & measured input (investigation candidates 2, 3, 4, 5)

#### Task 3.1: Musical time module

**Description:** New value types in `PianoMapper/Music/`: `NoteValue` (denominator 1/2/4/8/16 + dots), `TimeSignature` (numerator, beat NoteValue), `Tempo` (BPM). One conversion module (`MusicalTime`) turning (NoteValue, TimeSignature, Tempo) into `TimeSpan` and beats. Delete **both** `PCM.GetTimedNoteDuration` overloads (the overload-resolution trap goes away); `PCM` keeps only `NaturalDecaySeconds(frequency)` with the decay constants defined once. The two call sites (`OnUpdateFrame` key handler, `PlayRandomMeasureAsync`) compute duration as `min(NaturalDecaySeconds, MusicalTime duration)` explicitly. Note: `PCM.GetNoteDuration(frequency)` — the variant that clamps to [1.2, 6.0] s — has zero callers today; delete it in the same move. `NaturalDecaySeconds` pins the *unclamped* decay the live `GetTimedNoteDuration` paths actually compute (the clamp was never live behavior, and adopting it would change note lengths below ~78 Hz and above ~4.4 kHz).

**Acceptance criteria:**
- [x] No `GetTimedNoteDuration` remains; grep-clean. Duration behavior at both call sites is regression-pinned (whole note at 90 BPM ≈ 2.667 s capped by decay — the current effective behavior).
- [x] `MusicalTime` covers: dotted values, 6/8 vs 3/4 distinction, and beats↔seconds round-trips in Theory tests.
- [x] `PlayRandomMeasureAsync` composes measures from `NoteValue`s instead of raw denominator ints.

**Verification:**
- [ ] New `MusicalTimeTests` (+ `NoteValueTests` etc. as needed) pass; run the app — key notes and M-key measures sound the same length as before.

**Dependencies:** Checkpoint 1 (parallel with Phase 2)
**Files likely touched:** `PianoMapper/Music/NoteValue.cs`, `TimeSignature.cs`, `Tempo.cs`, `MusicalTime.cs` (new), `PianoMapper/PCM.cs`, `PianoMapper/PianoMapperWindow.cs`, new test files
**Estimated scope:** M

#### Task 3.2: Split `PerformedNote` from audio-engine bookkeeping

**Description:** Rename/split `NoteInstance` into `PerformedNote` (Pitch, StartTime, `TimeSpan? ReleaseTime` — null while sounding; effective duration = `ReleaseTime ?? now − StartTime`). Sample buffers and AL SourceId/BufferId move out of the timeline record into audio-side bookkeeping keyed by note identity (extending the pattern `AudioDispatcher.sampleOffsets` already uses). `NoteTimeline` gains `Complete(note, releaseTime)`; pruning treats open notes as active. The oscilloscope/spectrum path gets its sample window through the audio side (dispatcher exposes "samples + live offset for the primary note") instead of reading `note.Samples`. `PianoRollLayout`/`GrandStaffLayout` handle open notes by growing the bar/head trail to `now` (`GrandStaffLayout` only if Phase 2 has landed by then). Land it as two compiling steps: (1) additive — add `ReleaseTime` and `Complete` while `Samples`/AL ids still ride along; (2) move `Samples`/`SourceId`/`BufferId` to the audio side and reroute the scope/spectrum path.

**Acceptance criteria:**
- [x] `PerformedNote` contains no `Samples`, `SourceId`, or `BufferId`; rendering-layout and timeline tests run with zero OpenAL references.
- [x] Open-note semantics tested: a note without ReleaseTime renders to `now` and is never pruned; completing it fixes its extent.
- [ ] Oscilloscope/spectrum still track the most recent note (manual check); the estimate fallback still covers the gap between a note's duration elapsing and the audio thread's cleanup running, as it does today.

**Verification:**
- [ ] Updated `NoteTimelineTests`, `PianoRollLayoutTests`, `PlaybackPositionTests` green; manual run for scope/spectrum behavior.

**Dependencies:** 3.1 not required — depends on Checkpoint 1; touches layout code so coordinate with Phase 2 if run in parallel (prefer after 2.3 or before 2.1, not interleaved)
**Files likely touched:** `PianoMapper/NoteInstance.cs` → `PianoMapper/PerformedNote.cs`, `PianoMapper/NoteTimeline.cs`, `PianoMapper/AudioDispatcher.cs`, `PianoMapper/PlaybackPosition.cs`, `PianoMapper/PianoMapperWindow.cs`, both layout files, test files
**Estimated scope:** L — the widest refactor in the plan; the two-step landing above keeps each step compiling and green

#### Task 3.3: Extract the `Instrument` module

**Description:** Move the body of `PianoMapperWindow.PlayNoteAsync` (synthesis dispatch, buffer/source creation, polyphony gain policy, playback, cleanup scheduling, timeline + primary-note registration) into `PianoMapper/Audio/Instrument.cs` with a small surface: `Play(Pitch, TimeSpan duration)` returning the `PerformedNote`/completion task, plus `ClearAll()`. The window's key handler and `PlayRandomMeasureAsync` become one-line callers. The pure gain rule (`1/√polyphony`, clamped) becomes an internal static testable function.

**Acceptance criteria:**
- [x] `PianoMapperWindow` contains no `AL.*` calls; it composes Instrument + renderers + input routing only.
- [x] Gain policy unit-tested (1 → 1.0; 4 → 0.5; monotone non-increasing).
- [ ] Spacebar clear and app shutdown dispose cleanly (no AL errors logged on exit).

**Verification:**
- [ ] Suite green incl. new `InstrumentTests` for the pure parts; manual run — chords, fast runs, clear, exit.

**Dependencies:** 3.2
**Files likely touched:** `PianoMapper/Audio/Instrument.cs` (new), `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/AudioDispatcher.cs`, `PianoMapper.Tests/UnitTests/InstrumentTests.cs` (new)
**Estimated scope:** M

#### Task 3.4: NoteOn/NoteOff — measured key durations

**Description:** Switch the key handler from edge-triggered fixed-duration playback to `NoteOn` at key-down / `NoteOff` at key-up on the Instrument. `NoteOn` synthesizes a buffer capped at the pitch's natural decay (the sustain ceiling); `NoteOff` stops the source early, completes the timeline entry with the measured release time, and cleans up. Holding a key sustains (up to the cap) and the staff/roll bar grows while held; releasing ends it. Octave switching and Spacebar behavior unchanged. Known accepted limitation: a hard `SourceStop` can click; documented, with a short gain-ramp noted as a future refinement (see Risks). Open-note bookkeeping is keyed by physical key, not pitch — an octave switch mid-hold regenerates `keyToFrequencyMap`, and the later key-up must still complete the originally started note; `NoteOff` for a note already cleared by Spacebar is a no-op.

**Acceptance criteria:**
- [ ] Tap vs hold produce audibly and visually different note lengths; the timeline records the measured duration (test via `FakeTimeProvider` at the timeline/Instrument-pure level).
- [ ] A key held past the natural-decay cap ends at the cap without error; re-pressing the same key while sounding starts a new note.
- [ ] The 13 mapped keys, octave changes, and Spacebar clear all behave correctly — including release after an octave switch (completes the originally started note) and release after Spacebar clear (no-op, no AL error).

**Verification:**
- [ ] Suite green; manual run: hold/tap comparison on staff and roll views.

**Dependencies:** 3.2, 3.3
**Files likely touched:** `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Audio/Instrument.cs`, `PianoMapper/NoteTimeline.cs`, test files
**Estimated scope:** M

### Checkpoint 3 — Honest performance capture
- [x] Build clean, suite green.
- [ ] Manual: hold a key — the note sustains and its bar grows; release — it stops. Both views correct. Scope/spectrum still live.
- [x] The window class is a thin composition root (no AL calls, no synthesis, no rhythm math).
- [x] README updated in the same slice: tap vs hold (measured durations) replaces fixed-duration notes.

### Phase 4 — Score & MusicXML import (vertical slice: "load a file → see it → hear it")

#### Task 4.1: `Score` model

**Description:** New types in `PianoMapper/Music/`: `Score` (title, `TimeSignature`, `Tempo`, key signature as fifths int — stored, not yet displayed), measures of `ScoreNote`s and rests. `ScoreNote`: `Pitch`, `NoteValue`, onset (measure index + beat offset), `Staff` (Treble/Bass), tie-to-next flag; chord members share an onset. Derived: flattened absolute-beat event list (what playback and grading consume). Hand-built fixture scores in tests exercise onset math incl. dotted values and ties (tied pairs merge into one sounding event in the flattened list).

**Acceptance criteria:**
- [x] Absolute-onset derivation tested for: 4/4 and 3/4 measures, dotted values, a tie across a barline (one sounding event of combined duration), a two-note chord (same onset, two pitches).
- [x] `Score` and its parts are behavior-free records; derivation logic lives in a small module, not on the DTOs.

**Verification:**
- [x] New `ScoreTests` pass.

**Dependencies:** 1.1, 3.1
**Files likely touched:** `PianoMapper/Music/Score.cs`, `ScoreNote.cs`, `Staff.cs` (new), a derivation module, `PianoMapper.Tests/UnitTests/ScoreTests.cs` (new)
**Estimated scope:** M

#### Task 4.2: MusicXML subset reader — core (single staff)

**Description:** `PianoMapper/Music/MusicXmlScoreReader.cs` (System.Xml, zero new packages) parsing uncompressed `.musicxml`/`.xml`, `score-partwise` only, first part only: `attributes` (divisions, key fifths, time), `note` (`pitch` step/alter/octave, `duration` in divisions, `type`, `dot`, `rest`), `direction/sound@tempo` (default 120 if absent). Divisions-based durations convert to `NoteValue`s. **Unsupported *semantic* elements fail loudly**: anything that would change pitches or timing if ignored (grace notes, tuplets/`time-modification`, additional parts, `score-timewise`, `transpose`, …) throws a clear exception naming the element — never a silent misrender. Presentation and metadata elements that cannot affect pitch or time (`work`, `identification`, `defaults`, `credit`, `print`, non-`sound` directions, dynamics, lyrics) are skipped via one documented ignore list — without this split no real exporter output would ever load (MuseScore always emits `print`/`credit`/`defaults`) and Checkpoint 4's real-file test would be unreachable. Test fixtures: small hand-written MusicXML files checked into the test project, plus one trimmed real MuseScore export that keeps its presentation metadata.

**Acceptance criteria:**
- [x] A single-staff C-major melody fixture round-trips: correct pitches (incl. an altered note), NoteValues, rests, time signature, tempo. A real-export fixture parses despite its `print`/`credit`/`defaults` metadata.
- [x] Semantic unsupported-element fixtures throw with messages naming the element; the presentation ignore list lives in one place; malformed XML surfaces a readable error.
- [x] Reader returns our `Score`; no MusicXML types leak past it.

**Verification:**
- [x] New `MusicXmlScoreReaderTests` with fixtures pass.

**Dependencies:** 4.1
**Files likely touched:** `PianoMapper/Music/MusicXmlScoreReader.cs` (new), `PianoMapper.Tests/UnitTests/MusicXmlScoreReaderTests.cs` + `PianoMapper.Tests/Fixtures/*.musicxml` (new)
**Estimated scope:** M

#### Task 4.3: MusicXML subset reader — piano essentials (two staves, chords, ties)

**Description:** Extend the reader with the elements real piano scores need: `staff` element + `backup`/`forward` (how MusicXML interleaves treble and bass voices in one measure), `<chord/>` (simultaneous notes), `tie`/`tied` (merge handled by 4.1's derivation). Fixtures: a two-staff grand-staff snippet with a bass line under a treble melody, a chord, and a tie across a barline.

**Acceptance criteria:**
- [x] Two-staff fixture: every note lands on the right staff at the right onset (backup/forward arithmetic proven by test).
- [x] Chord fixture: chord members share one onset; tie fixture: one sounding event of combined duration.
- [x] Still loud-failing on anything outside the subset.

**Verification:**
- [x] Extended reader tests green.

**Dependencies:** 4.2
**Files likely touched:** `PianoMapper/Music/MusicXmlScoreReader.cs`, tests + fixtures
**Estimated scope:** M

#### Task 4.4: Score view — notation layout and rendering

**Description:** Extend `GrandStaffLayout` with a measure-based mode: barlines, onsets → X by beat within a horizontally scrolling window, notehead style by `NoteValue` (hollow = whole/half, filled = quarter and shorter), stems (up below middle line, down above), dots, single flag for eighth/sixteenth (beams out of scope), accidental glyph for altered pitches (key-signature-aware display is out of scope — every altered note shows its accidental; verbose but correct). `StaffRenderer` gains the score-view draw path; PgUp/PgDn (or Left/Right) scroll measures. Load path: CLI arg `--score <path>` parsed in `Program.cs`; on load the app opens in score view; parse errors print the reader's message and fall back to live view.

**Acceptance criteria:**
- [ ] The two-staff fixture piece renders with correct staff placement, barlines, notehead styles, stems, dots, and accidentals (manual against the printed fixture).
- [x] Layout math (onset→X, notehead style, stem direction, flag need) is pure and unit-tested; renderer stays assembly-only.
- [x] Bad path / bad file → readable console error, app still usable.

**Verification:**
- [ ] `GrandStaffLayoutTests` extended and green; manual: `dotnet run --project PianoMapper/PianoMapper.csproj -- --score PianoMapper.Tests/Fixtures/grand-staff-demo.musicxml`.

**Dependencies:** 4.3, Checkpoint 2
**Files likely touched:** `PianoMapper/Rendering/GrandStaffLayout.cs`, `StaffRenderer.cs`, `PianoMapper/Program.cs`, `PianoMapper/PianoMapperWindow.cs`, tests
**Estimated scope:** M

#### Task 4.5: Score playback through the Instrument

**Description:** 'P' plays the loaded score: the flattened event list schedules `Instrument.Play(pitch, duration)` calls at onset times derived via `MusicalTime` (driven by the timeline's `TimeProvider` clock, not `Task.Delay` drift — accumulate from the anchor). A cursor (vertical line) sweeps the score view at tempo; Spacebar stops playback and silences. This is the second caller of the Instrument seam — the extraction in 3.3 pays off here.

**Acceptance criteria:**
- [ ] The fixture piece plays with correct pitches, onsets, and durations (chords together, ties held); cursor position matches what is sounding.
- [x] Scheduling math (event list + anchor → due notes) is pure and tested with `FakeTimeProvider`; only the final `Play` call touches audio.
- [ ] Spacebar stops cleanly; playback can be restarted.

**Verification:**
- [ ] New scheduling tests green; manual listen-through of the fixture.

**Dependencies:** 4.4, Checkpoint 3
**Files likely touched:** `PianoMapper/Music/ScorePlayback.cs` (new, pure scheduling), `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Rendering/StaffRenderer.cs`, tests
**Estimated scope:** M

### Checkpoint 4 — Sheet music in and audible
- [x] Build clean, suite green (incl. fixture-based reader tests).
- [ ] Manual: load a real (simple) exported MusicXML file from e.g. MuseScore — it renders and plays; unsupported files fail with clear messages.
- [x] README updated in the same slice: `--score <path>` usage and score-view keys.
- [ ] Review with human — good moment to pick real lesson pieces as further fixtures.

### Phase 5 — Practice mode (grading)

#### Task 5.1: Grader — pure performance-vs-score assessment

**Description:** New `PianoMapper/Practice/` module. Inputs: the score's flattened expected events, an `IReadOnlyList<PerformedNote>`, the practice anchor time, and a `GradingOptions` record (defaults: onset tolerance ±200 ms; duration ratio window 0.5–1.5; all tunable). Matching: for each expected event, the nearest unmatched performed note whose onset falls inside the tolerance window, preferring pitch-equal candidates over nearer wrong-pitch ones (a correct-but-late C4 must not lose its match to a closer D4); pitch compared by `MidiNumber` (the player's keyboard carries no spelling). Verdicts per the Domain Language: Correct / WrongPitch / Early / Late / TooShort / TooLong / Missed, plus Extra for unmatched performed notes; chords match as sets within the window. Output: per-event verdicts + a summary (accuracy %, per-verdict counts).

**Acceptance criteria:**
- [x] Table-driven Theory tests cover: exact hit, early/late inside vs outside tolerance, wrong pitch at the right time, too-short/too-long holds, missed note, extra note, a chord with one wrong member, two same-pitch notes in close succession (nearest-onset matching disambiguates), and a wrong-pitch note closer to the expected onset than the correct-pitch one (pitch-equal preference wins).
- [x] Grader is deterministic and pure — no clocks, no audio, no rendering types.
- [x] Tolerances come only from `GradingOptions`; no magic numbers in the algorithm.

**Verification:**
- [x] New `GraderTests` pass.

**Dependencies:** 4.1 (expected events), 3.2 (PerformedNote); implementable in parallel with 4.4/4.5
**Files likely touched:** `PianoMapper/Practice/Grader.cs`, `GradingOptions.cs`, `Verdict.cs` (new), `PianoMapper.Tests/UnitTests/GraderTests.cs` (new)
**Estimated scope:** M

#### Task 5.2: Practice session — count-in, cursor, live feedback, summary

**Description:** Enter starts a `PracticeSession` on the loaded score: one measure of count-in ticks (short high note through the Instrument), then the cursor sweeps at tempo while the player plays along; performed notes keep sounding and appearing as usual. Expected noteheads recolor as their tolerance window closes (regrade the snapshot each frame — scores at this scale make that cheap; revisit only if profiling says otherwise). At the end, a summary panel (accuracy %, per-verdict counts) renders via `TextRenderer`; Enter retries from the summary (R cannot — it is the mapped F♯ note key), Escape/Spacebar aborts to free play. Two of today's bindings are re-routed contextually: Escape currently *exits the app* (`OnUpdateFrame` treats it like Q), so with a session active it must abort to free play instead and only exit otherwise; Spacebar's clear-notes role doubles as abort during a run.

**Acceptance criteria:**
- [ ] Full loop works on the fixture piece: count-in → play along → live verdict colors appear as the cursor passes → summary → retry.
- [x] Session state machine (idle → counting-in → running → finished) is pure and unit-tested with `FakeTimeProvider`; the window only routes input and draws.
- [ ] Aborting mid-run leaves the app in normal free-play state with no stuck audio; Escape aborts the session instead of exiting the app (and still exits in free play); the R key still plays F♯ during a run.

**Verification:**
- [ ] New `PracticeSessionTests` green; manual full practice run on the fixture piece, deliberately playing one wrong note, one late note, and one cut-short note — verdicts match intent.

**Dependencies:** 5.1, 4.5
**Files likely touched:** `PianoMapper/Practice/PracticeSession.cs` (new), `PianoMapper/PianoMapperWindow.cs`, `PianoMapper/Rendering/StaffRenderer.cs`, `PianoMapper/Rendering/GrandStaffLayout.cs`, tests
**Estimated scope:** M

### Checkpoint 5 — Grand plan complete
- [x] Build clean, full suite green.
- [ ] End-to-end demo: `--score <file>` → piece renders on grand staff → P plays it → Enter practices it with count-in, live verdicts, and a final score.
- [x] `README.md` was kept current at each checkpoint — run the verify-readme-docs skill here as the final audit (features, keys, `--score` usage, practice mode).
- [x] `CONTEXT.md` reflects the final vocabulary; record any contested decisions as ADRs in `docs/adr/` if they surfaced.

## Out of Scope (deliberate, this plan)

- MIDI in any form: file import, live MIDI keyboards (when it lands: DryWetMIDI for files; Haukcode.MidiDevice / RtMidi.Core for ALSA devices on Linux — DryWetMIDI's device layer is Windows/macOS-only).
- Compressed `.mxl` (tiny zip wrapper — natural small follow-up), multi-part scores, voices beyond two staves, grace notes, tuplets, beams.
- Key-signature *display* (signature at line start + omitting redundant accidentals) and courtesy accidentals — pitches are already imported correctly via `alter`; this is presentation only.
- SMuFL/Bravura glyph atlas (would need textured-quad support; current renderers are solid-color).
- Velocity/dynamics, pedal, articulation; free-play (unanchored) grading; recording/export.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| MusicXML variability in the wild (divisions schemes, exporter quirks) | High | Strict subset + loud errors listing unsupported elements; fixtures from real MuseScore exports at Checkpoint 4; grow the subset by fixture, never by guess. |
| `backup`/`forward` + chord arithmetic (two-staff interleaving) is the classic MusicXML trap | Med | Isolated in Task 4.3 with dedicated fixtures proving onset math per staff. |
| Open-ended (held) notes ripple through timeline/layout assumptions (`Duration` is currently required) | Med | Introduced early (3.2) with tests, *before* the input change (3.4) exploits it. |
| Hard `SourceStop` at NoteOff clicks audibly | Low | Accepted for v1 and documented; future: short gain ramp on the audio thread before stop. |
| Grading alignment ambiguity (repeated pitches, dense passages) | Med | Nearest-onset matching within tolerance, enumerated in table-driven tests; tolerances in `GradingOptions`. |
| Pixel clef glyphs (`StaffGlyphs`) look crude | Low | Acceptance is "recognizable + correctly anchored", not beautiful; Bravura atlas is the future polish path. |
| Per-frame full regrade cost during practice | Low | Fine at lesson-piece scale; state machine isolates it so an incremental grader can slot in if profiling demands. |
| Phase 2 ∥ Phase 3 parallel work colliding in layout files | Low | Dependency notes in 3.2 say: don't interleave with 2.1–2.3; sequence or rebase deliberately. |

## Open Questions (non-blocking — defaults chosen, confirm along the way)

- Key bindings for score/practice controls (current defaults: Tab view toggle, P play, Enter practice/retry, PgUp/PgDn scroll). Two collisions exist *today* and are handled in 5.2 — Escape exits the app, and R is the mapped F♯ note key; any binding added later must be checked against the 13 note keys, M, Space, Q, Escape, arrows, and 1–8.
- Grading tolerance defaults (±200 ms onset, 0.5–1.5 duration ratio) — tune by feel in 5.2; beginners may need looser onsets.
- Whether the piano-roll should eventually also get a score-overlay mode (expected vs played as bars) — not planned; would be a cheap byproduct of the grader if wanted.

## Execution Notes

- Follow TDD where the module is pure (everything in `Music/`, `Practice/`, and the layouts): failing test first, then implementation (AGENTS.md rule 4; tdd skill).
- Every task ends with the global verification commands plus its own manual check; paste evidence (AGENTS.md rule 5).
- One task per session/agent; commit per task or per checkpoint on a feature branch (e.g. `feature/piano-learning`), messages in the repo's existing `feature:`/`fix:` style.
- Corrections during implementation go to `.agents/lessons.md` (AGENTS.md rule 6); new/changed domain terms go to `CONTEXT.md` in the same change that introduces them.
