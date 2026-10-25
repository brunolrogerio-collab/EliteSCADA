# Reports: authoring and operation

## Reports: configure data, period and resolution

Start with the question the report should answer before drawing the page.

### Prepare verifiable data

Open Engineering > Reports. Start with a pressure TAG that already has historical capture and a short period with known values. A report queries stored data; it neither enables capture nor recovers unstored history. Have permission to edit and query the selected dataset. Sample history and alarm transitions use different datasets.

### Identification and query source

Create or select a report. Use reference selectors instead of inventing IDs. These fields configure the definition; executing a preview is separate.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Name, identifier and description | Name appears in the list; identifier preserves references; description explains purpose/context. Renaming creates no new data. | Pump pressure — shift |  |
| Saved Data Query or embedded query | A Data Query reuses an existing definition; an embedded query belongs to the report. Check the selection before changing layout. The editor does not accept arbitrary SQL. | Embedded query for the first example |  |
| Data source / dataset | historian.samples queries samples; alarm.events queries alarm occurrences/transitions. Fields depend on this choice; switching datasets may invalidate layout fields. | historian.samples |  |
| Default period and preview period | Default belongs to the definition; preview period selects the current test window. Use the editor unit. One hour is 3600 seconds; this is not a row count. | Last hour to check a small sample |  |
| Report data / variables | Add TAGs through the reference browser. Check path, type and unit; configure each variable presentation separately. | Pump 01 pressure TAG |  |
| Rows per page | Requested query page size; inspect the returned count. It does not define Historian retention duration. | 50 | 1–200 |

### Choose data retrieval/presentation

Resolution is a query choice; it changes neither capture nor stored raw samples. Report aggregation does not configure permanent database aggregation policy.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Raw, fixed interval or summary | Raw examines stored samples; fixed interval retrieves on defined steps; summary applies a function per bucket. An average may hide a peak; inspect raw data to investigate. | Raw for checking; hourly summary for overview |  |
| Fixed interval | Choose spacing between retrieval positions. It is neither Driver polling nor a guarantee of a sample at every position. Missing data remains significant. | 5 minutes if suitable for the question |  |
| Aggregation and bucket | Average, min, max, sum, count, first and last differ. Choose bucket width. Summing flow readings does not automatically calculate production volume; integration and appropriate units would be needed. | Hourly maximum to look for peaks |  |
| Long or wide table | Long organizes sample rows; wide organizes intervals for comparing variables. Wide is disabled for raw mode. | Long to inspect samples |  |

### First test before final layout

1. Use an embedded historian.samples query.
2. Add a known TAG.
3. Choose a short period and Raw.
4. Run Preview; inspect rows, times, values and available quality.
5. Switch to Summary, Maximum and hourly buckets; compare with raw data without expecting identical output.
6. Then arrange the page and validate. For no rows, check TAG, period, capture and permission before changing fonts, widths or colors.

[Open this guide in installed Help](/help?topic=reports.data-designer)

## Reports: page, variables, fields and summaries

Separate the queried value, its format and where it is printed.

### Understand the page

Use Design to edit and Preview to inspect execution. Dimensions are millimeters, not Runtime screen pixels. Portrait is vertical; Landscape horizontal. Content must fit usable width after margins. Preview zoom does not change print dimensions.

### How to format a variable

The variable remains linked to its selected TAG. These fields change presentation, not calibration or conversion by relabelling.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Show column and order | Choose visibility and use Move up/down for order. Removing a report variable does not delete its TAG. | Pressure before current |  |
| Display label | Readable variable heading, distinct from TAG identity. | Discharge pressure |  |
| Automatic, hidden or custom unit label | Automatic uses the TAG unit; Hidden omits it; custom label changes text only. Labelling a bar value psi does not convert it. | Automatic when the unit is correct |  |
| Decimals and numeric format | Choose standard, fixed or scientific. Decimals affect display, not accuracy or stored value. Do not round away relevant variation. | One place for tenths | 0–15 places in the editor |
| Date/time and Boolean labels | Choose local date/time, date, time or ISO 8601. Set meaningful True/False labels; missing data is not False. | Running / Stopped only if that is the meaning |  |

### Layout sections and controls

Report header/footer apply to the report; page header/footer repeat per page. Detail receives query rows; group header/footer follows grouping. Detail may be empty while the title remains visible if there are no rows. Start with Label and Field, the directly offered insertion controls.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Section height | Reserved band space. A taller control can be clipped; increasing height creates no data rows. | Fit the band to its content |  |
| X, Y, width and height (mm) | Layout position/size. Check section bounds and usable width; these are not monitor coordinates. | Field inside the Detail band |  |
| Label text | Fixed content such as a title. Use Field for row values; typing a TAG path into a label creates no binding. | Shift readings |  |
| Query and field | Query selects a result; Field must exist in its dataset. Use its exposed schema name, such as value or timestamp for samples, not an invented name. | Query main / field value |  |
| Image asset | An image control needs an existing resolved asset. If the picker does not offer it, do not invent an ID; use editable controls and check configuration availability. | Existing asset when available |  |

### Grouping is not temporal aggregation

Group by organizes rows by a selected field with ascending/descending order. A summary selects field, function and scope: group or entire report. Time bucket width comes from Resolution, not group names. Do not sum already aggregated data blindly. Count means records, not operating hours. Manually check a small group before trusting totals.

### From preview to active revision

1. Build a title, Detail and real fields.
2. Preview data and inspect pagination/clipping.
3. Validate references/structure.
4. Apply only a valid candidate; refresh/revalidate if the project changes.
5. Save/publish/activate.
6. Generate in Runtime with the same test parameters and compare. A preview execution is not a saved definition or an active revision.

[Open this guide in installed Help](/help?topic=reports.layout-variables)

## Runtime reports: generate, check and export

Output belongs to one execution with its own period and parameters.

### Find the available report

Open Runtime Reports and search by name. The list depends on active revision and session authorization. Engineering access or a report object on a screen does not grant every data query. For an empty list check revision and permissions before recreating definitions.

### Period, parameters and execution

1. Select a report.
2. Choose relative or From/To where the definition permits. Relative time is resolved for execution; repeating later may use another window.
3. For absolute time choose start before end and check timezone.
4. Enter correctly typed/allowed parameters, not arbitrary text for numbers/dates/Booleans.
5. Generate and wait; cancellation stops the request without deleting the definition.
6. Inspect generation time, window and used parameters before interpreting output.

### Preview, PDF, spreadsheet and print

Use page navigation/zoom for inspection; fit-to-width does not change the definition. PDF/print provide paginated presentation; XLSX/CSV support work on the output. They export results, not Engineering packages or TAG edits. Regenerate for updated data; opening an old export does not make it live. Check spreadsheet/CSV unit, date and decimal interpretation for the recipient.

### Problems needing different checks

Missing report: revision/authorization. Header without Detail: empty query, period/filters/capture. Blank/error field: field/query references, type and parameters. Unexpected number: resolution, function, label-only unit and summary scope. Clipping: millimeters, usable width and band height. Old data: inspect execution time and regenerate. Do not blindly change permissions or units to hide failure.

[Open this guide in installed Help](/help?topic=reports.runtime-output)
