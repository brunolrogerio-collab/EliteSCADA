# EliteSCADA

Plataforma SCADA / Supervisório Industrial.

## Princípios do projeto

- arquitetura modular e evolutiva;
- entidades de engenharia serializáveis e versionáveis;
- Engineering Import/Export como recurso transversal desde as fases iniciais;
- segredos e credenciais nunca armazenados em texto aberto nos arquivos de engenharia;
- testes automatizados e integração contínua desde o início.

## Engineering Import/Export

O projeto deverá permitir importação e exportação pública, sem dependência da interface gráfica, das principais entidades de engenharia, incluindo:

- TAGs;
- alarmes;
- Data Sources e Drivers;
- equipamentos;
- templates e dínamos;
- telas e popups;
- bindings, propriedades e metadados relacionados.

Configurações técnicas podem ser exportadas, mas credenciais, senhas, tokens e chaves devem ser referenciados por mecanismos seguros separados.

## Revisões de engenharia e runtime

A persistência de engenharia distingue três estados deliberadamente independentes:

- **Working Revision**: última revisão salva, ainda em trabalho;
- **Published Revision**: revisão aprovada para poder ser ativada;
- **Active Revision**: revisão que foi efetivamente validada e aplicada ao runtime de comunicação.

Publicar uma revisão não altera automaticamente o processo em execução. A ativação monta um runtime candidato isolado, inicia os drivers, exige qualidade `Good` para as TAGs de comunicação e somente confirma a troca após a persistência aceitar o novo ponteiro `ActiveRevision`. Uma falha de compilação, comunicação ou persistência mantém o runtime anterior.

A API operacional (`/api/tags`, `/api/alarms`, `/api/drivers` e escrita de TAGs) utiliza o runtime realmente ativo. A simulação embutida funciona como fallback de desenvolvimento enquanto não existe uma revisão industrial ativa.

### Projeto vinculado ao processo

Uma instância do runtime hospeda um projeto persistido por vez. Configure a chave do projeto antes de usar a ativação persistida:

```text
EngineeringRuntime__ProjectKey=plant-a
```

Com PostgreSQL habilitado, a conexão continua sendo fornecida por:

```text
ConnectionStrings__EliteScada=Host=...;Database=...;Username=...;Password=...
```

Opcionalmente, o tempo máximo de preparação do candidato pode ser ajustado em segundos:

```text
EngineeringRuntime__ActivationTimeoutSeconds=10
```

No reinício do serviço, o EliteSCADA recupera exatamente a revisão registrada como **Active**, mesmo que uma revisão mais nova já esteja **Published** aguardando ativação. Se houver uma Active Revision persistida e ela não puder ser recuperada, o processo falha de forma fechada em vez de iniciar silenciosamente com valores simulados.

A consistência entre o estado durável e o processo vivo pode ser consultada em:

```text
GET /api/engineering/persistence/{projectKey}/runtime
```

A ativação explícita da revisão publicada é feita por:

```text
POST /api/engineering/persistence/{projectKey}/published/activate
```

## CI

O workflow `.github/workflows/dotnet-ci.yml` executa automaticamente restore, build, testes, smoke test do runtime e validação end-to-end em Chromium.
