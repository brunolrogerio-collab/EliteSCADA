# Historian: capture and queries

## Historian: configure capture and check history

Choose what to record, under which conditions and how to verify storage.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Where to configure and query

To see what happened earlier, SCADIVO must have recorded the data. Historian is that history of TAG samples. The live monitor shows the current value; History, Reports and Intelligence use what was stored. Enabling today does not recover an unrecorded past.

In Engineering > Historian, review TAGs with capture enabled and their summary. To change how a TAG records, open Engineering > TAGs, select it and expand History/Historian in the editor.

### Example showing deadband effects

Deadband is the minimum change used to decide whether a new numeric observation should be recorded. It is compared with the last accepted value.

With constant quality, deadband 0.1 and last accepted value 1.00, observation 1.03 may be discarded. Value 1.12 exceeds the threshold and may be accepted. The next comparison starts from the last accepted value, not every discarded observation.

The first observation and quality changes have their own handling and may be recorded without that change. With a maximum-interval strategy, an observation received after the deadline may also be accepted. Do not calculate expected record count from deadband alone.

### Verification procedure

1. Check the live TAG: value, type, scale and quality.
2. In the TAG editor, enable History and choose a strategy with valid required fields.
3. Run Preview and Apply; save, publish and activate the revision. Editing the draft alone does not change running capture.
4. On a test source, produce known changes and note when they occurred.
5. Open Runtime History, choose Samples and filter the TAG for that same period.
6. Compare values, quality, times and count against the configured strategy.
7. Repeat with a stable signal and a quality change. For Intelligence, also check source timestamp and the analysis’s alignment requirement.

### TAG capture fields

Use the accepted names below in the current strategy field. Unknown text or incomplete settings may retain legacy all-observation capture rather than expected volume reduction. Inspect configuration/samples instead of assuming savings from filling a field.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| History enabled | Enables this TAG capture. Disabling neither deletes existing history nor creates a backup. | Enable the pressure TAG used in the test |  |
| Strategy | periodic captures observations respecting a period; onChange records value changes; onChangeDeadband requires numeric variation; onChangeDeadbandMaxInterval adds a maximum interval between accepted arriving observations. | onChangeDeadbandMaxInterval |  |
| Deadband | Minimum absolute change from the last accepted captured value. It is neither a percentage nor alarm limit. Use a compatible numeric strategy. | 0.1 for a desired tenth-unit change | Finite number ≥ 0 |
| Period (ms) | Used by periodic. 1000 ms = 1 second. It selects arriving observations, not faster Driver polling or invented samples when the source is silent. | 1000 | Positive integer |
| Maximum period (ms) | Used by onChangeDeadbandMaxInterval to accept an arriving observation when time elapses despite small change. Does not guarantee capture during communication outage. | 60000 for up to one minute between accepts while observations arrive | Positive integer |

### Query: period, filters and pages

Start by choosing the record collection, called a dataset. Value samples and alarm transitions are different collections. Select a short relative period or fill From/To; the current window allows up to 31 days per query.

Then use filters offered for that dataset. Each field expects a value type: number, boolean, date, text or enum. Choose an available operator and enter the right type rather than arbitrary text.

Sorting changes presentation order, not record selection. Moving pages continues the query; it does not extend its period. For no result, remove one filter at a time and check that capture existed at that time.

### Capture, retention, backup and Playback differ

These are different decisions. Capture selects observations to record. Retention defines how long to keep them. Aggregation summarizes samples; backup keeps a recoverable copy. Configuring TAG capture does not define a global database quota or archiving.

### Create a Historian archive

In Engineering > Database, under Archive and restore samples, choose the start and end of the period. The browser's local time zone is converted to UTC in the archive. The period must end in the past. The archive contains only raw samples from the Historian table; it does not include aggregates, alarms, events, Audit, the application, Authority, licensing, or HA state.

The destination is a folder on the API server. The service administrator can set it with `Historian:Archive:RootPath`. In Docker, mount this folder as a persistent volume so the files survive container recreation. With a remote Historian database, samples stream through the SCADIVO service; the PostgreSQL host does not need access to the archive folder.

Use Create verified archive. The service splits the period into UTC-day segments, compresses each segment, checks its row count and SHA-256 by reading it back, and saves progress before continuing. The list shows Queued, In progress, Paused, or Completed and verified. If the process restarts, confirmed segments resume. If the destination is full or unavailable, the job pauses and original samples remain; fix the destination and choose Resume.

Download only when the state is Completed and verified. To restore, select the `.scadhist` file and choose Verify and restore. SCADIVO checks the whole package before inserting samples. For each segment, it compares the number of identical occurrences already present and inserts only the missing rows, avoiding duplicates when the original samples remain in the database. Progress is recorded in the active Historian database. Repeating the archive does not duplicate samples and can restore data that a retention policy removed later.

Deleting original samples is optional and is unchecked by default. To enable it, check the option and the additional confirmation. Removal happens only after the complete package has been closed, read back, and verified. It affects raw samples only in the selected period; aggregate retention is a separate policy. Keep the source if you still need the samples in the active Historian.

### Schedule archives and set file rotation

Configure one schedule for this installation in the same section. Start archiving from sets the first cursor position. Archive only data at least this many days old leaves recent data outside the batch; Run every sets the frequency; Archive up to per run limits each batch to 31 days. The cursor advances only after a batch archive is completed and verified. Review the displayed cursor and job state. A paused job does not advance; fix the destination and choose Resume.

Keep archive files without an expiry date to retain every completed package. If you set a period, rotation deletes completed `.scadhist` packages after they become older than that period. It does not delete Historian database samples. The policy applies to files created manually as well as scheduled files. Make external copies if you need to keep them longer.

The destination is a folder on the API server. In Docker, mount it on persistent storage. If multiple API nodes use the same database, configure the same path on every node and point it to one shared persistent folder; independent local storage on each node is not enough. For a remote Historian database, the folder still belongs to the SCADIVO service, not the PostgreSQL host.

The schedule can also remove source samples, but this option starts unchecked and requires explicit confirmation. Removal starts only after the complete package has been read back and verified. Progress is saved one segment at a time in the same database transaction as that segment's source deletion; after a restart, already confirmed segments are skipped and later-arriving samples remain in the database. The batch is marked Completed only after every authorized segment is processed. Configure the archive age before retention or the size limit; those policies can remove samples before the schedule cursor reaches them. Test restore and monitor jobs before relying on archiving to free space.

Playback displays historical values. It does not restore the process or allow commands from the past. Return to live mode before operating. Missing samples do not mean zero or stopped equipment.

[Open this guide in installed Help](/help?topic=historian.capture-query)
