# Implementation Plan: Measure-Boundary Ties in Free Play

## Status

Implementation complete. Manual browser and physical-audio verification remains pending.

## Overview

Free Play currently represents one `PerformedNote` with one grand-staff layout. When a held note
crosses a barline—especially the boundary where the five-measure window advances—the original
onset falls outside the new score area. The temporary clipping safeguard keeps that onset out of
the clef/signature area, but it does not express the musical meaning.

The feature will keep the performed note and audio lifecycle continuous while splitting only its
visible notation at measure boundaries. Each visible fragment gets its own notehead, adjacent
fragments are connected by ties, and a fragment whose predecessor is outside the current
five-measure window gets an incoming tie stub at the left score edge. Releasing the key continues
to stop the original audio note once; it does not create extra audio events.

## Goals

- Render a Free Play note crossing a barline as measure-bounded note fragments connected by ties.
- Keep note-on, sustain, note-off, timeline retention, and piano-roll behavior unchanged.
- Keep every live notehead, ledger line, accidental, duration trail, and tie out of the
  clef/key/time-signature area.
- Handle both internal barlines and five-measure window rollover.
- Preserve current behavior for notes contained within one measure.
- Cover the behavior with deterministic .NET and JavaScript tests plus a browser checklist.

## Scope Boundaries

### Included

- Free Play grand-staff rendering for active and released `PerformedNote` instances.
- One or more measure-bounded visual fragments for a single performed note.
- Full ties when both fragments are visible and edge stubs when one fragment is offscreen.
- Existing supported rhythmic symbols plus dotted values needed for common measure fragments.
- 2–12 beat measures and the currently supported beat-note values.
- Chords and simultaneous notes, treated as independent performed-note identities.

### Deferred

- Drawing the `TiesToNext` data already present on imported `ScoreNote` instances. The new scene
  primitive should be reusable for that follow-up, but this feature will not expand imported-score
  behavior.
- Exact transcription/quantization of arbitrary human timing into several tied rhythmic values.
  Free Play will continue choosing the nearest supported value, now independently per fragment.
- Tuplets, voices, slurs, enharmonic respelling, tie editing, score export, and persistent recording.
- Keeping previous five-measure pages visible as score history.

## Architecture Decisions

1. **One performance event, multiple notation fragments.** `PerformedNote.StartTime` and
   `ReleaseTime` remain the source of truth. The renderer must never split the timeline event or
   restart audio at a barline.
2. **Split in absolute beat space.** Convert start/end times to absolute beats, intersect the note
   interval with the visible measure window, and split the intersection at measure boundaries.
   This keeps the logic independent of canvas size and works with every supported tempo and time
   signature.
3. **Use half-open intervals.** Treat fragments as `[startBeat, endBeat)` with a small comparison
   tolerance. A release exactly on a barline must not create a zero-length continuation.
4. **Keep automatic window rollover.** When the five-measure window advances, render the visible
   continuation at the first measure with an incoming tie stub. Freezing the old window would hide
   the current playback position and make long sustains stall navigation.
5. **Keep the scene declarative.** C# determines fragment/tie geometry; JavaScript only scales and
   draws the supplied primitives, matching the existing note/beam architecture.
6. **Preserve the existing public layout contract.** Add a segment-producing API and implement the
   existing single-layout API from the segment envelope where practical, avoiding an unnecessary
   source-breaking change.
7. **Approximate notation honestly.** Exact performance timestamps remain intact. Completed
   fragments choose the nearest supported note value, including dotted values; full rhythmic
   decomposition is a separate feature.

## Dependency Graph

```text
PerformedNote start/release times
            |
            v
measure-bounded live-note segment layout
            |
            v
GrandStaffNote fragments + GrandStaffTie scene primitives
            |
            v
canvas tie curves and edge stubs
            |
            v
Free Play browser behavior and learning copy
```

## Task 1: Add Measure-Bounded Live-Note Segmentation

**Description:** Add a layout result representing one visible fragment of a performed note and a
new `GrandStaffLayout` operation that returns all visible fragments. It will split in absolute beat
space, mark incoming/outgoing ties, and exclude zero-length or fully offscreen fragments. The
existing single-layout operation will remain compatible and reuse the new calculation rather than
maintaining separate boundary logic.

**Acceptance criteria:**

- [x] A note contained within one measure returns one fragment with no tie flags and the same
      onset/end coordinates as today.
- [x] A note crossing one or several internal barlines returns one fragment per crossed measure;
      adjacent fragments meet at the exact logical barline and have matching outgoing/incoming
      tie flags.
- [x] A note beginning before the current five-measure window returns only visible fragments, with
      the first marked as an incoming continuation and no coordinate before `ScoreX0`.
- [x] A release exactly on a barline produces no empty continuation fragment and no outgoing tie.
- [x] A note fully outside the visible window returns an empty collection.

**Verification:**

- [x] First prove each new boundary test fails against the current single-layout behavior.
- [x] Run `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release --filter "FullyQualifiedName~GrandStaffLayoutTests"`.
- [x] Run `rtk git diff --check` for the touched files.

