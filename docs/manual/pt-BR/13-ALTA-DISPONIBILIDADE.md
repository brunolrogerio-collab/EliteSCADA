# Alta disponibilidade: configuração e operação

## HA: configurar dois servidores e acompanhar a operação

Distinga configuração salva, servidor ativo, parceiro pronto e proteção contra execução duplicada.

### O que preparar antes

HA significa Alta Disponibilidade. Dois servidores são preparados para que o Runtime possa passar de um para o outro com proteção contra execução duplicada. Neste guia, nó significa cada um desses servidores.

Abra Engenharia > Alta disponibilidade. Para observar, é preciso licença HA Runtime e permissão para acompanhar os servidores. Para salvar ou operar, é necessária também a permissão de transferência. Confira rede entre serviços, autenticação, relógios e armazenamento dos dois nós.

O banco pode ser compartilhado ou separado, conforme a instalação preparada. Habilitar HA não cria sozinho replicação de dados nem garante que um banco esteja disponível. Confira essa parte com o responsável pela instalação antes de configurar a dupla.

### Preencha somente o que depende da instalação

A tela chama os dois nós de Este servidor e Servidor parceiro. Comece pelos endereços que permitem comunicação entre seus serviços. Use os seletores para o servidor preferencial e o fluxo protegido para autenticação. O produto cuida das identidades internas e do transporte automático; não invente esses valores. A tabela detalha as escolhas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço principal de cada servidor | Endpoint alcançável pelos serviços. O endereço detectado no navegador só serve se o parceiro também o alcançar; localhost em dois hosts não é o mesmo servidor. | Endereços reais dos dois serviços |  |
| Endereço externo alternativo | Abra somente quando a rede exigir endereço anunciado diferente. Sem necessidade, mantenha o automático para evitar destinos inconsistentes. | Sem override numa rede simples |  |
| Servidor preferencial | Preferência para início/failback; não promove um nó à força e não garante que ele esteja pronto. | Este servidor ou parceiro |  |
| Autenticação entre servidores | Configure o segredo compartilhado pelo fluxo protegido de ambos os nós. O valor enviado é limpo da tela; confira o estado de autenticação, sem copiar o segredo para capturas. | Configurar autenticação |  |
| Proteção e referência compartilhada | É a referência usada pela proteção contra dois servidores Active ao mesmo tempo, chamada fencing. Ambos precisam enxergar o mesmo armazenamento externo compartilhado. Dois arquivos locais com nomes iguais não formam uma referência comum. | Recurso compartilhado provisionado pela instalação |  |
| Failover automático | Permite a transição automática elegível e exige proteção habilitada. A opção não dispensa prontidão do parceiro, dados disponíveis e condições de segurança. | Habilitar após conferir prontidão |  |

### Procedimento de preparação

1. Confira o modo Em execução antes de editar. Ele informa a configuração que o serviço está usando agora.
2. Prepare HA, informe os endereços dos dois serviços e escolha o servidor preferencial.
3. Configure autenticação e proteção compartilhada. Resolva os itens indicados em Prontidão para HA.
4. Use Salvar configuração desejada. Isso guarda a escolha, mas não prova que ela já está Em execução.
5. Se houver reinício pendente, planeje-o e depois confira a configuração realmente carregada nos serviços.
6. Atualize o estado. Confira quem está Active efetivo, a prontidão do parceiro, o banco e a proteção. Standby sozinho não significa Ready Standby.
7. Antes de usar no processo, faça o ensaio manual acordado com o responsável pela instalação. As verificações deste guia não substituem essa avaliação.

### Leia os estados antes de operar

Active efetivo é o servidor autorizado a executar agora. Ele pode não ser o preferencial. O parceiro pode estar apenas em Standby, aguardando, ou em Ready Standby, quando a prontidão foi avaliada pelo servidor. Leia essa diferença antes de transferir.

Synchronizing indica sincronização ainda em curso. Promoting e Demoting são transições: aguarde o resultado. Stale avisa que a observação é antiga. Isolated, Faulted, estado degradado ou autoridade ambígua pedem diagnóstico.

Confira o banco e a comunicação com o parceiro separadamente. Alcançar o parceiro não prova que o banco funciona. Aumentar tolerâncias de rede para esconder uma falha de banco não resolve essa falha.

### Escolha a operação certa

Escolha a ação conforme a situação. Switchover é a transferência planejada do Active efetivo para um parceiro elegível. Failback solicita retorno ao preferencial quando permitido. Recovery serve para reconciliar uma falha ou bloqueio.

Forçar ressincronização tem uma condição específica: não pode haver Active e a autoridade anterior precisa estar cercada, isto é, impedida de continuar executando. A ação não declara um parceiro pronto por decisão do operador.

Antes de confirmar, leia ação e alvo. Depois, confira o resultado, o histórico e quem ficou Active. Se a solicitação for recusada, investigue a causa antes de tentar novamente.

### Ajustes avançados: significado e limites

Mantenha inicialmente os tempos preenchidos. Eles controlam por quanto tempo uma informação é recente e como a proteção reage a falhas.

Ajuste apenas depois de avaliar rede, relógios e operação. O fato de um número caber no limite do formulário não o torna uma recomendação para sua instalação. A tabela explica o efeito de cada tempo.

Para conferir as permissões na Segurança: HighAvailabilityObserve permite observar; HighAvailabilityTransfer é exigida também para salvar e operar. Esses são os identificadores de referência para o administrador.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Freshness (s) | Tempo máximo para tratar uma observação como recente. Depois disso ela pode estar antiga demais para orientar a operação; não é o intervalo de aquisição de um TAG. | Manter valor preenchido | 1–120 s |
| Lease (s) | Tempo de validade da autorização temporária para o servidor executar como Active. Ao expirar, essa autoridade deixa de valer. Lease não é licença comercial. | Manter valor preenchido | 3–120 s |
| Poll (ms) | Tempo entre consultas à referência compartilhada. Um valor menor faz consultar com mais frequência. O campo usa milissegundos, não segundos. | Manter valor preenchido | 100–10000 ms |
| Witness máximo (s) | Idade máxima da evidência de parceiro pronto. Não cria prontidão por si só. | Manter valor preenchido | Pelo menos Lease; até 600 s |
| Margem de clock (s) | Margem para diferença entre relógios. Não substitui sincronização de tempo. | Manter valor preenchido | 0 até piso(Lease/3) |

### Se houver problema

Se a resposta for 403, confira permissão para observar ou operar. Se Salvo for diferente de Em execução, confira se falta reiniciar. Para parceiro indisponível, examine endereços, rede e autenticação entre os serviços.

Se a referência de proteção estiver indisponível, investigue o armazenamento externo compartilhado. Se o parceiro não estiver pronto, leia sincronização, banco e diagnóstico; não force promoção para contornar a condição.

Investigue um problema por vez. Registre horário, ação, resultado e servidor efetivo para suporte, sem incluir o segredo de autenticação.

[Abrir este guia na Ajuda instalada](/help?topic=ha.configuration-operation)
