# Primeiro projeto: da entrada ao Runtime

## Primeiro projeto: da entrada à primeira leitura

Siga um pequeno ensaio de engenharia até conferir uma amostra conhecida no Runtime, sem conectar um equipamento.

### Prepare o ambiente do ensaio

Use uma instalação de teste destinada a este exercício e uma conta autorizada. Confirme o endereço e o projeto antes de editar; não use uma planta em operação para aprender a ativar revisões. Este exercício mostra uma leitura em memória interna, não um sensor real. Se já existe uma aplicação, não faça detach para forçar a tela inicial: use o projeto de teste autorizado ou peça ao responsável para preparar o ambiente.

### Entrar e criar o projeto

1. Abra o endereço correto. Em uma instalação nova elegível, conclua Criar Administrador inicial; se está recuperando dados existentes, use Restaurar backup. Não suponha login/senha padrão.
2. Entre com a conta autorizada.
3. Se Criar novo projeto for oferecido, informe Chave do projeto e Nome do projeto. Escolha Criar projeto e abrir Engineering.
4. Confirme o projeto aberto. Se a opção não aparece, confira se já há projeto e siga o fluxo correspondente. Criar o projeto não equivale a uma nova aplicação Active. Se outro usuário criou um projeto enquanto o formulário estava aberto, recarregue e confira o estado em vez de repetir por tentativa.

### Campos para este exercício

Os nomes abaixo são exemplos de um projeto de teste. Escolha referências legíveis; IDs internos são selecionados/gerados pelos fluxos do produto.

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

### Produzir uma amostra conhecida

1. Em Fontes de dados/TAGs, prepare Server Memory e a TAG Double Demo.Level. Aplique as alterações válidas ao Working.
2. Crie um Server Script em Python, habilitado, e declare a referência Demo.Level como ServerMemoryTag.
3. Cadastre Initialize com handler initialize_demo.
4. Use o código abaixo e valide. A função publica 42.0 com qualidade Good porque é um valor conhecido deste ensaio; isso não mede equipamento. Não é uma atualização contínua: Initialize publica quando a entrada é executada na inicialização da revisão. Para atualização periódica, estude Timer e cadência no guia de Scripts, sem acrescentar um laço infinito.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Montar a primeira tela

1. Abra Telas e crie uma tela para o dispositivo pretendido.
2. Adicione Texto com o título Ensaio de nível.
3. Adicione Exibição de valor e ajuste posição, tamanho e casas decimais no Inspetor.
4. No vínculo de dados, escolha Demo.Level. Use um texto de unidade apenas se ele corresponder ao significado do valor; neste exercício pode deixar sem unidade.
5. Use Pré-visualização, leia diagnósticos e aplique a edição.
6. Configure a navegação/modo de apresentação para abrir essa tela conforme o fluxo disponível. Não adicione comando de escrita para testar uma leitura.

### Salvar, publicar, ativar e conferir

Abra o lifecycle/revisões do projeto e confira alterações e validação. Salvar cria uma revisão persistida; selecione a revisão correta e Publique. Publicar prepara a revisão, sem torná-la automaticamente Active. Ative a publicada quando quiser executar este candidato no ambiente de teste. Confira o resultado e a revisão Active. Em TAG Monitor, selecione Demo.Level e espere a amostra 42 com qualidade Good; confira os horários e a origem de memória interna. Abra a tela pelo Runtime e compare. Se a tela mostra 42 fixo mas o TAG não tem amostra, o ensaio não passou. A edição aplicada ao Working, sozinha, não comprova execução.

### Se o resultado não aparecer

Confirme o projeto e a revisão Active antes de mexer no desenho. Sem amostra: confira fonte/TAG, referência declarada, Script habilitado, Initialize/handler e diagnóstico do Script. TAG válido mas tela vazia: confira tela aberta, vínculo, objeto e formatação. Sessão Visualização basta para observar; uma ação interativa requer classe/permissão próprias. Se Salvar/Publicar/Ativar falhar, leia a mensagem e confira o estado atual antes de repetir; uma etapa concluída não implica as seguintes. Para um dispositivo físico, substitua o ensaio pelo guia do driver correspondente e valide valor/qualidade com uma fonte independente. Não trate esse exemplo como teste de campo.

[Abrir este guia na Ajuda instalada](/help?topic=getting-started.first-project)
