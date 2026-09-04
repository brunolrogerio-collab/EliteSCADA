# Wave 14 C19 — Live Progress / Coordinator Handoff

**Updated:** 2026-09-04 BRT  
**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**PR:** #257 — OPEN / DRAFT -> `wave14/corrections-integration`  
**Exact accepted pre-C19 product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**Exact accepted C19 PRODUCT SHA:** `43d2734b18dc8b78caaa917b9bf6a381ca47b202`  
**Accepted C19 product tree:** `6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`  
**C19 status:** ISOLATED ACCEPTED  
**C11:** IMPLEMENTATION LOCKED

> GitHub is the official memory. Revalidate live refs before acting. Any documentation commit after `43d2734...` is branch metadata only and does not redefine the accepted C19 product-code authority.

## 1. C19 purpose

C19 closes `C11-P2-EVT-01`, the last functional product gap found after C12–C18 convergence:

1. normal Engineering authoring of canonical C14 Operational Event definitions;
2. generic Server Script emission through canonical C14 Active Runtime authority.

Required generic chain:

`Engineering authoring -> Preview/Apply -> Save/Publish/Activate -> Server Script -> emit_operational_event -> C14 Active Runtime -> event bus/history -> C18 Event Browser`

No EEE-specific code, direct history insertion, parallel event model, Command->Event shortcut, client-side event authority or DEMO-only bypass is allowed.

## 2. Accepted base before C19

Exact C12–C18 accepted product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

Evidence on that exact SHA:

- EliteSCADA CI #1360 / `33920467689` — SUCCESS;
- Wave11 Active HMI Runtime #288 / `33920467682` — SUCCESS;
- Preview Licensing CI #310 / `33920467789` — SUCCESS;
- L3 Seven-Driver Lab #265 / `33920467676` — SUCCESS;
- Interop Lab Smoke #187 / `33920467665` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #91 / `33921310281` — SUCCESS;
- Test Preview #16 / `33921526826` — SUCCESS on disposable infrastructure overlay based directly on the exact product SHA; PR #256 closed without merge.

## 3. C19 implementation

### Engineering authoring

Implemented on the C19 branch:

- first-class Operational Events Engineering navigation/editor;
- create/edit via canonical Working package, not manual JSON;
- protected Preview -> CAS recheck -> Apply flow;
- fields for stable ID, key, name, type, category, source, area, equipment, TAG association, message and enabled state;
- TAG selection through configured Engineering TAGs;
- pt-BR / en / es affected chrome;
- backend/CAS Apply errors remain visible.

Important identity correction: static review found the editor had recreated the same class of NEW-selection race previously fixed in C17. `Nova Evento` could enter `isNew=true` while still carrying the previous persisted draft/ID. The transition was corrected so a pristine draft is installed synchronously before new-mode identity, with deterministic Wave11 regression coverage. Commit `4556e30409e75c13d1446829ded9c6932a31c02f` contains the regression checkpoint after that correction.

### Server Script emission

Implemented:

- deterministic Python API `emit_operational_event(definition_id, message=None, context=None)`;
- bounded message/context validation aligned with C14 limits;
- Python process only returns requested occurrences and has no event-bus/history authority;
- C# executor validates stable definition GUID and replays through canonical runtime authority;
- TAG/Server Memory requests remain replayed before Operational Event requests;
- unresolved/disabled/stale definitions fail closed through Active C14 definition/revision authority;
- no new Script dependency kind was introduced because stable ID + exact Active revision resolution already provide authority and fail-closed semantics.

## 4. Canonical runtime-gate architecture

The first C19 implementation introduced a separate bridge gate and then attempted re-entrancy with `AsyncLocal` during activation. Wave11 #291 proved that design incorrect: Active revision advanced, but an `Initialize` emission never reached history.

That design is OBSOLETE and must not be restored.

