# Help/manual — whole-product depth review, 25 October 2026

Owner correction: the previous Screen/object expansion was not the full manual. Intelligence, HA, Database, Templates, Equipment and all other material user workflows need contextual introductions, actual field meanings/options, concrete examples, ordered procedures, expected outcomes and symptom-based troubleshooting. A topic ID or a matrix row is inventory, not evidence that someone can use the feature.

## Active delivery

Continue existing draft PR #772 over product base `d41c2f84ba179f5eae01dc1df96c9c1a28830dc7`. Predecessor `2361d615e1c05ef654bb6a6987bd25b919df6dbf` passed T1 #40192872747; that result must not be attributed to later changes. No HA, database, Intelligence execution or licensing contracts are changed by this documentation work.

Eleven additional localized guides now explain the application map, Template authoring, independent Equipment creation, database administration, HA preparation/operations, Intelligence activation, signal semantics, analysis interpretation, Header/Mobile, Media Sources and Home/Building locations. `Runtime/HelpGuides/system-guides.json` supplies the existing installed topic/section/field contract. It is embedded locally, not downloaded from a site. `scripts/docs/export_system_help_manual.py` produces the corresponding long-form chapters/sections from this same content; `--check` detects drift. This does not replace the existing Help framework or require network access.

Manual chapters 04, 13, 14 and 15 are added in pt-BR/en/es; chapter 12 is expanded. Navigation adds separate Equipment/Templates and Database/HA areas. Previously generic contextual fallbacks for Templates, Equipment, Database, Header, Mobile, Media Sources and Home/Building now route to their own guides. The Database page's existing Help disclosure links directly to the guide. Topic navigation preserves search context, supports browser Back/Forward, retains deep-link URLs and leaves modified/new-tab clicks to the browser.

## Source findings that constrain instructions

- `EngineeringApp.tsx` actually routes Templates to `VisualEditorWorkspace(definitionKind="template")` and Equipment to `EquipmentFaceplateWorkspace`. A legacy read-only copy helper remains in the source but is not proof these current routes are read-only. Follow active call sites.
- `EquipmentFaceplateWorkspace.tsx` generates a new path, preserves an existing path during rename, validates before applying and permits no Template. Changing a faceplate does not select Intelligence measurements.
- `HighAvailabilityAdminWorkspace.tsx`, `ha/i18n.ts` and its types define advertised endpoints, optional external override, write-only secret, external shared protection, desired versus running configuration, readiness and operation rejection. Document both shared/separate database considerations without claiming that the HA switch provisions replication. User's manual HA acceptance/backend verification remains separate; no test acceptance is invented.
- `database-topology/DatabaseTopologyApp.tsx`, `types.ts`, `i18n.ts` define SystemAdmin access, host/user/secret and advanced fields, migration phases, attach-without-copy matching project/revision, restart, rollback and preserved local storage. Returning to local is not data merging or backup restoration. Certificate paths belong to the service host/container; no Windows-only path is prescribed.
- `AssetProcessIntelligenceWorkspace.tsx`, Coverage/Performance/ContextualDeviation panels and current generators distinguish saved metadata, selected scopes, active revision and armed execution. Explicit global/restricted manage grant remains #774. Restoring authorization is not silent rearming. Coverage, role/unit eligibility and result availability are explained separately.
- Saved operating contexts have inclusive bounds in current Engineering units and exact synchronized UTC source timestamps. The contextual-deviation screen generator needs distinct targets/context signals and half-open ranges in displayed controlled units. Do not conflate them.
- `BrandingEngineeringWorkspace.tsx`, `RuntimeHeaderSettings.tsx`, `MobileRuntimeEngineeringWorkspace.tsx` define branding modes, conditional text/image settings, Header controls, clock/shortcuts, orientation and alternate-screen selection. Orientation does not author a new drawing.
- `MediaSourceEngineeringWorkspace.tsx` admits protocol/endpoint fields and separate write-only host credentials excluded from export. RTSP needs the server relay for browser playback. This is documented as a condition, not promised direct playback.
- `HomeBuildingWorkspace.tsx` separates location hierarchy, device identity/location and provider capabilities; location changes do not change protocols or grant commands.

## Navigation inventory and remaining editorial work

This is a review map, not a new feature backlog or a declaration of completed acceptance. “Expanded” means the candidate has concrete teaching material; walkthrough/copy review is still required.

| User area | Help entry / material | Review disposition |
| --- | --- | --- |
| First start, Overview, project lifecycle | existing first-start/lifecycle guides, application map | Expanded in prior batch; end-to-end first-project walkthrough still required |
| Installation, restore/import/switch | `installation.switching`, recovery/export guides | Current interface described; detailed recovery walkthrough still to review |
| Header / Mobile | `application.presentation`, chapter 15 | Expanded with conditional fields and procedure in this batch |
| Scripts / Object Browser / Client Memory | Server/Client guides and tested examples, chapter 06 | Expanded in prior batch; retain full syntax/API and recipe review |
| Libraries / Dynamos / Visual Assets | library/dynamo/object guides, chapters 04/05 | Cross-linked; detailed resource/version/import field review remains |
| Data Sources / production protocols | catalog-derived per-driver guides | Protocol-specific plain-language field review still needed; descriptor presence is insufficient |
| Home/Building | `home-building.organization`, chapter 15 | Location/device basics expanded; capability-specific scenarios still to review |
| Media Sources | `sources.media`, chapter 15 | Expanded with fields, credentials, playback conditions and errors |
| TAGs / Gateway / bulk import/export | existing typed TAG, routing and package guides | Detailed spreadsheet/routing file examples remain to review |
| Alarms / Operational Events / Audit | separate current guides | Existing distinction and known product residuals preserved; configuration field walkthrough remains |
| Templates / Equipment | dedicated contextual guides, chapter 04 | Expanded with creation, stable identity, optional linkage and shared-change effects |
| Screens / Popups / object fields | prior 31 type references and chapter 05 | Expanded; final real Editor field-position captures and walkthrough remain |
| Historian / Trends / Runtime History | capture/history/trend-series guides | Current procedures present; capture-profile/retention/query details still to review |
| Reports | existing report guide | Report authoring/parameters/scheduling/result workflow still to review |
| Security / Engineering Lock / Licensing | current guides and chapter 08 | Role/scope fields and installed behavior still require final review; future online licensing is not documented as delivered |
| Intelligence | profiles/context/stages + three new detailed guides, chapter 12 | Activation, fields, interpretation, generation and failure investigation expanded |
| HA | existing overview + detailed configuration/operation guide, chapter 13 | Fields/states/actions expanded; no new backend/manual operational acceptance claimed |
| Database | dedicated guide/link and chapter 14 | Fields, TLS, migrate/attach/rollback/restart and symptoms expanded |
| Monitor / TAG Monitor / Diagnostics / Information | existing diagnosis and signal guides | Separate purpose/status/action field review remains |
| Runtime operation / session / navigation | existing Runtime/session/history/object guides | Recheck discoverability and actions with final accepted UI |

## Validation and acceptance boundary

Keep local/backend content tests, locale/related-link checks, browser discovery/search/history tests, Web build, generated-manual drift checks and exact-head T1 evidence. Failures must be diagnosed, not hidden by retry/timeout changes. Existing Phase-1 overview URLs/tests remain intact; the new detailed HA guide complements the old overview instead of deleting its accepted structure.

User-facing screenshots must be actual system captures. Local Help component images with exact serialized catalog are QA evidence, not substitutes for real Editor/HA/Intelligence operation captures. Final product-interface audit #357 and manual images #425 remain in the agreed sequence. No count of topics, pages or fields closes #424/#425.
