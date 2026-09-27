# Wave 15 — Product Owner Human First-Project Preview Findings

**State:** HUMAN_PREVIEW = BLOCKED_BY_PRODUCT  
**Date:** 2026-09-27  
**Environment:** ENV_B fresh GitHub Codespace  
**Product base:** `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`  
**Harness / Codespace branch point:** `bb451fa6e07982ac12384895f6097d5833761d16`  
**Human starting boundary:** first Administrator already created only as readiness continuity marker; no project existed.

This document records the Product Owner's unaided first-project journey before any cross-audit guidance was provided.

## Overall disposition

The Human Preview is:

`BLOCKED_BY_PRODUCT`.

The interface is materially improved relative to prior Waves, but the Product Owner could not create an effective first SCADA Runtime application through the normal product path.

The blocker is not one single cosmetic issue. The journey exposed multiple product correctness/usability gaps that prevent a truthful fresh-install first-project success.

---

## H-01 — Fresh project Runtime contains unexpected Demo content

### Observed

After creating the first project and opening Runtime, the Runtime displayed content associated with the earlier Demo application, including:

- a tank;
- a pump;
- screen objects/indicators showing frequency/current-like values.

The Product Owner did not intentionally import or create these objects.

In Engineering, the Product Owner could not find the corresponding objects/state in a way that allowed deleting or reconciling them.

### Impact

A fresh first project is not presenting a trustworthy empty/user-owned Runtime state.

This undermines project/runtime authority because Runtime visibly contains objects that the user cannot account for from Engineering.

### Cross-audit evidence

Independent CODEX Stage 1 evidence, previously embargoed, also recorded a later visible Runtime state labeled:

`Demo · Estação Elevatória`

while Engineering showed a different project identity and no TAGs/Data Sources.

This convergence upgrades the hidden-Demo observation from a single-session report to a cross-audit product finding requiring correction/revalidation.

### Classification

`DEFECT / GENERIC_PRODUCT / FRESH_INSTALL_RUNTIME_DEMO_LEAK`

Priority recommendation: **P0/P1 gate blocker for fresh-project Preview**, because project/runtime authority is not trustworthy.

---

## H-02 — Data Source type cannot be selected

### Observed

While attempting to create a Data Source, the Product Owner reached the Data Source form but the **type** field did not present a usable/selectable list.

Because no Data Source type could be selected, the Product Owner could not progress to a useful configured Data Source and therefore did not reach normal TAG creation.

### Impact

This directly blocks the normal path:

`Project -> Data Source -> TAG -> bindings/Runtime`.

The first-project journey cannot produce a functional data-backed Runtime if Data Source creation cannot be completed.

### Cross-audit evidence

CODEX's independent follow-up also recorded that Data Sources/TAGs were visible as enabled Engineering controls but did not successfully advance the visible Engineering section during its attempt.

CODEX did not isolate the exact same dropdown failure, so the cross-audit evidence is supportive rather than a full independent reproduction.

### Classification

`DEFECT / GENERIC_PRODUCT / FIRST_PROJECT_BLOCKER`

Priority recommendation: **P0/P1 Preview blocker**.

---

## H-03 — Library / Dynamo objects still have no useful visual preview

### Observed

Dynamos and Library objects still do not provide useful visual previews for the objects before use/insertion.

### Impact

Object selection remains name-driven and does not meet the Wave 15 visual reuse/productization requirement.

### Existing owner

This is already explicitly required by:

`#308 W15-VISUAL-QUALITY — industrial symbol/Dynamo quality + visual Library previews before Productization`.

### Classification

`CONFIRMED_OPEN_REQUIREMENT / NOT_DELIVERED_IN_PREVIEW`

This is not a new issue; Human Preview provides direct acceptance evidence that #308 remains open.

---

## H-04 — Templates have no discoverable create/edit mechanism

### Observed

The Templates area did not expose a usable/discoverable mechanism to:

- create a template;
- edit an existing template.

### Impact

Templates are visible as a product concept but not functionally authorable through the normal Engineering workflow.

### Classification

`DEFECT / GENERIC_PRODUCT / AUTHORING_GAP`.

A dedicated correction item is required unless an existing owner can prove the intended authoring workflow exists and is discoverable.

---

## H-05 — Property Inspector contrast/readability is poor

### Observed

In the Screen Editor, multiple Property/field surfaces displayed very light backgrounds with light text, making labels/values difficult to read.

### Impact

This is an accessibility/usability defect in a primary Engineering surface and materially interferes with editing.

### Classification

