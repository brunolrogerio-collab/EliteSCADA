# Wave 14 C26 — Coordinator Handoff — 2026-09-07

**Prepared:** 2026-09-07 BRT  
**Status:** **C26 ACTIVE / C26.1–C26.3 IMPLEMENTED / C26.4 DIAGNOSED BUT NOT IMPLEMENTED / C26.5–C26.10 PENDING / #212 NOT AUTHORIZED / #266 AND #288 NEVER MERGE / WAVE13 PAUSED**

> **GitHub live is the official and sole authority.** Revalidate every branch head, PR state, issue state and exact-SHA workflow before making a decision or mutation. If this file differs from live GitHub, GitHub wins.
>
> This handoff is documentation only. The exact C26 **product/test authority immediately before this handoff** is `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`. A later docs-only handoff SHA must not be mistaken for a newly validated product candidate.

## 1. Repository, routes and permanent guards

Repository:

`brunolrogerio-collab/EliteSCADA`

C26 branch:

`wave14/c26-po-homologation-corrections`

C26 implementation PR:

#287 -> `wave14/c11-canonical-eee-demo` — **OPEN / DRAFT**

C26 exact-head validation PR:

#288 -> `main` — **VALIDATION ONLY / OPEN / DRAFT / MUST NEVER MERGE**

Canonical C11 branch:

`wave14/c11-canonical-eee-demo`

C11 implementation PR:

#263 -> `wave14/corrections-integration` — **OPEN / DRAFT**

Historical C11 validation-only PR:

#266 -> `main` — **MUST NEVER MERGE**

Wave14 integration branch:

`wave14/corrections-integration`

Wave14 integration PR:

#212 -> `main` — **OPEN / DRAFT / NO MERGE AUTHORIZATION**

Pre-C26 Preview:

#285 / `preview/wave14-c11-canonical-preview` — preserve as pre-C26 Product Owner homologation evidence. Do not reuse it as the post-C26 Preview.

Permanent rules:

- #212 must not merge to `main` without a later, separate and explicit Product Owner authorization.
- `siga`, C26 completion, green CI, mergeability, C11 acceptance, package freeze or Preview success do not authorize #212.
- #266 and #288 are validation-only surfaces and **must never merge**.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun. Never blind-rerun an unchanged failure.
- Never weaken tests, validation, authentication, authorization, Identity, Engineering Lock, licensing, lifecycle, package semantics, backend Active Revision Runtime authority, Drivers or security/distribution boundaries to obtain green.
- Runtime/Active remains self-contained and must not depend on `.escadalib`.
- Alarm, Operational Event and Audit remain separate concepts.
- No generic platform defect may be hidden behind an EEE-specific workaround.
- Wave13 issue #205 / PR #207 remains paused until the separately authorized Wave14 -> new `main` sequence is complete and validated.

## 2. Live authority at this handoff

Immediately before this docs-only handoff:

