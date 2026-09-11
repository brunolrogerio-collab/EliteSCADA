# Wave 15 — Initial Correction Backlog

**Source:** Wave 14 Product Owner validation + post-C26 real-browser audit + diagnostic closure  
**Status:** PREPARED DURING WAVE 14 / DO NOT START WAVE 15 UNTIL WAVE 14 IS MERGED AND EXACT NEW `main` IS VALIDATED  
**Authority:** GitHub live + final Wave 14 diagnostic transfer

> This file is a transfer backlog, not permission to patch Wave 14. Every item must be revalidated against the exact new `main` when Wave 15 is formally opened.

## 1. Backlog rules

Wave 15 begins implementation from the exact validated `main` produced by Wave 14 closure. It must not reuse the Wave 14 Preview branch as its correction baseline.

Each item carries a Wave 14 closure state:

- `CONFIRMED` — sufficient evidence/mechanism exists to define correction work;
- `PARTIAL — PENDING CODEX` — symptom/product responsibility is known but the direct-Codespace diagnosis still must close the exact cause/path;
- `UNCERTAIN — BOUNDED` — no product patch until the explicitly named missing evidence reproduces a product defect;
- `REQUIREMENT / FUTURE` — preserved product work, not a defect inferred from the current audit.

Developer usability in Screen/Popup Editor and Script Engineering is a product-correctness priority, not cosmetic polish.

## 2. Initial ordered backlog

| ID | Priority | Area | Wave 14 closure state | Wave 15 objective | Dependencies |
|---|---|---|---|---|---|
| W15-P0-01 | P0/P1 | Engineering lifecycle / Working identity | PARTIAL — PENDING CODEX | Make Engineering Working bootstrap/checkout/persistence identity coherent with intended project context while preserving Published/Active authority and cross-project activation protection. | Final A2 handoff |
| W15-P1-01 | P1 | Screen/Popup visual schema compatibility | CONFIRMED | Add version-aware compatibility/migration/recovery for known persisted legacy visual types without weakening truly unknown-type validation. | None after new main |
| W15-P1-02 | P1 | Screen/Popup Editor functional maturity | PARTIAL — UI INVENTORY PENDING | Make the graphical editor practically usable for a developer; every visible supported control must work and persist through canonical Engineering. | Working/Engineering stability; legacy compatibility |
| W15-P1-03 | P1 | Script Engineering functional maturity | PARTIAL — UI FLOW PENDING | Deliver a discoverable end-to-end script authoring, validation, trigger/binding, persistence and runtime-debug workflow. | Working/Engineering stability |
| W15-P1-04 | P1 | Runtime Trends | PARTIAL — PENDING CODEX | Eliminate silent return and provide deterministic live/historical/no-data/error behavior. | A1 transport separation + A4 handoff |
| W15-P1-05 | P1 | Runtime Popup live values/navigation | PARTIAL — PENDING CODEX | Keep popup bindings/live values/navigation deterministic; fix `—` despite Good source and disappearance/return mechanism once diagnosed. | A1; A5 handoff; ideally A2 |
| W15-P1-06 | P1/P2 | Engineering recovery/error UX | CONFIRMED PRODUCT UX / TRANSPORT CAUSE PENDING | Never present fallback `Demo Project` as authoritative Working identity during model-load failure; provide truthful loading/unavailable/retry diagnostics. | A1 determines any transport-owned correction |
| W15-P2-01 | P2 | Shared responsive shell/header | CONFIRMED UI | Prevent common notebook-width overlap while preserving Runtime/Engineering navigation and account controls. | Coordinate with shell CSS work |
| W15-P2-02 | P2 | Account menu accessibility | CONFIRMED UI | Give interactive account/menu controls accessible names and regression coverage. | None after route revalidation |
| W15-P2-03 | P2 | Engineering navigation | CONFIRMED UI | Remove harmful dependence on global scroll; provide coherent collapse/independent scroll behavior. | Editor/shell layout coordination |
| W15-P2-04 | P2 | Engineering Lock footprint | CONFIRMED UI | Reduce excessive vertical footprint without weakening lock authority or diagnostics. | Shared Engineering shell |
| W15-P2-05 | P2 | Templates / Equipment / Dynamos / Libraries | CONFIRMED USABILITY | Provide useful preview/inspection so reusable assets can be selected and understood before insertion/use. | Editor maturity |
| W15-U-01 | — | Engineering transport / route latency | UNCERTAIN — BOUNDED / PENDING CODEX | Patch only product-owned layer after browser↔Vite↔API correlation identifies divergence. | A1 handoff |
| W15-U-02 | — | Alarm timestamp interpretation | UNCERTAIN — BOUNDED | Reopen only with same-occurrence comparable timestamps/authorities; keep Alarm/Operational Event/Audit distinct. | Comparable event evidence |
| W15-U-03 | — | Simulation / Server Script freeze | NOT CONFIRMED PRODUCT DEFECT | Do not patch unless freeze recurs naturally with correlated API/Vite/browser/realtime/process evidence captured before restart. | New reproduction only |

