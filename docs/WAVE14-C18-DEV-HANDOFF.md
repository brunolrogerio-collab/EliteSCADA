# W14-C18 — DEV Handoff — Embeddable Alarm + Event Browser HMI Objects

**State:** **HOLD / IMPLEMENTATION NOT AUTHORIZED**  
**Coordinator branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — DRAFT / DO NOT MERGE TO `main`  
**Package branch:** `wave14/c18-hmi-alarm-event-browsers`  
**Parked branch base:** `1dcd80a4df448ced3a228d3f5b9057fa26ef547c`  
**C11 implementation:** **LOCKED**

GitHub is the official development memory. Revalidate live refs before changing code.

## 1. Binding HOLD rule

Product Owner / Coordinator decision on 2026-09-04 BRT:

**C18 must not be released for full implementation until C12–C17 are converged on one exact combined-green integration checkpoint.**

The earlier administrative release of C18 from `1dcd80a4...` is revoked.

Therefore:

- do not begin C18 implementation while this document says HOLD;
- do not advance the C18 branch merely because it already exists;
- do not rebase onto `607a60d0...` or later coordinator/documentation commits;
- keep the existing C18 branch parked unless the Coordinator explicitly changes this state;
- a new exact authorized development base will be declared only after C12–C17 convergence;
- the parallel C18 DEV chat may retain context/read the package, but implementation authorization is not active.

Issue #211 comment `5541091621` records this binding override.

## 2. Why C18 is currently held

The last combined-green product authority before C16 is:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

It contains accepted C12+C13+C14+C15+C17 and passed all five combined gates.

C16 isolated candidate `6d9f971eb469d931ca56becff4d240088725f37a` passed its isolated gates and was composed into integration at:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

The composition is not accepted because Wave11 #266 / run `33869678407` failed in the C17 Memory authoring lifecycle while the other four gates passed.

The last green Wave11 and the red C16 composition were both validated against the same `main` base `edbdf446ea657713bdc487be91bf10bfcd03c684`, so main drift is excluded.

Artifact/trace analysis shows a real composition-level authoring-state/identity defect: sequential normal Data Source creation can reuse an existing stable GUID and replace the previous Source. This is under root-cause isolation and must be resolved before the C12–C17 convergence gate can close.

C18 is intentionally held so another large visual package is not developed against a moving or unstable composition authority.

## 3. Package purpose once released

C18 closes:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — related Historical/Browser visible chrome that remains English-only.

Normal Engineering must allow:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

No hidden package editing, DEMO-only React page, global Runtime route substitute, DOM/CSS injection, private runtime wiring or historical DEMO path counts as acceptance.

## 4. Dependencies and release gate

Functional dependencies remain:

1. C14 First-Class Operational Events;
2. C15 first-class visual-object pattern;
3. now additionally, by Product Owner coordination decision, **complete C12–C17 convergence on one exact combined-green SHA**.

C14 and C15 are individually accepted, but that is no longer sufficient to release C18 while C12–C17 composition is unstable.

The Coordinator will explicitly replace `HOLD / IMPLEMENTATION NOT AUTHORIZED` with a release marker and exact base SHA when the convergence condition is satisfied.

## 5. Mandatory reading when release occurs

Before changing code, revalidate:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. C11 consolidated audit and HMI-object clarification on `wave14/c11-pass2-product-gap-audit`;
8. `docs/WAVE14-C14-OPERATIONAL-EVENTS.md`;
9. `docs/WAVE14-C15-HMI-TREND-MULTIPEN.md`;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211 and draft PR #212.

If copied text conflicts with live GitHub, GitHub wins.

## 6. Architecture authority

When released, preserve:

- backend canonical authority and backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identities;
- Active revision as Runtime project authority;
- lifecycle `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- accepted canonical visual-object schema / Property Inspector / Runtime rendering pattern from C05/C07/C15;
- C14 Operational Event semantics distinct from Alarm and Audit;
- pt-BR / en / es for affected visible chrome.

Do not hard-code DEMO behavior or EEE-specific browsing logic.

## 7. Alarm Browser required surface

Alarm Browser must be a first-class visual object insertable into Screen and Popup, with persisted practical configuration such as current/historical view, active/returned, acknowledged state, severity, Area/Equipment/TAG, text/time filters, visible columns, sort and bounded result controls where supported by canonical contracts.

ACK/shelve/unshelve or other alarm mutations must use backend-authorized product endpoints. Client rendering never substitutes backend authorization.

## 8. Event Browser required surface

Event Browser must be a first-class Screen/Popup visual object consuming the accepted C14 Operational Event model and protected query path.

It must not reinterpret operational events as alarms merely to reuse alarm UI. Persisted filtering/presentation should support relevant C14 dimensions including type/category, source, Area/Equipment/TAG, user/operator, operation/command, time/text filters, visible columns, sort and bounded result controls.

Operational Event remains distinct from Audit history.

## 9. Common visual-object contract

Both objects require:

- canonical visual object identities;
- insertion from normal Engineering UI;
- X/Y/width/height composition through accepted visual contracts;
- persisted canonical configuration;
- schema-driven Property Inspector wherever practical;
- deterministic independent multiple instances;
- Active Runtime rendering from persisted Active revision;
- loading, empty/no-data and backend-failure states;
- no hidden JSON/package edits for normal use.

Reuse C15 infrastructure only where genuinely common. Do not copy Trend-specific semantics into browser objects.

## 10. Historical / i18n ownership

Affected visible Historical/Browser strings must exist in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted technical identifiers, TAG paths, canonical enum wire values, IDs or backend keys.

## 11. Backend/query rules

Reuse protected backend query APIs and extend them only when a real generic product capability is missing. Do not fetch unbounded history and disguise the gap with client-side filtering.

Authorization remains backend-side for protected history and alarm state-changing actions.

## 12. Explicit non-scope

C18 does not own:

- redesign of C14 Event model/storage except a narrow proven integration defect;
- C15 Trend behavior/Multi-Pen;
- C16 Operational Command, Startup/Home or Popup X/Y;
- the current C16×C17 convergence defect;
- EEE Simulation physics or DEMO process screens;
- physical Modbus PLC mapping;
- Preview/Codespaces infrastructure;
- Wave13 packaging/signing.

## 13. Acceptance after future release

Exact C18 candidate HEAD must pass:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific browser tests proving authored Screen and Popup instances.

Acceptance must prove real Save/Publish/Activate/Active Runtime lifecycle, independent configurations, canonical alarm/event data, authorized and denied alarm actions where applicable, and pt-BR/en/es chrome.

Diagnose failures before rerunning. Do not weaken tests, authorization, event/alarm semantics or visual-object contracts to manufacture green.

## 14. Delivery boundary after future release

Package PR must target `wave14/corrections-integration`, never `main`.

PR #212 remains Coordinator-owned and DRAFT.

At delivery report branch/base/candidate SHA, changed subsystems, exact workflow run IDs, architecture decisions and known limitations.

## 15. Current marker

**C18 HOLD / IMPLEMENTATION NOT AUTHORIZED**

Release prerequisite:

**C12–C17 converged on one exact combined-green integration SHA, followed by explicit Coordinator release with a newly declared exact C18 base.**

C11 remains locked until all pre-DEMO corrections converge, C10 convergence cycle 2 establishes a new exact product freeze, affected C11 findings are revalidated, and the Coordinator explicitly releases C11 implementation.
