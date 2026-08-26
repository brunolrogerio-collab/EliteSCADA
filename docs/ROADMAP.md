# EliteSCADA Development Roadmap

The approved development north is preserved, with Engineering Import/Export acting as a transversal product requirement rather than a late add-on.

## Runtime foundation established

Implemented and validated on `main` before the current engineering-lifecycle security branch:

1. Repository, architecture and CI/CD foundation.
2. Tag Engine, quality, current-value cache and internal Event Bus.
3. Simulation driver and runtime smoke coverage.
4. Alarm engine and acknowledgement flow.
5. Historian abstraction and buffered in-memory historian.
6. Realtime WebSocket TAG stream.
7. Engineering Import/Export public model and JSON exchange.
8. TAG/Alarm CSV exchange.
9. Engineering Schema v2 Data Sources and driver configuration.
10. Engineering Schema v3 Equipment/Templates/Dynamos.
11. Engineering Schema v4 Screens/Popups and bindings.
12. Engineering Schema v5 extended visual component model and project package backup/restore.
13. PostgreSQL engineering persistence and TimescaleDB historian baseline.
14. Real Modbus TCP driver baseline through the Engineering Data Source model.
15. Published versus Active engineering revision lifecycle.
16. Transactional persisted runtime activation and fail-closed recovery.
17. Isolated editable Engineering Workspace with checkout/save revision lineage.
18. Capability-based authorization contracts and audit event/sink foundation.
19. Engineering Schema v6 authorization roles, explicit capability grants and scoped policies.
20. Trusted JWT Bearer principal adapter with issuer/audience/signature/lifetime validation.
21. Phase-one backend enforcement for process-value TAG writes, alarm acknowledgement, Engineering import apply and project-package restore apply.
22. Active-runtime authorization policy resolution from the exact persisted Active Revision with fail-closed mismatch behavior.
23. Durable PostgreSQL append-only audit trail with database-enforced UPDATE/DELETE/TRUNCATE rejection and protected query API.

## Engineering Import/Export status

The original gate before promoting persistence and real industrial communication has been met and remains locked as a product invariant.

Completed:

1. Versioned Engineering Package envelope. ✓
2. TAG serialization and import/export. ✓
3. Alarm serialization and import/export. ✓
4. Data Source/driver technical configuration serialization, excluding secrets. ✓
5. Equipment/Template/Dynamo engineering serialization. ✓
6. Screen/Popup/binding engineering serialization. ✓
7. Public JSON import/export APIs independent of the GUI. ✓
8. CSV TAG and alarm exchange. ✓
9. Project package backup/restore boundary. ✓
10. PostgreSQL engineering revision persistence. ✓
11. Working/Published/Active revision lifecycle. ✓
12. Transactional checkout/save lineage. ✓
13. Security roles/grants/scopes in Engineering Schema v6. ✓

The following continue as evolutionary extensions rather than prerequisites for the runtime foundation:

- XLSX workbook exchange;
- richer reusable libraries and cross-project copy/paste;
- additional driver configuration schemas;
- future migration tooling as engineering schemas evolve.

## Persistence and industrial communication gate

The original gate before promoting persistence and real industrial communication is complete:

1. Engineering Package schema and validation. ✓
2. JSON round-trip and import preview/apply. ✓
3. TAG and Alarm CSV import/export. ✓
4. Historical engineering schema migration/compatibility tests. ✓
5. Engineering Exchange handler refactor. ✓
6. PostgreSQL engineering persistence. ✓
7. TimescaleDB historian baseline. ✓
8. Real Modbus TCP Data Source/driver baseline. ✓

## Current execution slice

The strong runtime foundation is now being turned into a secure and engineer-friendly product without weakening the public engineering model.

Completed in the security track:

1. Capability-based authorization evaluator with configurable role names and explicit scoped grants. ✓
2. TAG access-policy evaluator preserving `null` versus empty-list semantics. ✓
3. Audit event and sink contracts. ✓
4. Versioned Engineering Schema v6 security-role/grant/scope serialization. ✓
5. Trusted JWT Bearer principal adapter. ✓
6. Phase-one backend capability enforcement for critical TAG/alarm/Engineering restore mutations. ✓
7. Active-runtime policy resolution from the exact persisted Active Revision. ✓
8. Browser authentication/authorization coverage for developer/operator/anonymous/invalid-token cases. ✓
9. PostgreSQL append-only audit event storage with database-enforced rejection of `UPDATE`, `DELETE` and `TRUNCATE`. ✓
10. Queryable audit trail protected by `SystemAdmin`. ✓
11. Succeeded/denied/failed audit recording for protected TAG writes, alarm ACK, Engineering import apply and project-package restore apply. ✓
12. Browser coverage validating trusted/anonymous audit subjects, authorization outcomes and audit-read protection. ✓
13. Persistence save/publish/checkout/apply authorization using `EngineeringModify`, authenticated lifecycle actors and succeeded/denied/failed audit records. ✓
14. Browser lifecycle coverage backed by PostgreSQL, including rejection of anonymous/operator mutations and proof that caller-supplied save/publish actor fields cannot override the trusted JWT subject. ✓

Activation is protected by the same lifecycle filter when authentication is enabled and derives `ActivatedBy` from the trusted JWT subject. Activation remains independently constrained by the configured runtime project key and transactional runtime validation.

Next:

15. Extend enforcement/audit to commands, alarm shelving and sensitive read/realtime/WebSocket surfaces.
16. Add a real login/token-issuance or external identity-provider workflow and user lifecycle administration.
17. Add audit retention/query policy and durable buffering/outbox behavior for temporary storage outages.
18. Add historian retention/downsampling policies on TimescaleDB.
19. Add MQTT driver integration through the same Data Source/driver model.
20. Add Engineering XLSX workbook import/export.
21. Expand runtime diagnostics, driver health, offline behavior and operational hardening.
22. Stabilize frontend package versions/lockfile and continue CI performance/hygiene improvements.

## Locked future product requirements

These requirements remain part of the EliteSCADA product north and must be implemented through the public engineering model.

### Reusable libraries across applications

- Evolve Equipment Templates/Equipment and Dynamos into a version-aware reusable library experience.
- Preserve a class/instance model conceptually similar in responsibility to Elipse E3 XObject/XControl while using EliteSCADA's own contracts and implementation.
- Allow reusable definitions to expose properties/bindings and be instantiated with application-specific context.
- Support nested reusable components with deterministic dependency validation.
- Make library definitions importable/exportable independently of the graphical editor.
- Support controlled library update/migration while preserving safe instance overrides.

### Cross-project copy/paste

- Copy/paste screens, popups, equipment, Dynamos and engineering structures between projects.
- Preserve bindings and references where possible and report unresolved dependencies explicitly.
- Keep clipboard/import formats versioned and based on the same public engineering contracts rather than private UI state.

### Product engineering UX

- Engineering edits must remain isolated from the operational runtime until explicitly saved/published/activated according to lifecycle semantics.
- Validation, preview and dependency diagnostics must be available before applying potentially disruptive engineering changes.
- UI authorization hints may improve usability but backend authorization remains authoritative.

### Security and operations

- Credentials, tokens, private keys and Data Source secrets remain outside Engineering Import/Export payloads.
- Process-impacting and security-sensitive operations remain auditable using trusted identities.
- Runtime activation remains transactional and fail closed when the persisted active state cannot be safely recovered.
