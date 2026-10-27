# Reports: authoring and operation

## Reports: configure data, period and resolution

Start with the question the report should answer before drawing the page.

### Prepare verifiable data

Start with the question: which readings should appear, and for what period? In Engineering > Reports, first use a pressure TAG with existing history and a short interval with known values. This lets you check data before designing the page.

The report queries stored records. It does not enable capture or recover an unrecorded past. Your account needs permission to edit and query the chosen source. Use the samples dataset for values and the alarm dataset for alarm transitions.

### First test before final layout

1. Choose an embedded query, which belongs to the report, and historian.samples, the samples dataset.
2. Add a known TAG through the reference picker.
3. Choose a short period and Raw resolution.
4. Run Preview. Check available rows, times, values and quality.
5. Switch to Summary with Maximum and an hourly interval. Compare each output with raw data: an interval maximum is not a list of every reading.
6. Then arrange the page and validate the definition. For no preview rows, check TAG, period, capture and permission before changing colors or widths.

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

Resolution defines how to retrieve and summarize records for this query. Raw shows stored samples; other options organize or summarize output. Check raw values before using a summary.

This choice does not change captured data, remove raw records or configure permanent database aggregation policy.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Raw, fixed interval or summary | Raw examines stored samples; fixed interval retrieves on defined steps; summary applies a function per bucket. An average may hide a peak; inspect raw data to investigate. | Raw for checking; hourly summary for overview |  |
| Fixed interval | Choose spacing between retrieval positions. It is neither Driver polling nor a guarantee of a sample at every position. Missing data remains significant. | 5 minutes if suitable for the question |  |
| Aggregation and bucket | Average, min, max, sum, count, first and last differ. Choose bucket width. Summing flow readings does not automatically calculate production volume; integration and appropriate units would be needed. | Hourly maximum to look for peaks |  |
| Long or wide table | Long organizes sample rows; wide organizes intervals for comparing variables. Wide is disabled for raw mode. | Long to inspect samples |  |

[Open this guide in installed Help](/help?topic=reports.data-designer)

## Reports: page, variables, fields and summaries

Separate the queried value, its format and where it is printed.

### Understand the page

In the Designer, Project/Design is where you build the page; Preview shows a run with data. Start with a title and one reading field before adding groups and totals.

Dimensions use millimeters, as on a printed page, not Runtime-screen pixels. Portrait is vertical; Landscape is horizontal. Reserve margins and fit content in the remaining width. Zoom only changes how you see Preview, not print size.

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

Label is fixed text, such as Discharge pressure. Field shows a query value. These are the controls directly offered for insertion; start with them.

Put the title in the report header and record fields in Detail, the section receiving query rows. Page header/footer repeat on each page; report header/footer belong to the report. Group headers/footers depend on configured grouping.

With no query rows, a title can appear while Detail stays empty. Check data first rather than trying to fix this by increasing section height.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Section height | Reserved band space. A taller control can be clipped; increasing height creates no data rows. | Fit the band to its content |  |
| X, Y, width and height (mm) | Layout position/size. Check section bounds and usable width; these are not monitor coordinates. | Field inside the Detail band |  |
| Label text | Fixed content such as a title. Use Field for row values; typing a TAG path into a label creates no binding. | Shift readings |  |
| Query and field | Query selects a result; Field must exist in its dataset. Use its exposed schema name, such as value or timestamp for samples, not an invented name. | Query main / field value |  |
| Image asset | An image control needs an existing resolved asset. If the picker does not offer it, do not invent an ID; use editable controls and check configuration availability. | Existing asset when available |  |

### Grouping is not temporal aggregation

Group by brings rows together by the chosen field and lets you order that organization. Resolution defines query time intervals. These are different choices: naming a group does not change the interval.

For a summary, choose field, function and scope: just the group or the whole report. Check whether the query already summarized data before summing again. Count reports record quantity, not running hours. Test a small group and check its total manually.

### From preview to active revision

1. Build a title, Detail section and fields linked to real data.
2. Run Preview and check data, pages and clipping.
3. Use Validate and correct invalid references or structure.
4. Apply only the valid candidate. If the project changed during editing, refresh and validate again.
5. Save, publish and activate the revision through project controls.
6. Generate the report in Runtime with the same test parameters and compare. Preview shows a run; it does not save the definition or prove the new revision is active.

[Open this guide in installed Help](/help?topic=reports.layout-variables)

## Runtime reports: generate, check and export

Output belongs to one execution with its own period and parameters.

### Find the available report

Open Reports in Runtime and search by name. The list uses the active revision and your session’s permissions. If a report is missing, check those two conditions before recreating it.

Engineering access or seeing a report object on a screen does not grant access to every dataset. The generating user also needs permission to query the chosen source.

### Period, parameters and execution

1. Select the report to generate.
2. Choose relative period or From/To from allowed modes. Relative is calculated at execution time: repeating later can use a different window.
3. For an absolute interval, enter start before end and check the displayed time zone.
4. Complete parameters with the requested types and options. Numbers, dates and booleans do not accept arbitrary text.
5. Use Generate report and wait. Cancel interrupts the request; it does not delete the definition.
6. Before interpreting, check generation time, queried period and parameters used.

### Preview, PDF, spreadsheet and print

Check output with previous/next page and zoom. Fit to width changes only the view. Use PDF or print for paginated presentation; XLSX and CSV let you work with output data.

These export the result, not project-import packages or TAG edits. An exported file represents that run. Generate again to update data; reopening the file does not make a live query.

Before sharing a spreadsheet or CSV, check units, dates and decimal formatting expected by the recipient.

### Problems needing different checks

For a missing report, review publication/activation and authorization. For a title with no rows, check period, filters and capture. For an empty field or error, confirm referenced field/query, type and parameters.

An unexpected number calls for checking resolution, function, unit and summary scope. A different label does not convert units. For clipping, check millimeters, usable width and section height.

If data looks old, check generation time and generate again. Do not change permissions or units blindly to try to fix output.

[Open this guide in installed Help](/help?topic=reports.runtime-output)
