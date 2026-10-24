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
| First start / authentication | `getting-started.startup-authentication` | global Help / Runtime | yes | yes | yes | TASK GUIDE — first-use vs neutral bootstrap, local sign-in, safe recovery and access troubleshooting |
| Neutral bootstrap | `getting-started.neutral-bootstrap` | related from lifecycle/startup | yes | yes | yes | COVERED |
| Engineering shell | `engineering.shell-navigation` | `/engineering` | yes | yes | yes | COVERED |
| Working / Save / Revision / Publish / Activate | `engineering.lifecycle` | Engineering + related links | yes | yes | yes | COVERED |
| Application export/import | `application.export-import`, `packages.escadapkg` | project/recovery entry + related links | yes | yes | yes | COVERED |
| Data Sources | `sources.data-sources` | `/engineering/dataSources` | yes | yes | yes | TASK GUIDE — schema-driven setup, capability-gated tools, protected credentials, Preview/Apply and lifecycle |
| TAG Gateway routes | `gateway.overview` | `/engineering/gateway` | yes | yes | yes | TASK GUIDE — endpoints, transfer/conversion policy, Preview/Apply and Runtime diagnostics |
| Communication Drivers | `drivers.overview` + `driver.<type>` | Data Sources + related links | yes | yes | yes | COVERED |
| TAG fundamentals | `tags.overview` | `/engineering/tags` | yes | yes | yes | TASK GUIDE — name/path, source binding, type, optional capture, transient read test, Preview/Apply and Runtime lifecycle |
| TAG addressing | `tags.addressing` | TAG entry + topic navigation | yes | yes | yes | TASK GUIDE — source-selected schema, driver-specific fields, transient Test read and lifecycle |
| TAG quality | `tags.quality` | TAG entry + topic navigation | yes | yes | yes | TASK GUIDE — interprets quality beside value/time and traces the source-to-display path |
| TAG timestamps | `tags.timestamps` | TAG entry + topic navigation | yes | yes | yes | TASK GUIDE — distinguishes observed/source/server timestamps and evidentiary limits |
| TAG writeability | `tags.writeability` | TAG entry + topic navigation | yes | yes | yes | TASK GUIDE — separates ProcessValueWrite from CommandExecute and avoids unsafe live writes |
| TAG scaling/formatting | `tags.scaling-formatting` | TAG entry + topic navigation | yes | yes | yes | TASK GUIDE — separates engineering conversion from display precision and uses transient read evidence |
| TAG copy/paste/duplicate/sequential | `tags.copy-duplicate-sequential` | TAG contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Screen Editor | `screens.overview` | `/engineering/screens` | yes | yes | yes | TASK GUIDE — stable identity vs route, screen format, object editing, TAG bindings, Preview/Apply and Runtime lifecycle |
| Intelligence — process-stage member insights | `intelligence.process-stage-member-insights` | `/engineering/intelligence` | yes | yes | yes | COVERED — bounded Stage 5B.11 route; independent per-equipment read-only analyses, no process aggregate |
| Popup Editor | `popups.overview` | `/engineering/popups` | yes | yes | yes | TASK GUIDE — popup identity/template, object authoring, logical bounds, Preview/Apply and Runtime lifecycle |
| Visual properties | `visual.properties` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Dynamics: Boolean conditions, numeric intervals/maps, Analog Fill | `visual.dynamics` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | TASK GUIDE — configuration choices and validation |
| Visual Events: quick actions, Script interaction events, permissions | `visual.events` | Screen/Popup contextual entry + local topic navigation | yes | yes | yes | TASK GUIDE — stable surface |
| Reusable Library | `libraries.reusable-resources` | `/engineering/libraries` | yes | yes | yes | COVERED |
| Dynamos | `dynamos.overview` | `/engineering/dynamos` | yes | yes | yes | TASK GUIDE — shared drawing, rotation, per-instance parameters, Preview/Apply and lifecycle |
| Script Engineering | `scripts.engineering` | `/engineering/scripts` | yes | yes | yes | COVERED — scope selection and lifecycle |
| Client Visual Python: APIs, TAG/Client Memory reads/writes and interaction examples | `scripts.client-visual` | `/engineering/scripts` + local topic navigation | yes | yes | yes | TASK GUIDE — examples checked against client sandbox bridge |
| Python validation/diagnostics | `scripts.python-validation` | Scripts contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Object Browser / guided authoring | `engineering.object-browser` | Script/Dynamics topic navigation | yes | yes | yes | COVERED |
| Server Script API/runtime | `scripts.server` | Scripts topic navigation | yes | yes | yes | COVERED |
| Client Memory | `memory.client`, `sources.internal-memory` | local topic navigation | yes | yes | yes | COVERED |
| Users / Roles / Capabilities | `security.users-roles-capabilities` | `/engineering/security` | yes | yes | yes | TASK GUIDE — local account lifecycle, roles/grants/scopes, backend validation and session impact |
| Scopes / Authority | `security.scopes-authority` | Security contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Engineering Lock | `security.engineering-lock` | Security contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Licensing | `licensing.overview` | `/licensing` | yes | yes | yes | TASK GUIDE — distinguish installed license from Runtime entitlement; machine request, signed-code installation and safe diagnostics |
| License Generator | `licensing.generator` | Licensing contextual entry + local topic navigation | yes | yes | yes | COVERED |
| Runtime operator orientation / Interactive / View Only | `runtime.overview`, `runtime.session-classes` | Runtime / Licensing contextual entry + local topic navigation | yes | yes | yes | TASK GUIDE — Active revision, navigation, data evidence, command/write distinction and Live recovery |
| Runtime Historical Browser and Playback | `runtime.history` | `/runtime/history` | yes | yes | yes | TASK GUIDE — datasets, relative/absolute windows, read-only queries, explicit Playback/Live transition and limits |
| High Availability administration | `ha.overview` | `/engineering/highAvailability` | yes | yes | yes | TASK GUIDE — Standalone/HA, readiness, desired-vs-running state and visible cluster actions; no unsupported failover guarantees |
| Installation Application switching | `installation.switching` | `/engineering/installation` | yes | yes | yes | TASK GUIDE — Application/Authority preservation, session invalidation, Historian preservation and license choice |
| Intelligence setup and use | `intelligence.overview` | `/engineering/intelligence` | yes | yes | yes | TASK GUIDE — selected Equipment, measurement profiles, contexts/stages, explicit analysis and editable Screen drafts; current behavior is reviewable as it evolves |
| Diagnostics | `diagnostics.overview` | Engineering diagnostics routes | yes | yes | yes | COVERED |
| Troubleshooting | `troubleshooting.overview` | topic navigation / diagnostics | yes | yes | yes | COVERED |
| Alarms: definition, activation, state, acknowledgement and troubleshooting | `alarms.overview` | `/engineering/alarms` | yes | yes | yes | TASK GUIDE — stable surface |
| Historian capture | `historian.overview` | `/engineering/historian` + TAG editor | yes | yes | yes | TASK GUIDE — capture strategy, required fields, Preview/Apply, activation and no backfill |
| Trend authoring | `trends.overview` | topic navigation + visual editor | yes | yes | yes | TASK GUIDE — Live/History, pens, TAG references, scales and Historian/TrendUse prerequisites |
| Reports: authoring and Runtime execution | `reports.overview` | `/engineering/reports` + `/runtime/reports` | yes | yes | yes | TASK GUIDE — query/dataset, TAG variables, layout, Preview/Validate/Apply, authorized generation and export |
| Operational Events: definition, Runtime emission and history query | `operational-events.overview` | `/engineering/operationalEvents` | yes | yes | yes | TASK GUIDE — stable surface |
| Audit: access, filters, outcomes, paging and troubleshooting | `audit.overview` | `/audit` | yes | yes | yes | TASK GUIDE — stable surface |
| Authority backup / recovery | `recovery.backup-system-recovery` | project/recovery entry + related links | yes | yes | yes | COVERED |
| Historical Time Range and Playback | `runtime.history` | `/runtime/history` and Playback entry in Runtime | yes | yes | yes | TASK GUIDE — validated current interface and supported limits; data availability still depends on capture and authorization |
| HA D1 / current admin UI | `ha.overview` | `/engineering/highAvailability` | yes | yes | yes | TASK GUIDE — interface accepted; backend verification remains separate |
| HA D2 / automatic failover | `ha.overview` | same HA admin page | yes | yes | yes | TASK GUIDE — automatic-failover interface accepted; remaining backend tests stay in #423; no test pass is implied |
| Installation UX | `installation.switching` | `/engineering/installation` | yes | yes | yes | TASK GUIDE — current detach/switch flow; real UI screenshots follow the interface audit |
| Intelligence overview and member insights | `intelligence.overview`, `intelligence.process-stage-member-insights` | `/engineering/intelligence` | yes | yes | yes | TASK GUIDES — current implementation may evolve; keep claims bounded and revise after product/UI review |
| Visual Quality final wording/screenshots | Phase 2 review only | existing stable Help only | n/a | n/a | n/a | BLOCKED_BY_PRODUCT / #308 |

