# Intelligence #858 — operação restrita do gestor

Baseline: merge #859, `854750285c25efd9665b39f8b20a016237626ff3`. Candidata separada da composição #854. A autorização do proprietário para concluir a auditoria #791 cobre a separação original desenvolvedor/gestor; AUTH-03/04, policy canônica, licença e HA não são reabertos.

## Decisão e implementação candidata

`AssetProcessIntelligenceApi.SetScopeActivationAsync` permite `AssetProcessIntelligenceManage` no Equipment/Location e `EngineeringView` para operar sem `EngineeringModify`. Configuração/seleção/perfis continuam exigindo edição. Não há role por nome, grant implícito, nova capability ou cópia de policy no projeto.

A operação reutiliza o ciclo persistente existente: alterar exclusivamente Active do recurso pode criar/publicar/ativar uma revisão derivada. Não é uma publicação geral concedida ao gestor. A transição usa `EngineeringWorkspace.AcquireMutationAsync` desde a conferência inicial. Para gestor restrito:

1. Exigir projeto/revisão observados pela UI nos headers X-Intelligence-Project-Key e X-Intelligence-Active-Revision. Divergência recusa a operação.
2. Exigir Working limpo, mesma revisão efetiva Runtime/persistência e escopo já elegível. Comparar o pacote atual inteiro com o snapshot ativo, normalizando ExportedAt e a versão da referência da Authority exclusivamente a partir do export canônico atual; contrato/IDs de papéis/escopos precisam continuar idênticos. Divergência, mesmo com indicador dirty incorretamente limpo, impede a operação.
3. Depois de salvar, comparar a revisão imutável inteira com o snapshot ativo acrescido somente da alteração Active do recurso pedido. Se aparecer outra mudança, não publicar nem ativar essa revisão; reverter somente Active e manter o Working como pendente.
4. Manter licença, propriedade efetiva, policy/HA epoch, permit exato, cancelamento e auditoria existentes. Reinício/remoção de permit continua exigindo reativação explícita. Idempotência não cria nova revisão.

GET /scopes informa canManage por recurso; UI libera apenas o controle operacional, mantendo autoria desabilitada. A API continua autoritativa. Help/manual três idiomas explica a diferença e que uma resposta de erro ao pausar não comprova pausa durável em todos os nós. Não apresentar a retirada local de permit no caminho de falha como conclusão persistente.

## Evidência e limites

Testes HTTP de contrato usam API real em processo com store de revisão de teste: gestor ativa/pausa/rearma sem edição; edição geral recusada; grant de outro recurso recusado; falta de acesso de leitura recusada; headers ausentes/desatualizados recusados; rascunho preservado; divergência com dirty limpo recusada; mudança injetada na revisão salva impede publicação; pedidos concorrentes sobre a mesma revisão resultam em uma transição e uma recusa. Reset do gate exige solicitação explícita, sem nova revisão na reativação idempotente. Isso não é prova de reinício do processo nem HA multi-node.

Navegador montado:6/6, gestor e editor nos três idiomas; headers observados e controles de autoria bloqueados. Build Web aprovado; manual69 arquivos alinhados. Contratos HTTP34/34 e Help30/30:64/64 aprovados. A primeira simulação da revisão alterada usou serialização diferente da canônica e gerou uma resposta inválida no teste; o injetor foi corrigido para preservar o JSON canônico e alterar somente o nome de um equipamento. Resultado final64/64, sem relaxar asserções. CI exato será registrado no PR/#858.

## Prova real API/PostgreSQL e conta local — 03/11/2026

Laboratório isolado intelligence-browser-853, mesma persistência TimescaleDB, conta local criada e autenticada pela rota normal, EngineeringView/TrendUse/TagRead e Manage somente em um Equipment e um Location. Sem EngineeringModify. Equipamento e área ativados/pausados/reativados nas revisões73–78; comparação integral dos pacotes comprovou somente os Active pedidos alterados. Configuração e save geral retornaram403; equipamento fora do alcance403; revisão antiga409. Rascunho de autor foi preservado após recusa409 e descartado explicitamente por checkout no laboratório. Pedidos simultâneos retornaram200/409, revisão79.

Reinício real da API: configuração/pacote preservados, sessão local continuou válida, todos permits fechados e área manteve Active persistido. Revogar Manage retornou403. A primeira restauração retornou409/WORKSPACE_DIFFERS_FROM_ACTIVE_REVISION: mudança legítima da versão da referência Authority foi confundida com edição do projeto. Corrigida comparação para aceitar somente a versão obtida do export canônico atual, preservando verificação exata de contrato e IDs. Teste dedicado recusa versão forjada, RoleIds/ScopeIds e contrato divergentes. Repetição real:403 revogado e200 após restaurar/rearm explícito. Não altera policy/epoch/claims nem inclui policy na aplicação.

A primeira conferência visual real encontrou Ativar bloqueado: o frontend exigia capability global mesmo com canManage=true no recurso. Corrigido para usar a decisão por recurso do backend, com fallback compatível. O teste montado agora remove também o grant global do gestor, sem apenas remover Modify. Interface real autenticada por cookie ativou o equipamento em200/revisão80; edição da seleção e bomba fora do alcance bloqueadas, zero analytics automáticos e zero pageerrors. Captura real inspecionada no manual nos três idiomas. A ausência de View no perfil sintético provocou fallback de branding no cabeçalho; isso não foi tratado como falha da operação nem incluído na captura do painel.

Registros PostgreSQL de auditoria: seis ativações e três pausas bem-sucedidas com IDs estáveis; três recusas com marcador genérico de escopo (sem divulgar recurso não autorizado). Nenhum segredo foi copiado. Evidências públicas em docs/evidence/intelligence-858; scripts, cookies e snapshots privados permanecem fora do repositório.

Final local:35/35 contratos HTTP/guardas +30/30 Help,6/6 navegador com perfil realmente limitado ao recurso, build Web aprovado e69 arquivos do manual alinhados. Prova não cobre HA multi-node, hardware/campo ou mudança de senha; não transfere aceite de outros escopos. Pendente CI exato/revisão/integração/pós-merge e conclusão global#791. Preview/DEMO/Portal preservados.
