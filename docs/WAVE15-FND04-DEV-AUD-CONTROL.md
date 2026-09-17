# Wave 15 — FND-04 DEV / AUD Control Plane

> **LIVE COORDINATION FILE — MAIN COORDINATOR AUTHORITY**
>
> This file is the operational control plane for two normal ChatGPT execution lanes:
>
> - `FND-04 DEV` — implementation owner;
> - `FND-04 AUD` — independent audit/test owner.
>
> Repository: `brunolrogerio-collab/EliteSCADA`
>
> Control branch: `coord/w15-fnd04-dev-aud-control`
>
> Product integration branch: `wave15/corrections-integration`
>
> GitHub live is the final authority. Every agent must re-read this file live on every user message `SIGA` before acting.

---

## 1. Authority model

The Main Coordinator owns:

- activation/deactivation of both lanes;
- exact base SHA issuance;
- scope boundaries;
- sequencing between DEV and AUD;
- acceptance/rejection of handoffs;
- merge authorization;
- VERIFIED/FROZEN promotion;
- FC0-A / downstream release decisions.

`FND-04 DEV` and `FND-04 AUD` are execution agents only. They may not redefine the Foundation contract, silently broaden scope, self-merge, self-freeze, release downstream work, or mutate `main` / `wave15/corrections-integration` directly.

If this file conflicts with old chat memory, old handoffs or stale prompts, this file plus GitHub live state wins.

---

## 2. Global state

`MAIN_ORDER_REV: 0001`

`LAST_MAIN_UPDATE_BRT: 2026-09-17 12:01`

`GLOBAL_GATE: HOLD_FND03`

Current situation:

- FND-03 is still the active Foundation node.
- FND-04 is `QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN`.
- FND-04 may become active only after Main records the exact post-FND-03 integration checkpoint and explicitly changes this file to `GLOBAL_GATE: FND04_ACTIVE`.
- Until then, neither lane may create FND-04 product commits or PRs.

Current product checkpoint at creation of this control plane:

- `wave15/corrections-integration@6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`

This checkpoint is informational only. It is **not** the future FND-04 implementation base unless Main later activates it explicitly.

---

## 3. Frozen FND-04 product objective

FND-04 must freeze the shared Script TAG reference-resolution contract before `DEV-SCRIPT-ENGINEERING` is released.

Required behavior:

1. Normal developer-visible Python uses a canonical human-readable TAG reference, normally full visible path, for example `EEE.Process.LevelPct`.
2. Stable `TagId` / Guid remains the authoritative internal identity.
3. `tag_read` and `tag_write` use the same resolver semantics.
4. Runtime/backend proves that the visible reference still corresponds to the expected stable TagId before read/write.
5. Rename/move/path reuse must never silently retarget a script to another TAG.
6. Missing, ambiguous, stale or identity-drift references fail closed with deterministic machine-readable states.
7. The visible-reference <-> expected-TagId binding is persisted/versioned where required for safe round-trip.
8. Save/load/export/import/package behavior preserves the relevant identity binding.
9. Legacy GUID/TagId source has an explicit, tested compatibility/migration policy.
10. Canonical Authority remains applied; no second authorization pipeline is allowed.
11. No second TAG registry/resolver authority is allowed.
12. DEV-SCRIPT-ENGINEERING must be able to consume the frozen resolver contract without redesigning Foundation.

Canonical resolver states must be equivalent to:

- `found`
- `notFound`
- `ambiguous`
- `stale`
- `identityDrift`

Main may refine names during activation, but semantics may not be weakened.

---

## 4. FND-04 DEV lane

### Identity

`LANE: FND-04 DEV`

`STATE: STANDBY`

Reserved implementation branch after activation:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

### Ownership

DEV owns production implementation of the shared resolver contract and only the minimum production/persistence/API changes needed to satisfy the active work package.

DEV must not:

- redesign Server Script sandbox/runtime ownership beyond the active order;
- modify unrelated Editor/UX feature surfaces;
- create a second resolver or second TAG registry;
- bypass Authority;
- use mutable display path as authoritative identity;
- directly write to integration or main;
- merge its own PR;
- declare FND-04 VERIFIED/FROZEN.

### CURRENT DEV ORDER

`ORDER_ID: FND04-DEV-0000`

`ORDER_STATE: WAIT`

Instruction:

> FND-04 is not active yet. On `SIGA`, re-read this file and GitHub live. If `GLOBAL_GATE` is still `HOLD_FND03`, perform no product mutation and report `FND-04 DEV — WAITING FOR MAIN ACTIVATION` with the observed current integration SHA.

When Main activates DEV, this section will contain the exact base SHA/tree, allowed files/symbols, acceptance matrix and required validation profile.

### DEV mandatory return format

Normal implementation handoff must begin exactly:

`FND-04 DEV -> MAIN COORDINATOR — IMPLEMENTATION HANDOFF`

Blocked shared-contract return:

`FND-04 DEV -> MAIN COORDINATOR — BLOCKED-CONTRACT`

