# Alarms and events: configuration and operation

## Alarms: configure condition, priority and behavior

Configure a notice for a known condition and test what the operator will see. The example uses high pressure without commanding equipment.

### Before you begin

Start with one simple condition: notify when pressure reaches its high limit. In Engineering > Alarms, use a test TAG whose value, type, scale and unit you have checked. A TAG is the variable providing the reading.

The alarm observes that variable; it does not configure the sensor or stop the pump by itself. Agree the limit and operating response with the process owner. For learning, use simulation with the applicable Engineering permission and lock, without acting on real equipment.

### Choosing a type

Choose the type by the question the alarm must answer:

Digital follows a boolean: choose whether true or false means an alarm condition. High and HighHigh activate when the value is greater than or equal to the limit (≥). Low and LowLow activate when less than or equal (≤). For two limits, create two definitions and choose each priority separately.

Communication follows reading quality: it activates when quality differs from Good and returns with Good. It uses no setpoint, the numeric limit, and selecting it alone creates no communication-loss timer. With poor quality, process alarms do not treat the number as a valid reading; use Communication to follow that condition.

System appears in the picker but has no generic process rule in this evaluator. For a high limit or boolean state, choose its own type rather than System as a substitute.

### Procedure: test a high threshold

Here, 10 bar is only a teaching value. ACK means acknowledgement: the operator confirms awareness of the notice.

1. Create a high alarm for the simulated pressure TAG, with setpoint 10. Check the TAG’s bar unit.
2. Fill priority, area and a clear message. Keep Requires acknowledgement checked for this test.
3. Run Preview, read errors and apply to Workspace, the editing draft. If you edit again, validate again.
4. Save, publish and activate the correct revision. Applying to Workspace does not activate Runtime by itself.
5. With Good quality, send 9: the condition is Normal. Send 10: it becomes Active. Equality also activates.
6. Acknowledge in Runtime while keeping the TAG at 10: it becomes Acknowledged, but pressure has not changed. Send 9: it becomes Returned and leaves the active list because it already had ACK.
7. Activate again with 10 and return to 9 without acknowledging: Returned remains pending. Acknowledge and check that it leaves the active list. Query history for previous transitions.
8. To test the unchecked option, publish a revision with Requires acknowledgement off. Activate at 10 and return to 9: the state remains Returned without ACK, but leaves the active list. The full view still shows Returned; if the TAG returns to 10, a new Active cycle appears.

### Identity and condition

Select New alarm or an existing definition. The path identifies the signal to evaluate; the name and message help operators understand the problem.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Name | Use a readable description distinguishing equipment and condition. Renaming does not change the observed TAG. | High pressure — Pump 01 |  |
| TAG path | Enter an existing TAG path. Check its already-scaled value in TAG Monitor before choosing the threshold. | Planta.Bomba01.Pressao | Not a Driver address or screen name. |
| Type | Chooses the comparison rule; consult the type explanation in this article. HighHigh is another limit definition, not an automatic priority. | high |  |
| Setpoint (threshold) | Shown for analog types. Enter a number in the current TAG unit. High/HighHigh activate at value ≥ threshold; Low/LowLow at value ≤ threshold. | 10 bar: enter 10 | Equality also activates. This editor has no hysteresis field. |
| Active digital value | Only shown for Digital. Checked means true is the alarm condition; unchecked means false. Verify actual signal polarity. | Fault=true / Ready=false |  |
| Priority | Orders and highlights attention: low, medium, high or critical. Does not change the threshold, permission or shutdown logic. | high |  |
| Class | Classification label for organizing definitions. Typing a name does not install a protection rule. | Process |  |
| Area | Process context also used for area-scoped action authorization. Use the name expected by the policy; entering an area grants no access. | Estacao01 |  |
| Equipment link | Optional. Choose the equipment that produces or is monitored by this alarm. Runtime uses its current location, so the suppression scope follows the equipment if it moves. | Pump 01 | Do not also choose an operational area or subarea. |
| Operational area or subarea | Optional. Choose a location in the operational hierarchy when this alarm is not linked to equipment. A suppression at this location also covers its child locations. This is different from the Area field used by access policy. | Plant / Pump station | Disabled while an equipment link is selected. |
| Message | Explain the condition and location usefully for the operator. A written instruction does not execute a command. | Pump 01: check discharge pressure |  |

### Behavior and sound

