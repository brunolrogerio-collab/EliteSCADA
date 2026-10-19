# Wave 15 issue audit and owner-authorized closeout

Date: 2026-10-19 (BRT). Main inspected #297 first, then #302, the current open issues/PRs, integration history and implementation/tests. Live GitHub remains authoritative after this snapshot. This audit does not authorize another product front or claim complete Wave 15 acceptance.

## Decision and method

The Product Owner instructed Main to close issues that have fulfilled their role. Physical driver L4 is performed by real people testing the product **outside GitHub control**; concrete corrections may be reported later. Do not keep implemented driver issues open for an L4 matrix/run. Untested hardware remains `NOT TESTED`, and software evidence does not imply certification or vendor/model coverage. Required software tests/CI and final product acceptance remain in force.

Classify requirements, not the age or OPEN state of their issue:

1. **Implemented, awaiting acceptance:** integrated behavior exists; a named final mounted/human/candidate acceptance is pending. Do not open duplicate implementation lanes.
2. **Proven residual defect:** a concrete current failure demonstrates an unmet product requirement. A historical incident, stale NO_MERGE comment or one unexplained CI timeout is not by itself a causal product diagnosis.
3. **Functionality actually not delivered:** an authorized behavior/artifact/content is absent or only a scaffold; actual development is needed.

A mother can contain implemented children and remaining work. Closing a fulfilled implementation/contract/research issue is a disposition, not a claim that the whole Wave or every future roadmap possibility was implemented.

## Fulfilled roles closed in this cycle

Every issue below has a Main closeout comment with evidence and limitations. Except #178, closure reason is `COMPLETED`. #178 is `NOT_PLANNED` because GitHub L4 tracking was retired, not because physical tests ran.

| Issues | Fulfilled role and accepted integration evidence |
| --- | --- |
| #489, #473 | Editable safe SVG and visual-library foundation. PR #490 merge `686ca205f73913046e1f80d000818ae3851d8197`; corrected SVG Dynamo PR #493 merge `9b23d2f103a416ad5b87b449bf20c2d13e4cf583`, exact T1 #37170522810 and accepted R1/R2/R3. Current catalog/Dynamo quality already accepted in #308. Old V4_CORRECTION_REQUIRED is superseded. |
| #474 | Asset Factory tooling/provenance pipeline, commit `9942e36a8a261cb5591dd60177e3d01db1544b4a` in integration; reported 26/26 exact-source validation. Not an unlimited artwork-growth program. |
| #475 | Home/Building research dossiers consumed by accepted implementations, including completed #545/#546/#547. Not a hardware-qualification tracker. |
| #469 | Modbus family / host serial package, PR #476 merge `f6ed7d8f3ac24a54673885d8be5c10f0dbf03e3f`; later generic diagnostics #500 completed. New concrete defects remain separately reportable. |
| #551, #566, #570 | Mitsubishi MELSEC PR #567 merge `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`, T1 #37782117990; Panasonic PR #572 merge `3f82487a16792945d663e9cf8c5e15199799d208`, T1 #37949879711 and exact T2 #37989607504. No new hardware claim. |
| #589 | Owner-approved experimental Matter inclusion: PR #594 merge `71bd07508e3340699bbb40c4463a8c5ac788c0d9`, exact T1 #38459079656 / T2 #38460067106. Not production/certification/HA/distribution qualification. |
| #472 | Agreed Home/Building W15 foundation: #483/#531/#543, interaction core, Shelly/ESPHome/HAB, external Z2M, Native Zigbee/KNX PR #582, DALI gateway PR #581, Z-Wave #586 and experimental Matter. Final F5/F6 PR #600 merge `f2668881e9866e8152be4c9e061320e48e9b456b`, T1 #38495115086 / exact T2 #38495789417. Original future possibilities are qualified by owner scope comment 6053621675, not silently promoted to mandatory W15 deliveries. |
| #178 | Retired Siemens physical L4 tracker; software convergence already completed via #187/#180. No family matrix marked PASS. |
| #468 | Theme/Restore-first duplication residual, PR #471 merge `056224d1498b217ccb9a7cf5d13e0a2eb27d27bb`, T1 #37086114733 / Main accepted. |
| #459 | Runtime compact/fullscreen/session shell, PR #464 merge `64ca1d87cc789356918a7f378543ceef6b31ab7e`, T1 #37073360651 / Main accepted. |
| #446 | Simulation-as-Gateway-source delivered by #450. Client Memory server-Gateway bridge explicitly deferred by owner comment 5954023256; closing does not implement or authorize that bridge. |
| #447 | Per-TAG Simulation/CurrentTime in #450; duplicate-authoring residual corrected by PR #462 merge `7b30aea8b624370a44eefdc5b04d0981fbdbe73b`. |
| #390, #391 | TAG commissioning #401/#406 and duplication #420, reconciled as implemented/integrated by Main in #386 comment 5953864742; #420 merge `ed04cf7358ca00b34593970ea3c1ba1931b01014`. |
| #354 | Neutral fresh-installation/Working-versus-Active authority correction, PR #372 merge `33e514eb3f5cf8f984779c0091069387741e7296`, Main-accepted real PostgreSQL mounted E3 including restarts and Working r3 versus Active r2. |
| #355 | Catalog selector correction, PR #370 merge `50b2750c73623b7ffef77f0ca93755c3e8278676`, accepted E2/E3. Later real forwarded E4 also opened the catalog successfully. |
| #359, #307 | Remote diagnostic/contract roles complete. Common timing PR #311 merge `43850a0239eb6d2a100958cb61d29fb054fd7292`; normal local, delayed local and real forwarded E4 passed. Final-candidate remote smoke remains #306/#300, per Main comment 5952457476. Historical HTTP 402 initiating cause remains unknown; no timeout rewrite or new transport freeze is inferred. |
| #386 | C0 owning-contract/consumer/freeze reconciliation completed; Main matrix comment 5953864742. C0 completion authorizes consumers, not complete-product acceptance or FC0. |
| #382 | Historian capture enforcement PR #404 merge `c3bb73c68bcc27a03190697b6fef902973241f8d`, exact T1 #36636344556; Main reconciliation marks implemented/integrated. Capture is still distinct from retention/query/presentation. |
| #377 | Branding PR #413 merge `32e2614997e050920e834906c081f91c9ce19046`, accepted T1 #36663454573, mounted Active/Working and shell coverage. |
| #369 | Script authoring PR #419 merge `bbb7513fef50bee847ec5cab86e4f8b24f91985b`, exact T1 #36711552620 / Main accepted, followed by accepted #444/#450 improvements. |
| #368 | Text/value/numeric-input PR #373 merge `2074070eb9e6227a186aabb27a050bccd0ae9522`, exact T1 #36437033735 and Main-accepted mounted E3 in comment 5883060120, including protected writes/readback and actual communication-loss rejection. |
| #380 | Structured forms #411/#415 and owner lifecycle UX simplification PR #461 merge `59bc99d55e0415f271561c6246e5e558ebdf7f98`, exact T1 #37068944604 with mounted one-click lifecycle, dirty Checkout, errors, three locales and desktop sizes. |

