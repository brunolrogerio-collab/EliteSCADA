# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

Wave 09 is closed on `main`. Current observed `main` head before Wave 10 product merge is `d7ef5db6a583fa949059f6b00cb2dfab3549e919`.

Wave 10 is active from frozen product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

Worker convergence integrated into the Wave 10 train:

- DEV 2 / #150: deterministic Runtime Visual tween scheduler and Python `visualTween.request`; exact worker head `a1f7584b23fd90a27257b5f12a42aafd90656ef0`, CI #811 green; integrated through replacement PR #158.
- DEV 3 / #151: mounted Python Preview/Test, bounded sample context, traceback/failing-line diagnostics and protected-data redaction; exact worker head `a81bf56b9ce23e7f982770744963f1b99b66a6ee`, CI #815 green; integrated through replacement PR #159.
- DEV 1 / #149: canonical visual Events editor and persisted event associations, including Timer `timerIntervalMs`, typed TAG identity `tagId + selector` and stable Client Memory identity; integrated through PR #156 after coordinator fixes and exact-head validation.
- Coordinator runtime composition: PR #170 merged into the integration train as merge commit `ee5d3c3f622765f79b49f32fa92c22760d195ae2`.

Coordinator functional product implementation before documentation-only checkpoint commits was exact head:

`8b7871bcd5a14ae17ffb070732f5a92c60462536`

Its EliteSCADA CI #872 (`33342402416`) is GREEN:

- Backend build, test and smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS;
- browser suite: 330/330 passed.

Mounted Wave 10 path proven in Chromium:

`DOM click -> canonical ScriptVisualEventReference -> ClientVisualPythonRuntime -> visualTween.request -> RuntimeVisualTweenScheduler -> RuntimeVisualInstance -> transient canonical renderer projection -> rendered intermediate frame -> deterministic stable final Script value`

The locked public precedence remains:

`Animation > Script > Binding/Expression > Engineering > Default`

Engineering remains immutable; runtime Script/Animation overlays are transient and fail closed. Screen and Popup instances are isolated by mounted runtime context.

### IMPLEMENTED IN PR

No Wave 10 worker/coordinator functional implementation remains outside the integration branch.

Final integration-to-main closure PR: #172.

The current PR #172 head is intentionally submitted to normal CI before merge; this checkpoint commit does not use `[skip ci]` so the exact integration head receives fresh evidence.

### SPECIFIED / NOT IMPLEMENTED

Wave 10 is not yet closed on `main`.

Remaining closure sequence:

1. validate the exact PR #172 integration head through normal CI;
2. merge that validated head to `main`;
3. require exact post-main green CI before declaring Wave 10 closed.

The complete owner-testable HMI Runtime demo vertical slice, including full product Runtime route composition, remains Wave 11 scope and must not be silently pulled backward into Wave 10.

Parallel Driver and Interoperability Lab work remains isolated and lower priority unless a real shared canonical contract requires Coordinator action.

## CI policy

CI mode remains **NORMAL**. Do not run reassurance CI on unchanged product trees. Exact functional integration/product heads require green evidence before merge/stage transitions. Documentation-only coordination commits normally use `[skip ci]`; closure-gate checkpoints may deliberately run normal CI when an exact branch head must be validated.