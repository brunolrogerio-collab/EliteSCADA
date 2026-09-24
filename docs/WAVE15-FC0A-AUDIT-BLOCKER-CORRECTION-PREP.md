# Wave 15 — FC0-A Audit Blocker Correction Preparation

> Main Coordinator owns the post-FND06 audit. This file now carries the active bounded correction sequence produced by that audit.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`STATE: SUPERSEDED_BY_CONSOLIDATED_FC0A_PACKAGE`

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`AUDIT_RESULT: CHANGES_REQUIRED`

`AUDIT_REPORT: docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`

`EXACT_BASE_PRODUCT_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`EXACT_BASE_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

Main revalidated the exact frozen product base. Integration commits above it at audit time were coordination-only; product correction branches remain based on the exact frozen product SHA.

---

## 0A. Main supersession — consolidated FC0-A correction package

Product Owner requested that all confirmed/shared FC0-A findings be corrected now where safely possible, with one larger CODEX delivery before Main re-review.

Binding active control is now:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`

Active order:

`FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`

Work branch:

`work/w15-fc0a-consolidated-corrections`

Exact base remains:

`560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

The old P1-01 branch was still identical to this exact base with no PR when superseded. Do not execute P1-01 or P1-06 as separate serialized orders unless Main later explicitly reverts this supersession.

## 1. Prepared correction A — W15-P1-01 Server Script recovery

`ORDER_ID: FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

`ORDER_STATE: SUPERSEDED_UNUSED / DO_NOT_EXECUTE`

`WORK_BRANCH: work/w15-fc0a-p101-server-script-recovery`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: SCRIPT_RUNTIME`

`EXECUTOR: SAME_SEQUENTIAL_CODEX_USED_FOR_PRIOR_FOUNDATION_WORK`

### Why prepared

Current exact source shows a permanent failure latch:
- `ScriptFailureThrottle` sets `_isThrottled=true` after the configured consecutive failures;
- successful completion only clears the failure counter, not the already-latched throttle;
- `ScriptRuntimeExecutionCoordinator.ProcessNextAsync` returns `Throttled` while the latch is true and never dequeues/probes work;
- only explicit `ResetThrottle()` clears the latch;
- no production automatic Server Script reset caller was found.

This matches the Wave14 confirmed W15-P1-01 mechanism.

### Frozen contract to preserve

Do not alter:
- FND-04 readable TAG/source-binding/stable-TagId semantics;
- canonical TAG registry or Authority path;
- Python process isolation/allowlist;
- bounded queue/coalescing;
- Active revision gate;
- timeout/cancellation safety;
- no stale write/replay after authority changes.

### Bounded correction direction

If AUD confirms, implement a versioned bounded recovery state machine instead of a permanent latch.

Required behavior:
1. repeated timeout/fault enters a visible degraded/throttled state;
2. after a bounded cooldown, at most one half-open/probe execution may be attempted;
3. successful probe returns to healthy state and resets failure streak;
4. failed probe returns to bounded cooldown with no busy loop;
5. queue remains bounded/coalesced while recovery is pending;
6. stale queued effects are never replayed across Active revision/authority change;
7. persistent failure remains visibly degraded and auditable;
8. health exposes enough information for diagnosis: recovery state, last useful execution, last failure/timeout, next probe/cooldown or equivalent;
9. no timeout inflation and no EEE-specific exception.

### Expected primary surface

- `src/Scada.Engineering/VisualScripting/ScriptEventRuntimeFoundation.cs`;
- `src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs`;
- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs` only if required for server-owned health/recovery orchestration;
- focused existing tests, primarily:
  - `tests/Scada.Persistence.PostgreSql.Tests/ScriptRuntimeExecutionCoordinatorTests.cs`;
  - `tests/Scada.Persistence.PostgreSql.Tests/ScriptRuntimeDiagnosticsInvariantTests.cs`;
  - `tests/Scada.Drivers.Tests/ServerScriptRuntimeAutomationIntegrationTests.cs`.

If the generic coordinator is shared with Client Visual and a server-only rule is required, do not silently change client semantics; introduce the smallest explicit policy/configuration boundary.

### Mandatory RED / GREEN if activated

RED on exact old behavior:
- threshold reached -> permanent `Throttled`;
- queued event remains but no automatic probe occurs.

GREEN:
- transient failures -> throttle/degraded -> bounded probe -> recovery;
- persistent failures -> bounded repeated probes without spin;
- queued count/coalescing stays bounded;
- successful execution after recovery updates health/freshness;
- no stale write after revision/authority transition;
- diagnostics surface recovery truthfully;
- full focused + natural T1 + required broad post-merge validation green.

---

## 2. Prepared correction B — W15-P1-06 truthful Engineering fallback

`QUEUED_ORDER_ID: FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