Count: **27** closed trackers (26 fulfilled bounded work packages plus one retired L4-only tracker). Their comments preserve the acceptance evidence; the issue bodies' obsolete ACTIVE/HOLD/NO_MERGE states are not current orders.

## 1. Implemented requirements awaiting final acceptance

These are not declarations that every clause of a broad mother has passed. They identify implemented behavior and the remaining named acceptance; retain current owners rather than reissue obsolete DEV bootstraps.

| Owning issues | Implemented requirement / remaining acceptance |
| --- | --- |
| #639 | Six generic display-only measurement widgets, PR #647 merge `0054d587fe855bbea4b1af81726af016208643da`; exact T1 #39258159241 and T2 #39259192178 attempt 2 passed. **Owner visual Preview remains pending before acceptance/freeze.** |
| #303 / #357 / #358 / #367 | Canonical single-surface editor, compact toolbox and dynamics/authoring improvements in accepted #444/#450/#373 and later #647. Final mounted Screen/Popup journey/owner acceptance remains under #300; OPEN is not evidence a new editor must be built. |
| #356 / #365 | Template/Equipment/Dynamo reuse materially implemented in #444/#450 and SVG/library work. Full mounted reuse journey remains to be accepted; do not schedule a broad rewrite from the old finding. |
| #364 | Dedicated Gateway/multi-route authoring and Simulation source behavior are in the integrated product, including #450 and tabular exchange #598. Final journey acceptance must cover independent routes/fan-out, persistence and duplicate-destination rejection. |
| #301 / #302 / #304 | Licensing/Authority/Installation technical slices are integrated, including #345/#346/#422 and Restore/theme #471. Final fresh-application entitlement/session/detach/attach acceptance is distinct from implementing these slices. No frozen Auth contract is reopened. |
| #299 / #423 / #366 | HA and database-topology technical packages include #463 and process evidence #611. Owner-supervised local HA Preview remains pending; two API processes are not proof of independent PostgreSQL-server failure or client reconnection. Earlier unsupervised HA work retains a regression risk and possible rollback disposition if owner/CODEX verification finds problems. No new HA change is authorized here. |
| #384 / #385 | Shared Query core #409, reporting/media/visual convergence #520 and portability/library core #405 are integrated. Final cross-consumer and mounted fragment/library compare/upgrade/fork/reuse acceptance must be reconciled; an old OPEN body does not prove the core is absent. |
| #379 | Phase 1 localization is integrated; final current-product parity sweep remains. This audit did not reproduce a current UI translation defect. Do not confuse this acceptance sweep with the actually incomplete translated manuals below. |
| #300 / #297 / #305 / #378 | Final fresh-candidate Preview and aggregate closure/coordination roles remain open while real children and acceptance remain. They are not additional missing code features. #306 also owns real artifact/content work below. |

