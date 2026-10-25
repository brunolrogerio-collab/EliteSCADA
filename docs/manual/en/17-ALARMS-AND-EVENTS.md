# Alarms and events: configuration and operation

## Alarms: configure condition, priority and behavior

Build an alarm from a TAG and understand what each option actually does.

<!-- HELP-REVIEW-AFTER-PRODUCT-751: https://github.com/brunolrogerio-collab/EliteSCADA/issues/751; topic=alarms.configuration; fields=alarm.activationDelayMilliseconds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-753: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.configuration; fields=alarm.requiresAcknowledgement; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Before you begin

Open Engineering > Alarms. First verify the TAG reading, type, scaling and unit. A high-pressure alarm observes the TAG value; it neither configures the sensor nor stops the pump by itself. Agree on thresholds and operational response with the process owner. Learn with a simulation TAG without operating real equipment. Editing requires the applicable Engineering permission and lock.

### Identity and condition

Select New alarm or an existing definition. The path identifies the signal to evaluate; the name and message help operators understand the problem.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Name | Use a readable description distinguishing equipment and condition. Renaming does not change the observed TAG. | High pressure — Pump 01 |  |
| TAG path | Enter an existing TAG path. Check its already-scaled value in TAG Monitor before choosing the threshold. | Planta.Bomba01.Pressao | Not a Driver address or screen name. |
| Type | Selects the comparison rule; see the types below. HighHigh is another threshold definition, not automatic priority. | high |  |
| Setpoint (threshold) | Shown for analog types. Enter a number in the current TAG unit. High/HighHigh activate at value ≥ threshold; Low/LowLow at value ≤ threshold. | 10 bar: enter 10 | Equality also activates. This editor has no hysteresis field. |
| Active digital value | Only shown for Digital. Checked means true is the alarm condition; unchecked means false. Verify actual signal polarity. | Fault=true / Ready=false |  |
| Priority | Orders and highlights attention: low, medium, high or critical. Does not change the threshold, permission or shutdown logic. | high |  |
| Class | Classification label for organizing definitions. Typing a name does not install a protection rule. | Process |  |
| Area | Process context also used for area-scoped action authorization. Use the name expected by the policy; entering an area grants no access. | Estacao01 |  |
| Message | Explain the condition and location usefully for the operator. A written instruction does not execute a command. | Pump 01: check discharge pressure |  |

### Choosing a type

Digital compares the boolean with the selected active value. High and HighHigh use ≥; Low and LowLow use ≤. For two thresholds, create two definitions and choose their priorities separately. Communication activates for quality other than Good and returns with Good; it does not use a setpoint. With bad quality, process types do not evaluate the numeric limit as a valid reading: use a communication alarm to monitor this. System appears in the selector but has no generic process rule in this evaluator; do not use it instead of High or Digital. Selecting Communication does not imply a communication-loss timer.

### Behavior and sound

Expand Alarm behavior. Some options are saved in the definition but have current Runtime limitations, described alongside the field.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Enabled | Includes the definition in evaluation when the revision is activated. Disabling fixes neither the signal nor the physical cause. | Checked |  |
| Activation delay (ms) | Represents the intended delay. Current limitation: the value is saved but does not delay Runtime activation. Do not rely on it to filter chatter. | 1000 ms = 1 s | Integer field; delay not yet applied. |
| Requires acknowledgement | Expresses the intent to require operator ACK. Current limitation: clearing it does not remove pending acknowledgement in Runtime, including after return. | Checked |  |
| Allow shelving | Allows this definition to enter a suppressed state through an available operation. The action still requires AlarmShelve permission. The Alarm Center described here has no shelving button. | According to plant procedure | Not Mute sounds; no automatic expiry is promised. |
| Sound profile / preview | Choose none, short pulse, double, triple, rising or alternating and try the preview. In Runtime, operators must enable sounds in the browser. Volume, output device and browser permission affect playback. | double | Muting in the browser does not acknowledge the alarm. |

### Procedure: test a high threshold

1. Create a high alarm for the simulated pressure TAG with setpoint 10 and bar as the TAG unit.
2. Fill in priority, area and a clear message. Keep Requires acknowledgement checked for this test.
3. Run Preview, read errors and apply to Workspace. Any further edit requires validation again.
4. Save, publish and activate the correct revision; applying to Workspace alone does not activate Runtime.
5. With Good quality, send 9: the condition is Normal. Send 10: it becomes Active because equality reaches the threshold.
6. Acknowledge in Runtime: it becomes Acknowledged while the TAG remains
10. Send 9: it becomes Returned and leaves the active list because ACK was already recorded.
7. For another activation, send 10 then 9 without ACK: it becomes Returned pending acknowledgement. Acknowledge and verify removal from the active list. Use history for earlier transitions.

### If the result differs

Check the TAG/active revision, scaling/unit, boolean polarity, enablement and quality. High priority does not force activation. A chattering threshold gains no hysteresis from a different message or class. Preview validates a candidate; it does not prove field acquisition. Delay and ACK exemption retain the limitations described. If the project changes while editing, reload and validate again rather than applying an old candidate.