Expand Alarm behavior. The delay starts when the condition is observed and activates the alarm only after the configured interval. Requires acknowledgement controls whether a Returned alarm without ACK remains in the active list: checked keeps it pending; unchecked removes it without recording a synthetic ACK.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Enabled | Includes the definition in evaluation when the revision is activated. Disabling fixes neither the signal nor the physical cause. | Checked |  |
| Activation delay (ms) | After the condition becomes active, Runtime waits for this interval before activating the alarm. If the condition returns to normal or quality becomes non-Good before the interval ends, the timer is canceled. For communication alarms, non-Good quality is the active condition. | 1000 ms = 1 s | Non-negative integer; 0 or blank activates immediately. A pending timer starts over after Runtime restarts. |
| Requires acknowledgement | Controls whether return requires ACK to leave the active list. Checked keeps Returned without ACK pending. Unchecked removes it without creating an ACK; the full view still shows Returned. Reactivation starts a new Active cycle. | Checked |  |
| Allow operational suppression | Requires AlarmShelve in Authority and access to the canonical scope of the linked equipment/location when the grant is scoped. The condition and state remain visible in Runtime. | Checked for alarms that may be silenced during an intervention | Does not acknowledge, disable or change the process condition. |
| Sound profile / preview | Choose none, short pulse, double, triple, rising or alternating and try the preview. In Runtime, operators must enable sounds in the browser. Volume, output device and browser permission affect playback. | double | Muting in the browser does not acknowledge the alarm. |

### If the result differs

Check the TAG/active revision, scaling/unit, boolean polarity, enablement and quality. High priority does not force activation. A condition that returns to normal before the interval ends cancels the timer; this does not add hysteresis to a chattering threshold. Communication alarms treat non-Good quality as their active condition. With Requires acknowledgement checked, Returned without ACK stays in the active list; when unchecked, it leaves without a synthetic ACK. Preview validates a candidate; it does not prove field acquisition. If the project changes while editing, reload and validate again rather than applying an old candidate.

[Open this guide in installed Help](/help?topic=alarms.configuration)

## Runtime alarms: states, acknowledgement and sounds

Read the current condition, acknowledge it and distinguish silence, return and history.

### Where to look and how to interpret

Runtime Alarm Center helps identify what needs attention now. It shows the active list allowed for your session, counts and selected-row details. It is not the history of everything that happened.

Select a row and check equipment, message, priority, area, value and times. Details also show who acknowledged and when. IDs identify the exact definition for lookup or support; acknowledging does not require inventing or typing an ID.

### States shown in details

ACK confirms that the operator is aware of the alarm. It is separate from the physical condition, so an acknowledged alarm can still be active.

The active list includes Active and Acknowledged. Returned without ACK is also listed when Requires acknowledgement is checked. When unchecked, it leaves the active list without receiving an ACK; the full view still shows Returned. The table explains the states shown in details.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Normal | Condition is not active now; a previous cycle may exist in history. | 9 < 10 |  |
| Active | Condition is present and this cycle is not yet acknowledged. | 10 ≥ 10 |  |
| Acknowledged | ACK recorded but the condition remains present. It does not mean the cause was resolved. | ACK while pressure remains 10 |  |
| Returned | The condition is no longer active. Without ACK it remains in the active list only when Requires acknowledgement is checked. When unchecked, it leaves that list and remains Returned in the full view. | 10 → 9 |  |
| Disabled | Definition disabled for evaluation. Does not indicate healthy equipment. | Enabled unchecked |  |
| Shelved | Shelved is the state of the existing shelving operation. The panel’s operational suppression is a separate layer and preserves states such as Active and Acknowledged. | Controlled maintenance | Do not use this state to identify operational suppression. |

### Operational suppression

In Alarm Center, open Operational suppression. Choose Alarm to affect one definition, or Area or subarea to cover the selected location and its child locations. An equipment link follows the equipment’s current location; coverage follows explicit parent relationships, not names or path prefixes. Choose a reason and duration: until manually released, or 1, 4 or 8 hours. AlarmShelve permission and Allow operational suppression must authorize every affected alarm; if any one does not allow it, nothing is applied. Access is checked against the stable equipment or location link in Authority. The operational location hierarchy determines which alarms are affected; Authority’s separate scope hierarchy determines who may act. If an alarm has no resource linked to an Authority scope, a scoped grant does not authorize it. The Area field remains alarm context, but entering text there does not grant access. The list continues to show every alarm and its real condition (Active, Acknowledged or Returned); only audible annunciation is suppressed. ACK, evaluation, commands and history are unchanged. When suppressions overlap, the panel shows every cause; releasing one does not release the others. Expiry takes effect automatically at the selected time. In HA with separate databases, a node may take over only after restoring the peer’s suppression snapshot; a missing snapshot fails closed.

### Procedure: acknowledge a notification

1. Select the row. Read condition, value and time and confirm the equipment.
2. Follow the plant’s operating response for that condition.
3. Use Acknowledge alarm if your session permits it for that area. The technical permission is AlarmAcknowledge; a View Only session still cannot perform it.
4. Wait for confirmation and list refresh. If the server accepted ACK but rereading failed, refresh to check before repeating actions.
5. If the condition remains, the acknowledged alarm stays visible. If it has returned, it leaves the active list after ACK. A new activation creates another acknowledgement pending.

