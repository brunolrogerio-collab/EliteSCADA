# Telas, Popups e objetos

Uma tela reúne o desenho do processo, suas leituras e as ações do operador. Um Popup é uma janela de apoio, útil para detalhes de um equipamento sem trocar a tela principal. Este capítulo monta um exemplo pequeno antes de apresentar os recursos de acabamento e reutilização.


Este capítulo começa com uma tela pequena: um título e uma leitura. Siga as seções 1 a 3 e confira o resultado antes de acrescentar interações ou reutilização. As seções seguintes servem para aprofundar conforme a necessidade.

**TAG** é a variável que fornece a leitura. **Estrutura** é a lista dos objetos do desenho. **Inspetor** é o painel onde você ajusta o objeto selecionado. Uma **propriedade** é um desses ajustes, como largura ou cor. Os controles têm nomes próprios, mas o percurso é sempre selecionar, ajustar e conferir o efeito.

## 1. Prepare um exemplo verificável

Use uma aplicação de teste com um TAG numérico de pressão, cuja unidade e valor atual você conheça. Nos exemplos, a leitura é `Plant.Pump.Pressure`, em bar. Esse nome é demonstrativo: selecione o TAG real da sua aplicação, sem criar um vínculo para uma referência inexistente. Para testar escrita, use uma variável de teste que aceite escrita; não utilize um comando de processo real para aprender o editor.

Entre em Engenharia no projeto correto e confirme que a edição está disponível para sua conta e que você possui o bloqueio de edição quando exigido. A edição visual tem duas etapas: validar/aplicar o desenho ao projeto e depois salvar/publicar/ativar uma revisão. Ver a mudança no editor não significa que o Runtime já a executa.

## 2. Encontre os controles do editor

| Região | Para que serve | Como começar |
| --- | --- | --- |
| Lista de Telas ou Popups | Escolher uma definição existente ou criar outra | Confira nome e projeto antes de editar. |
| Barra superior | Inserir formas, textos, valores, controles e navegadores; desfazer/refazer | Comece com um texto e um valor exibido. |
| Área de desenho | Posicionar, selecionar e redimensionar objetos | Se não conseguir selecionar um objeto, procure-o na Estrutura. |
| Barra junto ao desenho | Alinhar, distribuir, ajustar tamanhos, agrupar e bloquear; controlar zoom | Selecione os objetos que participarão da operação. |
| Estrutura / Biblioteca / Recursos | Encontrar objetos, inserir itens reutilizáveis e importar recursos visuais | Estrutura ajuda a selecionar itens sobrepostos. |
| Propriedades / Dinâmicas / Eventos | Configurar o objeto selecionado | Sem objeto selecionado, Propriedades mostra a superfície e Eventos pode mostrar eventos da tela. |
| Validação e aplicação | Conferir o candidato e aplicá-lo ao projeto | Leia os erros antes de aplicar. |

O painel muda conforme a seleção. Se você procura uma propriedade e ela desapareceu, confirme se selecionou um objeto, vários objetos ou o fundo. Alguns campos só fazem sentido para um tipo de objeto.

## 3. Construa a primeira tela

1. Abra **Telas** e crie uma definição. Use um nome de exibição como `Bombeamento`. O identificador é a referência estável da tela; ele não é seu título visual. Preserve-o depois que a tela passar a ser referenciada.
2. Sem objetos selecionados, confira as dimensões, a orientação e o fundo da superfície. As dimensões definem a área lógica do desenho; o zoom apenas amplia a visão no editor. Mudar orientação não desenha automaticamente uma versão alternativa para celular.
3. Insira um objeto de texto. Defina o conteúdo `Pressão de descarga`, escolha uma cor legível e posicione-o perto da leitura. Um texto fixo não precisa de TAG.
4. Insira um **Valor exibido**. Selecione-o e, em **Propriedades**, localize **Vincular valor à TAG / variável**. Escolha o TAG de pressão no seletor e aplique o vínculo. Nesse objeto, o destino do vínculo é o texto exibido.
5. Ative as casas decimais fixas e escolha `1` para apresentar décimos. Isso altera a apresentação, não a precisão do TAG. Acrescente uma legenda de unidade `bar`; não confunda o número com a unidade.
6. Posicione e dimensione os objetos. Use as ferramentas de alinhamento para organizar título e leitura. Execute **Preview da alteração** e examine os erros ou o candidato válido; então use **Aplicar ao Workspace**. Esses nomes podem aparecer como Pré-visualização/Aplicar no idioma selecionado.
7. Siga o fluxo de salvar, publicar e ativar descrito em [Engenharia e dados](03-ENGINEERING-E-DADOS.md). Configure a navegação para a tela se necessário; criá-la não a transforma automaticamente na tela inicial.
8. Abra a tela no Runtime e compare a leitura com o TAG. Varie o valor de teste e confira atualização, casas decimais e unidade. Se faltarem dados, consulte qualidade e disponibilidade; zero não prova que a comunicação está funcionando.