## 3. W15-P0-01 — Working identity / bootstrap / lifecycle

### Wave 14 evidence

Real product observation has shown Engineering Working as `demo` / Demo Project while Runtime remains Active on `eee-demo`, revision 2. The lifecycle correctly prevents cross-project activation.

### Wave 15 contract

- fix the origin of the wrong Working identity, not Active Runtime;
- preserve `Working -> Revision -> Published -> Active` authority;
- preserve fail-closed cross-project activation checks;
- explicitly distinguish loading/fallback UI state from a real Working project;
- cover fresh bootstrap, reopen/recovery and explicit checkout/import paths.

### Required regression

A deterministic lifecycle test must prove the intended project identity across Working, saved revision, Published and Active, and a negative test must prove cross-project activation remains blocked.

**Final root cause/path remains pending the Wave 14 direct-Codespace A2 diagnostic.**

## 4. W15-P1-01 — persisted legacy visual type compatibility

Canonical Wave 14 diagnosis:

`docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`

### Confirmed mechanism

`DynamicPropertyEditor` -> `listDynamicPropertyDestinations(element)` -> `getBuiltinVisualObjectSchema(element.type)` reaches the strict current `core.*` registry with persisted legacy identifiers and lacks a compatibility boundary.

### Correction contract

- explicit known-legacy migration/normalization table or equivalent version-aware boundary;
- normalize before strict schema consumers;
- do not make arbitrary unknown identifiers valid;
- preserve unsupported objects with actionable diagnostic rather than blanking the application or silently deleting them;
- use same behavior in Screen and Popup authoring;
- preserve canonical current type identifiers on new/updated state.

### Required regression

- Screen + known legacy type;
- Popup + known legacy type;
- representative move/resize/property or dynamic mutation;
- truly unknown negative case;
- normal current `core.*` case.

## 5. W15-P1-02 — Screen/Popup Editor developer-functional maturity

Wave 15 must produce a capability matrix from real UI behavior and then correct the failed/absent flows.

Minimum developer workflow:

- object and structure-tree selection;
- move and resize;
- copy/paste and duplicate;
- undo/redo;
- group/ungroup where product supports it;
- lock/unlock semantics;
- alignment/distribution where surfaced;
- z-order;
- zoom/pan;
- grid/snap;
- Properties access and editing;
- text/content editing;
- bindings/expressions/conditions;
- Popup bounds/composition;
- preview;
- Save -> Revision -> Published/Active flow where applicable.

Visible enabled controls that do nothing are defects. Unsupported functionality must not masquerade as enabled completed functionality.

Acceptance requires at least one representative Screen and Popup built/edited end-to-end through the product UI without repository, DB or manual API intervention.

## 6. W15-P1-03 — Script Engineering developer-functional maturity

Wave 14 static inspection already indicates building blocks such as project-object discovery and snippets for TAG/visual-property operations, but practical discoverability/composition was not proven.

Wave 15 minimum bar:

- create/open/edit a Python script in the product;
- discover TAGs, screens, popups, objects, properties, Client Memory and permitted APIs;
- useful autocomplete/snippets where practical;
- understandable syntax/semantic diagnostics with line/column context;
- associate the script with intended scope/event/trigger;
- validate/test within sandbox boundaries;
- persist through canonical Engineering/revision/package semantics;
- publish/activate where applicable;
- observe runtime execution and failure diagnostics;
- complete a representative compound task such as reading/comparing TAG values and conditionally writing an allowed visual property, without manual DB/API/repository work.

Security/capability boundaries remain backend-authoritative; scripts never gain stronger authority than the logged-in principal.

## 7. W15-P1-04 — Runtime Trends

Observed symptom: entering Trends can show a live-connection state and then silently return to the operational screen.

Wave 15 must implement only after the Wave 14 A4 handoff identifies the responsible Historian/realtime/projection/navigation/error-recovery layer.

Required regression set should include:

- live data available;
- historical data available;
- valid zero/no-data state;
- temporary realtime/historian unavailability;
- navigation state retained with an actionable error instead of silent route loss.

## 8. W15-P1-05 — Runtime Popup live values/navigation

Observed symptom: Popup may display `—` while the main card/TAG monitor has numeric Good data, and may disappear/return unexpectedly.