## 2. Proven residual defect

This cycle did **not establish a current causal product regression** in the audited requirements. That is not proof that the product is defect-free or that human Preview passed.

There was a real failed automated gate: T2 #39259192178 attempt 1 at merge `0054d587fe855bbea4b1af81726af016208643da`, in `AuthorityPolicyTwoApiPostgreSqlEvidenceTests.RealApiProcesses_RevokeCancelRejoinAndRequireExplicitReactivation(sharedDatabase: False)`. Initial policy stayed pending without acknowledgement even after the standby became ready. The cause remains indeterminate; the peer-sync non-2xx response lacks sufficient diagnostics. Do not call it Modbus, a proven widget regression, or a proven infrastructure-only failure.

CODEX reproduced the exact test in isolated checkouts: merge 3/3 passed, base `65479ec5c92fec7c6d9c49f1e9001689b3743fe0` 1/1 passed; relevant HA/Authority/test files are byte-identical. These passes support a bounded intermittent-failure retry but do not exclude full-runner interactions. Main requested one backend-job retry; **attempt 2 passed on the same SHA without code/test changes**. Web and Chromium success were retained; full-browser E2E remained skipped. The failed attempt and unknown cause are not erased. [CODEX's diagnostic record](https://github.com/brunolrogerio-collab/EliteSCADA/issues/639#issuecomment-6268543313) preserves settings, command and limitations. No further rerun, HA implementation, test weakening or timeout relaxation is ordered.

## 3. Functionality actually not delivered

| Owning issue | Concrete remaining development |
| --- | --- |
| #588 | **Residual Stage 4D–4G** under ADR-021: reference provenance/data sufficiency; explicit versioned operating profiles/comparability; separately validated joint multivariate score; representative-data/security/persistence/platform/disabled-scope closure evidence. Frozen 4A–4C/ADR-020 are complete only within their accepted V1 limits, not the whole original Stage 4. |
| #588 | **Stage 5B guided dashboards** from equipment/process profiles and **Stage 6 final hardening/package** remain later work. Generic widgets #639 do not deliver automatic dashboard assembly. Sequence is 5A gates -> residual Stage 4 -> 5B -> 6. |
| #424 | Final contextual Help content/coverage for the final product. Phase 1 PR #439 actually merged at `750871838ee3a3faa9320565f9baff4fb7ffe892`; its old WAIT comment is stale. `docs/WAVE15-HELP-PHASE1-COVERAGE.md` still lists later coverage/placeholders; current help source lacks the new Intelligence task coverage. |
| #425 | Complete manual and translated content, not just acceptance: `docs/manual/COVERAGE-MATRIX.md` still has draft/blocked content and final screenshots pending. English/Spanish README scaffolds explicitly do not constitute complete translations. Phase 1 PR #437 is not completion of this mother. |
| #306 | Actual final **EEE Simulation v15 and real-Modbus v15** distributable demo packages, checksums/provenance/mapping/walkthrough. The existing `preview/fixtures/EliteSCADA-EEE-Demo.escadapkg` has 2026-09-09 Wave11/schema16 provenance; its presence does not demonstrate final v15 artifacts. Fresh final Preview #300 is the acceptance gate after candidate preparation, not another new feature. |

## Scope exclusions and preserved restrictions

- EliteGO #298 is deferred indefinitely. Native installers/Release Factory #408, service/OCI work #361/#363 and distribution hygiene #381 are later release scope; do not silently pull them into Intelligence or treat them as new W15 blockers.
- Paid direct Tuya/Intelbras cloud, direct BTHome/Bluetooth qualification and native DALI are not current mandatory W15 deliveries. DALI is the delivered gateway profile, not a native bus stack. Existing Zigbee/Matter device compatibility is not a promise of every vendor model.
- Matter remains `Matter Wi-Fi (Versão em teste)`, opt-in Development/Test, absent by default and blocked in Production/Staging/HA. The stable replacement/preparation after Wave16 before distribution remains required. Closing #589/#472 does not relax these restrictions.
- No completed feature is reopened merely because a historical branch/issue description says ACTIVE or because physical L4 is still unrun. New reproducible corrections get concrete evidence and a bounded owner.
- Main next follows the existing #639 owner-acceptance gate and dispatches ADR-021 4D against a revalidated exact base, then 4E–4G. No HA, drivers, Stage 5B/6, FND/FC0 or full-Wave acceptance is released by this audit.
