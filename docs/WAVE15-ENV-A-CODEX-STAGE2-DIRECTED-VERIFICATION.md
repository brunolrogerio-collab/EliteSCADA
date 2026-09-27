# Wave 15 — ENV_A CODEX Stage 2 Directed Verification

**Status:** PREPARED / NOT ACTIVE  
**Owner:** MAIN COORDINATOR  
**Executor when activated:** shared CODEX on local ENV_A  
**Product authority:** exact Wave 15 integrated product checkpoint accepted by Main  
**Current checkpoint at preparation:** `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd` / tree `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`  
**Purpose:** second verification stage after the fresh-install first-project black-box journey, using the real project created by CODEX as a realistic test body for directed Wave 14 -> Wave 15 regression verification.

GitHub live remains the sole authority. Revalidate the exact product/harness/control state before activation.

---

## 1. Positioning

This is **not** the first-project black-box audit.

Stage 1 answers:

> Can a new user, from a truly fresh installation and using only normal product-visible guidance, create a small SCADA application and reach a functional Runtime?

Stage 2 answers:

> After the user journey succeeded or stopped honestly, do the generic defects and bounded uncertainties transferred from Wave 14 remain corrected under realistic use, failure, recovery, persistence and restart conditions on the final Wave 15 product?

The two stages must never be conflated.

Stage 1 must complete before Stage 2 begins. Stage 1 evidence is immutable once sealed.

Stage 2 may be more directed and adversarial because its purpose is verification of known contracts, not discovery of the user route.

---

## 2. Historical authority

### Wave 14 diagnostic closure

Canonical transfer documents:

- `docs/WAVE14-DIAGNOSTIC-CLOSURE-ACCEPTANCE-2026-09-11.md`;
- `docs/WAVE15-CORRECTION-BACKLOG-FINAL.md`.

Final direct-CODEX Wave 14 handoffs:

- issue #286 comment `5629630825` — A1 transport/error UX + A2 Working/Active authority;
- issue #286 comment `5634220264` — SIM Server Script throttle + A4 Trends + A5 Popup values/persistence;
- issue #286 comment `5634503355` — A7 Editor selection + A8 Script Engineering composition/scope;
- issue #286 comment `5634970301` — Wave 14 CLOSED / diagnostic transfer complete.

Wave 14 closure rule retained here:

A finding is considered closed only when it is either:

- `CONFIRMED` with an implementable correction/regression contract; or
- `UNCERTAIN — BOUNDED` with exact facts, exclusions, missing observation and deterministic future capture.

Stage 2 should try to convert the bounded Wave 14 findings into direct Wave 15 evidence where ENV_A can remove the old Codespaces forwarding ambiguity.

### Wave 15 product premises

The six Wave 15 lanes have passed their release/integration gates:

- Script Engineering -> `T2_VERIFIED`;
- Editor -> `T2_VERIFIED`;
- Authority UX -> `INTEGRATED / VERIFIED_COMPLETE`;
- Licensing UX -> `INTEGRATED / VERIFIED_COMPLETE`;
- FND-05 -> `VERIFIED / FROZEN`;
- FND-07 -> `VERIFIED / FROZEN`.

Broader integrated T2:

`W15-FOUR-FEATURE-INTEGRATED-T2-01 -> PASS / ACCEPTED`.

Stage 2 therefore verifies integrated behavior. It is not another lane-local T1.

---

## 3. Entry gate

Stage 2 remains `PREPARED / NOT ACTIVE` until Main records all of the following:

1. ENV_A harness is `READY`, including persistent pause/resume proof.
2. Stage 1 CODEX black-box first-project journey is `COMPLETE` or honestly `BLOCKED_BY_PRODUCT`.
3. Stage 1 detailed evidence has been flushed and sealed.
4. Exact Stage 1 project/application state has been preserved.
5. A reproducible Stage 2 working copy/checkpoint of that state exists.
6. Product Owner human journey remains isolated from CODEX details until its own completion.
7. Main publishes an explicit Stage 2 activation order on an exact product/harness SHA.

