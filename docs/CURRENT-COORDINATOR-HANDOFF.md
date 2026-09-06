# EliteSCADA — Current Coordinator Handoff

**Date:** 2026-09-06 BRT  
**Status:** **WAVE 14 ACTIVE / C25.0-C25.6 COMPLETE / C25.7 ARCHITECTURE EXACT-SHA GREEN / C25.7 PRODUCT IMPLEMENTATION NOT STARTED / C11 FROZEN / WAVE13 PAUSED**

> GitHub live state is the sole authority. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation. This file records the clean coordinator handoff point; if live GitHub differs, live GitHub wins.

## 1. Read first

1. `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`
2. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
3. `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`
4. `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`
5. `docs/WAVE14-C25-REUSABLE-LIBRARIES-IMPLEMENTATION-STATUS-2026-09-06.md`
6. issue #282
7. PR #283

## 2. Exact handoff authority

Active branch:

`wave14/c25-post-demo`

Implementation PR:

#283 -> `wave14/corrections-integration`

Exact handoff HEAD:

`c84882d578cb891797a80faf6073d422c4fe55ed`

This SHA contains the frozen C25.7 architecture/coordination documents only. No C25.7 product mutation has started.

Exact-SHA validation on this same SHA:

- Wave 14 C25 Post-Demo #292 / `34055739682` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #290 / `34055739696` — **SUCCESS**.

Therefore the next coordinator may begin C25.7 implementation only after first confirming that PR #283 still points to this SHA or revalidating any newer HEAD.

C25.6 reusable libraries remains complete on exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

## 3. Live guard-rail state revalidated at handoff

- #212 — OPEN/DRAFT -> `main`; no merge authorization. Integration head remains `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155` at this handoff.
- #263 — OPEN/DRAFT; C11 canonical EEE branch remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266 — OPEN/DRAFT validation-only -> `main`; MUST NEVER MERGE.
- issue #205 — OPEN; Wave 13 Windows release/signing remains paused.
- #207 — OPEN/DRAFT; preserved Wave 13 release branch head `fda87ba4445127c174f6ea533a6bcabaabc7bb20`.

## 4. Current checkpoint — C25.7 Distributed Runtime Foundation

The Product Owner moved a small distributed-runtime foundation ahead of Help so Help documents the stabilized product rather than an immediately superseded boundary.

Binding architecture:

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

### Immediate next execution step

C25.7 product code is intentionally untouched at this handoff. The next coordinator must:

1. revalidate #283/#212/#263/#266 plus current exact HEAD and workflows;
2. perform the live code audit defined in the C25.7 execution log: local Runtime, Active application projection, realtime TAG transport, Authority/capabilities, Runtime session state and command/write paths;
3. derive the smallest Server Runtime Contract from existing canonical authorities;
4. implement only the bounded C25.7 scope with tests proving backend authority and topology-neutral package behavior;
5. close C25.7 only on one exact SHA with C25 + C03 green on that same SHA.

Do not jump directly to Help before C25.7 is closed.

## 5. Remaining C25 sequence

- C25.7 — Distributed Runtime Foundation — ACTIVE / implementation not started;
- C25.8 — Contextual multilingual Help/manual — NOT STARTED;
- C25.9 — Integrated regression/audit — NOT STARTED;
- C25.10 — Exact final candidate matrix / explicit Product Owner acceptance — NOT STARTED.

No C25 merge is authorized before exact final acceptance.

## 6. Required post-C25 sequence

After explicit Product Owner acceptance of one exact C25 candidate:

1. merge accepted C25 only into `wave14/corrections-integration`;
2. exact-SHA post-merge validation on integration;
3. only then synchronize/adapt C11 canonical EEE Demo to the accepted C25 application/package/runtime contracts;
4. revalidate normal generic Save -> Publish -> Activate and package portability;
5. export/version/freeze canonical `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. launch the canonical EEE package in Preview Codespace;
7. **keep that Preview Codespace active** during Product Owner visual homologation and through the subsequent approved transition to `main`;
8. only after later explicit Product Owner authorization may #212 merge into `main`;
9. validate the resulting **new `main` containing accepted Wave 14/C25** while Preview remains available for comparison/revalidation;
10. resume paused Wave 13 #205/#207 from that new `main`, not from the preserved pre-C25 release snapshot;
11. integrate/adapt the preserved Wave 13 release work through normal history-preserving changes, re-audit packaging/signing assumptions against the new mainline and rerun release gates;
12. produce/sign/validate the Windows-installable EliteSCADA from the new mainline authority.

Do not use the older pre-C25 `main` as the final Windows release source after this sequence.

## 7. C11 remains frozen

Branch:

`wave14/c11-canonical-eee-demo`

Preserved exact head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

- #263 remains OPEN/DRAFT -> integration;
- #266 remains validation-only -> main and MUST NEVER MERGE;
- do not synchronize C25 into C11 until C25 is accepted, integrated into integration and post-merge exact-SHA revalidated;
- no EEE-specific workaround for a generic product gap.

## 8. Wave 13 remains paused

Issue #205 and PR #207 preserve the Windows packaging/signing work. They remain paused now.

When resumed, the release authority is the new `main` containing accepted Wave 14/C25. The preserved Wave 13 branch is implementation/history input, not permission to sign or release the stale pre-C25 product snapshot.

Existing external blockers such as real Authenticode authority/publisher/timestamp evidence and DNP3 commercial-distribution authorization remain independent release gates and must still be revalidated at that time.

## 9. Permanent governance

- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without explicit Product Owner authorization.
- Never alter `main` directly.
- #283 remains OPEN/DRAFT -> `wave14/corrections-integration`.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose CI red before rerun; never weaken tests, security, identity, lifecycle, licensing, package or Runtime authority for green.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for generic product defects.

## 10. Resume protocol for the next coordinator

1. Treat GitHub as the only project memory.
2. Fetch PR #283 and capture its live head SHA.
3. Fetch exact-SHA C25 and C03 workflow results for that SHA.
4. Revalidate #212, #263, #266, #205 and #207.
5. Read the C25.7 architecture and execution log before mutating code.
6. If the live head is still `c84882d578cb891797a80faf6073d422c4fe55ed`, start with the required live code audit; this is the clean implementation boundary.
7. If the head moved, inspect and validate the newer state rather than reconstructing authority from this handoff or chat history.
