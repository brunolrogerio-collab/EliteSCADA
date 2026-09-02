# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-02 BRT  
**Status:** **WAVE 12 COMPLETE / ACCEPTED; WAVE 14 #211 ACTIVE WITH FIRST CORRECTION INTAKE CLOSED; TEST PREVIEW #208/#210 ACTIVE AS VALIDATION HARNESS; WAVE 13 #205/#207 PAUSED AT GREEN CHECKPOINT**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Development Lead direction

Real owner use through the GitHub Codespaces browser Preview exposed material product/usability findings before final Windows signing. The execution order was therefore intentionally changed:

- **Wave 14 Product-owner validation #211 is the active product priority**;
- **Preview #208 / draft PR #210 is the reproducible validation harness**, not the authority for product scope;
- **Wave 13 #205 / draft PR #207 is paused** at its already-green repository-side checkpoint;
- the first owner correction intake has now been closed and partitioned into coordinated DEV packages in `docs/WAVE14-CORRECTION-PACKAGES.md`;
- final Wave 13 signing must target the accepted post-correction Wave 14 product, not the stale pre-validation product snapshot.

If a Windows installer is requested before final commercial/signing acceptance, its **product payload must still be the latest corrected/accepted Wave 14 build**, while the proven Windows packaging mechanism comes from Wave 13.

## 2. Mandatory resume protocol for the new coordinator

Before taking any code, branch, merge, CI or release action, read in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/WAVE14-CORRECTION-PACKAGES.md`;
5. `docs/ROADMAP.md`;
6. `docs/CI-VALIDATION-POLICY.md`;
7. live `main`, all open PRs/issues relevant to the active work and exact Actions state.

Then inspect:

8. issue #211 — Wave 14 Product-owner validation and its owner-finding comments;
9. issue #208 — Temporary browser Test Preview;
10. draft PR #210 / branch `preview/codespaces-test-preview`;
11. `docs/CODESPACES-PREVIEW-RUNBOOK.md` from the Preview branch while #210 remains unmerged;
12. issue #205 and draft PR #207 only to understand the **paused** Wave 13 release boundary.

Never trust SHAs copied into this document without re-checking live GitHub immediately before acting.

## 3. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.  
Wave 12 issue #201 is **COMPLETE / ACCEPTED / CLOSED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted lifecycle authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only. Working edits remain isolated until Save/Publish/Activate. Do not reopen accepted Wave 11/12 architecture without a demonstrated defect.

Permanent coordination boundaries remain:

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- no Preview-only auth/licensing/runtime bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity remains authoritative;
- product-code PRs to `main` require universal `EliteSCADA CI` plus appropriate impact-specific validation.

## 4. Current GitHub snapshot inspected for this handoff

Before the documentation-only correction-plan commits in this synchronization, inspected live `main` was:

`80cb7057cbc2656cf7b39c5d79c8a3adf8993778`

The new correction-package and handoff docs advance `main` with `[skip ci]`; those documentation commits do **not** represent a new validated product-code baseline.

Preview draft PR #210 was inspected as:

- state: **OPEN / DRAFT**;
- mergeable: true at the inspected moment;
- branch: `preview/codespaces-test-preview`;
- head: `0ab6e80c1c47a78b0bd33b07424d906b5f847faa`;
- latest product-code correction explicitly validated in that branch: `6304144a1beab6d4f3b4cf41b95fd16b5b82ba25`;
- exact validation retained on that product correction:
  - Test Preview #13 / `33652433077`: **SUCCESS**;
  - EliteSCADA CI #1136 / `33652432886`: **SUCCESS**;
  - Wave 11 Active HMI Runtime #66 / `33652432755`: **SUCCESS**.

PR #210 later contains runbook/docs commits, so `630414...` is **not** the current PR head. Always distinguish product-code SHA from later documentation head.

## 5. Preview / Codespaces state

Tracking:

- issue #208;
- draft PR #210;
- branch `preview/codespaces-test-preview`;
- operating procedure: `docs/CODESPACES-PREVIEW-RUNBOOK.md` on that branch.

The real Codespace path has already achieved actual browser entry and successful authentication through the real EliteSCADA login UI.

Repository-controlled Preview requirements learned from real Codespace use:

- exact .NET SDK 10.0.400;
- Node 24;
- TimescaleDB/PostgreSQL through Compose;
- disposable per-Codespace machine identity mounted read-only at `/etc/machine-id`, preserving normal fail-closed licensing;
- protected Codespaces secret named `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- automatic launcher through `postAttachCommand` -> `bash scripts/preview/launch-test-preview.sh`;
- Web forwarded on port 5173 and normally kept **Private**;
- API 5080 and database remain internal;
- normal product identity/lifecycle/licensing paths are used.

