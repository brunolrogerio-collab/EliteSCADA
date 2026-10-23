# Homologação local do SSO da comunidade

Esta pasta contém somente a configuração da prova de conceito local do DiscourseConnect. Ela não configura o domínio público, o VPS ou a produção.

## Isolamento

- O fórum é executado pelo launcher oficial `discourse_docker` em Docker Desktop. Copie `scadivopoc.yml` para `containers/scadivopoc.yml` no checkout temporário do launcher e use o nome `scadivopoc` no bootstrap.
- A porta HTTP do fórum (`18085`) e a porta HTTPS do Caddy (`443`) ficam vinculadas a `127.0.0.1`.
- O endereço `https://localhost` usa certificado interno de homologação; não é um certificado público.
- `portal-compose.override.yml` deve ser usado somente junto de `deploy/compose.homolog.yml`. Ele habilita SSO apenas na API de homologação já existente.
- Banco, uploads, logs e chave SSO ficam em volumes Docker locais separados. Nenhuma chave deve ser gravada nos arquivos desta pasta, no Git ou em variáveis de produção.

## Configuração necessária

Crie uma chave aleatória local com pelo menos 32 bytes e monte-a no volume externo `scadivo-community-homolog-secret`, legível somente pelo usuário da API de homologação. Configure exatamente a mesma chave em `discourse_connect_secret` no Discourse local.

No Discourse de homologação, use `https://localhost` como endereço público, force HTTPS e configure o endpoint do portal local `http://localhost:18084/portal/api/community/sso`. Ative DiscourseConnect e desative login local somente nessa instância descartável. Em **Administração → Configurações → Login**, configure `logout redirect` como `http://localhost:18084/portal/#community-logout`. Assim, ao sair do fórum, o navegador volta ao portal e encerra também a sessão do portal, sem expor uma chave administrativa do Discourse à aplicação. O logout iniciado pelo portal encerra somente a sessão do portal; terminar também a sessão do fórum exigiria uma integração administrativa separada, ainda não habilitada.

## Estado e validação desta prova de conceito

Em 23/10/2026, a instância local concluiu o fluxo com uma conta sintética `example.test`: cadastro, confirmação capturada pelo Mailpit, abertura direta de `/session/sso`, redirecionamento ao login do portal, autenticação e retorno ao fórum. Uma segunda entrada pelo link do fórum manteve a mesma conta. A conta foi criada com alias pseudônimo, idioma português, nível de confiança inicial e sem papel de administrador/moderador. O portal agora processa o retorno `#community-logout` para encerrar a sessão no sentido fórum → portal e elimina um desafio SSO pendente, evitando que a volta ao portal recomece o login automaticamente. Configure o `logout redirect` da instância local conforme acima para validar o caminho completo.

O certificado local foi aceito somente pelo cliente de teste em chamadas para `localhost`; isso não valida nem autoriza ignorar avisos de certificado em outros endereços. Os testes restantes para produção incluem logout e reentrada após logout, conta suspensa/desativada, falhas e recuperação, além de backup/restauração, moderação, privacidade, monitoramento e roteamento público.

Não usar esta configuração como Compose de produção nem abrir o fórum publicamente com base apenas nesta prova de autenticação.
