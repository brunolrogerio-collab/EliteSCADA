# Wave 14 Diagnostic Closure and Wave 15 Transfer

**Decision date:** 2026-09-10 BRT

> GitHub live is the official memory and sole authority. Revalidate the relevant live issue, PR, branch, SHA and CI before every decision, write, rerun, integration or merge.

## Product Owner strategic adjustment

Wave 14 has grown beyond a useful execution boundary. Its remaining objective is therefore narrowed deliberately.

**Wave 14 will close as a diagnostic, classification and transfer wave.**

The remaining Wave 14 work is to finish diagnosis of the observed product failures, identify **what is wrong, where the responsible layer/code path is, why it fails, and how it should be corrected**, and preserve that information in GitHub with a deterministic regression contract.

Wave 14 must not continue expanding into a broad correction wave. Existing accepted Wave 14/C26 product work remains valid, but newly diagnosed corrections are transferred to Wave 15 unless a future Product Owner decision explicitly changes this boundary.

After the Wave 14 diagnostic package is complete and the accepted Wave 14 baseline is integrated through the authorized route, Wave 14 will be merged to `main` and closed. Wave 15 will then be opened from the exact new `main` and will own the correction work.

Wave 13 remains preserved and paused. It will resume only when the SCADA is materially more mature and the Product Owner explicitly decides that release/signing work is again worthwhile; completion of Wave 15 alone does not automatically resume Wave 13.

## Wave 14 closure objective

For every material open finding that is intended for Wave 15, Wave 14 must preserve at least:

1. stable finding identity/title and severity;
2. current reproduction status and exact evidence authority;
3. classification (`GENERIC PRODUCT`, `EEE-SPECIFIC`, infrastructure/environment, or explicitly unresolved when evidence truly cannot decide);
4. responsible subsystem/layer;
5. concrete code path, component, API/lifecycle path, schema contract or runtime projection involved, when determinable;
6. causal mechanism, not merely the visible symptom;
7. proposed generic correction contract;
8. deterministic regression/acceptance proof required in Wave 15;
9. dependencies and ordering constraints;
10. explicit non-actions and protected architectural boundaries.

The goal is that a Wave 15 implementer can start from the diagnostic package without repeating the exploratory audit merely to discover where to work.

## Immediate Wave 14 diagnostic lanes

### A — Engineering transport / `Failed to fetch` / route latency

Finish the same-window correlation between forwarded browser, local Vite `5173`, API `5080`, authentication/proxy/forwarding, process health and logs. Distinguish environment/forwarding latency from product recovery behavior. Do not patch uncertain transport symptoms during Wave 14.

### B — Engineering Working `demo` vs Runtime Active `eee-demo`

Finish bootstrap/checkout/persistence/public-model diagnosis. Preserve that backend Active Runtime authority is currently doing the correct thing when it rejects cross-project activation. Identify the exact origin that leaves Engineering Working on the wrong project; correction moves to Wave 15.

### C — persisted legacy visual schema crash

Complete the generic compatibility diagnosis for persisted legacy visual types such as `tank`, `value` and `dynamo`. Record the exact lookup/migration/recovery path and the required distinction between known legacy identifiers and truly unknown visual types. Required future regression: Screen + Popup + negative unknown-type case. No Wave 14 product patch.

### D — Runtime Trends silent return

Identify whether the failure is in Historian/no-data handling, realtime subscription, projection, route/subview state or error recovery. Preserve explicit future data/no-data/error regressions. No silent return is acceptable as final product behavior.

### E — Runtime Popup value mismatch / auto-return

Identify binding/realtime/projection/navigation cause for popup `—` while authoritative TAG displays remain numeric/Good, and for popup disappearance/return. Preserve future stopped/running, zero/missing, quality and navigation-state regressions.

### F — Engineering recovery UX and fallback identity

Separate infrastructure transport cause from product UX responsibility. Even when a fetch failure originates outside the product, the Engineering shell must not present a fictitious authoritative Working identity. Record the correction contract for Wave 15.

### G — deterministic UI/authoring backlog

Preserve known deterministic issues with code/component location and regression contract: shared responsive header overlap, account-menu accessible names, Engineering navigation/scrolling, Engineering Lock footprint, resource previews, residual theme/state issues, TAG/source identity coherence and remaining accessible-name defects.

### H — Script Engineering / PO-PRE-07 developer usability