A forwarded 5173 HTTP 502 means forwarding exists but no Web process is listening. It is never an accepted ready state.

Recovery model from the runbook:

- **A — browser reload/HMR:** appropriate frontend-only change;
- **B — restart Preview launcher:** backend/launcher/process change or dead API/Web;
- **C — Rebuild Container:** devcontainer/Compose/SDK/environment change;
- **D — fresh Codespace:** ambiguous persisted/bootstrap state or clean reproducibility proof.

Any manual workaround required to make a real Codespace succeed must be converted to repository-controlled automation before the Preview path is accepted as reproducible.

### Password-policy chronology

Historical Preview evidence correctly states that a real bootstrap previously failed because its secret did not satisfy the **then-current 12-character minimum**.

Development Lead has now explicitly changed the product requirement to **minimum 8 characters**. This is a pending W14-C01 product correction, not yet something to assume exists in the current running Preview branch. Acceptance must include **7 characters rejected / 8 accepted**, with backend authority preserved.

Do not rewrite the historical runbook event as if the policy had always been 8. Never commit, echo or expose the actual protected password.

## 6. Wave 14 first correction intake is closed

Issue #211 remains the product finding/acceptance ledger. The first owner correction intake has been organized into `docs/WAVE14-CORRECTION-PACKAGES.md`.

Coordinator package model:

- **W14-C01 — Identity / secure first-run / password minimum 8**;
- **W14-C02 — backend-authoritative Driver catalog + Source/Driver forms**;
- **W14-C03 — DNP3 unrestricted production adapter / commercial unblock**;
- **W14-C04 — TAG Source selection + protocol-aware address/discovery assistants**;
- **W14-C05 — canonical visual properties + schema-driven Property Inspector**;
- **W14-C06 — Engineering Diagnostics / TAG Monitor boundary**;
- **W14-C07 — Screen Engineering + Dynamo authoring/library maturity**;
- **W14-C08 — Python Script Assistant / project object browser**;
- **W14-C09 — application shell + operator Runtime presentation**;
- **W14-C10 — integration/regression/real Preview acceptance**, owned by the coordinator rather than an unconstrained feature DEV.

Recommended concurrency:

### Stage A — parallel

C01, C02, C03, C05 and C06.

### Stage B — after prerequisite contracts stabilize

- C04 after C02;
- C07 after C05;
- C08 after C04/C05 interfaces stabilize enough to consume;
- C09 after a common Web integration baseline is frozen to reduce broad-shell conflicts.

### Stage C

C10 integration and acceptance.

Nine dedicated DEV chats can therefore own C01-C09 while the coordinator remains the tenth lane/integrator.

Do not let independent DEV agents merge directly to `main`. Each must receive an exact base SHA, bounded subsystem ownership, branch name, acceptance tests and a GitHub evidence obligation.

## 7. Owner findings captured in this first intake

The correction plan consolidates the material owner observations already recorded in #211, including:

- Script Engineering contrast/readability defect, with an already-validated narrow correction on the Preview branch;
- Driver selection must use the actual available catalog rather than typed internal strings;
- selecting a Driver must render fields specific to that protocol/transport;
- TAG Source selection must use configured Sources with searchable human-facing labels and stable canonical identity;
- protocol-aware TAG address editors and discovery/browse assistants, including OPC UA endpoint/address-space browse/import where supported;
- graphical Property Inspector must expose canonical object properties with appropriate editors such as a real color picker;
- visual properties must preserve generic Python read/write parity where permitted;
- TAG Monitor belongs to Engineering diagnostics while observing actual Active Runtime;
- Screen/Dynamo authoring and built-in component maturity need product-level correction;
- Python Script Engineering needs a project object/API assistant rather than memorized identifiers/syntax;
- Dark/Light shell themes, capability-pruned navigation, no-scroll uniformly scaled operator Runtime, screen navigation/Popups and secure first-run onboarding are binding Wave 14 acceptance requirements;
- password minimum is now 8 by explicit Development Lead direction.

