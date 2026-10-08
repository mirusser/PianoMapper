# Personalized fingering validation

Validated on 2026-10-08, after a review of the first implementation, a follow-up on the default reach, long scales and the reach boxes, and a test audit that cut the suite to the tests that pin distinct behaviour. Plan: [personalized piano fingering](plans/2026-10-08-personalized-piano-fingering.md).

## Commands run

```bash
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter 'FullyQualifiedName~Fingering'
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter 'FullyQualifiedName~ScoreFingeringGeneratorTests'
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ScoreFingering|FullyQualifiedName~FingeringProfile|FullyQualifiedName~BrowserFingeringProfileStore|FullyQualifiedName~ScoreNoteAddressResolver'
rtk proxy dotnet build PianoMapper.slnx --configuration Release
rtk proxy dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --configuration Release --no-build --filter 'Category!=Integration&Category!=LiveApi'
rtk proxy node --test PianoMapper.Tests/JavaScript/*.test.mjs
```

| Check | Result |
| --- | --- |
| `FullyQualifiedName~Fingering` (Debug) | 185 passed (705 before the test audit) |
| `ScoreFingeringGeneratorTests` | 20 passed (32 before the audit, 25 before the follow-up, 16 before the review) |
| `ScoreFingeringScaleTests` | 32 passed (432 before the audit) |
| All fingering, profile, store and address tests | 154 passed (677 before the audit, 222 before the follow-up), plus 2 coordinator tests (`*_ChordsWithTheSelectedProfileTooNarrow_*`) |
| Release fast suite | 2,254 passed in about 2.7 s wall clock (2,778 and 3.2 s before the audit, 2,323 before the follow-up, 2,133 before the review) |
| JavaScript suite | 141 passed (142 before the audit, 137 before the review) |
| Release build | succeeded, 20 warnings, all the existing `NU1902`/`NU1903` ImageSharp advisories |

## What the tests cover

- Reach: different limits per hand, enforced independently; under the default profile C4–D5 (8 spacings) and C4–E5 (9) are accepted and C4–F5 (10) is rejected (right hand; left hand E5 and F5); a narrower 2–5 limit; black keys at either end of the span; thumb on a black key in a five-note chord; a narrow thumb-to-little-finger reach also bounds every other finger pair; invalid reach, pair and profile values.
- Comfort: a sixth uses thumb and little finger by default and a different shape when that pair is configured as uncomfortable; faster tempo makes the same hand movement cost more.
- Sustained notes: a long note under shorter ones, an unequal-length chord whose released finger is free for the next attack (including a note ending exactly where the next begins), holds and ties across a barline, unrelated same-pitch notes not merged into a tie, doubled unisons of different lengths, a key re-attacked while held (re-struck by the finger already on it), a sixth key needed while five are held, released leaps beyond any static reach.
- Locks: an interior anchor changes the neighbouring choices, a lock on the second tied note pins the chain, contradictory chord, unison and tie locks, locks that cross fingers or conflict with a held finger (each reports the conflicting note), addresses outside the score, both hands, repeated identical locks. The source score is left unchanged on success (scale tests) and on failure (coordinator tests).
- Alternatives: up to three distinct paths in cost order, the first equal to the single-path result, a single path when only one is feasible, locks and reach honoured by every alternative, repeated calls identical.
- Scales (`ScoreFingeringScaleTests`): one-note-per-beat scales of 15 and 22 notes in C and F major, both hands, ascending and descending, at 60 and 200 beats per minute (32 runs; the 8-note scale is covered by the generator tests). Instead of fixed strings the tests check the rule every scale fingering follows: moving away from the thumb the fingers climb one at a time and the thumb passes under finger 3 or 4; moving toward it the fingers fall one at a time and finger 3 or 4 crosses over; consecutive thumbs are then three or four notes apart.
- Corpus (`ScoreFingeringCorpusTests`): 6 passages (repeated notes, an arpeggio, an Alberti bass, a black-key run, triads in both hands, octaves) under three profiles, plus the W3C `accidentals` sample and the repository's `mia_sebastians_theme` score under the default profile, and the linear-time guard. An independent validator (`FingeringConstraintValidator`, written against the score and profile only) checks every path: no crossing, one finger per key, no finger on two keys, every pair within its maximum, tied notes on one finger.
- Web: settings parsing and storage (corrupt, partial, `{}` and out-of-range values fall back one hand at a time), the edit of one reach box (accepted, unparsable or out-of-range text, comfortable and maximum out of order; a rejected edit returns the last accepted values), the browser store with a fake JS runtime (stored value, storage disabled, module missing, write refused), the JS storage module, the session lock/alternative state, the mapping from a displayed note to the score note that generation reads (the display can be re-barred), and exercise generation and **Retry missed** using the selected profile (a too-narrow profile throws and keeps the current score).

## Defaults and long scales

