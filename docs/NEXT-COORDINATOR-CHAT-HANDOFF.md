# Next Coordinator Chat Handoff

Copy the text below into a new coordinator chat if rotation is required.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Antes de qualquer decisão, diagnóstico, alteração de código/documentação, ação em PR, rerun ou merge, revalide o estado ao vivo. Havendo divergência, GitHub live prevalece.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Branch ativa:

`wave14/c26-po-homologation-corrections`

Issue coordenadora:

#286

## TOPOLOGIA E PROIBIÇÕES

- PR #287 — C26 -> `wave14/c11-canonical-eee-demo` somente.
- PR #288 — C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**.
- PR #266 — C11 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**.
- PR #263 — C11 -> `wave14/corrections-integration` somente.
- PR #212 — integração Wave14 -> `main` — OPEN/DRAFT e **SEM AUTORIZAÇÃO DE MERGE**.
- Preview #285 — evidência histórica pré-C26; preservar intocado.
- Issue #289 — gate posterior de auditoria real via ChatGPT Work; não executar durante C26.
- Wave13 #205/#207 — pausada.

`siga`, CI verde, C26 concluído, Preview, Work audit ou homologação não autorizam #212. O merge em `main` exige autorização futura, separada e explícita do Product Owner.

Não existe autorização atual para integrar #287. Primeiro C26 deve ficar 5/5 verde, ser aceito e receber autorização explícita para C26 -> C11.

## LEITURA OBRIGATÓRIA

