# Plan: Durable Progress Storage (Postgres, write-through browser cache)

**Date:** 2026-10-10
**Goal:** Keep the learner's progress (recent sessions, mastery, level ladder) in PostgreSQL so it survives the browser, while the browser's local storage stays a write-through cache that is read first. One shared learner for now, shaped so per-learner scope can be added later.

## Context

Progress today is `SightReadingHistory` (up to 100 `SessionSummary` entries, newest first) in browser localStorage under `pianomapper-sight-reading-history-v1`. `Piano.razor` builds `BrowserSightReadingHistoryStore` with `new` and calls load (`:1163`), save (`:1207`), clear (`:1248`). Postgres exists only for saved scores: raw Npgsql, `CREATE TABLE IF NOT EXISTS` at startup, no interface, no tests, and the host refuses to boot when the database is unreachable. There is no learner identity anywhere.

This plan implements the three candidates chosen from the architecture review (candidates 1, 2, 3, trimmed). Candidate 4 (derived views out of `Piano.razor`) is out of scope.

## Architecture decisions

- **Shared pool, learner scope as a seam.** The progress table carries a `learner_id` column that holds one well-known default learner today, and the server resolves the scope in exactly one place. Routes and the cache key do not mention a learner yet. Adding users later means changing that one resolution point (from auth), adding a cache-key suffix, and migrating the default learner's rows; no table or route redesign.
- **One row per finished session, not a history blob.** Server rows are append-only and idempotent by `SessionId`. The server keeps every session; the client keeps the newest 100 in memory (the existing cap).
- **`SessionSummary` gets a stable `SessionId` and schema version 3.** v1 and v2 entries stay valid; at read time they receive a deterministic id derived from their content (not rewritten in the cache), so two browsers uploading the same old entry dedupe.
- **One JSON shape, three uses.** The entry JSON written by Core is the cache format, the wire format, and the `jsonb` document. The server validates by parsing through the Core serializer and never invents a second shape. Existing enum-string casing is pinned by a golden fixture, not changed.
- **Progress store module, two adapters.** A deep `ProgressStore` module owns cache-first loading, write-through, reconciliation, and availability status. Adapters at its seam: browser cache (existing JS module, key unchanged) and server (typed `HttpClient`). In-memory adapters serve the tests. Two production adapters make the seam real, which satisfies the repo rule of no interface before a second adapter.
- **Reconcile rule.** Union by `SessionId`; keep the newest 100 by `CompletedAt`; entries only in the cache are pushed to the server (this is also first-sync migration and the retry for failed writes); entries only on the server are added to the cache and the merged result is written back to the cache.
- **Write-through order.** Cache first (instant, works offline), then server. A server failure never blocks practice and is retried by the next reconcile.
- **Degradation.** No server (standalone PWA returns 404) means cache-only with no error. Server unreachable means cache-only with a visible "not synced" status. Blocked localStorage with a reachable server means server-only with history held in memory. The status the store reports replaces the `IsStorageAvailable` flag the page never read.
- **Server persistence module.** One module owns connection, ordered idempotent schema steps recorded in a `schema_migrations` table, lazy initialization with retry on first use (the host boots with Postgres down), error mapping (database unavailable gives 503, invalid input gives 400), and the first `ILogger` use in the server. Saved scores move onto it with their table and behavior unchanged.
- **Routes** (constants in Core, like `SavedScoreApiRoutes`): `GET /api/progress/sessions?limit=100` (newest first), `POST /api/progress/sessions` (array, idempotent upsert, used for one session and for reconcile uploads), `DELETE /api/progress/sessions` (clear all for the learner scope).
- **Clear means clear everywhere.** The existing confirm text is updated to say the saved copy is deleted too; a failed server delete is surfaced, not swallowed.

## Not built (deliberately)

Users, authentication, learner ids in routes or cache keys, tombstones for cross-device clears, and multi-tab `storage` events. (The candidate 3 follow-ups and moving derived views out of `Piano.razor` were first deferred, then scheduled as Phase 5 below.)

## Task list

### Phase 1: Stored shape (Core)

- [x] **Task 1: Reproduce the suspected Rhythm-only recording bug.**
  - Description: `SightReadingExerciseCoordinator.ConsumeCompletionSummary` neutralises `IsGrandStaff` for Rhythm only but stores the live `PresetId` and `Staff` (`:717`, `:719`). Levels needing FiveNote + treble might never advance from a manual Rhythm-only run. Found by code reading only; not executed.
  - Outcome (2026-10-10): **Confirmed for `PresetId`, not changed for `Staff`.** A Rhythm-only run with GMajor, OneOctave or Chords selected recorded that range although `Generate` composes Rhythm only on the five-note range, so it never matched ladder levels 6, 7, 13, 20-23 (red: 4 of 5 new tests failed, e.g. `Expected: "FiveNote"  Actual: "GMajor"`). The recorded preset is now `FiveNote` for a Rhythm-only run. `Staff` is deliberately left as chosen: the composer builds the Rhythm-only exercise on the chosen staff's centre line and the panel keeps the Staff selector enabled, so a bass Rhythm-only run is truthfully recorded as bass and does not count toward the (treble) rhythm levels. Whether it should is a ladder-design question, not a recording bug (see the open question below).
  - Also found and fixed in the same line: the existing `IsGrandStaff` normalisation used the live `Mode` (`IsPitchSetupIgnored`) instead of the mode the run was generated with (`RunMode`), so changing the Mode select to Rhythm only after a finished grand-staff run, before the summary was consumed, recorded `IsGrandStaff = false`. Both normalisations now use `RunMode == RhythmOnly`.
  - Acceptance criteria:
    - [x] A failing test shows a Rhythm-only run recorded under a non-FiveNote or bass selection does not match its ladder level, or a test shows it does and the finding is withdrawn in this plan.
    - [x] If confirmed, the recorded summary is normalised the same way `IsGrandStaff` is, and the test passes.
  - Verification: `dotnet test --filter "FullyQualifiedName~SightReadingExerciseCoordinatorTests|FullyQualifiedName~LevelProgressionTests"`
  - Dependencies: None
  - Files: `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`, `PianoMapper.Tests/UnitTests/SightReadingExerciseCoordinatorTests.cs`
  - Scope: Small

