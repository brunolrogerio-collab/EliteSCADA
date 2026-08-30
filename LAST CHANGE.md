# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

Wave 09 is closed on `main`. Current observed `main` head before Wave 10 product merge is `d7ef5db6a583fa949059f6b00cb2dfab3549e919`.

Wave 10 is active from frozen product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

Worker convergence already integrated into the Wave 10 train:

- DEV 2 / #150: deterministic Runtime Visual tween scheduler and Python `visualTween.request`; exact worker head `a1f7584b23fd90a27257b5f12a42aafd90656ef0`, CI #811 green; integrated through replacement PR #158.
- DEV 3 / #151: mounted Python Preview/Test, bounded sample context, traceback/failing-line diagnostics and protected-data redaction; exact worker head `a81bf56b9ce23e7f982770744963f1b99b66a6ee`, CI #815 green; integrated through replacement PR #159.
- DEV 1 / #149: canonical visual Events editor and persisted event associations, including Timer `timerIntervalMs`, typed TAG identity `tagId + selector` and stable Client Memory identity; integrated through PR #156 after coordinator fixes and exact-head validation.

### IMPLEMENTED IN PR

Coordinator PR #170 (`coordination/wave-10-runtime-composition` -> Wave 10 integration) implements the central mounted Runtime composition on exact product head:

`8b7871bcd5a14ae17ffb070732f5a92c60462536`

Delivered path:

`DOM click -> canonical ScriptVisualEventReference -> ClientVisualPythonRuntime -> visualTween.request -> RuntimeVisualTweenScheduler -> RuntimeVisualInstance -> transient canonical renderer projection -> rendered intermediate frame -> deterministic stable final Script value`

The composition preserves the locked public precedence:

`Animation > Script > Binding/Expression > Engineering > Default`

Engineering remains immutable; runtime Script/Animation overlays are transient and fail closed. Screen and Popup instances are isolated by mounted runtime context.

Exact-head EliteSCADA CI #872 (`33342402416`) is GREEN:

- Backend build, test and smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS;
- browser suite: 330/330 passed, including the mounted Wave 10 Python visual tween acceptance and Events editor persistence tests.

Temporary PR #171 exists only to trigger normal `main`-target CI for the coordinator head because the current workflow listens to pull requests targeting `main`; it must never be merged.

### SPECIFIED / NOT IMPLEMENTED

Wave 10 is not yet closed on `main`.

Remaining closure sequence:

1. merge coordinator PR #170 into the Wave 10 integration branch after exact-head green evidence;
2. validate the exact resulting integration head through normal CI;
3. merge the validated Wave 10 integration head to `main`;
4. require exact post-main green CI before declaring Wave 10 closed.

The complete owner-testable HMI Runtime demo vertical slice, including full product Runtime route composition, remains Wave 11 scope and must not be silently pulled backward into Wave 10 merely to satisfy a test harness.

Parallel Driver and Interoperability Lab work remains isolated and lower priority unless a real shared canonical contract requires Coordinator action.

## CI policy

CI mode remains **NORMAL**. Do not run reassurance CI on unchanged product trees. Exact functional integration/product heads require green evidence before merge/stage transitions. Documentation-only coordination commits use `[skip ci]`.