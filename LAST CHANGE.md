# LAST CHANGE — EliteSCADA

**Date:** 2026-09-06 BRT  
**Operational state:** **WAVE 14 ACTIVE / C25 ACTIVE / C25.0-C25.3 COMPLETE / C25.4 IN PROGRESS / C11 PRESERVED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting.

## Current correction authority

Active correction package:

`W14-C25 — Post-Demo consolidated corrections`

Branch:

`wave14/c25-post-demo`

PR:

#283 -> `wave14/corrections-integration`

Tracking:

#282

Product HEAD immediately before the handoff-only documentation commit:

`a7ac6a8a008749e8224e24a59d7f7b1752d02fe2`

Exact-head evidence:

- Wave 14 C25 Post-Demo #89 / `34013048034` — SUCCESS
- Wave 14 C03 DNP3 Adapter #187 / `34013048107` — SUCCESS

## Current checkpoint

C25.0 through C25.3 are complete with focused validation recorded in:

`docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`

C25.4 Restore-first/System Recovery is **IN PROGRESS**.

Backend/system-recovery implementation now includes:

- encrypted/authenticated Authority backup v1;
- atomic Authority replace/restore;
- secure true-empty bootstrap restore boundary;
- prospective recovered-Administrator authorization against the package SecurityRoles;
- application recovery coordinator using canonical package Inspect/Preview/Apply -> root Save -> Publish -> Activate;
- explicit partial-state reporting after durable Save;
- `RestoreFirstPanel.tsx` component and a dedicated Restore-first Playwright specification;
- optional license handling kept separate and non-blocking for core application recovery.

However C25.4 is not closed because the coordinator handoff audit found two concrete gaps on exact product HEAD `a7ac6a8a...`:

1. `web/scada-web/src/auth/AuthGate.tsx` does not yet render/wire `RestoreFirstPanel` into the real bootstrap / first-project state machine.
2. `.github/workflows/wave14-c25-post-demo.yml` does not execute `web/scada-web/tests-e2e/wave-14-c25-restore-first.spec.ts`.

Therefore the current green C25 workflow is backend/build evidence, not browser proof of Restore-first. Do not mark C25.4 complete until the UI is wired and the Restore-first browser contract runs green on the exact SHA.

## Current coordinator handoff

Authoritative resume document:

`docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md`

Also read:

- `docs/CURRENT-COORDINATOR-HANDOFF.md`
- `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`
- `docs/WAVE14-C25-FULL-CODE-AUDIT.md`
- `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`
- issue #282
- PR #283

## Accepted baseline beneath C25

C24 is:

**ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED**

Accepted C24 product merge:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Wave 14 integration branch remains:

`wave14/corrections-integration`

Integration PR #212 remains OPEN/DRAFT and is not authorized to merge into `main`.

## C11 remains preserved

Canonical C11 branch:

`wave14/c11-canonical-eee-demo`

Preserved head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

- #263 remains OPEN/DRAFT -> integration.
- #266 remains validation-only -> main and MUST NEVER MERGE.
- Do not sync/adapt C11 until C25 is accepted, integrated and post-merge revalidated.

## Next exact action

Resume C25.4 by revalidating live #283, then:

1. wire `RestoreFirstPanel` into `AuthGate`;
2. preserve application/license selection across Authority restore -> real login -> application recovery without persisting secrets;
3. expose Restore backup alongside Create Administrator on true-empty bootstrap;
4. expose Restore backup alongside Create New Project for authenticated local Administrator with empty catalog;
5. add `wave-14-c25-restore-first.spec.ts` to the C25 Chromium workflow;
6. inspect exact-SHA C25 + C03 CI and diagnose any red before rerun;
7. only then close C25.4 and proceed to C25.5.

## Permanent governance

- #212 remains OPEN/DRAFT; no merge to `main` without explicit Product Owner authorization.
- Never modify `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose red CI before rerun.
- Never weaken validation, security, identity, lifecycle, licensing, package or Runtime authority to obtain green.
- Backend Active revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for generic product deficiencies.
- Wave13 #205/#207 remains paused.