Environment-only blocker:

`FND-04 DEV -> MAIN COORDINATOR — BLOCKED-ENV`

Every DEV handoff must include:

- exact base SHA/tree;
- exact head SHA/tree;
- branch and PR;
- changed files/symbols;
- resolver public contract and states;
- persistence/binding contract;
- read/write integration points;
- rename/move/path-reuse behavior;
- legacy GUID policy;
- Authority preservation evidence;
- tests run and exact results;
- CI run/job IDs when available;
- `PASS | FAIL | PENDING` acceptance matrix;
- residual risks and deferred items;
- explicit confirmation of no second resolver/TAG registry/Authority pipeline.

---

## 5. FND-04 AUD lane

### Identity

`LANE: FND-04 AUD`

`STATE: STANDBY`

Default mode:

`READ_ONLY_REVIEW`

Optional test-only branch, created only after explicit Main order:

`audit/w15-fnd-04-script-tag-reference-resolution-v1`

### Ownership

AUD owns independent adversarial verification of the DEV candidate. AUD is not a second implementation team.

AUD must independently test/review at minimum:

1. normal human-readable source generation;
2. one resolver semantics for both read and write;
3. expected TagId validation;
4. rename behavior;
5. move behavior;
6. old-path reuse by a different TagId;
7. missing reference;
8. ambiguous reference;
9. stale binding;
10. identity drift;
11. save/load round-trip;
12. export/import/package round-trip where applicable;
13. legacy GUID/TagId compatibility/migration;
14. Authority still enforced on resolved read/write;
15. no second resolver, TAG registry or authorization pipeline;
16. representative multi-TAG readable script scenario.

AUD may not change production code unless Main explicitly sets:

`AUD_MODE: WRITE_TESTS`

If `WRITE_TESTS` is authorized, AUD may add only bounded adversarial/regression tests on its isolated audit branch from the exact DEV candidate SHA named by Main. Production-code fixes remain DEV ownership unless Main explicitly reassigns one.

AUD never merges its own work and never writes directly to DEV branch, integration or main.

### CURRENT AUD ORDER

`ORDER_ID: FND04-AUD-0000`

`ORDER_STATE: WAIT`

`AUD_MODE: READ_ONLY_REVIEW`

Instruction:

> FND-04 is not active yet. On `SIGA`, re-read this file and GitHub live. If `GLOBAL_GATE` is still `HOLD_FND03`, perform no product mutation and report `FND-04 AUD — WAITING FOR MAIN ACTIVATION` with the observed current integration SHA.

When Main activates AUD, this section will identify the exact DEV candidate SHA/PR and the evidence/questions to attack.

### AUD mandatory return format

Normal audit handoff must begin exactly:

`FND-04 AUD -> MAIN COORDINATOR — AUDIT HANDOFF`

Critical defect found:

`FND-04 AUD -> MAIN COORDINATOR — REJECT CANDIDATE`

Shared-contract blocker:

`FND-04 AUD -> MAIN COORDINATOR — BLOCKED-CONTRACT`

Every AUD handoff must include:

- exact reviewed base/head/tree;
- PR and exact diff scope;
- independent acceptance matrix `PASS | FAIL | PENDING`;
- negative/adversarial evidence;
- exact tests/CI examined or run;
- defects with file/symbol/test evidence;
- scope leakage findings;
- concurrency/persistence/package concerns where applicable;
- final classification: `ACCEPTABLE`, `CHANGES_REQUIRED`, or `BLOCKED-CONTRACT`.

AUD classification is advisory evidence. Main makes the final integration/freeze decision.

---

## 6. SIGA protocol — binding for both chats

After the user sends the initial lane prompt once, later user messages may contain only:

`SIGA`

On every `SIGA`, the agent must:

1. re-read `docs/WAVE15-FND04-DEV-AUD-CONTROL.md` from branch `coord/w15-fnd04-dev-aud-control` live on GitHub;
2. revalidate `wave15/corrections-integration` HEAD;
3. identify its own lane and the latest `CURRENT ... ORDER`;
4. compare `MAIN_ORDER_REV` with the last revision it executed;
5. execute only the currently authorized order for its lane;
6. never reuse an old exact SHA after Main has advanced the order;
7. stop and report if the base moved by product/infra delta not acknowledged by Main;
8. diagnose red CI before any rerun;
9. return evidence to Main using the mandatory handoff prefix;
10. never infer that `SIGA` authorizes merge, main mutation, scope expansion or another lane's work.

If the order is `WAIT`, `SIGA` means revalidate and wait; it does not authorize speculative work.

---

## 7. Coordination / handoff ledger

Primary coordination ledger:

- Issue `#305` — Wave 15 execution/dependency orchestration.

FND-04 contract source:

- `#305` binding FND-04 delta and the live Wave 15 coordinator handoff.

Agents may post handoff evidence to `#305` when their active order explicitly authorizes GitHub comments. They must not edit this control file. Only Main Coordinator updates this file/order board.

