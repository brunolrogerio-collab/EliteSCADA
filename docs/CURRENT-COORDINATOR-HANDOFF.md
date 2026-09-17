# Current Coordinator Handoff — Wave 15

> **PONTE CURTA da coordenação corrente.**
>
> Handoff operacional vivo/canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> GitHub live é a autoridade final. Esta ponte não substitui a ordem canônica.

## Estado rápido

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN.
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN.
- FND-03 Runtime Admission — VERIFIED/FROZEN.
- FND-03 Shared Runtime Seat Accounting — VERIFIED/FROZEN.
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **ARCHITECTURE FROZEN / PHASE A PR_READY-PENDING-CI / NOT INTEGRATED**.
- FND-03 global — ACTIVE / NOT FROZEN.
- FND-04 — QUEUED / CONTRACT DEFINED / WAIT.
- FC0-A — BLOCKED.

## Verified product checkpoint

`wave15/corrections-integration@a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

PR #331 is MERGED and exact post-merge CI #1546 / run `35269829080` is green.

Coordination/documentation commits after that SHA do not create a new product checkpoint. Live compare confirmed only `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md` and `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` changed since the product checkpoint.

## Current execution lanes

### CODEX

`ORDER_STATE: WAIT`.

Codex is reserve. No implementation/commit/PR/CI is authorized until Main explicitly activates a bounded task.

### FND-03 DEV

`ORDER_STATE: ACTIVE`  
`DEV_MODE: OPEN_PHASE_A_PR_ONLY`

Architecture amendment #301 comment `5722165708` was independently reviewed by Main and is frozen for implementation.

Work branch:

`work/w15-fnd-03-license-lifecycle-fencing-v1`

must be created from exact product base:

`a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

Phase A only:

1. canonical non-mutating `VerifyCandidate`;
2. AuthorityRevision / durable transition-state foundation + PostgreSQL migration / in-memory parity / bulk-fence primitives;
3. admission/validate/heartbeat/terminate epoch enforcement using `ExpectedAuthorityRevision`.

Do not yet implement local Runtime re-evaluation, Demo recovery, lifecycle orchestrator, licensing mutation API/audit cutover, FND-04 or FC0-A.

Corrected Phase A head `509d794e92fd5e6333663020738d2713c73a7e9f` / tree `ebb607695815197d419d28dd463c47bd0e702284` was independently re-reviewed by Main. CA1/CA2 are closed at source/test-definition level.

DEV is authorized only to open the Phase A PR from the exact candidate to `wave15/corrections-integration`, without changing candidate/rebase/retarget/merge.

Required return in #301:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE PHASE A PR HANDOFF`

Natural PR CI is owned by Main. Execution evidence remains PENDING until CI.

## FND-04

FND-04 DEV and AUD remain `WAIT`.

## Retomada obrigatória

1. Read canonical handoff in full.
2. Revalidate product checkpoint and integration HEAD.
3. Review latest Phase A DEV handoff/head if present.
4. Main decides Phase B only after independent Phase A review.
5. Keep Codex WAIT unless a bounded blocker/correction justifies it.
6. Keep FND-04 WAIT and FC0-A blocked.

`Hora: HH:MM` in America/Sao_Paulo.
