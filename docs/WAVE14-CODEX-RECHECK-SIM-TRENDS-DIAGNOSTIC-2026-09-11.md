# Wave 14 — Codex RECHECK-SIM-PUMP-LEVEL e Runtime Trends — 2026-09-11

## Escopo e autoridade

Diagnóstico/documentação somente, executado após A1/A2 conforme a instrução MAIN COORDINATOR -> CODEX mais recente na issue #286, comentário 5629189357. GitHub live foi revalidado. Candidato técnico: 59e815eae524b9ff043ea6bf3f797f4c01ba9143. Pacote SHA-256: e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d.

Nenhum restart, reopen deliberado, ativação, checkout, Save/Publish, mutação de Active, alteração de produto, teste, workflow, PR protegido ou merge foi realizado. A ocorrência apareceu naturalmente e foi preservada antes de qualquer recuperação.

## Linha temporal correlacionada

- API iniciou às 04:02:45 UTC; Vite às 04:02:58 UTC. PIDs 2776 e 3014 continuavam vivos por mais de uma hora.
- Runtime Active: eee-demo, revisão 2, activatedAtUtc=2026-09-11T04:02:50.5928172Z.
- Historian registrou 104 amostras de EEE.Process.LevelPct entre 04:02:37.732744Z e 04:04:33.018222Z. Valor final 39.060000000009474, quality 0, source eee.sim.server-memory.
- Às 05:07 UTC, API direta 5080 e proxy Vite 5173 retornavam 200 e exatamente o mesmo valor/timestamp. /health, /api/runtime/application, /api/diagnostics/runtime e History também respondiam 200.
- O Runtime encaminhado exibiu todos os valores como —, ambos os conjuntos como FALHA e QUALIDADE RUIM. Depois alternou para HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE (500) Failed to fetch, sem API 500 correspondente, coerente com A1.

## W14-CODEX-SIM-001

- **Severidade:** P1
- **Área:** Runtime / Server Scripts / TAGs / Historian
- **Título:** Server Script Active entra em throttling permanente após timeouts e congela o processo enquanto API/Vite permanecem saudáveis
- **Classificação:** CONFIRMED — GENERIC PRODUCT. O script EEE é o estímulo; o mecanismo de paralisação durável pertence ao runtime genérico.
- **Reprodutibilidade:** 1/1 nesta janela natural, consistente com a hipótese RECHECK-SIM-PUMP-LEVEL estacionada.

### Reprodução

1. Iniciar o Preview canônico e deixar eee-demo r2 Active sem reiniciar ou reativar.
2. Observar EEE.Process.LevelPct e os demais TAGs por alguns minutos.
3. Quando a tela mostrar —/qualidade ruim, consultar /api/tags, History e /api/diagnostics/runtime antes de recuperação.
4. Comparar timestamps dos TAGs/Historian com PIDs, health e diagnóstico do Server Script.

### Esperado

O timer de 1000 ms deve continuar executando ou entrar em estado de falha observável e recuperável. Falha transitória do sandbox não pode congelar silenciosamente o modelo Active indefinidamente. TAGs sem atualização devem expor frescor confiável.

### Observado

Diagnóstico do handler eee-demo@2:c1100000-0000-4000-8000-000000000002:

- executionCount=108; completedCount=102;
- timeoutCount=6; faultedCount=0; cancelledCount=0;
- consecutiveFailures=5; isThrottled=true;
- lastStatus=2; lastDuration=00:00:00.4515763; lastCompletedAt=04:04:38.3925189Z;
- queuedEvents=1; queueCoalescedCount=3889.

O Active contém um script Python Server habilitado, timer de 1000 ms, 301 linhas, executado em subprocesso isolado por dispatch. O host usa timeout padrão de 250 ms e throttling após cinco falhas consecutivas.

### Mecanismo e paths

1. IsolatedPythonScriptHandlerExecutor.ExecuteAsync cria novo processo Python isolado, envia payload, espera saída e reaplica escritas canônicas. Todo o caminho recebe o lease com timeout: src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs:24.
2. Timeout padrão é 250 ms e limite padrão é cinco falhas: src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs:424.
3. ProcessNextAsync retorna Throttled antes de retirar o evento quando IsThrottled está ativo: src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs:129.
4. ScriptFailureThrottle ativa o latch após cinco Faulted/TimedOut; reset somente explícito: src/Scada.Engineering/VisualScripting/ScriptEventRuntimeFoundation.cs:460.
5. O timer continua chamando TriggerAndDrainAsync; um evento fica na fila e os seguintes são coalescidos: src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs:596.
6. Historian parou em 04:04:33; o script concluiu sua última tentativa em 04:04:38 e permaneceu throttled com API/Vite vivos.

