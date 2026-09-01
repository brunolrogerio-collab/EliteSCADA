# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 12 #201 IN PROGRESS — AUDIT COMPLETE / IMPLEMENTATION STARTED**

> Mutable Coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Live GitHub refs and exact-SHA Actions override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA.

## 1. Last accepted product-code mainline

Wave 11 Active Engineering HMI Runtime and its owner-test package handoff are **IMPLEMENTED / MERGED / POST-MAIN VALIDATED / ACCEPTED**.

### Wave 11 implementation integration

- issue #194: **CLOSED / COMPLETED** on 2026-09-01 after final acceptance evidence and continuity synchronization;
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
- final validated product-code `main` merge: `4ccc29cb4bb334dc473d8265f48a9c8601993413`;
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

## 3. Wave 12 active coordination

Wave 12 is **IN PROGRESS** under issue #201.

Preparation artifact:

- `docs/WAVE-12-HARDENING-PREPARATION.md`.

Active audit artifact:

- `docs/WAVE-12-HARDENING-AUDIT.md`.

Formal start state:

- live `main` was fetched and revalidated at `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`;
- dedicated branch: `coordination/wave12-hardening`;
- open PRs at start: none;
- open coordination issues at start: #201 plus intentionally deferred L4 issue #178;
- latest product-code validation remains Wave 11 workflow #14 / `33552016447` and EliteSCADA CI #1067 / `33552016454`, both successful at `4ccc29cb4bb334dc473d8265f48a9c8601993413`;
- commits from that product-code SHA through the Wave 12 branch base are documentation-only `[skip ci]` commits.

The pre-branch audit identified concrete hardening work in realtime WebSocket isolation, consistent/serialized Engineering saves, bounded Engineering import ingress, `.escadapkg` export-limit symmetry, persistence Apply concurrency/CAS, local-identity mutation races, audit-outage disposition and diagnostic/request-boundary consistency. Exact findings, existing controls and ordered slices are recorded in `docs/WAVE-12-HARDENING-AUDIT.md`.

Wave 12 purpose is hardening of accepted behavior before Wave 13 packaging/signing. The prepared scope covers fail-closed/recovery behavior, authorization/audit, persistence/restart, `.escadapkg` integrity, fault/resource isolation, concurrency, diagnostics sanitization and regression/CI hardening.

Current implementation boundary:

- the branch exists and the formal coordination/documentation start is being committed before production-code changes;
- no Wave 12 PR exists yet;
- no Wave 12 production-code acceptance or CI evidence is claimed yet;
- Wave 13, Linux `.deb` packaging and physical L4 work remain explicitly outside this branch.

Issue #201 is the active coordination surface. It must not be closed until identified defects are fixed with regression evidence or explicitly dispositioned with residual risk and exact-SHA CI evidence is green.

## 4. Other durable state

- Pre-Wave-11 gate #191: **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Issue #178 remains intentionally open only as deferred L4 Siemens hardware/vendor-simulator evidence; it is not a Wave 12 implementation blocker.
- `EliteSCADA CI` remains the universal Coordinator acceptance gate.
- Preview Licensing CI and L3 Seven-Driver Lab remain affected-subsystem/manual-release validation surfaces.
- `main` currently has no GitHub-enforced branch protection/required status checks; the universal CI rule is operational.
- Historical branch refs must not be repointed merely for cosmetic cleanup.

### Future Linux / Debian distribution direction

A future official **Linux x64 / Debian `.deb`** distribution is now **SPECIFIED / NOT STARTED**. This is not part of the current Wave 12 execution and must start only when explicitly requested by the Development Lead.

Durable specification: `docs/LINUX-DEBIAN-DISTRIBUTION.md`.

Planned first targets and boundaries:

- Debian 12 `amd64` first, followed by Debian 13 homologation;
- `linux-x64` self-contained, initially multi-file inside one `.deb` artifact;
- product layout `/usr/lib/elitescada`, configuration `/etc/elitescada`, mutable state `/var/lib/elitescada`;
- system user/group + `elitescada.service`;
- React/Pyodide integrated into the product served by Kestrel;
- externally configured PostgreSQL/TimescaleDB;
- Linux machine-bound licensing through the existing Machine Request Code/signed-license contract; Windows-only offline License Generator may remain authority-side;
- clean Debian install/upgrade/reboot validation, installed Runtime/Web/licensing/Driver tests and SBOM/dependency-license audit.

Commercial dependency gate: Step Function I/O `dnp3` 1.6.0 is publicly licensed for non-commercial/non-production use. Before any commercial EliteSCADA distribution includes/enables that Driver, obtain an appropriate commercial license or approve/revalidate an alternative. DNP3 licensing remediation is expected before the future `.deb` front unless an authorized package explicitly excludes DNP3.

No Linux packaging branch, PR, code change or Linux distribution CI has been created in this coordination cycle.

## 5. Exact next action

Continue Wave 12 on `coordination/wave12-hardening`; do **not** resume Wave 11 branches and do **not** start Linux `.deb`, Wave 13 signing or physical L4 work.

Immediate execution order:

1. implement the first audited hardening slice: isolate slow/stalled realtime WebSocket clients, serialize consistent Engineering saves, bound JSON/CSV import bodies and make `.escadapkg` export obey its own import limits;
2. add focused regression tests for each root cause;
3. commit the slice coherently, open the Wave 12 PR and require EliteSCADA CI on its exact head SHA;
4. continue through the remaining audited concurrency, identity, audit-durability and request/diagnostic dispositions without weakening existing boundaries;
5. run specialized CI only if the actual diff reaches a specialized impact surface;
6. validate post-merge `main` when the completed Wave 12 change justifies it;
7. keep Wave 13 Authenticode/release-signing work separate and preserve the future Linux packaging direction and DNP3 commercial-license gate without starting those fronts.

`PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md` and the Wave 12 audit/preparation records are synchronized with this start. No locked current architecture was relaxed. Wave 13 remains the mandatory signed Windows x64 + Authenticode/trusted-timestamp stage; Waves 14/15 remain owner validation and feedback/corrections; physical L4 remains later device/site validation.
