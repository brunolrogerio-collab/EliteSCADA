# Asset and Process Intelligence

This chapter describes the workflows available today. Intelligence is optional: the license enables the feature, but you configure the Equipment, profiles, and stages you intend to analyze. You do not need to apply analyses to the entire plant.

**Contextual Help:** `/help?topic=intelligence.overview`.


## Start with one small question

Imagine Pump 01. SCADIVO already receives current and speed, and you want to investigate whether current changed under similar operating conditions. Start with that question instead of configuring the whole plant. A comparison can guide investigation; it does not by itself establish physical efficiency or a defect.

Editor names represent parts of this task:

| Name | Meaning in this example |
| --- | --- |
| TAG | The variable providing current or speed. |
| Measurement profile | The sheet gathering chosen signals and each signal’s meaning. |
| Assignment | The connection between that sheet and Pump 01 registered as Equipment. |
| Operating context | The condition under which data should be compared, such as a defined operating range. |
| Process stage | An organized group of assignments, such as pumping equipment. It does not program starts. |
| Coverage | The query showing which usable data exists in the period. |

Follow this path and use the sections below for detail:

1. Check existing TAGs, their units and recorded data for the period. Do not invent missing sensors.
2. Build the profile and assign it to Pump 01. Pickers handle internal IDs.
3. Save configuration, choose where to use Intelligence, publish/activate the revision and explicitly enable the resource for analysis. License, configuration and authorized execution are separate conditions.
4. Query coverage first. Then choose only analyses whose requirements your signals meet. An unavailable result needs investigation; do not read it as zero.
5. For an operator view, generate a screen, review it in the normal editor and follow publication/activation. It opens in Runtime and queries require an explicit request; generation does not query data or automatically put the screen into operation.

You can start with one equipment. Contexts and stages are useful when they serve the question. Time, signal-role and limit details follow for lookup during configuration.

## 1. Start with Equipment and TAGs

Choose registered Equipment and useful measurement TAGs, such as current, voltage, pressure, flow or temperature. For each TAG, check value type and unit. Quality describes the reading’s condition; timestamp is its source time. Historian is recorded history, which needs data for the chosen period. Include a signal for the question it helps answer, not merely because it exists.

To edit, your account needs `EngineeringModify` and `AssetProcessIntelligenceManage`. The latter must be explicitly granted for all resources or for the equipment/location you will configure. Also check the Intelligence license. The account’s role name does not replace effective permission.

## 2. Create a measurement profile and assignment

1. Open Engineering > Asset Intelligence and create a Measurement profile with an identifiable name/key.
2. Add the TAGs that describe the Equipment. Mark each signal required or optional and complete any requested semantic qualifiers; a label does not replace the unit or actual TAG reference.
3. Save the configuration to Working.
4. In assignments, choose the existing Equipment and profile. One Equipment identity can use more than one profile.
5. Review missing and duplicate references before saving and following the normal project lifecycle.

The profile organizes signal references and meaning. It does not create TAGs, enable Historian capture by itself, or prove that instruments are calibrated.

## 3. Configure operating context

Higher current may be expected when speed or load rises. An operating context states conditions, such as pump running and speed within a defined range, to select comparable evidence. It does not automatically learn operating mode or turn correlation into diagnosis. Choose limits using Equipment knowledge.

Choose the equipment assignment and give the context a stable key and a name explaining the condition, such as Pump running at normal flow. Add at least one required predicate. A predicate is a condition checked against a TAG in the assigned profile.

For numbers, the range includes minimum and maximum in the TAG’s current unit. For booleans, choose True or False. For String or Enum, use the exact value, including case when applicable.

Every condition needs evidence at the same synchronized, exact source UTC timestamp. Readings merely close in time are not enough. The server controls profile version; on an editing conflict, refresh configuration and review before saving.

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

Choose which equipment to analyze, prepare its configuration and activate use. Having the license does not automatically start analyses.

### Follow the full sequence

Start with one piece of equipment, such as Pump 01. Open Engineering > Asset Intelligence. You will gather its variables, identify which equipment they belong to and choose where to use analysis.

The profile lists signals and their meanings. An assignment connects that profile to registered Equipment. Configure contexts and stages only when they fit your question. Then save, publish and activate the project revision.

One separate decision remains: explicitly activate the selected resource to allow analyses. Creating the profile, checking a box or having the license does not do that for you.

### Permissions and resource selection

Your account needs permission to edit Engineering and manage Intelligence for the selected equipment or locations. Security shows these as EngineeringModify and AssetProcessIntelligenceManage. The latter needs an explicit grant, global or scoped to the resource. A role name such as Developer or SystemAdmin does not by itself establish that grant.

Queries also require TrendUse and the Intelligence license. If a control is blocked, check the session’s effective capabilities there. Choose resources through the pickers; there is no need to memorize or invent IDs.

### Activate only where useful

1. Under Eligible scopes, select only the equipment or locations you want to analyze. Here, scope means that selection of resources.
2. Save the scopes and prepared configuration.
3. Publish and activate the project revision. Resolve pending changes before allowing execution.
4. On the resource row, use Activate and check that its execution permit is armed, meaning execution is allowed for that resource.
5. Open the assignment query, choose a period and request analysis. Activation allows a query; it does not automatically run one.
6. Use Pause to stop analyses. Pending work is cancelled, while TAGs and history remain. Pause a resource before removing it from the selection.

### Configured active does not mean armed

If a permission was revoked, a resource can remain marked Active in configuration while execution is blocked. These are different facts: what was configured and what is authorized now.

Restoring the grant does not silently restart work. Check status and use Activate again, including after restart when requested by the screen. This requires an explicit decision by the responsible person. Check this condition before investigating missing data in the recorded history, called Historian.

