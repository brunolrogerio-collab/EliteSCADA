# LAST CHANGE — EliteSCADA

**Date:** 2026-09-06 BRT  
**Operational state:** **WAVE 14 ACTIVE / C25.0-C25.6 COMPLETE / C25.7 DISTRIBUTED RUNTIME FOUNDATION ACTIVE / C11 FROZEN / WAVE13 PAUSED**

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

Latest exact-SHA green documentation authority before C25.7 resequencing:

`78c3370d9943c4ba71cd8fb8a38da1025d3672ce`

- Wave 14 C25 Post-Demo #283 — SUCCESS;
- Wave 14 C03 DNP3 Adapter #284 — SUCCESS.

## Completed through C25.6

C25.0 through C25.5 are complete.

C25.6 Reusable Resource Libraries is complete on exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

The feature includes `.escadalib`, safe non-mutating association, selective dependency-aware incorporation, project-owned provenance, self-contained `.escadapkg`, safe disassociation, no Runtime library dependency and Engineering Libraries UI/browser validation under existing Authority/Engineering Lock.

## Current checkpoint — C25.7 Distributed Runtime Foundation

The Product Owner approved moving a small distributed-runtime foundation ahead of Help.

Binding documents:

- `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`
- `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`
- `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`

Current C25.7 scope:

- Server Runtime Contract;
- transport-independent canonical Runtime boundary;
- Runtime Session Lease foundation;
- Viewer / Interactive server-side capability downscope;
- voluntary View Only;
- topology-neutral `.escadapkg` proofs;
- optional thin EliteGO proof only if it remains small and reuses canonical Runtime code.

HA orchestration/replication/failover and advanced commercial connection tiers remain future-Wave work.

## Remaining C25

- C25.7 Distributed Runtime Foundation;
- C25.8 contextual multilingual Help/manual;
- C25.9 integrated regression/audit;
- C25.10 exact final candidate / Product Owner acceptance.

## C11 / EEE sequence after C25

C11 remains preserved now at:

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
9. validate the new `main` while the Preview remains available.

## Return to Wave 13 / Windows installable

Wave 13 #205/#207 remains paused now.

After the approved Wave 14/C25 -> EEE/C11 -> Preview -> `main` sequence, Wave 13 resumes using the **new `main` containing accepted C25** as release authority.

The Windows-installable/signing/release work must be based on that new mainline, not the older pre-C25 `main`.

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
