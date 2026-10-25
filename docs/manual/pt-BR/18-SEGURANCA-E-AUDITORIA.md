# Segurança e auditoria: contas, permissões e proteção

## Usuários locais: criar, atribuir papéis e manter acesso

Cadastre quem entra no sistema e diferencie conta, papel, sessão e licença.

### Antes de administrar contas

Abra Engenharia > Segurança. Usuários e credenciais pertencem à Security Authority da instalação; não são objetos do projeto nem são transportados no pacote de engenharia. A conta local do produto é diferente de uma conta no portal comercial. Administrar usuários exige UserRoleAdmin ou SystemAdmin; editar o projeto, sozinho, não concede isso. Preserve pelo menos um administrador local habilitado e confira qual conta está usando antes de alterar papéis ou senha.

### Campos da conta

Use Novo usuário para cadastrar uma identidade. Para uma conta existente, selecione-a na lista; o nome de login é mostrado para identificação e não é renomeado por esse formulário. O campo de senha é usado na criação ou no formulário separado de redefinição, sem exibir a senha atual.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Usuário / login | Nome utilizado para entrar. Não confunda com o nome legível mostrado ao operador. | operacao.turno1 | 3–200 caracteres; nome único. |
| Nome de exibição | Identifica a pessoa de forma legível. Alterá-lo não troca o login nem os papéis. | Operação — Turno 1 | 1–300 caracteres. |
| Senha / Nova senha | Crie uma senha individual e siga a política aceita pelo servidor. Redefinir é substituir, não recuperar a anterior. Nunca copie senha para mensagens, scripts ou pacotes. | Preencher de forma privada | O formulário exige pelo menos 8 caracteres; o servidor valida a política. |
| Habilitado | Permite o uso da conta. Desabilitar não apaga a identidade nem o histórico de ações. Alterações podem invalidar a sessão; tenha outra conta administrativa disponível. | Marcado |  |
| Papéis | Marque papéis existentes cujas permissões correspondem ao trabalho. As concessões dos papéis atribuídos são combinadas; adicionar um papel não retira permissões de outro. Nomear um papel developer ou administrator não cria privilégios por si só. | Operador da Estação 01 | Confira os grants efetivos, não só o nome. |

### Procedimento: cadastrar e testar um operador

1. Confira os papéis disponíveis e suas capabilities/escopos no painel da Authority. Se faltar um papel adequado, prepare e aplique a política primeiro.
2. Use Novo usuário, preencha login, nome, senha privada e habilitação.
3. Selecione apenas os papéis necessários e crie a conta.
4. Para alterações posteriores, revise o resumo e confirme o salvamento; editar campos não salva por si só.
5. Entre com a nova conta em uma sessão separada e confira uma leitura permitida e uma ação que deve ser negada. A licença e a classe View Only/Interactive continuam limitando as ações.
6. Retorne à conta administrativa para corrigir os grants se necessário; não use a mesma sessão como prova de dois usuários diferentes.

### Redefinição, busca e problemas comuns

Busca filtra nome/login/papel e o filtro de estado separa habilitados e desabilitados; esconder uma linha não desabilita a conta. Para redefinir senha, selecione a conta, use Nova senha, revise o impacto e confirme. Alterar conta/papéis ou senha pode encerrar sessões; entre novamente quando solicitado. O sistema rejeita remover o último acesso administrativo local habilitado e atribuir papéis inexistentes. Se a tela disser 401, renove o login; 403 indica falta de permissão. Uma revisão de projeto não transporta usuários, e importar um .escadapkg não é restauração da Authority.

[Abrir este guia na Ajuda instalada](/help?topic=security.local-accounts)

## Papéis e escopos: conceder a ação no recurso certo

Monte concessões explícitas, confira o alcance e aplique a política validada da Authority.

### Conta, papel, capability e escopo

A conta identifica a pessoa. Um papel reúne concessões (grants). Cada grant une uma capability, isto é, a ação permitida, a um escopo opcional, isto é, onde ela vale. A política pertence à Authority, não ao desenho ou pacote do projeto. Abra Engenharia > Segurança e o painel de papéis/capabilities/escopos. Consultar a política exige UserRoleAdmin. Validar/aplicar exige EngineeringModify e UserRoleAdmin ou SystemAdmin. Os nomes dos papéis não concedem acesso por si só.

### Campos do papel e da concessão

