# Plan: Save fingering edits and delete saved scores

**Date:** 2026-09-14
**Goal:** Keep the existing saved-score update flow for fingering edits and add confirmed deletion of complete saved-score entries from PostgreSQL.

## Context

The hosted browser app stores each `Score` as a JSONB document. The existing **Save changes** action already sends the current score to `PUT /api/scores/{id}`, and the score serializer includes fingering number and placement. The missing operation is deletion across the repository, HTTP API, browser client, and saved-score list.

## Request

- Make it clear that fingering edits to a database-loaded score are persisted with **Save changes**.
- Add a delete option for every saved score.
- Confirm deletion before removing the database row.
- If the open score is deleted, keep it open but clear its database identity so a later save creates a new entry.

## Plan

### Phase 1: Deletion contract and persistence

- [x] Add a repository delete operation using a parameterized `DELETE` and report whether a row existed.
- [x] Map `DELETE /api/scores/{id}` to `204 No Content` or `404 Not Found`.
- [x] Add a browser-client delete operation and a focused request-contract test.

### Checkpoint: Server path

- [x] Focused saved-score tests pass.
- [x] The solution builds without warnings.

### Phase 2: Saved-score list interaction

- [x] Add a confirmed **Delete** action beside **Load**.
- [x] Remove a deleted entry from the visible list and report status.
- [x] Notify the page when the current saved entry is deleted so its saved ID is cleared without unloading the score.
- [x] Add minimal styling for the grouped list actions and destructive button.

### Phase 3: Documentation and end-to-end verification

- [x] Document **Save changes** for fingering edits and saved-score deletion.
- [x] Run the complete automated test suite and Release build.
- [x] Exercise create, fingering update, reload, and delete against PostgreSQL through the hosted browser/API when local infrastructure is available.

## Risks and open questions

- Deletion is irreversible at the application level, so the UI must require explicit confirmation.
- The current architecture stores whole score documents; updating fingerings replaces the JSONB document rather than patching individual notes.
- No database migration is required because deletion uses the existing `scores` table.
