# Asset and Process Intelligence

This chapter describes the workflows available today. Intelligence is optional: the license enables the feature, but you configure the Equipment, profiles, and stages you intend to analyze. You do not need to apply analyses to the entire plant.

**Contextual Help:** `/help?topic=intelligence.overview`.

## 1. Start with Equipment and TAGs

Before creating a profile, identify the stable Equipment resource and TAGs that represent useful measurements, such as current, voltage, pressure, flow, or temperature. Check unit, type, quality, timestamp, and Historian capture for the period to be analyzed. Do not include a signal merely because it is available; state which operational question it helps answer.

Editing requires `EngineeringModify` and an explicit `AssetProcessIntelligenceManage` grant, global or covering the resource. Intelligence features also require the corresponding license.

## 2. Create a measurement profile and assignment

1. Open Engineering > Asset Intelligence and create a Measurement profile with an identifiable name/key.
2. Add the TAGs that describe the Equipment. Mark each signal required or optional and complete any requested semantic qualifiers; a label does not replace the unit or actual TAG reference.
3. Save the configuration to Working.
4. In assignments, choose the existing Equipment and profile. One Equipment identity can use more than one profile.
5. Review missing and duplicate references before saving and following the normal project lifecycle.

The profile organizes signal references and meaning. It does not create TAGs, enable Historian capture by itself, or prove that instruments are calibrated.

## 3. Configure operating context

Context distinguishes operating conditions that should not be compared as if they were the same—for example, flow ranges or modes indicated by TAGs. Create a context for the correct Equipment assignment and add predicates for TAGs in the profile.

Predicates can represent an inclusive numeric interval or Boolean, string, or enum equality. Numeric bounds are stored in the current Engineering unit; if the unit changes, review and explicitly reconfirm the bounds. For a query to use the context, predicates must match the required temporal evidence at the same synchronized, exact source-origin UTC timestamp. Without a match, the analysis may have no eligible context; do not weaken the criterion just to force a result.

## 4. Organize process stages

Use **Ordered process stages** to build the sequence you want to monitor. Create each stage with a stable key and readable name; add Equipment/profile assignments and explicitly set their order. A stage organizes members but does not itself infer causality, balance, or an aggregate process KPI.

Save the configuration to Working. Runtime data and analyses remain subject to the Active revision and current authorization.

## 5. Request analyses

Select an assigned Equipment and choose an available analysis, such as profile coverage, performance, or contextual deviation. Set the time range and explicitly request the query. Contextual deviation requires configured operating conditions and sufficient temporal evidence. Analysis is read-only; it does not change a TAG, command Equipment, or replace Alarms.

Interpret results together with coverage, quality, timestamps, and each signal's limitations. Missing values, poor quality, unavailable history, or temporal misalignment do not mean zero, normal operation, or a confirmed fault. Current analyses are not a maintenance diagnosis or proof of physical efficiency. For stage members, inspect one Equipment at a time; do not combine independent results as a process score.

**Contextual Help:** `/help?topic=intelligence.process-stage-member-insights`.

## 6. Generate Screen drafts

Choose a profile or stage and generate an available Screen. Generation uses TAG metadata to create an editable Screen; it does not query live values or Historian and does not invent KPIs. Review the draft in the editor, run Preview, and use Apply to Workspace. Then save, publish, and activate through the explicit lifecycle steps. The generated Screen is not activated automatically.

## 7. Common problems

- **Resource blocked:** check the license, `EngineeringModify`, `AssetProcessIntelligenceManage`, and scope.
- **Incomplete profile/assignment:** check Equipment, profile, TAGs, and required qualifiers.
- **No coverage/history:** confirm that the TAG received useful samples with quality and timestamps and that capture existed during the period. Historian does not backfill data.
- **No context match:** review predicates, unit, interval, and the required temporal synchronization/equality.
- **Draft unavailable:** complete the selections required by that Screen type and use the normal editor to refine the result.
- **Result differs after editing:** save the correct configuration, check the Active revision, and explicitly run a new query.

**Related Help:** `/help?topic=historian.overview`, `/help?topic=tags.quality`, `/help?topic=tags.timestamps`, `/help?topic=screens.overview`, `/help?topic=engineering.lifecycle`.

<!-- installed-system-guides -->

## Intelligence: enable, pause and resolve blocks

Available licensing, selected resources and active execution are different conditions.

### Follow the full sequence

Open Engineering > Asset Intelligence. First create Equipment and TAGs, then signal profiles, Equipment/profile assignments, useful contexts and stages. Save configuration and selected scopes; publish/activate the revision. Then explicitly activate the resource for analysis. Creating a profile, checking a box or holding a license does not automatically arm execution.

### Permissions and resource selection

EngineeringModify permits configuration; AssetProcessIntelligenceManage must be explicit, global or covering the Equipment/Location. The Developer name or SystemAdmin alone does not replace that grant. Queries also need TrendUse and Intelligence licensing. Grant through Security and inspect effective session capabilities. Select resources rather than memorizing IDs, preserving their identity.