Accepted architecture reuses the existing canonical `ServerScriptRuntimeManager._revisionGate`, already responsible for exact Active revision serialization of Server Script TAG/Server Memory access and runtime swap.

Relevant correction commits:

- `075904ff82b425ed20aed2d8ced83ab33b4a608e` — host exposes revision-gated execution helper;
- `48202f29a9f6ac2e52109c6a5421f7472c04a8c3` — bridge simplified, no `AsyncLocal`, no parallel gate;
- `33313f5d56c343d98c80c9fd790fb82e18db3384` — published activation binds canonical event authority then activates;
- `d989003efd9be364b8c333e749fbe3c033c47fb2` — persisted Active recovery follows the same model;
- `537ce074466aaf34d98142309ed4fd71036cf050` — Wave11 surfaces canonical Server Script diagnostics so faults cannot masquerade as generic history timeouts.

Accepted locking behavior:

1. runtime swap is serialized under `_revisionGate`;
2. an old generation attempting event emission during swap waits;
3. after swap it fails exact project/revision identity instead of emitting into the new revision;
4. a new generation `Initialize` starts after the swap gate is released and may emit normally;
5. Operational Event emission executes under that same gate and resolves only through C14 Active authority;
6. no second gate, reentrant token, `AsyncLocal` ownership trick or pre-check TOCTOU shortcut exists.

Coordinator architecture review on the final candidate found no second lock, lifecycle bypass, direct historical insertion, C14 bypass, EEE-specific behavior, identity weakening or security/licensing weakening.

## 5. Packaging blocker and correction

Product SHA `537ce074466aaf34d98142309ed4fd71036cf050` was 5/6 green. Wave11 #293 failed because the Debug application output did not contain `ServerScriptRunner.py` at the location required by `ServerScriptRuntimeManager`:

`python3: can't open file '/home/runner/work/EliteSCADA/EliteSCADA/src/Scada.Api/bin/Debug/net10.0/ServerScriptRunner.py': [Errno 2] No such file or directory`

Root cause: `Scada.Api.csproj` copied `Runtime/ServerScriptRunner.py` while preserving its relative subpath, but runtime resolves the normal application asset as `Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py")`.

Correction in `1d048b0e86afce674520dcfa49450cbd66c69b47`:

- keep `CopyToOutputDirectory="PreserveNewest"`;
- keep `CopyToPublishDirectory="PreserveNewest"`;
- add `TargetPath="ServerScriptRunner.py"`.

No repository path was hardcoded, timeout was not increased, browser coverage was not weakened and isolated Python execution was preserved.

## 6. Deterministic backend coverage added during acceptance review

Coordinator review found that the browser test covered the integrated C19 path, while deterministic backend tests covered C14 and Server Script separately but did not directly prove the new bridge contract required by the C19 implementation handoff.

Added `ServerScriptOperationalEventBridgeIntegrationTests.cs` to prove:

1. real Python `Initialize` can call `emit_operational_event` through the bridge;
2. occurrence identity/type/category/source remain canonical C14 definition authority;
3. allowed `message` and `context` overrides flow correctly;
4. unknown definition is rejected;
5. disabled definition fails closed;
6. stale revision fails closed;
7. forged-looking `type`, `category`, `source` and `definitionId` context keys remain inert context and cannot replace canonical occurrence fields;
8. existing Server Memory behavior remains functional in the same Python handler.

Two test-only corrections were diagnosed rather than rerun blindly:

- `682c111...` initially failed C# compilation because an interpolated raw string used Python dictionary braces; corrected in `bb7e3f6...` with the proper `$$"""` interpolation form;
- `bb7e3f6...` then proved the positive full bridge test green, while the negative fixture failed before bridge assertions because it attempted to activate an event-only package with no runtime source/TAG. The fixture was corrected to use the same minimal `memory.client` runtime shape already used by canonical C14 tests.

Final test correction commit and accepted product candidate:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

The full backend test suite then passed, including both bridge tests.

## 7. Final isolated acceptance evidence

