# Wave 14 - Main Coordinator Workstream Matrix

Checkpoint established: 2026-09-10 BRT

> **GitHub live is the official memory and sole authority.** Revalidate the relevant live issue/PR/branch before every decision, diagnosis conclusion, write, rerun, integration or merge. This matrix is a coordination checkpoint, not a replacement for live state.

Main Coordinator: ChatGPT coordination session designated by the Product Owner.

Diagnostic co-coordinator: GPT Codex with direct Codespace/application/browser/local-port access.

Coordination ledger: #286.

Current technical candidate checkpoint: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`.

Canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`.

Accepted C26: `08e2530671de10d48933c4b712a1a1abc9e41dce`.

Frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`.

## State matrix

| Workstream | State | Owner | Base / evidence authority | Branch / route | Dependencies | Can correct now? | Required regression / proof | CI / validation | Integrated? |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A - Transport / Failed to fetch / latency | DIAGNOSING | GPT Codex | post-C26 Preview + live Codespace | diagnostic evidence on Preview/#286 | same-window browser -> Vite 5173 -> API 5080 -> WS/process/log correlation | **NO** - cause still uncertain | correlated timeline; authoritative vs transient failure separation; recovery behavior | N/A until mechanism/classification permits a fix | No |
| B - Working `demo` vs Runtime `eee-demo` | REPRODUCED / DIAGNOSING | GPT Codex | live UI evidence + lifecycle state | diagnostic evidence on Preview/#286 | bootstrap/checkout/persistence/public-model correlation; preserve Active authority | **NO** until origin is demonstrated and correction route revalidated | clean/rebuilt Working identity; restart/reopen persistence; cross-project activation remains rejected | exact-SHA after authorized fix | No |
| C - persisted legacy visual schema crash | CLASSIFIED / MECHANISM PARTIAL | Main Coordinator; Codex diagnostic support | current audit evidence; legacy `tank`/`value`/`dynamo`; schema lookup path | correction route to be explicitly authorized after live revalidation | distinguish legacy-known aliases/migration from truly unknown types | **NOT YET** - first establish exact minimal generic contract/route | Screen legacy selection; Popup legacy selection; truly-unknown negative test; zero shell blank/crash | local + pertinent exact-SHA gates after fix | No |
| D - Runtime/Engineering recovery UX | BLOCKED | Main Coordinator after Codex diagnosis | audit evidence | route depends on A classification | A | **NO** | retain valid Runtime navigation/subview on transient failure; invalidate on authoritative failure; Engineering load/error no fictitious Working identity | exact-SHA after fix | No |
| E - Runtime Trends silent return | BLOCKED | GPT Codex diagnosis | audit reproduction | diagnostic evidence first | A; Historian/policies/realtime/projection/navigation | **NO** | data / no-data / controlled-error scenarios must remain in Trends with explicit state | exact-SHA after authorized fix | No |
| F - Runtime Popup `-` / auto-return | BLOCKED | GPT Codex diagnosis | audit reproduction | diagnostic evidence first | A; realtime/projection stable; ideally B | **NO** | stopped/running states; zero vs missing data; no silent auto-close; bindings/quality coherence | exact-SHA after authorized fix | No |
| G - Data Source legacy/canonical mapping | QUEUED / REPRODUCED | Main Coordinator | audit evidence | isolated correction route later | B / Engineering stability | **NO**, not priority yet | legacy fixture + save/reopen roundtrip + invalid-type diagnostic | later exact-SHA | No |
| H - Script Engineering / PO-PRE-07 | BLOCKED | GPT Codex real-use recheck + Main Coordinator | partial audit/static evidence | after Engineering stable | B + A/Engineering stability + domain editors | **NO** | complete user-discoverable two-TAG -> visual-state flow; insertion/discovery/runtime separately proven | later exact-SHA + targeted real-use recheck | No |
| I1 - shared responsive header | READY_FOR_ROUTE | Main Coordinator | deterministic UI evidence (notebook/1024 class) | isolated UI correction route may run in parallel | avoid conflict with concurrent shared-shell edits | **YES only after live route revalidation** | Runtime + Engineering at 1024, ~1080, 1366 and wide; zero overlap | local + exact-SHA | No |
| I2 - account-menu accessibility | READY_FOR_ROUTE | Main Coordinator | deterministic audit evidence | isolated accessibility route may run in parallel | minimal; avoid overlapping shared component edits | **YES only after live route revalidation** | accessible names, keyboard, focus and understandable action | local + exact-SHA | No |
| I3/I4 - Engineering navigation + Lock footprint | QUEUED | Main Coordinator | deterministic audit evidence | preferably one coordinated shell/layout line | A/B stabilization; coordinate shared shell/CSS | **NO**, defer behind P1 lanes | own scroll/collapse; canvas dominance; Lock semantics/security unchanged | later exact-SHA | No |
| I5/I6 - resource previews + residual theme | QUEUED | Main Coordinator | deterministic audit evidence + historical C26 theme work | later isolated lines | shell stability; revalidate existing theme regressions first | **NO**, defer | useful preview/fallback; editable/readonly/disabled/placeholder only where still reproducing | later exact-SHA | No |
| J - TAGs / Sources UX identity coherence | QUEUED | Main Coordinator | audit evidence | later Engineering-domain route | B + shell stability | **NO** | selected TAG identity consistent across summary/editor/context and reopen | later exact-SHA | No |
| K - custom roles/capability sets | PARKED / SCOPE DECISION | Main Coordinator | PROJECT GOAL + live requirements + current auth contract | no implementation route yet | determine whether v0.1 requires custom roles | **NO** | only if in-scope: create/assign/persist/enforce/deny correctly | security suites required if implemented | No |
| L - P01/P02 accessible-name residue | QUEUED / NEEDS CLASSIFICATION | Main Coordinator | audit evidence | generic component or EEE package only after classification | prove generic vs project-specific | **NO** | parametrized P01/P02 labels with no cross-reference | later exact-SHA | No |
| M - language/polish | PARKED P3 | Main Coordinator | audit evidence | late polish route | functional stabilization | **NO** | deliberate pt-BR/technical-term policy | later | No |
| N - Alarm timestamp vs ledger | PARKED / UNCERTAIN | GPT Codex opportunistic evidence only | insufficient audit evidence | evidence-only | same occurrence identity across Alarm Center/ledger/timezone/window | **NO** | same-event occurrence/timestamps/UTC/local/emission/restore/ACK/persistence windows | none until classification | No |
| RECHECK-SIM-PUMP-LEVEL | PARKED / NOT CONFIRMED | GPT Codex only if symptom returns naturally | local dynamic LevelPct evidence; #296 failed before effective observation | #296 evidence-only / MUST NEVER MERGE | new correlated natural reproduction before restart/reopen | **NO** | API/Vite/browser/WS/TAG value+timestamp+quality/process/Server Script capture | **NO blind rerun of #296** | No |

## Parallel execution decision

### Lane 1 - Codex live diagnostics - ACTIVE

Current assignment remains #286 comment `5628511409`.

Primary live-access objective: transport/Failed-to-fetch/latency correlation. Working `demo` vs Runtime `eee-demo` must be captured in the same live window where useful because the bootstrap/lifecycle evidence overlaps, but transport findings remain unpatchable while uncertain.

### Lane 2 - legacy editor compatibility - PREPARED, NOT YET FIXING

The product finding is confirmed generic and the likely lookup path is known. Before product code changes, the Main Coordinator must revalidate and define an isolated correction route plus exact regression contract. This lane may proceed in parallel with Codex live diagnosis once that route is explicitly opened.

### Lane 3 - deterministic UI - ELIGIBLE BUT NOT STARTED

Responsive header and account-menu accessibility are sufficiently isolated to be candidates for early parallel work after live route revalidation. Do not start concurrent changes in shared shell/CSS if they would conflict with Navigation/Engineering Lock work.

### Lane 4 - Engineering domain editors - BLOCKED

Data Source, TAG/Sources and Scripts depend on authoritative/stable Engineering Working state. Scripts remains last in this lane.

### Lane 5 - backlog / scope - PARKED

Custom roles, language and residual EEE/accessibility items must not distract from current P1 diagnostic/correction gates.

## Dependency DAG

`LIVE BASELINE -> A TRANSPORT CORRELATION -> D RECOVERY + E TRENDS + F POPUPS`

`LIVE BASELINE -> B WORKING/RUNTIME DIAGNOSIS -> G/J ENGINEERING DOMAIN -> H SCRIPTS`

`C LEGACY SCHEMA MECHANISM -> GENERIC COMPATIBILITY FIX -> SCREEN + POPUP + UNKNOWN-TYPE REGRESSIONS`

`I1 RESPONSIVE HEADER + I2 ACCOUNT ACCESSIBILITY` may run independently after route revalidation.

`P1/SYSTEMIC STABILITY -> I3/I4/I5/I6/J -> integrated exact SHA -> full regression -> fresh Preview evidence -> targeted Work recheck where justified -> Product Owner final homologation`.

## Candidate declaration rule

Parallel branches or commits never become a candidate automatically. The Main Coordinator must know the exact parent/diff of each line, integrate deliberately, run combined regressions, and declare exactly one product SHA. Documentation/evidence HEADs are not product candidates.

## Current protected boundaries

- #212 remains OPEN/DRAFT and requires a later separate explicit Product Owner authorization before merge to `main`;
- `siga`, green CI, audit completion, coordinator agreement or homologation preparation do not authorize that merge;
- no direct `main` mutation, force push, destructive rebase, branch deletion or blind workflow rerun;
- #266/#288/#292/#293 remain validation-only / MUST NEVER MERGE where applicable;
- #296 MUST NEVER MERGE;
- #290 remains Preview-only and not a route to `main`;
- preserve #285 as historical pre-C26 evidence;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- no EEE-specific workaround for a generic platform defect;
- Wave13 #205/#207 remains paused.
