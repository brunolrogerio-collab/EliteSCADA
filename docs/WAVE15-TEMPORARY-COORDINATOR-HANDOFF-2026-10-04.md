# Wave 15 — Temporary Coordinator Handoff — 2026-10-04

GitHub live is authoritative. Revalidate issue, branch, PR and CI state before taking action. This is a development checkpoint, not Wave 15 completion and not merge authorization.

## Integration snapshot

This branch was started from the live `wave15/corrections-integration` head, verified at `4717ab165e5b627fc41976f8f76e591d4c9cf368` on 2026-10-04. It includes the product integration through PR #498. The branch has not been pushed as a PR at the time of this checkpoint.

Recently integrated foundations relevant here:

- #490 — first-class editable SVG / `core.svgSymbol`;
- #491 — categorized static SVG asset-library foundation;
- #493 — SVG-backed Dynamo composition;
- #498 — Reporting V3.

Other already-merged work visible in the live history includes #479 (Modbus family/host serial), #480 (Playback, Modbus and Asset Factory convergence), and #499 (protected material authority). Verify exact merge ancestry in GitHub before using this note as a release record.

## #501 — Dynamo catalog checkpoint

Issue: [#501 — W15-DYNAMO-CATALOG-V1](https://github.com/brunolrogerio-collab/EliteSCADA/issues/501)

Contract `C-DYNAMO-CATALOG-V1`; expected state on completion is `READY_FOR_MAIN_REVIEW / NO_MERGE`. Main owns integration; DEV does not merge.

The current branch adds an initial 26-item replacement-generation catalog, separate from the original 72 built-ins:

- 4 signal-lamp variants;
- 4 operator-button variants;
- 6 motor variants;
- 6 valve variants;
- 6 electrical-contact/disconnector variants, including mono/trifilar and horizontal/vertical artwork.

The artwork is generated as first-party SVG from native geometry, registered as stable Visual Assets, and referenced by `core.svgSymbol` in Dynamo composition. Existing project references to the legacy 72 remain intact. Only the default library palette hides entries explicitly marked `catalogStatus=legacy`; legacy definitions are not deleted.

The current initial runtime behavior includes numeric TAG-state paint maps for lamps, motors and valves, configurable color parameters, a fixed-state option, semantic SVG paint slots, and buttons that project to existing authorized command / TAG-write actions. The SVG semantic-slot mechanism also supports dynamic paint on a placed static SVG without wrapping it as a Dynamo; verify the complete mounted screen/runtime path as part of acceptance.

Continuation after the first checkpoint fixed two additional runtime gaps: contact-blade geometry now changes its fill and rotation from the selected state TAG, and the C07 state indicator now resolves the public `state` TAG through the same canonical `PropertyMap` source used by rendering. Numeric profiles now classify running/open, fault, communication-failure and inhibited states; quality/fault/alarm/uncertain/inhibited/command precedence is deterministic. Button artwork has accessible keyboard and pressed feedback. Latest local exact HEAD: `c5c368e8` (pushed to the existing draft PR #502). Focused Dynamo model/state specs are now 22 passing; production web build passes.

### Explicitly incomplete — do not call #501 done

- Lamps now accept typed numeric TAG or numeric-expression sources through a dedicated authoring control and runtime projection; inverted Boolean and Boolean-result script binding remain unimplemented.
- Motor/valve state selection is still a single numeric state TAG. Independent Boolean signals/expressions and configurable per-signal conflict resolution remain incomplete. Quality is surfaced through the runtime state indicator; prove the end-to-end bad-quality rendering and palette behavior in mounted Runtime.
- Per-state text parameters are declared but not yet rendered; command mappings for motors/valves are not wired to canonical interactions.
- Button released/pressed artwork is currently driven by a numeric state mapping. Momentary interaction feedback, robust failure feedback, and the script-authoring wizard are not implemented.
- Contact symbols now animate canonical blade color/rotation from their selected two-state TAG, but broader electrical semantics and user-configurable per-pole state configuration remain to be done.
- Professional visual review of all 26 symbols and mounted editor/runtime parity have not been completed.
- Test coverage so far is focused contract/model coverage, not the full issue acceptance matrix: package/export-import round trip, save/reopen, copy/paste, screen/popup/template parity, security, and exact-head T1 remain required.

Do not silently shrink the issue criteria. Main should either release a bounded continuation order to finish these items or revise the acceptance criteria with Product Owner authority.

## Validation recorded on this branch

- Backend `Scada.Drivers.Tests`: 900/900 passed locally on the branch including #504.
- Focused `PointRead`/reachability .NET selection: 17/17 passed; one RTU write-authority timeout from a broader concurrent selection passed when isolated and remains classified as likely contention, not fixed.
- Focused Dynamo catalog/runtime-model Chromium specs on this continuation: 13/13 passed in the Dynamo model-only Playwright profile, including typed TAG/expression value-source projection and mismatch validation. The independent geometry PR #505 has 24 focused model tests passed locally.
- `Wave09PopupDynamoNavigationEngineeringTests`: 8/8 passed locally; `BuiltinDynamoLibraryTests`: 18/18 passed locally.
- Web production build: passed; existing large-chunk warning only.
- The standard mounted Playwright E2E bootstrap was attempted but could not start: the local API requires durable PostgreSQL, and this checkout does not start the test database or register its engineering project catalog in this profile. This is infrastructure-not-run, not a product test pass.
- Draft PR #502 remains OPEN / DRAFT / NOT MERGED; live head at the start of this continuation was `6bb2521566abdd409d70dab126cb9a353705e575`. Its first T1 failed at profile classification and skipped product test jobs; no exact-head product T1 result exists. The branch is being advanced by merging the current integration base and applying only the Dynamo source commit; keep geometry from #505 out of #502.

## Other product lanes still open

PR #505 now declares `VALIDATION_PROFILE: UI_EDITOR`; its first T1 predates that correction, failed profile classification and skipped product jobs. Geometry local evidence is 24 focused model tests, separate from Dynamo-specific model coverage.

- [#495 — Media Source Core](https://github.com/brunolrogerio-collab/EliteSCADA/issues/495) is OPEN and was unblocked after #497 integration. No consumer implementation has been published yet; canonical local PDF/video resources, network media sources, protected credentials, camera relay, package fidelity and bounded failure/reconnect behavior remain incomplete.
- [#496 — Visual utility objects](https://github.com/brunolrogerio-collab/EliteSCADA/issues/496) is OPEN and explicitly gated on #492 V4, #495 media foundation and #494 report toolbar/launcher reconciliation. Video/IP-camera and PDF objects must not be claimed complete based on #498 reporting.
- [#500 — Driver diagnostics convergence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/500) remains OPEN pending acceptance/review despite PR #504 being merged at `407b37ad`. Local `Scada.Drivers.Tests` reported 900/900 previously; the current targeted PointRead/reachability selection passed 17/17. A broader selection also included one RTU server write-authority timeout, which passed on isolated rerun (likely test contention; not called fixed).
- Product Owner reproduction is now correlated to the running Docker bundle: Engineering's persisted `testemodbus` TAG has a stable ID and configured Modbus source ID, `int16`, `holding:0`; the Runtime and Engineering tabs are open against the same active project. API logs record the 400 at `POST /api/engineering/driver-tools/point-read-test` (555-byte request) at `2026-10-04T05:30:31Z`. The containers started `2026-10-03T23:52Z`, before #504 merged at `2026-10-04T06:28Z`, but the image has no source-revision label, so exact bundle ancestry is unproven. Current source routes persisted TAGs with stable source IDs to `/api/engineering/data-sources/{id}/driver-tools/point-read-test`, while retaining the draft route for unpersisted changes. The observed request route is therefore consistent with the pre-fix persisted/draft selection path and is a strong lead, not a closed root-cause proof. Acceptance still requires rebuilding from the merged code and testing the same persisted TAG, then confirming a successful or appropriately classified response. No live device write was issued.
- Remaining #500 evidence includes the broader per-provider failure/quality/timeout/protected-material/cancellation matrix and OPC UA against a real test server (current positive unit case uses the canonical session seam); do not describe those as complete. The canceled intermediate post-merge CI is not the requested final CI. The stable Docker stack has not yet been updated.
- [#503 — Editor/Runtime responsive backlog](https://github.com/brunolrogerio-collab/EliteSCADA/issues/503) is OPEN. Bézier anchor add/remove and thin-line hit-target/selection-frame support are in draft PR #505 (`work/w15-editor-geometry-authoring-20261004`), with local build and 24 model tests green. The profile declaration is now fixed, but its exact-head T1 still needs a rerun. Runtime header controls, responsive desktop scaling and mobile orientation/screen variants remain backlog work.
- Residential automation-driver implementation remains gated by the common driver-diagnostics foundation; do not begin protocol fan-out until Main explicitly releases it.

The Opto 22 [SVG library/editors](https://www.opto22.com/support/resources-tools/image-library-svg-editors) and Wikimedia [P&ID symbol category](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols) are research references only. Use only assets with confirmed redistribution rights. The present 26 replacement symbols are original EliteSCADA geometry, not copied site artwork.

## Next coordinator actions

1. Review this branch's exact diff and decide whether to open a draft PR checkpoint or return it for a bounded completion pass. Do not merge from DEV.
2. Complete #501's missing source modes, state/quality priority, per-state labels, equipment commands, button feedback, electrical contact animation, visual QA, and full package/editor/runtime acceptance.
3. Run the required exact-head T1 once the mounted test environment is correctly provisioned; do not represent skipped E2E or absent CI as green.
4. Track #495, #496 and #500 as independent active product outcomes; their completed portions must not be inferred from #498.
5. Reconcile the issue ordering/dependencies with Product Owner before releasing the next residential-driver coordinator. Preserve canonical TAG/Runtime/command and protected-material authority.
6. Do not stop/reset local services or alter the stable Docker container as part of this handoff; no such local runtime mutation was made in this checkpoint.

Checkpoint disposition:

`#501 IMPLEMENTATION CHECKPOINT / INCOMPLETE / NO_MERGE`

`#495 MEDIA CORE OPEN / #496 VISUAL MEDIA OBJECTS OPEN / #500 DRIVER DIAGNOSTICS OPEN`

`NEXT COORDINATOR MUST REVALIDATE GITHUB LIVE`
