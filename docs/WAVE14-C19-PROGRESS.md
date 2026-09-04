# Wave 14 C19 — Live Progress

**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**PR:** #257 — DRAFT -> `wave14/corrections-integration`  
**Exact product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**C11:** IMPLEMENTATION LOCKED

This file is intentionally operational. Read `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md` first. Live GitHub wins if the branch moved after this note.

## Current implementation checkpoint

Latest product commit before this docs-only note:

`3b943e36fd1e8ca8799dbd073e6ff05d127455e7`

Do not treat this as an accepted candidate until exact-head gates and the focused C19 Wave11 proof are green.

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
- [x] affected editor/nav chrome exists in pt-BR / en / es;
- [x] Apply failures preserve the backend/CAS error instead of clearing it immediately at `3b943e36fd1e8ca8799dbd073e6ff05d127455e7`.

### Server Script / C14 bridge

- [x] `ServerScriptOperationalEventBridge` added at `761105f9968380809c0d391c43f2503af13b77a9`;
- [x] deterministic Python API `emit_operational_event(definition_id, message=None, context=None)` added at `723c75577177bbc93f3679c848051385bee87ffd`;
- [x] Python message/context bounds mirror canonical C14 limits: message <=4000 chars, context <=128 entries, key <=160, value <=4000;
- [x] Python child only returns requested occurrences; it has no event-bus/history authority;
- [x] C# executor replay added at `e8dbd7bbeb33d0aa886ae3b0677f787a833caadb`;
- [x] executor validates the stable definition GUID and delegates to the C19 bridge;
- [x] TAG/Memory requests are replayed first, then Operational Event requests, preserving existing deferred write semantics;
- [x] published activation and persisted Active recovery bind the shared Script host to the existing runtime/event authorities without creating another runtime coordinator;
- [x] bridge verifies exact Active project/revision and resolves definitions only from the C14 Active snapshot;
- [x] unresolved/disabled definitions fail closed because disabled definitions are absent from the Active C14 snapshot;
- [x] stale script generation cannot cross an Active revision swap;
- [x] missing Script diagnostic namespace import fixed at `eef644b0a8c31e3392106f447d0cc1532c1eae76`.

### Activation gate correction

Static review found that the first C19 bridge gate could self-deadlock if a newly activated Server Script `Initialize` handler emitted an Operational Event while its own activation still held the bridge gate.

The correction at:

`45adfe3fe952c5dfbd81e3b6c04258ccb2b45ea0`

makes the activation lease re-entrant only for the exact current activation context/token.

Verified design against `ServerScriptRuntimeManager.ActivateRuntimeCoreAsync(...)`:

1. normal Script runtime still serializes Runtime revision swap under its existing `_revisionGate`;
2. C19 bridge serializes ordinary event emission against the outer activation/recovery flow;
3. after the canonical runtime swap, new-generation `Initialize` handlers inherit the exact current activation lease and may emit without self-deadlock;
4. emissions from old/outside generations do not own that lease and wait;
5. after activation disposes the lease, `ActiveLeaseToken` is invalidated, so inherited async/timer contexts cannot keep re-entrant privilege;
6. an old generation that reaches emission after the swap then fails the exact project/revision check instead of emitting into the new revision.

Do not replace this with a pre-call revision check that reintroduces a TOCTOU race.

## Wave11 C19 proof added

`a19da7a97baa1fb1e30997bbb6c83551177d47a7`

adds:

`web/scada-web/tests-wave11/c19-operational-event-script-bridge.spec.ts`

The test deliberately uses a Server Script `Initialize` handler so it proves both the normal user workflow and the re-entrant activation boundary:

`Engineering UI author Event -> Preview/Apply -> add canonical Server Script -> Save -> Publish -> Activate -> Initialize -> emit_operational_event -> C14 event bus/history -> C18 Event Browser`

It also checks the C19 Engineering heading in pt-BR / en / es and verifies the resulting occurrence through protected `operational.events` before requiring it in the already-authored C18 Event Browser.

