# ADR — Matter Wi‑Fi v1, Etapa 0

**Data:** 2026-10-12
**DEV-ID:** `DEV-HOME-MATTER-WIFI-V1`
**Base:** `wave15/corrections-integration@4d668d38f708a3643061c1b72587c2539550825e`
**Estado:** `MAIN_DECISIONS_RECORDED / ADR_ONLY / NO_PRODUCT_CODE / NO_MERGE`

Este ADR registra a auditoria da Etapa 0 da #589 e as decisões posteriores de Main. Ele não seleciona uma versão de produção, não altera contrato comum e não libera a implementação do driver Matter.

## Decisões já tomadas por Main — manter

1. **Arquitetura:** seguir #542/#589: serviço Matter.js gerenciado como sidecar; primeira fatia Matter sobre Wi‑Fi e Linux 64-bit qualificado. Isso limita somente a disponibilidade inicial deste driver: EliteSCADA continua multiplataforma. Windows/WSL não é declarado suportado. Thread/RCP próprio, BLE commissioning e acesso a locks/access-control ficam fora da V1.
2. **Fluxo Engineering:** usar os registros, candidatos e o ciclo Preview → Apply já existentes. Não criar agora o wizard/UX guiado HOME/BUILDING das fases F5/F6 de #472; #305 posiciona essa UX depois de Engineering Import/Export. Commissioning administrativo não vira Command de processo.
3. **Autoridade de execução:** reutilizar o gate comum Active/Standby. O Standby não abre o controlador, socket, sessão nem subscription Matter. Não criar eleição ou liderança paralela.
4. **Escopo HA da V1:** `STANDALONE_ONLY`. Matter não é anunciado nem ativado em instalação HA; o controller Matter não inicia no Standby. Não copiar fabric/identidade entre nós, não gerar fabric substituto durante failover e não prometer continuidade Matter em HA. Uma futura capacidade HA exige contrato arquitetural separado, revisado por Main, para persistência protegida, identidade/fabric e recuperação.

## Evidência upstream revalidada em 2026-10-12

