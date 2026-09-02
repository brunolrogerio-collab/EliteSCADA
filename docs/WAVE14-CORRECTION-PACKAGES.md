# Wave 14 — First Correction Intake / Coordinated DEV Packages

**Created:** 2026-09-02 BRT  
**Authority:** Development Lead owner-validation direction  
**State:** **FIRST CORRECTION INTAKE CLOSED / IMPLEMENTATION PACKAGES READY FOR COORDINATION**

> GitHub is the development memory. Before assigning or implementing any package, re-read live `main`, issue #211, issue #208, PR #210, the relevant product files and exact CI state. SHAs in this document are checkpoints, not permission to work from stale code.

## 1. Why this plan exists

Wave 14 Product-owner validation started early because real use through the Codespaces Preview exposed material product and usability gaps before final Wave 13 signing.

The Development Lead has now completed the **first correction intake**. Rather than placing all corrections in one large branch/chat, this document partitions the work into bounded DEV packages that can be delegated to separate development agents/chats and later integrated by one coordinator.

The objectives are:

- let several independent DEVs work without treating chat history as project memory;
- minimize file/subsystem conflicts;
- preserve backend authority, security, licensing and the accepted Runtime lifecycle;
- make each package independently reviewable/testable;
- integrate corrections in dependency order;
- produce one accepted corrected Wave 14 product baseline for owner testing and later Windows packaging.

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

## 2. Current coordination snapshot

Snapshot inspected while this plan was created:

- `main`: `80cb7057cbc2656cf7b39c5d79c8a3adf8993778` before this documentation-only plan commit;
- Wave 14 tracker: issue #211 — ACTIVE EARLY;
- Preview harness: issue #208 / draft PR #210;
- Preview branch: `preview/codespaces-test-preview`;
- PR #210 head inspected: `0ab6e80c1c47a78b0bd33b07424d906b5f847faa`, open/draft/mergeable;
- latest product-code correction already validated on Preview branch: `6304144a1beab6d4f3b4cf41b95fd16b5b82ba25`;
- Wave 13 issue #205 / PR #207: PAUSED at preserved green repository-side checkpoint.

Documentation-only commits on `main` do not by themselves supersede a validated product-code baseline.

### Important branch boundary

Do **not** turn PR #210 into an unlimited Wave 14 feature bucket merely because it hosts the Preview. #208/#210 owns the Preview harness/reproducibility. #211 owns product findings/corrections.

The Wave 14 coordinator should establish or confirm a dedicated correction integration workstream from then-live `main`, incorporate the already-proven Script Engineering correction from Preview as appropriate, and give every DEV an exact base SHA. A DEV must not guess its base from an old chat.

## 3. First correction intake — package priorities

The packages below are numbered for coordination, not because every package must wait for the previous one to finish.

### W14-C01 — Identity, secure first-run bootstrap and password policy

**Priority:** P0 / validation blocker  
**Primary classification:** A/B  
**Can start immediately:** yes

Scope:

- change canonical local-password minimum from **12 to 8 characters** as explicitly directed by the Development Lead;
- keep the existing maximum and other security behavior unless a demonstrated defect requires change;
- update backend validation/error text and relevant UI hints/contracts;
- add boundary tests: 7 characters rejected, 8 accepted;
- align Preview bootstrap/preflight documentation/automation with the new 8-character product policy;
- preserve historical runbook evidence that an earlier real Codespace failed under the then-current 12-character rule; do not rewrite history;
- implement secure server-authoritative first-run detection;
- provide one-time first Administrator creation only when the installation has no valid initial administrative identity/bootstrap state;
- close anonymous bootstrap permanently after successful initial Administrator creation;
- if no project exists after authenticated bootstrap, guide the Administrator through canonical first-project creation;
- no generic anonymous self-registration;
- do not duplicate Local Identity registration/wiring in `Program.cs`;
- security remains backend-enforced and race-safe.

Acceptance includes exact-head auth/security tests, universal `EliteSCADA CI`, and clean/fresh-state Preview validation.

### W14-C02 — Backend-authoritative Driver catalog + Source/Driver configuration forms

**Priority:** P0 / foundational Engineering correction  
**Primary classification:** C with functional risk  
**Can start immediately:** yes

Scope:

- remove free-text Driver selection from the ordinary UI path;
- expose/use the actual running Driver catalog from backend/product authority;
- do not create a second independent hard-coded Driver catalog in React;
- expose canonical Driver configuration metadata/schema/capabilities needed by Engineering;
- after Driver selection, render **Driver-specific fields only**;
- provide labels, types, format help, safe defaults, examples and actionable validation;
- changing Driver type must not silently reinterpret incompatible old settings;
- Source selection/reference must use canonical identity and human-friendly labels;
- preserve backend canonical persistence.