Accepted C19 PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

Git tree:

`6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`

Final six required workflows associated with that head all completed SUCCESS:

- EliteSCADA CI #1369 / `33930751824` — SUCCESS;
- Wave11 Active HMI Runtime #297 / `33930751831` — SUCCESS;
- Preview Licensing CI #319 / `33930751817` — SUCCESS;
- L3 Seven-Driver Lab #275 / `33930751839` — SUCCESS;
- Interop Lab Smoke #196 / `33930751819` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #112 / `33930740869` — SUCCESS.

Wave11 #297 is the direct regression proof for the original packaging blocker: the Active revision browser lifecycle completed successfully, including the C19 integrated flow and Playwright evidence upload.

EliteSCADA CI #1369 completed backend build/test/runtime smoke and Chromium E2E successfully. The backend suite includes the deterministic bridge tests described above.

### Exact-byte CI note

Validation PR #259 is a DRAFT CI-trigger PR against `main` and must NEVER be merged. GitHub pull-request workflows use a synthetic merge commit for checkout.

For the final candidate:

- PR head PRODUCT SHA: `43d2734b18dc8b78caaa917b9bf6a381ca47b202`;
- product tree: `6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`;
- synthetic merge ref during validation: `0a252f011cea04ab8d585b61f5951188ad9f075e`;
- synthetic merge tree: `6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`.

Therefore the pull-request workflows executed a different commit object but a byte-identical Git tree to the accepted product candidate. Do not describe the synthetic merge SHA as the PRODUCT SHA.

## 8. Isolated acceptance decision

**C19 ISOLATED ACCEPTED** at exact PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

This acceptance is isolated only. It does not merge or authorize `main`, does not release C11 and does not redefine the final Wave14 product freeze.

## 9. Mandatory next route

1. revalidate PR #257 head and `wave14/corrections-integration` live;
2. compose accepted C19 into `wave14/corrections-integration` preserving history;
3. record the resulting exact combined C12–C19 PRODUCT SHA;
4. run EliteSCADA CI, Wave11, Preview Licensing, L3, Interop and dedicated C03 again on the combined product bytes;
5. repeat real Preview/browser Product Owner validation on the combined product;
6. declare a new exact product freeze only after coherent automated + real-browser evidence;
7. finish remaining C11 browser evidence: Analog Fill live behavior, Dynamo operational/bad-quality state, two independent Dynamo instances, canonical Runtime resolutions/scaling, fullscreen/no-scroll/overlay without reflow, and one integrated living chain;
8. only if every C11 blocker is cleared, explicitly record `RELEASE C11 IMPLEMENTATION`;
9. construct the canonical living EEE Simulation entirely through ordinary generic EliteSCADA mechanisms;
10. final Wave14 acceptance;
11. resume Wave13 issue #205 / PR #207 packaging/signing only on final accepted Wave14 bytes.

## 10. Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- PR #257 targets only `wave14/corrections-integration`; isolated C19 acceptance authorizes composition there, not `main`;
- PR #259 is validation-only, DRAFT, targets `main` only as a CI trigger and must NEVER be merged;
- C11 remains IMPLEMENTATION LOCKED;
- Wave13 remains paused;
- Alarm, Operational Event and Audit remain distinct;
- backend Active revision remains canonical authority;
- no EEE-specific simulator service/Driver/hidden package/private host/DEMO-only route;
- no direct historical insert;
- no test/security/identity/licensing weakening;
- diagnose before rerun.

## 11. Mandatory resume order

1. `PROJECT GOAL.md`;
2. integration `LAST CHANGE.md`;
3. integration `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. integration `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
6. this file;
7. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
8. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
9. C11 Pass-2 audit + HMI clarification;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211;
12. live PRs #212, #257 and #259;
13. revalidate exact branch/product SHA and workflow runs;
14. continue from C19 composition into integration, never from a docs-only HEAD mistaken for product authority.