`ORDER_STATE: SUPERSEDED_BY_CONSOLIDATED_PACKAGE / DO_NOT_EXECUTE_SEPARATELY`

### Why prepared

Exact current `EngineeringApp.tsx`:
- renders `Demo Project` whenever `snapshot` is null;
- renders no-model workspace fallbacks such as `unsaved` / `clean`;
- keeps those shell values visible while the real public model is loading or failed.

Current `engineering/api.ts`:
- actual HTTP response failures become `"<status> <statusText>"`;
- transport rejection from `fetch` propagates as a generic exception;
- the shell has retry UX but no typed distinction guaranteeing truthful HTTP-vs-transport context.

### Frozen contract to preserve

Do not change:
- FND-01 Working/Revisions/Published/Active authority;
- no silent seed/apply/activate;
- actual authoritative project named "Demo Project" remains valid **when supplied by a real snapshot**;
- retry must be read-only and must not mutate lifecycle state;
- FND-02 Authority;
- FND-06 visual/editor contract.

### Bounded correction direction

If AUD confirms:
1. project identity area shows truthful loading/unavailable/unknown state when `snapshot=null`; never synthesize `Demo Project`;
2. WorkspaceBar no-model state shows neutral/unknown placeholders, not `clean` or `unsaved` as authoritative facts;
3. successful real snapshot may still display an authoritative project whose real name is `Demo Project`;
4. distinguish transport rejection from actual HTTP response status in the Engineering bootstrap error model;
5. show bounded retry with useful endpoint/context and no fictitious HTTP code;
6. retry success mounts the returned authoritative snapshot without project mutation;
7. navigation that requires snapshot remains disabled/contained while unavailable;
8. no indefinite blank shell.

### Expected primary surface

- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- `web/scada-web/src/engineering/api.ts` only for a minimal typed load-error boundary if required;
- `web/scada-web/src/engineering/i18n.ts`;
- focused mounted Playwright regression.

Shared shell files remain Main-coordinated; this work must not be absorbed opportunistically by DEV-EDITOR.

### Mandatory RED / GREEN if activated

RED:
- initial/failed snapshot currently exposes synthetic `Demo Project`;
- no-snapshot bar can display `unsaved/clean`.

GREEN mounted matrix:
- initial loading: truthful Loading, no fake project identity;
- transport rejection: truthful transport unavailable, no fake HTTP status/project;
- real HTTP 500: exact HTTP status/context, no transport misclassification;
- retry -> real project snapshot succeeds;
- authoritative real `Demo Project` name still displays when returned by backend;
- retry does not mutate Working/Published/Active;
- no blank SPA.

Natural Wave 15 validation must use a valid profile selected from the live router.

---

## 3. Sequencing if audit confirms blockers

Do not combine these two corrections merely because both block FC0-A; they are different authorities/surfaces.

Preferred sequencing:
1. Main audit has confirmed both blockers;
2. Main activates the smallest confirmed correction first;
3. CODEX executes exact bounded order on isolated branch;
4. Main review + exact-head CI + post-merge broad;
5. repeat for the second blocker if still required;
6. rerun affected rows of `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
7. only then consider FC0-A release.

No DEV/FND-05/FND-07 release is authorized until P1-01 and P1-06 close and Main re-runs the affected audit rows.


## 4. Active P1-01 execution contract

CODEX must work only on `work/w15-fc0a-p101-server-script-recovery`.

Mandatory behavior:
- prove RED for permanent-throttle-latch behavior on exact base before production correction;
- implement bounded recovery without timeout inflation;
- preserve queue/coalescing, sandbox isolation, Active revision gating, FND-04 TAG binding and Authority;
- add truthful health/recovery observability;
- no client-only or EEE-specific workaround;
- no P1-06 Engineering-shell changes in this order.

Return prefix:

`FC0-A P1-01 CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include exact base/head/tree, changed files, RED evidence, GREEN matrix, local tests, natural T1 run, and explicit non-actions.

No self-merge/freeze authority.