This package owns the metadata/form foundation. Protocol-specific remote browse/import behavior is W14-C04.

### W14-C03 — DNP3 unrestricted production adapter / commercial unblock

**Priority:** P0 / commercial release blocker + technical-risk spike  
**Primary classification:** release blocker  
**Can start immediately:** yes, independently of C02

Current confirmed state:

- product adapter project: `src/Scada.Drivers.Dnp3.StepFunction`;
- its `.csproj` references Step Function package `dnp3` version `1.6.0`;
- the adapter README intentionally isolates that implementation behind the vendor-neutral EliteSCADA DNP3 master-session contract and records the present commercial/redistribution licensing restriction;
- the L3 Seven-Driver Lab DNP3 peer is **not the current product adapter**: it builds `interop-lab/dnp3-dnp3py`, cloning `craigpnnl/dnp3py` at pinned commit `8a20d4c276274f2b98800716cd7da963f21da2c1`;
- that pinned `dnp3py` source declares **MIT License**, pure Python, no project dependencies, and documents both Master and Outstation support with a Level-2 subset.

Development direction:

Investigate `dnp3py` first as the candidate for removing the DNP3 commercial-license bottleneck, because it is already part of the proven interoperability lab and its pinned source has a permissive MIT license. Do **not** assume that the lab peer can simply replace the .NET production adapter without engineering work.

Required spike/implementation evidence:

1. confirm exact license/provenance, notice obligations and distributable bytes/dependencies for the chosen version/commit;
2. map the current vendor-neutral EliteSCADA DNP3 master-session contract to the candidate implementation;
3. determine a maintainable Windows/.NET product integration model for a Python implementation without weakening runtime/process/security boundaries;
4. verify required master functionality, including connection lifecycle, polling, events/SOE, supported point groups/variations, timestamps/quality, reconnect/fault behavior and product-required writes/commands;
5. explicitly identify unsupported DNP3 features rather than silently degrading them;
6. preserve canonical TAG/Driver contracts and avoid Driver-to-Driver coupling;
7. if viable, implement a new adapter and remove Step Function from the **commercially distributed product dependency graph**;
8. retain/expand independent interoperability tests against a peer, avoiding a false test in which both sides share the same defect path where practical;
9. run DNP3-specific tests, L3 Seven-Driver Lab and universal `EliteSCADA CI` on the exact candidate SHA;
10. inspect the final Windows dependency/package manifest to prove that the restricted Step Function package is no longer part of the distributable product before declaring the commercial blocker removed.

Do not mark DNP3 commercially unblocked merely because `dnp3py` itself is MIT. Functional adequacy and actual packaged dependency closure must also be proven.

### W14-C04 — TAG Source selection + protocol-aware address/discovery assistants

**Priority:** P1  
**Primary classification:** C/B  
**Dependency:** consume stable catalog/capability contract from W14-C02

Scope:

- TAG Source selector lists/searches configured Sources rather than requiring typed identifiers;
- stable canonical Source reference survives rename where architecture supports it;
- deleted/unresolved Source is shown explicitly as invalid;
- TAG communication editor adapts to selected Source/Driver;
- address/register fields use protocol-aware typed editors, syntax help and validation;
- discovery-capable Drivers expose backend-driven discovery/browse/import capabilities;
- OPC UA normal path should support endpoint/security selection, connection test, address-space browse/search and variable multi-selection/import without requiring memorized NodeIds;
- security/trust decisions must not be silently disabled for convenience.

### W14-C05 — Canonical visual properties + schema-driven Property Inspector

**Priority:** P1 / graphical-editor foundation  
**Primary classification:** B/C  
**Can start immediately:** yes

Scope:

- Property Inspector enumerates engineering-editable properties from canonical object schemas;
- expose existing fill/background, stroke, width/style, visibility, opacity, rotation, scale, z-order, text/font/alignment and other applicable properties;
- use type-appropriate editors: color picker + canonical text entry, boolean toggle, enum selector, validated numeric controls, asset browser, font/stroke selectors, etc.;
- allow explicit transparent/no-color or disabled border/line where canonical model supports it;
- do not create UI-only visual state separate from canonical registry/schema;
- preserve property lifecycle through Save/Publish/Activate/Runtime;
- preserve generic Python visual property read/write/tween path and existing Runtime precedence;
- explicit security/structural read-only exceptions only.

