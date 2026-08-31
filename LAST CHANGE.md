# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

**Wave 10 is CLOSED / MERGED / POST-MAIN GREEN.**

Final integration-to-main PR:

`#172 — Wave 10 — Python visual events, animation and preview`

Exact validated integration head:

`adb0153dff36e172d0553463cc961a11bd7c7e1e`

EliteSCADA integration CI #873 (`33343012947`): **SUCCESS**

- Backend build, test and smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS.

Wave 10 merged to `main` as product merge commit:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Exact post-main EliteSCADA CI #874 (`33343325987`): **SUCCESS**

- Backend build, test and smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS.

Wave 10 worker/coordinator convergence now on `main` includes:

- canonical visual Events editor and persisted event associations, including Timer `timerIntervalMs`, typed TAG identity `tagId + selector` and stable Client Memory identity;
- deterministic Runtime Visual tween scheduler and Python `visualTween.request`;
- mounted Python Preview/Test with bounded sample context, traceback/failing-line diagnostics and protected-data redaction;
- central `ScriptVisualEventReference -> ClientVisualPythonRuntime -> RuntimeVisualInstance` composition;
- transient Script/Animation renderer projection that never mutates canonical Engineering;
- Screen/Popup runtime-instance isolation;
- mounted Chromium acceptance of the Wave 10 exit path.

The proven exit path is:

`DOM click -> canonical ScriptVisualEventReference -> ClientVisualPythonRuntime -> visualTween.request -> RuntimeVisualTweenScheduler -> RuntimeVisualInstance -> transient canonical renderer projection -> rendered intermediate frame -> deterministic stable final Script value`

The locked public visual precedence remains:

`Animation > Script > Binding/Expression > Engineering > Default`

Temporary CI-only PR #171 was closed unmerged after serving its validation purpose. Coordinator PR #170 was merged into the Wave 10 train before final PR #172.

### IMPLEMENTED IN PR

None for Wave 10. All accepted Wave 10 functional work is merged on `main`.

### SPECIFIED / NOT IMPLEMENTED

**Wave 11 — Complete HMI Runtime demo vertical slice** is the next product wave and has not been started by this Wave 10 closure.

Wave 11 owns the complete owner-testable HMI Runtime route/demo composition. Wave 10 intentionally established the canonical event/Python/animation behavior and mounted acceptance without creating a competing Runtime surface.

Parallel Driver and Interoperability Lab work remains isolated and does not change the Wave 10 closure state.

## CI policy

CI mode remains **NORMAL**. Exact functional integration/product heads require green evidence before stage transitions. Documentation-only checkpoint commits may use `[skip ci]`; the Wave 10 functional `main` product head `15daff2cc076f46f9433812babbd5cbb4b8d9554` is independently covered by post-main CI #874 GREEN.