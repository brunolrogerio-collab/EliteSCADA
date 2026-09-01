# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **WAVE 11 ACTIVE / RESUMED — issue #194 / draft PR #195**

> Repository/CI state is the continuity source. Read live refs and Actions before acting. Stable product intent is governed by `PROJECT GOAL.md`; exact mutable state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/ROADMAP.md`;
5. `docs/CI-VALIDATION-POLICY.md`;
6. live `main`, issue #194, draft PR #195 and `coordination/wave11-hmi-runtime`;
7. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` only when Driver evidence is relevant.

## 2. Accepted pre-Wave-11 foundation

Pre-Wave-11 issue #191 is **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.

Repository/CI hygiene is also **COMPLETE / ACCEPTED / INTEGRATED**:

- PR #196 merged at `95abfad5c35f2715e15422f726d93013fd7290fd`;
- PR #197 exact implementation head `01fc71df4162c3728073ae4fc056d04429349e4f` merged to product-code `main` `e117849827f1409ad6dd383dbdc2ed936ce62567`;
- pre-merge #197: EliteSCADA CI #1057 **SUCCESS**, Preview Licensing #115 **SUCCESS**, L3 #62 **SUCCESS**;
- post-main #197: EliteSCADA CI #1058 **SUCCESS**, Preview Licensing #116 **SUCCESS**, L3 #63 **SUCCESS**.

CI specialization now reflects actual ownership:

- EliteSCADA CI = universal Coordinator acceptance gate;
- Preview Licensing = licensing/capacity/License Generator and known licensing-sensitive paths, plus manual/release validation;
- L3 Seven-Driver Lab = Drivers/DriverHost/communication/Gateway/TAG-event/Driver tests/interop lab, plus manual/cross-cutting/release validation.

Wave-11-only HMI changes have demonstrated that Preview Licensing and L3 are no longer automatically started when their subsystems are unaffected.

`main` currently has no GitHub branch protection / required status checks; the universal CI rule is operational, not GitHub-enforced.

## 3. Hygiene cleanup retained as history

Superseded/historical PRs #108, #109, #111, #128, #135, #146, #167, #169, #176, #177, #179, #181 and #182 are closed with lineage retained.

Driver coordination issues #120-#123 are closed. Issue #178 remains open only as `[Deferred L4] Driver 8: Siemens hardware/vendor-simulator interoperability validation matrix` and is not a Wave 11 blocker.

Historical branch refs remain because the connected GitHub action set does not expose safe delete-ref. Do not repoint old refs for cosmetic cleanup.

## 4. Wave 11 current state

- issue #194 — **OPEN / ACTIVE**;
- branch `coordination/wave11-hmi-runtime`;
- draft PR #195 — **OPEN / DRAFT**;
- pre-reconciliation current head: `b2c93cbfca3c4794796a0b251ab612b32ee08fed`;
- Wave 12 remains blocked until #194 closes.

Required lifecycle authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Existing Wave 11 implementation includes:

- protected `/api/runtime/application` projection from persisted Active revision;
- fail-closed project/revision/persistence/package consistency boundaries;
- Runtime `View` authorization without granting operator Working Engineering access;
- canonical Screen/Popup/Dynamo renderer/navigation mount;
- explicit Simulation fallback when no Engineering runtime is active;
- stable mount while Active project/revision identity is unchanged;
- Active visual-asset endpoint with SHA-256/media type/byte length validation;
- explicit Runtime asset resolver propagated through Runtime mount, Navigator and canonical renderer;
- deterministic `builtin.memory.server` lifecycle fixture because `builtin.simulation` is intentionally non-activatable.

Current exact pre-reconciliation evidence:

- Wave 11 Active HMI Runtime #8 / `33544807476`: **SUCCESS**;
- browser lifecycle proves Simulation -> Active A -> Working remains isolated -> Active B;
- it also proves fail-closed projection, operator Runtime View vs Working denial, and a real imported PNG loaded from `/api/runtime/visual-assets/{id}/content`;
- EliteSCADA CI #1061 / `33544807512`: Web build **SUCCESS**, backend build/tests/runtime smoke **SUCCESS**, Chromium E2E was still running when this handoff was written. Re-read live Actions before acting.

## 5. Owner-test Demo deliverable

When the demonstration application is finalized for testing with EliteSCADA Preview, provide the actual portable application artifact, preferably `.escadapkg`, usable through the normal Save/Open workflow. Source code or CI screenshots do not satisfy this owner-test delivery requirement.

Issue #194 carries the requirement and an existing condition watch will notify the owner when a finalized accepted artifact is available.

## 6. Exact next action

1. re-read EliteSCADA CI #1061 on exact Wave 11 head `b2c93cbf...`; diagnose any red instead of blind rerun;
2. fetch the final live documentation-advanced `main` SHA;
3. reconcile `coordination/wave11-hmi-runtime` with that live `main` using a true merge that preserves both histories, not force/repoint/copy-over;
4. remove the obsolete CI-hygiene pause paragraph from PR #195 and comment the reconciled head on issue #194;
5. require exact reconciled-head Wave 11 dedicated workflow + universal EliteSCADA CI green; specialized workflows should remain unselected unless actual paths/impact require them;
6. verify remaining #194 acceptance criteria, then mark #195 ready only when genuinely complete;
7. merge normally to `main`, require exact post-main CI, then accept/close #194 and synchronize `PROJECT GOAL.md` review, `LAST CHANGE.md`, ROADMAP and this handoff;
8. only then authorize Wave 12.

## 7. Non-negotiable rules

- repository/CI state overrides stale chat/prose for implementation truth;
- no red universal CI into `main`;
- specialized path filters never excuse manual validation when architectural impact demands it;
- no test weakening to manufacture green evidence;
- Runtime presentation never reads mutable Working as Active truth;
- no Driver-to-Driver calls or canonical TAG/cache/event bypass;
- no plaintext protected material;
- licensing remains host-owned and private signing material never enters GitHub/CI/distributed product;
- every material coordination transition is persisted before claiming completion.
