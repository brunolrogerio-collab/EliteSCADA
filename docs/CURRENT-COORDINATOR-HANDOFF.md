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
- FND-03 — ACTIVE / NOT FROZEN.
- PR #325 machine-license-v2 — INTEGRATED, mas ainda não VERIFIED/FROZEN após revisão semântica do Main.
- integration candidate atual antes do hardening: `ac2b7f49f53734132d88c7d367b88d33383c384a`.
- FC0-A — BLOCKED.
- FND-04 — QUEUED / NOT ACTIVE.

## ORDEM ATIVA DO MAIN AO CODEX

**FND-03 — LICENSE V2 HARDENING**

Binding completo: #301 comentário `5703758565`.  
Sequencing: #305 comentário `5703760732`.

Exact base:

`ac2b7f49f53734132d88c7d367b88d33383c384a`

Branch autorizada:

`work/w15-fnd-03-machine-license-v2-hardening`

Target:

`wave15/corrections-integration`

### Importante

A autorização anterior para iniciar:

`FND-03 — COMMON RUNTIME ADMISSION / REQUESTED→GRANTED SESSION CLASS / AUTHORITY ENFORCEMENT`

está **SUPERADA E BLOQUEADA** até o hardening ser integrado e verificado pelo Main.

Se o Codex já tiver começado Runtime Admission apenas localmente, deve **parar e preservar o worktree sem push/PR**, então executar o hardening a partir do exact base acima.

Na última verificação do Main não existiam branch remota nem PR para `work/w15-fnd-03-runtime-admission-v1`.

## Retomada obrigatória

1. Ler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` por completo.
2. Revalidar live HEAD/tree da integração.
3. Ler os comentários mais recentes de #301 e #305, especialmente `5703758565` e `5703760732`.
4. Não seguir a antiga ordem de Runtime Admission.
5. Criar/usar `work/w15-fnd-03-machine-license-v2-hardening` partindo exatamente de `ac2b7f49...`.
6. Executar somente o hardening bounded descrito no handoff vivo.

## Semântica congelada para o hardening

No ESLIC2, `viewOnlySeats` e `interactiveSeats` são **totais efetivos de capacidade comercial remota/cliente**, não adicionais ao Demo 2+2. `0` e `false` explícitos são válidos e devem ser distinguíveis de campo ausente.

## Retorno esperado

Codex -> Main Coordinator deve começar por:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 HARDENING HANDOFF`

O formato completo, testes obrigatórios e fronteiras estão em `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Fontes de coordenação

- handoff operacional vivo/canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`;
- combinador corrente: `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- FND-03/licensing: #301;
- Foundation/dependency graph: #305;
- Wave 15 global: #297.

Hora de respostas ao Product Owner: `Hora: HH:MM` em America/Sao_Paulo.
