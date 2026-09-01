# Coordinator Handoff — 2026-08-31

Timezone: America/Sao_Paulo  
Status: **L3 ACTIVE / BLOCKED ON SLICE B (ACQUISITION) REGRESSION**

## Purpose

This document is the continuity handoff for the next EliteSCADA coordinator chat. It records the live repository state, the current validation blocker, completed prerequisites, and the mandatory sequence forward so project coordination does not depend on chat history.

The next coordinator should read this file first, then issue `#180`, `.github/workflows/l3-seven-driver-lab.yml`, and `docs/DRIVER-INTEROP-LAB.md` before changing code or project sequencing.

## Executive status

- Preview/Demo licensing priority gate: **DONE / VALIDATED / INTEGRATED**.
- Seven-driver convergence and L2 validation: **DONE / ACCEPTED**.
- Integrated L3 seven-driver interoperability: **ACTIVE / NOT ACCEPTED**.
- Wave 11: **BLOCKED** until the complete L3 workflow passes on the exact implementation head and issue `#180` is accepted/closed.

## Current L3 execution

- Tracking issue: `#180`
- Active branch: `coordination/driver-l3-seven-protocol-lab`
- Implementation SHA under the failed L3 run: `65fbb6ee67040610eef4b6ef88073c38e127913b`
- Failed GitHub Actions run: `33434301171`
- Failing job: `99626884954`
- Failed slice: **Slice B — acquisition**
- Workflow: `.github/workflows/l3-seven-driver-lab.yml`
- Lab guide: `docs/DRIVER-INTEROP-LAB.md`
- Current blocker: acquisition Slice B regression/failure. L3 has **not** passed.

### First action for the next coordinator

Open run `33434301171`, job `99626884954`, inspect the complete job log and identify the exact failing Slice B predicate/assertion/tool condition. Isolate the regression and apply only the smallest source or workflow correction justified by that evidence.

Do not infer the exact root cause from this handoff document. The failed job log is the authority for the failure mechanism.

## L3 acceptance gate

Do **not**:

- close issue `#180`;
- declare L3 PASS;
- begin Wave 11;
- merge red or incompletely validated code;
- authorize a fix from an older green workflow run after the branch head changes.

L3 must prove in one EliteSCADA build/runtime, with all seven protocol Data Sources concurrently:

1. acquisition;
2. supported writes/commands;
3. shared readiness;
4. canonical cache identity isolation;
5. one-peer fault isolation;
6. recovery;
7. clean shutdown.

The seven-driver set remains the integrated interoperability target established by the driver convergence workstream.

## Mandatory next sequence

1. Inspect run `33434301171`, job `99626884954`, and capture the exact Slice B failure evidence.
2. Reproduce or isolate the regression on `coordination/driver-l3-seven-protocol-lab`.
3. Apply the minimum justified fix.
4. Run the **complete L3 workflow** on the resulting exact head SHA.
5. Require all L3 slices/gates green on that same SHA. Previous runs are stale as soon as implementation changes.
6. Record the exact SHA/run/job evidence in issue `#180`.
7. Only then mark issue `#180` **ACCEPTED / CLOSED**.
8. Only after `#180` is closed release and begin Wave 11.

## Closed prerequisite: Preview/Demo licensing

Licensing is **not an open workstream**. It was implemented, validated and integrated before L3 resumed.

Accepted behavior includes:

- no valid installed license means Demo mode;
- Engineering may contain more than 200 TAGs;
- Demo Run is allowed only at or below 200 TAGs;
- a Demo Run is limited to 300 continuous minutes per explicit Run session;
- expiry stops the industrial runtime gracefully while Engineering/UI remain available;
- a later explicit Run starts a fresh Demo session;
- valid signed machine-bound licenses remove the Demo duration limit subject to tier capacity;
- tiers: 500 / 1000 / 1500 / 3000 / 5000 / Unlimited;
- versioned SHA-256-derived machine request code;
- asymmetric signed offline licenses;
- external-only private signing key boundary;
- invalid, tampered or wrong-hardware installed licenses fail closed and do not silently fall back to Demo;
- protected licensing API plus the React/Vite `/licensing` product UI using the normal authentication shell;
- offline `EliteSCADA.LicenseGenerator` executable and CI artifact validation.

Authoritative licensing evidence:

- `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`
- `docs/licensing/OFFLINE-LICENSE-OPERATIONS.md`
- issues `#183` and `#184`: completed

Do not reopen licensing merely because coordination moved to a new chat. Reopen it only for a demonstrated defect, regression, security problem, or an explicit new product requirement.

## Coordination rules that remain in force

- Repository/live CI state outranks stale planning prose.
- Exact-head evidence is mandatory for merge/acceptance gates.
- A documentation-only commit does not validate implementation behavior.
- Keep driver protocol specifics inside their adapters; shared host/runtime contracts stay protocol-neutral.
- Do not bypass transactional runtime activation or readiness/cache identity guarantees to make an interoperability test green.
- Wave numbering and project completion estimates must follow the accepted roadmap and live issue state, not optimistic arithmetic.

## Continuity note about this handoff commit

This handoff is a **documentation-only change** on the active L3 branch. Therefore the branch SHA created by this document will differ from the implementation SHA associated with the failed L3 run.

The implementation SHA associated with run `33434301171` remains:

`65fbb6ee67040610eef4b6ef88073c38e127913b`

Do not confuse the later handoff documentation commit with an implementation validation SHA. After the actual Slice B fix, the complete L3 workflow must validate the new exact implementation head.
