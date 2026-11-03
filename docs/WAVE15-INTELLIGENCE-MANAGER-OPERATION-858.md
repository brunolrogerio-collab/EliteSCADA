# Intelligence #858 — operação restrita do gestor

Baseline: merge #859, `854750285c25efd9665b39f8b20a016237626ff3`. Candidata separada da composição #854. A autorização do proprietário para concluir a auditoria #791 cobre a separação original desenvolvedor/gestor; AUTH-03/04, policy canônica, licença e HA não são reabertos.

## Decisão e implementação candidata

`AssetProcessIntelligenceApi.SetScopeActivationAsync` permite `AssetProcessIntelligenceManage` no Equipment/Location e `EngineeringView` para operar sem `EngineeringModify`. Configuração/seleção/perfis continuam exigindo edição. Não há role por nome, grant implícito, nova capability ou cópia de policy no projeto.

A operação reutiliza o ciclo persistente existente: alterar exclusivamente Active do recurso pode criar/publicar/ativar uma revisão derivada. Não é uma publicação geral concedida ao gestor. A transição usa `EngineeringWorkspace.AcquireMutationAsync` desde a conferência inicial. Para gestor restrito:

1. Exigir projeto/revisão observados pela UI nos headers X-Intelligence-Project-Key e X-Intelligence-Active-Revision. Divergência recusa a operação.
2. Exigir Working limpo, mesma revisão efetiva Runtime/persistência e escopo já elegível. Comparar o pacote atual inteiro com o snapshot ativo, normalizando somente ExportedAt. Divergência, mesmo com indicador dirty incorretamente limpo, impede a operação.
3. Depois de salvar, comparar a revisão imutável inteira com o snapshot ativo acrescido somente da alteração Active do recurso pedido. Se aparecer outra mudança, não publicar nem ativar essa revisão; reverter somente Active e manter o Working como pendente.
4. Manter licença, propriedade efetiva, policy/HA epoch, permit exato, cancelamento e auditoria existentes. Reinício/remoção de permit continua exigindo reativação explícita. Idempotência não cria nova revisão.

GET /scopes informa canManage por recurso; UI libera apenas o controle operacional, mantendo autoria desabilitada. A API continua autoritativa. Help/manual três idiomas explica a diferença e que uma resposta de erro ao pausar não comprova pausa durável em todos os nós. Não apresentar a retirada local de permit no caminho de falha como conclusão persistente.

## Evidência e limites

Testes HTTP de contrato usam API real em processo com store de revisão de teste: gestor ativa/pausa/rearma sem edição; edição geral recusada; grant de outro recurso recusado; falta de acesso de leitura recusada; headers ausentes/desatualizados recusados; rascunho preservado; divergência com dirty limpo recusada; mudança injetada na revisão salva impede publicação; pedidos concorrentes sobre a mesma revisão resultam em uma transição e uma recusa. Reset do gate exige solicitação explícita, sem nova revisão na reativação idempotente. Isso não é prova de reinício do processo nem HA multi-node.

Navegador montado:6/6, gestor e editor nos três idiomas; headers observados e controles de autoria bloqueados. Build Web aprovado; manual69 arquivos alinhados. Contratos HTTP34/34 e Help30/30:64/64 aprovados. A primeira simulação da revisão alterada usou serialização diferente da canônica e gerou uma resposta inválida no teste; o injetor foi corrigido para preservar o JSON canônico e alterar somente o nome de um equipamento. Resultado final64/64, sem relaxar asserções. CI exato será registrado no PR/#858.

Pendências antes de fechar #858: identidade gestora real e persistência PostgreSQL no laboratório isolado; cenário Location/área; revalidação de revogação/concorrência/restart nos limites reais; capturas e auditoria final. Não atualizar Preview/DEMO/Portal nem encerrar a auditoria global por esta candidata.
