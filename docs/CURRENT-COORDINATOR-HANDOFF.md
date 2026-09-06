# EliteSCADA — Current Coordinator Handoff

**Date:** 2026-09-06 BRT  
**Status:** **WAVE 14 ACTIVE / C25.0-C25.6 COMPLETE / C25.7 DISTRIBUTED RUNTIME FOUNDATION ACTIVE / C11 FROZEN / WAVE13 PAUSED**

> GitHub live state is the sole authority. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation.

## 1. Read first

1. `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`
2. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
3. `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`
4. `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`
5. `docs/WAVE14-C25-REUSABLE-LIBRARIES-IMPLEMENTATION-STATUS-2026-09-06.md`
6. issue #282
7. PR #283

## 2. Live C25 branch state before this handoff refresh

Branch:

`wave14/c25-post-demo`

Implementation PR:

#283 -> `wave14/corrections-integration`

Latest exact-SHA green documentation authority before the new C25.7 architecture documents:

`78c3370d9943c4ba71cd8fb8a38da1025d3672ce`

- Wave 14 C25 Post-Demo #283 — SUCCESS;
- Wave 14 C03 DNP3 Adapter #284 — SUCCESS.

C25.6 reusable libraries is complete on exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

C25.6 documentation closure `c820665a9dc526e39dd7596825f01e21b345ffba` also closed exact-SHA green with C25 #279 / `34052472926` and C03 #282 / `34052472951`.

## 3. Current checkpoint — C25.7 Distributed Runtime Foundation

The Product Owner moved a small distributed-runtime foundation ahead of Help so Help documents the stabilized product rather than an immediately superseded boundary.

C25.7 binding architecture:

`docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`

Product evolution roadmap:

`docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`

C25.7 scope is deliberately limited to:

- Server Runtime Contract;
- transport-independent canonical Runtime boundary without a second HMI engine;
- Runtime Session Lease foundation;
- Viewer / Interactive effective-capability reduction;
- voluntary View Only;
- topology-neutral `.escadapkg` proofs;
- optional thin EliteGO proof-of-architecture only if small and canonical-runtime based.

Production HA, cluster replication, TAG Mirror, epoch/fencing, automatic failover, seamless EliteGO failover and advanced commercial connection licensing are future-Wave work and must not expand C25.

## 4. Remaining C25 sequence

- C25.7 — Distributed Runtime Foundation — ACTIVE;
- C25.8 — Contextual multilingual Help/manual;
- C25.9 — Integrated regression/audit;
- C25.10 — Exact final candidate matrix / explicit Product Owner acceptance.

No C25 merge is authorized before exact final acceptance.

## 5. Required post-C25 sequence

After C25 explicit acceptance:

1. merge accepted C25 only into `wave14/corrections-integration`;
2. exact-SHA post-merge validation on integration;
3. only then synchronize/adapt C11 canonical EEE Demo to the accepted C25 application/package/runtime contracts;
4. revalidate normal generic Save -> Publish -> Activate and package portability;
5. export/version/freeze canonical `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. launch canonical EEE Demo in Preview Codespace;
7. **keep the Preview Codespace active** during Product Owner visual homologation and through the subsequent authorized transition to `main`;
8. only after later explicit Product Owner authorization may #212 merge to `main`;
9. validate the resulting new `main` while the Preview environment remains available for comparison/revalidation;
10. resume paused Wave 13 #205/#207 from the **new main containing accepted Wave 14/C25**;
11. produce/sign/validate the Windows-installable EliteSCADA from that mainline authority.

Do not use the pre-C25 `main` as the final Windows release source after this sequence.

## 6. C11 remains frozen

Branch:

`wave14/c11-canonical-eee-demo`

Preserved exact head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

- #263 remains OPEN/DRAFT -> integration;
- #266 remains validation-only -> main and MUST NEVER MERGE;
- do not synchronize C25 into C11 until C25 is accepted, integrated into integration and post-merge exact-SHA revalidated.

## 7. Wave 13 remains paused

Wave 13 release/signing/installable work (#205/#207) remains paused now. It resumes only after the approved C25 -> integration -> EEE/C11 -> Preview -> authorized `main` sequence, using the resulting new `main` as release authority.

## 8. Permanent governance

- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without explicit Product Owner authorization.
- Never alter `main` directly.
- #283 remains OPEN/DRAFT -> `wave14/corrections-integration`.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose CI red before rerun; never weaken tests, security, identity, lifecycle, licensing, package or Runtime authority for green.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for generic product defects.

If this file conflicts with live GitHub, live GitHub wins.
