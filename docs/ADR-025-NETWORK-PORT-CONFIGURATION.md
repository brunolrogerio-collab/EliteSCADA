# ADR-025 — Configuração de portas por instalação

**Status:** perfil Docker estável em desenvolvimento no PR draft #828; escopo corrigido em 29/10/2026.
**Data:** 2026-10-29
**Issue:** [#780 — Configurar portas TCP/UDP e serviços pela interface](https://github.com/brunolrogerio-collab/EliteSCADA/issues/780)

## Direção vinculante — Windows/Linux nativos e Docker — 30/10/2026

Esta decisão substitui o recorte exclusivamente Docker descrito abaixo. #780 atende configuração de portas de API, Web, banco local gerenciado e mídia no mesmo produto, com adaptadores próprios para Windows nativo, Linux nativo e Docker. O conteúdo Docker abaixo descreve somente a primeira implementação disponível.

Reutilizar #361 e #408 e a direção `coord/w15-correction-now-parallel-control:docs/WAVE16-WINDOWS-NATIVE-INSTALLER-DIRECTION.md`: payload imutável, configuração/estado externos e executor do host. O instalador completo continua na Wave 16; a configuração de portas deve poder ser consumida por ele sem dependência de Docker.

### Contrato comum e proprietários

| Serviço | Configuração existente a reutilizar | Dependências ao trocar |
|---|---|---|
| API nativa | Endpoints Kestrel/URLs do host ASP.NET | destino do proxy Web, saúde e WebSocket |
| Web nativa | listener do servidor Web definido pelo perfil de instalação | upstream da API, origem/TLS e novo endereço de navegação |
| PostgreSQL local gerenciado | configuração `port` do serviço PostgreSQL | ConnectionStrings:EliteScada e Historian quando apontarem ao mesmo serviço |
| Banco remoto | conexão existente do produto | alterar apenas conexão; não administrar o servidor externo |
| Mídia | listeners API/HLS do MediaMTX | MediaSources:GatewayControlUrl e GatewayPlaybackUrl |
| Docker | publicação Compose para Web/API/DB; listeners privados para mídia | URLs derivadas, isolamento por instância e Compose |

Não inferir endereços nativos a partir de defaults Docker. O perfil precisa declarar serviços administrados, configuração persistente, identidade dos serviços, comandos limitados de reinício, saúde e reversão. Ausência desse inventário deve impedir aplicação e explicar o dado faltante; não fabricar serviço SCM/systemd ou marcar suporte como entregue apenas por selecionar uma plataforma.

A interface deve informar o perfil detectado. Scripts nativos precisam validar portas antes de parar serviços, salvar as configurações sob os respectivos proprietários, preservar credenciais, reiniciar pela integração do host e restaurar todas as dependências se a saúde falhar. Não alterar portas dos Data Sources, regras de firewall ou serviços externos por analogia.

### Próxima implementação

1. Consolidar inventário de endpoints efetivamente configurados em Kestrel, proxy, banco e mídia, com origem/serviço proprietário e estado desconhecido explícito.
2. Estender API/UI com perfil de instalação e capacidades reais de aplicação. Geradores Docker só aparecem para Docker; nativos usam adaptadores próprios.
3. Implementar plano transacional de configuração e adaptadores PowerShell/SCM e Bash/systemd com configurações externas ao payload, respeitando a definição de serviços da distribuição.
4. Verificar mudança e reversão em processos nativos Windows/Linux antes de declarar suporte. Publicar evidências separadas por perfil.

Evidência da correção de Drivers: testes focados 4/4 passaram (perfil API, porta BACnet 47809 chegando à sessão e leitura/escrita Modbus na porta configurada). Seleção ampla 16/17: timeout RTU serial registrado separadamente, sem causa atribuída à reversão. Preview preservado.

## Contexto observado

- `docker-compose.stable.yml` publica Web `127.0.0.1:18080→80`, API `127.0.0.1:15080→5080` e PostgreSQL `127.0.0.1:15432→5432` por configuração da instalação.
- O projeto Compose e os volumes têm nomes fixos (`elitescada-stable*`), portanto mudar só as portas não isola os dados de duas instâncias.
- `docker/stable/nginx.conf` usa o listener interno `api:5080`. MediaMTX usa controle `9997/TCP` e HLS `8888/TCP` por padrão, somente na rede Docker; Runtime entrega vídeo pelo relay da API com tíquetes temporários.
- Portas de equipamento/protocolo são configuradas nos Data Sources. Drivers e suas portas não fazem parte da publicação de portas do host desta tarefa.
- O processo da UI/API não tem host agent privilegiado. O pacote `docker-compose.stable.yml` é o único mecanismo atual confirmado para uma distribuição Docker estável; instalação nativa via serviço Windows/systemd não é controlada por este Compose (`docs/ADR-010-CONTAINER-NATIVE-DISTRIBUTION.md`).

## Decisão

Construir uma interface de configuração para o pacote Docker estável, utilizável em Windows com Docker Desktop e Linux com Docker Compose. A UI prepara o ID da instância, bind de Web/API e portas publicadas de Web/API/PostgreSQL. Também permite trocar as portas internas do controle MediaMTX/HLS e atualiza os endereços internos usados pela API. Esses listeners continuam privados na rede Docker e não são publicados no host.

A UI gera scripts revisáveis de PowerShell e Bash. O administrador os baixa, inspeciona e executa no host da instalação. Eles validam os valores, conflito local e `docker compose config` antes da alteração; salvam backup atômico do `.env` sem imprimir ou substituir segredos; preservam volumes; aplicam o Compose; verificam readiness e restauram a configuração anterior se falhar. Não mudam firewall, não expõem PostgreSQL a todas as interfaces, não abrem shell arbitrário e não montam o Docker socket na aplicação. Navegador não executa comandos no host.

Os padrões atuais devem permanecer iguais em portas, bind, nome do projeto e nomes dos volumes. A publicação padrão de Web/API/banco continua no loopback. Uma instalação adicional usa diretório e `.env` separados; o gerador não copia credenciais/chaves. Banco remoto e peers HA seguem suas integrações próprias. Portas de protocolo permanecem nos Data Sources; esta tela não publica listeners de Drivers nem altera dispositivos externos. Mudanças de portas podem tornar a página atual inacessível; recuperação local e novo URL devem ser descritos no Help.

Execução automática pela UI e configuração de instalações nativas Windows/Linux ficam fora desta entrega porque não existe agente privilegiado suportado. Nenhuma regra do firewall externo será aberta por padrão.

## Evidência de aceite

1. O perfil legado mantém nome, portas padrão, binds e volumes padrão; media defaults permanecem privados.
2. Duas instalações Docker isoladas iniciam juntas com IDs e portas distintas, usando diretórios/secrets próprios.
3. Porta inválida/ocupada é recusada antes de interromper containers.
4. `.env` e volumes permanecem recuperáveis; health check e rollback têm prova automatizada.
5. A UI distingue publicação host de Web/API/banco e portas internas privadas de mídia; explica proxy/firewall e que portas de Drivers ficam nos Data Sources.
6. Help pt-BR/en/es ensina o fluxo que realmente existe, incluindo Local/Docker, novo endereço e recuperação.

Os critérios de integração continuam na #780; este ADR não declara o pacote integrado ou aceito.

## Correção de escopo registrada pelo Product Owner — 29/10/2026

O pedido para alterar portas de módulos refere-se a banco, API, interface Web e mídia. As portas de conexão dos Data Sources já pertencem à configuração de cada driver e não autorizam adicionar uma camada paralela de publicação de listeners de Drivers no host. Essa publicação foi uma ampliação indevida e está sendo removida do PR #828.

Para mídia, a solução deste pacote altera as portas internas do controle MediaMTX/HLS e sincroniza os URLs da API. Mantém MediaMTX inacessível diretamente pelo host, preservando o relay com tíquetes. Não adicionar publicação host de portas de drivers, broadcast BACnet ou os listeners dos protocolos por analogia.

Este candidato atende somente a instalação Docker estável. Configuração nativa Windows/systemd permanece ligada aos contratos próprios de distribuição e não é declarada como entregue nesta branch. A #780 continua aberta até validação do recorte autorizado e revisão/integração.

## Padrões automáticos e alteração posterior — decisão de 30/10/2026

Todos os perfis instalam com portas padrão, sem exigir que o usuário conheça ou preencha portas. API, interface Web, banco local gerenciado e mídia devem permitir alteração posterior opcional. O formulário deve abrir preenchido com os valores atuais; valores não observados não podem ser apresentados como confirmados. O instalador/host executor fornece os padrões do respectivo perfil.

A porta da própria interface Web faz parte do requisito. Ao alterá-la, apresentar o novo endereço antes da aplicação, explicar a interrupção, preservar configuração/dados e oferecer recuperação local se a navegação cair. A troca de API deve atualizar o destino do proxy; a troca do banco/mídia deve atualizar os consumidores correspondentes. Instalação normal não exige edição de código nem configuração manual desses campos.

## Host Web nativo opt-in

`InstallationNetwork:WebRootPath` permite ao executor nativo fornecer a pasta imutável do build Web (`index.html`, assets e Pyodide). Sem esse parâmetro o comportamento anterior da API é preservado. Configuração explícita sem index.html impede a inicialização, evitando serviço aparentemente saudável sem interface.

Neste perfil integrado, Web e API compartilham o endpoint Kestrel e sua porta, configurável por `Kestrel:Endpoints`/URLs ASP.NET. Isso mantém chamadas API/WebSocket na mesma origem; não exige Docker nem uma nova porta preenchida pelo usuário. Padrões e a pasta são fornecidos pelo perfil de instalação. Um proxy externo pode continuar sendo outro perfil com listener próprio. O executor nativo ainda precisa aplicar configuração persistente, reinício, saúde e rollback; esta hospedagem não declara o instalador concluído.

Teste com Kestrel real valida SPA, arquivo estático, API existente, 404 de API/arquivo ausentes e cabeçalhos de isolamento do navegador. Não substitui teste do pacote nativo completo.

## Adaptador Windows nativo — candidato

`public/installation/apply-native-network-profile.ps1` consome um perfil administrativo fornecido pela instalação, schema `scadivo-native-network-v1`, platform `windows`, com `serviceName`, `configurationFile` e `endpointName`. O arquivo deve identificar o appsettings efetivamente carregado pelo serviço e seu endpoint Kestrel. O executor/instalador deve fornecer esse perfil protegido; não pedir ao usuário comum que invente esses valores.

Sem WebPort mantém a porta atual sem gravar/reiniciar. Com WebPort prepara o plano; Apply explícito aplica. Web/API integrados compartilham o listener. Valida disponibilidade da nova porta antes da troca, guarda backup, preserva demais valores do JSON, reinicia somente o serviço registrado, verifica /health e tenta restaurar configuração/serviço/saúde em caso de falha. Não reconfigura banco, mídia, TLS, ambiente/command-line que sobrescreva o endpoint nem instala serviços. Esses casos precisam de adaptadores próprios e verificação da configuração efetiva.

Teste `scripts/ci/test_native_network_profile.ps1`: default sem alteração, plano sem escrita, aplicação com segredo preservado e recuperação após falha de saúde. Serviço/HTTP simulados; não representa aceite SCM real. Ainda pendentes integração à UI, serviço real Windows, adaptador Linux e aplicação coordenada de banco/mídia.

## Adaptador Linux nativo — candidato

`apply-native-network-profile.sh` invoca o companheiro `apply-native-network-profile.py` (Python 3 padrão, sem dependências pip). Ambos precisam estar na mesma pasta. O perfil administrativo usa o mesmo schema do Windows com platform `linux` e a identidade da unidade systemd existente. O executor da instalação fornece o perfil e assegura que o arquivo é a configuração efetiva do serviço.

Sem --web-port mantém a porta atual. Sem --apply apresenta o plano. A aplicação valida a porta, cria backup privado 0600, preserva outras configurações, reinicia somente a unidade identificada e verifica /health. Falha tenta recuperar configuração, serviço e saúde anterior. Serviço e saúde são simulados no teste automatizado; nenhuma alegação de homologação systemd real. Python 3 é pré-requisito deste adaptador administrativo, não da API comum. Banco/mídia, integração à UI, concorrência entre execuções administrativas e testes nativos reais permanecem pendentes antes de liberar aplicação ao usuário.

## Porta do banco local — integração ao resolver

`DatabaseTopology:LocalManagedPort` é uma opção de provisionamento do host, ausente por padrão. O resolver aplica o valor somente ao modo LocalManaged, usando NpgsqlConnectionStringBuilder nas conexões locais principal/Historian. Exige host loopback e faixa válida; preserva senha (inclusive delimitadores), identidade do banco, TLS e demais opções. Topologia Remote continua pelo caminho próprio, sem alterar seu endpoint.

Esta opção é preparação para o adaptador coordenado de PostgreSQL, não uma ação de migração nem uma troca de porta completa. Ainda é necessário mudar o listener PostgreSQL, parar/reiniciar consumidores, verificar a conexão real e restaurar todas as configurações se houver falha. Não orientar o usuário a mudar apenas essa opção antes desse procedimento. Teste focal validou preservação de credenciais e recusas de endpoint remoto/porta inválida.

## Pré-condição de validação do banco — 30/10/2026

O inventário da API deve usar DatabaseRuntimeConnectionSet, a seleção efetiva do produto, para banco principal/Historian. Não pode inferir estado em execução apenas das connection strings do arquivo, pois uma topologia remota ou porta local provisionada pode substituí-las. A resposta mantém somente host/porta/origem e não inclui credenciais.

`/health` atual em Program.cs informa apenas que a API está respondendo; não consulta PostgreSQL. Portanto o adaptador coordenado do banco precisa verificar conexão autenticada, identidade do banco/instalação e endpoint efetivo antes de concluir. Um TCP aberto, pg_isready ou HTTP 200 isoladamente não bastam. Reutilizar as operações de teste da topologia e suas fronteiras de manutenção; não copiar/migrar dados ao alterar somente porta. Essa validação ainda não está implementada nos scripts nativos candidatos.
