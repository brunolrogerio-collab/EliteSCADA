# Primeiro projeto: da entrada ao Runtime

## Primeiro projeto: da entrada à primeira leitura

Crie uma variável de teste, mostre o valor 42 em uma tela e confira se a aplicação está funcionando.

### O que você vai montar

Neste exercício, uma pequena função coloca o número 42 em uma variável chamada Demo.Level. Uma tela mostra esse valor. Assim, você aprende o caminho completo sem ligar um equipamento. Use uma instalação de teste e uma conta autorizada. Se já houver uma aplicação em operação, confirme o ambiente com o responsável antes de editar.

### Entrar e abrir o projeto de teste

1. Abra o endereço fornecido para a instalação de teste.
2. Se for o primeiro acesso, siga a criação do Administrador inicial. Se estiver recuperando uma aplicação existente, use Restaurar backup. Não há uma senha padrão a presumir.
3. Entre com sua conta. Se aparecer Criar novo projeto, informe a chave planta-piloto e o nome Planta piloto. A chave identifica o projeto; o nome é o título que você verá na tela.
4. Clique em Criar projeto e abrir Engineering. Confira o nome do projeto aberto. Se a instalação já tiver um projeto ou informar que outro usuário acabou de criá-lo, recarregue e confira a situação; não apague dados para fazer a opção aparecer.

### Colocar um valor na variável

1. Em Fontes de dados, prepare uma fonte de Memória do Servidor. Ela guarda o valor no servidor, sem endereço de equipamento.
2. Em TAGs, crie Demo.Level, do tipo Double, e associe à fonte preparada. Double permite usar um número com casas decimais. Aplique as alterações conforme os controles do editor.
3. Em Scripts, crie um Script Python com execução no servidor: escolha Server e habilite o Script. Declare Demo.Level nas referências como ServerMemoryTag, ou seja, um TAG de memória do servidor.
4. Nas entradas do Script, escolha Initialize e informe initialize_demo como nome da função. Initialize executa na inicialização da revisão.
5. Cole o exemplo e valide. Quando a função rodar, publicará 42.0 com qualidade Good, que indica uma amostra considerada válida neste exercício. Ela publica uma vez na inicialização; não produz atualização contínua nem leitura de sensor.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Mostrar o valor na tela

1. Em Telas, crie uma tela para o dispositivo que usará no teste.
2. Adicione um Texto com o título Ensaio de nível.
3. Adicione uma Exibição de valor. Selecione o objeto e ajuste tamanho, posição e casas decimais no Inspetor.
4. No vínculo de dados do objeto, escolha Demo.Level. Esse vínculo faz o objeto acompanhar a variável; deixar 42 como texto fixo não testa a leitura.
5. Abra a Pré-visualização, confira os avisos e aplique a edição. Prepare a navegação ou a forma de abertura da tela conforme o modo de apresentação usado. Não é preciso criar um comando para esse exercício.

### Colocar a versão pronta em execução

Enquanto você edita, o trabalho fica no rascunho chamado Working. Salvar cria uma revisão, que é uma cópia identificada do trabalho. Publicar prepara uma revisão para uso. Ativar coloca a revisão publicada em execução; essa passa a ser a Active. No lifecycle/revisões do projeto, siga Salvar, selecione a revisão correta, Publique e então Ative no ambiente de teste. Aguarde as confirmações. Abra TAG Monitor e confira a revisão ativa, a TAG Demo.Level e a amostra 42 com qualidade Good. Depois abra a tela no Runtime e compare o valor. Se o objeto mostra um 42 fixo e o TAG está sem amostra, o exercício ainda não passou.

### Consulte os campos se precisar

Você pode seguir o exercício e voltar a esta tabela quando surgir uma dúvida. Os nomes técnicos servem para identificar campos; os IDs internos são gerados ou escolhidos pelos próprios seletores do sistema.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Chave do projeto | Identificação estável do projeto, distinta do título de apresentação. | planta-piloto | De 1 a 200 caracteres após ajuste de espaços nas pontas; não é chave de licença nem um ID a inventar por tentativa. |
| Nome do projeto | Nome legível apresentado na engenharia. | Planta piloto | De 1 a 300 caracteres após ajuste de espaços nas pontas; confirme que abriu o destino certo. |
| Fonte da TAG | Para este ensaio, selecione/crie uma fonte Server Memory pelo catálogo, sem host ou porta. | Memória do Servidor | Provider interno, não driver de equipamento nem memória do navegador. |
| Path da TAG | Crie a referência de engenharia usada pelo Script e pela tela. | Demo.Level | Associe à fonte de memória preparada; caminho sozinho não produz amostras. |
| Tipo | Use um tipo numérico compatível com a amostra deste exercício. | Double | Não usar String para esconder incompatibilidade de valor. |
| Scope do Script | Escolha Server para publicar a amostra no servidor. | Server | Client Visual executa no cliente e não substitui esse publicador. |
| Gatilho e handler | Cadastre uma entrada Initialize com o nome exato da função definida abaixo. | Initialize / initialize_demo | O nome da função sozinho não configura o gatilho; habilite o Script e a entrada conforme o editor. |
| Referência declarada do Script | Declare Demo.Level como ServerMemoryTag pelo fluxo de referências do Script. | Demo.Level | A API exige memória do servidor declarada; texto no código não substitui configuração válida. |
| Vínculo da Exibição de valor | Selecione a TAG Demo.Level no seletor de dados do objeto. | Valor de Demo.Level | Não digite GUID nem deixe 42 como valor fixo fingindo uma amostra. |
| Tela | Crie uma tela simples com título e Exibição de valor; configure o modo de abertura/navegação disponível. | Tela de ensaio | Criar uma tela não garante que ela abrirá sozinha no Runtime. |

### Se algo não aparecer

Comece pelo TAG Monitor. Se Demo.Level não tem amostra, confira a fonte, o Script habilitado, a referência declarada e a entrada Initialize com a função initialize_demo. Leia o diagnóstico do Script. Se o TAG está correto, mas a tela não, confira se abriu a tela certa e se o objeto está ligado à TAG. Se uma ação de Salvar, Publicar ou Ativar foi recusada, leia a mensagem e confira o estado antes de repetir. Não é necessário pedir uma sessão Interativa só para observar a leitura. Para conectar um equipamento real depois, use o guia do driver correspondente e verifique a leitura com uma fonte independente.

[Abrir este guia na Ajuda instalada](/help?topic=getting-started.first-project)
