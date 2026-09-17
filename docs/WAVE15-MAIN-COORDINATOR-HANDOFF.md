# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é o **canal primário de ordens do Main Coordinator**. Issues, PR comments e outros documentos podem espelhar decisões/evidências, mas não substituem a ordem ativa deste arquivo.
>
> **PROTOCOLO `SIGA`:** antes de agir, CODEX, DEV ou AUD deve reler este arquivo no GitHub live e executar somente a ordem mais recente destinada à sua lane. Nunca continuar por memória quando este arquivo trouxer estado diferente.
>
> **GitHub live é a autoridade final.** Antes de qualquer mutação, revalidar HEAD/tree, PRs, Actions e o candidate exato.

**Status date:** 2026-09-17 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Main Coordinator:** único emissor das ordens abaixo

---

## 1. ESTADO LIVE DA FOUNDATION

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

### Integração atual

- `wave15/corrections-integration` product checkpoint: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree do checkpoint: `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`
- esse checkpoint contém o merge do PR #330 / Runtime Admission;
- CI pós-merge #1543 validou o checkpoint com Backend, Web e Chromium verdes.

### Foundation

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**
- FND-02 Security Authority, incluindo AUTH-04 — **VERIFIED/FROZEN**
- FND-08 common timing — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 schema/codec + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission / requested->granted / Authority enforcement — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **PR_READY / UNDER COORDINATOR VALIDATION**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

Um PR ou teste verde isolado não implica `VERIFIED/FROZEN`.

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**Mission:** FND-03 Shared Runtime Seat Accounting — candidate validation only

### Exact candidate

- PR: `#331` — `FND-03: enforce shared runtime seat accounting`
- target: `wave15/corrections-integration`
- authorized product base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact candidate head: `09f81e97369089def481ceb25629779a5aba8aff`
- PR state at coordinator validation: OPEN / mergeable / not draft

### Current CI evidence

EliteSCADA CI #1544 / run `35255337014` on the exact candidate:

- Backend build, test and smoke — **SUCCESS**
- Web build — **SUCCESS**
- Chromium end-to-end — **FAILURE**
- failed Chromium job: `105318015702`
- failing historical test: `web/scada-web/tests-e2e/c04-tag-source-browser.spec.ts`
- observed assertion: `previewCandidate` unexpectedly `null`

The failing test is outside the Runtime seat-accounting surface changed by PR #331. No product correction is authorized from that fact alone.

### ORDER CODEX-331-CI-01

CODEX must execute **one controlled rerun only** of the failed Chromium job `105318015702`, preserving the exact candidate SHA `09f81e97369089def481ceb25629779a5aba8aff`.

Binding constraints:

1. do **not** change product code before this rerun;
2. do **not** change `c04-tag-source-browser.spec.ts`;
3. do **not** change workflow YAML, filters or CI configuration;
4. do **not** create an artificial commit to obtain another run;
5. do **not** rebase or retarget PR #331;
6. do **not** merge PR #331;
7. do **not** begin another FND-03 slice;
8. do **not** begin FND-04;
9. do **not** release FC0-A.

### If the controlled rerun succeeds

