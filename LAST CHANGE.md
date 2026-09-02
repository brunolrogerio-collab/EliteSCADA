# LAST CHANGE — EliteSCADA

**Date:** 2026-09-02 (BRT)  
**Operational state:** **WAVE 12 #201 COMPLETE / ACCEPTED; WAVE 14 #211 ACTIVE WITH DEDICATED INTEGRATION PR #212 AND STAGE A DEVS DISPATCHED; TEST PREVIEW #208/#210 ACTIVE AS VALIDATION HARNESS; WAVE 13 #205/#207 PAUSED AT GREEN CHECKPOINT**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. What changed in this synchronization

### Wave 14 correction integration started

Live GitHub was revalidated before branch creation:

- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684`;
- issue #211: open with the first correction intake closed;
- issue #208 / draft PR #210: open Preview harness, head `0ab6e80c1c47a78b0bd33b07424d906b5f847faa`;
- exact Preview-head validation: Test Preview #14 / `33654310816`, EliteSCADA CI #1137 / `33654310731` and Wave 11 Active HMI Runtime #67 / `33654311040` — all **SUCCESS**;
- issue #205 / draft PR #207: still paused/open/draft, head `fda87ba4445127c174f6ea533a6bcabaabc7bb20`.

The coordinator created the dedicated branch `wave14/corrections-integration` and draft PR #212, separate from Preview PR #210.

Pinned Stage A DEV base:

`c6bd4c4d09dc902f1571750e95b7f0460ba3b77a`

That base starts from live `main` `edbdf446...` and contains only the already-proven Script Engineering correction transferred from Preview with source provenance retained:

- integration fix commit `26afb0f9a8022f55737a50fabf6d28c44deb0fbd`, sourced from `cd7fca14d9b23b2f40417dcc282520728a095905`;
- integration test commit `c6bd4c4d09dc902f1571750e95b7f0460ba3b77a`, sourced from validated Preview SHA `6304144a1beab6d4f3b4cf41b95fd16b5b82ba25`.

Stage A branches were created from that exact base and assigned as bounded workstreams:

- `wave14/c01-secure-first-run`;
- `wave14/c02-driver-catalog-forms`;
- `wave14/c03-dnp3-unrestricted-adapter`;
- `wave14/c05-visual-property-inspector`;
- `wave14/c06-engineering-tag-monitor`.

No package DEV may merge directly to `main` or use PR #210 as a feature bucket. The coordinator owns integration through #212 and C10.

### Previous correction-plan synchronization

The Development Lead completed the first Wave 14 owner-correction intake and requested that work be organized for multiple independent DEV chats/agents under one new coordinator.

Repository coordination was updated accordingly:

- created `docs/WAVE14-CORRECTION-PACKAGES.md`;
- updated `docs/CURRENT-COORDINATOR-HANDOFF.md` with current correction, project and Codespaces Preview context;
- split implementation into nine bounded DEV packages plus one coordinator integration/acceptance gate;
- updated the Wave 14 first-run requirement in issue #211 to supersede the old 12-character password rule with **minimum 8 characters**;
- added DNP3 unrestricted-adapter investigation as a P0 commercial-release blocker.

Documentation commits created in this synchronization before this `LAST CHANGE.md` update:

- `b8d47539829bd1be0a5bc5140b570dfb81d6678f` — Wave 14 correction packages;
- `27c41c9fa04a731e4864ccb958c2cd74d88b1df0` — current coordinator handoff.

These are documentation-only `[skip ci]` commits and do not establish a new product-code acceptance baseline.

## 2. Mandatory resume reading

A new coordinator must read, in order:

1. `PROJECT GOAL.md`;
2. this `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-CORRECTION-PACKAGES.md`;
5. `docs/ROADMAP.md`;
6. `docs/CI-VALIDATION-POLICY.md`;
7. live `main`, issue #211, issue #208, draft PR #210, and exact current Actions;
8. issue #205 / draft PR #207 only to understand the paused Wave 13 boundary.

GitHub, not previous chats, is the development memory.

## 3. Accepted foundation

Wave 12 remains **COMPLETE / ACCEPTED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Exact post-merge Wave 12 evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Do not reopen accepted Wave 11/12 architecture without a demonstrated defect.

## 4. Wave 14 correction packages

Canonical package plan: `docs/WAVE14-CORRECTION-PACKAGES.md`.

Current execution checkpoint: Stage A C01/C02/C03/C05/C06 is in progress on the dedicated branches recorded above, all pinned to base `c6bd4c4d09dc902f1571750e95b7f0460ba3b77a`. C04/C07/C08/C09 have not been dispatched yet because their prerequisite/common-Web bases are not stable. C10 remains coordinator-owned.

### Stage A — may start in parallel

- **W14-C01:** Identity / secure first-run / password minimum 8;
- **W14-C02:** backend-authoritative Driver catalog + Source/Driver forms;
- **W14-C03:** DNP3 unrestricted production adapter / commercial unblock;
- **W14-C05:** canonical visual properties + schema-driven Property Inspector;
- **W14-C06:** Engineering Diagnostics / TAG Monitor product boundary.

### Stage B — consume prerequisite contracts

- **W14-C04:** TAG Source selection + protocol-aware address/discovery assistants, after C02;
- **W14-C07:** Screen Engineering + Dynamo maturity, after C05;
- **W14-C08:** Python Script Assistant / project object browser, after C04/C05 contracts are sufficiently stable;
- **W14-C09:** application shell + operator Runtime presentation, preferably after a common Web integration baseline is frozen.

### Stage C — coordinator-owned gate

- **W14-C10:** integration, regression, exact-head CI, real Codespace/browser owner validation and accepted corrected Wave 14 baseline.

Nine dedicated DEV chats can therefore own C01-C09. The coordinator should remain the tenth lane and must control integration rather than having a tenth independent agent edit overlapping product code.

Every DEV must receive an exact live base SHA, bounded subsystem ownership, branch name, architecture/security constraints, acceptance tests and GitHub evidence obligation. No independent DEV silently merges to `main`.

## 5. Password policy update — requested, not yet implemented

Development Lead explicitly changed the desired product minimum password length from **12 to 8 characters**.

This direction now supersedes the old 12-character requirement in the Wave 14 first-run acceptance comment on issue #211.

W14-C01 must implement the actual product change. Until a product-code correction is integrated and accepted, current product/Preview code may still enforce the old 12-character minimum.

Acceptance requirement:

- 7 characters rejected;
- 8 characters accepted;
- backend remains canonical authority;
- upper-bound/other security behavior remains unchanged unless separately justified;
- Preview/bootstrap hints and checks follow the accepted new product policy;
- historical runbook evidence remains truthful that an earlier Codespace failed under the then-current 12-character policy.

## 6. DNP3 investigation — important new evidence

Current product adapter:

`src/Scada.Drivers.Dnp3.StepFunction`

Its project references Step Function NuGet package:

`dnp3` version `1.6.0`

The adapter README already isolates Step Function behind the vendor-neutral EliteSCADA DNP3 master-session contract and records the current commercial/production/redistribution restriction.

The L3 Seven-Driver Lab uses an independent DNP3 peer built from:

`interop-lab/dnp3-dnp3py/Dockerfile`

That Dockerfile pins `craigpnnl/dnp3py` commit:

`8a20d4c276274f2b98800716cd7da963f21da2c1`

Inspection of that pinned source confirms:

- MIT License;
- pure Python implementation;
- `pyproject.toml` declares no project dependencies;
- Master and Outstation components are documented;
- project describes a DNP3 Level-2 subset.

This makes `dnp3py` a promising first replacement candidate, but the current L3 lab uses it as the **peer/outstation**, not as the EliteSCADA production adapter. Therefore the commercial blocker is **not yet removed**.

W14-C03 owns the proof of:

- maintainable Windows/.NET integration;
- master feature adequacy/parity;
- polling/events/quality/timestamps/reconnect and required write/command behavior;
- DNP3-specific + L3 Seven-Driver + universal CI on exact SHA;
- final product dependency closure with no restricted Step Function package/bytes before commercial unblock is declared.

## 7. Preview / Codespaces current state

Tracking:

- issue #208;
- draft PR #210;
- branch `preview/codespaces-test-preview`.

Live PR #210 inspection during this synchronization showed:

- OPEN / DRAFT / mergeable at inspection time;
- head `0ab6e80c1c47a78b0bd33b07424d906b5f847faa`;
- latest explicitly validated product-code correction `6304144a1beab6d4f3b4cf41b95fd16b5b82ba25`;
- exact validation on that product SHA:
  - Test Preview #13 / `33652433077`: SUCCESS;
  - EliteSCADA CI #1136 / `33652432886`: SUCCESS;
  - Wave 11 Active HMI Runtime #66 / `33652432755`: SUCCESS.

Real browser entry and actual EliteSCADA login have already succeeded in Codespaces.

Preview runbook:

`docs/CODESPACES-PREVIEW-RUNBOOK.md` on the Preview branch while #210 remains unmerged.

Key environment contract:

- .NET SDK 10.0.400;
- Node 24;
- TimescaleDB/PostgreSQL through Compose;
- disposable read-only `/etc/machine-id` for normal fail-closed licensing;
- secret name `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- automatic launcher `bash scripts/preview/launch-test-preview.sh` via `postAttachCommand`;
- Web 5173 Private; API 5080/DB internal;
- HTTP 502 on 5173 means Web is not listening, not ready.

