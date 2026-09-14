# Interface de execução local — EliteSCADA AUTH-03

## Protocolo

Ao receber `SIGA`:
1. Leia este arquivo.
2. Execute somente a ordem marcada como `PENDENTE`.
3. Registre comandos, resultado, falhas e artefatos no bloco da ordem.
4. Marque a ordem como `CONCLUÍDA`, `FALHOU` ou `BLOQUEADA`.
5. Antes de parar, publique obrigatoriamente somente `COPILOT-INTERFACE.md`:
	- execute `git status --short` e registre o estado;
	- `git add -- COPILOT-INTERFACE.md`;
	- `git commit --only -m "chore: update Copilot execution interface" -- COPILOT-INTERFACE.md`;
	- `git push origin work/w15-auth-03-security-authority-persistence`;
	- execute `git status --short` novamente e registre o hash do commit e o resultado do push.
6. Pare. Não execute outra ordem automaticamente.

### Regras permanentes

- Nunca use `git add .`, `git commit` sem `--only`, nem inclua arquivos já staged.
- Nunca publique código, testes ou workflows junto com a interface.
- Se commit ou push falhar, marque a ordem como `BLOQUEADA`, registre o erro e pare.
- A ordem só é considerada `CONCLUÍDA` após o push da interface ter confirmado sucesso.

## Contexto

- Checkout: C:\Users\Bruno Rogerio\Documents\GitHub\EliteSCADA-auth03
- Último Playwright: 624 testes; 601 passaram; 16 falharam; 3 flaky; 4 não executados.
- A API inicia; a falha de DI foi resolvida.
- Relatórios: web/scada-web/test-results/ e web/scada-web/playwright-report/.

## Ordem atual

### Ordem 001 — Classificar as falhas E2E restantes

**Estado:** `CONCLUÍDA`

**Objetivo:** analise os relatórios e traces da última execução Playwright e agrupe as 16 falhas por causa provável.

**Limites:** não reexecute testes; não altere arquivos; não crie commit; não dispare GitHub Actions.

**Resultado:**

**Comandos de análise (somente leitura):**

- `Get-ChildItem web/scada-web/test-results -Recurse -Filter error-context.md`
- Leitura dos `error-context.md` das falhas originais e dos retries.
- `Get-ChildItem web/scada-web/test-results -Recurse -Filter trace.zip`
- Nenhum teste foi reexecutado.

**Resumo dos artefatos:** 34 `error-context.md` e 34 `trace.zip`: 18 artefatos da tentativa inicial e 16 de retry. O relatório conhecido registra 16 falhas, 3 flaky, 601 aprovados e 4 não executados.

**Classificação das 16 falhas por causa provável:**

1. **Contrato de capabilities divergente (2):**
	- `effective-capabilities-contract.spec.ts`: o mapeamento de `EngineeringModify` não corresponde ao objeto esperado de superfícies.
	- `effective-capabilities-contract.spec.ts`: `licensing` foi recebido como `false`, embora o contrato espere `true`.
2. **Seed/modelo Engineering incompleto ou inconsistente (8):**
	- `administration-workspace.spec.ts` e `user-administration.spec.ts`: o checkbox/registro de papel `Operator` não aparece no formulário e causa timeout.
	- `engineering.spec.ts`: a entidade `operator` esperada não aparece na UI.
	- `engineering-mutation-security.spec.ts`: TAG `Demo.P01.Frequency` ou alarme deletável não possui ID utilizável.
	- `interface-wave-03-readiness.spec.ts`, `runtime.spec.ts` e `security.spec.ts`: conjunto de TAGs/`Demo.P01.Current` diverge do seed esperado.
	- `wave-10-visual-events-editor.spec.ts`: Screen/TAG estável esperado não é encontrado no export de Engineering.
3. **Preview/Apply e estado persistente de Engineering rejeitando pacotes (4):**
	- `engineering-apply.spec.ts` (bulk TAG): preview retorna `canApply: false`.
	- `python-editor-workspace.spec.ts`: `Apply Preview` permanece desabilitado.
	- `script-engineering-workspace-roundtrip.spec.ts` e `script-engineering-workspace-contract.spec.ts`: preview de criação retorna `canApply: false`.