[Open this guide in installed Help](/help?topic=alarms.configuration)

## Runtime alarms: states, acknowledgement and sounds

Read the current condition, acknowledge it and distinguish silence, return and history.

<!-- HELP-REVIEW-AFTER-PRODUCT-753-RUNTIME: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.runtime-operation; fields=alarmState.returned; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Where to look and how to interpret

Open Runtime Alarm Center. It shows the active list visible to your session, counts and details for the selected item. It is not a list of all past occurrences. Select a row to inspect message/name, priority, area, last value, activation, last transition and who/when acknowledged. IDs in details identify the exact definition; you do not need to invent or type an ID to acknowledge.

### States shown in details

The active list includes Active, Acknowledged and Returned without ACK. An empty list therefore does not mean no alarm ever occurred. States outside it can be found through other appropriate queries.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Normal | Condition is not active now; a previous cycle may exist in history. | 9 < 10 |  |
| Active | Condition is present and this cycle is not yet acknowledged. | 10 ≥ 10 |  |
| Acknowledged | ACK recorded but the condition remains present. It does not mean the cause was resolved. | ACK while pressure remains 10 |  |
| Returned | Condition is no longer active. Without ACK it remains pending in the active list. After ACK it leaves that list. | 10 → 9 |  |
| Disabled | Definition disabled for evaluation. Does not indicate healthy equipment. | Enabled unchecked |  |
| Shelved | Notification suppressed by shelving. Value and underlying state continue to be tracked; removing suppression restores the current underlying state. | Controlled maintenance | The current center provides no button for this action. |

### Procedure: acknowledge a notification

1. Select the row and read the condition, value and time; confirm the equipment.
2. Follow the plant operational response.
3. Use Acknowledge alarm if the session has AlarmAcknowledge permission for the area. A View Only session does not gain this action.
4. Wait for server confirmation and refresh. If ACK was accepted but refresh failed, refresh to verify the result before repeating actions.
5. If the condition persists, the acknowledged state remains visible. If it already returned, it leaves the active list after ACK. A new activation opens another pending acknowledgement.

### Sounds, mute and unavailability

Enable alarm sounds unlocks audio for this browser session, subject to support, volume and output device. Configured profiles repeat while an alarm awaits ACK, including Returned without ACK. Mute sounds is local and does not acknowledge, disable or shelve. Acknowledgement sends no process command. If access is denied, check login, session class and permission/area; do not try to fix access by changing the TAG. If data or service is unavailable, refresh and check Runtime diagnostics. For past transitions, open History and choose alarm.events with an appropriate period; do not confuse it with operational.events or Audit.

[Open this guide in installed Help](/help?topic=alarms.runtime-operation)

## Operational events: define, emit and query

Record a process occurrence with context, distinguishing definitions, occurrences, alarms and Audit.

### Choose the right resource

An operational event records a fact, such as batch start or mode change. Each emission creates an occurrence with its own time and identity; it maintains no active condition and requires no ACK. An alarm tracks a condition that activates/returns. Audit traces security/administrative actions and outcomes. A visual object click is also called an event but is not itself an operational occurrence. Creating the definition below emits nothing automatically; linking a TAG provides context, not a change trigger.

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

### Procedure: prepare the definition and producer

1. Create the definition with required fields and useful context; avoid using one generic key for every fact.
2. Run Preview and read diagnostics. Fix duplicate keys and invalid references.
3. Apply to Workspace, then save, publish and activate using the project lifecycle.
4. Configure the producer: a Server Script can call emit_operational_event. Obtain the saved definition stable ID from exported project data/references; do not substitute the text Key.
5. Bind the Python handler to the appropriate Server trigger. Avoid Timer to record a single transition: every call creates another occurrence even with the same message.
6. Activate the revision containing both definition and script.
7. Produce a controlled fact and query operational.events in History with a period covering execution.

### Python example: one occurrence at initialization

The UUID below is illustrative: replace it with your definition stable ID. In Script Engineering choose Server scope and bind the Initialize event to the initialize handler; a function name alone does not create that binding. The example records simulation start at each script initialization, not a physical pump start. The event argument is trigger context and can remain unused. emit_operational_event(definition_id, message=None, context=None) returns None, not an occurrence ID. Context dictionary keys and values are strings: use str(value) for calculated numbers.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

### Query and troubleshoot

In History select operational.events, set start/end and available filters; inspect time, source, message and context. alarm.events contains alarm transitions, while Audit has another purpose. Additional context does not change the definition's own type, category, source or identity, even with keys bearing those names. Context allows up to 128 pairs, keys up to 160 characters and values up to 4000; messages also have a 4000-character limit. Include no passwords or secrets. Missing occurrence: check active revision, enabled definition, correct ID, handler binding, script diagnostics, time range and query permission. Emission must respect active Runtime authority; failure does not authorize an alternative path. Nothing appearing after only creating a definition is expected.

[Open this guide in installed Help](/help?topic=operational-events.configuration)
