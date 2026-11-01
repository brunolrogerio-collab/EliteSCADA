# Segurança e auditoria: contas, permissões e proteção

## Usuários locais: criar, atribuir papéis e manter acesso

Cadastre quem entra no sistema e diferencie conta, papel, sessão e licença.

### Antes de administrar contas

Abra Engenharia > Segurança para cadastrar quem entra nesta instalação. As contas locais, senhas e permissões ficam na Security Authority, o cadastro de segurança da instalação. Elas não são objetos do projeto e não viajam no pacote de engenharia. Conta local também não é conta do portal comercial.

Para administrar usuários, é preciso permissão UserRoleAdmin ou SystemAdmin. Editar o projeto não concede esse acesso. Antes de mudar papéis ou senha, confira qual conta está usando e preserve pelo menos um administrador local habilitado.

### Prazo da sessão local

Em Segurança, ajuste o tempo para pedir login novamente. Ele começa quando a pessoa entra e define a validade máxima do login local; não é um temporizador de inatividade. Fechar o navegador não encerra a sessão. O novo prazo vale para próximos logins, enquanto sessões já abertas mantêm o vencimento que receberam. Escolha de 5 a 1.440 minutos (5 minutos a 24 horas); o valor inicial do produto é 480 minutos, ou 8 horas, salvo se a instalação definir outro valor inicial na configuração do servidor.

Se o prazo vencer com a aplicação aberta, ela não mostra o login automaticamente. O servidor deixa de aceitar ações protegidas e encerra a atualização em tempo real. A tela pode continuar exibindo os últimos valores recebidos. Atualize a página ou abra a aplicação novamente para voltar ao login. Na troca de turno, use Sair antes de entregar a estação e peça ao próximo operador para entrar com a própria conta.

### Procedimento: cadastrar e testar um operador

1. Confira os papéis disponíveis e o que cada um permite fazer e em quais recursos. Se faltar um papel adequado, prepare e aplique a política primeiro.
2. Use Novo usuário e preencha login, nome, senha privada e habilitação.
3. Selecione apenas os papéis necessários e crie a conta.
4. Ao editar depois, revise o resumo e confirme o salvamento. Mudar um campo não salva sozinho.
5. Em outra sessão, entre com a conta criada e teste uma leitura permitida e uma ação que deve ser negada. A licença e a classe View Only/Interactive continuam limitando as ações.
6. Volte à conta administrativa para corrigir concessões se necessário. Uma mesma sessão não comprova o acesso de dois usuários diferentes.

### Campos da conta

Use Novo usuário para cadastrar uma identidade. Para uma conta existente, selecione-a na lista; o nome de login é mostrado para identificação e não é renomeado por esse formulário. O campo de senha é usado na criação ou no formulário separado de redefinição, sem exibir a senha atual.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Usuário / login | Nome utilizado para entrar. Não confunda com o nome legível mostrado ao operador. | operacao.turno1 | 3–200 caracteres; nome único. |
| Nome de exibição | Identifica a pessoa de forma legível. Alterá-lo não troca o login nem os papéis. | Operação — Turno 1 | 1–300 caracteres. |
| Senha / Nova senha | Crie uma senha individual e siga a política aceita pelo servidor. Redefinir é substituir, não recuperar a anterior. Nunca copie senha para mensagens, scripts ou pacotes. | Preencher de forma privada | O formulário exige pelo menos 8 caracteres; o servidor valida a política. |
| Habilitado | Permite o uso da conta. Desabilitar não apaga a identidade nem o histórico de ações. Alterações podem invalidar a sessão; tenha outra conta administrativa disponível. | Marcado |  |
| Papéis | Marque papéis existentes cujas permissões correspondem ao trabalho. As concessões dos papéis atribuídos são combinadas; adicionar um papel não retira permissões de outro. Nomear um papel developer ou administrator não cria privilégios por si só. | Operador da Estação 01 | Confira os grants efetivos, não só o nome. |

### Redefinição, busca e problemas comuns

