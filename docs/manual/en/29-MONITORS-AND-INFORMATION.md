# Monitors and Information

## Development Monitor: watch references

Build a watch list of TAGs, memories and diagnostics without writing or forcing values.

### Choose this monitor to compare variables

In Engineering, open Development Monitor. It brings selected references into a read-only table. The catalog uses the open engineering project, but TAG and Server Memory values come from accessible Runtime; a Working definition does not create an active sample. Client Memory belongs to this browser context. System/driver states are diagnostics, not physical measurements. Use TAG Monitor for point details/history and communication diagnostics for connection health.

### Add and observe

1. Check the open project and active application.
2. In Browse project references, search/expand families and folders and select the desired item to add it.
3. Alternatively use Exact quick-add, then Add or Enter. It requires an exact reference/path, not a substring.
4. Read Source, Type, Value, Quality / State and Last update together.
5. Add references to compare and wait for new observations.
6. Remove a row or Clear the list when finished. Neither action deletes project TAGs or definitions.

### Fields and actions

Use the reference browser to avoid typing and resolve ambiguous names. The watch list is kept in the browser session; it is not published configuration or guaranteed persistence after that session closes.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Exact quick-add | Accepts a complete path/reference. A shared label is ambiguous; select in the reference browser. | Plant.Motor.Status | Does not create a TAG or perform substring search; missing/ambiguous input reports a message. |
| Browse project references | Search/family reference browser for existing TAG, memory, system, driver or asset references. | Select in the reference browser | Catalog presence does not guarantee a live value; assets do not become process signals. |
| Name / Reference | Row label and reference. Verify the resource before comparing. | Plant.Motor.Status.03 | A bit view is not a new physical TAG. |
| Source | Distinguishes TAG, Server Memory, Client Memory, System, Driver and Asset. | Client Memory | Local browser memory is not another client value. |
| Value | Available value or a dash when absent/not representable. | 8 or true | A dash is not zero/false; Int64 must retain precision. |
| Type | Reference/sample type; a bit view is Boolean. | Int16 → Boolean bit | Observation does not convert the original definition. |
| Quality / State | TAG quality; local/diagnostic references can show LocalSession, Available or Unavailable. | Good / LocalSession | Different concepts; local state is not physical Good acquisition. |
| Last update | Source timestamp where available, otherwise observation time. | Observed read time | Local memory does not gain a sensor timestamp; check origin. |
| Remove | Removes a reference from the watch list. | Remove one row | Does not delete resource, sample or history. |
| Clear | Empties this session watch list. | Table without entries | Does not delete the application or stop acquisition. |

### Example: observe a word bit

An Int16 word with value 8 has bit index 3 on: 8 = 2³. Suffix .03 selects that zero-based bit; .00 is the first bit. Add the word and bit view for comparison. The base type must support bit selection within its width. .03 is not a decimal digit, coil address or separate field. Names in this example are fictional; the TAG must exist with a valid integer sample. Observing a bit does not write the word.

```json
{
  "reference": "Plant.Motor.Status",
  "dataType": "Int16",
  "value": 8,
  "bitReference": "Plant.Motor.Status.03",
  "expectedBit": true
}
```

### Missing values or updates

Not found: check path/project; ambiguous names require explicit selection. Working-only definition: confirm Active before expecting values. Unavailable/no sample: check permission, server, source and quality; do not hide absence with zero. The table combines batched updates and realtime; disconnection does not prove the last value remains current. Client Memory observes this local context, not another Runtime. Investigate the resource diagnostics and record reference, state and time. This monitor offers no write/force command.

[Open this guide in installed Help](/help?topic=engineering.development-monitor)

## TAG Monitor: inspect value, quality and history

Check whether a variable is arriving, whether its value is valid and which recent samples were stored.

### Start with the running value

A TAG is a variable, such as pump pressure. TAG Monitor checks it without relying on the screen drawing. Open Engineering, Diagnostics, TAG Monitor. This is read-only: it does not change/force values, even for a TAG labeled Writable.

### Find and check a TAG

1. Check project and Active identity at the top: this identifies the application supplying values.
2. Search by part of name/path.
3. If missing, clear Quality/Access filters and try again.
4. Select the TAG. Read Current value, Quality and times together. A number alone says neither valid nor recent.
5. Review Recent history to compare stored samples in the shown interval. Refresh history queries again. This panel has no window adjustment; use History for other periods.

### Example: you edited but the value stayed the same

You are editing revision 12 while Active identity shows revision 11. The monitor reads the version still running. That is expected: editing/saving does not replace Active. When ready, follow Save, Publish and Activate, then check identity and reading again. If simulation is shown, values come from that test mode, not physical equipment.

### Understand context and filters

