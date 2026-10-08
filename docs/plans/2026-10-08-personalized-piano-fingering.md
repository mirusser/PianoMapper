# Implementation plan: personalized piano fingering

Status: implemented and reviewed on 2026-10-08; results are in [the validation report](../personalized-piano-fingering-validation.md). Still open: the learner trying the generated alternatives (Checkpoint B) and measuring comfortable reach for each hand.

## Outcome and confirmed context

Improve fingering suggestions for the user's **C4–D5 reach: a major ninth, 14 semitones**. The user confirmed the interval, but did not confirm whether it is comfortable or maximum reach, or whether both hands have identical reach. Treat it as the reported upper reach when proposing a profile; do not silently label it comfortable or infer other finger-pair limits from it.

Keep the existing deterministic dynamic-programming search. Combine personalized feasibility constraints, ergonomic costs, and sustained-note state. Evaluate this baseline before adding learned preferences.

## Repository handoff

- Follow root `AGENTS.md`, `.agents/lessons.md`, and the repo-onboarding, code-standards, writing-tests and run-tests skills. Prefix shell commands with `rtk`. Recheck graph health and callers before implementation.
- Core: `PianoMapper.Core/Music/ScoreFingeringGenerator.cs`; entry point `Generate(Score)`; important methods `CreateGroups`, `CreateCandidates`, `GetChordCost`, `GetTransitionCost`, `ApplyFingering`.
- Related models/editor: `ScoreNote.cs`, `ScoreFingering.cs`, `ScoreFingeringEditor.cs` in the same directory. `ScoreFingering` currently contains only number and placement; `ScoreNote` carries ties and duration, but no separate voice identifier.
- Consumers: `PianoMapper.Web/Pages/Piano.razor` and `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs` (`Generate` and `RetryMissed`). Changes must work for imported scores and generated exercises.
- Tests: `PianoMapper.Tests/UnitTests/ScoreFingeringGeneratorTests.cs`, `ScoreFingeringEditorTests.cs`, and `SightReadingExerciseComposerTests.cs`.
- Current behavior: independent hands, same-onset chord groups, fixed mirrored finger offsets, finite stretch penalties, nearest-pitch transitions, no active held-finger state, no tempo-dependent movement cost. Regeneration intentionally replaces existing numbers. Existing tests cover C-major scales, triads, unisons and replacement behavior.

## Design decisions and boundaries

1. Separate comfortable ranges from absolute modeled limits, per hand and finger pair. Use configurable generic defaults for unmeasured pairs, explicitly distinguished from user measurements. Express reach using consistent keyboard geometry; C4–D5 is eight white-key center spacings in the current coordinate system. Do not apply a universal 14-semitone cap to every finger pair or every key combination.
2. Enforce reach on simultaneously depressed keys, including notes started earlier. Released-hand leaps may exceed the static span and receive movement/time costs instead.
3. Build event states from note starts and ends; retain assignments for held notes. Merge tie chains into one held event and map the result back to each score note. At coincident boundaries, release ended notes before assigning new attacks, while preserving ties.
4. Use hard constraints for modeled feasibility and explicit locks; soft costs choose among feasible paths. Preserve deterministic tie-breaking. When no valid path exists, report the location/reason without partially replacing the score.
5. Keep existing hand assignments and pitches. Do not silently roll chords, redistribute hands, remove notes, shorten durations or assume pedal use. Silent finger substitution, joint hand assignment and learned models are deferred.
6. Personal preferences belong in the existing user/settings mechanism if one exists; do not bake this user's reach into global engine defaults. Preserve `Generate(Score)` compatibility while adding an explicit options/profile path.

## Ordered tasks

### Task 1: Enforce a configurable reach profile

**Description:** Add the smallest profile/options contract needed to distinguish comfort from maximum span; reject invalid same-onset candidates and handle an empty candidate set explicitly.

**Acceptance criteria:**
- [x] Separate left/right and finger-pair limits validate consistently; comfortable limits cannot exceed maximum limits. Legacy callers retain a documented default profile.
- [x] An explicitly configured C4–D5 1–5 limit accepts that boundary and rejects a wider simultaneous span; a narrower configured 2–5 limit is also enforced. Excessive stretch cannot win merely because every other candidate is worse.
- [x] Infeasible input produces a meaningful measure/note diagnostic and leaves the original immutable score intact.

**Verification:** Focused generator/profile tests for boundaries, invalid profiles, both hands, black/white-key geometry and impossible chords; build Core.

**Dependencies:** None. **Scope:** M, 4–5 files. **Likely files:** generator, new profile/options types under Core/Music, generator tests and profile tests.

### Task 2: Respect sustained notes and ties

**Description:** Replace onset-only states with active-note event states, including held finger assignments in the DP state so histories with different occupied fingers are not incorrectly merged.

**Acceptance criteria:**
- [x] A finger cannot play another key while its previous note remains held; reach constraints include all active notes, even across intervening groups/measures.
- [x] Ties preserve one key press/finger throughout the chain; simultaneous releases, attacks and doubled unisons behave consistently. Tie matching does not conflate unrelated same-pitch events.
- [x] Released-hand leaps beyond a ninth remain possible; blocked sustained passages return the diagnostic from Task 1.