`DEFECT / GENERIC_PRODUCT / EDITOR_USABILITY`.

---

## H-06 — Text object content/rename workflow is not discoverable

### Observed

The Product Owner inserted a text object but could not identify where to change the displayed text value.

The object continued to show a generic `Texto`-like label/name and no obvious rename/content-edit workflow was found.

### Impact

A basic Screen authoring operation is not discoverable enough for a first-time user.

### Existing owner relationship

#303 requires representative text/property editing to update the selected object immediately on the single canvas.

Human Preview demonstrates that this acceptance path is not satisfied from a normal user's perspective.

### Classification

`DEFECT / GENERIC_PRODUCT / EDITOR_AUTHORING_USABILITY`.

---

## H-07 — Basic shape fill/color editing is not effective/discoverable

### Observed

The Product Owner inserted rectangles and other basic shapes, found property-like fields, attempted to change display color, but could not obtain the intended visual color change.

It is uncertain whether:

1. the correct property was edited but the Editor failed to reflect it; or
2. the intended color property exists elsewhere but is not discoverable.

Either outcome fails the first-project usability contract.

### Impact

A core WYSIWYG operation — changing the appearance of a basic visual primitive — could not be completed reliably.

### Existing owner relationship

#303 explicitly requires changing fill/color/text/style through Properties and seeing the selected object update immediately on the same authoring canvas.

### Classification

`DEFECT / GENERIC_PRODUCT / EDITOR_PROPERTY_OR_DISCOVERABILITY`.

---

## H-08 — Editor improved materially but remains below first-project usability gate

### Product Owner qualitative observation

The Editor interface improved substantially compared with previous state.

However, the combined issues above mean the Product Owner still could not build an effective functional Runtime application.

The positive UI improvement is preserved as context, but it does not override the failed acceptance scenarios.

---

## Cross-audit summary after embargo lift

Human and CODEX observations are now allowed to be compared because the Human Preview reached a blocked disposition.

### Convergent

1. Fresh/project Runtime authority is suspicious:
   - Human: unexpected Demo tank/pump/frequency/current content in first-project Runtime.
   - CODEX: visible `Demo · Estação Elevatória` Runtime state while Engineering showed another project identity and zero TAGs/Data Sources.

2. Data authoring path is not healthy:
   - Human: Data Source type field not selectable, blocking TAG creation.
   - CODEX: visible Data Sources/TAG controls did not advance usefully in its follow-up.

### Human-only direct findings so far

- Template CRUD missing/not discoverable.
- Library/Dynamo preview absent.
- Property Inspector contrast/readability failure.
- Text content/rename not discoverable.
- Shape fill/color edit not effective/discoverable.

These require directed revalidation after correction and are incorporated into the Stage 2 contract/update.

---

## Human journey exit state

`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = BLOCKED_BY_PRODUCT`.

The Product Owner did **not** reach a trustworthy effective data-backed Runtime application.

The Human Preview evidence is now sufficient to:

- end the CODEX findings embargo;
- allow cross-audit comparison;
- create/return correction work;
- update Stage 2 directed verification.

It is not sufficient to declare Wave 15 Preview accepted.



## H-09 — Security/Authority administration unavailable with observed HTTP 402

### Observed

During the same real Codespace Human Preview, the Product Owner reported that some product surfaces failed to load correctly and showed an HTTP `402` condition.

The most material repeatable failure was the **Security/Authority administration surface**, which never became usable during the journey.

As a result, the Product Owner could not:
- create users;
- edit users;
- exercise the real User/Role administration workflow.

### Causal boundary

At triage time, repository search did not identify an explicit intentional product `402 Payment Required` response path.

The exact request URL, response body/headers and backend correlation were not captured during the unaided journey, so root cause remains bounded between:
- generic Authority/product defect;
- license/session composition;
- same-origin/proxy routing;
- remote Codespaces transport/timing;
- environment-only behavior.

Do not infer the root solely from the numeric status.

### Impact

Wave 15 Authority UX cannot be considered practically accepted if the real mounted Security page never loads and user administration cannot be performed.

### Classification

`DEFECT / PREVIEW_SECURITY_ADMIN_UNAVAILABLE / ROOT_LAYER_UNCERTAIN_BOUNDED`.

Priority recommendation: **P1 Preview blocker**.

### Owner

Issue #359:
`W15-PREVIEW-P1 — Security/Authority administration fails to load in Codespace with observed HTTP 402`.

It is linked to #302 Authority and #307 remote/WAN resilience until the responsible layer is isolated.
