# SCADIVO — Fundamentos para começar

## 1. Conheça o caminho de uma aplicação

O SCADIVO permite montar telas para acompanhar e operar uma aplicação residencial, predial ou industrial. Pense numa bomba: você quer ver a pressão, consultar o histórico e oferecer uma ação ao operador autorizado. Cada parte tem uma função, e você pode construí-las aos poucos.

Em **Engenharia (Engineering)**, você prepara conexões, TAGs, telas e regras. No **Runtime**, a pessoa usa a versão colocada em operação. Alterar um desenho em Engenharia não muda imediatamente a tela que já está operando.

Não é preciso conhecer todos os termos para começar. Faça primeiro um exemplo pequeno e use o glossário da seção 5 quando encontrar uma palavra nova.

## 2. Faça uma primeira leitura aparecer

1. Siga [Primeiro projeto](30-PRIMEIRO-PROJETO.md) para preparar uma aplicação de teste e uma leitura conhecida em Server Memory, a memória do servidor, sem depender de equipamento físico.
2. O **TAG** identifica essa leitura no projeto. Confira seu valor, qualidade e horário antes de desenhar; [Engenharia e dados](03-ENGINEERING-E-DADOS.md) explica a origem e o endereço.
3. Em [Telas, Popups e objetos](05-TELAS-E-OBJETOS.md), insira um valor exibido e selecione o TAG pelo vínculo de dados. Um Popup é uma janela de detalhes que pode ser aberta sobre a tela.
4. Confira a Pré-visualização e aplique a edição ao projeto quando esse fluxo for oferecido. Corrija os diagnósticos antes de continuar.
5. Salve o trabalho, publique e ative a revisão em etapas separadas, como explicado abaixo.
6. No Runtime, confira a revisão ativa e a leitura esperada. Acrescente alarmes, histórico ou Scripts depois de entender esse primeiro caminho.

## 3. Saiba o que cada parte guarda

| Parte | Para que serve | Exemplo do que conferir |
| --- | --- | --- |
| Application / aplicação | Guarda a engenharia do projeto: TAGs, telas, Scripts e outros objetos portáteis. | Qual projeto e revisão estão sendo editados ou usados? |
| Security Authority | Mantém contas, senhas, papéis e decisões de acesso da instalação. | Esta pessoa pode apenas visualizar ou também executar a ação? |
| License / licença | Define recursos e limites comerciais disponíveis. | A instalação tem licença compatível com os recursos usados? |
| Historian | Grava e consulta amostras quando a captura foi configurada. | O período procurado tem dados gravados e qualidade adequada? |

Uma licença não concede uma permissão de segurança. O servidor decide se a ação é autorizada, mesmo que um botão apareça no navegador. A classe da sessão e a licença podem restringir recursos; não concedem uma permissão ausente na Authority.

O pacote `.escadapkg` transporta a aplicação; não é backup de usuários/Authority, licença nem banco/histórico. Essas cópias têm procedimentos próprios em [Backup e recuperação](28-BACKUP-E-RECUPERACAO.md). Nos fluxos atuais de arquivo, a licença é vinculada à máquina de destino; confira sua situação em Licenciamento.

Telas, Scripts e relatórios usam os valores fornecidos pelo Runtime. Não crie uma conexão particular com um Driver dentro deles para contornar a fonte de dados, a autorização ou o ciclo do projeto. Conversão de escala e controle de acesso não passam a ter uma segunda versão no navegador.

## 4. Edite agora, coloque em operação quando estiver pronto

Antes de editar, confira o projeto, sua permissão de Engenharia e o controle de edição solicitado. O rascunho editável se chama **Working**. Uma cópia salva e identificada é uma **Revision**. **Published** indica uma revisão publicada; **Active** é a que o Runtime executa.

1. Abra Engenharia no projeto correto.
2. Edite o recurso no rascunho Working.
3. Valide e use Preview quando disponível. A prévia confere a proposta sem aplicá-la.
4. Use Apply quando houver essa etapa para incorporar a edição validada ao projeto. Depois salve o trabalho com Save/Salvar.
5. Crie ou selecione a revisão correspondente e confira suas mudanças.
6. Publique a revisão pronta com Publish/Publicar.
7. Use Activate/Ativar quando a mudança puder entrar em operação. Confira a revisão informada como ativa pelo servidor e o resultado no Runtime.

