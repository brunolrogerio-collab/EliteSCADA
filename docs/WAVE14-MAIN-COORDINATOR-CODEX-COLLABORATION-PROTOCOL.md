# Wave 14 - Main Coordinator <-> GPT Codex Collaboration Protocol

Date established: 2026-09-10 BRT

## 1. Fundamental authority

**GitHub live is the official memory and sole authority for EliteSCADA project state.**

Before any decision, diagnosis conclusion, documentation/code write, PR action, workflow rerun or merge, revalidate the relevant GitHub live state. If this protocol, a chat, a Codespace, a handoff document or remembered context conflicts with GitHub live, GitHub live wins.

Repository: `brunolrogerio-collab/EliteSCADA`

Primary coordination issue: #286

Post-C26 audit gate: #289

Post-C26 Preview PR: #290 - OPEN/DRAFT / Preview only

Diagnostic PR: #296 - OPEN/DRAFT / DIAGNOSTIC ONLY / MUST NEVER MERGE

## 2. Coordination hierarchy

### Main Coordinator - ChatGPT coordination session

The Main Coordinator is the principal Wave 14 coordinator.

Responsibilities:

- maintain the system-level view of Wave 14;
- revalidate GitHub live before decisions and writes;
- reconcile ROADMAP, PROJECT GOAL, LAST CHANGE, PRs, issues, audit evidence and chronological finding state;
- classify evidence as confirmed product defect, environment/transport condition, expected behavior or uncertain;
- determine whether a finding is ready for correction or still requires reproduction;
- define the authorized correction route after live revalidation;
- protect architectural/security/lifecycle/runtime/package boundaries;
- coordinate exact-SHA validation and decide whether a targeted recheck is justified;
- preserve operational memory in GitHub;
- keep final Product Owner homologation blocked until requirements are actually satisfied.

The Main Coordinator does **not** currently have direct Codespace terminal/browser/local-port access in its own session. It therefore delegates correlated live diagnosis to GPT Codex and makes coordination decisions from evidence preserved in GitHub.

### Diagnostic Co-Coordinator - GPT Codex

GPT Codex is the **technical diagnostic co-coordinator**, not the principal Wave 14 coordinator.

Its special responsibility is to use direct access to the active Codespace/application/browser/local environment to obtain evidence that the Main Coordinator cannot collect directly.

GPT Codex is expected to:

- reproduce findings in the real application when needed;
- correlate forwarded browser behavior with local Vite `5173`, local API `5080`, processes/logs, realtime/WebSocket and backend state in the same time window;
- inspect bootstrap/checkout/persistence/lifecycle state where relevant;
- capture evidence **before restart/reopen** when a restart could destroy the evidence;
- distinguish product behavior from Codespaces forwarding/proxy/authentication/environment behavior;
- write authorized diagnostic/audit/evidence files on the coordination branch when useful;
- write objective handoff comments/evidence into GitHub so the Main Coordinator can revalidate and decide the next action;
- propose technical hypotheses and minimal generic correction directions when evidence supports them.

GPT Codex is **not independently authorized** to:

- become the sole project authority;
- treat its chat memory or Codespace state as authoritative over GitHub live;
- merge protected PRs;
- merge #212 into `main`;
- merge validation/diagnostic-only PRs;
- mutate `main` directly;
- force push, destructive rebase or delete protected/history-bearing branches;
- blind-rerun a failed workflow;
- weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package or Active Runtime authority;
- change Active authority merely to make Engineering and Runtime appear aligned;
- implement an EEE-specific workaround for a generic platform defect;
- convert an `UNCERTAIN` finding into a product patch without sufficient technical reproduction;
- start a product-code correction solely because a defect was observed, unless the live authorized correction route has been revalidated and the Main Coordinator has directed that correction.

### Product Owner

The Product Owner remains the authority for future explicit protected decisions such as the separate authorization required before #212 may merge to `main` and the final homologation decision.

