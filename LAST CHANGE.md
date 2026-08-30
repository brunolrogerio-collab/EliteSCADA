# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

Wave 09 is closed on `main`.

Current observed `main` head:

`d7ef5db6a583fa949059f6b00cb2dfab3549e919`

Commits after the validated Wave 09 product head include coordination/documentation-only changes.

Wave 10 remains active from frozen product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

### IMPLEMENTED IN PR

- DEV 2 / #150: runtime visual animation/tween is implemented in PR #153 at validated worker head `a1f7584b23fd90a27257b5f12a42aafd90656ef0`; EliteSCADA CI #811 green. Integration into the shared renderer/event path remains Coordinator-owned.
- DEV 3 / #151: mounted Python Preview/Test and traceback UX is implemented in PR #154 at validated worker head `a81bf56b9ce23e7f982770744963f1b99b66a6ee`; EliteSCADA CI #815 green.
- Driver 5 / Allen-Bradley CIP independent-software L2 validation is implemented in validation PR #165 on a lab-only branch derived directly from validated Driver 5 head `18ff6dc989a65c1f8b006f83c08d8394a5510914`. Functional lab head `c47a1cd2093c0f3fc30ea9376eba806ae4455168` passed direct EtherNet/IP client read/write, full `AllenBradleyLogixDriver` read/write and real peer restart/reconnect. A separate diagnostic gap remains: hard-close can fail to increment `DisconnectionCount`; this does not invalidate the functional reconnect evidence.
- Driver 6 / IEC-104 independent-software L2 validation is implemented in draft validation PR #166 on branch `coordination/driver6-iec104-lib60870-l2-lab-v1`, derived directly from validated Driver 6 head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`. Exact functional head `01096e7ca1978e08d6e1a1c24e8fd252da9ace38` passed dedicated run `33337416898`: 2/2 tests green against MZ Automation `lib60870-C` v2.4.1 pinned to upstream commit `7a388e3e133999e1ca77ba7521d55d074b7cd2bc`. Evidence includes real TCP/2404, STARTDT, startup GI for CA 1, monitored points, periodic telemetry and real peer restart with managed reconnect plus repeated GI.
- MQTT independent-software interoperability/freshness checkpoint is frozen at lab head `25a23c028fb096d77d51ff527a5d74ac54be7736`, with Mosquitto/HiveMQ, TLS/auth/security negatives, QoS/retained/persistent-session/restart and freshness evidence green. A prior isolated QoS1 timeout passed on same-SHA rerun and is classified as a test flake, not a product regression.

### SPECIFIED / NOT IMPLEMENTED

DEV 1 / #149 event-binding schema gaps were resolved by Coordinator in #152 and delegated back to DEV 1:

- Timer associations persist explicit nullable `timerIntervalMs`; Timer requires a valid interval meeting the existing runtime minimum, while non-Timer events leave it null. Interval is never encoded in `TargetReference`.
- TAG value-change associations persist typed stable `tagId` plus optional existing canonical `TagValueSelector`; display syntax such as `.NN` remains UX only and is never persisted identity.
- Client Memory change uses stable definition identity, not friendly path/name.

DEV 1 is authorized to continue the smallest shared DTO/schema/validation/runtime-adapter extension plus mounted Events editor. Central DI/shared event dispatcher composition remains Coordinator-owned.

Driver/lab evidence still NOT provided by the above software tests:

- physical PLC/RTU/IED hardware acceptance;
- vendor-specific conformance/certification;
- IEC 62351/TLS acceptance for IEC-104;
- alternate IEC-104 Peer B acceptance;
- full IEC-104 command/SBO matrix, load/window wrap and soak certification;
- production merge authorization for any driver merely because a lab gate is green.

## Wave 10 exit gate

`click -> canonical event binding -> Python entry point -> script visual command -> animated public visual property -> deterministic stable final result`

The final exact integration head must pass normal CI before any transition to `main`; exact post-main green evidence is required before Wave 10 closure.

Parallel Driver and Interoperability Lab work remains isolated and lower priority than Wave 10 unless a real shared canonical contract requires Coordinator action.

## CI policy

CI mode remains **NORMAL**. Normal product CI, independent-software interoperability and physical hardware/vendor acceptance are separate evidence levels and must be reported separately. Do not run reassurance CI on unchanged product trees. Exact final product/integration heads require green evidence before merge/stage transitions. Documentation-only coordination commits use `[skip ci]`.