Novo papel cria um rascunho. Duplicar copia a configuração para outro papel, não cria usuários. Na hierarquia exibida, os IDs identificam nós e recursos existentes; este painel permite escolher os nós, mas não oferece formulário para criar ou reorganizar essa árvore. Não invente um UUID para substituir um nó ausente.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Identificador estável | Chave usada nas atribuições aos usuários. Edite antes do primeiro Apply. Depois fica protegida; mudar Nome de exibição não muda a chave. | operador-estacao01 |  |
| Nome de exibição | Rótulo legível para selecionar e reconhecer o papel. Não é a sua lista de permissões. | Operador da Estação 01 |  |
| Descrição | Explique a finalidade e o alcance pretendido para evitar concessões erradas depois. | Ler a estação e reconhecer seus alarmes |  |
| Capability | Escolha a ação específica no seletor. Use a referência abaixo; uma capability não vira outra apenas por ter nome parecido. | AlarmAcknowledge |  |
| Escopo | Sem escopo concede essa capability globalmente. Selecionar um nó restringe a concessão ao recurso correspondente. Global não significa todas as capabilities. | Nó da Estação 01 |  |
| Incluir descendentes | Com nó selecionado, inclui sua descendência na hierarquia de autorização. Desmarcado cobre só o nó. Não é busca pelo começo do nome/caminho. | Estação e equipamentos descendentes | Desabilitado quando não há nó. |

### Referência das ações disponíveis

Estas são as opções exibidas na versão atual. Permissão não cria recurso, não altera licença e não dispensa revisão ativa, prontidão, contexto ou limites da operação.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| View | Visualizar o Runtime; não concede comandos. | View |  |
| TagRead | Ler TAGs conforme o recurso/escopo autorizado. | TagRead |  |
| CommandExecute | Executar comandos registrados. É diferente de escrever um valor de processo. | CommandExecute |  |
| ProcessValueWrite | Solicitar escrita de valor de processo, como um setpoint. Não substitui a permissão de comando. | ProcessValueWrite |  |
| AlarmAcknowledge | Reconhecer alarmes; não corrige a causa física. | AlarmAcknowledge |  |
| AlarmShelve | Suprimir/retirar supressão de alarmes pela operação disponível, respeitando a definição. | AlarmShelve |  |
| TrendUse | Usar consultas/tendências autorizadas. | TrendUse |  |
| TrendSave | Salvar configurações de tendência onde disponível; não concede edição geral do projeto. | TrendSave |  |
| EngineeringView | Consultar a Engenharia autorizada. | EngineeringView |  |
| EngineeringModify | Alterar Engenharia, respeitando o Engineering Lock e os requisitos de cada operação. | EngineeringModify |  |
| UserRoleAdmin | Administrar usuários/papéis pelos fluxos autorizados. Alterar a política também exige edição de Engenharia. | UserRoleAdmin |  |
| SystemAdmin | Administrar operações de sistema que exigem esta capability, como consultar Audit. Não presuma que substitui toda capability específica. | SystemAdmin |  |
| HighAvailabilityObserve | Observar estado de HA. | HighAvailabilityObserve |  |
| HighAvailabilityTransfer | Solicitar transferência de autoridade HA conforme prontidão e permissões. | HighAvailabilityTransfer |  |
| HighAvailabilityAdmin | Administrar configurações de HA. A operação pode exigir outras permissões de Engenharia. | HighAvailabilityAdmin |  |
| AssetProcessIntelligenceManage | Gerenciar Intelligence com grant explícito global ou restrito a Equipment/Location válido. Licença e ativação continuam necessárias. | AssetProcessIntelligenceManage |  |

### Procedimento: preparar e aplicar uma política

1. Carregue a política atual e identifique quem continuará administrando.
2. Crie um papel ou selecione um existente; confira usuários já atribuídos antes de editar.
3. Adicione um grant por capability necessária e escolha global ou o nó apropriado. Se houver descendentes, confira o alcance antes de marcá-los.
4. Para Intelligence, adicione AssetProcessIntelligenceManage explicitamente: global ou Equipment/Location com vínculo válido ao recurso. Um SystemAdmin isolado não deve ser usado como substituto desse grant na configuração das ferramentas.
5. Execute Preview da política; corrija os erros e aplique somente o candidato validado. Nova edição invalida a prévia.
6. Atribua o papel à conta e teste acesso permitido/negado com essa conta.
7. Se outro administrador mudou a política, recarregue e refaça a edição sobre a versão atual. Não tente aplicar uma versão antiga nem apagar usuários para contornar conflito.

### Prévia, sessão e recusas

