# Plan: Complete MusicXML Coverage Follow-Up

**Date:** 2026-10-03  
**Goal:** Finish, validate, and document the in-progress implementation prompted by `docs/research/musicxml-coverage.md` without expanding into the document's larger future-feature proposals.

## Context

The working tree already contains an uncommitted, cross-layer implementation: MusicXML reader partials and domain types, W3C fixtures and parser tests, persistence support, import-status UI, measure timing, and grand-staff rendering. The research document prioritizes ordinary valid constructs, a non-fatal warnings channel, correct measure/key semantics, and readable notation. Existing changes appear to cover those areas, but their completeness and build status are not yet known.

## Request and acceptance criteria

Complete the existing MusicXML coverage work so that it builds and the relevant tests prove the intended supported behavior:

- Ordinary MusicXML constructs covered by the in-progress reader tests import or produce an explicit warning rather than unexpectedly rejecting a score.
- New score semantics survive the score-document persistence round trip and reach the import UI and grand-staff scene.
- The supported W3C piano samples and targeted regression fixtures pass with explicit expectations.
- Existing behavior outside this work is preserved by the repository's focused and full unit-test checks.

## Plan

### Phase 1: Establish the inherited implementation baseline

- [x] Task 1: Inspect the changed reader/model/test contracts and run the narrow MusicXML and rendering tests.
  - Acceptance: every changed public value is traced from parse to consumer or identified as incomplete.
  - Verification: build and targeted test output identify no unexplained compile or test failures.
  - Dependencies: none.

### Phase 2: Complete parser and model behavior

- [x] Task 2: Repair only the missing or incorrect reader/model behavior exposed by the coverage tests (including warning collection and measure/key/timing semantics).
  - Acceptance: valid documented constructs either preserve the supported semantic data or surface a typed warning; unsupported musical semantics remain explicit.
  - Verification: `MusicXmlScoreReader*`, `ScoreTiming*`, and `ScoreKeys*` tests pass.
  - Dependencies: Task 1.

- [x] Task 3: Complete cross-layer propagation for newly represented score data.
  - Acceptance: serialization is backward-compatible where expected; importer status and scene output agree with parsed score data.
  - Verification: serializer, importer-status, measure-range, and grand-staff tests pass.
  - Dependencies: Task 2.

### Checkpoint: Feature behavior

- [x] Targeted parser, serializer, import, and renderer suites pass.
- [x] The W3C fixture suite reports expected success/warning behavior.

### Phase 3: Regression validation and documentation

- [x] Task 4: Address only test failures or analyzer/build errors caused by this implementation, preserving unrelated working-tree changes.
  - Acceptance: no new compiler warnings/errors and no changed test is left failing.
  - Verification: Release build plus full non-integration .NET test suite.
  - Dependencies: Tasks 2-3.

- [x] Task 5: Align user-facing limits documentation with the implemented behavior if validation demonstrates a stale statement.
  - Acceptance: documentation describes the actual supported/import-warning behavior without promising unimplemented features.
  - Verification: targeted read against tests and reader behavior.
  - Dependencies: Task 4.

### Checkpoint: Completion

- [x] `git diff --check` is clean.
- [x] Release build and full fast test suite pass.
- [x] Modified files remain limited to the inherited MusicXML coverage implementation and directly related documentation/tests.

## Risks and open questions

| Risk | Mitigation |
| --- | --- |
| The inherited diff deliberately covers more than the research document's cheap fixes. | Treat tests and end-to-end data flow as the authoritative boundary; do not add unrelated features. |
| Source-generated JSON serialization or old persisted documents may break on new properties. | Exercise serialization tests and preserve absent-field defaults. |
| Rendering geometry changes can be semantically correct but visually wrong. | Run scene-contract/unit tests; report any browser visual check that remains outside automated coverage. |
