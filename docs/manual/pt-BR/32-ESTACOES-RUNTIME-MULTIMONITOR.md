# Estações Runtime em vários monitores

## Estações Runtime: usar janelas em vários monitores

Configure uma tela por janela e organize cada janela no desktop estendido.

<!-- HELP-REVIEW-AFTER-PRODUCT-781: https://github.com/brunolrogerio-collab/EliteSCADA/issues/781; topic=runtime.station-multimonitor; fields=station.name,station.screen,station.mode,station.windows; status=WAITING_PREVIEW_ACCEPTANCE_AND_REAL_SCREENSHOTS -->

### O que uma estação organiza

Uma estação reúne de uma a quatro janelas independentes da mesma aplicação Runtime. Para cada janela, o perfil define qual tela abre primeiro e se a janela pode navegar ou fica presa a essa tela. O perfil não escolhe um monitor físico nem guarda posição, tamanho ou escala da janela. Depois de abrir, você distribui as janelas manualmente com os recursos do sistema operacional e do navegador. Isso também funciona quando o Runtime é acessado pela rede a partir de um computador com vários monitores.

### Configurar o perfil

1. Crie e aplique ao projeto as telas que pretende usar.
2. Abra Engenharia > Estações / Monitores e escolha Adicionar estação.
3. Dê um nome que ajude a identificar o posto, como Sala de controle EEE.
4. Em cada janela, escolha a tela inicial e o comportamento. Use Adicionar janela para incluir mais posições, até quatro; cada posição representa uma futura janela do Runtime.
5. Clique Validar e resolva qualquer erro mostrado.
6. Quando a configuração estiver válida, escolha Aplicar ao Working.
7. Salve e publique a revisão e ative-a pelo ciclo do projeto. Só depois a estação aparecerá na aplicação Runtime ativa.

### Entender os campos

O número da janela acompanha a ordem em que ela será aberta. Não é o número de um monitor. Os seletores usam as telas existentes no projeto; criar uma estação não cria nem publica as telas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome da estação | Identifica o conjunto no seletor do Runtime. Escolha um nome que a equipe reconheça; ele não identifica um computador ou monitor físico. | Sala de controle EEE | O perfil precisa ter ao menos uma janela. |
| Tela de cada janela | Escolhe qual tela existente será mostrada primeiro naquela janela. Configure cada linha separadamente para colocar informações diferentes lado a lado. | Tela 1: Visão geral; Tela 2: Bombas | A tela precisa existir na aplicação que será ativada. |
| Comportamento da janela | Navegação permite trocar de tela naquela janela. Tela fixa mantém a tela escolhida e solicita uma sessão de Visualização; isso não altera as permissões da conta. | Visão geral: Navegação; Bombas: Tela fixa | Cada janela autenticada conta como uma conexão Runtime independente. Os limites da licença e as permissões da conta continuam valendo. |
| Adicionar janela | Acrescenta mais uma tela ao conjunto. Cada linha vira uma janela independente quando a estação é iniciada. | Quatro janelas para quatro áreas de operação | O perfil aceita de uma a quatro janelas; a posição física em cada monitor é organizada manualmente. |

### Iniciar e distribuir as janelas

Abra o Runtime em uma janela normal, com o cabeçalho visível. Se estiver em tela cheia, saia dela primeiro. No cabeçalho, escolha a estação e clique Iniciar estação. O navegador pode bloquear novas janelas; nesse caso o Runtime mostra links para abri-las uma a uma. Coloque cada janela no monitor desejado arrastando-a. Ajuste o tamanho e, se for útil, coloque cada uma em tela cheia. O produto não detecta nem restaura automaticamente a posição física, a orientação ou a escala dos monitores.

### Permissões e conexões por janela

Uma janela fixa solicita uma sessão de Visualização; uma janela de navegação solicita uma sessão Interativa. O servidor define o nível de acesso de cada janela autenticada, considerando os limites da licença e as permissões da conta. Confira o resultado no menu da conta antes de enviar comandos. Se não houver vaga para uso interativo, a janela pode ficar somente para visualização ou ter o acesso recusado. A Visualização permite acompanhar a tela, mas não enviar comandos. Consulte também Sessão Runtime: visualizar, interagir e encerrar.

### Encerrar, reabrir e resolver problemas

Para encerrar a sessão e liberar uma vaga, abra o menu da conta em cada janela e escolha Encerrar sessão antes de fechá-la. Fechar a janela ou perder um monitor não avisa ao servidor que a conexão terminou. Se a janela fechar inesperadamente, a licença pode continuar contando essa conexão até a sessão expirar automaticamente. Para voltar, inicie a estação novamente e organize as janelas nos monitores; o produto não restaura as posições anteriores.

Se o seletor de estação não aparecer, confira se o cabeçalho está habilitado, se o Runtime não está em tela cheia e se a revisão em uso contém o perfil. Se uma janela abrir na tela errada, confira a tela selecionada naquela linha e ative a revisão atualizada. Se uma janela fixa não mudar de tela, ela está funcionando como foi configurada. Se o acesso interativo for limitado ou recusado, confira quantas conexões da licença estão em uso e as permissões da conta.

[Abrir este guia na Ajuda instalada](/help?topic=runtime.station-multimonitor)