## Deterministic gates

The alarm task guide records current activation-delay and acknowledgement-option limitations in all three locales. Source review and local reproductions at `b04eb9c118220bf47f117acc0e88a7d1a929863a` confirmed that Engineering persists `ActivationDelay` and `RequiresAcknowledgement`, while `InMemoryAlarmEngine` does not consume them. A 10-second delay activated immediately, and a returned alarm configured with `RequiresAcknowledgement=false` remained pending in the active list. Product corrections are tracked separately in #751 and #753; this Help slice does not change alarm Runtime behavior.

PR validation profile: `DOCS_I18N_HELP`.

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

## Remaining convergence

Open product tests are not a blanket blocker for Help about already validated interfaces. The automatic-failover interface is accepted; some backend tests remain in #423. Describe visible configuration and safety boundaries without implying those backend tests passed or asserting guarantees they have not established. Intelligence Help is authorized as current-behavior documentation and may be revised as the product evolves.

After the coherent content batch:

1. run the focused local content/routing suite once and fix any failures;
2. execute one exact-head T1 and one exact-merge T2 as agreed in #771;
3. perform the final pt-BR/en/es semantic sweep with #379;
4. complete the requested interface audit before capturing screenshots for the manual;
5. validate actual contextual links and remove stale/superseded instructions;
6. do not declare Wave 15 Help or the full manual complete until remaining coverage converges.
