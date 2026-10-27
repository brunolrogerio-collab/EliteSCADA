# Sessões Runtime e diagnóstico

## Sessão Runtime: visualizar, interagir e encerrar

Entenda quando você pode apenas acompanhar a aplicação e quando pode operar seus controles.

### Visualizar ou operar

Para acompanhar valores e estados, escolha Visualização. Para usar controles que comandam o processo, solicite Interativa. A escolha é um pedido ao servidor: confira sempre o campo Concedida para saber o que ficou disponível. Sua conta também precisa ter permissão para a ação. Mesmo com sessão Interativa, uma pessoa não ganha acesso a um equipamento que não lhe foi autorizado.

### Conferir sua sessão

1. Entre na aplicação e abra o Runtime.
2. Abra o menu da sua conta no cabeçalho. Procure Classe da sessão Runtime.
3. Escolha Visualização para acompanhar ou Interativa se precisar operar.
4. Aguarde a resposta. Veja Solicitada, que mostra seu pedido, e Concedida, que mostra a resposta efetiva.
5. Se pediu Interativa e recebeu Visualização, leia o aviso antes de tentar um comando. Pode não haver capacidade interativa disponível. Peça ao responsável para conferir o motivo quando necessário.

### Exemplo: você vê a bomba, mas não consegue comandá-la

Primeiro olhe Concedida. Se aparece Visualização, a sessão permite acompanhar, mas não operar. Se aparece Interativa, confira a permissão da sua conta para aquela bomba e para aquele comando. Se sessão e permissão estão corretas, verifique a mensagem do comando e o diagnóstico da comunicação. Vá por partes: mudar a classe da sessão não corrige uma ligação de TAG ou uma falha do equipamento.

### Trocar de classe ou encerrar

Para mudar, escolha a outra classe no mesmo painel. A sessão anterior é liberada antes de pedir a nova. Se o pedido falhar, pode ser necessário solicitar uma sessão novamente; a anterior não é conservada automaticamente. Para terminar, clique em Encerrar sessão e espere a confirmação. Fechar o menu só esconde o painel. Sair da conta é outra ação. Se a conexão falhar, confira com o responsável se a sessão foi realmente liberada; fechar a janela não é uma confirmação.

### Consultar os controles e os avisos

Os resultados são preenchidos pelo servidor. Você não precisa digitar um número ou ID de sessão. Abra os detalhes técnicos apenas quando precisar explicar uma recusa ao responsável.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Visualização | Solicita uma sessão para acompanhar a aplicação, sem comandos ou escritas operacionais. | Acompanhar estado da bomba | Não é login anônimo nem altera as permissões da conta. |
| Interativa | Solicita capacidade interativa ao servidor. Confira a classe concedida antes de operar. | Operar com conta autorizada | Pode ser recusada ou receber Visualização como alternativa; não garante a ação. |
| Solicitada | Mostra qual classe foi pedida pelo cliente. | Interativa | Não comprova a classe efetiva. |
| Concedida | Mostra a classe efetiva da sessão admitida. É o resultado a conferir. | Visualização | Se difere de Solicitada, leia o aviso; não trate fallback como sucesso interativo. |
| Motivo de admissão | Detalhe técnico da decisão do servidor. Abra Detalhes técnicos quando precisar explicar a admissão. | Código retornado pelo servidor | Código é diagnóstico, não um campo para configurar ou contornar a decisão. |
| Motivo de capacidade | Ajuda a explicar disponibilidade/limites de sessões. Consulte o licenciamento e as sessões em uso quando houver recusa. | Capacidade interativa indisponível | Capacidade não equivale a permissão da Authority. |
| Encerrar sessão | Libera a sessão Runtime atual após confirmação do servidor. | Sem sessão Runtime ativa | Não é Sair da conta; fechar o menu também não encerra a sessão. |
| Erro ou aviso | Leia a mensagem da solicitação, encerramento ou redução de classe antes de repetir. | Solicitação recusada | Uma falha de rede não deve ser interpretada como liberação ou concessão confirmada. |

### Quando a sessão é recusada

Anote a mensagem, o horário e a ação que tentou fazer. Uma mudança de permissões ou uma restauração pode exigir que você entre e solicite a sessão novamente. Não copie IDs ou cabeçalhos de outra sessão para tentar obter acesso. Nos detalhes de permissão, ProcessValueWrite significa escrita de valor e CommandExecute significa execução de comando. São autorizações diferentes. A licença limita os recursos disponíveis; as permissões da conta definem quais ações você pode usar.

[Abrir este guia na Ajuda instalada](/help?topic=runtime.session-classes)

## Diagnóstico: localizar a primeira causa da falha

Interprete saúde do host, comunicação, rede e qualidade do TAG antes de alterar uma tela ou um driver.

