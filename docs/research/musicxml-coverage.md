# MusicXML coverage: what the importer and grand-staff renderer are missing

**Date:** 2026-10-03
**Question:** How do we turn MusicXML into a grand staff today, what does the official MusicXML format actually contain, and what are we missing?
**Evidence type:** the official W3C schema and sample files, run through the real `MusicXmlScoreReader`, plus a read of the reader and renderer code. **No pixel-level check of the rendered staff was done** (see [Caveats](#caveats)).

## Summary

1. **The reader is all-or-nothing.** It throws on the first element it doesn't recognize, so one unsupported construct rejects the whole file. Of 18 official W3C sample files, **1 imports** (`tutorial-hello-world`). Most of the rest are feature-demo files outside our scope (multi-part, tablature, percussion), but the official Chopin piano sample, a single-part two-staff piano piece, fails on a valid measure-level `<sound>`.
2. **Several ordinary, spec-legal piano constructs reject the file**: a whole-measure rest without `<type>`, a slur ending and starting on the same note, two articulations on a note, `<offset>` in a direction, `<sound dynamics>`, `<harmony>`, volta `<ending>`, 32nd notes, `<movement-number>`.
3. **Some things are accepted but come out wrong, with no warning.** The worst: a mid-piece key change makes the *last* key apply to the whole score; a pickup measure is treated as a full measure; `print-object="no"` is ignored; a `<metronome>` mark without `<sound tempo>` is ignored (tempo falls back to 120).
4. **A lot of valid notation is parsed away and never drawn**: dynamics, hairpins, pedal marks, tempo words, lyrics, repeat signs, clef changes, the common/cut time symbol. In an imported score, **rests and ties are not drawn at all** (already documented in `README.md:153`).
5. The stated policy ("fail with a readable error instead of silently dropping") is applied inconsistently: musically meaningful things are dropped silently (dynamics, key changes), while presentation-only things throw (`<offset>`, `<staff-details>`, `<part-symbol>`).

## Official sources

| Source | URL | Used for |
|---|---|---|
| MusicXML 4.0 specification (W3C Music Notation Community Group) | https://www.w3.org/2021/06/musicxml40/ | Landing page; links to tutorial, element reference, examples |
| Tutorial | https://www.w3.org/2021/06/musicxml40/tutorial/introduction/ | How a file is structured |
| Element reference (one page per element) | e.g. https://www.w3.org/2021/06/musicxml40/musicxml-reference/elements/note/ (also `attributes`, `clef`, `rest`, `grace`, `barline`, `direction-type`, `notations`, `key`) | Meaning and content model of each element |
| Reference examples | https://www.w3.org/2021/06/musicxml40/musicxml-reference/examples/ | Per-element snippets |
| Spec repository | https://github.com/w3c/musicxml | Schema, docs source, tests. Default branch is `gh-pages` (last push 2026-10-01); the only tagged release is `v4.0` (2021-06-03). The Chopin tutorial sample declares `version="4.1"`, so the repo carries the 4.1 draft |
| XSD, version 4.0 | https://raw.githubusercontent.com/w3c/musicxml/v4.0/schema/musicxml.xsd | **Authoritative content models.** All "valid per spec" claims below were checked against it. It also carries the schema documentation strings quoted here |
| Official sample files | `docs/src/data/examples/musicxml/*.musicxml` (11 files) and `tests/files/*.musicxml` (7 files) in the repo above | The 18-file import test below |
| SMuFL glyph names | referenced throughout the XSD (`smufl-glyph-name`, `smufl-accidental-glyph-name`, ...); the repo bundles `BravuraText.woff2` | Reference for engraving glyphs if the renderer's hand-picked glyphs need replacing |

## How import and rendering work today

- **Import**: `MusicXmlScoreReader` (`PianoMapper.Core/Music/MusicXmlScoreReader.cs`) reads `score-partwise` only, exactly one `<part>`, up to two staves, and builds a `Score` (`Score.cs`): one title, **one** `TimeSignature`, **one** `Tempo`, **one** `KeyFifths`, and measures of `ScoreNote`/`ScoreRest`. `<staff>` 1/2 maps to `Staff.Treble`/`Staff.Bass`. Images go through Audiveris and `AudiverisMusicXmlNormalizer` first; hand-supplied MusicXML goes straight to the strict reader.
- **Strictness**: the measure loop ends in `default: throw Unsupported(name)` (`MusicXmlScoreReader.cs:244`), and every nested parser (`ParseAttributes`, `ValidateNoteElements`, `ValidateBarline`, `ParseTempo`) does the same.
- **Render**: `GrandStaffSceneBuilder` (`PianoMapper.Web/Rendering/`) builds a scene of lines, glyphs, notes, beams, ties, slurs and arpeggio marks that `canvas.js` draws.
  - Clefs are fixed treble over bass (`BuildClefGlyphs`, `GrandStaffSceneBuilder.cs:1600`); the source `<clef>` is skipped.
  - When a score uses both staves, the display staff is **derived from pitch**, not from `<staff>` (`GrandStaffSceneBuilder.cs:217-222`), so the drawn staff can differ from the source engraving.
  - Rests and ties are only drawn for generated exercises (`Piano.razor:2850`: `drawRests = sightReadingExerciseCoordinator.Score is not null`).

## Method

1. Extracted the content models of the key MusicXML types from the official 4.0 XSD, flattening nested groups and choices. Required/optional markers were read from the XSD separately where a claim depends on them.
2. Wrote a throwaway harness (a file-based .NET app outside the repo) that feeds minimal spec-valid MusicXML snippets to the real `MusicXmlScoreReader`: 73 probes across structure, notes, notations, attributes, directions, barlines and voices.
3. Downloaded the 18 official `.musicxml` samples and ran each through the reader.
4. Read the reader, `GrandStaffSceneBuilder`, `GrandStaffLayout`, the README and the existing plans for stated scope limits.

One of my own probes (beam hooks) initially failed because I wrote a wrong duration; I corrected and re-ran it. **Beam hook values are accepted**; the reader only looks at `<beam number="1">`.

## Findings

### 1. Valid MusicXML that rejects the whole file

Content-model coverage, from the XSD versus the reader:

| Parent | Children in schema | Recognized | Throw |
|---|---|---|---|
| measure content (`music-data`) | 14 | 7 (`note`, `backup`, `forward`, `direction`, `attributes`, `barline`, `print`) | `harmony`, `figured-bass`, `sound`, `listening`, `grouping`, `link`, `bookmark` |
| `attributes` | 14 | 5 (`divisions`, `key`, `time`, `staves`, `clef` skipped) | `part-symbol`, `instruments`, `staff-details`, `transpose`, `for-part`, `directive`, `measure-style`, `footnote`, `level` |
| `note` | 25 | 15 | `grace`, `unpitched`, `cue`, `instrument`, `notehead`, `notehead-text`, `play`, `listen`, `footnote`, `level` |
| `barline` | 9 | 2 (`bar-style`, `repeat`) | `ending`, `fermata`, `segno`, `coda`, `wavy-line`, `footnote`, `level` |
| `note-type-value` | 14 | 5 (whole to 16th) | 1024th to 32nd, breve, long, maxima |
| `articulations` | 17 | 4 (accent, staccato, tenuto, staccatissimo) | 13 others, e.g. `strong-accent`, `breath-mark`, `spiccato` |
| `ornaments` | 15 | 1 (`trill-mark`) | `turn`, `mordent`, `tremolo`, `shake`, `wavy-line`, ... |
| `accidental-value` | 41 | 6 | microtonal, arrow and `other` values |

Probe results for constructs a piano score can reasonably contain (all valid per the XSD):

| Construct | Reader result | Why / where |
|---|---|---|
| `<rest measure="yes"/>` with no `<type>` | `InvalidData`: "`<note>` requires `<type>`" | `type` is optional in the schema (`minOccurs=0`). `ParseNoteValue` requires it (`MusicXmlScoreReader.cs:1038`). The official `barline-multiple-coda` and `repeats-jumps` samples use exactly this form |
| Whole-measure rest with `<type>whole</type>` in 3/4 | `InvalidData`: duration mismatch | `ValidateDuration` compares duration to the note type, but a whole-measure rest's length is the measure's |
| `<slur type="stop"/><slur type="start"/>` on one note | `NotSupported`: "exactly one slur per note" | `ParseSlur` (`:735`). Ending one phrase and starting the next on the same note is routine |
| Two articulations (staccato + accent) | `NotSupported` | `ParseArticulation` (`:677`) |
| `trill-mark` + `wavy-line` | `NotSupported` | `ParseOrnament` (`:710`) |
| Measure-level `<sound tempo="40"/>` | `NotSupported`: "`<sound>`" | `sound` is a legal child of `<measure>`; the reader only handles it inside `<direction>`. **This is why the official Chopin sample fails** |
| `<sound dynamics="112"/>` in a direction | `NotSupported` | `ParseTempo` rejects every `sound` attribute except `tempo` (`:397-403`). Also in the Chopin sample |
| `<offset>` in a direction | `NotSupported` | not in `IgnoredDirectionElements` |
| `<harmony>` (chord symbol) | `NotSupported` | official `tutorial-chord-symbols` |
| Volta `<ending>`; barline `<fermata>`, `<segno>`, `<coda>`; `<sound dacapo>` | `NotSupported` | `ValidateBarline` (`:361`) |
| 32nd / 64th notes | `NotSupported` | official `beams-ties` samples |
| `<movement-number>` in the score header | `NotSupported` | `ValidateRootElements` allows `movement-title` but not its sibling |
| `<staff-details>`, `<part-symbol>`, `<measure-style>`, `<transpose>` | `NotSupported` | `ParseAttributes` default branch |
| `<fingering>3-2</fingering>` (finger change) | `InvalidData` | only a single digit 1-5 is accepted |
| `slur type="continue"`, `tied type="let-ring"`/`"continue"`, `octave-shift type="continue"` | `InvalidData` / `NotSupported` | the schema allows `continue` for slurs and ties |
| `<score-timewise>` root | `NotSupported` | the spec defines both roots |
| Two `<part>`s (RH and LH as separate parts) | `NotSupported` | documented limit |
| Additive meter `<beats>3+2</beats>`, `<senza-misura/>`, non-traditional key | `InvalidData` | `beats` is `xs:string` in the schema |

The documented limits (multipart, grace notes, tempo/time changes, `stem` `none`/`double`; `README.md:145`) were confirmed and are not repeated as news.

### 2. Accepted but silently wrong

| Construct | What happens | Evidence |
|---|---|---|
| **Key change mid-piece** | `case "key": keyFifths = ...` overwrites a single variable; the **final** key signature is drawn for the whole score. Time and tempo changes throw; key changes don't | probe A01: `score.KeyFifths=2` after a 0 → 2 change (`MusicXmlScoreReader.cs:332`) |
| **Pickup measure** (`<measure implicit="yes">`) | Measure length is never read, so a one-beat pickup becomes a full-length measure with the note at beat 0 | probe S06. `ScoreMeasure` has no duration; the effect on screen and in playback is inferred from that, not screenshotted |
| `print-object="no"` (hidden rests/notes) | Attribute never consulted, so hidden things are treated as real | probe S10: hidden rest parsed as a rest |
| `<metronome>` without `<sound tempo>` | Ignored; tempo becomes the 120 qpm fallback | probe D06 |
| Clef changes, C clefs, 8va clefs | Skipped; display staff chosen by pitch | probes A08, A09; `GrandStaffSceneBuilder.cs:217` |
| Time `symbol="common"`/`"cut"` | Not read; numerals are always drawn | probe A05; `AddTimeSignatureGlyphs` (`:2047`) |
| Score title | Always the file name; `<work-title>` and `<movement-title>` are not used | `MusicXmlScoreReader.cs:256` |

### 3. Parsed away and never drawn

All of these are accepted and dropped before the scene is built, so nothing can render them:

- `<direction-type>` content: `words` (tempo and expression text), `dynamics`, `wedge` (hairpins), `pedal`, `metronome`, `rehearsal`, `segno`/`coda`. Only `octave-shift` is used.
- `<lyric>`, `<dynamics>` inside `<notations>`, and `<print>` layout hints.
- Repeat barlines: accepted (probe B01) but not drawn, and playback is linear (`README.md:145`).
- In imported scores: **rests and ties** (`Piano.razor:2848-2851`, `README.md:153`). Voice numbers are not stored either; voices are inferred from `<chord/>` and onset (`docs/plans/chord-voice-identity-and-spacing.md`).

### 4. Renderer approximations (drawn, but not as engraved)

- **Secondary beams**: a beamed group draws `min(flag counts)` beams, and each note gets its remaining flags as individual flags (`GrandStaffSceneBuilder.cs:1668`, `:378`). A group of an eighth plus two sixteenths gets one beam with small flags on the sixteenths, not a partial second beam.
- **Rests**: only quarter and eighth rest glyphs exist (`GetRestGlyph`), even for generated scores. Half, whole and sixteenth rests would draw nothing.
- **Noteheads**: filled or hollow only; no `<notehead>` shapes.
- **Display staff**: pitch-derived, as above. This was a deliberate choice recorded in `.agents/lessons.md` (for OMR-derived scores, where hand assignment is not display staff), but it departs from what a faithful MusicXML file says through `<staff>` and `<clef>`.

### 5. Official sample results

18 files from the official repo, run through the reader:

| File | Result | First blocker |
|---|---|---|
| `tutorial-hello-world` | OK | none |
| `tutorial-chopin-prelude` (one-part, two-staff piano) | rejected | measure-level `<sound tempo>` and `<sound dynamics>`. **Imports fine (27 notes, key -3) once the two `<sound>` elements are stripped** |
| `tutorial-chord-symbols` | rejected | measure-level `<sound>`, then `<harmony>` |
| `tutorial-apres-un-reve` | rejected | two parts (voice and piano): documented limit |
| `tutorial-percussion`, `tutorial-tablature` | rejected | multiple parts: documented limit |
| `parts-groups` (+ `.invalid`) | rejected | `<staves>` above 2: documented limit |
| `beams-ties` (+ `.invalid`) | rejected | 32nd notes |
| `repeats-jumps` (+ `.invalid`), `barline-multiple-coda` | rejected | `<rest measure="yes"/>` without `<type>` |
| `accidentals` | rejected | non-traditional key |
| `accidental-element-multiple` | rejected | accidental value `other` |
| `harmonic-element` | rejected | `<harmonic>` (guitar technique, irrelevant here) |
| `voice-direction-element` | rejected | `<part-symbol>` |
| `rest-and-display-step-elements` | rejected | a fragment with no `<divisions>`; **not a fair test**, ignore |

These are mostly feature-demo files, not repertoire. The point is that one unknown element rejects a file that is otherwise fine.

## Proposed priority (a proposal, not a decision)

**Cheap fixes that unblock ordinary files**
1. Accept measure-level `<sound>` (read `tempo`, ignore the rest), `sound@dynamics`, `<offset>`, and `<movement-number>`.
2. Make `<type>` optional: derive the note value from `duration` and `divisions`, and treat `<rest measure="yes"/>` as the measure's length.
3. Allow more than one slur or articulation per note (the slur pairing already matches by `number`, so this mostly needs `ScoreNote` to hold a list).
4. Decide the key-change behavior now: support per-measure keys, or throw like time and tempo do. Silently drawing the wrong key signature is the worst current outcome.
5. Read `implicit`/measure length for pickup measures; honor `print-object="no"`.

**Structural: a non-fatal import-warnings channel.** Instead of choosing between "drop silently" and "reject the file", the reader could return a list such as "ignored: dynamics ×12, words ×3, clef change ×1". That would make the existing policy consistent and let presentation-only elements stop being fatal.

**Larger features, in the order a beginner-piano app likely needs them**
6. Draw rests and ties in imported scores (already tracked in `README.md:153`), then repeat signs and voltas.
7. 32nd notes; a few more ornaments (`turn`, `mordent`); dynamics and pedal marks as text or glyphs.
8. Time and tempo changes (needs `Score` to stop holding one `TimeSignature`/`Tempo`) and clef changes.
9. Two-part piano scores (RH and LH as separate parts), chord symbols.

## Caveats

- **No visual verification.** The renderer findings come from reading the code and the README. The memory notes describe a headless-Firefox screenshot recipe; I did not use it. Comparing the app's output for the Chopin sample (after the `<sound>` fix) against the spec's own engraving would be the natural next check.
- The probes are minimal hand-written snippets; "valid per spec" was checked against the XSD, not by running a schema validator.
- I did not verify what any specific exporter (MuseScore, Finale, Sibelius, Dorico) emits. Where this note says a construct is "ordinary", the evidence is the official samples and the schema, not exporter output.
- The probe harness lives in the session scratchpad and is not committed. Turning the failing-but-valid cases into `MusicXmlScoreReaderTests` would be the way to keep them.