### W14-C06 — Engineering Diagnostics / TAG Monitor product boundary

**Priority:** P1  
**Primary classification:** C/B  
**Can start immediately:** yes

Scope:

- move user-facing TAG Monitor/Inspector into Engineering Diagnostics/commissioning;
- remove it from normal operator Runtime navigation;
- continue observing actual **Active Runtime** values, quality, timestamps, source, access and recent history;
- clearly distinguish Active Runtime observation from Working Engineering configuration;
- preserve backend authorization;
- do not silently add TAG writes as part of the move.

### W14-C07 — Screen Engineering + Dynamo authoring/library maturity

**Priority:** P2  
**Primary classification:** C/B  
**Dependency:** W14-C05 canonical property/editor contract should be stable first

Scope:

- mature graphical Screen authoring for normal HMI engineering;
- improve selection/layout/Z-order/grouping/background workflows;
- preserve canonical project-asset handling;
- make the built-in Dynamo inventory explicit and usable at real HMI scale;
- define/document/version public interfaces and deterministic visual states for built-in Dynamos;
- bind TAGs/properties/events/actions through public interfaces rather than editing internals;
- preserve Engineering preview vs Active Runtime consistency.

### W14-C08 — Python Script Assistant / project object browser

**Priority:** P2  
**Primary classification:** C with B-level write gap to verify  
**Dependencies:** consume stable Source/TAG and visual-property contracts from W14-C04/C05 where relevant

Scope:

- make project objects/TAGs/screens/Dynamos/public members discoverable from Script Engineering;
- provide guided insertion/help for canonical Python/Pyodide APIs rather than requiring memorized identifiers/property keys;
- preserve the existing generic visual-property API instead of adding one Python method per property;
- verify safe TAG-write capability against readOnly/permissions/interlocks/backend authority;
- no direct DOM/React mutation path that bypasses Runtime property layers;
- keep Pyodide/security sandbox behavior intact.

### W14-C09 — Application shell + operator Runtime presentation

**Priority:** P2, but material for Wave 14 acceptance  
**Primary classification:** B/C  
**Start strategy:** preferably after the first integration contract freeze because this package touches broad Web surfaces

Scope:

- first-class Dark and Light application-shell themes using centralized semantic tokens and readable selection/focus states;
- shell theme must not recolor explicitly authored HMI process artwork;
- capability-pruned navigation: operation-only identity sees Runtime only while backend authorization remains the actual security boundary;
- Runtime HMI uses a fixed logical/design canvas uniformly scaled to fit viewport without document scrolling;
- preserve aspect ratio and center/letterbox as needed;
- screen navigation and Popups stay inside the same logical/scaled Runtime coordinate system;
- validate representative 1280x720, 1920x1080, 2560x1440 and 3840x2160 behavior.

### W14-C10 — Integration, regression, real Preview validation and accepted corrected baseline

**Priority:** GATE  
**Owner:** **Coordinator**, not an unconstrained feature DEV

Scope:

- integrate packages in dependency order with expected-head protection;
- resolve conflicts by preserving canonical backend/Runtime contracts, not by weakening tests;
- require universal `EliteSCADA CI` for every product-code integration head plus relevant specialized workflows;
- rerun L3 Seven-Driver Lab when Driver/TAG/DNP3 changes are present;
- rerun Runtime/Preview-specific validations when affected;
- exercise the actual product through real Codespaces browser Preview, not only CI;
- record exact tested SHA and evidence in #211;
- transfer remaining non-blocking D findings to Wave 15;
- declare one accepted corrected Wave 14 product baseline only after representative owner validation;
- future Windows installer requests must package this latest accepted/corrected product baseline using Wave 13 packaging machinery, never the stale pre-validation product snapshot.

## 4. Recommended parallel execution waves

To gain concurrency without manufacturing a merge-conflict festival:

### Stage A — start in parallel

- W14-C01 — Identity / first-run / password;
- W14-C02 — Driver catalog/forms;
- W14-C03 — DNP3 adapter investigation/replacement;
- W14-C05 — Visual properties/Inspector;
- W14-C06 — TAG Monitor/Diagnostics.

These five are substantially separable when the coordinator assigns exact file/subsystem ownership.

### Stage B — after prerequisite contracts stabilize

- W14-C04 after C02;
- W14-C07 after C05;
- W14-C08 after C04/C05 interfaces are stable enough to consume;
- W14-C09 after the coordinator freezes the common Web integration baseline to reduce broad-shell conflicts.

### Stage C — coordinator integration/acceptance