Do not put C01-C09 directly into PR #210 merely because Preview reproduces the defects. #208/#210 is the harness. #211/Wave 14 owns product correction scope.

## 8. Wave 13 remains paused

Preserved fully validated Wave 13 implementation SHA:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Preserved branch head after earlier docs synchronization:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

Retained validation:

- Wave 13 Windows Release #27 / `33643546191`: SUCCESS;
- EliteSCADA CI #1134 / `33643546119`: SUCCESS;
- L3 Seven-Driver Lab #102 / `33643546111`: SUCCESS;
- Wave 11 Active HMI Runtime #64 / `33643546139`: SUCCESS.

Do not advance final Authenticode signing, merge or commercial release from that stale product snapshot while Wave 14 is changing the product.

## 9. Windows installer rule

Whenever the Development Lead asks for a Windows installer during Wave 14 corrections:

`product = latest corrected/accepted Wave 14 baseline`

`Windows packaging mechanism = proven Wave 13 machinery`

Record exact product SHA and packaging SHA. Never silently fall back to the old Wave 13 product snapshot merely because its workflow is already green.

If W14-C03 succeeds before the installer/release, verify that the selected product/package no longer carries the restricted Step Function DNP3 dependency before representing DNP3 as commercially unblocked.

## 10. Exact next action

1. keep draft PR #212 exact-head CI under observation;
2. review each Stage A DEV candidate against its fixed package scope and exact base;
3. publish package commits/PR evidence without allowing direct merges to `main`;
4. stabilize C02 and C05 contracts, then dispatch C04 and C07;
5. dispatch C08 only after C04/C05 interfaces are stable and C09 only after a common Web integration base is frozen;
6. integrate exact-head green work through #212 in dependency order;
7. run affected specialized workflows and real Codespace/browser owner validation on the integrated SHA;
8. record evidence in issue #211 and establish one accepted corrected Wave 14 product baseline before final Wave 13 signing resumes.
