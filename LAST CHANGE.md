# LAST CHANGE — EliteSCADA

**Date:** 2026-09-02 (BRT)  
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; TEST PREVIEW #208/#210 — IMPLEMENTED / REAL CODESPACE VALIDATION PENDING; WAVE 13 #205/#207 — ACTIVE UNDER SEPARATE COORDINATION**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. Accepted product baseline

Wave 12 Hardening is **COMPLETE / ACCEPTED**.

Final accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Accepted runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

## 2. Final Wave 12 acceptance evidence

Exact product SHA `63bced02426fcb84b26028913f6c68feb3457d80`:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Historical Wave 12 detail remains in `docs/WAVE-12-HARDENING-AUDIT.md` and issue #201.

## 3. Coordination split authorized on 2026-09-02

The Development Lead released Wave 13 from the temporary Preview pause and split coordination into two concurrent workstreams:

### A. Temporary Browser Test Preview

- issue #208;
- draft PR #210;
- branch `preview/codespaces-test-preview`;
- current coordinator remains focused on this workstream.

### B. Wave 13 Windows release/signing

- issue #205;
- draft PR #207;
- branch `wave13/windows-release-signing`;
- released for a separate coordinator to continue independently.

Neither front blocks the other. Neither coordinator may assume the other branch has merged. Live GitHub state and exact-head CI must be rechecked before merge/release decisions.

## 4. Preview current state

Last validated Preview head:

`208ac69b5638ace8557a700d34dd16571360c8f6`

Exact-head evidence:

- Test Preview #4 / `33594259242`: **SUCCESS**;
- EliteSCADA CI #1122 / `33594259232`: **SUCCESS**.

The Preview implementation has no product-code diff. It adds the Codespaces/devcontainer + Compose topology, private TimescaleDB, Web port 5173 forwarding, protected admin secret injection, validated Wave 11 Demo fixture, launcher/task, normal Import -> Save -> Publish -> Activate bootstrap and browser/Pyodide smoke.

Final Preview acceptance remains pending a **fresh real Codespace** and confirmation that the forwarded private Web URL opens the actual EliteSCADA and representative browser behavior is usable.

Administrative username:

`EliteSCADA`

Protected secret name:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

The supplied password must never be committed or copied into docs, workflows, packages, images, logs or normal artifacts.

## 5. Wave 13 released state

Issue #205 is now **ACTIVE / RELEASED FOR SEPARATE COORDINATION**.

PR #207 remains **draft**. Removing the pause does not make the existing branch merge-ready or accepted.

Before Wave 13 development continues, its incoming coordinator must read the required project/handoff documents, inspect live `main`, issue #205, PR #207, current exact-SHA CI and the release/signing audit, then compare `wave13/windows-release-signing` against live `main`.

Concurrent Preview #208/#210 work must be inspected for repository/launch changes, but only changes actually merged into `main` become Wave 13 package assumptions.

Wave 13 remains responsible for:

- Windows x64 packaging;
- Authenticode signing;
- trusted timestamping;
- deterministic/fail-closed signed-byte manifest verification;
- existing security/licensing gates;
- DNP3 commercial-distribution gate.

## 6. Exact next action — Preview coordinator

1. verify live `main`, PR #210 head and current exact-head CI before any new change;
2. continue only issue #208 / PR #210;
3. diagnose the fresh Codespace startup problem seen during owner validation;
4. obtain a real forwarded private Web URL from a fresh Codespace;
5. validate login and representative Engineering/Runtime behavior;
6. only then close/accept #208 and integrate #210 according to CI/merge policy.

Do not develop Wave 13 as part of the Preview workstream.

## 7. Exact next action — Wave 13 coordinator

1. read `PROJECT GOAL.md`;
2. read this file;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read `docs/ROADMAP.md`;
5. read `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`;
6. read issue #205 and full draft PR #207 state;
7. read `docs/CI-VALIDATION-POLICY.md`;
8. inspect #208/#210 only for concurrent changes relevant to launch/package assumptions;
9. verify live `main`, open PRs/issues and exact Actions state;
10. compare the Wave 13 branch with live `main` before modifying it.

Do not begin Wave 14 owner validation, Wave 15 corrections, Linux packaging or physical L4 work without their own authorization/gates.
