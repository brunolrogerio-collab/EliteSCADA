# ADR-026 — Operational alarm suppression by alarm and location

**Status:** accepted for implementation in product issue [#789](https://github.com/brunolrogerio-collab/EliteSCADA/issues/789)
**Baseline:** `wave15/corrections-integration@4c8396ed8bb14c5dc5718e2394309f260d88eaf8`
**Date:** 2026-10-31

## Evidence from the current product

- `AlarmDefinition.Area` / `AlarmEngineeringDto.Area` is a text value passed to the existing Authority resource check. It is not a stable operational hierarchy key.
- `LocationEngineeringDto.Id` and `ParentLocationId` are the existing stable, parent-linked operational tree. `EquipmentEngineeringDto.LocationId` connects equipment to it.
- Current `IAlarmEngine.ShelveAsync` changes an in-memory `AlarmInstance.State` to `Shelved`; the saved underlying state also lives in memory. It does not provide the durable, overlapping, inherited state required here.
- `AlarmShelvingEndpointExtensions` provides a precedent for `AlarmShelve` permission and audit, but its semantics remain unchanged.

## Decisions

1. **Keep alarm evaluation separate from suppression.** A suppression is an overlay on annunciation, not an alarm state transition. The condition continues to evaluate; Active/Acknowledged/Returned state, ACK data, return, reactivation and alarm history remain truthful. A suppressed active condition remains visible and explicitly marked suppressed. It is not rendered as Normal. Runtime sound/annunciation consumers honor the overlay.
2. **Use stable project identities.** An authored alarm links to either a stable `EquipmentId` or a `LocationId`, never both. For equipment-linked alarms, Runtime resolves the equipment's current `LocationId` at activation, so moving equipment does not leave a stale operational scope. Unlinked alarms stay individually targetable and are shown as having no operational location. `Area` remains on the Authority resource check as alarm context. For scoped grants, the request also supplies the alarm's directly linked stable Equipment or Location identity; the canonical Authority graph resolves its own scope node and parentage. The operational parent chain is never used to decide who may act. An unlinked alarm has no scoped Authority resource, so a scoped grant does not match it; a global `AlarmShelve` grant is still explicit and can authorize it.
3. **Use two target kinds:** one alarm or one `Location`. A Location target includes alarms explicitly linked to that Location or descendant locations by the `ParentLocationId` chain. Names are presentation only. No name-prefix matching and no coupling between the operational tree and the Authority scope graph.
4. **Compose independent causes.** The effective suppressed state is true while at least one active alarm-level or ancestor-Location action exists. Releasing one target removes only that cause. The projection returns each cause, its stable target ID/name, actor, reason and expiry. Expired causes do not apply. New definitions linked under an active Location inherit its active cause; validation of the current target is all-or-nothing.
5. **Be explicit about lifetime.** Every action requires an allow-listed reason code. `expiresAtUtc` is either a future UTC instant or `null`, which means it remains active until explicitly released. The UI/API must show that distinction. This is operational scoped suppression, not a claim of ISA/IEC conformance for operator shelving. ISA-18.2 describes operator shelving as temporary and calls for a time limit, shelved-alarm visibility, access control, unshelving and a record; existing shelving remains the bounded temporary operation.
6. **Validate before mutation.** Use `AlarmShelve` through the existing runtime Authority check for every affected alarm, passing its `Area` context and direct stable Equipment/Location identity when present. Authority scope inheritance comes only from the canonical Authority graph; the operational tree is used only to calculate which alarms are affected. Reject the whole location operation if any affected alarm is unresolved, disallows suppression, or the principal lacks scope. ViewOnly/license status cannot grant Authority rights. Release is also authorized; no partial apply is hidden.
7. **Persist and replicate.** Persist active target state in the shared `elitescada` PostgreSQL schema, isolated by project key. Serialize each mutation in a database transaction and use existing durable-write admission. In shared-database HA, both nodes observe the same rows. In separated-database HA, include the canonical suppression snapshot in the versioned authenticated peer authoritative-state envelope and restore it before the takeover fence allows Runtime effects. New nodes must fail HA readiness/takeover closed if this required snapshot is absent; do not add a local-memory-only path.
8. **Audit the decision.** Record stable target kind/ID, project, actor, operation, reason code, expiry, affected count and result through the existing Audit service. Do not place credentials, tokens or free-form secrets in the audit. Reject and audit authorization/validation failures without changing suppression state.
9. **Keep editing and operation distinct.** Engineering authors the stable alarm-to-equipment/location link. Runtime manages active suppression. #790 may bind a CheckBox to the typed alarm-suppression action; it must read back effective state and causes rather than simulate local state or call arbitrary HTTP/scripts.

## Verification required

Unit/integration coverage must include exact alarm, ancestor and descendant scope; siblings excluded; overlapping causes and independent release; expiry and restart; missing/ambiguous operational link; disabled `ShelvingAllowed`; Authority denial/partial-scope rejection; concurrent writes; shared PostgreSQL visibility; separated-DB peer snapshot and fenced restore; fail-closed missing peer state; alarm evaluation/history/ACK unaffected; Runtime list/sound keeps suppressed alarms visible but quiet. Update installed Help and manual in pt-BR/en/es in the same candidate. Run focused tests, the issue's exact T1 profile, then T2 on the exact merge SHA.

## Source

- [ISA-18 series overview](https://www.isa.org/standards-and-publications/isa-standards/isa-18-series-of-standards)
- [ISA InTech: alarm suppression forms and distinctions](https://www.isa.org/intech-home/2017/september-october/features/from-managing-to-optimizing-alarms)
- [Emerson: advanced alarming techniques and shelving/out-of-service distinction](https://www.emerson.kr/is/content/emerson/ko/systems-and-software/deltav-distributed-control-system-dcs/white-papers/documents/advanced-alarming-techniques.pdf)
