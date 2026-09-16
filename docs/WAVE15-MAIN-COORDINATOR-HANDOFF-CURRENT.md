# Wave 15 — Main Coordinator Handoff (Current)

> Persistent current operational handoff for coordinator rotation.  
> **GitHub live is the source of truth.** Revalidate exact branch/SHA/tree, newest issue comments, PR state and exact-head Actions before any material action.

**Snapshot date:** 2026-09-16 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Snapshot integration SHA:** `456c66f4966ab5302831f642a39690ae3a3402a5`  
**Snapshot tree:** `f42d442933ded9bcf4290ae437193f2d1bb3d492`

The older `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` is retained as historical evidence through the earlier AUTH-03/AUTH-04 period. Its old active-state sections are **not** current sequencing authority.

## 1. Mandatory first actions for a new Main Coordinator

Before assigning, coding, reviewing, rerunning CI, merging or freezing:

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md` and this file;
4. read `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` and `docs/CHAT-COLLABORATION-PROTOCOL.md`;
5. inspect live #297, #305 and the currently active foundation issue #301;
6. re-fetch `wave15/corrections-integration` and record exact SHA/tree;
7. inspect all active Work/Codex branches/PRs and latest comments;
8. inspect exact-head CI evidence;
9. distinguish repository evidence from unpublished chat/worktree progress.

Never continue from a remembered SHA merely because it appears in this document.

## 2. Current Foundation state

State model:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A shared contract is consumable only when the required slice is `VERIFIED + FROZEN`.

Current snapshot:

- **FND-01 Working/lifecycle/bootstrap — VERIFIED/FROZEN**.
- **FND-02 Security Authority — VERIFIED/FROZEN**, including AUTH-04.
- **FND-08 common timing — VERIFIED/FROZEN**.
- **FND-03 Runtime Session Lease / Licensing v2 — ACTIVE / NOT FROZEN**.
- **FND-03 Slice 1 durable Runtime Session Lease identity/persistence — INTEGRATED / VERIFIED** at `456c66f...`.
- **FND-04 — not yet frozen** and now includes the binding Script TAG reference-resolution exit criterion from #305 comment `5701881550`.
- **FND-05/FND-07 — later FC0-B foundations**.
- **FC0-A — BLOCKED**.
- Parallel feature DEVs — **BLOCKED** until Main explicitly records FC0-A.

## 3. Verified FND-03 Slice 1 checkpoint

Integration merge:

`456c66f4966ab5302831f642a39690ae3a3402a5`

Parents:

- `b534f71ec45f1f93b15a93f8626cfcfe652b56ca`
- `9235baddd58871362a4529ccbd5e37ae609de0f7`

Tree:

`f42d442933ded9bcf4290ae437193f2d1bb3d492`

Exact post-merge CI run `35110143733` / #1531:

- backend build/test/smoke — PASS;
- Web build — PASS;
- Chromium end-to-end — PASS.

Coordinator record in #301 explicitly states Slice 1 is INTEGRATED/VERIFIED while FND-03 overall remains ACTIVE/NOT FROZEN.

## 4. Current ACTIVE Codex mission — FND-03 machine-license v2 schema/codec

Latest binding Main Coordinator authorization: #301 comment `5699620231` and #305 summary.

Required base:

`wave15/corrections-integration@456c66f4966ab5302831f642a39690ae3a3402a5`

Authorized branch when published:

`work/w15-fnd-03-machine-license-v2`

Target:

`wave15/corrections-integration`

### Scope

Implement only the machine-license v2 schema/codec foundation:

- preserve exact read/validation compatibility for existing signed machine-bound `ESLIC1` licenses;
- add the signed machine-bound v2 representation, referred to in the coordinator instruction as `ESLIC2`, with explicit `viewOnlySeats`, `interactiveSeats` and `haRuntime` entitlement;
- preserve the existing signature algorithm, hardware fingerprint verification, expiry and Demo behavior unless a narrow compatibility adapter is required by versioning;
- expose new entitlement data through narrow internal/versioned contracts;
- do not infer an Interactive entitlement from ESLIC1;
- preserve existing v1 vectors and trust boundaries.

### Explicitly outside this slice

Do not implement or silently absorb:

- Runtime admission enforcement;
- active shared quota accounting;
- requested-class -> granted-class policy;
- Authority/session-class enforcement changes beyond schema compatibility;
- license install/replace/remove flow;
- License Generator UI;
- Installation UX;
- EliteGO UX;
- HA election/fencing;
- FND-04 Script TAG reference resolution.

### Required deterministic proof

At minimum:

- ESLIC1 compatibility remains exact;
- valid v2/ESLIC2 encode/decode/verification;
- tamper rejection;
- wrong signing key rejection;
- wrong-machine rejection;
- expiry rejection;
- invalid/malformed seat values rejected;
- v2 entitlement fields are covered by the signature;
- no key/license/session state leaks into `.escadapkg`.

Open one reviewable PR only after exact-head validation. Handoff must begin:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`

Report exact base/head, files/symbols, compatibility behavior, tests, CI run/jobs, residual risks and recommended next slice. Codex must not self-freeze FND-03 or release FC0-A.

## 5. Interaction-limit continuity note

The Product Owner reports that Codex **already started the machine-license-v2 slice and paused only because the interaction limit was reached**.

At this snapshot, GitHub shows:

- no branch `work/w15-fnd-03-machine-license-v2`;
- no PR for this slice;
- no completed Codex handoff for this slice.

