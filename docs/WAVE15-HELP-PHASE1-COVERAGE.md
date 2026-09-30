# W15-HELP Phase 1 coverage

Issue: #424 — W15-HELP. Parent: #306. Terminology authority: #379.

This file is implementation evidence for the installed contextual Help. It is not end-user Help content.

## Reused foundation

Phase 1 keeps the Wave 14/C25.8 architecture:

- local same-origin Help catalog in `src/Scada.Api/Runtime/ContextualHelpApi.cs`;
- language-neutral topic IDs;
- pt-BR / en / es locale resolution;
- `/help?topic=<id>` installed UI;
- contextual route resolver in `web/scada-web/src/help/contextualHelpTopic.ts`;
- generated Driver/source-provider topics from the registered Data Source catalog;
- deterministic backend and browser tests.

No second Help system or external documentation dependency was added.

## Coverage matrix

| Feature | Help topic | Context entry | pt-BR | en | es | Status |
| --- | --- | --- | :---: | :---: | :---: | --- |
| First start / authentication | `getting-started.startup-authentication` | global Help / Runtime | yes | yes | yes | COVERED |
| Neutral bootstrap | `getting-started.neutral-bootstrap` | related from lifecycle/startup | yes | yes | yes | COVERED |
| Engineering shell | `engineering.shell-navigation` | `/engineering` | yes | yes | yes | COVERED |
| Working / Save / Revision / Publish / Activate | `engineering.lifecycle` | Engineering + related links | yes | yes | yes | COVERED |
| Application export/import | `application.export-import`, `packages.escadapkg` | project/recovery entry + related links | yes | yes | yes | COVERED |
| Data Sources | `sources.data-sources` | `/engineering/dataSources` | yes | yes | yes | COVERED |
| Communication Drivers | `drivers.overview` + `driver.<type>` | Data Sources + related links | yes | yes | yes | COVERED |
| TAG fundamentals | `tags.overview` | `/engineering/tags` | yes | yes | yes | COVERED |
| TAG addressing / quality / timestamps / writeability | existing `tags.*` topics | TAG entry + topic navigation | yes | yes | yes | COVERED |
| TAG copy/paste/duplicate/sequential | `tags.copy-duplicate-sequential` | TAG contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Screen Editor | `screens.overview` | `/engineering/screens` | yes | yes | yes | COVERED |
| Popup Editor | `popups.overview` | `/engineering/popups` | yes | yes | yes | COVERED |
| Visual properties | `visual.properties` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Dynamics | `visual.dynamics` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Visual Events | `visual.events` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Reusable Library | `libraries.reusable-resources` | `/engineering/libraries` | yes | yes | yes | COVERED |
| Dynamos | `dynamos.overview` | `/engineering/dynamos` | yes | yes | yes | COVERED |
| Script Engineering | `scripts.engineering` | `/engineering/scripts` | yes | yes | yes | COVERED |
| Python validation/diagnostics | `scripts.python-validation` | Scripts contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Object Browser / guided authoring | `engineering.object-browser` | Script/Dynamics topic navigation | yes | yes | yes | COVERED |
| Server Script API/runtime | `scripts.server` | Scripts topic navigation | yes | yes | yes | COVERED |
| Client Memory | `memory.client`, `sources.internal-memory` | local topic navigation | yes | yes | yes | COVERED |
| Users / Roles / Capabilities | `security.users-roles-capabilities` | `/engineering/security` | yes | yes | yes | COVERED |
| Scopes / Authority | `security.scopes-authority` | Security contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Engineering Lock | `security.engineering-lock` | Security contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Licensing | `licensing.overview` | `/licensing` | yes | yes | yes | COVERED |
| License Generator | `licensing.generator` | Licensing contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Runtime Interactive / View Only | `runtime.session-classes` | Runtime/Licensing contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Diagnostics | `diagnostics.overview` | Engineering diagnostics routes | yes | yes | yes | COVERED |
| Troubleshooting | `troubleshooting.overview` | topic navigation / diagnostics | yes | yes | yes | COVERED |
| Alarm basics | `alarms.overview` | `/engineering/alarms` | yes | yes | yes | COVERED |
| Historian fundamentals | `historian.overview` | `/engineering/historian` | yes | yes | yes | COVERED |
| Trend fundamentals | `trends.overview` | topic navigation | yes | yes | yes | COVERED |
| Operational Events | `operational-events.overview` | `/engineering/operationalEvents` | yes | yes | yes | COVERED |
| Audit | `audit.overview` | `/audit` | yes | yes | yes | COVERED |
| Authority backup / recovery | `recovery.backup-system-recovery` | project/recovery entry + related links | yes | yes | yes | COVERED |
| Historical Time Range | Phase 2 placeholder only | none frozen | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #383 |
| Historical Playback | Phase 2 placeholder only | none frozen | n/a | n/a | n/a | BLOCKED_BY_PRODUCT |
| HA D1 | Phase 2 placeholder only | none frozen | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #421 |
| HA D2 | Phase 2 placeholder only | none frozen | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #423 |
| Installation UX | Phase 2 placeholder only | none frozen | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #422 |
| Visual Quality final wording/screenshots | Phase 2 review only | existing stable Help only | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #308 |

## Deterministic gates

Phase 1 strengthens checks for:

- required topic presence in every locale;
- stable topic-ID syntax and pt-BR/en/es structural parity;
- duplicate topic IDs;
- related-topic IDs resolving to real local topics;
- task-guide section parity for new/updated Phase 1 topics;
- contextual route mapping to stable real topic IDs;
- explicit missing-topic behavior (no silent fallback);
- no external documentation dependency;
- no implementation-brand/coordination jargon in normal Help output;
- existing Driver/source inventory and Script API contract tests.

## Phase 2

After #383 / Historical Playback / #421 / #423 / #422 / #308 stabilize:

1. add only the implemented final behavior;
2. bind real contextual entries for those surfaces;
3. run the final pt-BR/en/es semantic sweep with #379;
4. validate the real UI links;
5. remove stale or superseded instructions;
6. do not declare Wave 15 Help complete before this convergence.
