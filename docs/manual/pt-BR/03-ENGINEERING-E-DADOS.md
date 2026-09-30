# Engineering, Data Sources e TAGs

Este capítulo-base cobre as partes estáveis das Partes III e IV. Superfícies de UI que ainda serão fechadas visualmente não recebem screenshots finais nesta fase.

## Working, Save, Revision, Publish e Activate

Working é editável. Save persiste trabalho sem convertê-lo silenciosamente em Published ou Active. Uma Revision deve representar um estado identificável validado. Publish publica a revisão; Activate é a transição explícita que altera a autoridade Active do backend.

**Ajuda relacionada:** `/help?topic=engineering.lifecycle`.

## Data Sources e Drivers

Crie Data Sources usando os tipos registrados no catálogo do produto. Campos, formatos, limites, defaults e referências protegidas vêm do schema do tipo selecionado.

Drivers de comunicação são distintos de source providers internos. Simulation é ferramenta de desenvolvimento/teste e não é contado como Driver de produção no catálogo de Help.

### Procedimento: configurar uma origem sem inventar parâmetros

**Objetivo:** criar/editar um Data Source usando somente o contrato real do tipo selecionado.

**Pré-requisitos:** acesso de Engineering e tipo de Data Source disponível no catálogo.

**Passos:**
1. Selecione o tipo de Data Source.
2. Preencha somente os campos expostos pelo editor/schema daquele tipo.
3. Use exemplos e formatos fornecidos pela própria superfície quando disponíveis.
4. Execute Connection Test, Discover, Browse, Import ou Reconcile apenas se o tipo declarar essa capability.
5. Corrija erros no Data Source antes de mascará-los no TAG ou na apresentação.

**Resultado esperado:** a configuração permanece compatível com o schema registrado.

**Troubleshooting:** não transplante sintaxe de outro protocolo. Uma rejeição do backend deve ser corrigida na configuração de origem.

**Ajuda relacionada:** `/help?topic=sources.data-sources` e tópicos `driver.<type-key>`.

## TAGs

TAGs usam identidade estável e binding compatível com o Data Source. Valores, quality e timestamps chegam pelo pipeline de Runtime sob autoridade do backend.

### Endereçamento

Use somente campos e formatos expostos pelo TagBinding schema do Driver. Endereços de protocolos diferentes não são intercambiáveis.

**Ajuda relacionada:** `/help?topic=tags.addressing`.

### Scaling / normalization

Scaling altera o valor de engenharia antes do consumo operacional e pode afetar lógica, alarmes, histórico e telas. Formatação visual controla somente apresentação. Não use formatação para simular scaling.

**Ajuda relacionada:** `/help?topic=tags.scaling-formatting`.

### Quality e timestamps

Quality representa confiança/estado da amostra e deve permanecer fiel ao pipeline. Falha, ausência ou dado stale não deve ser apresentado como Good apenas para estabilizar a UI.

Preserve timestamps provenientes da fonte/Runtime conforme o contrato; não substitua timestamp industrial pelo relógio do navegador para persistência.

**Ajuda relacionada:** `/help?topic=tags.quality`, `/help?topic=tags.timestamps`.

## Escrita e comandos

Um controle visível não torna um TAG gravável. Escrita depende do contrato da fonte/TAG e das capabilities efetivas, com enforcement no servidor. Sessão View Only ou ausência de capability não recebe autoridade de escrita.

**Ajuda relacionada:** `/help?topic=tags.writeability`, `/help?topic=bindings-commands.overview`.

## Teste/commissioning de TAG

A interface final de commissioning permanece sujeita à revisão visual final, mas a sequência de diagnóstico é estável:

1. confirme backend e autenticação;
2. confirme a revisão Active quando a leitura for operacional;
3. valide Data Source;
4. valide configuração/conexão do Driver;
5. valide binding/endereço;
6. observe quality e timestamp;
7. somente depois investigue apresentação.

## Copy / Paste / Duplicate

A duplicação estável copia configuração de Engineering e cria nova identidade estável. Dados transitórios de Runtime, como valor atual, quality, timestamp, histórico, alarm history e diagnósticos, não pertencem à cópia.

### Procedimento: duplicar TAGs com Preview

**Objetivo:** criar cópias sem colisão e sem mutar Working antes da validação.

**Pré-requisitos:** um ou mais TAGs selecionados.

**Passos:**
1. Selecione um TAG, ou ative a seleção múltipla.
2. Use Copy/Paste ou Duplicate.
3. Revise os drafts gerados.
4. Corrija colisões de ID, path e nome.
5. Execute Preview.
6. Aplique somente o candidato validado; se Working tiver mudado, repita Preview.

**Resultado esperado:** novos TAGs recebem IDs próprios e Working só é mutado no Apply validado.

**Troubleshooting:** colisões bloqueiam Preview. Não reaproveite identidade estável do TAG de origem.

## Sequential TAG generation

A geração sequencial atual aceita de 1 a 200 TAGs por operação e exige patterns de nome/path contendo `{n}`. O passo numérico de suffix e o passo de endereço devem ser inteiros diferentes de zero.

A geração automática de endereço nesta superfície é específica para endereços Modbus canônicos `coil`, `discrete`, `holding` ou `input`, com referência entre 0 e 65535. A validação detecta colisão de endereço no mesmo Data Source antes do Preview.

### Procedimento: gerar uma sequência Modbus

**Pré-requisitos:** exatamente um TAG de origem ligado a Data Source `modbus.tcp` e endereço canônico.

**Passos:**
1. Abra a geração sequencial no TAG selecionado.
2. Defina quantidade, patterns de nome/path, início/passo do sufixo e passo do endereço.
3. Gere os drafts.
4. Revise nome, path e endereço de cada item.
5. Resolva todas as colisões.
6. Execute Preview.
7. Aplique o candidato validado.

**Resultado esperado:** os TAGs são criados somente após Preview/Apply, com novas identidades e endereços derivados do padrão configurado.

**Limite:** não converta automaticamente endereços opacos de outros protocolos em sequência Modbus.

## Historian profile

A associação de TAG a perfil de captura é configuração de Engineering e deve preservar a referência estável do perfil. A política completa de captura permanece dependente do fechamento do workstream de Historian; não extrapole comportamento ainda não integrado.