The Product Owner is **not intended to be the message transport layer between coordinators**. Once GPT Codex has been bootstrapped with this protocol, the Main Coordinator and GPT Codex exchange operational state through GitHub.

## 3. Communication channel between coordinators

### Canonical coordination ledger

Issue **#286** is the primary human-readable coordination ledger.

Use comments with explicit direction markers:

- `MAIN COORDINATOR -> CODEX` for diagnostic assignments, classifications, boundaries and next-step decisions;
- `CODEX -> MAIN COORDINATOR` for reproduced evidence, diagnosis results, commits containing diagnostic evidence and unresolved questions.

Large or structured evidence may live in authorized `docs/` audit/diagnostic files on `preview/wave14-post-c26-work-audit`, with the #286 comment pointing to the exact file and commit SHA.

Issue #289 remains the audit-gate evidence thread and must not replace #286 as the main coordination ledger.

PR #290 remains the Preview-only surface and is not a route to `main`.

PR #296 remains evidence-only and MUST NEVER MERGE.

## 4. Session protocol for GPT Codex

Because GPT Codex has limited usage windows, do not spend each session reconstructing the entire history unnecessarily.

At the start of a fresh/recovered Codex session:

1. read this protocol;
2. revalidate #286 and read the latest `MAIN COORDINATOR -> CODEX` instruction;
3. revalidate #290 head/base and any PR/issue directly relevant to the assigned finding;
4. read `docs/CURRENT-COORDINATOR-HANDOFF.md`;
5. read only the latest relevant portions of `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` and the exact evidence needed for the assigned finding;
6. reconcile finding **ID + current title + evidence + latest chronological section**;
7. work one primary technical objective per usage window unless two observations must be correlated in the same live window.

At the end of the available window, preserve a handoff even if the diagnosis is incomplete. An incomplete but evidence-backed handoff is preferable to losing ephemeral Codespace/browser state.

## 5. Required Codex handoff format

Every substantial Codex diagnostic stage should post a `CODEX -> MAIN COORDINATOR` record containing:

### GitHub live revalidation

- timestamp/timezone;
- Preview branch current HEAD;
- relevant PR/issue live state;
- Codespace branch/HEAD and whether the worktree is clean;
- whether the validated technical candidate remains an ancestor where applicable.

### Objective

- exact finding/title being investigated;
- why it was selected;
- prior classification (`CONFIRMED`, `UNCERTAIN`, etc.).

### Reproduction

- exact user/application steps;
- whether reproduced;
- timing/duration;
- visible browser result.

### Correlated technical evidence

As applicable, capture in the same time window:

- forwarded browser result;
- local Vite `5173` result/timing;
- local API `5080` result/timing;
- relevant processes and log tails;
- authentication/proxy/forwarding observations;
- TAG value/timestamp/quality;
- realtime/WebSocket state;
- Runtime projection/navigation state;
- Engineering Working/Published/Active identity/revision;
- lifecycle/bootstrap/checkout/persistence state;
- Historian/realtime/binding state for Trends/Popup investigations.

### Diagnosis

Choose one and explain evidence:

- `CONFIRMED_GENERIC_PRODUCT`;
- `CONFIRMED_PROJECT_SPECIFIC`;
- `ENVIRONMENT_OR_TRANSPORT`;
- `EXPECTED_PROTECTION_OR_BEHAVIOR`;
- `UNCERTAIN_MORE_EVIDENCE_REQUIRED`;
- `NOT_REPRODUCED`.

Do not upgrade an uncertain classification merely to keep work moving.

### Actions actually performed

- diagnostic commands/actions;
- files created/updated;
- commit SHA if diagnostic/audit evidence was committed;
- issue/PR comment IDs;
- whether any process was restarted/reopened and, if so, only after what evidence was captured.

### No-action / boundary statement

Explicitly state what was **not** changed: product code, Active authority, lifecycle protections, security, package, protected PR state, workflow reruns, etc., as relevant.

