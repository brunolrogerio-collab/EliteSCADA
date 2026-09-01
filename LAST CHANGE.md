# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **PRE-WAVE-11 REPOSITORY/CI HYGIENE GATE ACTIVE — Wave 11 implementation paused, not cancelled**

> This file is the mutable coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Always verify live refs/Actions before acting because documentation-only `[skip ci]` commits may advance `main` beyond the latest validated code SHA.

## 1. Last accepted product checkpoint

Pre-Wave-11 owner-usability gate #191 is **COMPLETE / ACCEPTED / INTEGRATED**.

- implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- PR #193: **MERGED**;
- validated main code merge: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- pre-merge EliteSCADA CI #1033 / `33525910566`: **SUCCESS**;
- pre-merge Preview Licensing CI #90 / `33525910582`: **SUCCESS**;
- pre-merge L3 Seven-Driver Lab #39 / `33525910552`: **SUCCESS**;
- post-main Preview Licensing CI #92 / `33527294658`: **SUCCESS**;
- post-main EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of one transient IEC-104 timing failure**.

Post-main License Generator artifact:

- artifact `EliteSCADA-LicenseGenerator-win-x64`, id `9808306320`;
- `EliteSCADA.LicenseGenerator.exe`, 116,257,103 bytes;
- executable SHA-256 `841dea832d67f44e07aa10b2de96ccfffd5d518beeadafb48ed34e16d0317523`.

Seven-Driver convergence was integrated through PR #187 and the integrated L3 gate is completed/accepted in issue #180. L3 is evidence and regression validation now, not an open convergence stage.

## 2. Current coordination gate — repository and CI hygiene

Development Lead requested repository/CI sanitation before further Wave 11 implementation so GitHub reflects actual active work and specialized CI stops running on unrelated PRs.

Active hygiene branch:

- `coordination/ci-hygiene-pre-wave11`;
- base: live `main` `ab787c98350861566d06d1de07ade0b08c82ce3e`;
- current head at last checkpoint: `17b7064902209cbabec20beae335ab10e78d58ca` plus any later documentation commits on this branch;
- PR #196: **OPEN / READY FOR VALIDATION**, `Repository hygiene: scope specialized CI before Wave 11 resumes`.

### CI routing implemented in PR #196

`EliteSCADA CI` remains the universal Coordinator acceptance gate for PRs to `main`.

`Preview Licensing CI` now automatically selects changes affecting licensing, License Generator, product capacity, known licensing-sensitive shared paths, related tests/docs, or its own workflow. It remains manually executable for cross-cutting/release validation.

`L3 Seven-Driver Lab` now automatically selects changes affecting Drivers, DriverHost, communication/Gateway contracts, TAG/event core, Driver tests, interoperability lab, or its own workflow. It remains manually executable for cross-cutting host/composition/release validation.

No specialized workflow test body or acceptance assertion was weakened. The durable rule is recorded in `docs/CI-VALIDATION-POLICY.md`.

Important repository fact: `main` currently has no branch protection / required status checks configured. Therefore `EliteSCADA CI required` is an operational Coordinator/Development Lead rule, not currently a GitHub-enforced merge block. Do not document otherwise.

### Repository sanitation already applied

The following stale/superseded open PRs were closed with comments preserving successor/integration lineage:

- #108 DNP3 worker handoff;
- #109 BACnet worker handoff;
- #111 CIP/EtherNet-IP worker handoff;
- #128 MQTT worker handoff;
- #135 Siemens S7 worker handoff;
- #146 IEC-104 worker handoff;
- #167 DNP3 L2 validation;
- #169 OPC UA worker handoff;
- #176 OPC UA L2 validation;
- #177 BACnet L2 validation;
- #179 secure OPC UA L2 validation;
- #181 obsolete licensing workstream;
- #182 obsolete licensing validation surface.

After this cleanup, the only active development PR from the previous inventory is Wave 11 PR #195; PR #196 is the temporary hygiene gate itself.

Completed Driver coordination issues #120, #121, #122 and #123 were closed as completed because their convergence purpose has already been integrated through PR #187 / issue #180.

Issue #178 remains open but is retitled **`[Deferred L4] Driver 8: Siemens hardware/vendor-simulator interoperability validation matrix`** and explicitly classified as future physical/vendor evidence, not a Wave 11 blocker and not an active convergence task.

The repository still contains many historical branches. The current GitHub connector exposes branch create/update but not branch deletion. Do not rewrite or move historical refs merely to make the branch list smaller. Mechanical deletion of clearly obsolete closed-PR branches remains a repository-maintenance follow-up when a supported delete-ref path is available.

## 3. Wave 11 state

Wave 11 remains the next development stage and its acceptance criteria are unchanged.

- issue #194: **OPEN / ACTIVE, temporarily paused behind hygiene gate**;
- draft PR #195: **OPEN / DRAFT**;
- branch: `coordination/wave11-hmi-runtime`;
- branch head before hygiene pause: `2d85b910f7bcca4239bde54f71e8e81cd883ffe2`;
- Wave 12: **BLOCKED until #194 is accepted/closed**.

PR #195 contains the current Wave 11 implementation foundation: protected Active Engineering Runtime projection, canonical Runtime application mount with explicit simulation fallback, revision-stable mount behavior, dedicated Active A -> Working isolation -> Active B lifecycle test infrastructure, and the first asset-authority seam. Its first dedicated lifecycle run exposed `RUNTIME_NO_ACTIVE_SOURCES` because the Demo fixture used `builtin.simulation`; the test fixture was corrected to use deterministic `builtin.memory.server` rather than weakening activation rules.

No further Wave 11 code should be added until PR #196 is integrated and the Wave 11 branch is reconciled to live `main`.

## 4. Exact next action

1. let PR #196 run **EliteSCADA CI**, **Preview Licensing CI** and **L3 Seven-Driver Lab** once, because the PR changes both specialized workflow files;
2. if any workflow fails, fix the routing/workflow defect without weakening test bodies;
3. merge PR #196 normally only when all three are green;
4. verify post-main repository/workflow state and synchronize `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md` and `docs/CURRENT-COORDINATOR-HANDOFF.md` as needed;
5. reconcile `coordination/wave11-hmi-runtime` with the new live `main` without losing Wave 11 work;
6. confirm an unrelated Wave 11-only change no longer automatically triggers Preview Licensing CI or L3 Seven-Driver Lab;
7. resume issue #194 / PR #195 from its existing implementation head and continue the Active Engineering HMI Runtime acceptance sequence.

Wave 13 remains the mandatory Authenticode + trusted timestamp Windows release-signing stage. Physical L4 remains later Preview/device-specific validation.
