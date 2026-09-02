# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-02 BRT  
**Status:** **WAVE 12 COMPLETE / ACCEPTED; TEMPORARY BROWSER TEST PREVIEW #208 PLANNED / NEXT; WAVE 13 #205/#207 PAUSED**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Mandatory resume protocol

Read in this order before changing code:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/ROADMAP.md`;
5. `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`;
6. issue #208 — Temporary browser Test Preview;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live `main`, open PRs/issues and exact Actions state;
9. issue #205 and draft PR #207 only to understand the paused Wave 13 state;
10. for historical Wave 12 diagnosis only, `docs/WAVE-12-HARDENING-AUDIT.md` and issue #201.

If repository state differs from copied prose, GitHub/main/CI wins.

## 2. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.  
Wave 12 issue #201 is **COMPLETE / ACCEPTED / CLOSED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge acceptance evidence on that SHA:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted lifecycle authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Accepted Wave 11/12 architecture must not be reopened without a demonstrated defect.

Owner-test package from Wave 11 remains:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 3. Current direction — Temporary Browser Test Preview

Development Lead direction on 2026-09-02 inserted issue #208 as the active coordination target and paused Wave 13.

Objective:

Provide a temporary development/homologation environment where the real EliteSCADA can be started remotely and used through a browser without local installation.

Preferred operator experience:

`Open Codespace / Launch Test Preview -> automatic startup -> temporary authenticated Web URL -> use the real EliteSCADA`

Preferred technical direction to evaluate:

- GitHub Codespaces;
- automated .NET backend startup;
- React/Pyodide frontend available through the real product Web surface;
- PostgreSQL/TimescaleDB initialized inside the Codespace;
- automatic import of `EliteSCADA-Wave11-Demo.escadapkg` after revalidating location/checksum/compatibility;
- normal Save/Publish/Activate lifecycle to persisted Active Engineering;
- browser validation of Engineering, HMI Runtime, simulated TAGs, alarms, trends and other available Preview surfaces;
- only the required Web port forwarded;
- database/internal service ports private.

This is not a production hosting architecture, permanent public instance, supported deployment target, SLA or persistence guarantee.

Detailed requirements: `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`.  
Tracking: issue #208.

## 4. Preview administrative account / secret handling

A dedicated administrative test account is required with username:

`EliteSCADA`

The Development Lead supplied the requested password directly. Because the repository is public, the password must not be copied into source control, devcontainer files, workflow YAML, docs, Docker image layers, `.escadapkg` fixtures, logs or normal artifacts.

The Preview bootstrap should receive it via a protected Codespaces/GitHub secret or equivalent environment variable such as:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

Fail clearly when the protected value is missing; do not introduce a repository-embedded fallback password.

## 5. Preview implementation entry audit

Before coding, audit and record the exact current surfaces for:

- existing local/development startup procedures;
- .NET/Web binding and port model;
- PostgreSQL/TimescaleDB setup and migrations;
- local identity/admin creation and role assignment;
- `.escadapkg` import/open/save APIs/services;
- Publish/Activate APIs/services;
- Demo-mode and licensing startup contracts;
- existing Docker/devcontainer/Codespaces assets, if any;
- frontend/Pyodide asset preparation;
- current Wave 11 Demo package location/artifact availability.

Prefer reuse of accepted APIs/services. Do not create a parallel bootstrap path that bypasses normal backend authority merely because scripting it is easier.

## 6. Wave 13 paused state

Issue #205 remains open. Draft PR #207 on branch `wave13/windows-release-signing` preserves the implementation and audit work performed before the pivot.

State: **PAUSED**.

Do not merge or extend #207 while #208 is the active coordination direction. When Wave 13 resumes, re-read live `main`, current exact-SHA CI, issue #205, PR #207 and the release/signing audit before continuing. Preview work may change launch/package surfaces, so the paused branch must not be assumed current automatically.

Existing Wave 13 locks remain intact:

- controlled Windows x64 release package;
- Authenticode with trusted timestamp;
- protected organizational signing boundary;
- deterministic/fail-closed manifest and signed-byte verification;
- no private code-signing material in source/GitHub/normal artifacts/logs;
- commercial DNP3 distribution remains gated pending appropriate license or approved/revalidated replacement.

## 7. CI / merge rules

- EliteSCADA CI remains the universal Coordinator gate for PRs to `main` when product code changes;
- Preview-specific validation may complement but never replace universal CI;
- diagnose failures before rerun;
- do not weaken authentication, authorization, Runtime authority or tests to make Codespaces convenient;
- integration uses expected-head protection;
- validate post-merge `main` for any product-code Preview implementation before calling the Preview ready;
- documentation-only coordination changes may use `[skip ci]` according to repository policy.

## 8. Explicit exclusions

Do not include in the temporary Test Preview unless separately authorized:

- permanent/public production hosting;
- new Drivers/protocols;
- unrelated HMI/Engineering feature work;
- Wave 13 release-signing continuation;
- Wave 14 owner-validation execution;
- Wave 15 feedback/corrections;
- Linux `.deb` implementation;
- physical Driver L4 claims.
