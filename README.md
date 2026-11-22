# EliteSCADA

Plataforma SCADA / Supervisório Industrial.

## Estado atual, colaboração e handoff

O ponto de entrada para continuar o projeto sem reconstruir o histórico é:

- [`PROJECT GOAL.md`](PROJECT%20GOAL.md) — norte estável de produto/arquitetura;
- [`LAST CHANGE.md`](LAST%20CHANGE.md) — resumo operacional mutável;
- [`docs/CHAT-COLLABORATION-PROTOCOL.md`](docs/CHAT-COLLABORATION-PROTOCOL.md) — **protocolo obrigatório para todos os chats/agentes do projeto**;
- [`docs/CURRENT-COORDINATOR-HANDOFF.md`](docs/CURRENT-COORDINATOR-HANDOFF.md) — ponteiro operacional conciso;
- [`docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`](docs/NEXT-COORDINATOR-CHAT-HANDOFF.md) — prompt permanente/state-independent para trocar o Main Coordinator;
- [`docs/README.md`](docs/README.md) — mapa de autoridade documental;
- handoff específico da Wave/etapa atual, quando existir.

### Regra global dos chats

Todo chat que trabalhe no EliteSCADA, não apenas o Main Coordinator, deve seguir `docs/CHAT-COLLABORATION-PROTOCOL.md`.

Em especial:

- toda interação visível ao usuário termina com a hora local atual de `America/Sao_Paulo`, no formato `Hora: HH:MM`;
- todo passo material de projeto deve ser persistido na superfície correta do repositório, issue ou PR;
- decisões/blockers/CI/integrations não podem existir apenas na memória do chat;
- chats paralelos trabalham sob coordenação e devem evitar sobrescrever estado compartilhado mais novo.

Para estado de execução, branch, SHA, PR, blocker e CI atuais, use os handoffs operacionais e **revalide GitHub live**.

SHAs escritos em documentos são snapshots. Antes de qualquer mutação, releia o head vivo do GitHub, os comentários mais recentes das issues/PRs e o Actions do SHA exato. Documentos/assignments históricos não substituem o estado atual.

## Princípios do projeto

- arquitetura modular e evolutiva;
- entidades de engenharia serializáveis e versionáveis;
- Engineering Import/Export como recurso transversal desde as fases iniciais;
- segredos e credenciais nunca armazenados em texto aberto nos arquivos de engenharia;
- testes automatizados e integração contínua desde o início;
- stable IDs e contratos públicos versionados acima de nomes/paths de exibição;
- Runtime/Active deriva do Active persistido e não de Working ou biblioteca mutável;
- Security Authority, Application package, Historian/database e License permanecem autoridades separadas.

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

A persistência de engenharia distingue estados deliberadamente independentes:

- **Working**: estado editável do desenvolvedor;
- **Revision**: snapshot persistido/imutável;
- **Published**: revisão aprovada para poder ser ativada;
- **Active**: revisão efetivamente validada e aplicada ao runtime.

Publicar uma revisão não altera automaticamente o processo em execução. A ativação monta um runtime candidato isolado, valida readiness e somente confirma a troca após o boundary durável aceitar a nova Active Revision. Falha de compilação/readiness/comunicação exigida/persistência mantém o runtime anterior.

A API operacional (`/api/tags`, `/api/alarms`, `/api/drivers` e escrita de TAGs) utiliza o runtime realmente ativo. A simulação embutida permanece uma ferramenta de desenvolvimento e nunca substitui silenciosamente uma Active Revision persistida que deveria existir.

### Projeto vinculado ao processo

Uma instância do runtime hospeda um projeto persistido por vez. Configure a chave antes de usar a ativação persistida:

```text
EngineeringRuntime__ProjectKey=plant-a
```

Com PostgreSQL habilitado:

```text
ConnectionStrings__EliteScada=Host=...;Database=...;Username=...;Password=...
```

Opcionalmente, o tempo máximo de preparação do candidato pode ser ajustado:

```text
EngineeringRuntime__ActivationTimeoutSeconds=10
```

No reinício, o EliteSCADA recupera a revisão registrada como **Active**, mesmo que exista Published mais nova aguardando ativação. Se houver Active persistida e ela não puder ser recuperada com segurança, o processo falha fechado.

Consistência durável/runtime:

```text
GET /api/engineering/persistence/{projectKey}/runtime
```

Ativação explícita da Published:

```text
POST /api/engineering/persistence/{projectKey}/published/activate
```

## CI

Não assuma que um PR para a branch de integração atual dispara automaticamente toda a suíte disponível. Releia os workflows e a política de CI vigente.

A direção arquitetural é validação proporcional por perfil/tier:

- T0 local/focused;
- T1 DEV PR sanity/profile;
- T2 integrated broader;
- T3 checkpoint;
- T4 final full.

Não ligue todas as suítes pesadas a todo PR apenas para compensar triggers históricos. Seven-Driver, browser e suítes especiais devem ser executados por causalidade/risco e nos checkpoints apropriados.