Busca filtra nome/login/papel e o filtro de estado separa habilitados e desabilitados; esconder uma linha não desabilita a conta. Para redefinir senha, selecione a conta, use Nova senha, revise o impacto e confirme. Alterar conta/papéis ou senha pode encerrar sessões; entre novamente quando solicitado. O sistema rejeita remover o último acesso administrativo local habilitado e atribuir papéis inexistentes. Se a tela disser 401, renove o login; 403 indica falta de permissão. Uma revisão de projeto não transporta usuários, e importar um .escadapkg não é restauração da Authority.

[Abrir este guia na Ajuda instalada](/help?topic=security.local-accounts)

## Papéis e escopos: conceder a ação no recurso certo

Monte concessões explícitas, confira o alcance e aplique a política validada da Authority.

### Conta, papel, capability e escopo

Pense primeiro em quem pode fazer o quê e onde. A conta identifica a pessoa. O papel reúne permissões que você atribui à conta. Cada concessão liga uma ação permitida a todos os recursos ou a um alcance específico. Na interface, ação pode aparecer como capability e alcance como escopo.

Abra Engenharia > Segurança e o painel de papéis/capabilities/escopos. A política pertence à Authority da instalação, não ao desenho nem ao pacote do projeto. O nome de um papel não concede acesso sozinho.

Para consultar, é necessário UserRoleAdmin. Para validar/aplicar, é necessário EngineeringModify junto de UserRoleAdmin ou SystemAdmin. Confira essas permissões antes de editar a política.

### Procedimento: preparar e aplicar uma política

1. Carregue a política atual e confira quem continuará administrando a instalação.
2. Crie um papel ou selecione um existente. Antes de mudar um papel, confira quais usuários já o recebem.
3. Adicione uma concessão para cada ação necessária. Escolha global ou o nó correto da hierarquia. Se incluir descendentes, confira quais recursos passarão a ser alcançados.
4. Para Intelligence, adicione AssetProcessIntelligenceManage explicitamente: global ou Equipment/Location com vínculo válido. SystemAdmin sozinho não substitui essa concessão na configuração das ferramentas.
5. Execute Preview da política, corrija erros e aplique somente o candidato validado. Uma nova edição invalida a prévia anterior.
6. Atribua o papel à conta e teste, com ela, acesso permitido e negado.
7. Se outro administrador alterou a política, recarregue e refaça sobre a versão atual. Não force uma versão antiga nem apague usuários para contornar conflito.

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

### Prévia, sessão e recusas

A prévia da conta selecionada mostra o que os papéis do rascunho parecem conceder. Ela ajuda a conferir a edição, mas não decide se uma ação será permitida num recurso concreto. As capabilities efetivas mostradas pertencem à sua sessão atual, não à pessoa selecionada.

A decisão final é do servidor, considerando pessoa, recurso e alcance. No Runtime entram também classe de sessão e licença. A política tem versão e Preview/Apply próprios da Authority; exportar ou ativar projeto não a substitui.

O produto recusa excluir papel ainda atribuído e perder o último administrador. Se um nó não existe ou é incompatível, escolha um vínculo válido. Não aumente para global apenas para eliminar o erro.

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

Use o Engineering Lock para proteger o conteúdo de engenharia da aplicação. O segredo permite desbloquear essa proteção, mas não concede permissões ausentes. Ele é diferente da reserva de edição usada quando alguém está trabalhando no projeto.

Guarde cada segredo pela sua finalidade: a senha da conta identifica a pessoa; o segredo do Lock protege a aplicação; a senha do backup da Authority protege esse arquivo. Não escreva esses valores em telas, Scripts ou Help. Administração, importação/exportação, restauração e desbloqueio continuam exigindo suas permissões normais, mesmo com a aplicação bloqueada.

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
2. Defina o segredo privado. Use Configurar para continuar trabalhando ou Configurar e bloquear para restringir imediatamente.
3. Confira a resposta e o estado retornado pelo servidor.
4. Complete Salvar → Publicar → Ativar. A configuração é alteração do rascunho Working; esse ciclo leva a proteção à revisão ativa que deve permanecer após reinício. Clicar Configurar sozinho não conclui essa etapa.
5. Para editar depois, use Desbloquear com segredo correto e permissões apropriadas.
6. Confira o conteúdo disponível e a proteção da revisão que publicará. Se houver recusa, leia o motivo. Não limpe usuários, banco ou Authority para tentar desbloquear.

[Abrir este guia na Ajuda instalada](/help?topic=security.lock-workflow)