A prévia do usuário selecionado combina grants dos papéis do rascunho; é explicativa e não comprova permissão para um recurso concreto. As capabilities efetivas pertencem à sua sessão atual, não à pessoa selecionada. O backend decide cada operação considerando identidade, recurso e escopo; Runtime também considera classe de sessão e licença. A política da Authority usa seu próprio Preview/Apply e versão: não é ativada exportando um projeto. Exclusão de papel ainda atribuído e perda do último administrador são recusadas. Se um nó não existe ou é incompatível, escolha um vínculo correto; não aumente para global só para eliminar um erro.

[Abrir este guia na Ajuda instalada](/help?topic=security.policy-scopes)

## Auditoria: localizar ações e conferir a gravação

Use os filtros certos, leia o resultado e diferencie lista vazia de falha de persistência.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-AUDIT: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=audit.query-diagnostics; fields=audit.retention; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### O que a auditoria responde

Abra Auditoria no sistema. O acesso exige SystemAdmin no Runtime autorizado. Audit registra ações protegidas e seus resultados: quem tentou qual operação, sobre qual recurso e quando. Sucesso administrativo não é prova de que uma bomba realmente partiu; confira feedback do comando e TAGs. Audit não substitui alarm.events, operational.events ou leituras do Historian. A tela é de consulta/diagnóstico e não oferece edição das ocorrências.

### Campos de consulta

Deixe filtros opcionais vazios para ampliar a pesquisa. Os filtros preenchidos são combinados: um item deve satisfazer todos. São valores exatos, não trechos de texto livre. Use valores de um evento conhecido para evitar adivinhar IDs. Digitar filtros não consulta até selecionar Consultar.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| De | Início no horário local do navegador, convertido para UTC na consulta. Sem início não se aplica esse limite inferior. | Dia do incidente, 08:00 | Não pode ser posterior a Até. |
| Até | Fim do período; a comparação inclui o instante informado. Se a consulta não encontra o evento, confira fuso e período antes de supor que foi perdido. | Mesmo dia, 09:00 |  |
| Usuário / subject | Identidade estável do autor registrada no evento, não necessariamente seu nome de exibição ou login. Copie do detalhe de um evento. | ID da identidade observada |  |
| Ação | Nome técnico exato da operação registrada. Procure primeiro por período/resultado se não souber a ação. | command.execute |  |
| Resultado | Todos não restringe resultado. Sucesso/Succeeded indica operação registrada como bem-sucedida; Negado/Denied, recusa de autorização; Falha/Failed, operação que falhou. | Denied |  |
| Tipo de recurso (avançado) | Categoria do alvo, não o nome da ação nem o tipo de TAG. | command |  |
| Recurso / chave (avançado) | Identificador exato usado pelo evento para seu alvo. Pode ser uma chave/caminho ou ID, dependendo da operação; não é sempre um UUID. | pump.start |  |
| Área (avançado) | Área registrada no contexto da ação. Um evento sem área não ganha uma por causa do filtro. | Estacao01 |  |
| Correlation ID (avançado) | Relaciona registros da mesma requisição/fluxo quando o sistema informa essa correlação. Não é o ID individual do evento. | Copiar de um evento conhecido |  |
| Eventos por página | Limita quantos registros aparecem em cada página. Não muda retenção nem quantidade total armazenada. | 100 | Inteiro de 1 a 1000. |

### Procedimento: investigar uma recusa

1. Selecione um período curto ao redor da tentativa conhecida e Resultado = Negado.
2. Clique Consultar. Selecione uma linha da lista para abrir o detalhe; teclado e Enter também selecionam.
3. Leia identidade, ação, alvo e metadados de motivo; não conclua que a licença é a causa apenas por ver uma recusa.
4. Copie ação/identidade/alvo do detalhe para refinar os filtros, caso necessário.
5. Confira papéis, grants e escopo com a administração da Authority. Não altere o histórico para tentar liberar acesso.
6. Use Próxima/Anterior para páginas do mesmo filtro. Ao mudar filtro e Consultar, a navegação volta à primeira página.
7. Limpar remove filtros e consulta novamente; não apaga eventos. Atualizar relê a página atual e os diagnósticos.

### Diagnósticos: valores de leitura

