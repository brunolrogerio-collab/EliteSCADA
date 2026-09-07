# LAST CHANGE

**Wave 14 / C25 status after integrated audit — 2026-09-06 BRT**

C25 remains **ACTIVE / NOT ACCEPTED / NOT INTEGRATED**.

Completed checkpoints:

- C25.0 through C25.7 — COMPLETE;
- C25.8 Contextual multilingual Help/manual — **COMPLETE / exact-SHA green after acceptance remediation**;
- C25.9 Integrated regression/audit — **COMPLETE**.

Current exact product/test authority:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Exact evidence:

- Wave 14 C25 Post-Demo #314 / `34072225644` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #301 / `34072225635` — SUCCESS.

The C25.8 remediation closed the installed-manual coverage gap discovered by the earlier C25.9 audit. The Help now covers the mandatory product matrix in pt-BR/en/es, uses canonical Driver/source descriptors and schemas, preserves exactly 8 production communication Drivers including Modbus while excluding `builtin.simulation`, documents only shipped Server Script APIs/contracts, and preserves C25.6 `.escadalib` semantics including creation/export/Inspect and no Runtime dependency on external libraries.

C25.9 revalidated Engineering Lock, lifecycle/Active authority, reusable libraries, Authority backup, System Recovery, atomic replacement, Distributed Runtime, Help, browser flows, OpenDNP3 Linux/Windows, real OpenDNP3↔dnp3py interoperability and the Windows commercial publish dependency gate. No remaining C25 product-code blocker was found.

The audit also found stale coordination documentation/PR prose. The current execution log and `CURRENT-COORDINATOR-HANDOFF.md` supersede those stale records; PR #283/#212 metadata and issue #282 must be kept synchronized with the live state.

## Next

C25.10 — prepare the exact final candidate/evidence matrix and obtain **explicit Product Owner acceptance**.

No merge is authorized yet.

Permanent boundaries remain unchanged:

- #283 stays OPEN/DRAFT -> `wave14/corrections-integration`;
- #212 stays OPEN/DRAFT and MUST NOT merge to `main` without later explicit Product Owner authorization;
- C11 remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3`;
- #266 remains validation-only and MUST NEVER MERGE;
- Wave 13 #205/#207 remains paused;
- no direct `main` mutation, force push, destructive rebase, blind CI rerun or contract weakening.