Stage 2 must never self-start from this document.

---

## 4. Preserve Stage 1 before Stage 2

The project created during Stage 1 is valuable evidence and must not be sacrificed to diagnostic testing.

Before Stage 2:

- pause ENV_A cleanly;
- seal the Stage 1 session manifest, timestamps and evidence;
- record exact product SHA, harness SHA, session ID, project identity and Active revision;
- create a deterministic Stage 2 state checkpoint/clone using repository-controlled infrastructure;
- prove the original Stage 1 state remains restorable and unchanged.

Stage 2 runs against the derived verification state, not the sole original Stage 1 evidence copy.

No diagnostic/fault-injection operation may be performed until this boundary is established.

---

## 5. Stage 2 operating modes

### 5.1 Stage 2A — directed product-visible verification

Start with normal product UI and user-visible Help only.

Unlike Stage 1, CODEX may know the verification objective, e.g. “select existing Screen objects and exercise dependent editor functions,” but should still test the actual product surface rather than jump directly to internals.

During 2A:

- no direct database edits;
- no source patching;
- no product correction;
- no hidden fixture/import to manufacture a pass;
- no internal API shortcut when the same behavior can be exercised honestly through UI.

Capture the failure first if one occurs.

### 5.2 Stage 2B — diagnostic/adversarial verification

After the relevant visible behavior is captured, CODEX may use:

- source inspection;
- public/internal diagnostic endpoints;
- API traces;
- browser network/console;
- process/log inspection;
- controlled transport interruption;
- controlled fault injection already supported by tests/harness;
- deterministic test utilities that do not change product semantics.

Stage 2B exists to correlate cause, authority, freshness, recovery and persistence.

It may not:

- patch product code;
- weaken auth/licensing/Authority;
- mutate DB rows to create success;
- bypass lifecycle;
- invent an EEE-specific workaround;
- modify the Stage 1 evidence copy.

Any correction requires a separate Main order.

---

## 6. Verification matrix

### V2-01 — Fresh-install / Working / Active authority

**Wave 14 source:** A2 / W15-P0-01.

Verify on the Stage 1-created project:

- no unconditional hidden `demo` Working project appears;
- Working selection is explicit and truthful;
- restart/pause/resume returns to coherent persisted Working state;
- Published and Active remain distinct authorities;
- creating/selecting another Working project does not silently mutate Active;
- cross-project activation remains fail-closed;
- no fallback text masquerades as actual Working identity.

Required evidence:

- visible Engineering project identity before/after restart;
- lifecycle revision identities;
- Active Runtime project/revision identity;
- negative cross-project activation evidence.

**Pass:** Working/Published/Active identities remain coherent and intentional across restart/resume.

---

### V2-02 — Engineering transport/error/fallback UX

**Wave 14 source:** A1 product UX + A6 / W15-P1-06.

With a healthy baseline established, introduce controlled temporary API/proxy/network unavailability without changing project authority.

Verify:

- no indefinite blank Engineering bootstrap;
- transport rejection is not falsely presented as an HTTP 500;
- no fabricated `Demo Project` or other fake Working identity;
- loading/unavailable/retry state is truthful and bounded;
- retry restores the same Working/lifecycle authority;
- project state is not mutated merely by recovery.

Correlate browser request timing/status with API health.

Because ENV_A is local, this check intentionally removes the old Wave 14 Codespaces edge/forwarding ambiguity.

---

### V2-03 — Screen + Popup editor selection and schema compatibility

**Wave 14 source:** A3 + A7 / W15-P1-02 + W15-P1-03.

Use actual objects created in Stage 1 and, where safely available, representative supported legacy/current visual identifiers.

Verify for both Screen and Popup:

- select persisted objects through canvas;
- select through object/tree surface;
- Properties opens without blanking/poisoning Engineering;
- bindings remain usable;
- malformed/unsupported content is contained with actionable error;
- known legacy supported type normalizes/migrates safely;
- truly unknown type remains rejected safely.

After selection is proven stable, exercise representative dependent authoring:

- move/resize;
- undo/redo;
- copy/paste;
- grouping where supported;
- lock;
- alignment/distribution;
- z-order;
- property edit;
- binding edit;
- save/reopen persistence.

Do not count code presence or toolbar visibility as proof.

---

### V2-04 — Single-canvas Editor integration

**Wave 15 Editor premise.**

Verify Screen and Popup use the same canonical editing behavior rather than divergent legacy implementations:

- consistent selection semantics;
- consistent resize/move/history behavior;
- consistent Properties/binding behavior;
- interaction layer does not become a second authoritative visual model;
- save/reopen maintains canonical result;
- Runtime projection matches authored state.

This is a Wave 15 integration verification, not a new architecture redesign.

---

### V2-05 — Script Engineering authoring/discovery

**Wave 14 source:** A8 + Script event-authoring gap / W15-P1-05.  
**Wave 15 Script premise:** stable identity/event semantics, stale incompatible event fields cleared deterministically, no second runtime resolver/authority.

Build representative scripts using visible authoring assistance.

Minimum client-side flow:

1. discover two TAGs;
2. insert/read both without manually repairing malformed snippet boundaries;
3. compare values;
4. target a real Screen/Popup object/property using discoverable API/property metadata;
5. conditionally alter an allowed visual state/property;
6. validate with useful line/column diagnostics;
7. preview both true/false paths where supported;
8. persist through the normal lifecycle and observe the effect in Runtime.

Event verification:

- `timer` fields authorable;
- `tagChanged` reference authorable;
- switching event kinds clears or deliberately converts incompatible hidden fields;
- dirty client <-> server scope transition is explicit and recoverable;
- scope-specific API/events/help replace stale guidance.

Negative proof:

- invalid code remains rejected with understandable diagnostics;
- unsupported APIs/properties are not silently accepted.

---

### V2-06 — Server Script bounded recovery and observability

**Wave 14 source:** SIM / W15-P1-01.

Use only a controlled disposable Stage 2 script/workload.

Verify:

- process isolation/allowlist remain intact;
- timeout still enforces a bound;
- repeated transient failures do not create a permanent silent throttle latch;
- recovery is bounded and observable;
- persistent failure becomes visibly degraded rather than silently frozen;
- queue/coalescing remains bounded;
- no stale write is emitted after recovery;
- TAG and Historian freshness recover together when the script recovers;
- last useful execution / health / throttle-recovery state is observable enough to diagnose.

Required adversarial cases:

1. normal execution;
2. transient timeout burst followed by healthy work;
3. persistent failure;
4. recovery after the persistent condition is removed.

Do not “test” this by merely raising/removing the timeout.

---

### V2-07 — Runtime projection/navigation resilience

**Wave 14 source:** A5b / W15-P1-04.

Create a Runtime state with:

- a non-default active Screen where practical;
- at least one open Popup.

Capture current Active authority identity.

Inject controlled transient projection fetch failure/retryable transport interruption.

Verify:

- last successful projection is retained or represented as stale/unavailable without destroying navigation;
- Screen remains selected;
- Popup stack remains;
- recovery on the same `projectKey/revision/activatedAtUtc` keeps navigation state.

Then deliberately activate a genuinely different revision/authority and verify intentional reinitialization occurs.

**Pass:** transient transport != authority change.

---

### V2-08 — Popup live values

**Wave 14 source:** A5a / W15-P2-02.

Test a Popup bound to representative numeric TAGs.

Required states:

- Good numeric zero;
- Good non-zero;
- null/missing;
- non-Good quality;
- stopped source;
- running source;
- reconnect;
- close/reopen.