**Dependencies:** None.

**Files likely touched:**

- `PianoMapper.Core/Rendering/LiveNoteSegmentLayout.cs` (new)
- `PianoMapper.Core/Rendering/GrandStaffLayout.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffLayoutTests.cs`

**Estimated scope:** Medium (3 files).

## Task 2: Add a Tie Primitive to the Grand-Staff Scene

**Description:** Add an immutable `GrandStaffTie` scene record and a default-empty tie collection
on `GrandStaffScene`. The primitive should contain normalized endpoints, its staff-relative Y
anchor/curve direction, and whether it belongs to an actively held note. Update selected-octave
fitting so tie Y coordinates are transformed with notes, ledgers, glyphs, and beams while X
coordinates remain unchanged.

**Acceptance criteria:**

- [x] Existing scene constructors continue to work without specifying ties.
- [x] A scene can carry full ties and viewport-edge tie stubs without encoding them as generic
      staff lines.
- [x] `FitToSelectedOctave` keeps every tie inside the transformed vertical viewport and preserves
      its X endpoints.
- [x] Score scenes and piano-roll scenes remain unchanged when the tie collection is empty.

**Verification:**

- [x] Run `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release --filter "FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~PianoTests"`.
- [x] Run `rtk dotnet build PianoMapper.slnx --configuration Release`.

