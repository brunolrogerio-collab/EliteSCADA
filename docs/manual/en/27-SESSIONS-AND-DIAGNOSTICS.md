# Runtime sessions and diagnostics

## Runtime session: view, interact and end

Understand when you can watch the application and when you can operate its controls.

### Watch or operate

Choose View only to follow values/states. Request Interactive to use process controls. This is a request to the server: always check Granted for the actual result. Your account must also have permission for the action. Interactive does not give a person access to equipment they were not authorized to use.

### Check your session

1. Sign in and open Runtime.
2. Open the account menu in the header and find Runtime session class.
3. Choose View only to observe or Interactive to operate.
4. Wait. Requested shows your request; Granted shows the effective response.
5. If you requested Interactive but received View only, read the notice before attempting a command. Interactive capacity may be unavailable. Ask the owner to check the reason when needed.

### Example: you see the pump but cannot command it

First check Granted. View only allows observation, not operation. If it says Interactive, check your account’s permission for that pump/action. If both are correct, inspect the command message and communication diagnostics. Work through one cause at a time: session class does not fix a TAG binding or equipment failure.

### Change class or end

Choose another class in the same panel to change it. The previous session is released before the new request. If that fails, you may need to request a session again; the previous one is not automatically retained. To finish, choose End session and wait for confirmation. Closing the menu only hides the panel. Signing out is separate. After a connection failure, confirm release with the owner; closing a window is not confirmation.

### Look up controls and notices

The server supplies results. You do not type session numbers or IDs. Open technical details only when explaining a rejection to the person responsible.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| View only | Requests a session for monitoring the application, without operational commands or writes. | Watch the pump state | Not anonymous login; does not change account permissions. |
| Interactive | Requests interactive capacity from the server. Check the granted class before operating. | Operate with an authorized account | May be rejected or fall back to View only; does not guarantee the action. |
| Requested | Shows the class requested by this client. | Interactive | Does not prove the effective class. |
| Granted | Shows the effective class of the admitted session. This is the result to check. | View only | If different from Requested, read the notice; fallback is not interactive success. |
| Admission reason | Technical detail of the server decision. Open Technical details when investigating admission. | Server-returned code | Diagnostic code, not an editable configuration or bypass. |
| Capacity reason | Helps explain session availability/limits. Check licensing and occupied sessions after a capacity rejection. | Interactive capacity unavailable | Capacity is not Authority permission. |
| End session | Releases the current Runtime session after the server confirms. | No active Runtime session | Not Sign out; closing the menu does not end the session either. |
| Error or notice | Read the admission, end or fallback message before repeating the action. | Admission rejected | A network failure does not confirm release or a new grant. |

### When admission is rejected

Record message, time and attempted action. Permission changes or restoration may require signing in/requesting a session again. Do not copy session IDs/headers to obtain access. In permission details, ProcessValueWrite means value writing and CommandExecute means command execution. They are different authorizations. Licensing limits available features; account permissions determine which actions you may use.

[Open this guide in installed Help](/help?topic=runtime.session-classes)

## Diagnostics: locate the first cause of failure

Interpret host health, communication, network and TAG quality before changing a screen or driver.

### Choose the right layer

A frozen screen may result from session, active revision, communication, TAG or presentation. Diagnostics gathers evidence; opening the panel neither restarts a source nor changes the process. In Engineering, open communication diagnostics. It observes Runtime / Active Revision: a source created only in Working need not appear yet. Start with the API/DriverHost host, then source, network/protocol, sample and screen. Address the first upstream divergence before changing a color or inserting a fixed display value.

### Check a source step by step

1. Record TAG/source name, time, user and failed action; confirm the active revision.
2. Check API / DriverHost host and Observed. Unavailable/stale host evidence does not confirm current source states.
3. Clear search or enter part of the source, driver or endpoint. Start with All or Needs attention.
4. Select the source. Read state, Last error, last success/failure and data age.
5. Compare quality and counters across two observations; an accumulated total does not date each failure.
6. Use network reachability, Connection Test or Test Read only where available and needed.
7. Return to the TAG and confirm value, quality, time and updates; then check the screen dynamics.

### Search, refresh and observation trust

Filters affect the displayed list only. Disabling panel refresh does not stop a driver or acquisition.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Search Data Source, driver or endpoint | Matches part of source name/key, driver type or endpoint. | Part of the pump name | Does not create a source; clear it when the list seems empty. |
| Filter | All shows available sources; Needs attention includes nonhealthy states; Healthy only shows confirmed healthy sources. | Needs attention | No match can be just a filter. Inactive and filtered-out sources differ. |
| Automatic refresh | Updates panel observations every 3 seconds while enabled. | Enabled during investigation | Does not change driver scan, retention or acquisition. |
| Refresh now | Requests a fresh diagnostic reading. Wait before comparing. | Two successive observations | Does not restart or manually reconnect the driver. |
| API / DriverHost host — Observed | Check whether the process-health observation is still recent. | Observation timestamp | Stale/unavailable host evidence makes source states unconfirmed in the panel. |
| Panel totals | Source, healthy, attention, fault and Good TAG totals orient the initial review. | 2 sources, 1 needs attention | Do not replace individual errors or prove every point is healthy. |