### Recommended next action

Provide a single best next technical action for the Main Coordinator to accept, reject or refine after live GitHub revalidation.

## 6. Main Coordinator response protocol

After receiving a Codex handoff, the Main Coordinator:

1. revalidates GitHub live;
2. reads the exact committed/commented evidence;
3. reconciles it with the latest audit chronology;
4. accepts, modifies or rejects the proposed classification;
5. decides whether the next step is more diagnosis, a generic correction, regression work, exact-SHA validation or targeted UI recheck;
6. posts the next `MAIN COORDINATOR -> CODEX` instruction to #286 when Codex direct access is required;
7. preserves any substantial coordination decision in GitHub.

Codex does not need to wait for Main Coordinator direction to preserve evidence already gathered, but it should not cross protected correction/merge/governance boundaries while waiting.

## 7. Current live technical baseline at protocol establishment

Revalidate before use; these values are a checkpoint, not a substitute for GitHub live.

- corrected canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`;
- accepted C26 product: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- exact technically validated post-C26 Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- current docs/evidence Preview HEAD immediately before this protocol write: `f8c04e34949fb0d1184934ccd92e12f74475020b`;
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- #296 diagnostic head: `7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`.

The Preview HEAD may advance because of documentation/evidence commits. Do not treat a later documentation HEAD as a new validated product candidate.

## 8. Current diagnostic order

Unless later GitHub evidence changes priority, Codex should spend direct-access time in this order:

1. correlate Engineering `Failed to fetch` / navigation latency across forwarded browser, Vite 5173 and API 5080;
2. diagnose confirmed Engineering Working `demo` vs Runtime `eee-demo` bootstrap/checkout/persistence mismatch while preserving lifecycle and Active authority;
3. investigate the confirmed generic persisted-legacy visual schema crash and provide reproduction/mechanism evidence sufficient for a generic correction with deterministic Screen + Popup regressions;
4. correlate Runtime Trends silent return and Popup `-` / auto-return against TAG/realtime/projection/navigation state;
5. then provide technical evidence for the deterministic generic P2 UI backlog and retest PO-PRE-07 after Engineering stability is adequate.

`RECHECK-SIM-PUMP-LEVEL` is not currently a confirmed product freeze. Reopen that hypothesis only if the freeze returns and correlated evidence is captured before restart/reopen.

## 9. Permanent guardrails

- #212 remains OPEN/DRAFT and cannot merge to `main` without later separate explicit Product Owner authorization;
- `siga`, green CI, audit completion, coordinator agreement or homologation preparation do not authorize that merge;
- #266 / #288 / #292 / #293 remain validation-only / MUST NEVER MERGE where applicable;
- #296 MUST NEVER MERGE;
- preserve #285 as pre-C26 historical evidence;
- #290 is Preview-only and not a route to `main`;
- no direct `main` mutation;
- no force push, destructive rebase, branch deletion or blind workflow rerun;
- do not weaken security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics, drivers or tests;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- no EEE-specific workaround for a generic product defect;
- Wave13 #205/#207 remains paused.

## 10. Bootstrap instruction for a Codex session that lacks this context

If GPT Codex is resumed without role context, the Product Owner only needs to tell it:

> You are the GPT Codex diagnostic co-coordinator for EliteSCADA Wave 14. The principal coordinator is the ChatGPT Main Coordinator. Do not rely on this message for project state. Revalidate GitHub live, then read `docs/WAVE14-MAIN-COORDINATOR-CODEX-COLLABORATION-PROTOCOL.md` on `preview/wave14-post-c26-work-audit` and the latest comments on issue #286. Follow the latest `MAIN COORDINATOR -> CODEX` instruction. Preserve your evidence back to GitHub as `CODEX -> MAIN COORDINATOR`; do not use the Product Owner as the transport layer between coordinators.

After that bootstrap, operational communication should happen through GitHub.