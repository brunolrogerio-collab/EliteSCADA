# Matter Wi-Fi: versão em teste

## Matter Wi-Fi: preparar e entender a versão em teste

Verifique a disponibilidade, prepare a fonte e diferencie a leitura de um atributo já comissionado de parear um dispositivo.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-MATTER: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.matter-test-workflow; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-792-MATTER: https://github.com/brunolrogerio-collab/EliteSCADA/issues/792; topic=drivers.matter-test-workflow; fields=address,settings; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Comece pela disponibilidade

Antes de configurar, confirme se Matter Wi-Fi aparece na sua instalação. O SCADIVO é multiplataforma, mas este conector é uma prévia isolada para host Linux x64, com processo auxiliar local, chamado sidecar. Não é uma liberação de Matter para toda instalação.

A composição padrão não oferece o tipo. Ele só aparece quando o responsável habilita a prévia em Development/Test, sem HA. Se não estiver em Fontes de dados, confira a elegibilidade com esse responsável; não escolha outro driver para imitá-lo. O Help continua disponível para explicar esses limites.

### O que você pode preparar

A versão atual trabalha com atributos persistentes de dispositivos Matter Wi-Fi já comissionados. Comissionar significa incluir o dispositivo na rede lógica Matter, chamada fabric. Criar a fonte ou sua chave não realiza esse pareamento. Não há assistente de comissionamento, Thread, administração de dispositivos, Browse, Discover ou Testar conexão nesse conector. Ler e escrever atributos não representa suporte a todo comando de todo equipamento. Preserve a indicação de versão em teste; a qualificação futura não está concluída.

### Antes de abrir o editor

Peça ao responsável os números que identificam o ponto: nó, endpoint, cluster e atributo, além do tipo real do valor. Eles não são o ID interno do TAG.

Nó identifica o dispositivo na rede lógica Matter, chamada fabric. Endpoint identifica uma função, como uma saída. Cluster reúne funções e atributos de um serviço, como On/Off. Atributo é um valor desse grupo, como ligado/desligado. Um dispositivo pode ter vários endpoints; confira seus números em vez de copiar os de outra unidade.

No host, precisam estar preparados o sidecar e a rede IPv6. A dependência declarada requer Node.js 22.13+ ou 24+. Uma rede de contêiner pode ter condições diferentes da máquina. Fabric e armazenamento criptografado ficam no host: importar .escadapkg não os transfere nem recupera chave perdida. Este roteiro não pede chaves ou credenciais nos campos do projeto.

### Preparar a fonte e a chave do host

1. No ambiente isolado elegível, abra Engineering → Fontes de dados e crie uma fonte do tipo Matter Wi-Fi. Dê um nome legível.
2. Confira as alterações e aplique a fonte ao Workspace; ainda não é publicação/ativação.
3. Selecione a fonte aplicada. No painel da chave Matter, use Criar chave no host e confirme a operação. Se houver edição pendente, aplique-a primeiro: o botão fica indisponível enquanto a fonte não corresponde ao estado aplicado.
4. Aguarde a resposta e a atualização da página. O resultado esperado é a indicação de chave configurada; depois o botão permite Verificar chave do host.
5. Continue a preparação dos TAGs, revise, Salve, Publique e Ative somente o candidato validado. Criar/verificar a chave não contata dispositivos e não comprova comunicação. A operação exige permissão de modificar Engineering e a versão atual do Workspace.

### Entenda os campos e referências

