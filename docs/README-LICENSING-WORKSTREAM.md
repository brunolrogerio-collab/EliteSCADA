# Licensing Workstream Entry Point

Status: **IMPLEMENTED / VALIDATED — awaiting final documentation-head CI and coordinator integration**

Implementation branch: `product/preview-demo-licensing`

Read:

1. `docs/LICENSING-AND-DEMO-MODE.md` — authoritative product contract;
2. `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md` — implementation/validation evidence;
3. `docs/licensing/OFFLINE-LICENSE-OPERATIONS.md` — controlled generator/key operations;
4. `docs/LICENSING-PRIORITY-GATE.md` — sequencing gate;
5. `docs/PREVIEW-LICENSING-IMPLEMENTATION-PLAN.md` — implementation slices;
6. issues #183 and #184;
7. PR #185.

The historical `SPECIFIED / NOT IMPLEMENTED` wording inside the original contract records the coordinator handoff state before this implementation. The acceptance-evidence document is the current implementation-state authority.

After final exact-head CI succeeds, PR #185 is integrated into `coordination/driver-convergence-v3`, issues #183/#184 close, and the established Driver sequence resumes through PR #175.
