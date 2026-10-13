# ADR — Matter Wi-Fi v1, etapa de desenvolvimento isolado

**Auditoria original:** 2026-10-12
**Revisão desta lane:** 2026-10-13
**DEV-ID:** DEV-HOME-MATTER-WIFI-V1
**Branch:** work/home-matter-wifi-v1-e6ff6ec
**Base exata:** wave15/corrections-integration@e6ff6ec81141b551bd8f2b4ce08387763644f349
**Estado:** SOURCE_REVALIDATED / MOCKED_NO_NETWORK_DEV_TEST_ONLY / NO_DEVICE_COMMISSIONING / NO_PRODUCTION / NO_MERGE

Este ADR carrega a auditoria histórica da Etapa 0, originalmente registrada sobre a base 4d668d38f708a3643061c1b72587c2539550825e. A revisão atual corrige a base e o estado da lane, separa evidência histórica da revalidação atual e registra os limites para qualquer uso da dependência pré-lançamento.

## Autorização atual de Main — somente desenvolvimento/teste isolado

Main autorizou avançar a #589 em uma branch nova, isolada e baseada na integração atual, para desenvolvimento e testes de laboratório. Isso não é aprovação de produção, certificação, suporte multiplataforma do driver ou autorização para exposição em rede de planta/cliente.

Essa revalidação foi feita em 2026-10-13 para um candidato exato e está registrada abaixo. Ela qualifica somente integração/dev-test sem rede e com fake; o issue #1039 ainda bloqueia commissioning ou execução contra dispositivos reais. Se o candidato mudar, repetir os gates antes de usá-lo. Não substituir silenciosamente por outra nightly nem ampliar o escopo.

Qualquer artefato pré-lançamento deve permanecer restrito a desenvolvimento/teste isolado e fora de pacotes distribuíveis ou de produção. Não conectar a redes de planta/cliente. Nenhuma evidência de laboratório equivale a declaração de prontidão de produção.

## Decisões de arquitetura preservadas

1. Seguir #542/#589: serviço Matter.js gerenciado como sidecar; primeira fatia sobre Wi-Fi. A qualificação inicial do driver é restrita a Linux 64-bit, sem alterar a disponibilidade multiplataforma geral do EliteSCADA. Windows/WSL não está declarado suportado. Thread/RCP próprio, BLE commissioning e locks/access-control ficam fora da V1.
2. Usar os registros, candidatos e o ciclo Engineering Preview → Apply existentes. Não criar agora o wizard F5/F6 de #472; #305 posiciona essa UX depois de Engineering Import/Export. Commissioning administrativo não vira Command de processo.
3. Reutilizar os gates comuns de autoridade Active/Standby. Standby não abre controlador, socket, sessão ou subscription Matter; não criar eleição/liderança paralela.
4. HA da V1 permanece STANDALONE_ONLY. Matter não é anunciado nem ativado em instalação HA. Não copiar fabric/identidade entre hosts, não gerar fabric substituto no failover e não prometer continuidade Matter após switchover. Reabrir HA somente por contrato arquitetural independente, revisado por Main, cobrindo identidade, armazenamento protegido/consistente e recuperação.
5. Preservar os contratos/API congelados. Se a feature não couber nas abstrações atuais sem alterá-las, parar e solicitar decisão de Main em vez de ampliar políticas compartilhadas.

## Registro histórico da auditoria de 2026-10-12 — requer revalidação

A auditoria anterior registrou que OHF Matter Server 1.4.0 era a release estável observada, com 1.4.1/1.5.0 em alpha; o servidor se declarava Beta e sem recertificação CSA. Foram identificados #1092 (DoS pré-autenticação associado a PBKDF2) e #1039 (OOM/crash no fluxo CASE com maxPathsPerInvoke=0) sem correção estável confirmada naquela data. O documento anterior concluiu que 1.4.0 não era base aceitável para produção. Isso é evidência histórica, não afirmação sobre o estado atual: conferir novamente issues, releases, tags, changelog e artefato antes de usar qualquer candidato.

A mesma auditoria observou requisitos de Node.js e SO no README/documentação, WebSocket local sem autenticação documentada e diferenças entre commissioning por endereço/rede e provisionamento de Wi-Fi. Também não encontrou especificação de criptografia at-rest para o storage-path. Revalidar cada ponto nas fontes oficiais atuais; não tratar persistência em volume como proteção criptográfica nem expor WebSocket do sidecar à LAN, browser ou usuário remoto.