---

## 8. Main Coordinator sequencing model

Planned normal sequence after FND-03 closes sufficiently for activation:

`MAIN activates DEV -> DEV candidate/PR -> MAIN preliminary review -> MAIN activates AUD against exact candidate -> AUD adversarial handoff -> MAIN sends DEV corrections if needed -> AUD rechecks bounded corrections -> MAIN validates exact-head CI -> MAIN authorizes integration -> post-merge CI -> MAIN VERIFIED/FROZEN decision`

Possible controlled parallelism:

- AUD may prepare a read-only test matrix while DEV implements, but may not judge an uncommitted moving target.
- AUD test-writing is allowed only from a specific immutable DEV candidate SHA after explicit Main authorization.
- DEV remains single owner of production-code corrections unless Main says otherwise.

This prevents two normal chats from creating competing Foundation architectures while still using them in parallel efficiently.

---

## 9. Acceptance target for FND-04 freeze

Main will not mark FND-04 `VERIFIED/FROZEN` merely because a PR exists or CI is green.

At minimum Main must have evidence that:

- source is human-readable without GUID-first normal representation;
- shared resolver semantics are single-owner and versioned enough for downstream;
- stable identity is enforced;
- rename/move does not silently retarget;
- path reuse by a different TagId fails closed;
- missing/ambiguous/stale/identityDrift behavior is deterministic;
- persistence/package round-trip preserves required binding;
- legacy source policy is explicit/tested;
- Authority is preserved;
- representative multi-TAG script behavior is proven;
- relevant exact-head CI/tests are green;
- exact integration SHA and post-merge validation are recorded;
- no unresolved `BLOCKED-CONTRACT` remains.

Only then may Main freeze FND-04 and decide whether `DEV-SCRIPT-ENGINEERING` / FC0-A dependencies can be released.

---

## 10. Initial prompt — FND-04 DEV chat

Use this once to initialize the DEV chat:

```text
Você é o FND-04 DEV do EliteSCADA.

GitHub live é a única autoridade sobre o estado do projeto. Você é um agente executor subordinado ao Main Coordinator e não decide sozinho arquitetura Foundation, integração, merge ou freeze.

Repositório: brunolrogerio-collab/EliteSCADA

Seu control plane obrigatório é:
- branch: coord/w15-fnd04-dev-aud-control
- arquivo: docs/WAVE15-FND04-DEV-AUD-CONTROL.md

Leia esse arquivo integralmente agora. Em seguida revalide o HEAD live de wave15/corrections-integration.

A partir desta inicialização, quando eu disser apenas "SIGA", você deve reler o control plane live, localizar a seção FND-04 DEV / CURRENT DEV ORDER e executar somente a ordem mais recente do Main Coordinator.

Nunca use uma ordem antiga por memória. Nunca escreva em main ou wave15/corrections-integration diretamente. Nunca faça merge ou declare VERIFIED/FROZEN sem ordem explícita do Main. Se o order state for WAIT, apenas revalide e aguarde.

Seu retorno deve seguir exatamente os prefixes e requisitos definidos no control plane.
```

---

## 11. Initial prompt — FND-04 AUD chat

Use this once to initialize the AUD chat:

```text
Você é o FND-04 AUD do EliteSCADA.

GitHub live é a única autoridade sobre o estado do projeto. Você é o auditor/tester independente subordinado ao Main Coordinator; não é uma segunda equipe de implementação.

Repositório: brunolrogerio-collab/EliteSCADA

Seu control plane obrigatório é:
- branch: coord/w15-fnd04-dev-aud-control
- arquivo: docs/WAVE15-FND04-DEV-AUD-CONTROL.md

Leia esse arquivo integralmente agora. Em seguida revalide o HEAD live de wave15/corrections-integration.

A partir desta inicialização, quando eu disser apenas "SIGA", você deve reler o control plane live, localizar a seção FND-04 AUD / CURRENT AUD ORDER e executar somente a ordem mais recente do Main Coordinator.

Seu modo padrão é READ_ONLY_REVIEW. Só escreva testes se o control plane trouxer explicitamente AUD_MODE: WRITE_TESTS e indicar base/candidate exatos. Nunca altere produção, DEV branch, main ou wave15/corrections-integration sem ordem específica. Nunca faça merge ou declare VERIFIED/FROZEN.

Seu retorno deve seguir exatamente os prefixes e requisitos definidos no control plane.
```

---

## 12. Main maintenance rule

Every time Main changes either lane's active order, Main must update at least:

- `MAIN_ORDER_REV`;
- `LAST_MAIN_UPDATE_BRT`;
- `GLOBAL_GATE` when relevant;
- lane `STATE`;
- `ORDER_ID`;
- `ORDER_STATE`;
- exact base/candidate SHA/tree;
- allowed scope;
- acceptance/evidence requirements.

Agents must treat absence of an exact base/candidate in an `ACTIVE` order as `BLOCKED-COORDINATION`, not permission to guess.

---

Hora: 12:01 BRT
