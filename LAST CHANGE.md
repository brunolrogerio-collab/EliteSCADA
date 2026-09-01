# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 11 ACCEPTANCE GATE SATISFIED / WAVE 12 #201 PREPARED — NOT STARTED**

> Mutable Coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Live GitHub refs and exact-SHA Actions override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA.

## 1. Last accepted product-code mainline

Wave 11 Active Engineering HMI Runtime and its owner-test package handoff are **IMPLEMENTED / MERGED / POST-MAIN VALIDATED**.

### Wave 11 implementation integration

- issue #194: acceptance gate technically satisfied; final issue closure occurs in this coordination cycle after documentation synchronization;
- original draft PR #195: **CLOSED / UNMERGED / SUPERSEDED**;
- replacement implementation PR #199: **MERGED**;
- validated implementation head: `a03237feed578066a8a62f5837adb60f100f412a`;
- Wave 11 code merge on `main`: `57042b467471f4b1360e1642d5d160e6e66fc31c`.

Post-main evidence for the Wave 11 code merge:

- Wave 11 Active HMI Runtime #11 / `33548047016`: **SUCCESS**;
- EliteSCADA CI #1064 / `33548047037`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E.

### Owner-test application handoff

- PR #200 — `Wave 11 owner-test Demo package handoff`: **MERGED**;
- exact PR #200 head: `cc37be24ad8c8dc4594d99c5a3fd232dbf685d6f`;
- PR #200 pre-merge Wave 11 workflow #13 / `33551000846`: **SUCCESS**;
- PR #200 pre-merge EliteSCADA CI #1066 / `33551000852`: **SUCCESS**;
- final product-code `main` merge: `4ccc29cb4bb334dc473d8265f48a9c8601993413`;
- post-main Wave 11 workflow #14 / `33552016447`: **SUCCESS**;
- post-main EliteSCADA CI #1067 / `33552016454`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E.

Final owner-test artifact from post-main Wave 11 workflow #14:

- artifact name: `EliteSCADA-Wave11-Demo`;
- artifact id: `9817878392`;
- retention expiry: 2026-11-30;
- artifact ZIP digest: `sha256:2944b946bf0085e260aa147eb7da1711ba1ef9f496961724ebfe8053c1368f96`;
- application file: `EliteSCADA-Wave11-Demo.escadapkg`;
- application size: 5,394 bytes;
- application SHA-256: `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`.

Independent inspection confirmed:

- package format `elitescada.project-package`, format version 2;
- Engineering schema `scada.engineering`, schema version 15;
- project `e2e-wave11`, active revision 2;
- canonical screen `demo.overview` with `REVISION B ACTIVE`;
- one real PNG visual asset stored as package sidecar;
- manifest byte lengths and SHA-256 values match the packaged `engineering.json` and PNG;
- metadata records `validatedAgainstActiveRuntime: true` and the runtime/inspect/import-preview validation boundaries.

## 2. Accepted Wave 11 product behavior

Wave 11 establishes the Runtime application authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Accepted behavior includes:

- protected `/api/runtime/application` projection from persisted Active Engineering, never mutable Working;
- fail-closed project/revision/persistence/package consistency behavior;
- canonical Screen/Popup/Dynamo Runtime mount using the existing renderer/navigation path;
- Active revision visual assets with integrity validation and explicit Runtime asset authority;
- stable Runtime mount while the Active project/revision identity is unchanged;
- Runtime `View` authorization without granting operator Working Engineering read authority;
- explicit Simulation fallback only when no Engineering Runtime is active;
- protected Slider/TAG writes through `/api/tags/{id}/write`;
- automated proof of Active A -> Working isolation -> Active B, fail-closed behavior and Active PNG serving.

No test, security or lifecycle boundary was weakened to obtain acceptance.

## 3. Wave 12 preparation

Wave 12 is **PREPARED / NOT STARTED** under issue #201.

Preparation artifact:

- `docs/WAVE-12-HARDENING-PREPARATION.md`.

Wave 12 purpose is hardening of accepted behavior before Wave 13 packaging/signing. Preparation covers fail-closed/recovery behavior, authorization/audit, persistence/restart, `.escadapkg` integrity, fault/resource isolation, concurrency, diagnostics sanitization and regression/CI hardening.

Explicitly not started:

- no Wave 12 implementation branch exists;
- no Wave 12 PR exists;
- no Wave 12 production-code change has been made;
- no Wave 12 CI evidence is claimed.

Issue #201 remains preparation-only until issue #194 is closed. After #194 closes it becomes **READY / NOT STARTED**, and the next Coordinator must re-read live `main` before creating a dedicated Wave 12 branch.

## 4. Other durable state

- Pre-Wave-11 gate #191: **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Issue #178 remains intentionally open only as deferred L4 Siemens hardware/vendor-simulator evidence; it is not a Wave 12 implementation blocker.
- `EliteSCADA CI` remains the universal Coordinator acceptance gate.
- Preview Licensing CI and L3 Seven-Driver Lab remain affected-subsystem/manual-release validation surfaces.
- `main` currently has no GitHub-enforced branch protection/required status checks; the universal CI rule is operational.
- Historical branch refs must not be repointed merely for cosmetic cleanup.

## 5. Exact next action for the next Coordinator

Do **not** continue Wave 11 implementation and do **not** infer that Wave 12 has already started.

At actual Wave 12 start:

1. read live `PROJECT GOAL.md` and `LAST CHANGE.md` first;
2. read `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/ROADMAP.md`, `docs/WAVE-12-HARDENING-PREPARATION.md` and issue #201;
3. re-read live `main`, open PRs/issues and exact Actions state;
4. audit current failure/test surfaces before choosing hardening slices;
5. create a dedicated Wave 12 branch from the then-live `main` only at that point;
6. keep Wave 13 Authenticode/release-signing work separate.

`PROJECT GOAL.md` is being reviewed/synchronized in this same coordination cycle. No locked product architecture is being relaxed. Wave 13 remains the mandatory signed Windows x64 + Authenticode/trusted-timestamp stage; Waves 14/15 remain owner validation and feedback/corrections; physical L4 remains later device/site validation.