### Fatos excluídos

- Não houve queda dos processos API/Vite nem indisponibilidade local das portas.
- Active permaneceu eee-demo r2.
- History retornou 200 e as 104 amostras existentes.
- Não foi apenas perda visual/realtime: API e Historian congelaram no mesmo timestamp.
- Não há erro Python exposto: faultedCount=0 e lastSanitizedError=null; seis eventos foram timeouts.

### Contrato genérico para Wave 15

- Preservar isolamento Python, allowlist, timeout, cancelamento, Active revision gate e replay canônico.
- Medir separadamente startup/IPC, handler e replay; reutilizar worker isolado ou outro desenho bounded. Não apenas remover/elevar timeout.
- Trocar latch permanente silencioso por estado observável e recuperável, com cooldown/half-open ou probe bounded e reset auditável.
- Projetar saúde do script e frescor dos TAGs para Runtime/Monitoring, com causa e última execução útil.
- Manter coalescing e capacidade bounded; impedir loop ocupado durante throttle.

### Regressão determinística

- Ativar timer com volume equivalente de dependências/escritas e executar em janela longa.
- Injetar timeouts transitórios e provar recuperação automática controlada; falha persistente deve produzir degradação visível, fila bounded e nenhum write obsoleto.
- Verificar timestamps TAG/History, transição de frescor e recuperação sem restart/reativação.
- Validar contadores de execução, timeout, throttle, coalescing e recovery.

### Dependência e ordem

Corrigir primeiro runtime genérico e observabilidade; depois revalidar EEE, Historian, realtime e Screens/Popups/Trends no mesmo Active. Sem ajuste EEE específico.

### Evidência

- evidencias/W14-CODEX-RECHECK-SIM-TRENDS-2026-09-11.txt
- evidencias/W14-CODEX-RECHECK-SIM-FROZEN-RUNTIME-2026-09-11.jpg

## W14-CODEX-TRENDS-001

- **Severidade:** P2
- **Área:** Runtime / Trends / Historian / realtime / navegação
- **Título:** silent return não reproduzido; Trends ficou preso em conexão e depois foi encoberto por oscilação de transporte
- **Classificação:** UNCERTAIN — BOUNDED
- **Reprodutibilidade:** 0/2 para retorno silencioso; 1/1 para painel aberto em Conectando dados ao vivo…; tentativa posterior interrompida por (500) Failed to fetch.

### Reprodução e observado

1. Com eee-demo r2 visível, clicar TENDÊNCIAS.
2. Primeira tentativa permaneceu na mesma tela e montou Basic Trends; cinco séries ficaram sem valor em Conectando dados ao vivo….
3. History local do LevelTagId retornou 200 e 104 pontos.
4. Tentativa posterior fez o Runtime inteiro mudar para HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE (500) Failed to fetch; API local continuava saudável.

### Esperado

Trends deve permanecer montado, renderizar pontos históricos e distinguir ausência de realtime, no-data, falha de History e perda da projeção Runtime.

### Limite e captura futura

Não houve waterfall de TAGs/History, estado SignalR/WebSocket e RuntimeVisualNavigator antes/depois sob forwarding estável. O retorno silencioso foi refutado nesta amostra; espera indefinida e transporte oscilante impedem causa final.

Em sessão encaminhada estável, registrar no mesmo request ID: catálogo/projeção antes/depois, status/duração de TAGs e History, mensagens do hub, active screen/popups e projectKey/revision/activatedAtUtc. Repetir com History com e sem pontos e classificar separadamente BasicTrend, realtime e Codespaces forwarding.

## Limites preservados

Nenhuma correção foi implementada. Active/lifecycle, autenticação/autorização, Engineering Lock, Licensing, package contract, Historian, Drivers, testes e rotas de merge permaneceram intactos. PRs Preview/diagnostic/validation-only permanecem não integráveis.