Verify:

- Good zero renders as zero unless a documented freshness rule truthfully invalidates it;
- unavailable state is distinguishable from a real value;
- last request/success/freshness or equivalent diagnostic state is sufficient to explain `—`;
- reconnect restores values without reopening the entire application when authority is unchanged.

---

### V2-09 — Trends / Historian / realtime separation

**Wave 14 source:** A4 / W15-P2-01.

Use a stable local ENV_A path to remove Codespaces forwarding as the primary variable.

Verify Trends for:

1. History data available;
2. valid no-data range;
3. History request failure;
4. realtime disconnected;
5. realtime reconnecting/recovered;
6. transient projection poll failure under unchanged authority;
7. genuine Active authority change.

Capture under shared timestamps:

- History result/count;
- SignalR/realtime state;
- active navigator view;
- projection identity;
- visible Trends state.

**Pass:** Trends stays mounted when it should, distinguishes data/no-data/history/realtime states truthfully, and reinitializes only when authority truly changes.

---

### V2-10 — Authority UX integrated behavior

**Wave 15 Authority lane premise.**

This is a focused user-level/adversarial smoke, not a re-audit of the whole authorization engine.

Verify:

- stable role identity/key remains truthful through edit/reload;
- user-role assignment is not silently rebound to a different role by rename/display metadata;
- stale-version/conflict feedback is understandable;
- unauthorized direct request remains denied by backend even if UI is manipulated;
- no UI-only bypass exists.

Do not weaken Authority to simplify the test.

---

### V2-11 — Licensing UX integrated behavior

**Wave 15 Licensing lane premise.**

Verify the user-visible status matches backend entitlement truth:

- requested vs granted tier/capability;
- fallback/capacity state;
- Demo/no-license truth;
- session/HA entitlement where exposed;
- replacement/admission failure does not retain a stale lease/status;
- direct/tampered request cannot manufacture entitlement.

This is status/truth verification. Do not inject fake signed licenses unless a repository-controlled test license is explicitly authorized by Main.

---

### V2-12 — Fresh-install Neutral / Detach boundary

**Wave 15 FND-07 premise.**

Verify after ordinary restart/resume and relevant product transitions:

- no hidden Demo project/state reappears;
- Neutral state does not expose stale Operational Event / Client Memory / Driver / Server Memory runtime metadata;
- detach/neutral behavior does not retain false Active/runtime authority;
- first-project state remains the user's actual project, not a recovered fixture.

---

### V2-13 — HA authority boundary — bounded ENV_A scope

**Wave 15 FND-05 premise.**

ENV_A is primarily a single Product Owner-like local product environment.

Do **not** claim that Stage 2 validates HA/failover merely because ordinary Runtime works.

If the accepted local harness can instantiate the repository-supported two-independent-service handoff scenario without changing product semantics, CODEX may run the already-defined focused HA authority/lease readiness smoke.

Otherwise classify:

`NOT_APPLICABLE_IN_ENV_A / COVERED_BY_FND05_VERIFIED_FROZEN_EVIDENCE`.

No new consensus/network/failover architecture is in scope.

---

## 7. Restart and persistence loop

At least once during Stage 2, after meaningful project state exists:

1. record Working/Published/Active identities;
2. pause ENV_A;
3. stop/restart Docker engine or perform the already-accepted host-level resume boundary;
4. resume ENV_A;
5. revalidate project, authority, user identity, scripts, visual state and Runtime;
6. distinguish environment restart defects from product persistence defects.

This is separate from the ENV_A harness readiness proof: here the purpose is product-state regression after realistic use.

---

## 8. Fault-injection discipline

Before any fault:

- record exact visible state;
- record exact authority identity;
- start shared timestamp/request correlation;
- preserve evidence before recovery.

One fault at a time.

Examples allowed after Stage 2B starts:

