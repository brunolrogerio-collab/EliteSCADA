# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 SHARED SEAT ACCOUNTING VERIFIED+FROZEN / LIFECYCLE+FENCING ARCHITECTURE FROZEN / PHASE A PR_READY-PENDING-CI / CODEX WAIT / FND-04 WAIT / FC0-A BLOCKED**

> GitHub live is the official memory.
>
> Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Latest verified product checkpoint

`wave15/corrections-integration@a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

This is PR #331 — FND-03 Shared Runtime Seat Accounting — merged and VERIFIED/FROZEN by exact post-merge CI #1546 / run `35269829080`.

Later coordination-only documentation commits do not change the product checkpoint.

## Foundation state

- FND-01 — VERIFIED/FROZEN
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN
- FND-08 — VERIFIED/FROZEN
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN
- FND-03 Runtime Admission — VERIFIED/FROZEN
- FND-03 Shared Runtime Seat Accounting — VERIFIED/FROZEN
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **ARCHITECTURE FROZEN / PHASE A PR_READY-PENDING-CI / NOT INTEGRATED**
- FND-03 global — ACTIVE / NOT FROZEN
- FND-04 — WAIT
- FC0-A — BLOCKED

## Current execution strategy

CODEX remains `WAIT` to preserve scarce quota.

FND-03 DEV is `ACTIVE / OPEN_PHASE_A_PR_ONLY`.

Main reviewed and froze architecture amendment #301 comment `5722165708`. The first implementation phase is deliberately bounded to:

1. canonical `LicenseVerificationResult VerifyCandidate(string licenseCode)` with no installed-state mutation;
2. `AuthorityRevision` + durable `transition_pending` state, PostgreSQL migration `023_runtime_session_authority_fencing_v1`, in-memory parity and bulk-fence primitives;
3. admission/use epoch enforcement with `ExpectedAuthorityRevision`, pending/stale fail-closed checks and deterministic concurrency proof.

Exact product base:

`a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

Work branch:

`work/w15-fnd-03-license-lifecycle-fencing-v1`

Target:

`wave15/corrections-integration`

Corrected Phase A candidate `509d794e92fd5e6333663020738d2713c73a7e9f` / tree `ebb607695815197d419d28dd463c47bd0e702284` was re-reviewed by Main. CA1/CA2 are closed at source/test-definition level. DEV may now open the exact Phase A PR only; natural PR CI will be owned and evaluated by Main.

## Frozen architecture guards

- one canonical `FileProductLicenseService` authority;
- canonical `LicenseVerificationResult`;
- invalid replacement true no-op;
- short PostgreSQL advisory transactions only;
- durable fail-closed transition marker;
- `AuthorityRevision` global fencing epoch, `Generation` per-lease CAS;
- stale capacity cannot reserve after authority revision change;
- transition completion must prove no active stale-revision lease remains;
- Demo recovery cannot reset the bounded allowance;
- machine-license mutation requires EngineeringModify but not current Application Engineering Lock;
- no secrets/license/session/lifecycle state in `.escadapkg`;
- same-installation machine authority only for shared-ledger multi-process support; cross-machine HA remains out of scope.

## Next gate

Wait for:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE PHASE A PR HANDOFF`

Then Main independently reviews exact Phase A head/tests and either:
- orders corrections,
- activates Phase B,
- or uses Codex only for a material blocker/critical correction.

FND-04 stays WAIT. FC0-A stays blocked.