**Resultado esperado:** a revisão ativa anterior continua em uso até a ativação explícita. Salvar não publica; publicar não ativa. Apply de um editor ou de uma importação também não equivale a ativar.

Se uma mudança parecer ter aparecido antes de ativar, confira qual revisão o servidor informa como Active e qual área você está vendo. A aparência local do editor não comprova uma mudança na operação.

## 5. Consulte os termos quando precisar

| Termo usado no produto | Significado na sua tarefa |
| --- | --- |
| Engineering / Engenharia | Área onde você monta e modifica a aplicação. |
| Runtime | Área que apresenta e executa a revisão em operação. |
| Working | Rascunho atual do projeto; pode conter mudanças ainda não salvas. |
| Revision | Cópia identificada do trabalho salvo. |
| Published / Active | Publicada está preparada para ativação; ativa é a revisão em uso pelo Runtime. |
| Data Source / fonte de dados | Configuração da origem de um valor, como uma conexão com equipamento ou memória interna. |
| Driver | Recurso que comunica com um protocolo de equipamento. Memória interna é outra categoria de origem. |
| Server Memory | Memória do servidor que mantém valores retentivos; uma tela pode consultá-los por TAGs. |
| Client Memory | Estado local deste cliente Runtime; pode ser perdido ao terminar o contexto e não é compartilhado automaticamente com outros clientes. |
| TAG | Variável identificada do projeto, com tipo e origem. Um valor atual não é todo o seu histórico. |
| Binding / vínculo | Ligação entre uma propriedade e um recurso, como o valor mostrado e seu TAG. |
| Quality / qualidade | Informação sobre a condição da leitura. Falta de dados ou qualidade ruim não significa valor zero. |
| Timestamp / horário da amostra | Instante associado à leitura; ajuda a perceber se ela está atualizada. |
| Script | Regra escrita para executar uma tarefa, no servidor ou no cliente conforme o tipo escolhido. |
| Handler | Função declarada de um Script que recebe um evento; o nome deve corresponder à função que você preparou. |
| Template Faceplate | Desenho compartilhado que pode ser associado a equipamentos. Mudar a definição pode afetar as instâncias vinculadas. |
| Dínamo | Composição visual reutilizável, com definição compartilhada e valores próprios por instância quando parametrizada. |
| Popup | Janela de detalhes aberta sobre uma tela, com objetos e vínculos próprios. |
| Equipamento | Identidade de um ativo, como Bomba 01. É separada dos TAGs e do desenho que o representa. |
| Preview / Apply | Prévia para conferir a proposta; aplicação do conteúdo validado ao projeto. Nenhum dos dois é Activate. |
| Authority | Cadastro e regras que permitem ao servidor decidir quem pode fazer o quê. |
| Papel / role | Conjunto de permissões atribuído a pessoas. Confira os recursos e ações efetivamente permitidos. |
| Capability / permissão | Ação permitida; escrever um valor e executar um comando são ações distintas. |
| Scope / escopo | Recurso ou parte da aplicação a que uma permissão se aplica. |
| Engineering Lock / segredo da aplicação | Proteção opcional do conteúdo de Engenharia. Seu segredo difere da senha de login e da senha do backup da Authority. |
| Reserva de edição | Controle de quem pode editar no momento, quando exigido. Não substitui as permissões da conta. |
| Historian | Gravação e consulta de amostras; precisa de captura e dados disponíveis. |
| License / licença | Condições comerciais de uso e capacidade, separadas da conta e de seus papéis. |

## 6. Continue pela tarefa que precisa fazer

Use o [índice em português](README.md) para encontrar os capítulos por tarefa. Na Ajuda instalada, pesquise pelo nome da função, do campo ou do comando. As referências detalhadas explicam as opções depois do procedimento inicial.

Se um valor não aparecer, confira fonte, TAG, vínculo e revisão ativa. Se uma ação for recusada, leia o retorno e confira a sessão e a permissão; não repita um comando de resultado incerto sem verificar seu estado. Se faltar histórico, confira a captura e o período, não apenas o valor atual.

**Ajuda relacionada:** `/help?topic=getting-started.first-project`, `/help?topic=engineering.lifecycle`, `/help?topic=bindings-commands.overview`, `/help?topic=recovery.backup-system-recovery`.
