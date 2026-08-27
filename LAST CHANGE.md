# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE PRODUCT DEVELOPMENT — SECOND WAVE ASSIGNED**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Merged product/platform foundations include Internal Memory, TAG Gateway, common multi-Data-Source diagnostics, Engineering Schema v9 and the first integrated interface checkpoint through PR #58.

The active product order remains:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

The provisional Windows x64 validation package remains postponed until the interface reaches a materially useful validation state.

## RESEARCH BACKLOG CONSOLIDATED INTO MAIN

Five previously open research PRs are now incorporated into official `main` as architecture/evidence only:

- PR #53 graphical visual editor architecture — reconciled head `3eb6e4be34811896d3b2dd92ee37cd57635cdecc`, CI #383 green, merge `491ee337bf2723d13d2759bc677300edd34e1fca`;
- PR #54 Client Visual Python editor/browser sandbox — reconciled head `8f812e9425bc98e9c9225be0fef76074a5dbaace`, CI #384 green, merge `80d06ea467c7c844807c0548940308ccf74a7510`;
- PR #62 BACnet/IP + BACnet/SC architecture — reconciled head `69731f99cc7ad5984958701ff217ce1c0b09d2a8`, CI #380 green, merge `c60c611465bd82a898ee30d5f67fe79234381b8c`;
- PR #63 MQTT industrial Data Source architecture — reconciled head `53bb71a530f3d97c56562be1f5fdecde042bb898`, CI #381 green, merge `05df6bc63893cb025f87899d27a5988b2e1cf896`;
- PR #64 Allen-Bradley EtherNet/IP/CIP Logix architecture — reconciled head `08b83951863276a5082ecf24de85c146c548c6c1`, CI #382 green, merge `a71ce2d962d6b122714b61b5851465d9c284e7b6`.

These are **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED**. They do not add Python, graphical editing, MQTT, BACnet or Allen-Bradley runtime capability and do not reopen the protocol gate.

## SECOND INTERFACE WAVE — ASSIGNED

The coordinator reviewed `docs/ROADMAP.md` and `docs/INTERFACE-DEVELOPMENT.md` after the first interface checkpoint. The next parallel wave remains inside the active interface block:

- **DEV 1:** Engineering Alarm Workspace ergonomics on `feature/interface-engineering-alarm-workspace`;
- **DEV 2:** Runtime Alarm Center + protected ACK UX on `feature/interface-runtime-alarm-center`;
- **DEV 3:** Audit workspace ergonomics/cross-product consistency on `feature/interface-audit-workspace`.

Detailed AllowedScope, ForbiddenScope, MustReadSpecific and CompletionCriteria are authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## WHY THESE TASKS ARE NEXT

- Engineering already has scalable Data Source/TAG browsing but Alarm definitions still lag behind that workspace model;
- Runtime already reads active alarms in the operational overview and the backend already provides protected `/api/alarms/{id}/ack`, so a real alarm operations surface can add value without inventing backend authority;
- Audit is already complete as a protected backend/product route but remains visually dense and is a good isolated target for cross-product consistency;
- all three tasks are interface work, can run in parallel with low file overlap, and do not cross the locked Python/visual/protocol gates.

## LOCKED FUTURE CHAINS

Python/visual order remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

External protocol production remains postponed until after interface development and validation feedback. Research already merged is input for that later wave, not authorization to implement it now.

## COORDINATOR RESUME POINT

On next coordinator `siga`:

1. reread mandatory current-main docs;
2. verify real main, worker branches, PRs, heads and CI;
3. review delivered worker slices strictly against their assignments;
4. merge only exact-head green, semantically sound work;
5. centrally integrate surfaces where product composition requires it;
6. reassess UI maturity for the Windows validation package after the second interface wave, not before.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Merged research is architecture evidence, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
