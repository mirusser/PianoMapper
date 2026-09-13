# Plan: PostgreSQL Saved Scores

**Date:** 2026-09-12
**Goal:** Let the hosted Blazor application save its current score to one shared PostgreSQL table and load saved scores from a visible library.

## Context

MusicXML files and score images already converge into the canonical `Score` model before `Piano.razor` activates playback and rendering. `PianoMapper.Server` hosts the Blazor WebAssembly client and is the only process that can safely connect to PostgreSQL.

## Request

- Persist complete scores imported from either supported source.
- Use one shared PostgreSQL database without users, ownership, or tenancy.
- Show saved scores in the Blazor UI and allow one to be loaded.
- Keep the database design to one application table unless another table is necessary.

## Plan

### Phase 1: Persistence foundation

- [x] Add shared saved-score HTTP contracts and a stable, versioned score-document serializer.
- [x] Add a parameterized Npgsql repository and idempotent startup creation of the single `scores` table.
- [x] Verify score documents round-trip all supported score fields and repository SQL is covered by focused tests where practical.

### Checkpoint: Server foundation

- [x] The server builds and exposes list, get, create, and update operations without exposing a database connection to WebAssembly.

### Phase 2: Browser feature

- [x] Add a typed browser client for the saved-score API.
- [x] Add a focused saved-score panel with save, refresh, and load behavior.
- [x] Connect saved scores to the existing score-loaded lifecycle while preserving file import and standalone-client behavior.

### Checkpoint: User flow

- [x] A file- or image-imported score can be saved, appears in the list, and can be loaded back as the active score.
- [x] Re-saving a database-backed score updates the existing row.

### Phase 3: Local setup and verification

- [x] Add local PostgreSQL provisioning/configuration and update current run documentation.
- [x] Run focused tests, the full .NET suite, JavaScript tests, and a Release build.

## Risks and open questions

- Persisted JSON must not depend on computed `Pitch` properties; an explicit document mapping and format version mitigate this.
- PostgreSQL is unavailable to a standalone static PWA, so database failures must stay isolated from local MusicXML import.
- No delete operation is included because it was not requested.