### Example: start small

For Pump 01, start with current and speed if those measurements exist. Choose analyses that accept them. When more measurements become available, you can expand the profile and use other compatible analyses.

Keep unused resources paused. Do not invent a missing vibration signal. Selection limits what is allowed to run, but does not guarantee fixed processor or memory usage.

[Open this guide in installed Help](/help?topic=intelligence.activation)

## Intelligence: signal meaning and units

Describe what was measured, not just the TAG name.

### Why a name is not enough

Before calculating, specify what each variable measures. A TAG named Power might be active power, apparent power or a calculation; Pressure might measure suction, discharge or their difference. Its name alone does not identify the right formula.

In the profile, choose the required semantic role: the option that identifies the signal’s meaning for analysis. Then fill in its displayed qualifiers, the details that make that choice precise. Legacy role text remains a description; it does not replace this selection.

### Fields and choices

Use this table when a profile field is unclear. Pickers show options accepted by the current catalog. Choose what matches the instrument and process, even if another option would unlock a calculation.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Required | Marks signals whose absence compromises requested coverage. Do not mark essential signals optional just to obtain a result. | Current needed for comparison |  |
| Semantic role | Choose what the signal represents in the calculation: its quantity and function in the machine. Analysis uses that controlled option; it does not guess from the TAG name. | Active input power, if measured |  |
| Current basis | Specify the current basis supplied by the instrument and accepted by the role, such as RMS when appropriate. If its label only says current, confirm the measurement before choosing. | Select the documented instrument option |  |
| Signal kind | Distinguishes measurement, reference and the role-required function. A setpoint does not prove a measured value. | Actual measurement when required |  |
| Pressure reference | Specify which pressure was measured. Outlet minus inlet pressure differs from discharge pressure alone; choose the basis required by the role and confirmed by the instrument. | Measured outlet − inlet differential |  |
| Flow direction | Identifies transfer direction; the opposite direction may invalidate a rule. | Pump to process, when applicable |  |
| Frequency reference | Identifies frequency meaning per catalog; distinguish electrical reference from physical speed. | Check instrument source |  |
| TAG and unit | Select the actual TAG and check its controlled engineering unit. A profile label change does not convert data. Some calculations require specific catalog units/versions. | kW for power when it matches actual data |  |

### Example: do not confuse a proxy with efficiency

Suppose pump current rises while speed stays the same. That can guide an investigation, but current alone does not calculate the motor’s physical efficiency. It is a substitute indicator, also called a proxy.

Other rules may use electrical input power, flow and pressure difference. They become available only when signals meet the required roles, units and alignment. If a measurement or necessary detail is missing, read the reason for unavailability; do not enter fictional values to unlock the rule.

[Open this guide in installed Help](/help?topic=intelligence.semantics)

## Intelligence: query results and build dashboards

Start by checking data for the period. Then choose a calculation and, if needed, generate a screen for Runtime queries.

### Start with coverage

Coverage answers the first question: is there usable data for this analysis? With the assignment saved and execution authorized, open the profile’s coverage query. Choose start and end and request the query.

The form shows local times; the query also identifies its UTC window, the common time reference used to compare data. End must be after start.

Check which required signals have data, accepted and rejected sample counts, quality and time alignment. Only then interpret minimum, maximum or average. A large sample count alone does not prove gap-free data or that all signals were measured together.

### Performance: choose an eligible rule

In Performance, choose the calculation rule from the catalog. If an option is disabled, read which signal, meaning or unit is missing. Choosing another quantity merely to obtain a number would change the question.

Fill From/To and query. Read status, unit, actual period and limitations alongside the result. Unavailable is not zero. A substitute indicator, such as a current comparison, must not be presented as physical efficiency.

If assignment, revision or parameters change, request another query. The previous result belongs to the previous choices.

### Two context types with different bounds

Context describes an operating condition so similar situations can be compared. For example, studying a pump within a flow range can be more useful than mixing every condition. There are two different configurations:

In the saved operating profile, a numeric range includes both limits in the current engineering unit. Signals need evidence at the exact same source UTC timestamp. Readings merely close in time do not meet that requirement.

In the contextual-deviation screen generator, choose 2 to 8 target signals and two numeric context signals. Context signals must differ from each other and from the targets: at least four usable numeric signals are needed. Ranges use the displayed controlled unit and include the lower limit but exclude the upper: lower ≤ value < upper.

Check which configuration you are using before copying limits. Units and upper-limit inclusion can differ.

### Generate the screen and check the full chain

1. Choose the equipment assignment or process stage and the desired screen type.
2. Complete the requested signals and options. If the button is blocked, read the reason beside the field.
3. Generate the draft. This organizes objects and references; it does not yet read Historian data or activate the screen.
4. In the editor, check title, dimensions, objects, bindings and how to open the screen. Run Preview and Apply to put the edit in the project.
5. Save the revision, publish and activate. Open the Runtime screen through configured navigation.
6. In the analysis object, set the period and request the query. Check window and result. A stage screen groups its equipment; that does not create an aggregate indicator or establish that one equipment caused another’s result.

### How to investigate an empty result

If there is no result, look for the first missing condition:

Check license and permissions, then the resource’s saved selection and armed execution permit. Check that the active project and revision are the expected ones. Next review assignment, TAGs and period.

If those are correct, examine Historian capture, data quality, signal roles/units and alignment/context requirements. Correct the condition shown to be missing and query again.

Do not change a signal’s meaning or widen limits merely to produce a result. Analyses support investigation. They do not confirm defects or predict failures with field validity that has not yet been demonstrated.

[Open this guide in installed Help](/help?topic=intelligence.analysis-guide)
