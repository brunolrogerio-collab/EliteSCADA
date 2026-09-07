# LAST CHANGE — EliteSCADA

**Date:** 2026-09-07 BRT  
**Operational state:** **WAVE 14 ACTIVE / C25 ACCEPTED + INTEGRATION GREEN / C11 SYNCHRONIZING TO GREEN INTEGRATION / C11 PACKAGE PORTABILITY RED DIAGNOSED / #212 OPEN-DRAFT NOT AUTHORIZED / WAVE13 PAUSED**

> GitHub live is the official and sole development memory. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation. If this file differs from live GitHub, GitHub wins.

## Integration authority

C25 was explicitly accepted by the Product Owner on exact product/test candidate:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Retained evidence:

- Wave 14 C25 Post-Demo #314 / `34072225644` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #301 / `34072225635` — SUCCESS.

C25.10 coordination head:

`a781294ed2996888a8147c6f5fcaeb4eaddcf3c1`

C25 PR #283 merged only into `wave14/corrections-integration` with merge commit:

`f282758d47c0419f3534f948658701467807758b`

The post-C25 Chromium regression was diagnosed and corrected without weakening tests/contracts. Exact green integration head consumed by this C11 synchronization:

`ff185ffd67fe4abc597af9184c21f86376ba6e17`

Commit:

`fix(w14): restore Engineering E2E after lock integration`

Exact-SHA validation on `ff185ffd...`:

- EliteSCADA CI #1425 / `34079800458` — SUCCESS;
- Preview Licensing CI #373 — SUCCESS;
- Interop Lab Smoke #250 — SUCCESS;
- L3 Seven-Driver Lab #329 — SUCCESS;
- Wave 11 Active HMI Runtime #351 — SUCCESS.

The Chromium root causes were: a duplicate locale selector in unlocked Engineering Lock management plus an incomplete Report Designer test harness for `/api/engineering/lock/status`. The correction removed only the redundant unlocked selector and supplied the legitimate unlocked backend status in the isolated Report Designer scenario.

## C11 synchronization

Canonical branch:

`wave14/c11-canonical-eee-demo`

Pre-sync C11 head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

This synchronization is a normal two-parent history-preserving merge of:

1. C11 parent `41d24d89c3b9d2b881215255e44023fabde262f3`;
2. green integration parent `ff185ffd67fe4abc597af9184c21f86376ba6e17`.

Revalidate the live C11 branch head after this commit; do not infer the merge SHA from this file.

C11 product/application work preserved in the merge includes the canonical `eee-demo` Simulation project, generic Engineering lifecycle, HMI, package portability tests, real-EEE mapping and post-DEMO generic product-gap records.

## Diagnosed C11 pre-sync red

Exact pre-sync C11 head `41d24d...` had four normal gates green and Wave11 #338 / `33977314297` red.

The normal Wave11 browser lifecycle itself passed **22/22**. The sole failure was the isolated C11 canonical package portability gate when publishing a fresh `eee-demo` project.

Exact backend validation issue:

- `DYNAMO_TEMPLATE_NOT_FOUND`;
- entity `dynamo.pump.standard`;
- referenced template `pump.standard` was absent in the fresh project;
- Publish returned HTTP 400, so Export/Inspect/Re-preview could not proceed.

This failure is diagnosed. Do not blind-rerun unchanged. After synchronization, validate whether the accepted integration product resolves or changes this package behavior; if still red, correct the generic/package or fixture cause without hiding it behind EEE-specific behavior.

## Permanent governance

- PR #212 remains OPEN/DRAFT -> `main` and MUST NOT merge without a later, separate, explicit Product Owner authorization;
- `siga`, green CI, mergeability, C25 acceptance, C11 acceptance or Preview success do not authorize #212 -> `main`;
- PR #263 remains the C11 implementation route -> `wave14/corrections-integration` only;
- PR #266 remains validation-only -> `main` and **MUST NEVER MERGE**;
- temporary synchronization PR #284 is not an integration route and must be closed without merge after the manual history-preserving synchronization is confirmed;
- Wave 13 issue #205 / PR #207 remain paused; preserved Wave13 head is `fda87ba4445127c174f6ea533a6bcabaabc7bb20`;
- no direct `main` mutation, force push, destructive rebase, blind CI rerun, contract weakening or EEE-specific workaround.

## C11 binding product sequence

1. validate the synchronized exact C11 head;
2. finish canonical Simulation application-level validation;
3. prove `Save -> Publish -> Activate`;
4. prove canonical `eee-demo` package `Export -> Inspect -> Import Preview` and portability;
5. version/freeze `EliteSCADA-EEE-Demo.escadapkg` with checksum and provenance;
6. then resolve/audit whole-system backup/restore + Historian administration requirements;
7. implement/resolve generic TAG raw-to-engineering scaling;
8. implement/resolve normal human decimal-place authoring/runtime/package persistence;
9. exact-SHA validate those generic corrections;
10. update Preview harness and perform fresh Product Owner Codespace homologation;
11. only later build the real Modbus/PLC EEE variant;
12. final Wave14 acceptance;
13. only after separately authorized #212 -> `main` and validated new main may Wave13 release/signing resume.

Canonical detailed handoff:

`docs/CURRENT-COORDINATOR-HANDOFF.md`
