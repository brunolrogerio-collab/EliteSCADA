# Gateway and CSV/Excel files

## TAG Gateway: configure and test a route

Transfer values between server TAGs with explicit quality, conversion and diagnostics.

### When to use it

Open Engineering > Gateway. A route reads a source TAG and requests a destination TAG write through Runtime. It can connect already-configured protocol sources, but creates no Data Source, address or connection by itself. Transfer is one-way, not automatic two-way synchronization. Do not build a return-route loop without analyzing effects. Learn with two Server Memory TAGs disconnected from equipment.

### Route identity and endpoints

Pickers offer TAGs with enabled, server-owned sources. Client Memory belongs to a browser and cannot be a route endpoint. Simulation can be a source but not a destination. The destination must allow writes; source and destination must differ. IDs retain internal identity; paths help check meaning.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Route identifier | Key identifying the route in project and diagnostics. The current form keeps route name alongside this identifier; tabular Key and Name are separate columns. | copy-speed | Required, no whitespace. |
| Enabled | Controls route participation in the activated revision. Does not fix a destination failure. | Yes |  |
| Source TAG | Select the value feeding the destination. Check type, quality and scaling in TAG Monitor. | Server.Source |  |
| Destination TAG | Select a writable TAG already configured for equipment or Server Memory. A route does not automatically change its write permission. | Server.Destination |  |

### Trigger, startup and conversion

GoodOnly is the current quality policy: a bad-quality value is not transferred as valid. Conditional fields depend on mode/conversion. Periods are milliseconds and do not change the Driver acquisition cycle.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| OnChange / Periodic | OnChange reacts to acceptable updates with deadband/minimum interval. Periodic checks the current value at its period; it does not manufacture Good quality for a bad source. | OnChange |  |
| Initial transfer | Synchronize uses the first acceptable value available at startup. Wait for next avoids initial synchronization and awaits the next transfer condition. Assess effects before using physical destinations. | WaitForNextAcceptableValue |  |
| Deadband (OnChange) | Absolute source-unit difference from the last successfully transferred source value. A smaller difference is skipped; equality with the deadband can proceed. Neither a percentage nor alarm deadband. | 0.1 | Finite number ≥ 0; OnChange only. |
| Minimum interval (OnChange, ms) | Limits attempt frequency in change mode. Fast updates may coalesce and the current value is reread before writing; not a guaranteed queue of every sample. | 1000 ms = 1 s | If set: 100 to 86400000 ms. |
| Period (Periodic, ms) | Periodic transfer interval. Do not combine with OnChange deadband/minimum interval in a file. | 1000 | Required in Periodic: 100 to 86400000 ms. |
| Exact / CheckedNumeric | Exact requires direct compatibility without gain/offset. CheckedNumeric explicitly transforms numerics and validates the target type. Overflow or a fractional value lost in an integer conversion fails; there is no silent truncation. | CheckedNumeric |  |
| Gain | Multiplier in destination = source × gain + offset. Absent defaults to 1. Not the Driver scaling adjustment. | 2 |  |
| Offset | Added after gain; absent defaults to 0. Check target unit/range before publishing. | 5 |  |

### Procedure: 11 becomes 27 without real equipment

1. Create writable Server Memory TAGs Server.Source Int16 and Server.Destination Int32 with no physical destination.
2. Create copy-speed using these endpoints, OnChange, Wait for next, CheckedNumeric, gain 2 and offset 5. Leave deadband/minimum interval empty for this first test.
3. Preview configuration, fix errors and apply to Workspace. Save, publish and activate.
4. In the test environment update source to 11 with Good quality. Check 27 at destination: 11 × 2 + 5.
5. Inspect transfer diagnostics/last error; acceptance does not prove physical actuation.
6. Only then assess range, unit, quality, startup and rate before choosing real endpoints. Never use a route to bypass Authority, licensing or HA authority.

### Equivalent CSV and diagnostics

This CSV uses readable paths without IDs. Both TAGs and their source must exist before Preview/Apply. In Import/Export choose Gateway routes and CSV; the file does not create TAGs. Runtime diagnostics describe the active revision, not the draft. Waiting for source calls for checking source/quality; Degraded and last error call for conversion/range/destination checks. Skipped/coalesced counters do not automatically mean a defect: inspect mode and limits. Importing a route performs no transfer before the appropriate lifecycle.