- W14-C10.

Up to nine dedicated DEV chats can therefore own C01-C09. The tenth lane should be the coordinator/integrator rather than another independent agent editing the same product surface.

## 5. Contract for every delegated DEV

Every DEV assignment must include:

- repository: `brunolrogerio-collab/EliteSCADA`;
- exact package ID and scope from this document;
- exact base SHA supplied **at assignment time** by the coordinator;
- required source/docs/issues to read before changing code;
- branch name owned by that DEV, preferably `wave14/cXX-<short-slug>` or another coordinator-approved equivalent;
- explicit files/subsystems it may own and known overlap with other packages;
- architecture/security boundaries;
- exact acceptance tests/workflows;
- requirement to post exact head SHA, changed files, test results, risks and unresolved items back to GitHub;
- prohibition on silently merging to `main` or expanding scope.

Product-code changes must not use `[skip ci]`.

## 6. Cross-package architecture rules

All packages must preserve:

- backend as canonical authority;
- backend authorization as security boundary;
- host-owned, fail-closed licensing;
- no Preview-only auth/licensing/runtime bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identities;
- accepted `Working -> saved Revision -> Published -> Active -> HMI Runtime` authority;
- existing accepted Wave 11/12 architecture unless a demonstrated defect requires a controlled change;
- universal `EliteSCADA CI` as required gate for product-code PRs to `main`;
- exact-head evidence and post-integration validation.

## 7. Preview / Codespaces operating context for correction DEVs

Preview is a **validation harness**, not the architecture authority.

Live inspected PR #210 remains open/draft and uses branch `preview/codespaces-test-preview`, head `0ab6e80c1c47a78b0bd33b07424d906b5f847faa` at this snapshot.

The repository runbook on that branch is `docs/CODESPACES-PREVIEW-RUNBOOK.md`.

Current harness facts:

- exact .NET SDK 10.0.400;
- Node 24;
- TimescaleDB/PostgreSQL in Compose;
- one disposable per-Codespace machine identity mounted read-only at `/etc/machine-id` so normal licensing remains fail-closed;
- protected secret name `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- automatic launcher: `bash scripts/preview/launch-test-preview.sh` through `postAttachCommand`;
- Web forwarded on port 5173 and should remain Private;
- API 5080 and database remain internal;
- actual real-browser login has already succeeded;
- HTTP 502 on 5173 means the proxy exists but no Web process is listening and is not an accepted ready state.

Recovery levels:

- A — browser reload/HMR for appropriate frontend-only changes;
- B — restart Preview launcher for backend/launcher/runtime process changes;
- C — Rebuild Container for devcontainer/Compose/SDK/environment changes;
- D — fresh Codespace when persistent/bootstrap state is ambiguous or clean reproducibility must be proven.

If successful real-Codespace use requires an undocumented manual workaround, convert it to repository-controlled automation before accepting the Preview path.

### Password-policy update

Historical Preview evidence correctly records that an earlier bootstrap failed because the supplied secret did not satisfy the **then-current 12-character** product minimum.

The Development Lead has now explicitly changed the product requirement to **minimum 8 characters**. W14-C01 must implement this policy. Until its product-code SHA is accepted, the running Preview branch may still enforce 12 because the requested correction is pending code integration.

Never paste or record the actual protected password.

## 8. DNP3 licensing note for coordinators

The current Step Function adapter architecture was deliberately isolated behind a vendor-neutral DNP3 contract, which is favorable for replacement. The restricted dependency is not supposed to dictate the canonical EliteSCADA DNP3 runtime surface.

The L3 peer's pinned `dnp3py` source is promising because:

- its pinned LICENSE is MIT;
- its pinned `pyproject.toml` declares no project dependencies;
- it describes itself as pure Python and includes both master and outstation components.

But the lab currently proves interoperability **against a dnp3py peer**; it does not by itself prove that dnp3py is a production-quality replacement for the current EliteSCADA master adapter. W14-C03 owns that proof.

Until C03 is accepted and the distributable dependency graph is checked, retain the statement that DNP3 commercial distribution is blocked by the current adapter licensing situation.

## 9. Wave 13 boundary

Wave 13 #205/#207 remains paused. Its repository-side Windows packaging/signing machinery is preserved, but the product payload authority has moved to the evolving corrected Wave 14 baseline.

When a Windows installer is requested:

`product bytes = latest corrected/accepted Wave 14 baseline`

`Windows packaging mechanism = proven Wave 13 machinery`

Do not silently package the old Wave 13 product snapshot just because it is already green.