**Dependencies:** Task 1 defines the fragment/tie semantics consumed later.

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffTie.cs` (new)
- `PianoMapper.Web/Rendering/GrandStaffScene.cs`
- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Medium (4 files).

## Checkpoint: Foundation

- [x] Tasks 1–2 acceptance criteria pass.
- [x] The solution builds without warnings or errors.
- [x] Existing one-measure live-note rendering is unchanged.
- [x] Review the segment and scene contracts before connecting browser rendering.

## Task 3: Build Tied Free Play Notation from Segments

**Description:** Change the live branch of `GrandStaffSceneBuilder.Build` to create one
`GrandStaffNote` per visible fragment and the required ties. A tie between two visible fragments
uses both notehead positions; a missing predecessor/successor uses a short edge stub. Logical
fragment boundaries remain on barlines, while noteheads receive the existing small measure-edge
clearance so they do not collide with barlines or signatures.

Completed fragments will use standard notation based on their own duration. Only the unfinished
tail of an actively held note keeps the cyan active marker and growing duration trail. Continuation
fragments will not repeat an accidental because no new pitch attack occurred. Common dotted
values will be added to the nearest-value candidates so, for example, a three-beat 4/4 fragment
can appear as a dotted half note.

**Acceptance criteria:**

- [x] A released note crossing an internal barline produces two completed noteheads and one full
      tie, with each notehead inside its measure.
- [x] While the key remains held, completed earlier fragments use closed notation and only the
      final fragment has an active duration trail.
- [x] After five-measure rollover, the visible continuation begins inside the first measure with an
      incoming tie stub; no note-owned element appears in the signature area.
- [x] Releasing after rollover changes the continuation to completed notation without moving it
      into the clef/signature strip.
- [x] A tied accidental pitch shows the accidental at the original attack only, not at every
      continuation.
- [x] Notes, chords, and the piano roll retain their original `PerformedNote` identity and timing.

**Verification:**

- [x] Add failing builder tests for internal crossing, active crossing, rollover continuation,
      exact-boundary release, accidental continuation, dotted duration, and selected-octave fit.
- [x] Run `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release --filter "FullyQualifiedName~GrandStaffLayoutTests|FullyQualifiedName~GrandStaffSceneBuilderTests|FullyQualifiedName~PianoRollSceneBuilderTests"`.
- [x] Confirm `BrowserKeyboardStateTests` and `NoteTimelineTests` pass unchanged, proving the input
      and performance event lifecycle was not split.

**Dependencies:** Tasks 1 and 2.

**Files likely touched:**

- `PianoMapper.Web/Rendering/GrandStaffSceneBuilder.cs`
- `PianoMapper.Tests/UnitTests/GrandStaffSceneBuilderTests.cs`

**Estimated scope:** Medium (2 files, behavior-heavy).

## Task 4: Draw Ties on the Canvas

**Description:** Teach `canvas.js` to render ties as staff-space-scaled curved strokes. Draw ties
inside the existing note-owned clipping region and behind noteheads so endpoints meet cleanly.
Curve height and line width must derive from staff spacing, not canvas width, preserving appearance
on narrow and wide layouts. Edge stubs use the same primitive and stop at the score boundary.

**Acceptance criteria:**

- [x] A full tie renders as a curve between supplied notehead endpoints.
- [x] Incoming/outgoing stubs render as partial curves and remain inside the score area.
- [x] Tie curvature scales with staff spacing and is stable across canvas sizes.
- [x] Ties follow active/finished coloring without changing notehead, beam, ledger, or cursor draw
      behavior.
- [x] Scenes without ties produce the same canvas operations as before.

**Verification:**

- [x] Extend the fake canvas context to record curve calls and assert endpoints, direction,
      clipping, and scale.
- [x] Run `rtk node --test PianoMapper.Tests/JavaScript/canvas.test.mjs`.
- [x] Run `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`.

**Dependencies:** Tasks 2 and 3 establish the scene contract and geometry.

**Files likely touched:**

- `PianoMapper.Web/wwwroot/js/canvas.js`
- `PianoMapper.Tests/JavaScript/canvas.test.mjs`

**Estimated scope:** Small (2 files).

## Checkpoint: End-to-End Rendering

- [x] Focused .NET and JavaScript suites pass.
- [ ] In 4/4 Free Play, hold a note from the final beat of one internal measure into the next and
      confirm two heads, one tie, and uninterrupted sound.
- [ ] Hold a note across the five-measure rollover and confirm the new page shows an incoming tied
      continuation rather than a note in the signature area.
- [ ] Release exactly at a barline and confirm there is no duplicate or zero-length note.
- [ ] Repeat with a sharp/flat, a chord, 3/4, piano and synth, and both narrow and wide viewports.
- [ ] Switch to piano roll and confirm it still shows one continuous bar for the performed note.

## Task 5: Explain and Record the New Behavior

**Description:** Update the built-in notation help and the browser regression matrix. The help
should explain that a tie joins two written notes into one uninterrupted sound and that an edge
stub means the attack occurred on the previous/next visible page. Documentation must not claim
that Free Play performs exact transcription of unquantized timing.

**Acceptance criteria:**

- [x] The grand-staff help explains measure-boundary ties in plain language.
- [x] The manual test matrix includes internal barline, window rollover, exact-boundary,
      accidental, chord, time-signature, sound-source, responsive-layout, and piano-roll checks.
- [x] Existing run/build instructions remain accurate; no new configuration is introduced.

**Verification:**

- [x] Review text against the implemented scene behavior.
- [x] Run `rtk dotnet build PianoMapper.slnx --configuration Release` after the Razor change.
- [ ] Manually execute and record the new browser test-matrix rows.

**Dependencies:** Tasks 3 and 4 must establish final behavior and terminology.

**Files likely touched:**

- `PianoMapper.Web/Components/PianoCanvas.razor`
- `docs/browser-test-matrix.md`

**Estimated scope:** Small (2 files).

## Final Verification

- [x] `rtk dotnet build PianoMapper.slnx --configuration Release`
- [x] `rtk dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release`
- [x] `rtk node --test PianoMapper.Tests/JavaScript/*.test.mjs`
- [x] `rtk git diff --check`
- [x] Confirm every changed line traces to Free Play tie rendering, tests, or user-facing help.
- [ ] Confirm the browser matrix passes with no audio retrigger at any barline.
- [x] Confirm the temporary onset-clamping safeguard is either retained as a compatibility guard
      or replaced only after the segment tests cover its rollover case.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Automatic rollover hides the original notehead | Medium | Show an incoming tie stub and continuation head at the new window's first measure; do not pretend it is a new attack. |
| Arbitrary key timing has no exact single notation value | Medium | Preserve exact timestamps, choose the nearest per-fragment value including dotted values, and defer multi-value rhythmic decomposition. |
| A release exactly on a barline creates a duplicate fragment | High | Use half-open intervals plus tolerance and add exact-boundary tests before implementation. |
| Active segment state produces several cyan trails | High | Mark only the unfinished tail as active; earlier fragments are completed notation. |
| Accidentals repeat on tied continuations | Medium | Render accidentals only for the original attack and add sharp/flat regression tests. |
| Tie curves overlap barlines, labels, or noteheads | Medium | Apply measure-edge notehead clearance, draw ties behind heads, scale by staff space, and test narrow/wide canvases. |
| Per-frame segment allocation affects Free Play refresh | Low | Generate only fragments intersecting the five visible measures; measure before considering caching or pooling. |
| Generic tie work accidentally expands imported-score scope | Medium | Keep `BuildStaticScoreParts` behavior unchanged in this feature; add imported-score rendering as a separate approved plan. |

## Open Decisions Requiring Approval

1. **Window rollover:** Recommended—keep automatic rollover and show an incoming continuation tie
   stub. Alternative—freeze the old five-measure page until release, which can hide the current
   cursor and stall long sustains.
2. **Rhythmic precision:** Recommended—nearest existing value plus dotted values for this feature.
   Exact quantization/decomposition into multiple values should be planned separately.
3. **Imported scores:** Recommended—make the tie primitive reusable but defer rendering existing
   `ScoreNote.TiesToNext` data, keeping this change focused on Free Play.

## Parallelization

Tasks 1–4 are dependency-ordered and should be implemented sequentially. Task 5 documentation can
start after Task 3 defines final semantics, but its manual matrix cannot be completed until Task 4
is rendered in the browser. No shared-contract work should be parallelized before Task 2 is
reviewed.
