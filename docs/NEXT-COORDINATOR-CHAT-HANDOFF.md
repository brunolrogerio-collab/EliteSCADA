# Next Coordinator Chat Handoff

Copy the text below into the next coordinator chat.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Antes de qualquer decisão, diagnóstico, alteração de código/documentação, ação em PR, rerun ou merge, revalide o estado ao vivo. Havendo divergência com este handoff, GitHub live prevalece.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Issue coordenadora:

#286

Issue de auditoria pós-C26:

#289

Preview pós-C26:

PR #290

Linha diagnóstica atual:

PR #296 — **DIAGNOSTIC ONLY / MUST NEVER MERGE**

## CONTEXTO DA TROCA

Você está sendo escolhido como próximo coordenador porque esta sessão Codex tem acesso direto ao Codespace real e à aplicação EliteSCADA. Use essa vantagem para fazer diagnóstico dinâmico no ambiente vivo, especialmente onde o coordenador anterior ficou limitado pela autenticação privada do Codespace e pelo browser audit.

Isso não altera governança: não assuma causa sem evidência e revalide GitHub ao vivo antes de agir.

## LEITURA OBRIGATÓRIA

Leia ao vivo, nesta ordem:

1. `docs/WAVE14-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. issue #289 comment `5619800919`
5. issue #286 comment `5619803479`
6. PR #290, incluindo body, commits, branch/head e estado
7. PR #296, incluindo body, commits, branch/head e último workflow
8. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
9. `docs/WORK-UI-AUDIT-HANDOFF.md` no Preview branch

Depois revalide #286, #289, #290, #296, branches, SHAs e workflows ao vivo antes de qualquer decisão.

## ESTADO TÉCNICO NO MOMENTO DA TRANSFERÊNCIA

Accepted C26 product SHA:

`08e2530671de10d48933c4b712a1a1abc9e41dce`

Corrected canonical C11 SHA:

`19d5257d970f53ae798c5fa53946fce07c586452`

Technically validated/audited Preview candidate:

`59e815eae524b9ff043ea6bf3f797f4c01ba9143`

Preview docs-only HEAD no último recheck:

`f6196dfc113322ec7c11471071508314d70cd900`

Frozen package SHA-256:

`e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Second Work recheck artifact:

`ELITESCADA-RECHECK-UI-POST-C26-2026-09-10.zip`

83 arquivos

SHA-256:

`10c1e495d74a85f1f9b8571065e98220cd1baf3b163382536ef31f2176437a14`

Estado de coordenação:

`SECOND AUDIT ACCEPTED AS EVIDENCE -> P1s REPRODUCIBLE -> NEW RUNTIME/SIMULATION P1 CANDIDATE -> TECHNICAL REPRODUCTION/DIAGNOSIS REQUIRED -> NOT READY FOR FINAL PO HOMOLOGATION`

## SEGUNDO WORK RECHECK

Reproduzidos:

- `UIAUD-289-001`
- `UIAUD-289-002`
- `UIAUD-289-003`
- `UIAUD-289-007`
- `UIAUD-289-011`
- `UIAUD-289-017`

Parcialmente reproduzido:

- `UIAUD-289-004`

Dados insuficientes:

- `UIAUD-289-016`

Novo candidato P1 provisório:

`RECHECK-SIM-PUMP-LEVEL`

Durante o recheck, P01 ficou visualmente RUNNING enquanto o snapshot completo do processo permaneceu congelado por mais de 15 minutos; depois os valores degradaram para qualidade ruim/indisponível. A tentativa do auditor de acessar `/api/diagnostics/runtime` foi bloqueada por `net::ERR_BLOCKED_BY_CLIENT`.

**Não existe causa raiz confirmada.** Não conclua Server Script throttle, timer failure, cache freeze, realtime, WebSocket, UI, proxy ou Codespaces sem correlação técnica.

## PR #296

Branch:

`diagnostic/w14-post-c26-long-run-stability`

HEAD no último recheck:

`7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`

Último commit:

`ci(w14): pass ephemeral preview auth before diagnostic devcontainer attach`

Último workflow:

- run `34505442984`
- job `102966371728`
- conclusion: FAILURE
- classification: `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE`
- observed duration: `0s`
- direct/proxy LevelPct: unknown
- direct/proxy realtime: unknown

O workflow falhou antes da coleta >15 min e **não reproduziu o freeze**. Não faça blind rerun. #296 é apenas diagnóstica e **MUST NEVER MERGE**.

## SUA PRIMEIRA MISSÃO

**Diagnostique `RECHECK-SIM-PUMP-LEVEL` diretamente no Codespace real antes de corrigir produto ou gastar mais tempo no harness do #296.**

Durante um freeze, antes de restart/reopen, capture evidência read-only de:

- `runtime.serverScripts.executionCount` / `completedCount`;
- `faultedCount` / `timeoutCount` / `consecutiveFailures` / `isThrottled`;
- `lastStatus` / `lastCompletedAt` / `lastSanitizedError`;
- valor, timestamp e quality de `EEE.Process.LevelPct`, inflow, total flow, P01 running/flow/current/frequency/pressure;
- progressão de Operational Events no mesmo intervalo;
- API interna direta versus browser/Vite/porta encaminhada;
- realtime/WebSocket direto e via proxy quando possível.

Use a seguinte árvore de diagnóstico somente como discriminante, não como conclusão:

- counters/`lastCompletedAt` pararam -> investigar timer/Server Script/sandbox;
- diagnostics continuam mas `/api/tags` parou -> investigar bridge/cache/event publication;
- `/api/tags` continua mudando e browser congela -> investigar realtime/WebSocket/proxy/projection/UI;
- API interna saudável e apenas forwarded browser falha -> Codespaces/Vite/proxy ganha peso.

Não reinicie o ambiente antes de capturar a evidência que diferencia essas hipóteses.

## ORDEM DE TRABALHO APÓS O FREEZE

1. `RECHECK-SIM-PUMP-LEVEL` primeiro.
2. Correlacionar `UIAUD-289-003` com API interna 5080 versus Vite/forwarded 5173. Preserve a diferença entre texto de UI `(500) Failed to fetch` e HTTP 500 realmente provado pelo backend.
3. Diagnosticar e corrigir `UIAUD-289-001` genericamente, cobrindo Screen Editor `pump01 · DYN` e Popup Editor `current · VALU`. Não enfraquecer validação de tipo visual desconhecido.
4. Resolver semantics de project selection/checkout/workspace para `UIAUD-289-002` antes de tocar lifecycle/Active authority.
5. Separar tecnicamente os subproblemas de `UIAUD-289-007` sem renumerar o finding.
6. Depois tratar 011 e 017.
7. Reavaliar 004 somente após saúde da simulação.
8. Deixar 016 sem correção até existir evidência suficiente.
9. Para todo defeito confirmado: correção genérica, regressão determinística, novo exact SHA validado, targeted Work recheck somente quando justificado.
10. Não iniciar homologação final do Product Owner antes de concluir essa sequência.

## GUARDRAILS PERMANENTES

- GitHub live sempre prevalece.
- Nunca alterar `main` diretamente.
- PR #212 permanece OPEN/DRAFT e **não pode mergear em `main` sem autorização futura, separada e explícita do Product Owner**.
- `siga`, CI verde, Work audit, Preview ou homologação não autorizam #212.
- PRs #266, #288, #292 e #293 são validation-only / **MUST NEVER MERGE** onde aplicável.
- PR #296 é diagnostic-only / **MUST NEVER MERGE**.
- Preservar #285 como evidência histórica PRE-C26.
- PR #290 é Preview-only, não é rota para `main`.
- Sem force push, rebase destrutivo, exclusão de branch, blind rerun ou limpeza fora de escopo.
- Nunca enfraquecer testes/validação, segurança, autenticação, autorização, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics ou backend Active Runtime authority.
- Runtime/Active deve ser autocontido e não pode depender de `.escadalib`.
- Alarm, Operational Event e Audit continuam autoridades distintas.
- Não mascarar defeito genérico com workaround EEE-specific.
- Wave13 #205/#207 permanece pausada.

Ao assumir, não faça apenas um resumo. Revalide tudo ao vivo e continue o diagnóstico técnico a partir do Codespace real.

---