Expanda Diagnósticos. Uma fila temporária não comprova perda, mas falhas/rejeições/descartes exigem investigação. A saúde da consulta e a saúde da gravação são diferentes. Estes indicadores são de leitura: não há campos para editar política de retenção nesta tela.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Persistidos / última persistência | Contagem do armazenamento e horário da última gravação informados pelo serviço; não são apenas os itens da página. | Conferir contagem e horário juntos |  |
| Falhas de persistência | Indica falha ao gravar no armazenamento; consulte diagnóstico do serviço e disponibilidade do banco. Um contador histórico não prova falha em andamento. | Comparar contagem/última falha |  |
| Fila pendente | Registros aguardando encaminhamento pelo buffer. Verifique se a fila diminui quando o serviço se recupera. | 0 em repouso |  |
| Encaminhados / falhas de encaminhamento | Acompanham a saída do buffer e falhas desse encaminhamento; diferem da contagem da consulta. | Conferir último encaminhamento |  |
| Rejeitados por limite / descartados no shutdown | Avisam registros recusados pelo limite ou descartados ao encerrar. Não trate a lista consultável como prova de completude se houver esses indicadores. | Investigar ocorrências não zero |  |
| Retenção / última retenção | Mostra habilitação, condição finita/indefinida e execução da limpeza configurada no serviço. Indefinida significa sem prazo finito ativo, não disco sem limite. Backup e arquivamento são outros procedimentos. | Conferir diagnóstico; não preencher |  |

### Sem resultados ou sem acesso

Uma lista vazia pode significar combinação de filtros sem correspondência, período/fuso errado ou ausência de gravações. Limpe filtros, confira horário e examine os diagnósticos antes de concluir perda. Uma página cheia sem botão Próxima disponível não garante que não existam outros eventos fora do filtro. 401 pede novo login; 403 exige permissão adequada; 400 indica filtro inválido. A paginação usa referências internas fornecidas pelo servidor: não há cursor para digitar no formulário. Esta tela não configura tamanho global de banco ou arquivo; não confunda diagnósticos de retenção com essa administração.

[Abrir este guia na Ajuda instalada](/help?topic=audit.query-diagnostics)

## Engineering Lock: proteger e reabrir a aplicação

Use o segredo da aplicação sem confundi-lo com senha de usuário, backup ou permissão.

### Quando usar e o que ele protege

O Engineering Lock protege o conteúdo de engenharia da aplicação. Não é uma reserva de edição de outro operador e não concede capabilities a quem sabe o segredo. A senha da conta autentica a pessoa; o segredo do Lock protege a aplicação; a senha do backup da Authority protege outro material. Guarde-os pelos procedimentos apropriados, sem escrever em telas, scripts ou ajuda. Mesmo bloqueada, administração do sistema, Import/Export/Restaurar e desbloqueio permanecem sujeitos às suas permissões normais.

### Estados e ações

Na Engenharia desbloqueada, abra o controle do cadeado na barra superior. Quando bloqueada, a superfície restrita apresenta o formulário de desbloqueio e as funções administrativas disponíveis. Os editores protegidos não ficam abertos por trás de uma simples mensagem.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Segredo configurado | Informa que existe um segredo. Configurado não significa necessariamente bloqueado neste momento. | Configurado e desbloqueado |  |
| Novo segredo / Substituir segredo | Preencha de forma privada para configurar ou substituir. O sistema não exibe o segredo atual. Configurar mantém aberto; Configurar e bloquear fecha a Engenharia protegida. | Usar um segredo próprio da aplicação |  |
| Bloquear agora | Usa o segredo já configurado e passa para a superfície restrita. Não envia comando de parada ao Runtime. | Após configurar o segredo |  |
| Segredo do Engineering Lock / Desbloquear | Informe o segredo da aplicação para reabrir o conteúdo. Saber a senha do usuário não substitui esse segredo; desbloquear não concede permissões ausentes. | Preencher sem compartilhar em mensagens |  |
| Remover segredo | Remove a proteção configurada pelo fluxo permitido. Não é redefinir senha do usuário nem apagar o projeto. | Somente com decisão de retirar proteção |  |

### Procedimento e persistência após reinício

1. Com acesso autorizado, abra o controle do cadeado.
2. Defina o segredo privado e selecione Configurar para continuar trabalhando ou Configurar e bloquear para restringir imediatamente.
3. Confira o estado retornado pelo servidor.
4. Trate a mudança como alteração de Working: Salvar → Publicar → Ativar é necessário para que a nova proteção pertença à revisão ativa durável após reinício. Não suponha que clicar Configurar sozinho concluiu esse ciclo.
5. Quando precisar editar, use Desbloquear com o segredo correto e as permissões apropriadas.
6. Confira o conteúdo acessível e mantenha a proteção pretendida na revisão que publicar. Se a resposta for recusada, leia o motivo; não limpe usuários, banco ou Authority para tentar desbloquear.

[Abrir este guia na Ajuda instalada](/help?topic=security.lock-workflow)