Non-blocking preference/enhancement findings outside this material correction set should still be transferred to Wave 15 rather than silently growing Wave 14.

## 8. DNP3 correction / commercial blocker

This is now a dedicated P0 technical-risk package, W14-C03.

### Current product implementation

Product adapter project:

`src/Scada.Drivers.Dnp3.StepFunction`

Its project currently references NuGet package:

`dnp3` version `1.6.0`

The adapter README explicitly says the Step Function implementation is optional behind the vendor-neutral EliteSCADA DNP3 master-session contract, and documents the current upstream commercial/production/redistribution restriction. This isolation is valuable because the restricted implementation can potentially be replaced without changing the canonical EliteSCADA DNP3 surface.

### What the L3 lab actually uses

The L3 workflow builds an independent DNP3 peer from:

`interop-lab/dnp3-dnp3py/Dockerfile`

That Dockerfile clones:

`craigpnnl/dnp3py`

at pinned commit:

`8a20d4c276274f2b98800716cd7da963f21da2c1`

The pinned upstream source was inspected and confirms:

- **MIT License**;
- pure Python DNP3 implementation;
- no project dependencies declared in its `pyproject.toml`;
- documented Master and Outstation components;
- described as a DNP3 Level-2 subset.

Important distinction: in the current lab, `dnp3py` is the **independent peer/outstation used to test EliteSCADA**, not proof that the EliteSCADA production master adapter already uses it.

### Development direction

Investigate `dnp3py` first as the candidate replacement because its pinned source is permissively licensed and already participates in the proven lab. Do not declare the commercial blocker solved until W14-C03 proves:

- usable/maintainable Windows/.NET integration model;
- master feature parity adequate for EliteSCADA requirements;
- events/polling/quality/timestamps/reconnect and required commands/writes;
- explicit handling of unsupported features;
- DNP3/L3/universal CI green on exact SHA;
- final distributable dependency graph contains no restricted Step Function bytes/package if the replacement is accepted.

Until that proof exists, continue describing DNP3 commercial distribution as blocked by the current adapter licensing situation.

## 9. Dedicated Wave 14 integration branch boundary

Do not make Preview PR #210 the dumping ground for C01-C09. Preview is the reproduction/validation harness.

The coordinator should, after re-reading live GitHub, establish/confirm a dedicated Wave 14 correction integration workstream from a pinned live base and incorporate the already-proven Script Engineering product correction from #210 as appropriate. Then provide the same pinned integration contract to delegated DEV branches.

Every DEV must report back:

- base SHA;
- head SHA;
- files changed;
- tests/workflows run and exact results;
- architecture/security implications;
- unresolved risks/dependencies;
- whether the package is ready for coordinator integration.

Product-code changes must never use `[skip ci]`.

## 10. Wave 13 paused checkpoint

Issue #205 is **PAUSED BY DEVELOPMENT LEAD**. Draft PR #207 remains open/draft.

Preserved fully validated Wave 13 implementation SHA:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Retained validation:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Preserved branch head after its earlier docs synchronization:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

Do not advance final Authenticode signing/merge/release from that stale product snapshot while Wave 14 is changing product content.

When Wave 13 resumes:

1. re-fetch live `main` and accepted Wave 14 baseline;
2. incorporate/rebase the corrected product;
3. re-run Windows packaging/signing validation on the actual corrected bytes;
4. preserve external protected signing-authority/timestamp/private-key boundaries;
5. if W14-C03 has succeeded, verify DNP3 restricted dependency removal as part of release evidence.

## 11. Exact next coordinator action

1. re-fetch live `main`, #211, #208, #210, #205/#207 and current Actions;
2. read `docs/WAVE14-CORRECTION-PACKAGES.md`;
3. establish the pinned Wave 14 correction integration base/branch without polluting the Preview harness scope;
4. assign Stage A packages C01/C02/C03/C05/C06 to separate bounded DEV branches/chats;
5. require each DEV to work from GitHub state rather than copied chat history;
6. integrate only exact-head green package candidates in dependency order;
7. use real Codespace browser validation after integrated corrections;
8. record accepted evidence in #211;
9. establish the corrected Wave 14 baseline before any final Wave 13 signing or Windows commercial release action.