```csv
Key,Name,SourceTagPath,DestinationTagPath,TransferMode,QualityPolicy,ConversionPolicy,InitialTransferPolicy,Gain,Offset,Enabled
copy-speed,Copy speed,Server.Source,Server.Destination,OnChange,GoodOnly,CheckedNumeric,WaitForNextAcceptableValue,2,5,true
```

[Open this guide in installed Help](/help?topic=gateway.configure-test)

## CSV and Excel: export, edit and import while preserving references

Prepare one entity table, validate the candidate and inspect creates/updates.

### Choose the file

In project Import/Export/Management use CSV/Excel for TAGs, Alarms, Data Sources, Gateway routes or Operational Events. Choose one entity per file. For Screens, Popups, Templates, scripts and composite dependencies use appropriate project/library mechanisms rather than invented columns. Engineering CSV/XLSX is neither historical-value export nor Authority/database backup. Start with an export from the current version to preserve column names and conventions.

### Workflow controls

File and entity/format/mode identify the candidate. Any change requires Preview again. The summary reports creates, updates, skips and errors; skipped does not mean applied.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Entity | Choose the object type contained in rows. A route spreadsheet cannot be read as TAGs. | gateway-routes |  |
| CSV / XLSX | CSV uses commas and quotes text containing commas/quotes/newlines. XLSX accepts one sheet with the entity name used by export. Renaming old .xls does not create XLSX. | GatewayRoutes in XLSX |  |
| Merge mode | CreateOnly creates new and skips existing; UpdateExisting updates existing and skips new; CreateAndUpdate does both. Matching uses entity identity, not row position. This neither replaces everything nor deletes absent items. | CreateAndUpdate |  |
| File and confirmation | Select the saved file, Preview and read diagnostics before confirming Apply. Content must match what was validated; reload/revalidate if Workspace changes. | Export → edit → Preview → Apply |  |
| Reference keys and paths | Current exports use readable paths/keys and hide internal IDs. Preserve TAG path/entity key for updates; a new key may create another object. References must exist in the appropriate project/candidate. | SourceTagPath=Server.Source |  |

### Editing rules in Excel or text editors

Keep exported technical headers even when the UI is translated. Unknown or duplicate columns can block import. Use decimal point (2.5) and booleans true/false; do not append units to numbers. JSON fields require valid JSON, not Python or semicolon lists. Preserve paths/keys as text to prevent automatic date/number conversion. XLSX formulas are rejected even with cached results: paste values before saving. The single sheet must be Tags, Alarms, DataSources, GatewayRoutes or OperationalEvents for the chosen entity. Include no users, password hashes, login credentials or secrets; protected Data Source references are not secrets themselves. Start complex fields from a real export, not a simplified example.

### Example: two TAGs for the Gateway test

A Server Memory source with key memory.server must exist. This minimal CSV creates the Gateway example TAGs, not the source. In a test project select TAGs/CSV and Preview, inspect both items and apply through the protected flow. Then import the route from the Gateway guide. A reduced-column table is for guided creation: preserve full exports when updating complete objects, because omitted columns may assume defaults rather than preserve previous values.

```csv
Path,Name,DataType,DataSourceKey,ReadOnly,InitialValueDataType,InitialValueJson
Server.Source,Source,int16,memory.server,false,int16,0
Server.Destination,Destination,int32,memory.server,false,int32,0
```

### Complete procedure and error recovery

1. Export the current entity and keep a copy before editing.
2. Edit a small selection; preserve references/types. For expansion use unambiguous new paths/keys and create Data Sources before TAGs, then dependent alarms/routes/events.
3. Save values only in the chosen format and reselect the file.
4. Choose mode and Preview; inspect counts and error rows. Preview does not modify Working.
5. Confirm Apply of the validated candidate, then inspect objects in the editor.
6. Save, publish and activate to change Runtime through project lifecycle.
7. For row errors correct the file cause rather than deleting rows to hide dependencies; Preview again. If an update lost an option, compare omitted/defaulted columns with the original full file. Export is a configuration copy, not automatic activation rollback.

[Open this guide in installed Help](/help?topic=engineering.tabular-workflow)