CODEX must publish the complete final handoff beginning exactly:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING HANDOFF`

The handoff must include exact base/head/tree, PR, changed surfaces, capacity contract, PostgreSQL/in-memory atomicity mechanism, Demo/ESLIC2 behavior, reason codes, concurrency coverage, acceptance matrix `PASS | FAIL | PENDING`, local evidence, exact Actions run/job IDs, skips, residual risks and explicit confirmation that no second quota/lease/licensing/Authority authority was created.

Then **STOP and wait for Main Coordinator integration decision**.

### If the controlled rerun fails again

CODEX must **not rerun again and must not repair C04 autonomously**.

Return to Main with:

- new attempt/job ID;
- exact failing test/assertion;
- comparison with the first failure;
- any deterministic evidence available from logs/artifacts;
- confirmation that candidate SHA remained unchanged.

Then **STOP**.

---

## 3. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 DEV

Do not implement FND-04 yet.

On every `SIGA`:

1. reread this file live;
2. revalidate `wave15/corrections-integration`;
3. if `ORDER_STATE` remains `WAIT`, perform no product mutation and wait;
4. only begin when Main Coordinator changes this section to `ACTIVE` and supplies exact base SHA/tree, branch, scope and acceptance package.

Reserved implementation branch when activated:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

FND-04 DEV will be the **single owner of production implementation** for the Script TAG Reference Resolution contract. It must not merge or self-freeze.

---

## 4. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 AUD  
**Default mode:** `READ_ONLY_REVIEW`

Do not audit a speculative candidate and do not write tests yet.

On every `SIGA`:

1. reread this file live;
2. if `ORDER_STATE` remains `WAIT`, perform no mutation and wait;
3. when activated, Main Coordinator will provide exact DEV candidate SHA/tree and explicit `AUD_MODE`;
4. only if `AUD_MODE: WRITE_TESTS` is explicitly present may AUD create tests, and then only in the isolated branch named by Main;
5. AUD never modifies DEV production code, integration or `main`;
6. AUD never merges or declares `VERIFIED/FROZEN`.

AUD must remain independent/adversarial to DEV and report evidence to Main.

---

## 5. FND-04 BINDING CONTRACT — READY BUT NOT ACTIVE

Objective once activated:

- Python-visible TAG references use canonical human-readable references, normally full paths;
- `TagId`/Guid remains authoritative stable identity;
- one shared resolver serves `tag_read` and `tag_write`;
- persisted/versioned binding proves visible reference <-> expected stable TagId;
- rename/move/path reuse never silently retargets a script;
- missing/ambiguous/stale/identity-drift states fail closed;
- legacy GUID/TagId behavior is explicit and tested;
- Authority remains canonical and no second registry/resolver/authorization pipeline is created;
- contract must be consumable by Script Engineering downstream without Foundation redesign.

Expected core acceptance when activated includes readable source, shared read/write resolution, stable identity validation, rename/move/path-reuse negatives, persistence/package round-trip, legacy behavior, Authority preservation and representative multi-TAG scripts.

Main Coordinator will issue an exact work package after the FND-03 checkpoint allows activation.

---

## 6. FND-03 REMAINING AFTER SHARED SEAT ACCOUNTING

Even if PR #331 becomes integrated/verified, FND-03 global is not automatically frozen.

Remaining bounded work currently expected before global FND-03 `VERIFIED/FROZEN`:

- license inspect/verify/install/replace/remove lifecycle;
- entitlement reevaluation/fencing when authoritative machine license changes while Runtime is active;
- integration with Installation switching #304;
- final observability/rejection reasons where still missing;
- remaining concurrency/negative regressions required by #301.

Product Owner decision already binding: the product has not been released; no installed customer base requires commercial backward-compatibility behavior for legacy ESLIC1 session quotas. ESLIC2 is the current commercial session-entitlement contract; ESLIC1 must not receive inferred/unlimited remote-session capacity.

---

## 7. PERMANENT GUARDS

- GitHub live is authority.
- `main` is never mutated without explicit Product Owner final authorization.
- No direct feature-code write to `wave15/corrections-integration`; normal product integration is by reviewed PR.
- Coordinator-only documentation in this handoff may be updated by Main to communicate live orders.
- Red CI must be diagnosed before rerun; no repeated blind reruns.
- Exact-head evidence only.
- Required but unexecuted test = `PENDING`.
- No force push, destructive rebase or evidence deletion.
- Runtime Session Class/licensing is only a restrictive ceiling; Authority remains the capability authority.
- `CommandExecute` remains distinct from `ProcessValueWrite`.
- Web Runtime and EliteGO share the same logical lease/quota authority; no separate pools.
- No session state, license private material, credentials or topology state in `.escadapkg`.
- Stable IDs outrank mutable names/paths.
- No downstream silent redesign of frozen contracts.

---

## 8. COMMUNICATION / LEDGER RULE

### Primary live orders

**This file: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.**

CODEX / DEV / AUD must read this file whenever Product Owner says `SIGA`.

### Evidence / historical ledgers

- #301 — FND-03 / licensing ledger
- #305 — dependency/checkpoint ledger
- PR conversations — candidate-local evidence
- #297 — Wave 15 global ledger

Comments can mirror an order for traceability, but agents must use this file as the canonical active-order source.

### Return discipline

Agents do not choose the next mission. They execute the active order, return evidence, and stop when the order says `STOP` or `WAIT`.

Main Coordinator alone promotes mission state, authorizes integration/freeze and writes the next order.

---

## 9. MAIN COORDINATOR REVIEW SEQUENCE

After an agent handoff:

1. revalidate exact base/head/tree/PR live;
2. inspect real diff and scope leakage;
3. inspect relevant tests and negative/concurrency coverage;
4. inspect exact-head CI;
5. diagnose red gates before any rerun;
6. integrate only with bounded evidence sufficient;
7. capture exact merge SHA/parents/tree;
8. validate post-merge CI on the exact integrated SHA;
9. only then promote state/freeze that bounded slice;
10. update this file with the next binding order before asking an agent to continue.

---

For Product Owner control, Main Coordinator reports current America/Sao_Paulo time as:

`Hora: HH:MM`
