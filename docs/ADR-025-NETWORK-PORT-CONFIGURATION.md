# ADR-025 — Configuração de portas por instalação

**Status:** proposta implementada nesta branch; aguardando T1/revisão/integração.
**Data:** 2026-10-29
**Issue:** [#780 — Configurar portas TCP/UDP e serviços pela interface](https://github.com/brunolrogerio-collab/EliteSCADA/issues/780)

## Contexto observado

- `docker-compose.stable.yml` publica Web `127.0.0.1:18080→80`, API `127.0.0.1:15080→5080` e PostgreSQL `127.0.0.1:15432→5432` por `ELITESCADA_{WEB,API,DB}_PORT`; o bind é fixo em loopback.
- O projeto Compose e os volumes têm nomes fixos (`elitescada-stable*`), portanto mudar só as portas não isola os dados de duas instâncias.
- `docker/stable/nginx.conf` usa o listener interno `api:5080`. `docker/stable/mediamtx.yml` mantém o controle em `9997` e HLS em `8888` apenas na rede Docker, sem publicação no host.
- Portas de equipamentos e protocolos pertencem aos respectivos Data Sources/Drivers. `NetworkReachabilityDiagnosticsApi` faz sondagem TCP de saída; não administra listeners do host.
- O processo da UI/API não tem host agent privilegiado. O pacote `docker-compose.stable.yml` é o único mecanismo atual confirmado para uma distribuição Docker estável; instalação nativa via serviço Windows/systemd não é controlada por este Compose (`docs/ADR-010-CONTAINER-NATIVE-DISTRIBUTION.md`).

## Decisão

Construir uma interface de configuração para o pacote Docker estável, utilizável em Windows com Docker Desktop e Linux com Docker Compose. A UI apresenta e permite preparar o ID da instância, bind de Web/API e portas publicadas de Web/API/PostgreSQL. Mostra as portas internas fixas, mídia não publicada, configuração de Drivers separada, URLs derivadas, alcance e impacto do reinício.

A UI gera scripts revisáveis de PowerShell e Bash. O administrador os baixa, inspeciona e executa no host da instalação. Eles validam os valores, conflito local e `docker compose config` antes da alteração; salvam backup atômico do `.env` sem imprimir ou substituir segredos; preservam volumes; aplicam o Compose; verificam readiness e restauram a configuração anterior se falhar. Não mudam firewall, não expõem PostgreSQL a todas as interfaces, não abrem shell arbitrário e não montam o Docker socket na aplicação. Navegador não executa comandos no host.

O padrão atual deve permanecer byte-semantically igual em portas, bind, nome do projeto e nomes dos volumes. Uma instalação adicional usa diretório e `.env` separados; o gerador não copia credenciais/chaves. Banco remoto, peers HA, listeners internos, discovery/multicast e configuração de protocolo são exibidos com limites e instruções específicas, sem alegar que o perfil local os reconfigura. Mudanças de portas podem tornar a página atual inacessível; recuperação local e novo URL devem ser descritos no Help.

Execução automática pela UI e configuração de instalações nativas Windows/Linux ficam fora desta entrega porque não existe agente privilegiado suportado. Nenhuma regra do firewall externo será aberta por padrão.

## Evidência de aceite

1. O perfil legado mantém nome, portas, binds e volumes padrão.
2. Duas instalações Docker isoladas iniciam juntas com IDs e portas distintas, usando diretórios/secrets próprios.
3. Porta inválida/ocupada é recusada antes de interromper containers.
4. `.env` e volumes permanecem recuperáveis; health check e rollback têm prova automatizada.
5. A UI explica listener interno/publicação/proxy/firewall e os limites TCP/UDP/HA/DB.
6. Help pt-BR/en/es ensina o fluxo que realmente existe, incluindo Local/Docker, novo endereço e recuperação.

Os critérios de integração continuam na #780; este ADR não declara o pacote integrado ou aceito.

## Limite deste primeiro recorte

Este candidato implementa apenas o perfil Docker estável. Instalações nativas Windows/systemd, portas de protocolo dos Drivers, publicação externa de MediaMTX e uma execução real de duas stacks simultâneas ainda não estão entregues. A #780 permanece aberta até que esses critérios sejam concluídos ou explicitamente re-sequenciados; o teste local de configuração Compose não equivale ao ensaio simultâneo com dois bancos.