**Resultado esperado:** título fixo e leitura variável aparecem juntos, e o valor acompanha o TAG da revisão ativa. O título não muda quando o TAG muda.

## 4. Entenda as propriedades antes de preenchê-las

| Campo ou grupo | O que você está definindo | Exemplo e efeito |
| --- | --- | --- |
| Posição X/Y e largura/altura | Localização e tamanho no desenho | Aumentar a largura abre espaço para um valor longo; não muda a escala do processo. |
| Rotação | Orientação do objeto | Gire um símbolo quando a instalação exigir; confira também o texto e o espaço ocupado. |
| Visível | Se o objeto participa da apresentação | Útil com uma condição dinâmica; ocultar não desativa um equipamento. |
| Habilitado | Se o controle permite interação | Desabilitar o botão não concede nem revoga permissões do usuário. |
| Texto, fonte, tamanho e alinhamento | Conteúdo e legibilidade | Um título de seção e um valor operacional podem precisar de tamanhos diferentes. |
| Preenchimento, contorno e espessura | Aparência da forma | Use contraste e legenda, não apenas cor para estados importantes. |
| Mínimo, máximo, valor e unidade | Faixa e apresentação de um indicador ou controle | Uma régua de 0–10 bar deve receber pressão na mesma unidade. A faixa visual não converte automaticamente o TAG. |
| Casas decimais | Formatação da leitura | Uma casa mostra décimos; não melhora a exatidão do instrumento. |

**Um valor fixo**, **um vínculo** e **uma dinâmica** têm funções diferentes. O valor fixo é a configuração de partida. O vínculo fornece o valor de uma fonte, como um TAG. A dinâmica calcula ou escolhe uma apresentação conforme os dados. Quando uma propriedade muda sozinha, confira primeiro sua dinâmica ou vínculo antes de alterar repetidamente o valor fixo.

A Ajuda instalada oferece a referência individual de cada tipo de objeto, com os campos realmente admitidos, valores iniciais, unidades e limites. Abra **Ajuda → Telas e objetos** e escolha o tipo; a busca também encontra nomes de propriedades. Os identificadores técnicos ficam disponíveis como referência, sem exigir que você memorize todos eles.


Para conferir cor e transparência, teste primeiro com o fundo real da tela. Em uma cor escrita com oito dígitos, como `#00000066`, os dois últimos indicam a transparência, também chamada alfa. Ajustar esse efeito muda só a aparência. As opções e os limites da propriedade estão na referência do tipo de objeto na Ajuda.

## 5. Escolha o objeto pela função

| Necessidade | Objeto e configuração que merece atenção |
| --- | --- |
| Desenhar equipamento e tubulação | Retângulo, elipse, linha ou polígono: posição, tamanho, contorno/preenchimento; polígonos também têm pontos. Formas não controlam o processo por si mesmas. |
| Identificar ou mostrar uma leitura | Texto ou Valor exibido: texto/fonte/alinhamento e, para a leitura, vínculo e formatação. |
| Ver uma grandeza numa faixa | Mostrador, anel, barra graduada, termômetro, faixas ou real versus meta: valor, mínimo/máximo, unidade, limites/meta e variação de apresentação aplicável ao tipo. Escolha uma faixa que tenha significado operacional. |
| Alterar uma variável | Entrada numérica ou slider: vínculo de valor, faixa, passo e opção de permitir escrita. A direção de escrita do TAG, a autorização da conta e a sessão Runtime continuam necessárias. |
| Executar uma ação | Botão: legenda, habilitação e evento/ação. Uma legenda como “Ligar” não configura o comando. |
| Observar evolução | Tendência: adicione as séries e configure TAG, legenda, eixo e escala. A janela pertence ao gráfico; cada série tem sua própria configuração. |
| Consultar ocorrências | Navegador de alarmes ou eventos: escolha fonte/modo, período, filtros, colunas e ordenação. Reconhecer alarme não equivale a registrar evento. |
| Usar mídia ou documento | Imagem, símbolo SVG, modelo 3D, vídeo ou PDF: selecione o recurso admitido e configure ajuste/apresentação. Não coloque credenciais em URLs. |
| Abrir relatório | Lançador de relatório: selecione um relatório existente e os parâmetros disponíveis. Um objeto visual não cria a definição do relatório. |
| Mostrar Intelligence | Use a geração guiada para associar os objetos aos perfis/equipamentos e resultados existentes; veja [Inteligência de Ativos e Processos](12-INTELIGENCIA-ATIVOS-PROCESSOS.md). |