**Verification:** Focused cases for unequal-duration chords, a long note under several shorter notes, cross-bar ties, shared onset/end boundaries and released leaps; retain existing scale/triad regressions. Measure candidate growth on a dense passage.

**Dependencies:** 1. **Scope:** M, 3–5 files. **Likely files:** generator, an internal event/state helper if necessary, generator tests and a small fixture/helper file.

### Checkpoint A

- [x] Core tests and Release build pass; every returned path satisfies the configured constraints.
- [x] No silent fallback to an invalid path; deterministic output and acceptable candidate counts are demonstrated.

### Task 3: Expose the profile through actual user flows

**Description:** Add compact hand-profile controls and route the selected profile to score regeneration and exercise generation/retry. Inspect existing settings conventions before choosing persistence; split persistence into a separate small task if this exceeds five files.

**Acceptance criteria:**
- [x] The user can configure each hand's comfortable/maximum reach; C4–D5 is labeled clearly. Unconfirmed comfort and symmetry are not presented as measured facts.
- [x] Imported-score regeneration, new exercises and retries use the same selected profile; impossible passages show a useful message without replacing the current score.
- [x] Settings follow the existing storage convention and reload correctly; old/missing settings remain valid.

**Verification:** Focused settings/coordinator tests; manually regenerate a boundary chord and an oversized chord, then generate/retry an exercise and reload settings.

**Dependencies:** 1–2. **Scope:** M, up to 5 files. **Likely files:** Piano.razor, SightReadingExerciseCoordinator.cs, existing settings owner, and corresponding tests.

### Task 4: Improve ergonomic and timing costs

**Description:** Replace rigid preferred finger offsets as the primary stretch model with documented finger-pair comfort costs. Add contextual key-color/crossing costs and movement costs using available time. Keep the hard constraints from Tasks 1–2 decisive.

**Acceptance criteria:**
- [x] Comfortable shapes score better than near-limit stretches where musical context is otherwise comparable; thumb use on black keys is contextual, not universally forbidden.
- [x] Movement cost reflects tempo/time available, without treating a legal leap as an impossible static stretch. Staccato/slur information only affects supported, documented behavior.
- [x] Representative scales in multiple keys, arpeggios and repeated notes remain coherent; any added multi-note cost is supported by sufficient DP history rather than invalidating optimality assumptions.

**Verification:** Behavioral comparisons on fixed short passages, both hands and slow/fast versions; review changed conventional fingerings and runtime. Avoid tests that merely reproduce a cost formula.

**Dependencies:** 2–3. **Scope:** M, 3–5 files. **Likely files:** generator, focused ergonomic cost helper, generator tests and fixtures.

### Checkpoint B

- [x] Profile works end-to-end and existing fast tests pass.
- [ ] User tries a small set of generated alternatives to assess comfort; record preference as feedback, not proof of biomechanical accuracy. This feedback is needed for personal calibration, not for unrelated implementation work.

### Task 5: Honor explicit fingering locks in the engine

**Description:** Allow callers to pin selected note/finger assignments while optimizing the surrounding passage. Distinguish explicit locks from imported or previously generated numbers.

**Acceptance criteria:**
- [x] Locked notes retain their fingers, including tied continuations; invalid or contradictory locks return actionable diagnostics.
- [x] Unlocked notes remain optimizable; existing explicit “regenerate all” behavior remains available.
- [x] Pin identifiers map unambiguously to score notes and do not leak across different loaded scores.

**Verification:** Tests for interior anchors changing surrounding choices, conflicting chord/tie locks, invalid addresses and unchanged source scores.

**Dependencies:** 2, 4. **Scope:** M, 3–4 files. **Likely files:** options/lock contract, generator and generator tests.

### Task 6: Let the user preserve corrections

**Description:** Add explicit lock/unlock interaction to the existing fingering editor and pass locks into regeneration. Start with clearly scoped session locks; durable score-lock storage is deferred to a separate schema/serialization task if requested.

**Acceptance criteria:**
- [x] A corrected note can be locked, displayed as locked, and preserved during regeneration; unlock restores normal optimization.
- [x] “Regenerate all” and “preserve locked fingerings” have clear behavior; score replacement/removal clears obsolete locks.
- [x] Failure leaves both the score and locks intact and identifies the conflicting location.

**Verification:** Focused interaction/state tests and manual edit → lock → regenerate → unlock flow.

**Dependencies:** 3, 5. **Scope:** M, 3–5 files. **Likely files:** Piano.razor, existing edit state/component or a small dedicated helper, related tests.

### Checkpoint C

- [x] Locks, profiles and held-note constraints work together; fast tests and Release build pass.
- [x] Existing imported fingering placement and score serialization are unaffected.

### Task 7: Offer a few coherent alternatives

**Description:** Extend the search to return up to three distinct feasible passage fingerings with short cost-based explanations; retain the best result as the default.

