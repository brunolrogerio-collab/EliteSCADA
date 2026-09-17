# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 ACTIVE / SHARED RUNTIME SEAT ACCOUNTING PR #331 APPROVED FOR INTEGRATION / EXACT-HEAD CI GREEN / FND-04 WAIT / FC0-A BLOCKED**

> GitHub live é a memória oficial. Revalidar refs, SHA/tree, PRs, issues e Actions antes de decisão material.

> Handoff operacional canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> Prompt genérico de sucessão: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.
>
> Ponte curta: `docs/CURRENT-COORDINATOR-HANDOFF.md`.

## Latest verified product checkpoint

`wave15/corrections-integration@6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`.

Esse checkpoint contém PR #330 / Runtime Admission e foi validado pela CI pós-merge #1543. Commits posteriores até a autorização atual do #331 são somente documentação de coordenação; não são novo product checkpoint.

## Foundation state

- FND-01 — VERIFIED/FROZEN
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN
- FND-08 — VERIFIED/FROZEN
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN
- FND-03 Runtime Admission — VERIFIED/FROZEN
- FND-03 Shared Runtime Seat Accounting — **PR_READY / APPROVED FOR INTEGRATION**
- FND-03 global — ACTIVE / NOT FROZEN
- FND-04 Script TAG Reference Resolution — QUEUED / CONTRACT DEFINED / WAIT
- FC0-A — BLOCKED

## Approved candidate — PR #331

- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact head: `6789a989c85e7210c167945665f4ab6c8cef53a0`
- tree: `bd6d79490d7fc0630737fb834fa6b8fc95b7b8d9`
- target: `wave15/corrections-integration`
- PR live at Main review: OPEN / mergeable=true / mergeable_state=clean / no review threads

Acceptance-close delta from previous reviewed head `09f81e97369089def481ceb25629779a5aba8aff`:

- exactly one file: `tests/Scada.Drivers.Tests/DistributedRuntimeFoundationTests.cs`
- +59 lines
- zero production-file changes
- explicit Web+EliteGO shared-pool acceptance PASS
- high-concurrency mixed distinct-identity oversubscription acceptance PASS

Exact-head CI #1545 / run `35267768938`:

- Backend `105359117658` — SUCCESS
- Web `105359118013` — SUCCESS
- Chromium `105359631809` — SUCCESS

## Current Codex order

Canonical order: `ORDER CODEX-331-MERGE-04` in `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

CODEX is authorized to merge **only PR #331** into `wave15/corrections-integration` through the normal PR route after exact-head revalidation. No rebase/retarget/candidate changes, no other PR and no `main` mutation.

After merge CODEX reports exact merge SHA, parents, tree and new integration HEAD, then STOP.

Main Coordinator owns exact post-merge CI validation and state promotion. CI green pre-merge does not itself promote the slice.

## Main permanent CI authority

Product Owner permanently authorized Main to inspect/trigger/rerun pre-merge and post-merge CI under strict guards: exact SHA/ref, diagnosis before rerun, smallest sufficient rerun, no blind loop, no artificial commits, no weakening code/tests/workflows.

CI authority and merge authority remain separate. `main` remains protected.

## FND-03 remaining after Shared Seat Accounting

Mesmo após integração/verificação do #331, FND-03 global não congela automaticamente. Ainda são esperados bounded slices para:

- license inspect/verify/install/replace/remove lifecycle;
- entitlement reevaluation/fencing com Runtime ativo;
- integração com Installation switching #304;
- observability/rejection reasons finais restantes;
- regressões negativas/concurrency restantes de #301.

Product Owner binding: produto não lançado; sem base instalada exigindo compatibilidade comercial de quotas ESLIC1. ESLIC2 é o contrato comercial de session entitlement; ESLIC1 não recebe quota remota inferida/ilimitada.

## Resume sequence

1. Ler o handoff canônico integralmente.
2. Revalidar PR #331 e integration HEAD live.
3. Se o merge handoff existir, capturar exact integrated SHA/parents/tree.
4. Validar CI pós-merge no exact integrated SHA; Main pode operar essa CI diretamente.
5. Só depois promover Shared Seat Accounting para VERIFIED/FROZEN.
6. Reavaliar o restante de FND-03 antes de liberar novo slice.
7. FND-04 permanece WAIT até ordem ACTIVE com exact product base.
8. FC0-A permanece bloqueado.

Permanent guards: no direct feature write to integration, no direct `main` mutation without protected authorization, no destructive history operation, diagnose CI before rerun, no claim of PASS/VERIFIED/FROZEN without exact evidence.