4. **Contrato estático do worker Visual Python divergente (1):**
	- `visual-python-worker-clear-contract.spec.ts`: o código do worker não contém a implementação literal esperada de `visual_property_clear`.
5. **Persistência/lifecycle de projeto indisponível (1):**
	- `wave-03-integrated-composition.spec.ts`: a tela recebe `Failed to fetch` no workspace de lifecycle após a preparação do projeto.

**Flaky observados, fora das 16 falhas definitivas (3):**

- `engineering-apply.spec.ts`: mensagem de preview válido não apareceu.
- `engineering-project-management-contract.spec.ts`: `changeVersion` esperado `44`, recebido `45`, indicando deriva/contaminação de estado entre operações.
- `engineering.spec.ts`: mensagem de preview válido não apareceu.

**Conclusão:** a maior concentração é um descompasso de seed/estado persistido de Engineering, que explica as ausências de papéis, TAGs, Screens e IDs; em paralelo existem contratos de capabilities e scripts/worker que falham independentemente. A classificação foi feita somente pelos relatórios e traces existentes; nenhum arquivo de código foi alterado.

## Próxima ordem

### Ordem 002 — Diagnosticar o baseline de seed Engineering

**Estado:** `CONCLUÍDA`

**Objetivo:** compare o baseline esperado pelos 8 E2E de seed/modelo com os fluxos de inicialização do Engineering. Identifique diferenças concretas de TAGs, roles, Screens e IDs, e proponha a correção mínima.

**Limites:** não reexecute testes; não altere código ou workflow; não crie GitHub Actions.

**Resultado:**

Baseline canônico já existe em `SeedDemo`. A divergência vem do primeiro projeto limpar o workspace e os 12 workers compartilharem estado mutável; isso contamina TAGs, roles, Screens, IDs e `changeVersion`. A correção mínima proposta é serializar o projeto `chromium`, mantendo o restore canônico como baseline. Nenhum teste foi reexecutado e nenhum código/workflow foi alterado nesta ordem.

**Comandos/análise (somente leitura):**

- Leitura das 8 specs de seed/modelo: `administration-workspace.spec.ts`, `user-administration.spec.ts`, `engineering.spec.ts`, `engineering-mutation-security.spec.ts`, `interface-wave-03-readiness.spec.ts`, `runtime.spec.ts`, `security.spec.ts` e `wave-10-visual-events-editor.spec.ts`.
- Leitura de `EngineeringWorkspace`, `DemoProcessModel`, `EngineeringPersistenceApi`, `EngineeringWorkingBootstrapService`, `EngineeringWorkspaceCheckoutService`, `LocalIdentityConfiguration` e `playwright.config.ts`.
- Busca textual por `InitializeDemo`, `BootstrapAsync`, `Demo.P01`, `securityRoles`, `Operator`, `Screen` e IDs canônicos.
- Nenhum teste foi reexecutado e nenhum arquivo de código/workflow foi alterado.

**Baseline esperado pelos 8 E2E:**

- 7 TAGs `Demo.Tank01.Level`, `Demo.P01.Running`, `Demo.P01.Fault`, `Demo.P01.Current`, `Demo.P01.Frequency`, `Demo.Discharge.Pressure` e `Demo.Discharge.Flow`, todos com `builtin.simulation`; `Demo.P01.Frequency` deve ser gravável e `Demo.P01.Current` somente leitura.
- IDs de TAGs `10000000-0000-0000-0000-000000000001` a `...000000000007`, com Frequency no ID `...000000000005`.
- 2 roles: `operator` (ID `46000000-0000-0000-0000-000000000001`) e `developer`; o primeiro precisa de `commandExecute`, sem `processValueWrite`, e o segundo de `systemAdmin`.
- 1 Screen `demo.overview` (ID `44000000-0000-0000-0000-000000000001`), rota `/demo`, com elementos `tank01`, `pump01`, `pressure` e `flow`; 1 popup `popup.pump.standard`.
- Assets/IDs complementares: datasource `builtin.simulation` (`40000000-0000-0000-0000-000000000001`), template `pump.standard` (`41000000-0000-0000-0000-000000000001`), equipamento `Demo.P01` (`42000000-0000-0000-0000-000000000001`), alarms `20000000-...0001/0002` e commands `30000000-...0001/0002`.

