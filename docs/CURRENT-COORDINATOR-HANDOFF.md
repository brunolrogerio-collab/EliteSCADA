# Current Coordinator Handoff — Wave 15

> **COMBINADOR/PONTE CURTA da coordenação corrente.**
>
> O handoff operacional vivo e canônico da interação MAIN COORDINATOR <-> CODEX/FOUNDATION WORK é:
>
> `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
>
> Este arquivo existe para apontar rapidamente para a ordem ativa, as issues e o ponto de retomada. Não deve duplicar todo o handoff detalhado nem substituir o documento Wave 15.
>
> **GitHub live continua sendo a autoridade final.**

## Estado rápido

- Wave 15 — ACTIVE.
- FND-02 — VERIFIED/FROZEN.
- FND-03 — ACTIVE / NOT FROZEN.
- FND-03 Slice 1 — INTEGRATED / VERIFIED no checkpoint de produto `456c66f4966ab5302831f642a39690ae3a3402a5`.
- FC0-A — BLOCKED.
- ordem ativa do Main ao Codex — **FND-03 machine-license v2 schema/codec**.

## Retomada obrigatória

1. Ler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` por completo.
2. Revalidar o live HEAD/tree de `wave15/corrections-integration`.
3. Ler os comentários mais recentes de #301 e #305.
4. Retomar a sessão/worktree local do Codex que já iniciou o machine-license-v2 slice antes do limite de interação.
5. Não iniciar uma implementação paralela apenas porque a branch remota do slice ainda não foi publicada.

## Ordem ativa

Main Coordinator -> Codex:

`FND-03 — machine-license v2 schema/codec`

Binding order: #301 comentário `5699620231`.

Base de produto autorizada:

`456c66f4966ab5302831f642a39690ae3a3402a5`

Branch prevista:

`work/w15-fnd-03-machine-license-v2`

O escopo completo, provas mínimas e itens fora de escopo estão no handoff Wave 15 canônico.

## Retorno esperado

Codex -> Main Coordinator deve começar por:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`

O formato completo do retorno e a sequência de revisão do Main estão em `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Fontes de coordenação

- handoff operacional vivo/canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`;
- combinador corrente: `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- FND-03/licensing: #301;
- Foundation/dependency graph: #305;
- Wave 15 global: #297;
- sequencing/checkpoints: `docs/ROADMAP.md`;
- troca do chat Main Coordinator: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.

Não classificar `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` como histórico enquanto Wave 15 estiver ativa.

Hora de respostas ao Product Owner: `Hora: HH:MM` em America/Sao_Paulo.