### Sounds, mute and unavailability

Enable sounds applies only to this browser session. Mute is local too. Neither option acknowledges or changes an alarm. Shelved is a state of the existing shelving operation; the operational suppression described above keeps the alarm visible with its real state. Acknowledging does not send a process command. These actions are different.

To hear notices, use Enable alarm sounds in this browser session. Check support, volume and audio output. Profiles repeat while an alarm is pending; Returned without ACK stays pending only when Requires acknowledgement is checked.

For denied access, check login, session class and the relevant Authority grant. For a rejected suppression, check AlarmShelve and the stable equipment or location link. For missing data or unavailable service, refresh and consult Runtime diagnostics; changing a TAG does not resolve an authorization refusal. For past records, open History and choose alarm.events for the period. This dataset concerns alarms; operational.events and Audit serve other purposes.

[Open this guide in installed Help](/help?topic=alarms.runtime-operation)

## Operational events: define, emit and query

Record a process occurrence with context, distinguishing definitions, occurrences, alarms and Audit.

### Choose the right resource

Use an operational event to record a fact, such as batch start or a mode change. Each emission creates an occurrence with its own time and identity. It maintains no active condition and requests no ACK.

Choose an alarm to follow a condition that activates and returns. Audit tracks security or administration actions and outcomes. A screen click is also called an event, but does not become an operational occurrence by itself.

First register the fact’s definition, then configure what emits it. Registration does not automatically emit. Associating a TAG only provides context; it creates no change trigger.

### Procedure: prepare the definition and producer

1. Create the definition with required fields and a name/key distinguishing the fact. Add useful context.
2. Run Preview and fix duplicate keys or invalid references reported in diagnostics.
3. Apply to Workspace, then save, publish and activate the revision through project controls.
4. Prepare what will emit the occurrence. A Server Script can call emit_operational_event. Obtain the saved definition’s stable ID from project references or exported data; do not substitute its textual identifier.
5. Associate the handler, the Python function, with the appropriate Server trigger. Avoid Timer for a single transition: each call creates another occurrence, even with the same message.
6. Activate the revision containing the definition and Script.
7. Produce one controlled test fact and query operational.events in History, including execution time in the period.

### Python example: one occurrence at initialization

This example records simulation initialization, not a pump’s physical start. The illustrative UUID is a definition ID: replace it with your saved definition’s stable ID.

In the Script editor, choose Server and associate Initialize with handler initialize. Writing the function does not create that association. It runs at each Script initialization; event carries trigger context and may be unused.

emit_operational_event(definition_id, message=None, context=None) returns None, not the occurrence ID. In the context dictionary, keys and values are text. Use str(value) to turn a calculated number into text before including it.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

### Definition fields

Open Engineering > Operational Events and select New Operational Event. Define meaning and source first; expand Optional context when needed. Type/category/source labels are text from your convention, not connection settings.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Display name | Readable name for people. | Simulation start | Up to 240 characters. |
| Key | Text key unique across definitions, case insensitive. Not the stable ID used by the Python API. | simulation.started | Required; up to 160 characters. |
| Type | Describes the nature of the fact; does not select an alarm type. | process | Required; up to 120 characters. |
| Category | Groups occurrences using your convention for search and analysis. | operation | Required; up to 120 characters. |
| Source | Semantically identifies what produces the fact. Entering server-script does not automatically create or bind a script. | server-script | Required; up to 240 characters. |
| Enabled | Controls definition availability in the Runtime revision. Does not remove historical occurrences. | Checked |  |
| Default message | Text used if emission supplies no other message. An emission message may override it. | Simulation started | Up to 4000 characters. |
| Area (optional) | Places the fact within the process organization; grants no user permission. | Estacao01 | Up to 240 characters. |
| Equipment path (optional) | Equipment context reference. Does not create equipment or a drawing. | Planta.Bomba01 | Up to 500 characters. |
| TAG (optional) | Choose an existing TAG; the editor stores its identity and path. No TAG is valid for a general fact. | Planta.Bomba01.Estado | Association does not automatically monitor changes. |

### Query and troubleshoot

In History, choose operational.events, the operational-occurrence dataset. Adjust start/end and filters and check time, source, message and context. alarm.events contains alarm transitions; Audit records a different kind of action.

Extra context does not change definition type, category, source or identity, even with keys using those names. Up to 128 pairs are allowed, with keys up to 160 characters and values up to 4000; messages also have a 4000 limit. Include no passwords or secrets.

For a missing occurrence, check active revision, enabled definition, ID, handler connection, Script diagnostics, period and permission. Registering the definition alone produces no records. Emission failure does not authorize another route: emission must respect Runtime’s active authority.

[Open this guide in installed Help](/help?topic=operational-events.configuration)