**Fluxo de inicialização observado:**

1. `EngineeringWorkspace` começa com `SeedDemo()`, que cria o baseline completo acima.
2. Com PostgreSQL vazio, `InitializeEngineeringPersistenceAsync` executa `BootstrapAsync`; com `Engineering:InitializeDemoWhenEmpty=true`, chama `InitializeDemo()`, portanto o baseline inicial existe.
3. O endpoint de primeiro projeto (`SaveFirstProjectAsync`) limpa o workspace e salva deliberadamente um projeto vazio, repovoando apenas dynamos e o role `developer`.
4. `local-auth.spec.ts` captura o estado inicial, cria o primeiro projeto e depois restaura o pacote pelo endpoint canônico antes de salvá-lo. Esse é o único reset do baseline para os testes dependentes.
5. `playwright.config.ts` mantém um único `webServer`/workspace para o projeto `chromium`; a execução observada usou 12 workers e os testes mutáveis não têm isolamento por teste. Assim, imports, clears, saves e mudanças de `changeVersion` de specs paralelas contaminam as demais.

**Diferenças concretas observadas:**

- Os TAGs/roles/Screens/IDs esperados existem no `SeedDemo`, mas deixam de existir quando uma spec observa o workspace após o clear do primeiro projeto ou após um import concorrente.
- O role `operator` e os IDs de TAG/alarm desaparecem quando o estado vazio ou um pacote parcial vence a corrida; isso explica os timeouts de role, IDs ausentes e export sem Screen/TAG.
- O `changeVersion` observado como `44` versus `45` confirma mutação concorrente, não divergência dos IDs definidos no baseline.

**Correção mínima proposta:** serializar o projeto E2E mutável (`workers: 1` para `chromium`, mantendo o `local-auth` como pré-requisito) e usar o restore canônico do `local-auth` como baseline único antes dos dependentes. A correção não deve alterar `DemoProcessModel` nem duplicar seed em outro fluxo; se a suíte precisar de paralelismo, o passo seguinte é um fixture de reset/import por teste.

## Próxima ordem

### Ordem 003 — Executar validação E2E serializada

**Estado:** `CONCLUÍDA`

**Objetivo:** alterar `web/scada-web/playwright.config.ts` com a menor configuração válida para executar os E2E Chromium em um único worker; depois executar `npm.cmd run test:e2e` localmente com Docker/TimescaleDB, registrar a contagem final e parar.

**Limites:** não disparar GitHub Actions; não criar commit de código ou workflow. Ao terminar, publique somente a atualização do `COPILOT-INTERFACE.md`.

**Resultado:**

**Alteração aplicada:** `web/scada-web/playwright.config.ts` recebeu somente `workers: 1` no nível superior da configuração Playwright.

**Execução:**

- Container descartável `elitescada-auth03-db` iniciado com `timescale/timescaledb:2.29.2-pg18`, banco `elitescada_e2e` e `pg_isready` saudável.
- `npm.cmd run test:e2e` executado em `web/scada-web` com `ConnectionStrings__EliteScada` apontando para `127.0.0.1:5432`.
- Playwright confirmou `Running 624 tests using 1 worker`.
- Resultado final: **608 passaram; 16 falharam; 0 não executados**. O arquivo `test-results/.last-run.json` confirmou os 16 testes finais com falha.
- Falhas finais concentradas nos mesmos contratos/seed já classificados; a serialização eliminou a execução concorrente, mas não corrigiu as falhas funcionais.
- Container parado pelo bloco de limpeza e porta `5432` confirmada livre.

Nenhum GitHub Actions foi disparado e nenhum commit de código ou workflow foi criado. A publicação final deve conter somente a atualização deste arquivo de interface.

## Próxima ordem

### Ordem 004 — Diagnosticar a causa raiz dos E2E funcionais

**Estado:** `CONCLUÍDA`

**Objetivo:** diagnosticar, sem reexecutar testes, por que os 16 E2E funcionais persistem mesmo com 1 worker. Compare o estado canônico de `SeedDemo`, o export/import e restore de `local-auth`, e os relatórios da execução serializada. Determine se a causa é teste defasado, contrato de API/persistência ou seed aplicado parcialmente. Registre evidências e indique a menor correção de código necessária.