### Tendência: um exemplo de duas grandezas

Adicione uma série para pressão e outra para corrente. Dê legendas claras, como `Pressão de descarga` e `Corrente do motor`. Use um eixo para bar e outro para A: reunir unidades incompatíveis na mesma escala dificulta a interpretação. Diferencie cor e estilo. Uma escala fixa de 0–10 bar ajuda a comparar amplitudes; se o valor estiver fora dela, a curva pode ficar fora da área visível.

Os rótulos numéricos dos eixos ocupam no máximo sete caracteres, contando sinal e separador decimal, e mostram até duas casas decimais. O SCADIVO reduz as casas quando necessário (por exemplo, 9999,99; 99999,9; 9999999), usa o separador decimal do idioma e não agrupa milhares. Para valores muito grandes ou pequenos, apresenta notação científica compacta em vez de cortar dígitos. Isso muda apenas a leitura do eixo; não modifica os dados nem o histórico.

Confira modo ao vivo/histórico, janela, atualização e captura no Historian. Selecionar um período não cria amostras que nunca foram armazenadas. Comece sem filtros restritivos em navegadores e acrescente um filtro por vez; uma lista vazia pode ser consequência da configuração.

## 6. Adicione movimento e interação com intenção clara

Em **Dinâmicas**, faça o desenho acompanhar um valor. Por exemplo, um tanque pode mostrar metade de seu preenchimento quando a leitura for 50 numa escala de 0 a 100. Isso muda a apresentação; não escreve no TAG de origem.

Selecione um único objeto e confira as opções oferecidas para seu tipo. Uma condição booleana usa verdadeiro/falso para mudar uma propriedade, como visibilidade. Um mapa escolhe uma apresentação por regras, como cores por faixa de pressão; a primeira regra correspondente vence. O preenchimento analógico representa um nível entre os limites escolhidos.

Para aprender, use uma leitura de teste conhecida. Em um objeto que ofereça **Analog Fill**, escolha essa fonte numérica, informe escala 0–100 e ajuste cor/direção. **Clamp** limita a proporção visual a 0–100%, sem alterar a medição; **InvertScale** inverte essa proporção. Confira 0, 50 e 100, além de qualidade ruim e ausência de valor. Uma leitura indisponível não deve ser apresentada como nível zero ou equipamento parado. Valide/aplique ao projeto e salve/publique/ative antes de conferir o Runtime.

Em **Eventos**, escolha o que deve acontecer numa interação. Uma ação pronta pode abrir um Popup ao clicar; um Script executa lógica própria numa função chamada **handler**. Selecione o objeto e o destino da ação. O assistente de ação pronta usa **Click** e mantém uma ação por gatilho; adicionar outra nesse gatilho substitui a anterior. Para lógica maior, configure o handler Client e suas dependências conforme [Scripting](06-SCRIPTING-ALARMES-HISTORICO.md). A ação pronta e a associação de Script são configurações diferentes.

Para escrever numa variável de teste, selecione a ação de escrita e a referência real, respeitando tipo e faixa. Para executar um comando, selecione o comando real. `CommandExecute` e `ProcessValueWrite` são autorizações distintas: nenhuma propriedade visual concede essas capacidades. Após solicitar um comando, use o retorno e o estado real para indicar sucesso; trocar a cor do botão não confirma que o equipamento partiu.

### CheckBox: mostrar ou alterar um estado Booleano

1. Insira uma **Caixa de seleção** e escreva uma legenda clara, como `Bomba em automático`. A legenda é apenas o texto visto pelo operador; ela não define o TAG nem a ação.
2. Em **Dinâmicas**, vincule o estado `checked` a qualquer fonte booleana compatível escolhida no seletor, como TAG, expressão ou outra função que retorne verdadeiro/falso. Assim, a caixa pode indicar estados como automático/manual, habilitado/desabilitado ou uma condição própria da aplicação.
3. Para apenas observar, mantenha **Permitir ajuste pelo operador** desligado. Para deixar o operador mudar o estado, habilite essa opção e use vínculo com TAG Booleano gravável; a permissão da sessão e a autorização do servidor continuam valendo. O Runtime confirma a mudança pelo valor recebido. Qualidade ruim, TAG ausente ou escrita recusada não devem parecer sucesso.
4. A configuração **Supressão de um alarme** é um modo opcional e separado, não uma exigência do CheckBox genérico. Nesse modo, escolha a definição de alarme pelo nome e informe motivo e duração. O Runtime ainda verifica a autorização de supressão e mostra se a supressão é herdada de outra área. O estado é atualizado pelo Runtime, inclusive quando outro operador altera a supressão; se não for possível confirmar a leitura, o controle fica indisponível.

