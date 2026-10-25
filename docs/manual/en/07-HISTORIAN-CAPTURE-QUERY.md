# Historian: capture and queries

## Historian: configure capture and check history

Choose what to record, under which conditions and how to verify storage.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Where to configure and query

Engineering > Historian lists enabled historical TAGs and summarizes strategy, deadband and period. Configure a TAG in Engineering > TAGs by selecting it and expanding History/Historian. Live monitoring reads current values; History, Reports and Intelligence need stored samples. Enabling today does not recover the past.

### TAG capture fields

Use the accepted names below in the current strategy field. Unknown text or incomplete settings may retain legacy all-observation capture rather than expected volume reduction. Inspect configuration/samples instead of assuming savings from filling a field.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| History enabled | Enables this TAG capture. Disabling neither deletes existing history nor creates a backup. | Enable the pressure TAG used in the test |  |
| Strategy | periodic captures observations respecting a period; onChange records value changes; onChangeDeadband requires numeric variation; onChangeDeadbandMaxInterval adds a maximum interval between accepted arriving observations. | onChangeDeadbandMaxInterval |  |
| Deadband | Minimum absolute change from the last accepted captured value. It is neither a percentage nor alarm limit. Use a compatible numeric strategy. | 0.1 for a desired tenth-unit change | Finite number ≥ 0 |
| Period (ms) | Used by periodic. 1000 ms = 1 second. It selects arriving observations, not faster Driver polling or invented samples when the source is silent. | 1000 | Positive integer |
| Maximum period (ms) | Used by onChangeDeadbandMaxInterval to accept an arriving observation when time elapses despite small change. Does not guarantee capture during communication outage. | 60000 for up to one minute between accepts while observations arrive | Positive integer |

### Example showing deadband effects

With constant quality, deadband 0.1 and last accepted value 1.00, observation 1.03 can be skipped; 1.12 crosses the threshold and can be accepted. Comparison uses the last accepted value, not each rejected observation. First observation and quality transitions have their own handling and can be recorded without numeric change. Maximum-interval mode may also accept a later observation by elapsed time. Counts therefore cannot be inferred from deadband alone.

### Verification procedure

1. Inspect live TAG type/scale/quality.
2. Enable History and valid strategy/fields.
3. Preview/Apply and save/publish/activate.
4. Produce known test-source changes and note the period.
5. Open Runtime History for that period, select Samples and filter the TAG.
6. Check values, quality, times and count against the strategy.
7. Repeat for steady values and quality change. For Intelligence also check source timestamps and required alignment.

### Query: period, filters and pages

Select the right dataset: samples are not alarm transitions. Use a short relative or valid From/To period; the current history browser limits a query window to 31 days. Add schema-offered fields/operators with correctly typed numeric/Boolean/date/text/enum values. Sorting changes order, not filters. Paging continues the query without enlarging its period. For empty results remove filters one at a time and verify capture existed then.

### Capture, retention, backup and Playback differ

Capture decides admitted observations; retention decides storage duration; aggregation summarizes samples; backup preserves a recoverable copy. Capture fields are neither a global database quota nor archival configuration. Playback displays historical values in the current visualization, not process restoration or command authorization; return to live before operating. Missing samples mean neither zero nor stopped equipment.

[Open this guide in installed Help](/help?topic=historian.capture-query)