### Activate only where useful

1. Select Equipment/Locations under Eligible scopes.
2. Save scopes and configuration.
3. Publish/activate the revision and resolve pending changes before arming execution.
4. Use Activate on the resource and confirm its execution permit is armed.
5. Open an assignment query and choose the period.
6. Use Pause to stop analysis; pending work is cancelled without deleting TAGs/history. Pause before removing a resource from selection.

### Configured active does not mean armed

After authorization revocation the execution permit can stay closed while configuration remains active. Restoring a grant does not silently rearm work: inspect state and explicitly Activate again, including after restart when indicated. This avoids restarting analysis without an owner decision. Distinguish this from Historian errors or missing data.

### Example: start small

Start with Pump 01 in one area. Configure current and speed if available; do not invent missing vibration. Add measurements and analyses supported by their catalogs. Keep unused resources paused. Selective configuration controls eligible execution, not a fixed CPU/RAM promise.

[Open this guide in installed Help](/help?topic=intelligence.activation)

## Intelligence: signal meaning and units

Describe what was measured, not just the TAG name.

### Why a name is not enough

A TAG named Power may mean active/apparent power or a calculated value; pressure may mean suction, discharge or differential. Rules must not guess from names. In the profile select the controlled semantic role when required and complete its shown qualifiers. Legacy role text is descriptive only.

### Fields and choices

Choose only meaning supported by the instrument and process. Selectors list choices accepted by the current catalog.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Required | Marks signals whose absence compromises requested coverage. Do not mark essential signals optional just to obtain a result. | Current needed for comparison |  |
| Semantic role | Controlled quantity/function; analysis matches it rather than a similar TAG name. | Active input power, if measured |  |
| Current basis | Distinguishes the role-accepted basis, such as instrument-appropriate RMS current; do not infer from a generic label. | Select the documented instrument option |  |
| Signal kind | Distinguishes measurement, reference and the role-required function. A setpoint does not prove a measured value. | Actual measurement when required |  |
| Pressure reference | Identifies required basis/source; outlet-minus-inlet differential differs from discharge pressure alone. | Measured outlet − inlet differential |  |
| Flow direction | Identifies transfer direction; the opposite direction may invalidate a rule. | Pump to process, when applicable |  |
| Frequency reference | Identifies frequency meaning per catalog; distinguish electrical reference from physical speed. | Check instrument source |  |
| TAG and unit | Select the actual TAG and check its controlled engineering unit. A profile label change does not convert data. Some calculations require specific catalog units/versions. | kW for power when it matches actual data |  |

### Example: do not confuse a proxy with efficiency

Increasing current at the same speed may justify investigation but is not physical motor-efficiency calculation. Input electrical power, flow and differential pressure may enable other rules when roles, units and required alignment exist. If an instrument/qualifier is missing, read the unavailable-rule reason rather than entering fictional values.

[Open this guide in installed Help](/help?topic=intelligence.semantics)

## Intelligence: query results and build dashboards

Choose the question, check available data, then interpret the indicator.

### Start with coverage

With saved assignment and authorized execution, open profile coverage. Enter start/end and explicitly query. Form times are local and the response identifies the UTC window; end must follow start. Inspect required signals with data, accepted/rejected samples, quality and temporal alignment before min/max/mean. Sample counts alone prove neither cadence, completeness nor synchronized acquisition.

### Performance: choose an eligible rule

Open Performance and select a catalog rule. Disabled options explain missing signals, roles or units. Do not choose a different quantity just to obtain a number. Fill From/To, query and inspect status, unit, window and limitations. Unavailable is not zero; a proxy must not be labelled physical efficiency. Assignment/revision/parameter changes require a fresh query rather than reuse of an old number.

### Two context types with different bounds

Saved operating contexts use inclusive numeric bounds in the current engineering unit and evidence at the same exact UTC source timestamp. The contextual-deviation screen generator uses separate selections: 2–8 target signals and two distinct numeric context signals, different from targets. At least four usable numeric signals are needed. Generator ranges use the displayed controlled unit and lower ≤ value < upper. Do not automatically copy operating-profile bounds: units and upper-bound inclusion may differ.

### Generate the screen and check the full chain

1. Choose assignment/stage and screen type.
2. Complete required selections; read field reasons if blocked.
3. Generate the draft; this arranges objects/references without querying Historian or activating a screen.
4. Review name, dimensions, objects, bindings and navigation in the editor; Preview and Apply.
5. Save/publish/activate and open in Runtime.
6. Configure/run the object query and check window/result. A stage screen groups members without automatically creating an aggregate KPI or proving causality.

### How to investigate an empty result

Check in order: license/permissions; saved scope/armed permit; active project/revision; assignment/TAGs; period; Historian capture; quality; roles/units; alignment/context. Fix the first evidenced missing condition and query again. Do not broaden limits or change signal meaning just to make a result appear. Analyses support investigation, not confirmed defects or field-validated failure prediction.

[Open this guide in installed Help](/help?topic=intelligence.analysis-guide)
