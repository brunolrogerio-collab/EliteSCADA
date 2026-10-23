# Asset and Process Intelligence

This chapter describes the workflows available today. Intelligence is optional: the license enables the feature, but you configure the Equipment, profiles, and stages you intend to analyze. You do not need to apply analyses to the entire plant.

**Contextual Help:** `/help?topic=intelligence.overview`.

## 1. Start with Equipment and TAGs

Before creating a profile, identify the stable Equipment resource and TAGs that represent useful measurements, such as current, voltage, pressure, flow, or temperature. Check unit, type, quality, timestamp, and Historian capture for the period to be analyzed. Do not include a signal merely because it is available; state which operational question it helps answer.

Editing requires `EngineeringModify` and `AssetProcessIntelligenceManage` within the resource scope. Intelligence features also require the corresponding license.

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