**Acceptance criteria:**
- [x] Every alternative honors the same reach limits, held notes and locks; return fewer than three when appropriate.
- [x] Ranking is deterministic; alternatives are coherent paths, not independent per-note substitutions. Explanations describe modeled tradeoffs, not calibrated confidence percentages.
- [x] The user can preview/select an alternative without accidental score mutation; cap work to avoid freezing the UI. (There is no separate read-only preview: choosing an alternative is an explicit click that updates the editor, can be switched back, and is not saved until Save. Work is capped at three alternatives and is linear in passage length.)

**Verification:** Ranking/uniqueness/feasibility tests and manual preview/selection on a multi-measure passage; compare runtime with the single-result baseline.

**Dependencies:** 4–6. **Scope:** M, up to 5 files. **Likely files:** generator, result contract, Piano.razor or a dedicated component, generator tests and UI-state tests.

### Task 8: Validate quality and document limitations

**Description:** Compare the original baseline and new behavior on a small representative corpus. Update README/configuration documentation to describe profiles, locks, alternatives and unsupported techniques.

**Acceptance criteria:**
- [x] Corpus covers both hands, black keys, scales/arpeggios, octaves/ninths, overlapping voices, ties, repeated notes, impossible spans and manual locks.
- [ ] Report constraint violations, user preferences, movement/stretch tradeoffs and runtime; do not equate agreement with one published fingering with correctness.
- [x] All supported entry points are checked and documentation matches actual behavior. Remaining limitations are explicit.

**Verification:** Focused and full fast tests, Release build, manual representative passages, and verify-readme-docs. Add a short results document with commands and observed outcomes.

**Dependencies:** 1–7. **Scope:** M, 3–5 files. **Likely files:** generator tests/fixtures, README.md, existing configuration docs if applicable, a new validation report.

## Verification commands and completion checkpoint

Read run-tests before execution and use its prerequisite/category guidance. These commands are planned, not run during this investigation:

```bash
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter 'FullyQualifiedName~ScoreFingeringGeneratorTests'
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter 'Category!=Integration&Category!=LiveApi'
rtk proxy dotnet build PianoMapper.slnx --configuration Release
```

- [x] Expand the focused filter to include new profile/lock/coordinator tests as they are added.
- [ ] All task acceptance criteria are met, required checks pass, and remaining personal-calibration questions are reported honestly.
- [x] Tasks are sequential where they share the generator or Piano.razor. Independent fixture/document review can be delegated once contracts stabilize; avoid concurrent edits to shared files.

## Deferred work and research references

Only after evaluating the deterministic baseline, consider learned ergonomic weights or an HMM likelihood term inside constrained search. A learned scorer must never bypass physical constraints or locks. Neural models/RL, automatic hand redistribution, silent substitutions and pedal-aware release modeling are separate future work.

- [Parncutt et al. (1997)](https://static.uni-graz.at/fileadmin/_Persoenliche_Webseite/parncutt_richard/Pdfs/PaSlClRaDe97_FingeringModel.pdf): relaxed/comfortable/practical finger-pair spans; original scope is melodic fragments.
- [Al Kasimi et al. (2007)](https://ericpnichols.com/papers/KasimiNicholsRaphaelAutomaticFingering.pdf): closest structural precedent, note-start/end event states and personalized costs.
- [Balliauw et al. (2017)](https://www.dorienherremans.com/sites/default/files/ITOR_VNS_APF_preprint.pdf): polyphonic ergonomic rules; its reach penalties are soft, so do not inherit them as feasibility guarantees.
- [Nakamura et al. (2020)](https://arxiv.org/abs/1904.10237): optional statistical baseline. [PIG dataset](https://beam.kisarazu.ac.jp/research/PianoFingeringDataset/) has multiple human annotations but restricts use to nonprofit academic purposes; do not assume unrestricted training/redistribution.
- [Pydactyl](https://github.com/dvdrndlph/pydactyl): MIT reference for span tables, explainable rules and ranked suggestions; not a complete sustained-polyphony solution.
- [PianoPlayer](https://github.com/marcomusy/pianoplayer): MIT reference for timing/movement costs and anchors; hand presets are not personal maximum-reach measurements.
- [Chirality](https://github.com/vibetuned/chirality): experimental learned-rule weighting plus DP; AGPL-3.0 code, separately restricted PIG-derived artifacts, self-reported benchmarks. Prefer independently implemented paper-based rules unless reuse terms are suitable.
- [MusicXML fingering](https://www.w3.org/2021/06/musicxml40/musicxml-reference/elements/fingering/): representation of alternatives/substitutions, not a standard generation algorithm.

## Remaining questions and risks

- Confirm comfortable versus maximum C4–D5 reach and any left/right difference before presenting a calibrated personal profile. Core implementation and configurable controls can proceed without inventing those measurements.
- Finger-pair tables and keyboard coordinates approximate playability; a ninth measurement does not validate every chord shape. Evaluate with the user's own passages.
- Active-note state can increase search complexity. Prune invalid states early, preserve distinct held assignments, and measure before adding beam pruning or approximation.
- Correctly retain musical durations and tie continuity without assuming pedal support. Diagnose impossible score/hand assignments rather than silently changing the music.

The implementation, its review and the measured results are recorded in the validation report; the unchecked items above need the learner, not more code.