**Limites:** não alterar código/workflow, não executar testes, não disparar GitHub Actions.

**Resultado:**

**Análise somente leitura:**

- Enumerados os `error-context.md` da execução serializada e lido `test-results/.last-run.json`: 16 contextos originais, 16 IDs finais com falha e `status: failed`; a execução confirmou `624` testes em `1 worker`.
- Comparados `EngineeringWorkspace`/`SeedDemo`, `DemoProcessModel`, `EngineeringExchangeService`, `SecurityPolicyEngineeringHandler`, `EngineeringPersistenceApi`, `local-auth.spec.ts`, `playwright.config.ts` e os contextos das 16 falhas.
- Nenhum teste foi reexecutado e nenhum arquivo de código/workflow foi alterado.

**Evidências:**

1. `SeedDemo` contém o baseline canônico completo: 7 TAGs e IDs fixos, 2 roles (`operator` e `developer`), Screen `demo.overview`, popup, comandos, alarmes e assets.
2. `EngineeringExchangeService.ExportPackage()` retorna `SecurityRoles`/`SecurityScopes` vazios quando a política é Authority-owned e exporta apenas `AuthorityPolicyReference`. Isso explica o `local-auth.spec.ts` exigir `seededEngineering.body.securityRoles` vazio.
3. `SaveFirstProjectAsync` limpa o workspace e repõe somente dynamos e o role `developer`. O restore posterior do `local-auth` reutiliza o pacote sem roles; o preview/import de Engineering também bloqueia mutação de roles/scopes quando a Authority é dona da política. Portanto, o restore não reconstitui `operator`.
4. Os timeouts do checkbox `Operator`, a ausência do role na tela e as asserções de dois roles são consequência direta desse baseline parcialmente restaurado. Isso é uma divergência de seed/Authority, não uma corrida de workers.
5. Os testes de script chamam `buildCanonicalScriptPackage(...)` sem `AuthorityPolicyReference`, enquanto o preview da API valida essa referência em modo Authority-owned; os `canApply: false` são um contrato API/teste incompatível.
6. `effective-capabilities-contract.spec.ts` falha em expectativas puramente de capabilities e `visual-python-worker-clear-contract.spec.ts` falha por procurar uma implementação textual específica; são contratos/testes defasados independentes do seed.
7. As falhas de TAGs/IDs, runtime e lifecycle aparecem depois de operações de import/apply e de testes que assumem o baseline restaurado; a serialização removeu a concorrência, mas não criou isolamento/reset por teste. Os relatórios não sustentam atribuir todas essas falhas a uma única causa.

**Classificação:** a causa principal é **seed aplicado parcialmente por integração incompleta entre Engineering export/restore e Authority**; há também **contratos de API defasados** nos pacotes de script e **testes defasados** de capabilities/worker. A hipótese de concorrência dos 12 workers foi falsificada pela execução serializada.

**Menor correção de código indicada:** centralizar uma operação de restauração do baseline que, após `SaveFirstProjectAsync`/restore, reconcilie a Authority canônica com o seed demo (incluindo `operator`) sem tentar importar roles pelo pacote Engineering; em paralelo, fazer o construtor de pacotes de script carregar/anexar a `AuthorityPolicyReference` atual. Não se deve reintroduzir roles Authority-owned no export Engineering. Os testes de capabilities/worker devem ser alinhados ao contrato vigente separadamente.

## Próxima ordem

### Ordem 005 — Corrigir restore de Authority e pacotes de script

**Estado:** `PENDENTE`

**Objetivo:** implementar a menor correção para que o restore após `SaveFirstProjectAsync` reconcilie a Authority canônica do `SeedDemo`, incluindo `operator`, sem reintroduzir roles Authority-owned no export Engineering. Corrigir também o construtor de pacotes de script para anexar a `AuthorityPolicyReference` atual.

**Validação:** executar testes .NET diretamente relacionados e os E2E `local-auth`, `administration-workspace`, `user-administration` e `script-engineering-workspace`.

**Limites:** não disparar GitHub Actions. Se a validação passar, registrar os arquivos modificados e fazer commits separados com `--only`: um para código/testes e outro exclusivamente para `COPILOT-INTERFACE.md`. Publicar ambos e parar.

**Resultado:**

_Aguardando execução._
