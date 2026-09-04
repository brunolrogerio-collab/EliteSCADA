# Wave 14 C19 — Live Progress

**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**PR:** #257 — DRAFT -> `wave14/corrections-integration`  
**Exact product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**C11:** IMPLEMENTATION LOCKED

This file is intentionally operational. Read `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md` first. Live GitHub still wins if the branch moved after this note.

## Current implementation checkpoint

Latest completed product commit before this note:

`eef644b0a8c31e3392106f447d0cc1532c1eae76`

The branch also contains the immediately preceding bridge commits listed below. Revalidate the live branch HEAD before continuing.

## Completed implementation

### Engineering authoring

- [x] branch created from exact product base `2e284606...`;
- [x] PR #257 opened DRAFT to `wave14/corrections-integration`;
- [x] full implementation handoff committed at `68ac7ed1c019954f88671a983ee328dc9a7ceb47`;
- [x] protected Operational Event Engineering editor added at `b253f3fe5671f10d5988411b7120fb3cd848a901`;
- [x] normal Engineering navigation wired at `677149a75cd9c7e8ebe0c1558b327029c316d1cb`;
- [x] editor uses canonical package mutation flow: Working version -> Preview -> CAS recheck -> Apply;
- [x] editor supports create/edit with stable ID, key/name/type/category/source/area/equipment/TAG/message/enabled;
- [x] TAG association uses configured TAG selection instead of raw GUID typing;
- [x] affected editor/nav chrome exists in pt-BR / en / es.

### Server Script / C14 bridge

- [x] `ServerScriptOperationalEventBridge` added at `761105f9968380809c0d391c43f2503af13b77a9`;
- [x] bridge uses one gate keyed by the shared `ServerScriptRuntimeManager`, avoiding another runtime singleton;
- [x] published activation holds the same gate at `5f6629546080e4faeb71b23b42b2f1f2655e5b96`;
- [x] persisted Active recovery holds the same gate at `67bbfd62c76638a5e3fe7a613bf5753430689baa`;
- [x] deterministic Python API `emit_operational_event(definition_id, message=None, context=None)` added at `723c75577177bbc93f3679c848051385bee87ffd`;
- [x] Python message/context bounds mirror the canonical C14 limits: message 4000 chars, context <=128 entries, key <=160, value <=4000;
- [x] Python child only returns requested occurrences; it has no event-bus/history authority;
- [x] C# executor replay added at `e8dbd7bbeb33d0aa886ae3b0677f787a833caadb`;
- [x] executor validates the stable definition GUID and delegates to the C19 bridge;
- [x] TAG/Memory requests are replayed first, then Operational Event requests, preserving existing deferred write semantics;
- [x] bridge verifies exact Active project/revision after acquiring the same gate used by activation;
- [x] bridge resolves the definition from the active C14 snapshot and delegates to `IOperationalEventRuntime.EmitOperationalEventAsync(...)`;
- [x] unresolved/disabled definitions fail closed because disabled definitions are absent from the active C14 snapshot;
- [x] stale script generation cannot cross an Active revision swap through the bridge gate;
- [x] missing Script diagnostic namespace import fixed at `eef644b0a8c31e3392106f447d0cc1532c1eae76`.

## Architecture decision: event requests do not add a new Script dependency kind

C19 deliberately did **not** add an `OperationalEvent` value to `ScriptEngineeringDependencyKind`.

Reason:

- C14 already makes the Active Operational Event snapshot the runtime authority;
- the script must use a stable definition GUID;
- the host resolves that GUID only against the exact Active revision while holding the activation/emission gate;
- unknown/disabled/stale IDs fail closed;
- adding a new dependency kind would expand schema/import/reference-resolution surfaces without adding runtime authority that C14 does not already provide.

Do not reverse this decision merely for aesthetics. Reopen it only if a concrete security/lifecycle defect is proven.

## Known issue found during static review

`web/scada-web/src/engineering/OperationalEventEditor.tsx` currently has a small Apply-error presentation bug:

```text
catch -> setError(...) -> invalidatePreview()
```

`invalidatePreview()` also clears `error`, so the Apply error can disappear immediately.

Required correction before candidate freeze:

- clear preview/candidate/version while preserving the caught error, or
- invalidate first and set the caught error afterwards.

This does not affect backend authority, but do not leave it unresolved.

## Not yet validated / do not claim candidate

- [ ] fix the Apply-error UI issue above;
- [ ] web TypeScript/build validation;
- [ ] .NET solution build validation after C19 runtime changes;
- [ ] focused deterministic Server Script / Operational Event tests;
- [ ] integrated Wave11 Engineering -> Save/Publish/Activate -> Script emit -> Event Browser E2E;
- [ ] pt-BR/en/es browser evidence for the C19 Engineering section;
- [ ] exact C19 candidate freeze;
- [ ] five exact candidate gates;
- [ ] Coordinator exact-diff architecture review;
- [ ] composition into integration;
- [ ] five combined gates;
- [ ] C03 DNP3 carry-forward on exact combined SHA;
- [ ] real Preview/browser carry-forward;
- [ ] final C11 browser-evidence closure and release decision.

A development-time C03 workflow may run automatically because of PR/path configuration. Do **not** treat an intermediate-head run as C19 acceptance evidence.

## Runtime bridge map

Production shared Server Script host call sites:

1. `ScadaRuntimeFacade.Describe()` resolves `ServerScriptRuntimeManager.GetShared(...)` for diagnostics;
2. `PublishedRuntimeActivationService.ActivateAsync()` resolves the same shared manager for Publish -> Activate;
3. `PersistedRuntimeRecoveryService.RecoverAsync()` resolves the same shared manager for startup recovery.

C19 intentionally leaves `ServerScriptRuntimeManager.GetShared(...)` signature unchanged.

C14 authority remains:

- raw `GatewayEngineeringRuntimeCoordinator : IOperationalEventRuntime`;
- licensed/host-owned `IEngineeringRuntimeCoordinator` remains the runtime lifecycle authority;
- C19 binds both authorities to the shared Script manager without creating another coordinator;
- activation/recovery and script event emission are mutually exclusive under the C19 per-host gate.

## Next exact implementation step

1. correct the OperationalEventEditor Apply-error state bug;
2. inspect current C19 HEAD and run compile/build validation;
3. diagnose every compile/test failure before changing product code;
4. add focused tests, then the integrated Wave11 C19 E2E;
5. update this file after each material checkpoint.

## If a new Coordinator chat resumes here

1. read `PROJECT GOAL.md`;
2. read integration `LAST CHANGE.md`;
3. read integration `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read integration `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. read live C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
6. read this progress file;
7. revalidate live #257 and exact branch HEAD;
8. inspect workflow runs tied to that exact HEAD;
9. do not redo the authoring/bridge architecture from chat recollection;
10. continue at the first unchecked item above;
11. keep #212 DRAFT, #257 DRAFT, C11 locked and Wave13 paused.
