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
- FND-03 Shared Runtime Seat Accounting — **VERIFIED/FROZEN**.
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing v1 — **ACTIVE**.
- FND-03 global — ACTIVE / NOT FROZEN.
- FND-04 Script TAG Reference Resolution — QUEUED / CONTRACT DEFINED / WAIT.
- FC0-A — BLOCKED.

## Verified product checkpoint

`wave15/corrections-integration@a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

Esse checkpoint é o merge do PR #331 e foi validado pela CI pós-merge #1546 / run `35269829080`:

- Backend `105366045111` — SUCCESS
- Web `105366045298` — SUCCESS
- Chromium `105366583742` — SUCCESS

Commits posteriores somente documentais não mudam o product checkpoint.

## Ordem corrente ao Codex

Ler a `CURRENT ORDER` no handoff canônico.

Missão ativa:

`FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing v1`

Exact product base:

`a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

Work branch:

`work/w15-fnd-03-license-lifecycle-fencing-v1`

Target:

`wave15/corrections-integration`

Codex não deve repetir auditoria aberta: o Main já fechou o source audit e o work package no handoff canônico.

A missão inclui candidate verify sem mutação, replace/install transacional, remove->Demo, fence de todas as leases remotas após mudança válida de licença, reavaliação/fencing do Runtime ativo, EngineeringModify nas mutações, audit seguro e testes de concorrência/negativos.

A branch deve usar como product merge-base o exact checkpoint `a7067ac9...`; coordination HEADs documentais não devem ser tratados como nova base de produto. Se houver qualquer delta interveniente de produto/infra, Codex deve STOP com `BLOCKED-BASE-DIVERGENCE`.

## FND-04

DEV e AUD permanecem `WAIT`. Main ainda não autorizou implementação FND-04. Em `SIGA`, ambos relêem o handoff canônico e não fazem mutação enquanto WAIT.

## Autoridade permanente do Main

Main pode atualizar handoffs/ordens, espelhar ordens aos agentes e operar CI pré/pós-merge sob guardas. CI verde não equivale a merge. `main` continua protegido.

## Retomada obrigatória

1. Ler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` integralmente.
2. Revalidar product checkpoint `a7067ac9...`, integration HEAD live, PRs e Actions.
3. Acompanhar a entrega do lifecycle/fencing no exact candidate.
4. FND-03 só congela globalmente após Main fechar todos os critérios #301 no exact integration checkpoint.
5. FND-04 permanece WAIT até ordem ACTIVE.
6. FC0-A permanece bloqueado.

Fontes: handoff canônico, #301, #304, #305, #297.

`Hora: HH:MM` em America/Sao_Paulo.
