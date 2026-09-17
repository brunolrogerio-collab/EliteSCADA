# Current Coordinator Handoff — Wave 15

> **PONTE CURTA da coordenação corrente.**
>
> Handoff operacional vivo/canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> GitHub live é a autoridade final. Esta ponte não substitui a ordem canônica.

## Estado rápido

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN.
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN.
- FND-03 Runtime Admission — VERIFIED/FROZEN.
- FND-03 Shared Runtime Seat Accounting — **PR_READY / APPROVED FOR INTEGRATION**.
- FND-03 global — ACTIVE / NOT FROZEN.
- FND-04 — QUEUED / CONTRACT DEFINED / WAIT.
- FC0-A — BLOCKED.

## Product checkpoint

Último product checkpoint integrado/validado:

`6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`.

A integração avançou depois disso apenas por documentação de coordenação. O product checkpoint não muda até o PR #331 ser integrado e validado.

## PR #331 aprovado

- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact approved head: `6789a989c85e7210c167945665f4ab6c8cef53a0`
- tree: `bd6d79490d7fc0630737fb834fa6b8fc95b7b8d9`
- target: `wave15/corrections-integration`
- live review: OPEN / mergeable=true / mergeable_state=clean / no review threads
- acceptance-close desde `09f81e97...`: somente `tests/Scada.Drivers.Tests/DistributedRuntimeFoundationTests.cs`, +59, zero produção
- CI #1545 / run `35267768938`:
  - Backend `105359117658` SUCCESS
  - Web `105359118013` SUCCESS
  - Chromium `105359631809` SUCCESS

O antigo PENDING Web+EliteGO + high concurrency está PASS.

## Ordem corrente ao Codex

Ler a `CURRENT ORDER` no handoff canônico.

Estado atual: `ORDER CODEX-331-MERGE-04`.

CODEX deve revalidar o exact head e fazer merge **somente do PR #331** pela rota normal em `wave15/corrections-integration`, sem rebase/retarget/alteração de candidate e sem tocar `main`.

Depois deve retornar merge SHA, parents, tree e novo integration HEAD, e STOP.

Main Coordinator fará a validação de CI pós-merge e somente então promoverá o slice.

## Autoridade permanente do Main para CI

Main pode operar CI pré-merge/pós-merge sem nova autorização a cada execução, sob as guardas do handoff canônico: exact SHA/ref, diagnóstico antes de rerun, menor rerun suficiente, sem loop cego, sem workflow/test/code weakening e sem commit artificial.

CI verde não equivale a autorização de merge.

## Retomada obrigatória

1. Ler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` integralmente.
2. Revalidar PR #331, integration HEAD e Actions live.
3. Se o merge handoff já existir, Main captura o exact integrated SHA e valida CI pós-merge.
4. Shared Seat Accounting só vira VERIFIED/FROZEN após evidência pós-merge.
5. FND-04 permanece WAIT até ordem ACTIVE com exact product base.
6. FC0-A permanece bloqueado.

Fontes: handoff canônico, #301, #305, #297, PR #331.

`Hora: HH:MM` em America/Sao_Paulo.