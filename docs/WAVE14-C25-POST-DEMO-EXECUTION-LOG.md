# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / C25.6 COMPLETE / C25.7 ARCHITECTURE AUDIT ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`  
**Binding product contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Full code audit:** `docs/WAVE14-C25-FULL-CODE-AUDIT.md`  
**Restore-first architecture:** `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`  
**Reusable libraries architecture:** `docs/WAVE14-C25-REUSABLE-LIBRARIES-ARCHITECTURE.md`  
**Reusable libraries closure:** `docs/WAVE14-C25-REUSABLE-LIBRARIES-IMPLEMENTATION-STATUS-2026-09-06.md`  
**Contextual Help architecture:** `docs/WAVE14-C25-CONTEXTUAL-HELP-ARCHITECTURE.md`  
**Coordinator handoff:** `docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md`

> GitHub live state is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. Historical detailed ledger revisions remain preserved in Git history; this file is the current resumable authority.

## 1. Permanent governance

- #283 remains OPEN/DRAFT and may target only `wave14/corrections-integration`.
- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun; no blind reruns.
- Never weaken tests, validation, authentication, authorization, licensing, lifecycle, package or Runtime authority for green CI.
- Backend Active revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product gap.
- Wave13 #205/#207 remains paused.
- C11 remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3` until C25 is fully accepted, merged only into integration and post-merge exact-SHA revalidated.
- #263 remains the preserved C11 OPEN/DRAFT PR.
- #266 remains validation-only and MUST NEVER MERGE.

## 2. Accepted baseline beneath C25

C25 branch base:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Accepted C24 product authority beneath C25:

`40a491c2de2403f2934b8bae647c35072d5c2496`

C24 remains **ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED**.

## 3. Checkpoint matrix

### C25.0 — Durable bootstrap + full architecture/code audit

**COMPLETE**

Audit authority includes `063a826f551e840f21ccd1c96d2d4ff2960322c3` and `docs/WAVE14-C25-FULL-CODE-AUDIT.md`.

### C25.1 — Engineering Lock domain/package/security

**COMPLETE**

Canonical Lock state, one-way verifier, package persistence and secret-domain separation are implemented and tested. Engineering Lock does not encrypt `.escadapkg` and does not replace Authority authentication/capability.

### C25.2 — Engineering Lock backend enforcement

**COMPLETE**

Backend Authority remains first capability authority; Lock is an additional fail-closed application-content policy. Restricted administration/recovery/licensing boundaries remain explicit.

### C25.3 — Engineering Lock UI/lifecycle/package

**COMPLETE**

Exact historical closing authority:

`a5fb9959fa15c060f51417de7bea84a5eec11e5b`

C25 and C03 exact-SHA validation were green.

### C25.4 — Restore-first / System Recovery

**COMPLETE**

Exact historical closing authority:

`06948450c365009531d584b8b9d1d45e05c1aec8`

Authority backup remains encrypted and separate from application package/license/Lock secrets. Fresh bootstrap restore, restored-user normal authentication, prospective restored-Administrator validation and canonical Working -> Save -> Publish -> Activate application recovery are implemented and browser/backend validated.

### C25.5 — Runtime session UX

**COMPLETE**

Exact historical closing authority:

`8235dbec5c8af56961032a2770fd41de87a64bf5`

Exact-SHA CI:

- C25 #115 / `34033504818` — SUCCESS;
- C03 #200 / `34033504822` — SUCCESS.

System-owned current identity, fail-closed logout/switch, server invalidation first, capability reload, Runtime-only surface reduction and fullscreen session controls are proven.

### C25.6 — Reusable Resource Libraries

**COMPLETE / EXACT-SHA GREEN / NOT INTEGRATED**

Binding architecture:

`docs/WAVE14-C25-REUSABLE-LIBRARIES-ARCHITECTURE.md`

Closure status:

`docs/WAVE14-C25-REUSABLE-LIBRARIES-IMPLEMENTATION-STATUS-2026-09-06.md`

Exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

Exact-SHA CI:

- Wave 14 C25 Post-Demo #275 / run `34052101709` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #280 / run `34052101702` — **SUCCESS**.

Documentation-only closure HEAD `c820665a9dc526e39dd7596825f01e21b345ffba` was also exact-SHA validated:

- C25 #279 / `34052472926` — **SUCCESS**;
- C03 #282 / `34052472951` — **SUCCESS**.

C25.6 proves `.escadalib` package/inspection, non-mutating association, selective dependency-aware incorporation, deterministic collision/deduplication, project-owned informational provenance, self-contained `.escadapkg` roundtrip, safe disassociation, no Runtime library dependency and the complete Engineering Libraries browser flow under the existing Authority/Engineering Lock chain.

No unresolved C25.6 audit gap remains. C25.6 closure does not authorize integration or overall C25 acceptance.

### C25.7 — Contextual multilingual Help/manual

**ARCHITECTURE / CODE AUDIT ACTIVE / PRODUCT MUTATION NOT YET STARTED**

Binding architecture is being frozen in:

`docs/WAVE14-C25-CONTEXTUAL-HELP-ARCHITECTURE.md`

Binding requirements remain:

- stable language-neutral Help IDs;
- centralized resolver from Help ID to installed topic;
- pt-BR/en/es mandatory for shipped UI languages;
- local/offline normal use where practical;
- locale changes preserve topic identity;
- missing/broken Help IDs fail gracefully and are test-detectable;
- Driver documentation derives from actual shipped driver registry/contracts;
- Script API documentation derives from actual shipped Script contracts and public allow-lists;
- reusable-library behavior is documented from completed C25.6 semantics;
- product-facing content contains no internal Wave/handoff/coordinator prose;
- Alarm / Operational Event / Audit remain explicitly distinct.

#### Locale audit result

An earlier documentation assumption of two locale stores was incorrect. Live code establishes one canonical persisted locale authority already:

- `engineering/i18n.ts` owns `elitescada.engineering.locale` and pt-BR/en/es;
- `appShellI18n.ts` explicitly delegates to the Engineering locale owner and subscribes to the same storage key/document language;
- C25.6 Libraries introduced no additional locale state.

Therefore C25.7 must **reuse** the existing locale authority. It must not create a Help-specific persisted locale state. No locale migration product slice is required.

#### Help surface audit result

No existing centralized stable Help-ID registry/resolver was found in the current product audit. C25.7 therefore needs one product-owned registry/resolver rather than scattered route strings.

#### Driver documentation authority audit

The shipped Data Source inventory already has a canonical build-specific path:

`CommunicationDriverModuleRegistry` -> `EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(...)` -> `/api/engineering/data-source-types`.

The catalog already projects driver/source identity, Engineering capabilities, Data Source and TAG-binding configuration fields, expected formats and examples. It must remain the inventory authority; the manual must not contain a second handwritten Driver list.

Live audit also identified a contract gap: `CommunicationDriverTypeDescriptor` already owns `DriverContractVersion`, Runtime capabilities and acquisition modes, but the current `EngineeringDataSourceTypeView` does not project all of them. The descriptor/catalog also does not yet encode every user-facing documentation semantic required by the product contract, such as applicable quality/timestamp behavior, reconnect/timeouts, security/certificates and interoperability limits. C25.7 must enrich the canonical driver-owned documentation contract/projection rather than fabricating these facts in frontend copy.

#### Script documentation authority audit

The official Client Visual Script product API allow-list is `CLIENT_VISUAL_PYTHON_CAPABILITIES`. It currently exposes:

- `tag.read`;
- `tag.write`;
- `clientMemory.read`;
- `clientMemory.write`;
- `visualProperty.read`;
- `visualProperty.write`;
- `visualTween.request`.

`backendOperation.request` is explicitly a reserved host-composition protocol hook and is not an ordinary Script product API. The existing Script Assistant already builds its capability catalog from the official allow-list. Help/API reference must consume the same authority and must not advertise the reserved hook.

Script safety/lifecycle reference must also reflect actual shipped sandbox denied boundaries and execution policy from the Script contracts/runtime constants.

### C25.8 — Integrated regression/audit pass

**NOT STARTED**

### C25.9 — Exact final candidate matrix and acceptance

**NOT STARTED**

Overall C25 acceptance will require one exact final candidate SHA, required regression/compatibility matrix, diagnosed reds before any rerun, and explicit Product Owner acceptance. Only then may C25 merge into `wave14/corrections-integration`, followed by post-merge exact-SHA revalidation.

## 4. Current execution order

1. commit/freeze the C25.7 Help architecture plus the corrected locale audit;
2. exact-SHA validate that documentation-only HEAD with C25 + C03;
3. implement the Help registry/resolver, local multilingual general topics and Help surface using the existing canonical locale authority;
4. add contextual entry points and structural/browser validation for topic identity, locale preservation and missing IDs;
5. generate/project Client Visual Script API reference from the official Script capability authority and document sandbox/lifecycle semantics from shipped contracts;
6. enrich the canonical driver-owned documentation descriptor/catalog/API so all production Driver reference pages derive from the installed build rather than a handwritten inventory;
7. add Driver-reference coverage proving the Help Driver inventory equals the build catalog and field-specific Help IDs remain stable across locales;
8. run C25.7 exact-SHA backend/web/C03 matrix and record closure;
9. proceed to C25.8 integrated audit/regression;
10. proceed to C25.9 exact final candidate and explicit acceptance;
11. only after acceptance, merge into integration and revalidate before any C11 synchronization.

## 5. Resume protocol

On a new coordinator/chat session:

1. fetch #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch current C25 HEAD and exact-SHA workflows;
4. read this ledger plus the C25.6 closure status and C25.7 Help architecture;
5. distinguish the latest validated product/test SHA from any later documentation-only HEAD;
6. continue C25.7 product mutation only if the latest architecture/documentation HEAD is exact-SHA green;
7. never reconstruct authority from chat memory when GitHub live can be queried.
