# Wave 15 — FC0-A Audit Blocker Correction Preparation

> PREPARED ONLY. No product mutation is authorized by this file.
> These orders may be activated only if the independent post-FND06 audit confirms the corresponding blocker.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`STATE: PREPARED / NOT ACTIVE / WAIT_INDEPENDENT_AUDIT`

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`PROVISIONAL_BASE_PRODUCT_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`PROVISIONAL_BASE_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

The actual activation base must be revalidated after FND-06 freeze and any coordination-only commits.

---

## 1. Prepared correction A — W15-P1-01 Server Script recovery

`PREPARED_ORDER_ID: FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

`ORDER_STATE: NOT_AUTHORIZED / WAIT_AUDIT_CONFIRMATION`

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

`PREPARED_ORDER_ID: FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

`ORDER_STATE: NOT_AUTHORIZED / WAIT_AUDIT_CONFIRMATION`

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
1. independent AUD confirms exact blocker(s);
2. Main activates the smallest confirmed correction;
3. CODEX executes exact bounded order on isolated branch;
4. Main review + exact-head CI + post-merge broad;
5. repeat for the second blocker if still required;
6. rerun affected rows of `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
7. only then consider FC0-A release.

No DEV/FND-05/FND-07 release is implied by this preparation.