- **Default reach.** The thumb-to-little-finger maximum is 9 white-key spacings for both hands (comfortable 6.5, unchanged), up from 8. Spacings are white-key centre distances: C4 to D5 is 8 (a ninth by interval name) and C4 to E5 is 9 (a tenth), so C4–D5 was already inside the old default and the new one leaves a spacing of headroom. Every other finger pair keeps a comfortable 5 and is capped at 7 (an octave, up from 6.5), never above the 1–5 maximum. Raising only the 1–5 maximum to 9 was not enough for the `accidentals.musicxml` chord: G3 to G4 is 7 spacings and has to sit between the thumb and a middle or ring finger, which a 6.5 cap refused. Values already saved in the browser are kept as saved.
- **Scale fix.** A 15-note scale came out as `1 2 1 2 1 2 …` in the original and the staged generator (66 and 56 of 84 scale runs broke the rule above). The movement cost is the square of the hand's shift, which is convex, so one conventional thumb pass (a shift of 3 to 4 keys) cost more than several short passes under finger 2 covering the same distance; a thumb pass under finger 2 or 5 cost the same as under 3 or 4; and the start and end preferences (begin on 1, end on 5) hid all of this in an 8-note scale. A thumb pass now costs a fixed 0.35 (independent of tempo, reduced after a rest) plus a smaller distance term (weight 0.01, was 0.035), and passing under or over finger 2 or 5 costs 0.3 more. A thumb move that does not change finger no longer counts as a pass, as in the original generator. The six keys of the original test matrix (C, G, D, A, E and F major) stay legal (0 of 576 runs break the rule) at 40 to 240 beats per minute and 8 to 29 notes for pass costs of 0.3 to 0.4 with the distance weight at 0.005 or 0.01 (0.35 to 0.4 at 0.02); with the old weight of 0.035 60 of 576 still broke it even with the fixed cost, a pass cost that scaled with tempo broke at 40 and 60 beats per minute, and without the extra cost for finger 2 or 5 between 6 and 48 of 84 runs broke it, depending on the other values.
- **Still imperfect.** Across 12 major keys and 6 tempos 110 of 1,152 scale runs still break the rule, all left-hand scales of 22 or more notes in the black-key-heavy flat keys (A flat, D flat, E flat), more at 40 and 240 beats per minute: a finger is skipped or repeated at the very start or end of the run (`5 4 2 1 …`, `… 1 2 4 5`) because the start and end preferences outweigh a skip that costs almost nothing; the model has no cost for skipping a finger on a step. Thumbs also still land on black keys (the thumb-on-black cost is 0.1), for example in F major. Neither is covered by the tests.

## Runtime

Release build, one thread, native .NET. A passage of eighth notes in each hand; figures in brackets add a triad to the right hand on every fourth step.

| Notes per hand | Staged code, 1 / 3 paths | Now, 1 / 3 paths | Original generator |
| --- | --- | --- | --- |
| 500 | 0.47 s / 2.6 s (0.77 s / 4.4 s) | 0.06 s / 0.13 s (0.10 s / 0.23 s) | 0.02–0.03 s |
| 1,000 | 1.8 s / 10.6 s (3.0 s / 17.5 s) | 0.07 s / 0.16 s (0.07 s / 0.15 s) | 0.02–0.05 s |
| 2,000 | 7.1 s / 42 s (11.9 s / 70 s) | 0.08 s / 0.18 s (0.12 s / 0.27 s) | 0.02–0.03 s |

The staged search copied the whole path at every step and built a path string to break ties, so it was quadratic; the path is now a linked chain and ties go to the path found first. Search size on a dense passage (a held half note and quarter note under moving eighths) stays flat: at most 10 states per event with one path and 30 with three, at most 10 candidates per attack, about 32 and 95 transitions per event. A guard test fails the suite if 1,500 notes per hand take more than 10 s (0.8 s in the Release run). The "Now" column was measured again after the follow-up's cost and default changes: the figures are of the same size and still grow linearly (6,000 notes per hand: 0.25 s / 0.58 s, or 0.39 s / 0.89 s with the triads). The states and candidates per event are fixed by the reach limits, not the costs, and the dense passage never needs the wider limits, so its search size is as above.

## Samples and exercises

- `mia_sebastians_theme_ivanovskaya_transcription.musicxml` (150 notes, 0.04 s, in the corpus test) and `voice-direction-element.musicxml` have a key attacked again while the same key is held. The staged code reported them infeasible, so **Regenerate all fingerings** failed on the repository's own score; they now generate.
- `accidentals.musicxml` now generates under the default profile (it was reported at measure 5, note 3: a four-note G3 F4 G4 B4 chord in the right hand, nine white-key spacings from G3 to B4). It is in the corpus test. This is a behaviour change from the original generator, which ignored reach.
- 3,528 generated exercises (every staff, preset, rhythm, motion, grand-staff and hands-together combination the composer accepts, 16 prompts, four seeds each) all generate under the default profile, re-run after the follow-up. A zero-reach profile fails 336 of them, all Chords exercises, which the page reports without replacing the current exercise.

## Test audit