Implementation waits for Wave 14 A5 correlated diagnosis.

Regression should cover:

- numeric zero vs missing value;
- Good/Bad/unavailable quality;
- stopped/running process states;
- realtime reconnect;
- opening/closing/reopening popup;
- underlying screen remains stable;
- popup selection/navigation survives unrelated projection polling where authority remains valid.

## 9. W15-P1-06 — Engineering recovery and fallback identity

Even if the initial request failure is caused by Codespaces/proxy/environment, product-owned UX is already bounded:

- missing public model must not be represented as a real authoritative `Demo Project` Working state;
- loading, unavailable and retry states must be explicit;
- dependent modules must clearly state why they are unavailable;
- recovery must not imply project/lifecycle changes that did not occur.

A1 decides whether an additional transport/client-fetch correction belongs to the product.

## 10. Deterministic P2 backlog

### W15-P2-01 — responsive header

Correct overlap at normal notebook/common widths. Regression across representative Runtime and Engineering widths.

### W15-P2-02 — account accessibility

All account/menu interactive controls require programmatically determinable accessible names and keyboard-operable behavior.

### W15-P2-03 — Engineering navigation

Long navigation must not depend on scrolling the whole Engineering page in a way that hides/repositions unrelated workspace. Coordinate independent panel scrolling/collapse with editor canvas priority.

### W15-P2-04 — Engineering Lock footprint

Compact the lock surface while preserving state, purpose, diagnostics and security authority.

### W15-P2-05 — resource previews

Templates/Equipment/Dynamos/Libraries need useful preview/metadata/inspection before selection or insertion; no opaque list-only developer flow where a visual/resource preview is materially needed.

## 11. Bounded uncertain/evidence-only items

### W15-U-01 — transport/route latency

No product performance patch until same-window evidence identifies where latency/failure first appears among forwarded browser, local Vite, local API, auth/proxy and process state. Local API/Vite millisecond response evidence means public-path delay alone is insufficient to assign product cause.

### W15-U-02 — Alarm timestamp

Do not compare timestamps from different authorities/occurrences as if they were one event. Reopen with same occurrence identity and explicit server/client/timezone semantics.

### W15-U-03 — simulator freeze

Later local authenticated evidence showed dynamic TAG values while public forwarding failed. Existing #296 did not provide an effective long-run product observation. No blind rerun and no correction without a new natural reproduction.

## 12. Preserved future requirements — not audit defects

These requirements remain important but must not be mislabeled as confirmed Wave 14 defects:

- protected whole-system backup/restore distinct from `.escadapkg`;
- Historian Administration backup/export/import/restore;
- generic TAG raw -> engineering scaling with explicit inverse-write semantics;
- human decimal-place authoring persisted through Save/Revision/Publish/Activate/Runtime/package;
- real EEE Modbus/PLC variant only after generic product maturity gates;
- later Wave 13 signed Windows x64/AuthentiCode work after explicit Product Owner maturity decision.

## 13. Wave 15 opening gate

This backlog becomes executable only after all are true:

1. remaining Wave 14 material diagnostics are `CONFIRMED` or `UNCERTAIN — BOUNDED`;
2. final Wave 14 diagnostic transfer is preserved in GitHub;
3. canonical C11 is integrated through PR #263 into `wave14/corrections-integration`;
4. final closure documentation is propagated without merging the Wave 14 Preview as a product route;
5. exact integration SHA is green on universal and impact-required gates;
6. PR #212 is merged to `main` through its conditional Product Owner authorization and expected-head protection;
7. exact new `main` is validated;
8. Wave 14 surfaces are closed/preserved appropriately;
9. a Wave 15 branch and coordinator issue are created from that exact validated `main`.

Wave 13 remains paused unless the Product Owner separately resumes it.

## 14. Initial execution order when Wave 15 formally opens

Recommended dependency-aware order:

1. W15-P0-01 Working/bootstrap identity if the final A2 diagnosis confirms a product defect needing first correction;
2. W15-P1-01 legacy visual compatibility;
3. W15-P1-02 Screen/Popup Editor functional maturity;
4. W15-P1-03 Script Engineering functional maturity, parallel where it does not collide with Working lifecycle/editor shared state;
5. W15-P1-04 Trends and W15-P1-05 Popup runtime behavior after their direct-Codespace diagnoses are complete;
6. W15-P1-06 Engineering recovery/fallback UX and any product-owned A1 correction;
7. deterministic P2 shell/navigation/accessibility/resource-preview backlog;
8. integrated exact-SHA validation;
9. fresh Wave 15 Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log reading -> targeted follow-up corrections/recheck -> Product Owner maturity decision.