### ComboBox: escolher um valor de uma lista

1. Insira uma **Lista de seleção** e, em **Opções da lista**, acrescente cada valor que o TAG pode receber e o rótulo legível que o operador verá. Por exemplo, valor `AUTO` com rótulo `Automático`; os valores precisam ser distintos.
2. Em **Dinâmicas**, vincule `text` a um TAG do tipo String ou Enum. O valor atual do TAG precisa coincidir com uma das opções. O controle não escolhe outra opção quando o valor está ausente, a lista é inválida, a qualidade não é boa ou o tipo não corresponde.
3. Para permitir mudança, habilite **Permitir ajuste pelo operador** e use um TAG gravável. Teste primeiro com uma variável segura e confira o valor confirmado pelo processo; a seleção visual não comprova que o equipamento aceitou a mudança.

## 7. Crie e abra um Popup

1. Abra **Popups**, crie um e use um nome como `Detalhes da bomba`. Escolha dimensões adequadas ao dispositivo. Selecione um template existente somente se sua aplicação precisar dele; um Popup também pode ser independente.
2. Insira identificação e leituras usando os mesmos objetos da tela. Vincule os TAGs do equipamento correto. Valide e aplique o Popup ao projeto.
3. Volte à tela de origem e selecione um botão. Em **Eventos**, escolha **Open popup / Abrir Popup**, selecione o destino e adicione a ação. Os controles atuais podem mostrar o nome em inglês.
4. Valide e aplique a tela; salve, publique e ative a revisão que contém os dois recursos.
5. No Runtime, clique no botão, confira as leituras e feche o Popup. A tela principal deve continuar sendo o contexto da operação.

Se o Popup abrir com dados de outra bomba, corrija os vínculos ou parâmetros da instância. Mudar apenas o nome não muda a fonte dos dados. Um Popup aberto também não concede permissão para executar suas ações.

## 8. Organize e reutilize sem perder as referências

Use a Estrutura para selecionar objetos atrás de outros. Ordenação visual define o que aparece à frente; alinhamento posiciona bordas/centros; distribuição organiza espaçamentos; tamanho comum uniformiza dimensões. Agrupar facilita trabalhar com um conjunto, mas não cria um equipamento nem reúne suas permissões. Bloquear ajuda a evitar movimentos acidentais; não é um bloqueio de segurança.

Recursos visuais são arquivos importados, como uma imagem. Dínamos são desenhos reutilizáveis com objetos e parâmetros; uma instância pode expor parâmetros próprios. Depois de inserir um dínamo, confira parâmetros, dimensões, orientação e referências da instância. A aparência de uma bomba não escolhe automaticamente seus TAGs. Uma cópia de desenho também precisa ter seus vínculos revisados antes de representar outro equipamento.

## 9. Diagnostique pelo sintoma

| Sintoma | O que conferir primeiro |
| --- | --- |
| Propriedade não aparece | Tipo e quantidade de objetos selecionados; filtro do painel; se o campo é editável. |
| A leitura fica fixa | Vínculo aplicado, TAG real, disponibilidade/qualidade e revisão ativa. |
| Objeto muda apesar do valor fixo | Dinâmicas, vínculos e Script que atuam na mesma propriedade. |
| Botão não faz nada | Ação/evento configurado, destino existente, habilitação, revisão e autorização. |
| Escrita recusada | Tipo/faixa do valor, direção de escrita do TAG, permissão e classe de sessão Runtime. |
| CheckBox ou ComboBox não permite interação | **Permitir ajuste pelo operador**, direção de escrita, tipo do TAG, qualidade atual e autorização da sessão. |
| ComboBox não mostra o valor atual | Compare o valor recebido no TAG com os valores cadastrados; o rótulo visível é separado do valor gravado. |
| Objeto não aparece | Visibilidade, posição/tamanho, ordem de sobreposição, recurso visual e faixa/escala. |
| Aplicação rejeitada após validar | O projeto pode ter mudado; atualize, revise suas alterações e valide novamente. |
| Runtime mostra o desenho anterior | Salvar, publicar e ativar são passos distintos de aplicar o desenho. Confira a revisão ativa. |

## 10. Continue pela Ajuda

Tópicos instalados: `visual.first-screen`, `visual.object-selection`, `visual.properties`, `visual.dynamics`, `visual.events`, `visual.popup-workflow`, `visual.trend-series`, `visual.browser-filters`, `visual.object.core.checkbox`, `visual.object.core.combobox` e `libraries.reusable-resources`. Para abrir um diretamente, use `/help?topic=<identificador>` no produto. As capturas finais deste capítulo serão feitas na interface real após a auditoria visual; nenhuma imagem ilustrada substitui a interface aceita.