The suite grew to 705 fingering tests (2,778 in the fast suite), 432 of them one scale matrix. A test was kept only if it pins a distinct behaviour or contract that no other kept test already fails on. Release, same machine, fast suite: 2,778 tests and about 3.2 s wall clock before, 2,254 and about 2.7 s after (most of what remains is test-host start-up and the 1,500-note linear-time guard). JavaScript: 142 to 141.

| Test class | Before | After | Kept |
| --- | --- | --- | --- |
| `ScoreFingeringScaleTests` | 432 | 32 | the finger-order rule on 15- and 22-note scales; the thumb-gap test was implied by it |
| `ScoreFingeringCorpusTests` | 49 | 9 | 6 passages, `accidentals` and `mia_sebastians_theme`, the linear-time guard |
| `ScoreFingeringProfileTests` | 28 | 15 | validation of reaches, pair limits and options; the clamp of other pairs |
| `FingeringProfileSettingsTests` | 37 | 12 | default mapping, per-hand mapping, `TryEditReach` accept, reject and keep |
| `FingeringProfileSettingsJsonTests` | 18 | 9 | per-hand fallback for corrupt, partial, `{}` and invalid values |
| `ScoreFingeringGeneratorTests` | 32 | 20 | the 8 cases that predate the feature, default reach 9, per-hand and pair limits, black keys, sixth, lock conflicts |
| `ScoreFingeringSustainedNoteTests` | 13 | 10 | held fingers, ties, re-strike, released leaps |
| `ScoreFingeringLockTests` | 12 | 9 | conflicts report the right note, de-duplication, a tie pinned by its second note |
| `ScoreFingeringAlternativesTests` | 8 | 5 | bound of three, determinism, locks and reach, tempo cost |
| `ScoreFingeringSessionTests` | 13 | 6 | lock toggle and follow-edit, clearing alternatives and locks |
| `BrowserFingeringProfileStoreTests` | 8 | 5 | load, storage disabled, module missing, save, write refused |
| `ScoreNoteAddressResolverTests` (added) | 6 | 3 | the re-barred display mapping and two out-of-score addresses |
| Coordinator tests (added) | 5 | 2 | generate and retry with a too-narrow profile |
| `fingering-profile.test.mjs` | 5 | 4 | |

Line coverage of the fingering files in `PianoMapper.Core/Music` and `PianoMapper.Web/Practice` (measured with the built-in Microsoft code-coverage collector over the fast suite, no package added) went from 98.5% to 97.1% and branch coverage from 96.9% to 95.8%; the lines no longer run are guards and accessors (an invalid hand assignment, a negative note index in a lock, `GetHand` on an unknown hand, the `JSException` catch in `SaveAsync`, and the coordinator's `FingeringProfile` getter, which has no production caller). Each of these was broken in turn in `ScoreFingeringGenerator`, the profile, settings, session, store and resolver code (the thumb-pass cost and its two supporting terms, tempo scaling of the pass cost, re-strike, lock address mapping, default maximum of 9 and 10, the other-pair limit, `TryEditReach` keeping a rejected value, the lock de-duplication, tie-conflict address, the linear-time search, finger order) and at least one kept test failed each time.

## Manual checks

Standalone WASM server and headless Firefox 153, with the repository's `mia_sebastians_theme` score: default profile values; **Regenerate all fingerings** with three alternatives; switching between alternatives and back; locking F#4 to finger 2, then **Regenerate preserving locked fingerings** keeping it at 2; an impossible right-hand profile giving a measure and note message while the locks stay; the alternatives panel clearing after a profile change; reload keeping the profile; corrupt and partial stored values falling back per hand; the Chords exercise with a too-narrow profile showing a message; phone-width layout of the profile controls. Chrome and a physical keyboard were not used.

Follow-up, headless Firefox 157 against the standalone WASM server: before the change, typing 99, -5, 1e3, abc or an empty value in a reach box and pressing Enter showed the message in the status chip while the box kept the typed text (a comfortable span above its maximum, such as 11, also stayed in the box); with the box bound by `@bind:get`/`@bind:set` the box returns to the last accepted value (9, or 6.5 for the comfortable span) in each case, an accepted 8.50 shows as 8.5, and the phone-width (390 px) layout is unchanged. Blazor only treats a typed value as the box's state for a binding; with `value=` plus `@onchange` it re-rendered the same attribute and left the text. Tab and mouse-click commits could not be driven in headless Firefox, so Enter and a dispatched `change` event were used.

## Limits

White-key centre spacing is an approximation, and the hand-position offsets used for chord shape and movement are unchanged heuristics. The model does not use the pedal, substitute fingers, redistribute hands or roll chords. A key attacked again while held is treated as released and struck again by the same finger. C4–D5 is an upper-reach reference, not a calibrated comfort value, and the comfort and symmetry of the learner's hands are still to be measured. Alternative explanations compare modeled costs (stretch beyond the comfortable reach against hand shape, movement and finger changes); they are not confidence figures.