- A release estável mais recente do OHF Matter(.js) Server é **1.4.0 (2026-08-07)**; releases 1.4.1/1.5.0 observadas são pré-lançamentos alpha, não substitutos estáveis. O repositório declara o servidor **Beta** e ainda não re-certificado pela CSA. O repositório é Apache-2.0; cumprir avisos/SBOM continua obrigatório.
- O README pede Node.js **22.13+ ou 24.x/26.x**, recomendando 24.x. A documentação do servidor limita suporte a versões recentes de macOS 64-bit e Linux recente 64-bit, excluindo outros sistemas e WSL. Para a primeira qualificação, prevalece o limite mais restrito aprovado por Main: Linux 64-bit. Não extrapolar suporte por a biblioteca matter.js, isoladamente, declarar Node LTS.
- Há dois riscos de implementação ainda abertos no upstream estável: [#1092](https://github.com/matter-js/matterjs-server/issues/1092), DoS pré-autenticação por contagem PBKDF2, cujo autor maintainer diz que foi corrigido em matter.js #4514 e entrará na próxima release do servidor; e [#1039](https://github.com/matter-js/matterjs-server/issues/1039), OOM/crash durante CASE quando `maxPathsPerInvoke=0`, ainda marcado como planejado para próxima versão sem correção/release estável confirmada. Assim, **1.4.0 não pode ser aceito como base de produção**. A validade de uma versão corrigida precisa ser reprovada no commit/tag exato antes de piná-la.
- A API WebSocket do servidor documenta `ws://localhost:5580/ws`, mas o CLI diz que `--listen-address` sem valor faz bind em todas as interfaces; não encontrei autenticação documentada para esse WebSocket. Ele não pode ficar exposto à LAN, browser ou usuário remoto. A interface de controle EliteSCADA→sidecar precisa cumprir o transporte local autenticado exigido pela #589/#543.
- Para o recorte sem BLE, o upstream documenta commissioning por endereço/`commission_on_network` e `network_only` apenas para dispositivo já presente na rede. Isso não entrega Wi‑Fi a um dispositivo que ainda não recebeu credenciais; casos que precisem disso permanecem fora da V1.
- O servidor persiste dados Matter em `--storage-path`; a documentação examinada não especifica criptografia at-rest nem garantia de proteção/portabilidade desse diretório. Não presumir que volume persistente equivale a material protegido.

## Contratos do repositório que se aplicam

- `CommunicationDriverRuntimeServices.CanAcquireInputs` e `CanOwnExternalEffects` em `src/Scada.DriverHost/Engineering/CommunicationDriverRuntimePlanning.cs`; `ManagedSidecarRuntimeBinding.StartAsync` em `src/Scada.DriverHost/Sidecars/ManagedSidecarRuntimeBinding.cs`: autoridade ativa é gate de aquisição/ownership. Usar o caminho existente, sem bypass.
- `ManagedSidecarDefinition.Validate`, `ManagedSidecarPersistentStateOwnership` e `IManagedSidecarBackupHook` em `src/Scada.DriverHost/Sidecars/ManagedSidecarLifecycle.cs`: definem identidade/compatibilidade/ownership e seam de backup; não implementam replicação HA nem criptografia automática do estado Matter.
- `ManagedSidecarProcessStartSpec.StartupInput` em `src/Scada.DriverHost/Sidecars/SystemManagedSidecarProcess.cs`: permite entrada inicial limitada via stdin e zera o buffer depois do envio; segredos não devem ir em CLI, ambiente, browser ou logs. Isso não resolve por si só RPC autenticado em tempo de execução nem persistência criptografada.
- `IProtectedMaterialAuthority`, `ProtectedMaterialScope` e `ProtectedMaterialPortability` em `src/Scada.Api/Security/ProtectedMaterialAuthority.cs`: autoridade de material opaco, escopado e protegido, com API de mutação da feature. A autoridade não é replicação do estado completo do sidecar.
- `IntegrationProtectedMaterialScopePolicy.SecretField` em `src/Scada.Api/Security/IntegrationProtectedMaterialReferences.cs` atualmente não inclui Matter; `HostAuthorityDriverMaterialResolver.ResolveAsync` em `src/Scada.Api/Security/HostAuthorityDriverMaterialResolver.cs` encaminha o marker portátil ao serviço de integração já allowlisted. **Não ampliar essa política congelada nesta lane.** Se a solução exigir credenciais Matter por Data Source, será necessário um adaptador/API pertencente à feature sobre a autoridade existente e sem criar um segundo vault.
- `DataSourceEngineeringDto.SecretReferences` em `src/Scada.Engineering/Contracts/EngineeringContracts.cs` e `EngineeringFragmentPackageBuilder.SanitizeDataSource` em `src/Scada.Engineering/ImportExport/EngineeringFragmentPackageBuilder.cs` preservam referências/markers; o pacote não deve conter bytes secretos, referência host-específica ou estado/fabric Matter. Provisionamento em outro host deve indicar credencial/material ausente.
- `EngineeringDataSourceTypeCatalog.Validate` valida referências apenas contra campos `SecretReference`/`CertificateReference` do descriptor; `CommunicationDriverRuntimeComposition.BuildForCurrentSchema` e `EngineeringDriverCatalogApi.AddEngineeringDriverCatalog` são os pontos de registro. O novo driver deve se integrar a eles sem alterar DTO/schema comum.
- `DriverMaterializationCandidate` e `ICommunicationDriverDiscoverySource` em `src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs` mantêm descoberta como candidato; Runtime/Event Dispatcher existentes continuam sendo os caminhos canônicos. Não persistir eventos transitórios como TAGs sticky.

## Decisão de Main — V1 standalone-only

Main resolveu a fronteira da V1 como `STANDALONE_ONLY`. A #560 continua impedindo ownership duplicado; #497 protege material por authority/escopo; #543 define ownership/backup do sidecar. Nenhum deles é um protocolo de replicação do fabric e nenhum deve ser reinterpretado como tal.

Consequências obrigatórias para esta lane:

- Não anunciar nem habilitar Matter para uma instalação configurada em HA.
- Não iniciar controller, socket, sessão ou subscription Matter no Standby, nem após promoção sem estado local provisionado e validado.
- Não copiar fabric/identidade entre hosts, gerar fabric substituto no failover, ou declarar continuidade de dispositivos após switchover.
- Não alterar #497/#543/#560 nem introduzir mecanismo privado de persistência/replicação.
- Reabrir HA Matter somente por contrato arquitetural independente, revisado por Main, que trate identidade, armazenamento protegido/consistente, recuperação e critérios de crash/restart/upgrade.

## Gate de segurança — bloqueia integração e exposição real

Não usar o OHF Matter Server 1.4.0 em produção nem expô-lo a redes Matter. Não substituir por alpha/nightly. Só reavaliar integração real quando uma release estável upstream corrigir **ambos** #1092 e #1039 e a evidência (tag/commit/artefato e validação dos dois achados) for revista por Main. Até lá, esta lane não adiciona dependência, binário, commissioning, tráfego de rede nem Matter E2E.

## Resultado e próximos passos

- `#542 architecture`: mantida; `GO_WITH_GATES`, Linux/Wi‑Fi primeiro.
- `#305 sequence`: mantida; sem wizard F5/F6 antes de Engineering Import/Export; Matter permanece na lane aprovada, sujeita aos gates registrados aqui.
- `#589 implementation`: decisão Main registrada; a integração do produto continua bloqueada pelos gates de upstream e permanece `STANDALONE_ONLY` quanto a HA.
- Revisão do recorte offline: `ManagedSidecarCheckpoint3Tests.RuntimeBinding_NonAuthoritativeRuntimeCannotAcquireExternalResource` já verifica, com fake process e sem rede, que runtime não autoritativo é negado antes de iniciar o sidecar. `ManagedSidecarLifecycleTests` também cobre o ciclo genérico fake de start/readiness/stop e recuperação após crash. Duplicar esses testes para um driver Matter ainda inexistente não acrescenta cobertura específica; portanto, esta retomada fica somente no ADR.
- Nenhuma dependência foi fixada; nenhum binário/integração Matter, commissioning, tráfego de rede, feature de produto, CI ou PR foi criado por esta retomada.

## Referências oficiais

- [OHF Matter Server README/releases](https://github.com/matter-js/matterjs-server/blob/main/README.md)
- [OHF Matter Server OS requirements](https://github.com/matter-js/matterjs-server/blob/main/docs/os_requirements.md)
- [OHF Matter Server CLI](https://github.com/matter-js/matterjs-server/blob/main/docs/cli.md)
- [OHF Matter Server WebSocket API](https://github.com/matter-js/matterjs-server/blob/main/docs/websockets_api.md)
- [Matter.js releases and changelog](https://github.com/matter-js/matter.js/blob/main/CHANGELOG.md)
- [Matter.js migration guide — custom Storage factory](https://github.com/matter-js/matter.js/blob/main/docs/MIGRATION_GUIDE_08.md)
- [Matter.js license](https://github.com/matter-js/matter.js/blob/main/LICENSE)
- [OHF Matter Server license](https://github.com/matter-js/matterjs-server/blob/main/LICENSE)
- [Matter.js controller PASE DoS #1092](https://github.com/matter-js/matterjs-server/issues/1092)
- [Matter.js controller CASE OOM #1039](https://github.com/matter-js/matterjs-server/issues/1039)