Therefore the next Codex/Work continuation must first inspect the previous local worktree/session. Do not interpret the absence of a remote branch as proof that no local changes exist. Avoid duplicate implementation.

If the prior local state is unavailable, restart only from the authorized exact base and record that the local unpublished attempt could not be recovered.

The branch `work/w15-fnd-03-runtime-session-lease` belongs to already integrated Slice 1 and is not the current authorized branch.

## 6. FND-04 binding delta — Script TAG reference resolution

Product Owner W15-P1-05 requires normal generated Python to use human-readable canonical TAG references rather than raw GUIDs.

Main review found this is not purely downstream UX because current read/write semantics are inconsistent and path-only lookup would be unsafe after TAG rename/path reuse. #305 comment `5701881550` therefore extends FND-04 with a shared versioned resolution freeze criterion.

Before FND-04 becomes VERIFIED/FROZEN for DEV-SCRIPT-ENGINEERING, prove:

1. product-generated literal `tag_read` / `tag_write` Python uses readable canonical TAG paths;
2. read and write use one shared resolution semantic;
3. runtime/backend resolves to the expected stable `TagId` before authorized operation;
4. stable `TagId` remains the true internal identity authority;
5. Script Engineering retains explicit/versioned binding evidence sufficient to detect identity drift;
6. missing/ambiguous/stale references fail closed;
7. rename/move does not silently retarget source;
8. reuse of an old path by another TagId is rejected as identity drift;
9. diagnostics can expose readable source reference plus expected/resolved stable identity;
10. selector forms preserve the same stable-identity safety.

Required regressions include readable read/write, rename/move, old-path reuse by another TagId, missing/ambiguous/stale behavior and at least two independently bound TAG references in one source.

This delta is **queued behind the active FND-03 mission**. Do not interrupt FND-03 to implement it.

Downstream DEV-SCRIPT-ENGINEERING still owns Object Browser presentation, autocomplete/search, cursor insertion, readable snippets/examples, diagnostics UX, compare-two-TAG recipe and mounted UI regression after the Foundation contract freezes.

## 7. FC0-A release condition

FC0-A requires all of:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

plus:

- FND-08 common timing frozen;
- INFRA-CI-01 ready/frozen;
- one exact integration checkpoint with required evidence.

Only then may Main explicitly release:

- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX.

An individual green PR or partial Foundation slice does not imply FC0-A.

## 8. FC0-B release condition

FC0-B adds:

- FND-05 HA identity/topology/fencing;
- FND-07 secure installation detach/neutral bootstrap.

It releases:

- DEV-ELITEGO;
- DEV-INSTALLATION-UX;
- explicitly delegated DEV-HA-DOWNSTREAM slices.

F0 is complete only at FC0-B.

## 9. Coordination model

### Main Coordinator

Owns dependency graph, exact bases, mission activation, review, integration order, CI disposition, freeze records and escalation.

### Codex / Foundation Work

Owns one active high-risk Foundation mission at a time unless Main explicitly opens another. It may continue independent work while CI runs. Required but unexecuted validation is `PENDING`, never `PASS`.

### Parallel DEVs

Use one isolated branch/PR per bounded work package after dependencies freeze. They do not own shared architecture merely because their feature consumes it. If a frozen contract is insufficient, report `BLOCKED-CONTRACT` rather than redesigning it silently.

## 10. CI rules

- red CI is diagnosed before rerun;
- exact-head evidence only;
- no borrowing green from another SHA;
- no test weakening to force green;
- no empty commit/workflow mutation/PR retarget merely to trigger CI;
- T0/T1/T2/T3/T4 validation remains proportional to risk and checkpoint.

## 11. Permanent architecture guards

- no direct mutation of `main`;
- no destructive history operations;
- no direct feature writes to integration;
- stable IDs outrank mutable names/paths;
- Security Authority, Application/Engineering, Historian/database and License remain separate authorities;
- credentials/private signing material/secrets never enter plaintext Engineering/package/audit;
- Runtime derives from persisted Active, not mutable Working;
- clients do not own Driver/database/private Runtime truth;
- `CommandExecute` remains distinct from `ProcessValueWrite`;
- Session Class only restricts Authority and never grants absent capability;
- Web and EliteGO share server-owned logical lease/quota authority;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct.

## 12. Immediate resume sequence

1. Revalidate integration SHA/tree.
2. Read newest #301 and #305 comments.
3. Re-enter the prior Codex machine-license-v2 session if possible and inspect local/worktree state before making new changes.
4. Continue only the authorized schema/codec slice.
5. Publish a bounded branch/PR and exact-head validation when ready.
6. Review/integrate/verify that slice before authorizing the next FND-03 slice.
7. Do not freeze FND-03 or release FC0-A prematurely.
8. Preserve the queued FND-04 Script TAG reference-resolution requirement for later Foundation execution before Script Engineering is released.

## 13. Authoritative issue map

- #297 — Wave 15 global status;
- #305 — dependency graph / Foundation checkpoints / parallel DEV orchestration;
- #301 — Runtime Session Lease / Licensing v2;
- #302 — Security Authority / FND-02 historical/frozen ledger;
- #303 — Editor;
- #304 — installation detach/switch;
- #298 — EliteGO;
- #299 — HA/redundancy;
- #300 — final integration and fresh Preview.

Read latest comments, not only issue bodies. Issue bodies contain creation-time text that may be superseded by later binding comments.

For Product Owner control, EliteSCADA coordination messages end with current America/Sao_Paulo time in the exact form:

`Hora: HH:MM`