## Revalidação oficial do candidato isolado — 2026-10-13

- Release pré-lançamento escolhido apenas para testes locais: OHF Matter Server v1.5.0-alpha.0-20261013-75a39cb. O commit/tag publicado é b9e19f732ce644e5d3f6c6a5640c0e9ec60f206f, assinado/verificado pelo GitHub; tree 56c3593509d2c99b3cef3aa77f46088d7ea70703. Não há assets binários anexados à release.
- Pacote npm matter-server@1.5.0-alpha.0-20261013-75a39cb: dist.integrity SHA-512 `sha512-J7giwJkOHjhCQuNRMS6dFyJSuPMcpa18rOkvCd1XZ96dmetAkPN9Eg5G4btDZ60CbvLOa9kUH5q5QuuyWMlpQw==`. O registry publica assinatura/proveniência SLSA; a proveniência identifica o merge 7db89d5261bd418c55799422ad6aca26958978e0, cujo tree é igual ao tree do commit/tag acima. O `@matter-server/ws-controller` da mesma versão tem SHA-512 `sha512-NloWXh08cFUacDDuzrQFTRPzcAtK2CkgmGSutSlbkusJQ5y+OZlMJL93AkJwqjBlHUR+sYdXUTaPtkL0sMHmhw==`. O pacote declara `@matter/main` 0.18.0-alpha.0-20261008-6066b7499, SHA-512 `sha512-8j0tEMqyfGgpZtD3e6KxdesXRR1PymWbaeFpWUsLmBao76FhRfAL0foqgSLgMqMDwuPKpw1kpdhe0hpqEV2kVg==`.
- O repositório do servidor é Apache-2.0, mas os metadados npm dos pacotes Matter Server/controller não declaram campo license. Como nada desta lane pode ser distribuído, não empacotar esses pacotes; se isso mudar, validar o conteúdo legal do tarball e os avisos antes de qualquer pacote.
- O pacote Matter Server declara Node.js >=22.13. A documentação oficial limita o servidor a macOS recente de 64 bits e distribuição Linux muito recente de 64 bits; Windows/WSL e arquitetura não-64-bit não são suportados. O escopo autorizado mantém Linux 64-bit qualificado. Matter depende de IPv6 e multicast link-local; o guia Docker upstream exige host networking, então não presumir que Docker Desktop/bridge neste host Windows sirva para commissioning.
- O protocolo WebSocket não documenta autenticação e o CLI alerta que o padrão faz bind em todas as interfaces. Porém, esta tag aceita path absoluto de Unix socket e documenta que o arquivo respeita umask e só aceita clientes com permissão de escrita. A integração deve usar diretório privado restrito ao usuário do serviço, permissões mínimas no socket, sem porta TCP do WebSocket; a ponte interna EliteSCADA ainda deve exigir autenticação/token. Isso é requisito a implementar e testar, não uma garantia já demonstrada.
- #1092 permanece aberto e rotulado “planned for next version” no repositório do servidor. O maintainer reporta correção em Matter.js #4514, merged em 2026-09-24; o pacote exato acima declara Matter.js 0.18.0-alpha.0, cujo commit de origem descende da correção. Isso reduz o risco PBKDF no candidato, mas não fecha a issue do servidor por si só.
- #1039 permanece aberto e “planned for next version”, sem correção verificável no candidato. Por isso o candidato não pode ser executado em commissioning, com dispositivo real ou com peers não confiáveis. Testes desta lane ficam em fake/no-network. Nenhuma rede de planta/cliente será usada.