### Read operational state and counters

Missing values appear as a dash in several indicators; a dash is neither zero nor success. Runtime instance distinguishes executions: restarting/recreating can make accumulated totals incomparable. Cycles count acquisition executions; requests are protocol requests. Successes/failures count operation outcomes, while consecutive failures highlight the recent sequence without recovery. Timeouts are operations that did not finish by their deadline. Connections, disconnections and reconnects help assess link stability. Reads/writes count operations, not necessarily distinct TAGs. Published updates count values passed to Runtime, not guaranteed Historian persistence. Interpret each indicator for that driver and the same interval/instance; protocols need not count everything identically.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| State | Healthy, degraded, reconnecting, faulted or stopped directs investigation; also read Last error. | Reconnecting | A reachable endpoint does not replace driver state. |
| Endpoint | Destination associated with the selected source. Check it is the intended device. | Configured destination | May be serial or a local bridge; not every endpoint is TCP host/port. |
| Last success | When communication succeeded. Compare with now and the expected cadence. | Success a few seconds ago | An old success does not prove a current point. |
| Last failure | Locates a reported failure in time. Compare with later success. | Failure before latest success | Alone does not say whether failure continues. |
| State change | When the reported state changed. | Started reconnecting | Not necessarily a process-sample timestamp. |
| Data age | Helps detect missing updates. Compare with expected source behavior/scan. | Increasing age | Not every source is cyclic; check acquisition mode before declaring a defect. |
| Configured scan | Configured cadence where applicable. | Driver interval | Not edited here; event-driven updates may behave differently. |
| Latency and last cycle | Average/last latency and last scan duration help distinguish slowness from absent updates. | Longer than usual | Operation duration is not full sensor-to-pixel delay. |
| Recent failure rate | Observe the recent proportion of failed operations. | Increasing trend | Not equipment efficiency or the percentage of bad TAGs. |
| Activity and counters | Compare cycles, requests, successes, consecutive failures, timeouts, connections/reconnects, reads, writes and published updates. | Failures increase between observations | Accumulated totals and instance changes need context; do not reset the system to interpret them. |
| TAG quality | Separate Good, BadCommunication, Other and No sample. Inspect the affected TAG even if others are Good. | 1 point has no sample | No sample is not zero; Good does not prove a physically accurate sensor. |
| Protocol details | Read what that driver actually provides and report the exact message. | Protocol-specific error | Missing extra detail does not justify inventing a diagnosis; fields vary by driver. |

### Test network reachability

Network reachability (server-side) attempts TCP and ICMP against one destination. The origin is the server host, not your browser; localhost/127.0.0.1 means that environment, including a container where applicable. Test only the necessary authorized target. The panel uses a 3-second timeout per attempt; TCP/ICMP are separate results and total elapsed time may be longer. The action requires Engineering modification permission and can be rate-limited.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Host or IP | Enter one DNS name or IP without protocol, path, password or range. | 127.0.0.1 in a local exercise | No http://, URLs or scanning. Check the test origin. |
| Single port | Enter the expected service TCP port. Changes only the probe target, not product configuration. | 502 for a known Modbus TCP service | Integer 1 through 65535; not a UDP or serial test. |
| TCP | Connected means a TCP connection was accepted; Refused means rejection; Timeout means no completion before the deadline. | Connected | No authentication, protocol handshake or register read. |
| ICMP | Success confirms an ICMP response. Timeout/Unavailable may reflect network or host policy. | Unavailable while TCP is Connected | Blocked ICMP does not prove TCP unavailable; assess each result. |
| Probe duration and time | Situate the observation and repeat purposefully after addressing the cause. | Observed now | Not future availability or full process latency. |

### Interpretation example

A Modbus source reports timeouts and a TAG has no sample. TCP Connected with ICMP Unavailable proves only that a port accepted the connection; not a Modbus unit, register or value. Check host/port/unit, destination permissions and point binding. If API/DriverHost evidence is stale, recover that observation before asserting driver health. After correction compare fresh samples and TAG feedback. This excerpt is a technical request for a local exercise, not a scanning script or product port change.

```json
{
  "host": "127.0.0.1",
  "port": 502,
  "timeoutMilliseconds": 3000
}
```

### Other failures and support evidence

For permission denied, check account and exact action; do not change user to conceal it. For revision mismatch, compare Working, Published and Active. For a Script error, read its diagnostics and input types/values; a healthy network does not validate syntax. If a screen differs from the TAG, check binding, scale, unit and quality. Record time, project/active revision, source/TAG, message, two observations and reproduction steps. Exclude passwords, tokens and keys from screenshots/support tickets. A symptom record is evidence, not a confirmed cause.

[Open this guide in installed Help](/help?topic=diagnostics.overview)
