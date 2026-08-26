# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** PAUSED by explicit user request.

## Why this file exists

The project has already suffered context loss when a long ChatGPT conversation reached the platform session/duration limit and work continued in another chat. Conversation memory alone is therefore not reliable enough to identify the exact project position.

This file is the repository-side checkpoint. A fresh conversation must be able to read it and resume without reconstructing hundreds of previous messages.

## What changed in the latest task

This task was a continuity/documentation task only. No runtime/product code was intentionally developed.

Created on `main`:

- `PROJECT GOAL.md` — persistent product memory, locked principles and mandatory continuity protocol.
- `LAST CHANGE.md` — this operational handoff document.

New permanent operating rule:

1. Start every EliteSCADA task by reading `PROJECT GOAL.md` and `LAST CHANGE.md`.
2. If stable project intent changes in ChatGPT, update `PROJECT GOAL.md` in the same task.
3. Before the final reply to the user on every EliteSCADA task, update `LAST CHANGE.md` with the actual stopping point.
4. Do not rely on chat history alone to decide what to implement next.

## Repository state observed during this handoff

`main` head observed immediately before writing this `LAST CHANGE.md`:

`60cf92a8439b56edbcb9b154b5d89a089c7c050e` — `Add persistent EliteSCADA project goal`

The commit containing this `LAST CHANGE.md` is necessarily newer than the SHA above. On the next task, fetch the live `main` HEAD after reading this file rather than assuming the SHA above remains current.

Latest functional/product milestone immediately before the continuity-document commits:

`fdaa093f8ba735e447cb871beaf515f4417e7559` — `Secure alarm shelving lifecycle`

That means alarm shelving work is already integrated into `main`, despite an earlier chat handoff having remembered it as work still in progress.

At the time checked:

- branch `security/alarm-shelving` had **0 commits ahead of `main`**;
- it was **1 commit behind `main`**;
- there were no unique file changes on that branch relative to `main`.

Therefore **do not resume development from that branch based on old chat context**. Inspect current `main` first.

## Current implemented security/runtime position

The repository/roadmap currently records the following security track as completed:

- capability-based authorization and scoped grants;
- TAG access policies;
- Engineering Schema v6 security roles;
- JWT Bearer principal mapping/validation;
- protected TAG writes and alarm acknowledgement;
- protected Engineering import/restore and persistence lifecycle operations;
- trusted authenticated actor for save/publish/activate-style operations instead of caller-supplied authority;
- PostgreSQL append-only audit storage protected against update/delete/truncate;
- audit query protection;
- succeeded/denied/failed audit recording;
- PostgreSQL-backed browser security coverage;
- alarm shelving/unshelving runtime behavior;
- `AlarmShelve` authorization with area scoping;
- trusted actor metadata and audit coverage for shelving;
- browser coverage for shelving authorization/audit outcomes.

The Engineering Import/Export cross-cutting requirement remains mandatory and is documented in `PROJECT GOAL.md`, `docs/ROADMAP.md` and the accepted ADRs.

## Current development pause

The user explicitly requested development to stop after a ChatGPT/platform error.

**Do not continue implementation automatically.**

Repository/document inspection and continuity maintenance are allowed when requested, but new product development should resume only after a new user instruction to continue.

## Next product slice when development is explicitly resumed

According to the current roadmap, the next major technical slice is:

1. introduce a **first-class operational command domain**;
2. only then enforce and audit `CommandExecute` against actual command objects;
3. extend authorization to sensitive read/realtime/WebSocket surfaces;
4. continue later security/user-lifecycle, audit durability/retention, historian retention/downsampling, MQTT, XLSX Engineering, diagnostics and frontend hardening according to `docs/ROADMAP.md`.

Important architectural rule already established: **do not create a fake/placeholder command endpoint merely to exercise `CommandExecute`; the command domain must exist first.**

## Resume checklist for the next ChatGPT task

Before doing anything else:

1. Read `PROJECT GOAL.md` completely.
2. Read this `LAST CHANGE.md` completely.
3. Fetch live `main` HEAD and recent commits.
4. Read `docs/ROADMAP.md` for ordered implementation status.
5. If the requested task touches a specific architecture/security/import-export area, read the corresponding ADR/document.
6. Compare any referenced working branch against current `main`; never assume an old branch is ahead.
7. Only then plan or modify code.
8. Validate changes through GitHub CI when .NET cannot be executed in the ChatGPT local environment.
9. Immediately before the final user-facing message, update this file again.

## Last user instruction governing state

The user wants project continuity to survive ChatGPT conversation limits. These two repository files are now part of the project operating process, independent of the roadmap:

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.

That rule remains in force until the user explicitly changes it.