A fonte não recebe host, porta ou ajustes públicos genéricos de protocolo. A referência protegida é preparada pela ação do host. Os demais itens abaixo explicam o vínculo de um TAG; o exemplo técnico não é um pacote completo nem um formulário de importação.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Chave de armazenamento do fabric | Protege o armazenamento local Matter. Use a ação do host; não digite a chave, uma senha ou um caminho. | Chave configurada | Referência do host; o segredo não é exibido ou exportado. |
| Fonte do TAG | Associe o ponto à fonte Matter que foi preparada. O nome da fonte não descobre um equipamento. | matter.lab | A associação deve identificar a mesma fonte do vínculo. |
| Tipo do TAG | Escolha o tipo do atributo real; Boolean serve para um estado ligado/desligado. | Boolean | Boolean, Int16, Int32, Int64, Float, Double ou String; não suponha suporte a outros tipos. |
| Somente leitura | Comece com leitura para conferir valor e qualidade antes de testar atuação. | true | Permitir escrita no TAG não concede autorização ao usuário nem torna o atributo gravável. |
| Endereço do atributo | Reúna os quatro números reais no formato mostrado; não use endereço IP ou código de pareamento. | node/123/endpoint/1/cluster/6/attribute/0 | Números decimais sem sinal/zeros à esquerda; node de 1 até UInt64 máximo, endpoint UInt16, cluster/attribute UInt32. |
| Vínculo de comunicação | Declara a identidade e a versão da representação que o Runtime entende. Digitar só o endereço não cria esse objeto. | Vínculo Matter v1 | O assistente genérico atual não monta esse vínculo quando o catálogo não declara campos de ponto; não há passo gráfico completo prometido aqui. |
| Versão do contrato | Identifica o formato do objeto de vínculo, não a versão comercial do Matter. | 1 | O contrato atual do vínculo é 1. |
| Identificação do esquema | Identifica a interpretação Matter do endereço; fica na referência técnica. | elitescada.driver.matter.wifi | Não substituir pelo esquema de configuração da fonte. |
| Versão do esquema | Diz como interpretar o conteúdo do vínculo. | 1 | O planner atual aceita o esquema Matter v1. |
| Endereço no vínculo | Repete a identidade completa do atributo dentro do vínculo. | node/123/endpoint/1/cluster/6/attribute/0 | Se Address estiver preenchido, deve ser idêntico a PortableAddress. |
| Ajustes extras | Não acrescente configurações genéricas para tentar corrigir falha do vínculo. | Sem ajustes extras | Settings de fonte/ponto, selectors e transformação não identidade são rejeitados nesta versão. |
| Quantidade de pontos | Mantenha somente os TAGs persistentes que deseja ensaiar nessa fonte. | 1 TAG de leitura | De 1 a 512 TAGs associados, com identidades internas válidas; os números de nó não substituem esses IDs. |

### Exemplo de um atributo ligado/desligado

O exemplo representa apenas parte da configuração para discutir identidade e validar o contrato. O nó 123 é fictício; não garante que exista na sua rede. Cluster 6/atributo 0 representa um estado On/Off, quando o dispositivo oferece esse atributo no endpoint 1. O ambiente controlado deve fornecer os IDs internos e a referência preparada pelo host. Não cole este trecho como .escadapkg nem acrescente uma chave. A aceitação pelo planner verifica a configuração, não a conexão, o valor real ou a qualidade. Hoje o campo Address isolado e a ação de chave não completam a autoria do vínculo na interface; um candidato técnico precisa ser preparado/validado no ensaio controlado.

```json
{
  "source": {
    "key": "matter.lab",
    "driver": "matter.wifi"
  },
  "tag": {
    "path": "Lab.Lamp.On",
    "dataType": "Boolean",
    "readOnly": true,
    "address": "node/123/endpoint/1/cluster/6/attribute/0",
    "communicationBinding": {
      "contractVersion": 1,
      "schemaId": "elitescada.driver.matter.wifi",
      "schemaVersion": 1,
      "portableAddress": "node/123/endpoint/1/cluster/6/attribute/0"
    }
  }
}
```

### Leitura, escrita e segurança operacional

Depois de ativar uma configuração válida no ensaio, confira Diagnóstico e o TAG no Runtime: valor, qualidade e horário da amostra. Zero/false exibido sem amostra válida não comprova que a lâmpada está desligada. Antes de qualquer escrita, confirme que o atributo permite escrita e que o TAG, a Authority e a classe da sessão permitem a ação. Escrever valor de atributo usa ProcessValueWrite; não converta isso em CommandExecute nem prometa comandos de dispositivo não implementados. Confira o resultado efetivo e a leitura de retorno. Um botão de tela e a criação de uma chave não confirmam atuação.

### Investigue pelo sintoma

Tipo ausente: confirme a disponibilidade da prévia e a ausência de HA. Chave ausente: aplique a fonte e use a ação do host; trocar uma referência no JSON não cria a chave. Botão indisponível: há alterações não aplicadas ou uma operação em andamento. Vínculo obrigatório: o endereço foi preenchido sem CommunicationBinding. Endereço inválido: confira ordem, números, limites e ausência de zeros à esquerda. Esquema incompatível: confira esquema Matter e versão 1. Ativação recusada em HA: essa versão não suporta esse modo; não contorne a validação. Configuração aceita mas sem amostras: confira sidecar, armazenamento do host, rede e atributos já comissionados. Nunca recrie a chave como primeira tentativa de recuperar um fabric existente. Perda de chave/estado exige recuperação planejada com o responsável; o arquivo de engenharia não restaura esses segredos.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.matter-test-workflow)
