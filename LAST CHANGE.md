# LAST CHANGE — EliteSCADA

**Date:** 2026-09-06 BRT  
**Operational state:** **WAVE 14 ACTIVE / C25.0-C25.6 COMPLETE / C25.7 ARCHITECTURE EXACT-SHA GREEN / C25.7 PRODUCT IMPLEMENTATION NOT STARTED / C11 FROZEN / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting.

## Current correction authority

Active package:

`W14-C25 — Post-Demo consolidated corrections`

Branch:

`wave14/c25-post-demo`

PR:

#283 -> `wave14/corrections-integration`

Tracking issue:

#282

Current exact handoff HEAD:

`c84882d578cb891797a80faf6073d422c4fe55ed`

This SHA contains the frozen C25.7 architecture/coordination state and no C25.7 product mutation.

Exact-SHA validation:

- Wave 14 C25 Post-Demo #292 / `34055739682` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #290 / `34055739696` — **SUCCESS**.

## Completed through C25.6

C25.0 through C25.5 are complete.

C25.6 Reusable Resource Libraries is complete on exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

The feature includes `.escadalib`, safe non-mutating association, selective dependency-aware incorporation, project-owned provenance, self-contained `.escadapkg`, safe disassociation, no Runtime library dependency and Engineering Libraries UI/browser validation under existing Authority/Engineering Lock.

## Current checkpoint — C25.7 Distributed Runtime Foundation

Binding documents:

- `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`
- `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
- `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`
- `docs/CURRENT-COORDINATOR-HANDOFF.md`

Architecture is frozen and exact-SHA green. Product implementation has not started.

Current C25.7 scope:

- Server Runtime Contract;
- transport-independent canonical Runtime boundary;
- Runtime Session Lease foundation;
- Viewer / Interactive server-side capability downscope;
- voluntary View Only;
- topology-neutral `.escadapkg` proofs;
- optional thin EliteGO proof only if it remains small and reuses canonical Runtime code.

HA orchestration/replication/failover and advanced commercial connection tiers remain future-Wave work.

The next code action is a live audit of the existing Runtime/Active application/realtime TAG/Authority/session/command-write paths before defining the smallest new canonical boundary.

## Remaining C25

- C25.7 Distributed Runtime Foundation — implementation not started;
- C25.8 contextual multilingual Help/manual;
- C25.9 integrated regression/audit;
- C25.10 exact final candidate / explicit Product Owner acceptance.

## C11 / EEE sequence after C25

C11 remains preserved at:

`41d24d89c3b9d2b881215255e44023fabde262f3`

After C25 is explicitly accepted:

1. merge C25 only into `wave14/corrections-integration`;
2. exact-SHA validate the integration merge;
3. synchronize/adapt C11 to the accepted C25 contracts;
4. revalidate the canonical EEE Demo using normal generic product paths;
5. export/version/freeze `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. run Preview Codespace with that canonical package;
7. **keep the Preview Codespace active** through Product Owner visual homologation and the later authorized main transition;
8. #212 may merge into `main` only after explicit Product Owner authorization;
9. validate the new `main` while Preview remains available.

## Return to Wave 13 / Windows installable

Issue #205 and PR #207 remain paused and preserve the prior Windows release/signing implementation.

After the approved Wave 14/C25 -> EEE/C11 -> Preview -> `main` sequence, Wave 13 resumes using the **new `main` containing accepted Wave 14/C25** as release authority.

The preserved Wave 13 branch is input to that resumption, not permission to ship the stale pre-C25 product. Re-audit and adapt its packaging/signing work against the new mainline, then rerun all release gates before producing the Windows-installable build.

The Windows-installable/signing/release work must be based on that new mainline, not the older pre-C25 `main`.

## Revalidated guard rails at coordinator handoff

- #212 — OPEN/DRAFT; no merge authorization to `main`.
- #263 — OPEN/DRAFT; C11 frozen.
- #266 — OPEN/DRAFT validation-only; MUST NEVER MERGE.
- issue #205 — OPEN / Wave 13 paused.
- #207 — OPEN/DRAFT / Wave 13 paused.

## Permanent governance

- #212 remains OPEN/DRAFT; no merge to `main` without explicit Product Owner authorization.
- Never modify `main` directly.
- #283 remains OPEN/DRAFT -> integration.
- #263 remains the C11 implementation PR.
- #266 is validation-only and MUST NEVER MERGE.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose red CI before rerun.
- Never weaken validation, security, identity, lifecycle, licensing, package or Runtime authority to obtain green.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for generic product deficiencies.
