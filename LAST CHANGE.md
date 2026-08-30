# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

Wave 09 is closed on `main`. Current observed `main` head is `d7ef5db6a583fa949059f6b00cb2dfab3549e919`; commits after the validated product head include coordination/documentation-only changes.

Wave 10 is active from frozen product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

### IMPLEMENTED IN PR

- DEV 2 / #150: runtime visual animation/tween is implemented in PR #153 at validated worker head `a1f7584b23fd90a27257b5f12a42aafd90656ef0`; EliteSCADA CI #811 green. Integration into the shared renderer/event path remains Coordinator-owned.
- DEV 3 / #151: mounted Python Preview/Test and traceback UX is implemented in PR #154 at validated worker head `a81bf56b9ce23e7f982770744963f1b99b66a6ee`; EliteSCADA CI #815 green. Coordinator may reconcile the older duplicate presentation while preserving the accepted runtime host.

### SPECIFIED / NOT IMPLEMENTED

DEV 1 / #149 had stopped at two genuine canonical event-binding gaps and made no product mutation. Coordinator resolution is now recorded in #152 and delegated to DEV 1:

- Timer associations persist explicit nullable `timerIntervalMs`; Timer requires a valid interval meeting the existing runtime minimum, while non-Timer events leave it null. Interval is never encoded in `TargetReference`.
- TAG value-change associations persist typed stable `tagId` plus optional existing canonical `TagValueSelector`; display syntax such as `.NN` remains UX only and is never the persisted identity.
- Client Memory change uses stable definition identity, not friendly path/name.

DEV 1 is authorized to resume immediately with the smallest shared DTO/schema/validation/runtime-adapter extension plus the mounted Events editor. Central DI/shared event dispatcher composition remains Coordinator-owned.

Wave 10 exit gate remains:

`click -> canonical event binding -> Python entry point -> script visual command -> animated public visual property -> deterministic stable final result`

The final exact integration head must pass normal CI before any transition to `main`; exact post-main green evidence is required before Wave 10 closure.

Parallel Driver and Interoperability Lab work remains isolated and lower priority unless a real shared canonical contract requires Coordinator action.

## CI policy

CI mode remains **NORMAL**. Do not run reassurance CI on unchanged product trees. Exact final integration/product heads require green evidence before merge/stage transitions. Documentation-only coordination commits use `[skip ci]`.