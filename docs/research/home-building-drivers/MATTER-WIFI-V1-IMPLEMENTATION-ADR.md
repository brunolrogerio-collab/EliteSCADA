# ADR — Matter Wi-Fi v1, etapa de desenvolvimento isolado

**Auditoria original:** 2026-10-12
**Revisão desta lane:** 2026-10-13
**DEV-ID:** DEV-HOME-MATTER-WIFI-V1
**Branch:** work/home-matter-wifi-v1-e6ff6ec
**Base exata:** wave15/corrections-integration@e6ff6ec81141b551bd8f2b4ce08387763644f349
**Estado:** ACTIVE_DEV_TEST / UPSTREAM_REVALIDATION_REQUIRED / NO_PRODUCTION / NO_MERGE

Este ADR carrega a auditoria histórica da Etapa 0, originalmente registrada sobre a base 4d668d38f708a3643061c1b72587c2539550825e. A revisão atual corrige a base e o estado da lane sem converter as conclusões históricas em evidência atual: elas foram observadas em 2026-10-12 e precisam ser revalidadas em fontes oficiais antes de fixar ou executar qualquer dependência Matter.

## Autorização atual de Main — somente desenvolvimento/teste isolado

Main autorizou avançar a #589 em uma branch nova, isolada e baseada na integração atual, para desenvolvimento e testes de laboratório. Isso não é aprovação de produção, certificação, suporte multiplataforma do driver ou autorização para exposição em rede de planta/cliente.

Antes de adicionar dependência, binário ou executar o sidecar, validar em fontes oficiais e registrar os resultados para o candidato exato: versão/tag upstream e natureza estável/pré-lançamento; digest do artefato; licença e avisos; situação dos issues #1092 e #1039; manutenção e sistemas operacionais suportados. Fixar explicitamente o candidato de pré-lançamento autorizado para o teste. Se não houver candidato verificável, digest/licença incompatível, risco de segurança sem mitigação documentada ou incompatibilidade com os limites congelados, parar em MAIN_DECISION_REQUIRED. Não substituir silenciosamente por nightly nem ampliar o escopo.

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

## Contratos do repositório a respeitar

- CommunicationDriverRuntimeServices.CanAcquireInputs / CanOwnExternalEffects e ManagedSidecarRuntimeBinding.StartAsync são os gates canônicos de ownership; não os contornar.
- ManagedSidecarDefinition.Validate, ManagedSidecarPersistentStateOwnership e IManagedSidecarBackupHook definem identidade/compatibilidade/ownership e seam de backup; não fornecem replicação HA nem criptografia automática do estado Matter.
- ManagedSidecarProcessStartSpec.StartupInput permite entrada inicial limitada por stdin e zera o buffer após envio; segredos não devem ir em CLI, ambiente, browser ou logs. Isso não fornece por si só RPC autenticado em runtime nem armazenamento criptografado.
- IProtectedMaterialAuthority, ProtectedMaterialScope e ProtectedMaterialPortability são a autoridade de material protegido. Não criar um segundo vault. A política IntegrationProtectedMaterialScopePolicy.SecretField não deve ser ampliada nesta lane; se credenciais Matter exigirem adaptação, fazê-la dentro da feature sobre a autoridade existente.
- Engineering DTOs e o exportador preservam referências/markers, mas pacotes não podem conter bytes secretos, referências host-específicas ou estado/fabric Matter. Importar noutro host deve sinalizar material ausente.
- Integrar via EngineeringDataSourceTypeCatalog, CommunicationDriverRuntimeComposition e EngineeringDriverCatalogApi; usar DriverMaterializationCandidate, ICommunicationDriverDiscoverySource, Runtime e Event Dispatcher existentes. Não persistir eventos transitórios como TAGs sticky nem alterar DTO/schema comum.

## Gates e escopo de validação

- Primeiro concluir a revalidação oficial do candidato e registrar versão/tag, commit, digest, licença, issues de segurança, manutenção e matriz de SO. Não baixar/executar antes de decidir que o candidato cumpre as condições de laboratório autorizadas.
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
