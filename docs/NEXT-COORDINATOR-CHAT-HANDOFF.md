# Next Coordinator Chat Handoff

Copy the text below into a new coordinator chat if rotation is required.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Revalide todo o estado ao vivo antes de qualquer decisão, diagnóstico, alteração de código/documentação, ação em PR, rerun ou merge. Havendo divergência, GitHub live prevalece.

Repositório: `brunolrogerio-collab/EliteSCADA`

- Integração: `wave14/corrections-integration`, PR #212 -> main — OPEN/DRAFT, **NÃO MERGEAR sem autorização posterior, separada e explícita do Product Owner**.
- Branch ativa: `wave14/c26-po-homologation-corrections`.
- Issue coordenadora: #286.
- PR #287: C26 -> C11, DRAFT.
- PR #288: C26 -> main, **VALIDATION ONLY / MUST NEVER MERGE**.
- PR #266: **MUST NEVER MERGE**.
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

Depois revalide HEAD, issue #286, issue #289, PRs #287/#288/#212, C11 canônico, Preview vigente e workflows do SHA exato.

## Estado de transferência

Último SHA de produto C26 antes do handoff documental:

`a05d349a37a41629ee13b47b4652df74d287273d`

`fix(w14-c26): make Engineering field states readable`

Último SHA de produto/teste comprovadamente 5/5 verde:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

C26.1–C26.6 estão **IMPLEMENTADAS / VALIDADAS**.

C26.7 Engineering theme/contrast está **IMPLEMENTADA, MAS NÃO VALIDADA** e é o bloqueio atual.

No SHA `a05d349...`, quatro gates estão verdes:

- Preview Licensing #394 — SUCCESS
- Interop #271 — SUCCESS
- Wave11 #372 — SUCCESS
- L3 #350 — SUCCESS

EliteSCADA CI #1446 / run `34241442557` está vermelho.

O job Chromium `102112833905` falhou no novo teste C26.7. O relatório Playwright prova que, em Screens, o campo de rota com `readonly` continua usando o mesmo background do estado editável (`rgb(16, 25, 35)`). Isso viola diretamente a aceitação de #286, que exige estados editável/readonly/disabled visualmente distintos.

Portanto:

- não faça rerun cego de #1446;
- não afrouxe a regressão;
- revalide e busque a causa real de selector/specificity/scope do readonly em Screens;
- faça a menor correção genérica necessária;
- aguarde workflows naturais no novo SHA e exija 5/5 verde;
- somente depois marque C26.7 VALIDADA e avance para C26.8.

C26.8 ainda não deve começar enquanto C26.7 estiver vermelha.

## Gate obrigatório após C26

Issue #289 e `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` estabelecem um gate adicional deliberado antes da próxima homologação final do Product Owner.

Não executar agora.

Somente depois de C26 concluído/aceito e integrado ao C11 canônico, novo package/checksum/provenance e **novo** Preview pós-C26 tecnicamente verde, o coordenador deve preparar um SCADA realmente vivo e declarar `READY FOR WORK AUDIT` somente quando Runtime, Engineering, EEE Active, simulação dinâmica, Historian/HistoricalQuery, Alarm/Event, usuários de auditoria e URLs explícitas estiverem prontos.

Então:

`READY FOR WORK AUDIT -> ChatGPT Work audit-only de uso real no navegador (~40 min) -> relatório/evidências -> triagem e reprodução pelo coordenador -> correções genéricas + regressões -> novo candidato -> recheck dirigido se justificar -> homologação final do Product Owner`.

Na primeira passagem Work não desenvolve: não altera código, não cria patch/commit/merge e não deve desperdiçar a janela preparando dependências, banco, package ou descoberta de portas/credenciais.

Quando houver candidato real, criar `docs/WORK-UI-AUDIT-HANDOFF.md` curto e candidate-specific; nunca versionar segredo.

## Guardrails permanentes

- nunca alterar `main` diretamente;
- #212 não possui autorização de merge;
- #288 e #266 nunca mergear;
- preservar #285;
- #289 não autoriza execução antecipada nem merge protegido;
- sem force push, rebase destrutivo, exclusão de branch ou limpeza fora do escopo;
- diagnosticar CI vermelho antes de rerun;
- nunca enfraquecer teste, segurança, Identity, autorização, Engineering Lock, licensing, lifecycle, package, drivers ou Runtime Active Revision authority;
- Runtime/Active não pode depender de `.escadalib`;
- Alarm / Operational Event / Audit continuam separados;
- Wave13 segue pausada.

Quando o Product Owner disser `siga`, avance autonomamente pelas próximas tarefas seguras. `siga` nunca autoriza os merges protegidos.

---
