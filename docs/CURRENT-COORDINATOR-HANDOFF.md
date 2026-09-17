# Current Coordinator Handoff — Wave 15

> **PONTE CURTA da coordenação corrente.**
>
> Handoff operacional vivo/canônico:
>
> `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
>
> GitHub live é autoridade final. Este arquivo não substitui o handoff canônico.

## Estado rápido

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 — ACTIVE / NOT FROZEN.
- Runtime Session Lease v1 — VERIFIED/FROZEN.
- machine-license v2 + hardening — VERIFIED/FROZEN.
- Runtime Admission — VERIFIED/FROZEN.
- Shared Runtime Seat Accounting — **PR_READY / CORRECTION TEST-ONLY REQUIRED**.
- FND-04 — QUEUED / CONTRACT DEFINED / NOT ACTIVE.
- FC0-A — BLOCKED.

## Product checkpoint

`6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`.

Coordination-only commits advanced the integration branch after this product checkpoint. Do not confuse coordination HEAD with product base.

## Current candidate

PR #331 — `FND-03: enforce shared runtime seat accounting`

Reviewed head:

`09f81e97369089def481ceb25629779a5aba8aff`

tree:

`8a81cb965f754fb4299800333f8a5a5c28d1044e`

Exact-head CI #1544 / run `35255337014` is green on the reviewed head after the one controlled rerun.

## Current order

Codex must read the canonical handoff live and execute `ORDER CODEX-331-TEST-CLOSE-03`.

Reason: the final handoff explicitly left one binding acceptance item `PENDING`: explicit mixed Web/EliteGO client identities plus high-concurrency distinct-identity capacity coverage.

Required correction is **tests only**. No production code change is authorized unless the new required tests expose a real defect; if that occurs, Codex must STOP before product correction.

After tests-only change, new exact-head CI must validate the new candidate and Codex returns the required `ACCEPTANCE-CLOSE HANDOFF`.

## Main CI authority

Main Coordinator has permanent authority to operate pre-merge/post-merge CI under the canonical guardrails. CI authority is separate from merge authority.

## Resume

1. Read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` fully.
2. Revalidate PR #331 exact head/tree and Actions.
3. If the acceptance-close handoff is present, independently review tests-only diff and exact-head CI.
4. Do not integrate while required acceptance remains PENDING.
5. FND-04 remains WAIT until Main explicitly activates it.

Ao Product Owner: `Hora: HH:MM` em `America/Sao_Paulo`.