Leia ao vivo no branch C26, nesta ordem:

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
6. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
7. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`
8. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
9. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
10. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Depois revalide branch/exact HEAD, #286, #289, PRs #287/#288/#212/#266/#263, C11 canônico, Preview #285 e workflows/logs do exact product/test SHA.

## ESTADO DE TRANSFERÊNCIA

O commit documental desta rotação está acima do candidato de produto. Não confunda HEAD documental com SHA validado.

Exact C26 product/test SHA atual:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

Último exact product/test SHA comprovadamente 5/5 verde:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

Estado:

- C26.1–C26.7 — **IMPLEMENTADAS / VALIDADAS**
- C26.8 Screen Editor — **IMPLEMENTADA / VALIDADA** em `6de64ed4...`
- C26.9 Popup Editor — **IMPLEMENTADA / VALIDADA** em `55f29235...`
- C26.10 EEE cleanup — **IMPLEMENTADA / NÃO VALIDADA / BLOQUEIO ATUAL**
- C26.11 repackage/new Preview — **NÃO INICIADA / BLOQUEADA**

## EVIDÊNCIA DO EXACT SHA ATUAL

Em `e7c8a8bf3954890a1ca841498222bf6094952baf`:

- EliteSCADA CI #1471 / run `34347157464` — SUCCESS
- Preview Licensing CI #419 / run `34347157585` — SUCCESS
- Interop Lab Smoke #302 / run `34347159685` — SUCCESS
- L3 Seven-Driver Lab #375 / run `34347157398` — SUCCESS
- Wave 11 Active HMI Runtime #397 / run `34347157735` — **FAILURE**
- Interop natural adicional #301 / run `34347157333` — SUCCESS

Não houve rerun.

## BLOQUEIO ATUAL — C26.10

Job Wave11:

`102451385709`

Passo:

`Run Wave 11 Active Runtime browser lifecycle`

Resultado Playwright:

- 17 passaram;
- 2 falharam;
- 4 não executaram.

Falharam:

- `tests-wave11/c11-eee-demo-hmi.spec.ts`
- `tests-wave11/c26-popup-composition.spec.ts`

As duas falhas ocorreram no Preview do package, antes das assertions de Runtime. `preview.canApply` retornou false.

Entidade inválida:

`eee.dynamo.pump`

Erros `VISUAL_PROPERTY_INVALID`:

- `pump-stopped-label`
- `pump-running-label`
- `pump-fault-label`

Motivo exato:

`core.text` não declara `backgroundColor`.

Histórico: o candidate C26.10 `50363bcc50037cc6932a1282e58e3b6d75fda9f2` tentou impedir a sobreposição visual de `PARADA` sob `OPERANDO`/`FALHA`, adicionando `backgroundColor` e `cornerRadius` aos três textos. A intenção visual é correta, mas a representação é inválida: os schemas backend e browser de `core.text` contêm somente Base + Text e não declaram nenhuma dessas propriedades.

Artifact:

- `playwright-report-wave11`
- ID `10102316731`
- run `34347157735`

O SHA atual também contém uma correção genérica e válida de PostgreSQL: Operational Event history passou a usar o mesmo advisory lock `4993446713136202561` das demais stores do schema compartilhado, com regressão concorrente. EliteSCADA CI #1471 ficou verde. Não reverta essa correção.

## PRÓXIMA AÇÃO OBRIGATÓRIA

Antes de editar:

1. revalide GitHub ao vivo;
2. recupere novamente o job `102451385709` e artifact se necessário;
3. leia no exact SHA:
   - `web/scada-web/tests-wave11/c11-eee-demo-hmi.ts`
   - `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`
   - `src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs`
   - `web/scada-web/src/visual-runtime/builtinVisualObjectSchemas.ts`
   - `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`

Depois:

1. substitua a representação inválida por uma composição EEE mínima usando somente objetos/propriedades visuais públicos e válidos;
2. preserve placas opacas, cobertura integral dos bounds e precedência Falha > Operando > Parada;
3. preserve Preview/package validation e testes estritos;
4. não crie exceção EEE na validação;
5. não amplie `core.text` apenas para acomodar a fixture EEE; qualquer expansão genérica exige justificativa independente, paridade backend/browser e regressões completas;
6. não altere security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package authority, Drivers ou Runtime Active Revision authority;
7. publique somente a menor correção sustentada pela evidência;
8. deixe os workflows normais dispararem naturalmente;
9. diagnostique qualquer vermelho antes de rerun;
10. exija os cinco gates normais verdes no mesmo exact SHA.

Somente então C26.10 pode ser registrada como VALIDADA.

Somente depois iniciar C26.11.

## GATE PÓS-C26 — CHATGPT WORK

Issue #289 e `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` continuam obrigatórios, mas **NÃO executar agora**.

Ordem:

`C26 5/5 verde + aceito -> integração C26->C11 explicitamente autorizada -> C11 corrigido -> novo .escadapkg/checksum/provenance -> NOVO Preview pós-C26 -> técnico verde + ambiente real preparado -> READY FOR WORK AUDIT -> Work audit real no navegador -> triagem/reprodução/correções -> novo candidato -> recheck dirigido se necessário -> homologação final do Product Owner`

O Work deve receber o SCADA vivo, EEE Active, simulação, TAGs dinâmicas, Alarm/Event/Historian/HistoricalQuery, Runtime, Engineering, Screen Editor, Popup Editor e usuários preparados previamente. Não gastar a janela aproximada de 40 minutos fazendo setup.

Criar `docs/WORK-UI-AUDIT-HANDOFF.md` somente quando houver candidato real pronto, com exact SHA, URLs reais, SHA-256 do package e autenticação sem segredo versionado.

## GUARDRAILS PERMANENTES

- GitHub live sempre prevalece;
- nunca alterar `main` diretamente;
- nunca mergear #288;
- nunca mergear #266;
- nunca mergear #212 sem autorização futura, separada e explícita do Product Owner;
- #287 é apenas C26 -> C11 e não está autorizado agora;
- #263 é apenas C11 -> integração;
- preservar #285;
- #289 não autoriza execução antecipada nem merge;
- sem force push, rebase destrutivo, exclusão de branch ou limpeza fora do escopo;
- sem rerun cego;
- nunca enfraquecer testes/validação, segurança, autenticação, autorização, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers ou Runtime authority;
- Runtime/Active deve ser autocontido e não pode depender de `.escadalib`;
- não mascarar defeito genérico com workaround EEE-specific;
- Alarm, Operational Event e Audit continuam distintos;
- Wave13 permanece pausada.

Quando o Product Owner disser `siga`, avance autonomamente pelas próximas tarefas seguras. Se workflows estiverem rodando e não houver tarefa paralela, pare para o Product Owner monitorar. `siga` nunca autoriza merges protegidos.

No fim de cada interação, registre as últimas ações no repositório, normalmente em comentário preciso na issue #286.

---
