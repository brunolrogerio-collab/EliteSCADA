# Next Coordinator Chat Handoff

Copy the text below into a new coordinator chat if rotation is required.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Revalide todo o estado ao vivo antes de qualquer decisão, diagnóstico, alteração de código/documentação, ação em PR, rerun ou merge. Havendo divergência, GitHub live prevalece.

Repositório: `brunolrogerio-collab/EliteSCADA`

- Integração: `wave14/corrections-integration`, PR #212 -> `main` — OPEN/DRAFT, **NÃO MERGEAR sem autorização posterior, separada e explícita do Product Owner**.
- Branch ativa: `wave14/c26-po-homologation-corrections`.
- Issue coordenadora: #286.
- PR #287: C26 -> C11, DRAFT.
- PR #288: C26 -> `main`, **VALIDATION ONLY / MUST NEVER MERGE**.
- PR #266: C11 validation-only, **MUST NEVER MERGE**.
- PR #263: C11 -> integração apenas.
- Preview #285: preservar intocado como evidência pré-C26.
- Gate pós-C26 de auditoria real via ChatGPT Work: issue #289.
- Wave13 #205/#207: pausada.

## Leitura obrigatória

Leia no branch C26, nesta ordem:

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
6. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
7. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
8. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
9. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Depois revalide branch HEAD, issue #286, issue #289, PRs #287/#288/#212/#266/#263, C11 canônico, Preview #285 e workflows do SHA exato de produto/teste.

## Estado de transferência

O SHA de produto/teste C26 atual é:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

A branch contém commits documentais/de coordenação acima desse SHA. Não trate o HEAD documental como candidato de produto validado; revalide ambos ao vivo.

Último SHA de produto/teste comprovadamente 5/5 verde:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

C26.1–C26.6 estão **IMPLEMENTADAS / VALIDADAS**.

C26.7 Engineering theme/contrast está **IMPLEMENTADA, MAS NÃO VALIDADA** e é o bloqueio atual.

C26.8 Screen Editor **NÃO FOI INICIADA** e permanece bloqueada até C26.7 ficar 5/5 verde no mesmo exact product/test SHA.

### Histórico imediato da C26.7

No SHA anterior `a05d349a37a41629ee13b47b4652df74d287273d`, o Chromium provou que um input de rota em Screens continuava visualmente igual ao estado editável depois de receber `readonly`.

A causa foi diagnosticada como especificidade CSS: o selector editável acumulava especificidade pelos vários `:not([type=...])` e vencia `[readonly]`.

Foi aplicada correção mínima e genérica no SHA `642e33c...`, usando `:where(...)` nos filtros de tipo para que eles não elevem a especificidade. Não foi adicionado `!important` e o teste Playwright estrito não foi afrouxado.

No exact product/test SHA `642e33c...`:

- Preview Licensing CI #402 / run `34255187708` — SUCCESS
- Interop Lab Smoke #279 / run `34255187740` — SUCCESS
- Wave11 Active HMI Runtime #380 / run `34255187706` — SUCCESS
- L3 Seven-Driver Lab #358 / run `34255187748` — SUCCESS
- EliteSCADA CI #1454 / run `34255187823` — **FAILURE**

No EliteSCADA CI #1454:

- Backend build/test/smoke — SUCCESS
- Web build — SUCCESS
- Chromium end-to-end job `102159657922` — **FAILURE** no passo `Run browser E2E tests`

**Não houve rerun.**

Muito importante: **não assuma que a falha atual do Chromium em `642e33c...` é a mesma assertion que falhou em `a05d349...`.** Antes de qualquer novo diagnóstico ou edição, refaça ao vivo a leitura completa da evidência do job `102159657922` e confronte-a com os arquivos exatos daquele SHA.

Próximo trabalho correto:

1. revalidar GitHub ao vivo;
2. obter a falha completa do Chromium `102159657922`;
3. ler no exact SHA `642e33c...` o CSS, componente/DOM afetado e `web/scada-web/tests-e2e/wave-14-c26-engineering-contrast.spec.ts`;
4. identificar a causa real atual;
5. fazer apenas a menor correção **genérica de produto** necessária;
6. não criar workaround específico da EEE;
7. não enfraquecer teste, security, Authority, Identity, Engineering Lock, Licensing, lifecycle, package, drivers ou Runtime authority;
8. deixar os workflows naturais rodarem no novo SHA;
9. exigir os cinco gates normais verdes no mesmo exact product/test SHA;
10. somente então registrar C26.7 como VALIDADA e começar C26.8.

### Nota de coordenação

Durante a descoberta de ferramenta de escrita do GitHub no chat anterior, um arquivo placeholder `dummy` foi criado acidentalmente e removido imediatamente por commit normal. Não houve force push/rebase e não deve existir efeito de produto. Os commits envolvidos são apenas coordenação/limpeza e não servem como evidência de validação.

## Gate obrigatório após C26

Issue #289 e `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` estabelecem um gate adicional deliberado antes da próxima homologação final do Product Owner.

**Não executar agora.**

Somente depois de C26 concluído/aceito e integrado ao C11 canônico, novo package/checksum/provenance e **novo** Preview pós-C26 tecnicamente verde, o coordenador deve preparar um SCADA realmente vivo e declarar `READY FOR WORK AUDIT` somente quando Runtime, Engineering, EEE Active, simulação dinâmica, Historian/HistoricalQuery, Alarm/Event, usuários de auditoria e URLs explícitas estiverem prontos.

Então:

`READY FOR WORK AUDIT -> ChatGPT Work audit-only de uso real no navegador (~40 min) -> relatório/evidências -> triagem e reprodução pelo coordenador -> correções genéricas + regressões -> novo candidato -> recheck dirigido se justificar -> homologação final do Product Owner`.

Na primeira passagem Work não desenvolve: não altera código, não cria patch/commit/merge e não deve desperdiçar a janela preparando dependências, banco, package, portas ou credenciais.

Quando houver candidato real, criar `docs/WORK-UI-AUDIT-HANDOFF.md` curto e candidate-specific; nunca versionar segredo.

## Guardrails permanentes

- nunca alterar `main` diretamente;
- #212 não possui autorização de merge;
- #288 e #266 nunca mergear;
- #287 é C26 -> C11;
- #263 é C11 -> integração;
- preservar #285;
- #289 não autoriza execução antecipada nem merge protegido;
- sem force push, rebase destrutivo, exclusão de branch ou limpeza fora do escopo;
- diagnosticar CI vermelho antes de rerun;
- nunca enfraquecer testes/validação, segurança, autenticação, autorização, Identity, Engineering Lock, Licensing, lifecycle, package, drivers ou Runtime Active Revision authority;
- Runtime/Active não pode depender de `.escadalib`;
- não mascarar defeito genérico com workaround EEE-specific;
- Alarm / Operational Event / Audit continuam separados;
- Wave13 segue pausada.

Quando o Product Owner disser `siga`, avance autonomamente pelas próximas tarefas seguras. `siga` nunca autoriza os merges protegidos.

---