- temporary API/proxy unavailability;
- retryable projection failure;
- controlled script timeout/failure workload;
- realtime disconnect/reconnect;
- History failure/no-data selection.

Do not combine multiple failures and then guess the cause.

Restore the controlled fault before moving to the next case.

---

## 9. Classification vocabulary

Every Stage 2 item must end as exactly one of:

- `PASS / VERIFIED_ON_ENV_A`;
- `DEFECT / GENERIC_PRODUCT`;
- `DEFECT / ENVIRONMENT_HARNESS`;
- `UNCERTAIN — BOUNDED`;
- `NOT_APPLICABLE_IN_ENV_A`;
- `BLOCKED_BY_UPSTREAM_FINDING`.

A `PASS` requires observed behavior, not source inspection alone.

A `DEFECT` requires reproduction/evidence and a bounded responsible layer.

An `UNCERTAIN — BOUNDED` must state:

- known facts;
- exclusions;
- missing observation;
- exact future capture method.

---

## 10. Evidence contract

Create a dedicated Stage 2 evidence surface separate from Stage 1.

Required summary per verification item:

- verification ID;
- exact product SHA;
- exact harness SHA;
- Stage 2 session/checkpoint ID;
- precondition;
- user-visible steps;
- expected contract;
- observed result;
- timestamps;
- screenshots/video where useful;
- browser console/network where relevant;
- API/log/diagnostic correlation only after visible capture;
- classification;
- whether state was restored;
- exact evidence paths.

Detailed Stage 2 findings remain embargoed from the Product Owner until the independent ENV_B human journey completes.

Main may expose only coarse coordination state while embargo is active.

---

## 11. Stop conditions

Stop the current verification item and hand back to Main when any of the following occurs:

- P0/P1 product blocker prevents meaningful continuation;
- product state becomes ambiguous and cannot be restored to the Stage 2 checkpoint;
- a required fault cannot be injected without modifying product semantics;
- auth/licensing/Authority would need weakening;
- harness corrupts or loses the derived Stage 2 state;
- product SHA/harness SHA changes;
- a finding would require product correction before subsequent evidence is trustworthy.

Do not correct the product during this stage.

---

## 12. Exit criteria

Stage 2 is complete only when:

1. all applicable Wave 14 P0/P1 transferred findings have a Wave 15 ENV_A disposition;
2. A4 Trends and A5a Popup-value bounded findings are either directly resolved or remain explicitly bounded with stronger evidence;
3. Editor dependent authoring is exercised after stable selection;
4. Script Engineering completes a representative UI-discoverable compound flow;
5. Script runtime bounded recovery is exercised;
6. Runtime navigation/popup persistence is tested through transient failure and real authority change;
7. restart/resume product persistence is revalidated;
8. Authority/Licensing user-facing truth gets focused integrated smoke;
9. FND-07 no-hidden-Demo/Neutral truth is checked;
10. FND-05 is not overclaimed beyond what ENV_A can actually exercise;
11. no product correction occurred during verification;
12. evidence is durable and classified;
13. ENV_A is left in a known `PAUSED_RESUMABLE` or explicit reset state chosen by Main.

---

## 13. Required CODEX -> MAIN handoff

The final handoff must begin:

`CODEX -> MAIN COORDINATOR — ENV_A STAGE 2 DIRECTED VERIFICATION`

Include:

- exact live GitHub revalidation;
- exact product/harness SHA;
- Stage 1 sealed evidence/checkpoint reference;
- Stage 2 derived-state/session reference;
- matrix V2-01 through V2-13 with classification;
- defects and exact reproduction evidence;
- bounded uncertainties and missing capture;
- actions actually performed;
- explicit non-actions;
- any state restoration performed;
- one recommended Main action per blocking defect;
- overall recommendation limited to `STAGE2_COMPLETE`, `STAGE2_BLOCKED` or `STAGE2_INCOMPLETE`.

CODEX must not merge, freeze, correct product code or declare Wave 15 globally accepted from this Stage 2 handoff.

