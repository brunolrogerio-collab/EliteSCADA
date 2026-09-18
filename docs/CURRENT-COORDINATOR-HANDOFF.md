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
- FND-03 License Lifecycle/Fencing Phase A — **VERIFIED/FROZEN**.
- FND-03 License Lifecycle/Fencing Phase B — **TEST-FIXTURE CORRECTION ACTIVE / NOT INTEGRATED**.
- FND-03 global — ACTIVE / NOT FROZEN.
- FND-04 — QUEUED / CONTRACT DEFINED / WAIT.
- FC0-A — BLOCKED.

## Verified product checkpoint

`20b934f23d8798ffb65cca203b62f8b5c3d8f111`

tree `4e227fdde1d8475c23852e142c51946c7a2e1859`.

This is PR #332 merged into `wave15/corrections-integration`.

Exact post-merge CI:
- EliteSCADA CI #1551 / run `35341475101`
- Backend `105588126265` — SUCCESS
- Web `105588126462` — SUCCESS
- Chromium `105588538108` — SUCCESS

Coordination/documentation commits after this SHA do not change the product checkpoint.

## Current FND-03 DEV order

`ORDER_STATE: ACTIVE`  
`DEV_MODE: IMPLEMENT_PHASE_B_TEST_FIXTURE_CORRECTION`

Mission:

**Active Runtime Re-evaluation + Durable Demo Recovery v1**

New work branch:

`work/w15-fnd-03-runtime-authority-reevaluation-v1`

must be created from exact product base:

`20b934f23d8798ffb65cca203b62f8b5c3d8f111`

Phase B is bounded to:
1. `ProductLicensedRuntimeCoordinator.ReevaluateForAuthorityChangeAsync`;
2. durable Demo semantic start / remaining-duration behavior;
3. persisted Runtime recovery fail-closed for transition pending and Demo without durable anchor;
4. deterministic focused tests.

Do not implement Phase C lifecycle mutation orchestration/API/audit yet.

Required return in #301:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE PHASE B HANDOFF`

No PR or Actions yet. Main reviews the exact Phase B head first.

## Other lanes

- CODEX — WAIT / reserve.
- FND-04 DEV/AUD — WAIT.
- FC0-A — BLOCKED.

## Retomada obrigatória

1. Read canonical handoff in full.
2. Revalidate product checkpoint and integration HEAD.
3. Review latest Phase B DEV handoff/head if present.
4. Main decides correction or PR/CI gate.
5. Keep CODEX reserve unless a material blocker justifies it.


## Latest Phase B CI diagnosis

PR #333 exact head `abb1e497c66a8f0888d6cde83331c51623c18979` entered CI #1552 / run `35369719457`. Web passed. Backend failed at build with a single test-helper warnings-as-error blocker: `PersistedRuntimeRecoveryServiceTests.RecordingTimeProvider._timestamp` is never assigned (CS0649). DEV is ordered to make a one-test-file correction only; no production change is authorized.


## Latest CI #1553 diagnosis

Exact Phase B head `5ebf533132b217085ff74db8f26ddb16cf95da88` builds successfully. Scada.Drivers.Tests is 669/670 PASS. The sole failure is a fixture defect in `Recovery_DemoWithAuthorityAnchor_UsesNormalPathAndPreservesRemainingWindow`: `CreateSimplePackage(0)` has no active Runtime source and canonical Runtime correctly returns `RUNTIME_NO_ACTIVE_SOURCES`.

DEV is ordered to change only `PersistedRuntimeRecoveryServiceTests.cs`, replacing that zero-source fixture with one deterministic Server Memory TAG/DataSource. Zero production/workflow changes.