Referências para esta avaliação: release [Matter Server v1.5.0-alpha.0](https://github.com/matter-js/matterjs-server/releases/tag/v1.5.0-alpha.0-20261013-75a39cb), pacote [matter-server no npm](https://registry.npmjs.org/matter-server/1.5.0-alpha.0-20261013-75a39cb), [README](https://github.com/matter-js/matterjs-server/blob/v1.5.0-alpha.0-20261013-75a39cb/README.md), [requisitos de SO/rede](https://github.com/matter-js/matterjs-server/blob/v1.5.0-alpha.0-20261013-75a39cb/docs/os_requirements.md), [CLI e Unix socket](https://github.com/matter-js/matterjs-server/blob/v1.5.0-alpha.0-20261013-75a39cb/docs/cli.md), [WebSocket API](https://github.com/matter-js/matterjs-server/blob/v1.5.0-alpha.0-20261013-75a39cb/docs/websockets_api.md), [LICENSE](https://github.com/matter-js/matterjs-server/blob/v1.5.0-alpha.0-20261013-75a39cb/LICENSE), [#1092](https://github.com/matter-js/matterjs-server/issues/1092), [#1039](https://github.com/matter-js/matterjs-server/issues/1039) e [Matter.js #4514](https://github.com/matter-js/matter.js/pull/4514).

## Contratos do repositório a respeitar

- CommunicationDriverRuntimeServices.CanAcquireInputs / CanOwnExternalEffects e ManagedSidecarRuntimeBinding.StartAsync são os gates canônicos de ownership; não os contornar.
- ManagedSidecarDefinition.Validate, ManagedSidecarPersistentStateOwnership e IManagedSidecarBackupHook definem identidade/compatibilidade/ownership e seam de backup; não fornecem replicação HA nem criptografia automática do estado Matter.
- ManagedSidecarProcessStartSpec.StartupInput permite entrada inicial limitada por stdin e zera o buffer após envio; segredos não devem ir em CLI, ambiente, browser ou logs. Isso não fornece por si só RPC autenticado em runtime nem armazenamento criptografado.
- IProtectedMaterialAuthority, ProtectedMaterialScope e ProtectedMaterialPortability são a autoridade de material protegido. Não criar um segundo vault. A política IntegrationProtectedMaterialScopePolicy.SecretField não deve ser ampliada nesta lane; se credenciais Matter exigirem adaptação, fazê-la dentro da feature sobre a autoridade existente.
- Engineering DTOs e o exportador preservam referências/markers, mas pacotes não podem conter bytes secretos, referências host-específicas ou estado/fabric Matter. Importar noutro host deve sinalizar material ausente.
- Integrar via EngineeringDataSourceTypeCatalog, CommunicationDriverRuntimeComposition e EngineeringDriverCatalogApi; usar DriverMaterializationCandidate, ICommunicationDriverDiscoverySource, Runtime e Event Dispatcher existentes. Não persistir eventos transitórios como TAGs sticky nem alterar DTO/schema comum.

## Gates e escopo de validação

- Para o candidato registrado acima, revalidação de fonte concluída. Manter o pin e não baixar/executar o sidecar em runtime de rede enquanto #1039 permanecer aberto; fake/no-network é o limite atual.
- Se aprovado para laboratório, manter pin exato, teste offline/isolado e artefatos fora de distribuição. Sem rede de planta/cliente, commissioning de dispositivos reais ou declaração de produção.
- Verificar integração e lifecycle com testes locais focados, fake/simulador quando possível, preservando os gates comuns de autoridade. Registrar comandos, resultados e identificadores exatos dos testes. Não alegar E2E real sem executá-lo.
- A #589 continua NO_MERGE. Nenhuma alteração deve tocar/rebasear/resetar/apagar a branch antiga work/home-matter-wifi-v1, baseada em 4d668d38f708a3643061c1b72587c2539550825e.

## Resultado atualizado

- #542 architecture e #305 sequence permanecem como decisões de escopo.
- #589 está autorizada somente para desenvolvimento/teste isolado com gates. A revalidação upstream ainda é uma pré-condição; produção e distribuição permanecem bloqueadas.
- A revisão de 2026-10-12 registrou que ManagedSidecarCheckpoint3Tests.RuntimeBinding_NonAuthoritativeRuntimeCannotAcquireExternalResource cobre a negação de runtime não autoritativo antes de iniciar o sidecar e que ManagedSidecarLifecycleTests cobre ciclo fake de start/readiness/stop e recuperação após crash. Reavaliar esses testes na base atual antes de acrescentar duplicatas.
- Neste checkpoint, ainda não foi fixada dependência nem criado binário, integração, commissioning, tráfego Matter, feature de produto, execução de CI ou PR.

## Referências oficiais para revalidar

- OHF Matter Server README e releases: https://github.com/matter-js/matterjs-server
- Requisitos de SO: https://github.com/matter-js/matterjs-server/blob/main/docs/os_requirements.md
- CLI e WebSocket API: https://github.com/matter-js/matterjs-server/tree/main/docs
- Matter.js releases/changelog e migração: https://github.com/matter-js/matter.js
- Licenças do Matter Server e Matter.js: repositórios upstream acima
- Issue #1092: https://github.com/matter-js/matterjs-server/issues/1092
- Issue #1039: https://github.com/matter-js/matterjs-server/issues/1039