### Escolha a camada certa

Uma tela parada pode resultar de sessão, revisão ativa, comunicação, TAG ou apresentação. Diagnóstico reúne evidências; abrir o painel não reinicia a fonte nem muda o processo. No Engineering, abra Diagnóstico de comunicação. Ele observa Runtime / Active Revision: uma fonte criada só no Working ainda não precisa aparecer. Comece pelo host API/DriverHost; depois examine a fonte, rede/protocolo, amostra e tela. A falha mais próxima da origem deve ser tratada antes de trocar a cor ou colocar um valor fixo na interface.

### Conferir uma fonte passo a passo

1. Anote nome do TAG/fonte, horário, usuário e ação que falhou; confirme a revisão ativa.
2. Confira o sinal Host API / DriverHost e Observado. Se estiver indisponível ou desatualizado, os estados antigos das fontes não são confirmação atual.
3. Limpe a busca ou use parte do nome da fonte, driver ou endpoint. Em Filtro, comece por Todos ou Requer atenção.
4. Selecione a fonte na lista. Leia estado, Último erro, último sucesso/falha e idade dos dados.
5. Compare qualidade e contadores em duas observações; um total acumulado não informa quando ocorreu cada falha.
6. Use alcance de rede, Connection Test ou Test Read apenas no contexto disponível e necessário.
7. Volte ao TAG e confirme valor, qualidade, horário e atualização; só então confira a dinâmica da tela.

### Busca, atualização e confiança da observação

Os filtros mudam somente a lista apresentada. Parar a atualização do painel não desativa o driver nem muda a aquisição.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Buscar Data Source, driver ou endpoint | Filtra por parte do nome/chave da fonte, tipo de driver ou endpoint. | Parte do nome da bomba | Busca não cria fonte; limpe-a se a lista parecer vazia. |
| Filtro | Todos mostra a lista disponível; Requer atenção inclui estados que não são saudáveis; Somente saudáveis reduz aos confirmados como saudáveis. | Requer atenção | Sem resultado pode ser apenas filtro. Fonte não ativa é diferente de fonte filtrada. |
| Atualização automática | Atualiza as observações do painel a cada 3 segundos enquanto habilitada. | Habilitada durante o diagnóstico | Não altera scan do driver; não é controle de retenção ou aquisição. |
| Atualizar agora | Solicita uma nova leitura do diagnóstico. Aguarde terminar antes de comparar. | Duas observações consecutivas | Atualização do painel não reinicia nem reconecta manualmente o driver. |
| Host API / DriverHost — Observado | Confirme se a saúde do processo que fornece os dados ainda é recente. | Horário da observação | Host desatualizado/indisponível faz o painel tratar o estado das fontes como não confirmado. |
| Totais do painel | Fontes, saudáveis, atenção, falha e TAGs Good dão uma visão geral antes de selecionar uma fonte. | 2 fontes, 1 exige atenção | Contagens não substituem leitura de cada erro nem comprovam saúde de todos os pontos. |

### Ler o estado operacional e os contadores

