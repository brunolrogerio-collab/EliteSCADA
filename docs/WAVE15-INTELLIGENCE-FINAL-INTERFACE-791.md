# Intelligence #791 — revisão final de permissões na autoria

Baseline#860 integrado em2a9c6dcbd58af5f7c57559177ffb1ba17a25dee1. T1 da candidata41742976603 SUCCESS; CI pós-merge41749116899 SUCCESS. Operação restrita#858 validada e integrada; não é mais lacuna não entregue.

## Defeito e correção

O compositor central, atalhos especializados por associação/etapa e edição de contextos ainda usavam a capability global de gestão. Um autor com EngineeringModify e grant de Intelligence em um equipamento podia operar esse recurso pela API, mas o frontend bloqueava a autoria correspondente. Isso é defeito residual da interface, não mudança de capability/policy/método.

GET /scopes passa a projetar canManage também em cada recurso disponível, antes de ele ser escolhido como elegível. O frontend usa essa decisão canônica para equipamento/associação/contexto e exige autorização em todos os membros para gerar uma etapa. Sem remover EngineeringModify, licença ou validações da API. Recursos não autorizados não recebem concessão por pertencerem ao mesmo processo. Fonte permitida pode ser escolhida independentemente da etapa bloqueada; mensagem simples explica a alternativa. Defaults/metadados, fórmulas, runtime histórico, geração sem consultas e editor canônico preservados.

## Prova

Local:37/37 contratos HTTP +30/30 Help;19/19 navegador em três baterias complementares (16 compositor/atalhos/operação,2 contexto,1 etapa mista). Inclui sem grant global permitido/negado por recurso; membro negado impede etapa e não impede o perfil autorizado. Build Web aprovado; manual69 arquivos alinhados. Não transferir teste do predecessor para o novo head; CI exato será registrado no PR.

Laboratório API/TimescaleDB reais, revisão80, novo usuário local author791: EngineeringModify presente, Manage global ausente; somente primeiro Equipment autorizado, segundo e Location negados. Login por cookie. Geração por perfil abriu rascunho no editor; etapa mista foi bloqueada com motivo visível; zero POST/PUT/PATCH/DELETE de Engenharia, zero analytics, Working limpo e zero pageerrors. O primeiro roteiro escolheu a primeira etapa, que era inteiramente autorizada; corrigido para a etapa855 de dois equipamentos, com espera pelo estado renderizado. Não houve falha de produto nessa seleção inicial. Capturas reais recapturadas com viewport suficiente para evitar corte de conteúdo; nenhuma imagem foi inventada ou editada.

Resultados e capturas: docs/evidence/intelligence-791-final. Scripts/cookies/policy snapshots privados fora do repositório. A prova deste ajuste não reaplica/publica telas: geração/publicação/Runtime/restart e resultados analíticos permanecem documentados nas provas anteriores#855/#857/#858. Não é validação de campo/HA.

## Fechamento

Este reparo completa o ponto de permissões de autoria encontrado na revisão. Ainda requer CI/revisão/integração/pós-merge, conferência final das issues-mãe e atualização controlada do preview. Nenhuma funcionalidade nova de outra Wave foi iniciada. Indicador agregado de processo/aprendizado/prognóstico e dados de campo permanecem nos limites futuros já registrados, sem serem apresentados como entregues. Preview/DEMO/Portal preservados durante a prova.

## Último acabamento de linguagem e reconciliação

#861 integrada emf6d2265bd95490300d5444e811191258da7a7874; CI da candidata41750384591 e pós-merge41754074650 SUCCESS. #855 e #857 encerradas no escopo implementado/verificado, com corpos atualizados para não apresentar constatações históricas como estado atual; #858 já concluída. Esses fechamentos não substituem aceite de produto/campo.

A conferência final da matriz encontrou texto técnico remanescente no bloqueio de desempenho e nomenclatura genérica de atalhos. A candidata de linguagem substitui TrendUse/Equipment/permit no texto de orientação por próximos passos compreensíveis, explica o relatório temporário e mantém limites de interpretação. O conjunto de quatro telas é nomeado como desempenho e desvio, o básico de etapa como medições/cobertura e o atalho analítico como desempenho/desvio por equipamento. Links levam ao compositor principal, que mantém vistas independentes. Nenhum gate, fórmula ou geração foi alterado.

Local:19/19 regressões de compositor/atalhos/acesso e30/30 Help, build Web e manual69 alinhados. A matriz principal foi reconciliada com os contratos e provas atuais; indicador agregado/prognóstico/campo continuam fora dos métodos entregues, sem promessa fictícia. Pendente CI/revisão/integração/pós-merge da candidata de linguagem e atualização controlada do preview.

Prova visual real do acabamento: sessão local, bloqueio de desempenho com próximos passos, atalho nomeado pelo conjunto fixo e link funcional ao compositor; zero gravações/analytics/pageerrors. Capturas nativas inspecionadas e incluídas no manual. A primeira captura usou clip deslocado e foi refeita diretamente nos elementos, sem editar imagens. O laboratório não iniciou inicialmente porque o papel de autoria adicionado na prova anterior ainda não tinha referência publicada na revisão80. Não é regressão deste texto: a guarda recusou corretamente a divergência de IDs. Restauro isolado via PostgreSqlLocalIdentityStore.UpdateAsync (desabilitar/desassociar somente a conta fixture author791) e PostgreSqlAuthorityPolicyStore.TryReplaceAsync (remover somente o papel fixture, com CAS/advisory-lock canônicos). Projeto/dados/política do proprietário não alterados. Reinício subsequente normal; imagens usam a sessão gestora real remanescente. Reintroduzir a fixture de autoria exige publicar sua referência antes de novo reinício.