Working is the work open for editing. Active is the running revision. They may differ. Context fields help check that the editing project and observed values are the versions you intended. Use these tables to look up a field when needed.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Engineering context | Open editing project. | Station project | Does not guarantee this project is active. |
| Working revision | Base revision loaded in Engineering; unsaved edits can differ. | Revision 12 | Not automatically the running revision. |
| Active identity | Observed Runtime project/revision, simulation or no Active. | Project · rev 11 | Unavailable identity is not a confirmed comparison. |
| Working vs Active | Aligned/different guides comparison; read mode/unavailability notices. | Working differs from Active | Save does not imply Publish/Activate. |
| Activated at | Reported revision activation time. | Activation time | Not sample time. |
| Search TAGs | Filters visible TAG metadata/value. | Part of path or unit | A TAG may exist but be invisible to your account. |
| Quality | All, Good, Attention, Bad or No sample finds the relevant condition. | No sample | Does not change quality; no matches may reflect filters. |
| Access | All, Read-only or Writable according to TAG attribute. | Writable | Not effective write permission; panel stays read-only. |
| Refresh | Reloads the list. Also observe realtime/periodic status. | Fresh query | Does not restart acquisition or clear history. |

### Understand value, quality and times

Current value is the available reading. Quality describes how the system classifies it. Times indicate when it was produced or received. A query that answered now may return an old sample; also inspect Value freshness and sample time.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Current value | Displayed value with unit where configured. | Measured pressure | No sample is not zero; appearance does not prove health. |
| Quality | Sample condition such as Good or BadCommunication. | BadCommunication | Good alone proves neither sensor calibration nor recent updates. |
| Type and unit | Data type and configured unit. | Double · bar | A unit label does not convert sensor physics. |
| Source / Data Source | Point source; compare with intended device. | Configured source | Not the screen name. |
| SCADIVO timestamp | Time associated with the displayed Runtime sample. | Sample time | Compare with other timestamps; not page-opening time. |
| Source timestamp | Source-provided time where available. | Device-reported time | May be absent; clocks/driver require context. |
| Server timestamp | Server time where present in the sample. | Server time | Does not mean the sensor measured then. |
| Value freshness | Fresh, aging, stale or without update timestamp, with Reason. | Stale data | Successful request is not necessarily a new sample. |
| Last request / valid response | Separates query failure from a sample that does not advance. | Response received now | May return an existing value; check sample time. |
| Path, stable ID and description | Identifies the resource for comparison/diagnosis. | Readable TAG path | No ID to fill in; do not change identity to fix a fault. |
| Recent history | Bounded query of the selected point; default 15-minute window, up to 120 requested samples. | Recent interval values | Not all history or retention/capture configuration; no editable window control here. |
| Refresh history | Queries the selected point and recent history again. | New interval query | Does not create absent samples or force recording. |

### Missing value or history

Clear search/filters first. For access denied, ask for the resource permission check. No sample is not zero; inspect source diagnostics. If only history is empty, check recording and whether samples existed in that period; Historian stores those samples. Realtime connected describes update transport, not sensor health. Read periodic-update notices when that connection is unavailable.

[Open this guide in installed Help](/help?topic=engineering.tag-monitor)

## Information: identify product and project

Find the installed version and distinguish product version, schema and application revision.

### When to use it

In Engineering open Information to identify product and engineering context for support, version comparison or package analysis. It is read-only. It does not activate a revision, update software, change schema or reveal license consumption; those have separate workflows.

### Read and interpret

1. Check Product version. If unavailable, record that condition rather than infer from project name.
2. Open Technical details for package/context comparison or support.
3. Check Project, Base revision and Loaded snapshot.
4. Inspect Active in TAG Monitor/Runtime to learn what is running.
5. Report version, project/revision, time and message; avoid publishing private application details.

### What each item means

The system supplies these items; users do not need to fill them in.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Product version | Installed SCADIVO version, distinct from project revision. | Displayed product version | Does not alone identify the active application or revision. |
| Engineering schema | Engineering package format identity/version. | Format identity | Not PostgreSQL version or project revision number. |
| Base revision | Revision underlying loaded Working; can be unsaved. | Displayed base revision | Later edits may not be saved/published/active. |
| Loaded snapshot | Time of the loaded engineering snapshot. | Displayed snapshot time | Not sensor time, full backup or activation evidence. |
| Project | Name/key identifying the open Engineering context. | Project name | Check Active separately; readable name is not a license key. |
| Build commit | Technical build identity when provided. | Displayed identity | May be absent; do not invent an identifier or confuse it with application revision. |

### Comparison example

Two stations can share product version but have different projects/revisions. They can also share a project with different product versions. Compare each item in its own role and confirm Active before blaming a screen, driver or license.

### Missing information

Unavailable version or absent commit is not manually filled in by the operator. Record it and ask the installation owner to check. For consumption/capacity use Licensing; for value/quality use TAG Monitor; for communication use Diagnostics. An identification field does not establish a process defect.

[Open this guide in installed Help](/help?topic=engineering.information)