- [x] **Task 2: SessionSummary identity, schema version 3, one serializer, golden fixture.**
  - Description: Add `SessionId` (supplied by the coordinator, not generated inside `Create`) and bump `CurrentSchemaVersion` to 3. Readers accept 1–3. Give v1/v2 entries a deterministic id at parse time. Expose a single-entry and array serializer in Core that the cache, HTTP and Postgres all use.
  - Implementation notes: `SessionId` is a positional `Guid` on the record and `Create` takes it as its first argument (the coordinator passes `Guid.NewGuid()`). The single serializer is the new `SightReadingSessionSerializer` (`Serialize`, `SerializeAll`, `TryParse(JsonElement|string)`); `SightReadingHistory.FromJson/ToJson` delegate to it. Legacy ids are SHA-256 over every scalar member plus the per-pitch counts (millisecond completion time), hand-written so a later shape change cannot change an id, and frozen by literal tests (the v1 and one v2 literal were also worked out independently in Python). `TryParse` returns null for an unsupported version, a v3 entry without an id, a null `presetId`/`pitchAttempts`, or a value the domain types refuse. **Regression found and fixed:** `LevelProgression.Matches` compared `SchemaVersion >= CurrentSchemaVersion`, so bumping to 3 would have silently dropped every stored v2 session from the ladder; it now uses the new `DetailedSchemaVersion = 2` (red test first: `Evaluate_EntriesStoredBeforeSessionIds_StillCountTowardTheirLevel`). Loading then saving rewrites a v1/v2 entry with its derived `sessionId` still at its old schema version (deterministic, so harmless); the plan's "not rewritten in the cache" holds only until the next save.
  - Acceptance criteria:
    - [x] A v3 entry round-trips every field including `SessionId`.
    - [x] The v1 fixture and a new v2 fixture still parse, with the same derived id on every parse.
    - [x] A v3 golden fixture pins the exact stored shape, including the mixed enum-string casing as it is today.
    - [x] Worst-case 100 entries stays under the existing 2 MB assertion.
  - Verification: `dotnet test --filter "FullyQualifiedName~SightReadingHistoryTests|FullyQualifiedName~SightReadingSessionSummaryTests"`
  - Dependencies: Task 1 (same coordinator code path)
  - Files: `PianoMapper.Core/Practice/SightReadingSessionSummary.cs`, `PianoMapper.Core/Practice/SightReadingHistory.cs`, `PianoMapper.Web/Practice/SightReadingExerciseCoordinator.cs`, `PianoMapper.Tests/Fixtures/sight-reading-history-v3.json`, tests
  - Scope: Medium

### Checkpoint: Stored shape

- [x] `dotnet test` green; existing localStorage data from the shipped v1/v2 format still loads (fixtures).

### Phase 2: Server persistence module and progress slice

- [x] **Task 3: First DB-backed test tier, characterising saved scores.**
  - Description: Add `IntegrationTests/` with a Postgres fixture and `[Trait("Category", "Integration")]`, per the run-tests and writing-tests skills. Write characterisation tests for `SavedScoreRepository` (create, get, list/paging, update, delete, startup creation is idempotent on a populated database).
  - Implementation notes: `Testcontainers.PostgreSql` 4.16.0 added to the test project (the only new package so far). One `postgres:17-alpine` container is shared through a collection fixture; every test creates its own empty database on it. The 14 characterisation test cases passed on first run (they pin existing behavior, so there is no red step) and run against the unchanged `SavedScoreRepository(NpgsqlDataSource)`.
  - Acceptance criteria:
    - [x] Integration tests run against a real Postgres and are excluded from the default unit run.
    - [x] Saved-score behavior is pinned before it moves in Task 4.
  - Verification: `dotnet test --filter "Category=Integration"` (needs Docker); `dotnet test --filter "Category!=Integration"`
  - Dependencies: None
  - Files: `PianoMapper.Tests/PianoMapper.Tests.csproj`, `PianoMapper.Tests/IntegrationTests/PostgresFixture.cs`, `PianoMapper.Tests/IntegrationTests/SavedScoreRepositoryTests.cs`
  - Scope: Medium