Wave 14 must finish diagnosis of what prevents Script Engineering from being a practical developer surface. Static existence of APIs/snippets is not sufficient. Record the gap between available implementation and a discoverable end-to-end authoring flow, including object/TAG discovery, editing, validation/errors, insertion/binding, persistence/lifecycle and runtime observation/debugging.

## Developer-functional product bar carried into Wave 15

The Product Owner explicitly considers Screen Editor and Script Engineering currently too far from functional for a developer. Wave 15 must therefore treat developer usability as product correctness, not cosmetic polish.

### Screen/Popup Editor minimum functional bar

A developer must be able to complete representative authoring through the product UI without repository, database or manual API intervention. At minimum, supported visible controls must actually work for selection/tree selection, move/resize, copy/paste, undo/redo, grouping where supported, lock, alignment/distribution where supported, z-order, zoom/pan, grid/snap, Properties, text/content editing, bindings and preview. Unsupported actions must not masquerade as enabled functional controls. Persisted legacy objects must degrade or migrate safely rather than blanking the application.

### Script Engineering minimum functional bar

A developer must be able to discover project objects/TAGs, author a representative script, validate it with understandable diagnostics, connect it to the intended trigger/binding/lifecycle, save/publish/activate as applicable, and observe/debug the resulting behavior through the product. Documentation snippets or hidden capabilities do not satisfy this bar if a normal developer cannot discover and complete the workflow in the UI.

## Integration and closure route

Current live topology must be revalidated again before execution, but the intended route is:

1. finish the Wave 14 diagnostic package and transfer table;
2. stop opening Wave 14 product-correction branches for newly diagnosed findings;
3. integrate canonical C11 through PR #263 into `wave14/corrections-integration` when exact live state permits;
4. propagate the final Wave 14 diagnostic/roadmap/handoff documentation into the integration branch through an explicit non-Preview documentation route;
5. validate the exact integration SHA with the universal and impact-required gates; diagnose red before rerun;
6. use PR #212 as the only Wave 14 route to `main`;
7. the Product Owner decision recorded on 2026-09-10 authorizes #212 -> `main` **after** the diagnostic-closure package is complete, the intended Wave 14 content is present on the integration head, and required exact-SHA validation is green. This is not authorization to merge #212 early or to bypass validation;
8. after the merge, validate the exact new `main`;
9. close Wave 14 coordination/audit issues and close obsolete Preview/validation/diagnostic PRs without merging any PR marked validation-only or `MUST NEVER MERGE`;
10. do not delete preserved branches/evidence as part of cleanup unless separately authorized.

PR #290 remains Preview-only and is not a route to `main`. PR #296 remains diagnostic-only / `MUST NEVER MERGE`. Validation-only PRs remain non-mergeable by policy even when Wave 14 closes.

## Wave 15 opening contract

Only after the Wave 14 `main` merge and post-merge validation:

1. create a Wave 15 coordinator issue;
2. create the Wave 15 integration/correction branch from the exact validated new `main`;
3. import the Wave 14 diagnostic transfer table as Wave 15's initial correction backlog;
4. execute corrections in dependency order with minimal generic fixes and deterministic regressions;
5. prioritize developer-functional Screen/Popup Editor and Script Engineering, together with confirmed P1 lifecycle/identity/runtime defects;
6. keep uncertain/environment-only findings out of product patches until technically reproduced as product defects;
7. declare one exact integrated Wave 15 product SHA only after combined regressions are green.

## Wave 15 validation endgame

After Wave 15 corrections are integrated and exact-SHA green:

`Wave 15 corrections -> fresh Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log reading -> targeted correction/recheck if necessary -> Product Owner maturity decision`

The fresh Preview must be generated from the new Wave 15 corrected baseline; the existing Wave 14 Preview is evidence and must not be repurposed as the Wave 15 final validation candidate.

Wave 13 stays paused after this sequence unless the Product Owner separately decides that EliteSCADA has reached sufficient product maturity for signed Windows release work to resume.

## Permanent boundaries

- no direct mutation of `main`; use the authorized PR route;
- no force push, destructive rebase or branch deletion as cleanup;
- no blind workflow rerun;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or Drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct authorities;
- no EEE-specific workaround for a generic product defect;
- diagnostic closure is not permission to label an uncertain finding as solved;
- Wave 15 correction completion does not automatically authorize Wave 13 resumption.