Ausência de valor aparece como traço em diversos indicadores; não interprete o traço como zero ou como sucesso. Instância Runtime distingue execuções: ao reiniciar ou recriar, uma comparação de totais pode deixar de representar o mesmo intervalo. Ciclos contam execuções de aquisição; requests são solicitações de protocolo. Sucessos/falhas contam resultados de operações, e falhas consecutivas destacam a sequência recente sem recuperação. Timeouts são operações que não concluíram no prazo. Conexões, desconexões e reconexões ajudam a observar estabilidade do enlace. Leituras/escritas contam operações, não necessariamente TAGs distintos. Atualizações publicadas indicam valores encaminhados ao Runtime, não persistência garantida no Historian. Compare cada indicador com o driver e o mesmo período/instância; nem todo protocolo conta tudo da mesma forma.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Estado | Saudável, degradado, reconectando, falha ou parado orientam onde aprofundar; leia também Último erro. | Reconectando | Um endpoint acessível não substitui o estado do driver. |
| Endpoint | Mostra o destino associado à fonte selecionada. Confira se é o equipamento esperado. | Destino configurado | Pode ser serial ou ponte local; nem todo endpoint é um host/porta TCP. |
| Último sucesso | Indica quando houve comunicação bem-sucedida. Compare com agora e com a cadência esperada. | Sucesso há poucos segundos | Sucesso antigo não prova que o ponto está atual. |
| Última falha | Ajuda a situar temporalmente uma falha informada. Compare com sucesso posterior. | Falha anterior ao último sucesso | Não informa sozinho se a falha continua. |
| Mudança de estado | Indica quando o estado informado foi alterado. | Passou a reconectar | Não é necessariamente o horário de uma amostra de processo. |
| Idade dos dados | Ajuda a detectar falta de atualização. Compare com o comportamento e scan esperados da fonte. | Idade aumentando | Nem toda fonte é cíclica; verifique o modo de aquisição antes de concluir defeito. |
| Scan configurado | Mostra a cadência configurada quando existe. | Intervalo do driver | Não se edita o scan neste painel; atualizações por evento podem ter outra dinâmica. |
| Latência e último ciclo | Latência média/última e duração do último ciclo ajudam a separar lentidão de ausência de atualização. | Duração maior que o habitual | Duração de operação não é atraso completo entre sensor e pixel da tela. |
| Taxa recente de falha | Observe a proporção recente de operações que falharam. | Tendência aumentando | Não é indicador de eficiência do equipamento nem percentual de TAGs ruins. |
| Atividade e contadores | Compare ciclos, requests, sucessos, falhas consecutivas, timeouts, conexões/reconexões, leituras, escritas e atualizações publicadas. | Falhas aumentam entre observações | Totais acumulados e mudanças de instância exigem contexto; não zere o sistema para interpretar. |
| Qualidade das TAGs | Separe Good, BadCommunication, Outras e Sem amostra. Confira o TAG específico mesmo que outros estejam Good. | 1 ponto sem amostra | Sem amostra não é valor zero; qualidade Good não comprova que a física do sensor está correta. |
| Detalhes do protocolo | Leia os detalhes que esse driver realmente fornece e leve a mensagem exata ao responsável. | Erro específico do protocolo | A ausência de detalhes extras não autoriza inventar um diagnóstico; campos variam por driver. |

### Testar alcance da rede

A seção Alcance da rede (origem: servidor) faz uma tentativa TCP e uma tentativa ICMP para um destino. A origem é o host do servidor, não seu navegador; localhost/127.0.0.1 aponta para esse ambiente, inclusive o contêiner quando aplicável. Teste apenas o destino necessário e autorizado. O painel usa timeout de 3 segundos por tentativa; TCP e ICMP são resultados separados e a operação total pode levar mais tempo. A ação exige permissão de modificar Engineering e pode ser limitada por frequência.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host ou IP | Informe um nome DNS ou IP, sem protocolo, caminho, senha ou faixa de endereços. | 127.0.0.1 em um ensaio local | Não usar http://, URLs ou varredura. Confira se está testando a origem correta. |
| Porta única | Informe a porta TCP do serviço esperado. Altera só o alvo do teste, não a configuração do produto. | 502 para um serviço Modbus TCP conhecido | Inteiro de 1 a 65535; não é teste UDP nem serial. |
| TCP | Connected significa que uma conexão TCP foi aceita; Refused indica recusa; Timeout indica ausência de conclusão no prazo. | Connected | Não faz autenticação, handshake do protocolo ou leitura de registrador. |
| ICMP | Success confirma resposta ao teste ICMP. Timeout/Unavailable pode ocorrer por rede ou política do host. | Unavailable com TCP Connected | ICMP bloqueado não prova indisponibilidade TCP; examine cada resultado. |
| Duração e horário do teste | Use para situar a observação e repetir de forma dirigida após corrigir a causa. | Resultado observado agora | Não é garantia de disponibilidade futura nem latência de processo de ponta a ponta. |

### Exemplo de interpretação

Uma fonte Modbus mostra timeouts e um TAG sem amostra. TCP Connected com ICMP Unavailable comprova apenas que a porta aceitou conexão; não comprova unidade Modbus, registrador ou valor. Confira host/porta/unidade, permissões do destino e vínculo do ponto. Se o host API/DriverHost estiver desatualizado, recupere essa observação antes de afirmar que o driver está saudável. Depois de corrigir, compare novas amostras e o retorno do TAG. O trecho abaixo é a requisição técnica de um ensaio local, não um script de varredura nem uma alteração de porta do produto.

```json
{
  "host": "127.0.0.1",
  "port": 502,
  "timeoutMilliseconds": 3000
}
```

### Outras falhas e evidência para suporte

Para permissão negada, confira conta e ação exata; não troque o usuário para mascarar. Para revisão divergente, compare Working, Published e Active. Para erro de Script, leia o diagnóstico do Script e os tipos/valores usados; rede saudável não valida sintaxe. Para uma tela diferente do TAG, confira binding, escala, unidade e qualidade. Registre horário, projeto/revisão ativa, fonte/TAG, mensagem, duas observações e passos para reproduzir. Não inclua senha, token ou chave em prints e chamados. A coleta é evidência do sintoma, não uma causa confirmada.

[Abrir este guia na Ajuda instalada](/help?topic=diagnostics.overview)
