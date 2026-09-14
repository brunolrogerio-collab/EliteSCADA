# Interface de execução local — EliteSCADA AUTH-03

## Protocolo

Ao receber `SIGA`:
1. Leia este arquivo.
2. Execute somente a ordem marcada como `PENDENTE`.
3. Registre comandos, resultado, falhas e artefatos no bloco da ordem.
4. Marque a ordem como `CONCLUÍDA`, `FALHOU` ou `BLOQUEADA`.
5. Pare. Não prossiga para outra ordem automaticamente.
6. Não altere código, crie commit, push ou GitHub Actions, exceto se uma ordem disser isso explicitamente.

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

`AGUARDANDO DEFINIÇÃO APÓS A ORDEM 001`
