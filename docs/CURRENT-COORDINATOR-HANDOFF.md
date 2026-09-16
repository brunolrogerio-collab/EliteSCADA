# Current Coordinator Handoff — Wave 15

> **COMBINADOR/PONTE CURTA da coordenação corrente.**
>
> O handoff operacional vivo e canônico da interação MAIN COORDINATOR <-> CODEX/FOUNDATION WORK é:
>
> `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
>
> Este arquivo apenas aponta para a ordem ativa e o ponto de retomada. Não substitui o handoff Wave 15.
>
> **GitHub live continua sendo a autoridade final.**

## Estado rápido

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 — ACTIVE / NOT FROZEN.
- Runtime Session Lease v1 contract — VERIFIED/FROZEN.
- machine-license v2 schema/codec + hardening contract — VERIFIED/FROZEN.
- exact integration checkpoint validado: `897ae7ca243f0f75d0d8ddf81e4b53a80b37f4f5`.
- EliteSCADA CI automática #1537 / run `35160493083` nesse SHA: Backend PASS, Web PASS, Chromium E2E PASS.
- FND-04 — QUEUED / CONTRACT DEFINED / NOT ACTIVE.
- FC0-A — BLOCKED.

## ORDEM ATIVA DO MAIN AO CODEX

**FND-03 — COMMON RUNTIME ADMISSION / REQUESTED→GRANTED SESSION CLASS / AUTHORITY ENFORCEMENT**

Exact authorized product base:

`897ae7ca243f0f75d0d8ddf81e4b53a80b37f4f5`

Branch autorizada:

`work/w15-fnd-03-runtime-admission-v1`

Target:

`wave15/corrections-integration`

## Fronteiras principais

- `requestedClass` vem do cliente; decisão efetiva é server-side.
- `Interactive` é teto de classe, nunca concede capability ausente.
- `ViewOnly` explícito permanece ViewOnly.
- subject intrinsecamente read-only pela Authority deve ser downscoped para ViewOnly.
- `CommandExecute` continua separado de `ProcessValueWrite`.
- ViewOnly deve falhar fechado no backend para mutações.
- REST, WebSocket e reconnect usam a mesma Runtime Session Lease lógica.
- Não criar segundo lease registry, segundo licensing path ou segundo Authority pipeline.
- Shared concurrent seat accounting Web + EliteGO é o próximo slice e **não** deve ser implementado silenciosamente agora.
- FND-04 permanece bloqueado/queued.

## Retomada obrigatória

1. Ler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` por completo.
2. Revalidar live HEAD/tree da integração e confirmar que qualquer avanço desde `897ae7ca...` é somente documental; se houver delta de produto/infra, retornar ao Main.
3. Ler os comentários mais recentes de #301 e #305.
4. Criar/usar `work/w15-fnd-03-runtime-admission-v1` partindo exatamente do authorized product base.
5. Executar somente o slice bounded de Runtime Admission.
6. Usar os gatilhos reais de `.github/workflows/dotnet-ci.yml`: PR/push automático é o caminho normal quando branch/path filters permitirem; `workflow_dispatch` não é requisito genérico.

## Retorno esperado

Codex -> Main Coordinator deve começar por:

`CODEX -> MAIN COORDINATOR — FND-03 RUNTIME ADMISSION HANDOFF`

Se houver blocker de contrato frozen:

`CODEX -> MAIN COORDINATOR — FND-03 RUNTIME ADMISSION BLOCKED-CONTRACT`

O formato completo, critérios, testes e fronteiras estão em `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Fontes de coordenação

- handoff operacional vivo/canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`;
- combinador corrente: `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- FND-03/licensing: #301;
- Foundation/dependency graph: #305;
- Wave 15 global: #297.

Hora de respostas ao Product Owner: `Hora: HH:MM` em America/Sao_Paulo.