`05d78b916cb1a6873881ba3c76376beb03359cce`

inserts the C19 test deterministically after C18 and before the Wave11 owner-package artifact.

## Development validation already completed on intermediate product SHA

Intermediate product SHA:

`eef644b0a8c31e3392106f447d0cc1532c1eae76`

This SHA predates the re-entrant-gate correction and C19 E2E, so it is **not** a C19 candidate. It nevertheless proved that the original authoring/bridge implementation compiled and did not regress the existing product surface.

All completed SUCCESS on exact `eef644b0...`:

- EliteSCADA CI #1362 / `33926163307`;
- Wave11 Active HMI Runtime #290 / `33926163231`;
- Preview Licensing CI #312 / `33926163221`;
- L3 Seven-Driver Lab #268 / `33926163254`;
- Interop Lab Smoke #189 / `33926163315`;
- Wave 14 C03 DNP3 Adapter #97 / `33925884987`.

No rerun or contract weakening was used for this checkpoint.

## Architecture decision: no new Script dependency kind

C19 deliberately does **not** add an `OperationalEvent` value to `ScriptEngineeringDependencyKind`.

Reason:

- C14 already makes the Active Operational Event snapshot the runtime authority;
- the Script API requires a stable definition GUID;
- the host resolves that GUID only against the exact Active revision under the activation/emission boundary;
- unknown/disabled/stale IDs fail closed;
- adding a dependency kind would expand schema/import/reference-resolution surfaces without adding authority.

Reopen only if a concrete security/lifecycle defect is proven.

## Not yet accepted / next exact route

- [x] Apply-error UI issue fixed;
- [x] intermediate Web/.NET compile and regression validation completed;
- [x] integrated Wave11 C19 E2E authored and sequenced;
- [ ] run the new C19 Wave11 proof on exact current product SHA `3b943e36...`;
- [ ] run exact-head universal/specialized gates on the current product SHA;
- [ ] diagnose every red before rerun or product change;
- [ ] if green, perform Coordinator exact-diff architecture review and declare isolated C19 candidate/acceptance;
- [ ] compose accepted C19 into `wave14/corrections-integration` without rewriting accepted history;
- [ ] five combined gates on exact C12–C19 product SHA;
- [ ] C03 DNP3 carry-forward on exact combined SHA;
- [ ] real Preview/browser carry-forward;
- [ ] freeze the new exact product authority;
- [ ] finish C11 browser-evidence revalidation and explicit release decision.

A development-time workflow is evidence only when tied to the exact product SHA being discussed. Docs-only commits do not redefine product authority.

## Runtime bridge map

Production shared Server Script host call sites remain:

1. `ScadaRuntimeFacade.Describe()` resolves `ServerScriptRuntimeManager.GetShared(...)` for diagnostics;
2. `PublishedRuntimeActivationService.ActivateAsync()` resolves the shared manager for Publish -> Activate;
3. `PersistedRuntimeRecoveryService.RecoverAsync()` resolves the shared manager for startup recovery.

C19 leaves `ServerScriptRuntimeManager.GetShared(...)` signature unchanged.

C14 authority remains separate from Alarm/Audit and remains canonical:

- raw `GatewayEngineeringRuntimeCoordinator : IOperationalEventRuntime` owns the Active Operational Event definition snapshot and emission;
- licensed/host-owned `IEngineeringRuntimeCoordinator` remains lifecycle authority;
- C19 binds those existing authorities to the existing shared Script host;
- Python never publishes directly to the event bus or writes history.

## If a new Coordinator chat resumes here

1. read `PROJECT GOAL.md`;
2. read integration `LAST CHANGE.md`;
3. read integration `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read integration `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. read live C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
6. read this progress file;
7. revalidate live #257 and exact branch HEAD;
8. distinguish docs-only branch HEAD from latest product SHA;
9. inspect workflow runs tied to the exact product SHA, not merely the branch name;
10. keep #212 DRAFT, #257 DRAFT, C11 locked and Wave13 paused;
11. diagnose any red before rerun.
