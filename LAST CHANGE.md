# LAST CHANGE — EliteSCADA

Date: 2026-08-29

## Current checkpoint

Wave 08 FOLLOW-A and FOLLOW-B are **CLOSED / MERGED / POST-MERGE GREEN**.

Final validated product baseline on `main`:

`dededaca980fdb72b5d4955685ab1161aca441fd`

FOLLOW-A evidence:

- PR #105 merged;
- exact pre-merge product CI #541 green;
- exact post-merge `main` CI #543 green.

FOLLOW-B evidence:

- canonical contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`;
- final integrated/product head: `dededaca980fdb72b5d4955685ab1161aca441fd`;
- CI #657 green on that exact final FOLLOW-B head, including Web, backend build/tests/smoke and Chromium E2E;
- FOLLOW-B was fast-forwarded into `main` without force;
- post-merge/push CI #658 green on exact `main` head `dededaca980fdb72b5d4955685ab1161aca441fd`, including Chromium E2E.

FOLLOW-B therefore no longer blocks downstream product work.

## Active stage

**Wave 09 is ACTIVE.**

Initial Wave 09 substage is the shared historical/navigation foundation:

- protected typed Historical Query v1 shared by Browser, Reporting and Trends;
- initial datasets `historian.samples` and `alarm.events`;
- canonical Popup/Dynamo/navigation Engineering over the existing Screen/visual runtime;
- Historical Data Browser consuming the shared query contract.

Canonical planning contracts:

- `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md`;
- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`;
- `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.

Reporting/Report Designer remains part of Wave 09, but its implementation slice starts only after the shared Historical Query contract is integrated and accepted. This is sequencing inside an active Wave, not a separate product gate.

Parallel protocol Drivers remain isolated and parked from `main`; Wave work has priority.

## CI policy

CI mode remains **NORMAL**. Do not run reassurance CI on unchanged product trees. Exact final integration/product heads require green evidence before merge/stage transitions. Documentation-only coordination commits use `[skip ci]`.