# Wave 15 — FC0-A Post-Merge Contextual Help E2E Load Closeout

> GitHub live is the sole authority.
> This is a bounded post-merge validation correction. It does not reopen FC0-A product scope.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`MAIN_ORDER_REV: 0002`

`STATE: MERGED / HELP_LOAD_DEFECT_CLOSED / NO_MUTATION`

`ORDER_ID: FC0A-POSTMERGE-HELP-E2E-LOAD-V1`

`EXECUTOR: SEQUENTIAL CODEX`

`EXACT_BASE_SHA: da0e64122f1e4d0293e027f45ef95021cd03c1a1`

`EXACT_BASE_TREE: a724e565f11121a43b97bf0c59683b414c72f48c`

`WORK_BRANCH: work/w15-fc0a-postmerge-help-e2e-load`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: UI_SHELL / CONTEXTUAL_HELP`

## 1. Trigger

INFRA-CI-01C PR #341 was accepted and merged at:

`da0e64122f1e4d0293e027f45ef95021cd03c1a1`

Exact post-merge broad gate:

`EliteSCADA CI 36047274028 / #1567`

Results:
- Web build — SUCCESS;
- Backend build — SUCCESS;
- Backend Test — SUCCESS;
- Runtime smoke — SUCCESS;
- Chromium E2E — FAILURE before test execution.

Exact Chromium load failure:

`SyntaxError: web/scada-web/src/auth/auth.css: Unexpected token (1:0)`

Load chain:
- `web/scada-web/tests-e2e/contextual-help-routing.spec.ts`
- imports `contextualHelpTopic` from `../src/AppNavigation`;
- `AppNavigation.tsx` imports React/auth/theme modules and `./app-navigation.css`;
- Playwright's Node-side spec loader therefore reaches CSS while loading a pure routing assertion and aborts before the browser suite starts.

## 2. Classification

`FC0A_POSTMERGE_E2E_TEST_LOAD_DEFECT / PR340_TEST_CAUSAL / PRODUCT_BEHAVIOR_NOT_SHOWN_DEFECTIVE`

Evidence:
- `contextual-help-routing.spec.ts` was added by the accepted FC0-A package;
- the exact broad failure occurs during module parsing, not a mounted product assertion;
- Backend/Web are green;
- INFRA-CI-01C's PostgreSQL recurrence is closed in the broad backend Test + smoke path;
- no rerun can establish release while this deterministic spec-load failure remains.

Do not reopen Contextual Help product behavior unless a corrected test exposes a real behavior defect.

## 3. Preferred bounded correction

Preserve the routing behavior while making the pure mapping independently importable by a Node-side test.

Preferred implementation:
1. move/extract only `contextualHelpTopic(path)` into a CSS-free pure module under the existing app/help source boundary;
2. `AppNavigation.tsx` imports and uses that helper;
3. `contextual-help-routing.spec.ts` imports the pure helper rather than the React shell component;
4. preserve all current topic mappings and fallback behavior unchanged.

An equally small mounted-test correction is acceptable if it avoids copying/reimplementing the routing table and does not weaken evidence.

## 4. Allowed scope

Expected files:
- `web/scada-web/src/AppNavigation.tsx`;
- one small pure helper module if extraction is chosen;
- `web/scada-web/tests-e2e/contextual-help-routing.spec.ts`.

Only directly necessary test/import files may be added.

Production semantic changes are not authorized unless the focused corrected test exposes a real routing defect.

## 5. Forbidden

Do not:
- skip/exclude the failing spec;
- change broad CI selection to hide it;
- remove CSS imports from product components merely for test convenience;
- duplicate the mapping table inside the test;
- weaken Help routing assertions;
- broaden Contextual Help product scope;
- touch PostgreSQL/01C code;
- alter frozen Foundation contracts;
- self-merge or declare FC0-A released.

## 6. Acceptance

Candidate PASS requires:

1. exact branch base remains `da0e6412...`;
2. the deterministic load failure is explained and removed;
3. `contextual-help-routing.spec.ts` actually loads and executes;
4. all current route mappings remain asserted, including safe fallback;
5. smallest focused Playwright command for that spec is green;
6. Web production build is green;
7. relevant existing Help regression(s) remain green;
8. natural Wave 15 T1 is green on the exact candidate where selected;
9. bounded PR to `wave15/corrections-integration`;
10. Main review before merge;
11. exact post-merge `EliteSCADA CI` must be globally green — Backend, Web and Chromium — before FC0-A release.

## 7. Return

Return exactly:

`FC0-A POSTMERGE HELP-E2E CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- base -> exact candidate SHA/tree;
- root cause;
- changed files;
- focused command/results;
- Help regressions;
- Web build;
- natural T1 run;
- explicit non-actions;
- no merge/freeze/release.


## 8. Main closure evidence

Candidate PR #342:
- head `55052251814a441f454f1d7d77d8b5d1c763637e`;
- tree `09d58d58e96e4c758c19d6ff2945a96786d420c3`;
- natural T1 `36048610668` — SUCCESS;
- focused Chromium — 32 passed;
- Web semantic build — SUCCESS.

Protected merge:
- SHA `1ab3550e1afb258e38caaa6de3f6547f481bbef7`;
- tree `701f4591a294885b414284691b1051cf274c9707`.

Exact broad CI `36049229264 / #1568` proves the original CSS/Node spec-load failure is closed: the Chromium suite loaded and executed 655 tests.

The broad gate later failed one **different** stale Visual Editor source-contract assertion after 654 tests passed. That blocker is owned by:

`docs/WAVE15-FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-CONTROL.md`

order:

`FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`

No more Help E2E mutation is authorized by this control.
