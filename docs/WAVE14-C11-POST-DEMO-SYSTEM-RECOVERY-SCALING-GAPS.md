# Wave 14 C11 — Post-DEMO System Recovery, Scaling and Display Gaps

**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decision during canonical C11 EEE DEMO construction  
**Sequence:** finish canonical Simulation DEMO first; resolve these generic product gaps before fresh Codespace Product Owner homologation and before constructing the real Modbus/PLC EEE variant.

> These are product/system requirements. Do not hide them inside the EEE fixture, an EEE-only Script, a visual expression, or a private package format.

### Product Owner closure update — System Recovery / Backup & Restore

The broad backup/restore gap recorded below has now been refined into a locked product design.

Read:

`docs/WAVE14-POST-DEMO-SYSTEM-RECOVERY-BACKUP-RESTORE.md`

That document is authoritative for future Backup/Restore implementation and fixes the recovery architecture as separate authorities:

- application/project portability remains the existing `.escadapkg` mechanism;
- database/Historian recovery uses the supported native database backup/restore facilities;
- Security Authority receives a dedicated encrypted Export / Preview / Import protected by a user-supplied master export password;
- a fresh installation enters a controlled System Recovery Bootstrap with a provisional Recovery Administrator and provisional workspace;
- if an imported authority user collides with the newly created Recovery Administrator, the new local recovery user has priority and is not overwritten;
- System Recovery can be finalized only after a usable administrator and Active production application are verified.

Do not reinterpret sections 2–3 below as requiring one monolithic EliteSCADA system-backup file. They remain the gap history/context; the dedicated System Recovery document is the implementation contract.

## 1. `.escadapkg` responsibility

Product Owner direction is that `.escadapkg` should remain an **application/project package**.

It should not become a machine/system backup containing local user passwords, Historian samples, host secrets or other installation-specific state merely to make computer migration convenient.

The intended separation is:

- `.escadapkg`: application Engineering definition and portable project content;
- identity/authentication store: system-level state;
- Historian database: external/system-level state;
- host/runtime configuration and secrets: system-level state;
- licensing/trust material: system-level state subject to its own security rules.

Do not couple these stores by silently embedding credentials/history into the application package.

## 2. System backup / restore is still required

Application portability alone is not sufficient for disaster recovery or migration to another computer.

EliteSCADA therefore needs a coherent **system backup / restore** concept and an Administration surface or documented controlled workflow that can restore an installation without pretending that `.escadapkg` is the whole machine.

The product gap must be audited and designed around at least:

1. application/projects and persisted revisions as applicable;
2. local identities/users/roles and authentication state, without exposing plaintext passwords;
3. Historian database/data;
4. Alarm/Event/Audit persistent data where stored outside the project package;
5. necessary host configuration;
6. configuration/secrets that may be exportable only through protected/encrypted handling;
7. explicit exclusions such as machine-bound licensing/trust material when portability would violate security/licensing rules;
8. backup metadata/versioning and compatibility validation;
9. restore preview/validation where practical;
10. safe behavior when restoring to a fresh machine versus restoring over an existing installation.

Do not implement a blind filesystem ZIP or database overwrite as a shortcut. Recovery semantics must be explicit and protected.

## 3. Historian import/export / administration

The Historian is intentionally external to `.escadapkg`.

Product Owner agrees with keeping the historical database separate, but wants a normal Administration capability/operational workflow for backup, export, import and restoration of historical data.

Current product uses external PostgreSQL/TimescaleDB surfaces in validation. The post-DEMO audit must determine what is already supported and what generic Administration capability is missing.

The desired product behavior is not necessarily a proprietary historical-file format. It is a supported, safe, operator/admin-facing way to:

- export/backup Historian data;
- import/restore it;
- understand project/TAG identity compatibility;
- avoid accidental duplicate/cross-project history;
- report failures rather than partially restoring silently.

This is a system-recovery concern, not an EEE-specific concern.

## 4. TAG engineering scaling is a confirmed product gap

The real EEE material demonstrates a normal PLC pattern: a Holding Register may contain an integer/raw engineering representation while the HMI must operate in physical units with decimals.

Canonical example:

- Modbus Holding Register raw value = `100`;
- intended process value = `1.00 m`.

EliteSCADA needs first-class generic TAG scaling so the semantic TAG/runtime value can be expressed in engineering units without requiring every Screen, Alarm, Trend or Script to repeat the conversion.

Minimum contract to design/prove:

- deterministic raw -> engineering conversion;
- at least affine scaling (`engineering = raw * gain + offset`) or an equivalent raw-range/engineering-range representation;
- datatype/range/overflow validation;
- engineering unit remains explicit;
- server/runtime authority performs scaling before normal consumers use the value;
- HMI, Alarm evaluation, Historian and Trend consume the scaled engineering value consistently;
- quality/timestamp semantics are preserved;
- write-enabled TAGs require explicit inverse scaling semantics or must fail closed if inverse conversion is not valid;
- configuration is authorable through normal Engineering, persists through Save/Publish/Activate and survives project export/import;
- no Driver-specific EEE logic.

For the later real EEE Modbus variant, the Modbus address list supplied by the Product Owner remains the primary authority for addressing. Canonical EliteSCADA TAG names may remain normalized and semantic.

## 5. Analog display decimal places is a confirmed product gap

The operator must be able to configure how many decimal places an analog value displays, independently from the raw transport datatype where appropriate.

Examples:

- level: `1.00 m`;
- pressure: e.g. `2.35 bar`;
- current/frequency may use one decimal;
- counters may use zero decimals.

C11 fixture code currently uses formatting metadata in places, but Product Owner requires this to be a normal, explicit product capability rather than fixture-only knowledge.

The post-DEMO gap audit must ensure ordinary Engineering can author and persist decimal-place formatting for applicable visual numeric/value objects, and that Runtime rendering honors it after Save/Publish/Activate and package round-trip.

Formatting must not change the canonical numeric value. Scaling changes the engineering value; decimal places change only its presentation. Keep those concepts separate.

## 6. Sequencing gate fixed by Product Owner

The required sequence is now:

1. finish the canonical self-contained EEE Simulation DEMO;
2. close its current repository/browser validation and exportable `eee-demo` application package;
3. audit and implement/resolve generic system backup/restore + Historian administration gaps required for credible system restoration;
4. implement/resolve generic TAG scaling;
5. implement/resolve analog display decimal-place authoring/runtime persistence;
6. revalidate exact product SHA through normal CI/specialized gates;
7. update Preview harness to use the canonical EEE DEMO;
8. Product Owner performs fresh Codespace visual/product homologation;
9. correct any findings and revalidate;
10. only after those gates, build the second EEE project/package using the real Modbus addresses to communicate with the existing PLC logic.

Scaling/decimal gaps do **not** need to stop finishing the current Simulation DEMO unless the DEMO itself cannot be completed without a generic mechanism. They **do** block the subsequent fresh Codespace homologation and the real Modbus/PLC variant.

## 7. Real EEE reference authority

Read:

`docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`

The Product Owner supplied original FvDesigner material:

- `tags(1).csv`;
- `alarm.csv`;
- 14 unique real-HMI Screens (15 PNG files were supplied because one `ESCALAS` capture was duplicated).

The mapping document preserves the required address/semantic authority. Do not ask the Product Owner to resend the CSVs or reconstruct the addressing from memory.

Important semantic rule: the historical `alarm.csv` mixes actual alarms and operational/status events. The canonical EliteSCADA DEMO must keep **Alarm**, **Operational Event**, and **Audit** distinct.