- [x] **Task 4: Server persistence module; saved scores move onto it.**
  - Description: Introduce the module (connection, ordered schema steps plus `schema_migrations`, lazy init with retry, error mapping, logging). The `scores` table is step 1 and stays byte-compatible. Remove the startup `InitializeAsync` await from `Program.cs`.
  - Implementation notes: `PostgresDatabase` owns the `NpgsqlDataSource` and prepares the schema on first use (`GetDataSourceAsync`), under a process-level gate and a PostgreSQL advisory lock inside one transaction, so several hosts sharing a database apply each step once; a failed attempt is not remembered and the next use retries. `SchemaSteps` holds the ordered steps (step 1 is the old `scores` DDL verbatim); applied versions are recorded in `schema_migrations`. `SavedScoreRepository` now takes the module and lost its `InitializeAsync`; `ConnectionStringName` moved to `PostgresDatabase`. `PersistenceErrorFilter` (one endpoint filter on a single route group in `Program.cs`, which also carries the progress routes) maps `PersistenceFailure.IsDatabaseUnavailable` exceptions (non-Postgres `NpgsqlException`, `TimeoutException`, transient SQLSTATEs and classes 08, 28, 3D, 53, 57) to 503 and `ArgumentException` to 400, as problem details, and logs the exception (no connection string in it). Missing `ConnectionStrings:PianoMapper` still fails fast at startup. **New test packages beyond the plan:** `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (needed to test the real `Program.cs`; `PianoMapper.Server` and `PianoMapper.Web` both define a top-level `Program`, so `PianoMapperServerFactory` names the server by `PostgresDatabase` instead).
  - Acceptance criteria:
    - [x] The host starts with Postgres unreachable; static files and the OMR route work.
    - [x] While the database is down, saved-score routes return 503 (not 500) and recover without a restart once it is up.
    - [x] A blank saved-score title returns 400.
    - [x] An existing database with `scores` rows upgrades in place with no data change.
    - [x] Failures are logged without the connection string.
  - Verification: Task 3 tests still green plus new integration tests; manual: stop the compose container, run `make run`, open `/`.
  - Dependencies: Task 3
  - Files: `PianoMapper.Server/Persistence/*` (new module files, `SavedScoreRepository.cs`, `SavedScoreEndpoints.cs`), `PianoMapper.Server/Program.cs`, tests
  - Scope: Medium (may need splitting during implementation)

- [x] **Task 5: Progress sessions table and repository.**
  - Description: Step 2: `progress_sessions` (`session_id uuid` PK, `learner_id uuid` default learner, `completed_at timestamptz`, `document_version int`, `session_document jsonb`, `created_at`), index on (`learner_id`, `completed_at desc`). Repository: idempotent batch upsert, newest-N list, delete-all. Learner scope resolved in one place.
  - Implementation notes: `LearnerScope.Resolve()` is the one place that decides the learner (a fixed well-known default id); repository methods take the learner id explicitly, so tests and the later per-user change need no repository edits. `SaveAsync` is one `INSERT ... SELECT FROM unnest(...) ON CONFLICT (session_id) DO NOTHING` (returns the number actually stored), checks the whole batch first (unsupported schema version or empty id throws `ArgumentException`, nothing stored), and converts `CompletedAt` to UTC for `timestamptz`. `ListNewestAsync` skips a stored document this version cannot read (a newer app's) instead of failing the list. The primary key is `session_id` alone, as planned.
  - Acceptance criteria:
    - [x] Re-posting the same `SessionId` changes nothing.
    - [x] List returns newest first and respects the limit.
    - [x] Delete removes only the resolved learner's rows.
    - [x] A document whose version is unsupported is rejected on write, not stored.
  - Verification: `dotnet test --filter "Category=Integration"`
  - Dependencies: Tasks 2, 4
  - Files: `PianoMapper.Server/Persistence/ProgressSessionRepository.cs`, schema step file, `PianoMapper.Core/Practice/ProgressApiRoutes.cs`, tests
  - Scope: Medium

- [x] **Task 6: Progress endpoints.**
  - Description: Map GET/POST/DELETE over the repository using the Core serializer for both directions; register in `Program.cs`.
  - Implementation notes: `GET` returns the Core serializer's array (default and maximum `limit` 100, anything else is 400), `POST` takes a JSON array, parses each entry through `SightReadingSessionSerializer.TryParse` (one entry it rejects fails the whole request with 400) and answers 204, `DELETE` clears the learner's rows and answers 204 (repeatable). The stored `jsonb` document is the re-serialised canonical entry, never the raw client text.
  - Acceptance criteria:
    - [x] A v3 history POSTed then GET returns the same entries in newest-first order.
    - [x] Invalid JSON or an unsupported version returns 400; database down returns 503.
    - [x] A 100-entry worst-case POST is under the 10 MiB request limit.
  - Verification: `dotnet test --filter "Category=Integration"`; manual `curl` against `make run`.
  - Dependencies: Task 5
  - Files: `PianoMapper.Server/Persistence/ProgressEndpoints.cs`, `PianoMapper.Server/Program.cs`, tests
  - Scope: Small

### Checkpoint: Server

- [x] All unit and integration tests pass; with the compose Postgres, `curl` round-trips a session; with it stopped, the app still serves and the routes return 503. (Checked 2026-10-10 against the real compose database, which holds four saved scores whose table checksum was identical before and after; the test sessions were removed again with the DELETE route.)

### Phase 3: Progress store module (Web)

- [x] **Task 7: ProgressStore with in-memory adapters.**
  - Description: The module and its seam, tested only through its interface with in-memory cache and server adapters (including failing ones). Final signatures are settled test-first; the behavior it must provide is the decisions above plus a notification to the page when a background server merge changes the history.
  - Implementation notes (2026-10-10): `ProgressStore(IProgressCache, IProgressServer)` exposes `LoadAsync`, `RecordAsync(summary)`, `ClearAsync`, `History`, `Status` (`ProgressStatus(Sync, IsCacheAvailable)`, `Sync` is `Connecting | Synced | NoServer | NotSynced | DeleteFailed`), the `Changed` event and `PendingSync` (a task the page never awaits; tests do). The cache seam answers `ProgressCacheRead(History, IsAvailable)` and bools for write/clear; the server seam never throws, it answers `ProgressServerReach` (`Reached | NoServer | Unreachable`), and `ListAsync` returns `ProgressServerSessions(Reach, Sessions)`. Decisions made while settling them: (1) **`RecordAsync` awaits only the cache write**; the server upload runs in the background so a slow server never holds up practising, and a server step is skipped when the state is `NoServer`, is a single-session POST when `Synced`, and is a full reconcile otherwise, which is how a session that failed to upload is retried by the next finished exercise without a page reload. (2) Server steps run one after another through a chain (`PendingSync`), and **`ClearAsync` queues behind an in-flight sync**, otherwise a server list fetched before the delete would resurrect the sessions; a mutation check (removing the ordering) makes the race test fail. A step that was cancelled or faulted never blocks the next. (3) A failed delete leaves `DeleteFailed` (shown by the panel) instead of a return value; the next successful sync clears it. (4) **New Core method `SightReadingHistory.MergedWith`** (union by `SessionId`, existing copy wins, newest 100) holds the merge rule and is exercised through the store tests. (5) Cache entries older than the server's newest 100 are still uploaded (idempotent), so the server keeps every session.
  - Acceptance criteria:
    - [x] Load returns the cache immediately, then merges the server copy and notifies once.
    - [x] Cache-only entries (old browser data, failed writes) are pushed on the next reconcile.
    - [x] Record writes the cache first; a failing server leaves the entry cached and reports "not synced".
    - [x] Server-only mode when the cache is unavailable; cache-only mode when there is no server.
    - [x] Clear removes both copies and reports a failed server delete.
    - [x] Merge keeps the newest 100 and never duplicates a `SessionId`.
  - Verification: `dotnet test --filter "FullyQualifiedName~ProgressStoreTests"`
  - Dependencies: Task 2
  - Files: `PianoMapper.Web/Practice/ProgressStore.cs`, adapter interface files (one type per file), in-memory test adapters, `PianoMapper.Tests/UnitTests/ProgressStoreTests.cs`
  - Scope: Medium

- [x] **Task 8: Real adapters; the pass-through store is removed.**
  - Description: Browser cache adapter (reusing `sight-reading-history.js` and its key) replaces `BrowserSightReadingHistoryStore`; server adapter is a typed client over the registered `HttpClient`, treating 404 as "no server" and network errors as "unreachable".
  - Implementation notes (2026-10-10): `BrowserProgressCache` wraps the unchanged `sight-reading-history.js` (same key) and reads the module's `{ json, isAvailable }` through a private nested record, so `SightReadingHistoryStorageResult` is gone along with `BrowserSightReadingHistoryStore`; its tests use a `FakeRuntime`/`FakeModule` pair whose module answers like the real JavaScript object (a JSON round trip into whatever type the adapter asks for). `ProgressServerClient` is a typed client registered in `Program.cs` beside `SavedScoreClient`. **Deviation from "404 means no server":** the real standalone dev server answers `GET /api/progress/sessions` with **200 `text/html`** (its `index.html` fallback) and `POST`/`DELETE` with **405**, found by probing it with `curl` before writing the adapter, so the client reports `NoServer` for 404, 405, and a successful GET whose content type is not JSON; with only the 404 rule the standalone app would have parsed HTML and shown a "not synced" error. Any other non-success status, a network error, or a request timeout is `Unreachable`; a cancellation the caller asked for still throws. An entry the Core serializer rejects is skipped when listing, as in the cache.
  - Acceptance criteria:
    - [x] The existing JS module test and the `FakeRuntime` pattern cover the cache adapter, including unavailable storage.
    - [x] A `StubHttpMessageHandler` test covers 200, 404, 503 and a network failure for each route.
    - [x] `BrowserSightReadingHistoryStore` and its result record are deleted (superseded, not orphaned).
  - Verification: `dotnet test`; `node --test PianoMapper.Tests/JavaScript/*.test.mjs`
  - Dependencies: Task 7
  - Files: cache adapter, server adapter, `PianoMapper.Web/Program.cs`, tests
  - Scope: Medium

- [x] **Task 9: Wire the store into the page and Progress panel.**
  - Description: DI-registered store replaces `new BrowserSightReadingHistoryStore(JS)` in `Piano.razor`. Load, record and clear go through it; the background-merge notification calls `RefreshSightReadingInsights()` and `StateHasChanged()`. The Progress panel shows the sync status and the updated clear-confirm wording.
  - Implementation notes (2026-10-10): `Piano.razor` injects `ProgressStore` (registered scoped in `Program.cs`); `sightReadingHistory` is now a read-only view of `ProgressStore.History`; `Changed` is subscribed in `OnInitialized` and unsubscribed in `DisposeAsync`, and its handler runs `RefreshSightReadingInsights()` and `StateHasChanged()` inside `InvokeAsync`. The panel takes one new `StatusMessage` string (`SightReadingLabels.DescribeProgressStatus`, null when there is nothing to say: still connecting, or standalone with working storage), so no internal type crosses the component boundary. The subtitle "Local history and pitch mastery" became "History and pitch mastery", the button "Clear local history" became "Clear history", and the confirm now reads "Clear your note-reading history, including the copy saved on the server? This can't be undone and doesn't affect saved scores." Manual verification used headless Firefox over WebDriver BiDi with a mocked Web MIDI keyboard against a Release build of the real server on port 5081 (the compose PostgreSQL on 5434) and the standalone WASM dev server on 5199, each scenario in a brand-new empty Firefox profile; exercises were really finished by playing the amber next-key hints through the mock keyboard (8 notes, pitch only, wait for me). Evidence:
    - (a) Server up: finished an exercise, `progress_sessions` went 0 to 1 and the panel read "Saved on the server as well as in this browser." (screenshot viewed). New empty profile, `localStorage` empty at start: after load the panel showed that session, the guided path read "Next: Five notes · Treble · Pitch only · 88% over your last 1 session", and `localStorage` held 1 entry again (the merged result written back).
    - (b) Postgres stopped (`docker stop`): the route answered 503, three more exercises finished, the panel read "Not synced: the server can't be reached. Progress is kept in this browser and uploads when the server is back." with 3 then 4 sessions listed and in `localStorage` (screenshot viewed). After `docker start` (the server recovered without a restart) and a page reload the database went from 2 to 4 rows and the status returned to "Saved on the server...". A second outage: exercise #4 finished while down (Not synced), the database restarted still at 4 rows, and finishing exercise #5 uploaded both (rows 6, status Synced).
    - (c) Standalone (no server): `GET` answered 200 HTML, so the store settled on no server; no status line, no error UI, one exercise finished and saved to `localStorage`, still there after reload; the page made only that one progress request.
    - (d) `localStorage` blocked by a preload script that makes it throw `SecurityError` (a simulation, not a real private window): the panel listed the 6 server sessions with "This browser can't store progress, so it is kept on the server only."; a finished exercise reached the database (7 rows) and survived a reload.
    - (e) Clear: with the server up the confirm text above appeared and the database went to 0 rows; with Postgres stopped, Clear emptied the page but the panel said "The saved copy on the server could not be deleted, so cleared history may come back. Clear it again when the server is reachable."; after the restart the 1 saved row was still there, a reload brought it back, and clearing again left 0 rows.
    - Not exercised: a real private window, a second browser racing a clear, the Roland output.
  - Acceptance criteria:
    - [x] With server and Postgres: finish an exercise, clear browser site data, reload; the session and ladder state come back from the database.
    - [x] With Postgres stopped: practising and saving keep working and the panel says progress is not synced; after restart the cached sessions upload.
    - [x] Standalone (no server): behaves as today, with no error message.
    - [x] In a private window, progress is still saved to and loaded from the server. (Simulated by blocking `localStorage`; see (d).)
  - Verification: manual, using the real running app per the Firefox BiDi screenshot recipe; `dotnet build -c Release`.
  - Dependencies: Task 8
  - Files: `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Web/Components/SightReadingHistoryPanel.razor`, `PianoMapper.Web/Program.cs`
  - Scope: Medium

### Checkpoint: Complete

- [ ] Full `dotnet test` (unit plus integration), JS tests, and Release build pass. (2026-10-10: Release build 0 warnings, 2447 unit tests and 68 integration tests pass, `sight-reading-history.test.mjs` 7 of 7; `node --test PianoMapper.Tests/JavaScript/*.test.mjs` is 160 of 161 because `canvas.test.mjs` "grand staff sizes signature glyphs to their requested heights" fails with `context.strokeText is not a function`. That failure is not from this plan: neither `canvas.js` nor `canvas.test.mjs` has a working-tree change and both are as committed in `d8eaf02`. Left unticked until that test is fixed or accepted.)
- [x] The manual scenarios in Task 9 pass, with evidence pasted.

### Phase 4: Docs

- [x] **Task 10: Documentation.**
  - Description: Update `README.md` where it says history is browser-local, standalone PWA limits, and that backend synchronization is out of scope (`:34`, `:152`, `:181`), plus the new routes, the 503 behavior, and how to run integration tests. Run the verify-readme-docs skill.
  - Implementation notes (2026-10-10): stale claims fixed at the learn-at-the-piano bullet (guided path "from local progress history"), the browser-experience bullet (`:34`), the quick-start table row for the standalone PWA, the standalone-PWA limits paragraph (`:152`), the project-map row for the server, and the limits list (`:181`, which now states the one shared history and the accepted cross-device clear limitation). New section "Progress storage" with the three routes, the 400/503 behavior (saved scores included), the startup change, and the Progress panel states; "Verify a checkout" now says a bare `dotnet test` needs Docker and gives the `Category` filters. Audit with the verify-readme-docs skill, minimal: linked files exist, project names, routes against `ProgressApiRoutes`/`ProgressEndpoints`, status codes against `PersistenceProblems` and the endpoint code, filters against runs of both tiers, `git diff --check` clean. Stale outside the README and not edited: `docs/browser-test-matrix.md` has no rows for the sync states (its "Local-storage history failure" row is still accurate).
  - Acceptance criteria:
    - [x] README matches the code and the commands it lists work.
  - Dependencies: Task 9
  - Files: `README.md`
  - Scope: Small

### Phase 5: Cleanup (behavior-preserving, scheduled 2026-10-10 on the user's "finish the cleanup")

Everything here must leave the existing tests green. Only Task 12 may change an observable result, and only by removing a divergence it first reproduces with a failing test.

- [x] **Task 11: One place decides whether a stored session counts.**
  - Description: The legacy-trust checks (`SchemaVersion`, `Mode`, nullable v2 members) are re-derived by `SightReadingHistory.ContributesToPitchMastery`, `LevelProgression.Matches`, `SightReadingInsights.ToTrendPoint` and `SightReadingLabels.DescribeSession`/`DescribeTimingBreakdown`. Concentrate them behind the session summary (or a small Core type next to it), named for the question each reader asks, and delete the duplicated logic. Locate the current line numbers first; Phase 1–3 moved code.
  - Implementation notes (2026-10-10): the module is `SightReadingSessionSummaryExtensions` in Core, extension methods rather than members of the record (a get-only member would be written into the stored JSON by the shared serializer options and change the golden shape). One method per question: `IsLegacy` (before schema 2), `HasReliablePitchCounts` (was `ContributesToPitchMastery`), `CanBeMatchedToLevel`, `PitchFirstTryPercent`, `TimingCleanPercent` and `GetTimingBreakdown` (a small `SessionTimingBreakdown` record). All were located in the current code first (`SightReadingHistory.cs:305`, `LevelProgression.cs:68-91`, `SightReadingInsights.cs:113-132`, `SightReadingLabels.cs:239,316,328`). `ContributesToPitchMastery` is deleted, `LevelProgression` lost its own `Matches` trust check and both percent formulas, the trend and the two labels ask the module. `NoteReadingMode.IsTimingGraded()` moved to `NoteReadingModeExtensions` because Core needs it (see Task 12). Deliberately not moved: `DescribeSession` still reads `IsGrandStaff == true`, `RhythmPreset`, `Motion`, `Pacing` and `TempoBeatsPerMinute`, which are display choices for optional members, not trust decisions. `IsLegacy` is written as `SchemaVersion < DetailedSchemaVersion`, which gives that constant its production user back; it answers like the old `== Legacy`, `> Legacy` and `>= Detailed` checks for every version the parser and the server accept (1 to 3). Evidence: all 2447 existing unit tests pass unchanged; 34 new tests (`SightReadingSessionSummaryExtensionsTests`, driven by the v1, v2 and v3 fixtures) failed to compile before the module existed, and a mutation check (dropping the legacy rule from `CanBeMatchedToLevel`, widening `HasReliablePitchCounts`) failed 7 of them; a throwaway differential test (not kept) compared the old inline rules with the module over 200,000 random entries (every version, mode, null pattern and prompt count) and found them equal for mastery, matching, pitch percent and, except Rhythm only in the trend (Task 12), timing percent.
  - Acceptance criteria:
    - [x] Each of the readers above asks the one module instead of re-reading `SchemaVersion`, `Mode` and nullable members itself.
    - [x] The new module is tested through its interface with v1, v2 and v3 entries (the fixtures exist); all existing history, ladder, insight and label tests still pass unchanged.
  - Verification: `dotnet test -c Release --filter "Category!=Integration"`
  - Dependencies: None
  - Files: `PianoMapper.Core/Practice/SightReadingSessionSummary.cs` (or a new sibling), `SightReadingHistory.cs`, `LevelProgression.cs`, `PianoMapper.Web/Practice/SightReadingInsights.cs`, `SightReadingLabels.cs`, tests
  - Scope: Medium

- [x] **Task 12: One timing-clean computation.**
  - Description: `LevelProgression.TimingCleanPercent` special-cases Rhythm only (unplayed notes count against the learner) but `SightReadingInsights.ToTrendPoint` does not, so a Rhythm-only session can look clean in the trend yet fail the ladder. Reproduce the divergence with a failing test, then make both ask one computation. Also look at whether the three encodings of "is timing graded" (`ExerciseLevel.IsTimed`, `SightReadingLabels.IsTimingGraded`, `GetGradedAxes`) can share one without churn; merge them only if the change is small, otherwise record why not.
  - Implementation notes (2026-10-10): reproduced first. `Build_Trend_RhythmOnlySessionWithUnplayedNotes_CountsThemAgainstTheTimingCleanRate` (10 prompts, 6 first-try correct, 0 timing mistakes) failed with `Expected: 60  Actual: 100`, and `Build_Trend_TimingCleanRate_IsTheOneTheGuidedPathUsesForTheSameSession` (the trend point against the matching ladder level's `AverageTimingCleanPercent`, for Rhythm only and for Pitch + rhythm) failed for Rhythm only with `Expected: 60  Actual: 70` while the Pitch + rhythm case already agreed. The ladder's formula (Rhythm only counts every non-clean prompt) became `SightReadingSessionSummaryExtensions.TimingCleanPercent`, and `LevelProgression` and `SightReadingInsights.ToTrendPoint` both ask it; both tests pass. The one observable change is the trend's "Timing clean" figure for a Rhythm-only session (seeded history in the manual check: 100% before, 60% after); a Rhythm-only trend point no longer depends on `TimingMistakeCount` being recorded either. The ladder keeps its old rule that a timed entry which never recorded its timing mistakes counts as 0% (`?? 0` at its one call site), pinned by the differential run in Task 11. **The three encodings of "is timing graded":** `GetGradedAxes` stays the source; `IsTimingGraded` now exists once, as `NoteReadingModeExtensions.IsTimingGraded` (Core needs it), and `SightReadingLabels.IsTimingGraded` is a one-line delegate that stays because existing tests call it by that name and about ten call sites in the page, panel and coordinator use it (retargeting them would orphan the delegate and gain nothing). `ExerciseLevel.IsTimed` is not merged: it means "the level carries a tempo", and the `StartTempoPulsesPerMinute!.Value` reads in `LevelProgression` and the coordinator rely on that, whereas a mode-derived `IsTimed` could say true for a level without a tempo; the existing catalog test already pins the two to the same answer, and `IsTimed` now says so in its doc comment.
  - Acceptance criteria:
    - [x] A test shows the divergence before the change and passes after it.
    - [x] Ladder results for existing fixtures are unchanged.
  - Verification: `dotnet test -c Release --filter "Category!=Integration"`
  - Dependencies: Task 11
  - Files: `LevelProgression.cs`, `SightReadingInsights.cs`, the session-summary module from Task 11, tests
  - Scope: Small

- [x] **Task 13: Derived progress views are built once per history change, outside `Piano.razor`.**
  - Description: A progress-views module in `PianoMapper.Web/Practice` takes a `ProgressHistory` and owns every derived value the page uses: insights, weak notes, ladder, recent sessions (`Take(10)`), the pitch-mastery lists the panel shows, the note mastery handed to `Generate`/`GenerateRecommended`, and drill availability. `Piano.razor` rebuilds it when `ProgressStore.History` changes (the `Changed` notification, local record, clear) and reads values; the per-render recomputation at the panel parameters and recent-session slice, the per-generate recomputation, and the double `GetDrillAvailability` call disappear. Also replace the `Levels[Number-1]` indexing assumption by a lookup that does not depend on catalog order. Add the term to `CONTEXT.md` if a new domain name is introduced.
  - Implementation notes (2026-10-10): `ProgressViews` (internal sealed, `PianoMapper.Web/Practice`) is built from a `SightReadingHistory` and holds `RecentSessions` (`RecentSessionCount` = 10 moved here), `WeakestPitches`, `InsufficientDataPitchCount`, `Insights`, `NoteMasteryWeakestFirst` (what `Generate` and `GenerateRecommended` take), `WeakNotes`, `Ladder` and `RecommendedExerciseDescription`; `ProgressViews.Empty` is the starting value. `Piano.razor` keeps one `progressViews` field, rebuilt by `RefreshProgressViews()` at the four places that change the history (first load, the store's `Changed` notification, a local record, a clear), and the panel parameters, the exercise presentation and both generate paths only read it; `sightReadingHistory`, `sightReadingInsights`, `sightReadingWeakNotes`, `sightReadingLadder`, `RecentSightReadingSessions` and `InsufficientMasteryDataPitchCount` are gone. The `Levels[Number - 1]` indexing is replaced by a lookup of the recommended level's progress by the level itself (`Ladder.Levels.First(progress => progress.Level == Ladder.Recommended.Level)`), done once in the module. The panel's parameters are unchanged, so `SightReadingHistoryPanel.razor` is untouched. **Deviation: drill availability is not in the module.** It depends on the live exercise setup (staff, range, mode, pattern, grand staff) as well as the weak notes, so it cannot be a function of the history. The page now works it out once into a `drillAvailability` field, in `RefreshProgressViews()` and after every exercise action (`HandleSightReadingExerciseActionAsync` wraps the old switch, renamed `ApplySightReadingExerciseActionAsync`, in a `finally`), which is the only path through which the setup changes; the constructor sets the first value. The double `GetDrillAvailability` call per render is gone, and the three remaining calls are in the constructor, `RefreshDrillAvailability` and nowhere in markup. Left as they were: `StatusMessage` is still `SightReadingLabels.DescribeProgressStatus(ProgressStore.Status)` per render (a switch over the store's status, not derived from history, and status changes already raise `Changed`), and the panel's own default parameter values (`LevelProgression.Evaluate(Empty)` at construction). A new term, ProgressViews, was added to `CONTEXT.md` (and the SessionSummary row names the module from Task 11). One theoretical window remains: `RecordAsync` replaces `ProgressStore.History` before it awaits the cache write, and the views are rebuilt right after that await, so a Generate dispatched inside that gap would use the previous mastery; the old code read the store directly and would not. The gap is one browser storage call wide and sits between a finished exercise and the learner pressing a button. Evidence: 12 new `ProgressViewsTests` (history in, views out: empty, newest 10 in order, ranked versus insufficient pitches, note mastery order and the weak threshold, insights, the recommendation description for levels 1, 2, 6, 15 and all passed, and that repeated reads return the same instances); then the manual check below. Manual check (2026-10-10, headless Firefox over WebDriver BiDi, fresh empty profiles, mocked Web MIDI keyboard): the new code as a server on port 5081 and, for comparison, the staged Phase 1-4 tree (exported with `git checkout-index`) as a server on 5082, both on the compose Postgres, seeded with 15 sessions through `POST /api/progress/sessions` (v3 sessions across 7 levels, one v1 entry, one Rhythm-only session with 6 of 10 clean). (a) The Progress panel text of the two servers was identical except one line, the intended Task 12 change (`Timing clean: 100% → 62%` became `60% → 62%`). The viewed screenshot shows the panel with the expected content, checked against the seed by hand: 10 recent sessions newest first with the legacy one marked "older session", 23 guided-path rows (level 1 "Next ... 75% over your last 3 sessions", level 2 in progress at 75%, level 4 94% over 2 sessions, level 6 and 8 in progress), five weak spots (B4 treble 2 of 6, D3 and C3 bass 3 of 6, D4 treble 12 of 20, A4 treble 5 of 6), a treble habit of 16 and a bass habit of 6, pitch mastery for 10 pitches with "2 more pitch(es) need more attempts", and "Saved on the server as well as in this browser". (b) The exercise panel of both servers was identical across 12 setting changes (range Chords, Accidentals, One octave; pattern Melodic and Random; bass staff; grand staff on and off; Rhythm only and back; Ledger lines): the Start recommended text and the drill button's enabled state and reason matched at every step, so the event-driven drill availability follows the setup exactly as the per-render one did. (c) On 5081, an 8-note exercise was really finished by playing the amber next-key hints through the mock keyboard: the panel's first row became the new session (8 of 8), the level 1 line moved from 83% to 92% over its last 3 sessions, pitch mastery and weak spots updated (D4 treble from 22 of 30 to 24 of 32), the server rows went from 16 (the seed plus a 64-note exercise finished by an earlier, crashed run of the same script, itself the first proof that a finished session reaches the server and the panel) to 17, and `localStorage` too. (d) Clear through the panel's button (confirm text as in Task 9): the panel showed "No completed exercises yet", the guided path fell back to "Next: Five notes · Treble · Pitch only", the Clear button disabled, weak spots and pitch mastery back to "Not enough attempts yet", and the server and `localStorage` both held 0 sessions. The 5081 and 5082 servers and the Firefox instances were stopped by port afterwards; `progress_sessions` is back to 0 rows and the `scores` table is unchanged (4 rows, same checksum as before).
  - Acceptance criteria:
    - [x] No progress computation runs inside a render path or per key press; the page only reads values.
    - [x] The module is tested through its interface without a Razor host (history in, views out).
    - [x] The Progress panel looks and behaves as before: recent sessions, guided path, weak spots, pitch mastery, sync status, clear.
  - Verification: `dotnet test -c Release --filter "Category!=Integration"`; Release build; manual check of the Progress panel against a server on a spare port, using the Firefox BiDi recipe in memory (viewed screenshot, with seeded history).
  - Dependencies: Task 11
  - Files: new views module, `PianoMapper.Web/Pages/Piano.razor`, `PianoMapper.Web/Components/SightReadingHistoryPanel.razor` (only if its parameters change), tests, `CONTEXT.md`
  - Scope: Medium

### Checkpoint: Cleanup

- [x] Release build clean; unit, integration and JS tests as before (the one known `canvas.test.mjs` failure aside); the Progress panel verified in a real browser. (2026-10-10: Release build 0 warnings, 2503 unit tests (2447 before Phase 5) and 68 integration tests pass, `node --test` 160 of 161 with the same `canvas.test.mjs` failure, Progress panel verified as described under Task 13.)

## Risks and mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Clearing on one device while another has a stale cache re-uploads the old sessions | Med | Accepted for the single-learner case; documented; tombstones are the later fix. |
| Deterministic ids for legacy entries could collide for two real sessions with the same timestamp, preset and mode | Low | Include millisecond `CompletedAt` and all identifying fields in the derivation; test it. |
| Saved-score failures change from 500 to 503/400 | Low | Called out in the README; pinned by Task 3/4 tests. |
| Integration tier adds a package and needs Docker | Med | Shared run-tests skill says it fails, not skips, without Docker; documented in README. |
| Whole-history rewrite by two tabs, last writer wins, today | Low | Reconcile is union-by-id, so a stale tab can no longer drop sessions. |
| Suspected Rhythm-only bug may not reproduce | Low | Task 1 either confirms with a failing test or withdraws it. |

## Decisions (confirmed by the user 2026-10-10)

- Integration database: **Testcontainers** (new package; Docker required).
- "Clear history" deletes the saved database copy as well, and the confirm text says so.
- The cross-device clear limitation (a stale cache re-uploads cleared sessions) is accepted for now.

## Open questions

- (Resolved 2026-10-10, user asked to fix it: a Rhythm-only run on the bass staff now counts toward the rhythm levels 6, 7, 13 and 20-23. `LevelProgression.Matches` ignores `Staff` when the level's mode is Rhythm only, since that mode plays every note on the staff's centre line and grades no pitch; the recording still stores the staff truthfully. Pitch + rhythm levels still require the level's staff. Tests: `Evaluate_RhythmOnlySessionsOnTheBassStaff_CountTowardTheRhythmOnlyLevel` (red first) and `Evaluate_PitchAndRhythmSessionsOnTheBassStaff_DoNotCountTowardTheTrebleLevel`.)
- (Resolved: the candidate 3 follow-ups and candidate 4 are Phase 5, requested by the user.)