- C26 product/test head: `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`
- canonical C11 frozen head: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`
- accepted/green Wave14 integration head: `ff185ffd67fe4abc597af9184c21f86376ba6e17`
- preserved pre-C26 Preview head: `d92e81f821c1a9c376b39bc3684eead54b3f570e`
- current `main` base seen by validation PRs: `edbdf446ea657713bdc487be91bf10bfcd03c684`

Frozen canonical C11 application package:

`preview/fixtures/EliteSCADA-EEE-Demo.escadapkg`

SHA-256:

`4be1ca2338094799a8bf3c989322e5488a2381ce65d92cac3871188639e215c6`

The frozen package is self-contained, contains no `.escadalib` dependency, and its checksum/provenance are committed. C11 freeze validation was 6/6 SUCCESS before real Product Owner Codespace homologation exposed the C26 usability/product defects.

## 3. Why C26 exists

The pre-C26 Preview #285 was technically reproducible and green, but real Product Owner Codespace/browser homologation exposed operator and Engineering usability/product defects that CI had not captured adequately.

Coordinator issue:

#286 — `W14-C26 — PO Homologation Runtime & Engineering Corrections`

The C26 branch must correct generic product behavior first. Canonical EEE cleanup is allowed only after generic fixes, and only for residual defects that are genuinely application-authoring defects.

## 4. C26 progress already implemented

### C26.1 — Runtime transient-failure resilience — IMPLEMENTED

Commit:

`2cfdee7b943e35d8ad04ba77e20b89da93a48113`

`fix(w14-c26): preserve Runtime across transient projection transport faults`

Behavior:

- transport/no-response failure is distinguished from authoritative HTTP failure;
- last successfully loaded Active Runtime projection is retained across a transient transport fault;
- current selected Runtime screen remains mounted through the transient failure and recovery;
- retry/polling continues;
- initial load with no valid projection still blocks;
- an authoritative HTTP conflict still invalidates the stale mounted projection;
- backend persisted Active Revision remains authority.

Regression:

`web/scada-web/tests-e2e/wave-14-c26-runtime-resilience.spec.ts`

### C26.2 — Runtime logical viewport/layout — IMPLEMENTED

Commits:

`a918ed4dcca2a6f5fa2657d0c7108508ead91543`

`fix(w14-c26): fill Runtime logical viewport without editor scroll chrome`

and corrective scope refinement:

`3e312bd29a8d9109a9ec354e7f579d9bbc4aef3d`

`fix(w14-c26): scope viewport chrome reset to Runtime Screens`

Behavior:

- logical 1920×1080 Screen fits the available Runtime area while preserving aspect ratio;
- avoidable internal scrollbars/editor chrome are removed from Runtime Screen rendering;
- normal-shell and fullscreen layout are covered;
- the CSS reset was deliberately scoped to `.runtime-visual-screen`, not globally to Popup rendering.

Regression:

`web/scada-web/tests-e2e/wave-14-c26-runtime-viewport.spec.ts`

### C26.3 — Runtime technical-id leakage — IMPLEMENTED

Commits:

`fdc7ff2f065797138aada4831e986957a1843223`

`fix(w14-c26): suppress technical fallback ids in Runtime`

followed by the identity-preserving correction:

`b7f0566f9e30f3e4c6dee3d020710b23923b6db2`

`fix(w14-c26): suppress Runtime fallback text without mutating visual keys`

Final contract:

- Engineering/DOM/debug identities remain intact;
- Runtime operator rendering suppresses fallback technical key/type text when no visible text was explicitly authored;
- visual keys are **not mutated** merely to hide operator-visible text;
- canonical renderer keeps Engineering behavior by default and Runtime explicitly disables technical fallback text.

## 5. Exact-head validation at `b7f0566...`

Five normal `main`-scoped gates were naturally triggered through validation-only PR #288.

SUCCESS:

- EliteSCADA CI #1433 / run `34154058083`
- Preview Licensing CI #381 / run `34154058077`
- Interop Lab Smoke #258 / run `34154058109`
- L3 Seven-Driver Lab #337 / run `34154058154`

FAILURE:

- Wave 11 Active HMI Runtime #359 / run `34154058296`

The failure is diagnosed. **Do not rerun unchanged.**

Wave11 #359 reached the canonical C11 EEE HMI test after 17 preceding tests passed. The failure is a downstream timeout after the HMI flow becomes blocked by the Popup rendering defect described below. The uploaded Wave11 Playwright report artifact is `playwright-report-wave11`, artifact id `10030441924`.

## 6. C26.4 — Runtime Popup layout — DIAGNOSED / NOT IMPLEMENTED

This is the exact resume point.

The C11 canonical Popup is present in the DOM, but the real rendered Popup is clipped/misaligned and can become effectively ghosted while the underlying Screen still occupies/draws through the interaction region.

The important diagnosis from the Playwright trace is **not merely "z-index is wrong"**.

C26.2 intentionally reset Engineering renderer chrome only under:

`.runtime-visual-screen > .runtime-visual-definition`

Therefore Runtime Popup rendering still inherits Engineering editor renderer behavior, including approximately:

- `min-height: 450px`
- `margin: 18px`
- `overflow: auto`

The Popup renderer consequently develops internal scrolling/offset behavior (`scrollLeft` / `scrollTop`) and does not have a deterministic Runtime popup box. The base Screen and Popup can then compete visually/interactively in the same region.

The canonical EEE authored Popup itself already carries enough geometry to derive a Runtime box. The current pump Popup contains an authored panel envelope approximately 880×600 at `(0,0)` and is opened around logical position `(520,210)`. There is no justification for inventing a C11-only persisted `width`/`height` field merely to make this test pass.

### Binding implementation direction for C26.4

Implement a **generic Runtime Popup layout contract**, not an EEE workaround:

1. derive deterministic logical Popup bounds from authored Popup content/envelope using generic visual geometry;
2. preserve the existing Popup logical-position contract/helper (`resolvePopupLogicalPosition`) and extend it with a generic bounds calculation rather than replacing placement semantics arbitrarily;
3. apply a Popup-specific Runtime renderer reset so Engineering editor `min-height`, margin, overflow/grid/chrome cannot leak into the operator Popup;
4. give the Popup overlay an explicit stacking context/z-order sufficient to sit above the Screen without absurd magic values;
5. opening/closing a Popup must not resize, scroll or corrupt the underlying Runtime Screen viewport;
6. Popup scaling/placement must remain tied to the same logical Runtime coordinate system and remain deterministic in normal-shell/fullscreen conditions;
7. preserve authored object identity, bindings, Commands and Active Revision authority;
8. add regression coverage for at least one canonical EEE Popup, proving it opens visibly, its authored controls/content are reachable, it has no unintended internal editor scrollbars/clipping, and the underlying Screen layout remains unchanged after open/close.

Do **not** solve C26.4 with only `z-index: 999999`, arbitrary fixed browser-pixel dimensions, EEE-specific selectors, removal of legitimate controls, or weaker Playwright assertions.

No C26.4 product code has been committed yet. The branch product/test authority is still `b7f0566...`.

## 7. Remaining C26 work after C26.4

### C26.5 — Alarm/Event/Historian Runtime surface — NOT STARTED

Real Preview observation included:

`Historical query failed with HTTP 404`

Required:

- diagnose whether the cause is route/API contract, Runtime surface, Preview proxy/harness, or another concrete integration seam;
- do not turn a real backend/route failure into a fake empty-data state;
- legitimate no-data must render as a clean no-data state, not raw transport text;
- preserve Alarm / Operational Event / Audit separation and Historian authority.

### C26.6 — Engineering shell workspace usability — NOT STARTED

Required:

- collapsible/expandable Engineering navigation panels;
- independent scrolling for long panels where appropriate;
- Screen editor canvas remains the dominant work area;
- Properties remains reachable;
- preserve capability/authority projection.

### C26.7 — Engineering theme/contrast — NOT STARTED

Required:

- readable foreground/background tokens for editable, readonly, disabled and placeholder states;
- fix white/light fields with light text and ambiguous disabled/readonly states;
- regression coverage on affected Screen and Script Engineering surfaces.

### C26.8 — Screen editor basic authoring functionality — NOT STARTED

Audit and classify, then fix or make unsupported behavior explicit:

- object selection and structure-tree selection;
- move/resize;
- copy/paste;
- undo/redo;
- group/ungroup;
- lock;
- alignment/distribution;
- z-order;
- zoom/pan;
- grid/snap;
- Properties access/edit;
- text editing;
- bindings;
- preview.

Visible enabled controls must actually perform their documented action. Unsupported actions must not masquerade as working controls.

### C26.9 — Popup editor basic authoring — NOT STARTED

Required:

- visible Popup bounds during Engineering authoring;
- select/move/resize/Properties flow;
- Preview composition agrees with Runtime semantics.

### C26.10 — Canonical EEE cleanup — NOT STARTED

Only after generic C26 fixes, correct residual project-specific EEE authoring defects. Never use EEE-specific project changes to conceal a generic platform deficiency.

### C26.11 — repackage and NEW Preview — PENDING AFTER PRODUCT CORRECTIONS

After accepted exact-SHA C26 validation:

1. integrate C26 only into canonical C11 through #287 as explicitly directed;
2. regenerate canonical `.escadapkg`;
3. regenerate checksum and provenance bound to exact accepted heads;
4. prove package self-containment and portability;
5. create a **new** post-C26 Preview branch/PR;
6. preserve #285 as pre-C26 evidence;
7. run exact-head Preview CI;
8. perform a fresh real Codespace/Product Owner homologation;
9. diagnose/correct/revalidate any new findings.

## 8. Binding Product Owner backlog discussed before/during C11 that is NOT being executed by C26.1–C26.10

These requirements remain **binding**. They must not disappear merely because C26 is focused on the current homologation defects.

They are **not permission to mix unrelated changes into the current C26.4 commit**. Keep them as explicit post-C26/follow-on product gates in the overall Wave14 sequence.

### 8.1 Whole-system backup / restore

`.escadapkg` is an application/project package, not a disaster-recovery image.

EliteSCADA still requires a separate protected Administration/system recovery capability covering the appropriate persistent system state, including application/revision state, local identity/roles where applicable, Historian and other stores, host configuration/secrets handling, and explicit licensing/trust/machine-bound exclusions.

Security/licensing boundaries must remain fail-closed and explicit.

### 8.2 Historian administration

Historian samples remain outside `.escadapkg`.

Provide/audit a safe Administration workflow for Historian backup/export and import/restore with project/TAG identity compatibility and validation. Do not rely on blind database copying.

This requirement is separate from C26.5's immediate Runtime `HTTP 404` diagnosis.

### 8.3 Generic TAG raw -> engineering scaling

Confirmed generic product gap, required before the real PLC/Modbus EEE variant and before final homologation of that workflow.

Example:

- raw Modbus register value `100`
- engineering value `1.00 m`

Need first-class TAG-level scaling so HMI, Alarm, Historian and Trend consume one canonical engineering value.

Write semantics require explicit inverse transformation and must fail closed when inversion is invalid/undefined.

Do not implement scaling as EEE-only Script logic or repeated Screen expressions.

### 8.4 Decimal-place human authoring

Confirmed generic presentation/Engineering gap and distinct from scaling.

Engineering must let a human configure numeric display precision and preserve it through:

`Save -> Publish -> Activate -> Runtime -> package export/import round-trip`

Examples include `1.00 m`, one decimal for current/frequency, zero decimals for counters.

Formatting changes presentation; scaling changes the canonical engineering value. Do not conflate them.

### 8.5 Real Modbus/PLC EEE variant

Do not build the real PLC-backed EEE variant yet.

The later variant must reuse generic product mechanisms and the real EEE Modbus address mapping already recorded in:

`docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`

It comes only after:

- current canonical Simulation/C26 corrections;
- accepted exact-SHA C26 product;
- regenerated canonical package;
- new Preview and fresh Product Owner homologation;
- required generic scaling/precision/system-recovery/Historian-administration gates are resolved in the coordinator sequence.

## 9. Overall Wave14 sequence that must survive this chat transfer

1. finish C26.4 generic Runtime Popup layout;
2. exact-head validate naturally through #288; diagnose every red before rerun;
3. execute C26.5 through C26.10 in order, keeping generic defects generic;
4. run exact-SHA full C26 validation;
5. only after acceptance, integrate #287 into canonical C11, never directly to `main`;
6. regenerate/freeze canonical package, checksum and provenance;
7. create a new post-C26 Preview, not #285;
8. run fresh real Codespace/Product Owner homologation;
9. preserve and execute the binding non-C26 product gates: whole-system recovery, Historian administration, TAG scaling and decimal-place authoring, according to dependencies discovered live;
10. exact-SHA validate generic follow-on corrections;
11. only later build/validate the real Modbus/PLC EEE variant;
12. obtain final Wave14 Product Owner acceptance;
13. #212 may merge to `main` only after a **later separate explicit Product Owner authorization**;
14. validate the exact resulting new `main`;
15. only then resume Wave13 release/signing.

## 10. First action for the next coordinator

Before changing code:

1. read this file;
2. read live issue #286;
3. read live PRs #287 and #288;
4. revalidate C26 branch head and workflows;
5. confirm that the latest non-doc product/test authority is still `b7f0566...` unless GitHub has advanced;
6. inspect `RuntimeVisualNavigator`, Runtime Popup CSS, `RuntimeVisualDefinitionRenderer`, current popup-position helper/tests, and canonical C11 Popup geometry;
7. implement only the generic C26.4 Popup box/stacking contract above;
8. commit atomically to the C26 branch;
9. let #288 trigger natural validation;
10. diagnose any red before further correction or rerun.

Do not ask the Product Owner to reconstruct decisions already recorded here. The entire purpose of this handoff is to stop making a human carry repository state in a chat window.