---

## 14. Activation rule

This document is a prepared verification contract only.

Activation requires a new explicit Main order containing:

- exact product SHA;
- exact harness SHA;
- exact completed Stage 1 session/evidence reference;
- exact Stage 2 derived checkpoint;
- allowed verification subset if Main chooses not to run the full matrix;
- embargo state.

Until that order exists:

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.


## 15. Human Preview delta — mandatory Stage 2 additions

The Product Owner Human Preview has now ended as:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = BLOCKED_BY_PRODUCT`.

Canonical human evidence:
`docs/WAVE15-FIRST-PROJECT-HUMAN-PREVIEW-FINDINGS.md`
commit `e413b3c85224cbf3f62bad6896738ec7ebe7810c`.

The CODEX findings embargo is therefore lifted for Main/CODEX cross-audit work. The following additions are mandatory in Stage 2.

### V2-14 — Fresh first-project Runtime must not leak Demo content

Human Preview observed unexpected Demo-like Runtime content after creating the first project, including a tank, pump and frequency/current-style objects, while the Product Owner could not reconcile/delete those objects from Engineering.

Independent CODEX evidence also observed a visible Runtime labeled `Demo · Estação Elevatória` while Engineering showed another project identity and zero TAGs/Data Sources.

Verify from a true fresh state:
- no hidden Demo project/content is selected as Runtime fallback;
- first project creation does not inherit unrelated Demo visuals/state;
- Working/Published/Active/Runtime project identities agree intentionally;
- Runtime content is explainable from the authoritative Active revision;
- restart/resume does not reintroduce Demo state;
- no stale Demo runtime survives Neutral/bootstrap/project creation.

Track product correction under issue #354.

### V2-15 — Data Source type selection -> TAG creation -> Runtime path

Human Preview was blocked because the Data Source `Type` field did not expose a usable/selectable list.

Verify mounted UI end-to-end:
1. fresh project;
2. open Data Source creation;
3. Type selector opens and lists supported types;
4. select one representative supported type;
5. persist/reopen Data Source;
6. create one representative TAG;
7. persist/reopen/restart;
8. bind/use the TAG through normal Engineering/Runtime flow;
9. confirm validation/readability of errors.

A source-code enum or API endpoint existing is not a pass; the mounted UI must work.

Track under issue #355.

### V2-16 — Templates authoring CRUD/discoverability

Human Preview found no discoverable mechanism to create or edit Templates.

Determine whether functionality is missing or merely unreachable, then verify through mounted UI:
- create;
- name/rename;
- edit content/properties;
- save/reopen;
- instantiate/use where supported;
- delete/archive where supported;
- contained validation/error behavior.

Track under issue #356.

### V2-17 — Editor first-user property usability

Human Preview reported:
- Property Inspector fields with light-on-light contrast/readability problems;
- text object displayed content not discoverably editable;
- generic Texto-like label remained with no obvious content/rename path;
- rectangles/basic shapes could be inserted, but intended display/fill color could not be changed reliably even after finding property-like fields.

Revalidate #303 through mounted UI, not only unit/component evidence:
- readable contrast in Properties;
- selected text object's visible content can be found and edited by a first-time user;
- basic shape fill/stroke can be changed and visibly updates immediately;
- object identity/name/content distinctions are understandable;
- direct canvas + Properties remain synchronized;
- save/reopen preserves results.

Existing #303 owns the correction; Human Preview evidence is in comment `5857988331`.

### V2-18 — Library/Dynamo visual preview acceptance

Human Preview directly confirmed that Library/Dynamo reuse remains name-driven without useful object preview.

Revalidate #308 acceptance through real UI:
- visual preview before insertion;
- representative current Library object;
- representative Dynamo;
- preview uses canonical rendering;
- selection does not mutate Working;
- explicit insertion matches preview;
- malformed asset preview fails contained/actionably.

Existing #308 owns the correction; Human Preview evidence is in comment `5857988604`.

### Updated Stage 2 entry/exit note

Stage 2 remains PREPARED / NOT ACTIVE until Main accepts a stable ENV_A harness after the ongoing repair lifecycle proof.

Once activated, the matrix is now V2-01 through V2-18.

The former Human-Preview embargo restriction no longer applies because the human journey has ended. CODEX may consume the human findings above only after Main activates Stage 2. Product correction remains forbidden during Stage 2 verification itself unless Main opens a separate correction mission.


### V2-19 — icon-first object toolbox

**Owner:** #358 / UX2 #357 / Editor #303.

Human Preview follow-up Product Owner decision: the current object toolbox made of persistent text buttons consumes too much authoring workspace and is not sufficiently intuitive.

Verify after UX2 implementation:
- default toolbox is icon-first and materially more compact than the prior text-button layout;
- icons map truthfully to the canonical object/action types;
- hover/focus exposes localized tooltip/label;
- accessible name and keyboard focus/activation work;
- selected insertion mode is visibly explicit;
- representative primitives (selection, rectangle, ellipse, text) insert the expected canonical objects;
- Escape/cancel semantics are coherent where insertion mode is modal;
- Library/Dynamo entry point opens the canonical reuse surface without implicit insertion;
- #308 visual preview remains available after entering Library/Dynamo browsing;
- toolbar layout remains usable at 1366x768, 1440x900 and 1920x1080 without harmful global page scrolling;
- pt-BR/en/es tooltip/accessible-label structure remains in parity;
- Runtime rendering and Working/Active authority are unchanged by the toolbox redesign.

Do not count an icon-only visual conversion as PASS if labels/tooltips/accessibility/discoverability regress.


### V2-20 — Security/Authority mounted UI and HTTP 402 root isolation

**Owner:** #359 with semantic ownership from #302 and remote-path dependency on #307 if applicable.

Human Preview observed some Codespace surfaces failing with HTTP `402`; Security/Authority administration was persistently unusable and the Product Owner could not create or edit users.

Stage 2 must first isolate the responsible layer before any fix acceptance.

Required capture under shared timestamps:
- visible Security navigation action;
- exact request URL(s);
- exact HTTP status;
- response headers/body;
- browser console/network error;
- same-origin proxy route if present;
- API health/log correlation;
- authenticated identity/session class;
- current license/session state.

Run the same Security route in:
1. local ENV_A;
2. a remote/Codespace-equivalent path where available.

Then verify mounted UI behavior:
- Security page loads reliably;
- users list loads;
- create one local user;
- edit permitted fields;
- enable/disable as supported;
- assign/change role/profile(s);
- save/reload/restart persistence;
- backend rejects unauthorized direct mutations;
- UI shows truthful error states instead of blank/failing surfaces.

Do not treat a numeric 402 alone as proof of licensing or Authority root cause. At triage time no explicit intentional product `402 Payment Required` path was found in repository search.


#### V2-20 diagnostic ordering update — remote latency hypothesis first

Product Owner correlation with Wave 14 elevates remote latency/forwarding to the **primary hypothesis** for #359, while preserving the bounded root classification until A/B evidence exists.

Run V2-20 in this exact order:
1. ENV_A local / normal latency;
2. ENV_A local / deterministic latency+jitter injection at HTTP/proxy layer;
3. ENV_B/Codespace remote forwarded path.

Do not begin by changing Authority semantics or licensing.

Classification after the A/B/C matrix:
- A fails -> generic product/Authority;
- A passes, B fails -> generic remote/WAN resilience (#307);
- A+B pass, C fails -> forwarding/edge/environment-specific root remains likely;
- status code `402` alone is not semantic evidence of licensing or Authority.

Read/list requests may exercise bounded retry/backoff if allowed by #307. User/role mutations must not be blindly retried after ambiguous transport outcomes.
