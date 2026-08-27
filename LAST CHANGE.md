# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE PRODUCT DEVELOPMENT — SECOND WAVE DELIVERED / DRIVER SDK RESEARCH CONVERGENCE MERGED**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Merged product/platform foundations include Internal Memory, TAG Gateway, common multi-Data-Source diagnostics, Engineering Schema v9, the first integrated interface checkpoint through PR #58, all protocol/visual/Python research documents and the Driver SDK research-convergence hardening through PR #68.

The active product order remains:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

The provisional Windows x64 validation package remains postponed until the interface reaches a materially useful validation state.

## DRIVER SDK RESEARCH CONVERGENCE — MERGED

PR #68 `Converge merged research into Driver SDK architecture` is **MERGED**.

- exact final head: `44cc5a9b20b3b8f71cae4ea3e602880878b0ae4a`;
- exact-head EliteSCADA CI #391: **SUCCESS**;
- Web build: PASS;
- backend Release build: PASS;
- full automated tests: PASS;
- runtime smoke: PASS;
- Chromium end-to-end: PASS;
- merge SHA: `ec82389d1f27c9929b680e8174b38ca72bcf3b54`.

The convergence reviewed all merged research streams together: MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection, Allen-Bradley Logix EtherNet/IP/CIP, Client Visual Python sandbox/editor and graphical visual editor.

### New official Driver SDK foundations

- Active Runtime communication remains on the small `ICommunicationDriver` boundary.
- Protected Engineering tooling is now a separate capability surface.
- Driver types may independently expose connection test, discovery, browse, file import and reconciliation instead of implementing one oversized fake-universal interface.
- Driver descriptors now have a versioned public Data Source/TAG-binding configuration-schema direction and localization resource keys.
- Runtime acquisition modes are explicitly representable as Polling, Subscription, EventDriven or Hybrid without changing the common TAG pipeline.
- `TagValue.Timestamp` remains the local EliteSCADA observation/publication time and optional `SourceTimestamp` / `ServerTimestamp` preserve real protocol timestamps when available.
- Driver reconciliation status is typed rather than free-form text.
- Existing early `DriverCapabilities.Browse/Discover` remain only for compatibility; future Engineering discovery/browse uses the dedicated Engineering-capability contracts.
- protocol-library objects, subscription handles, browse indexes and similar implementation state remain non-authoritative runtime/import caches.

New architecture documents are official:

- `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md`;
- `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`;
- `docs/RESEARCH-CONVERGENCE-READINESS.md`.

`docs/ADR-002-DRIVER-SDK-AND-REALTIME.md`, `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md` and `docs/ARCHITECTURE.md` were reconciled with the merged research.

### Explicitly not implemented by PR #68

- no MQTT/OPC UA/BACnet/S7/Allen-Bradley production runtime;
- no production protocol package/dependency selection;
- no Driver Module loader/runtime registration implementation;
- no Engineering Schema v9 migration or final rich protocol TAG-binding DTO;
- no secret resolver/trust-store implementation;
- no Python engine/editor;
- no graphical Screen/Popup/Dynamo editor.

This is architecture hardening so later implementation fits one coherent platform rather than each protocol redefining the product.

## RESEARCH BACKLOG CONSOLIDATED INTO MAIN

Five previously open research PRs plus earlier OPC UA/S7 research are official architecture/evidence:

- PR #53 graphical visual editor architecture — CI #383 green, merge `491ee337bf2723d13d2759bc677300edd34e1fca`;
- PR #54 Client Visual Python editor/browser sandbox — CI #384 green, merge `80d06ea467c7c844807c0548940308ccf74a7510`;
- PR #62 BACnet/IP + BACnet/SC architecture — CI #380 green, merge `c60c611465bd82a898ee30d5f67fe79234381b8c`;
- PR #63 MQTT industrial Data Source architecture — CI #381 green, merge `05df6bc63893cb025f87899d27a5988b2e1cf896`;
- PR #64 Allen-Bradley EtherNet/IP/CIP Logix architecture — CI #382 green, merge `a71ce2d962d6b122714b61b5851465d9c284e7b6`.

These remain **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED**. The convergence in PR #68 converts common conclusions into platform contracts; it does not reopen product gates.

## SECOND INTERFACE WAVE — DELIVERED IN OPEN PRS

The three worker assignments are now delivered as green open PRs and remain **IMPLEMENTED IN PR / NOT MERGED**:

- **DEV 1 / PR #65:** Engineering Alarm Workspace ergonomics; exact worker head `071b56b532cf14039d1c0cab9891fc06a27f9873`; CI #388 SUCCESS.
- **DEV 2 / PR #67:** Runtime Alarm Center + protected acknowledgement UX; exact worker head `0c2baae9f3eabe501fa4b1790f4f1607bd04771b`; CI #387 successful after retry of an unrelated transient existing Modbus diagnostics test on the unchanged head.
- **DEV 3 / PR #66:** Audit workspace ergonomics; exact worker head `fdb622df821fefd14e5af9244ad0df4cd9eb1302`; CI #386 SUCCESS.

All three were created from the pre-PR-#68 `main` (`94c23847cd9ce876b481bd832e5648b66ce55794`) and therefore must be reconciled against the current `main` before coordinator merge. Their file domains are interface-specific and do not overlap the Driver SDK architecture changes from PR #68.

Detailed AllowedScope, ForbiddenScope, MustReadSpecific and CompletionCriteria remain authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## LOCKED FUTURE CHAINS

Python/visual order remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

External protocol production remains postponed until after interface development and validation feedback. Driver SDK convergence makes the later work coherent; it does not authorize starting it now.

## COORDINATOR RESUME POINT

On next coordinator `siga`:

1. reread mandatory current-main docs including ADR-009 and research-convergence/readiness docs where relevant;
2. verify real main, PR #65/#66/#67 heads, CI and compare state;
3. reconcile each worker branch with the current `main` without discarding worker work;
4. review delivered slices strictly against their assignments;
5. merge only exact-head green, semantically sound work;
6. centrally integrate Runtime Alarm Center placement and any cross-product consistency needed;
7. update official documentation after the second interface wave;
8. reassess UI maturity for the Windows validation package only after that integration.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Merged research is architecture evidence, not implemented product functionality.
- Merged architecture contracts do not themselves constitute a production protocol implementation.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
