# Homologação local do SSO da comunidade

Esta pasta contém somente a configuração da prova de conceito local do DiscourseConnect. Ela não configura o domínio público, o VPS ou a produção.

## Isolamento

- O fórum é executado pelo launcher oficial `discourse_docker` em Docker Desktop. Copie `scadivopoc.yml` para `containers/scadivopoc.yml` no checkout temporário do launcher e use o nome `scadivopoc` no bootstrap.
- A porta HTTP do fórum (`18085`) e a porta HTTPS do Caddy (`443`) ficam vinculadas a `127.0.0.1`.
- O endereço `https://localhost` usa certificado interno de homologação; não é um certificado público.
- `portal-compose.override.yml` deve ser usado somente junto de `deploy/compose.homolog.yml`. Ele habilita SSO apenas na API de homologação já existente. Em Development, os cookies de sessão/anti-CSRF usam nomes próprios sem o prefixo `__Secure-` para funcionar no HTTP loopback; nos demais ambientes, mantêm prefixo e flag `Secure`. Isso não altera a configuração de produção.
- Banco, uploads, logs e chave SSO ficam em volumes Docker locais separados. Nenhuma chave deve ser gravada nos arquivos desta pasta, no Git ou em variáveis de produção.

## Identidade visual e idioma

- O tema versionado está em `theme/`. Ele usa a paleta oficial SCADIVO (azul-marinho, teal e âmbar), a marca em SVG e oculta a assinatura visual padrão da plataforma.
- A configuração `run` aplica o nome público SCADIVO, avisos e títulos da configuração inicial sem marca da plataforma, as opções de idioma pela interface Rails e reescreve os tópicos iniciais com conteúdo da comunidade SCADIVO. `DISCOURSE_DEFAULT_LOCALE=pt_BR` define Português como padrão.
- Visitantes podem selecionar Português, English ou Español no seletor compacto abaixo do cabeçalho. A escolha usa o parâmetro `tl` e o cookie local do Discourse. Sem seleção anterior, a interface respeita o idioma do navegador e usa Português como padrão.
- Ao autenticar pelo Portal, o locale salvo no perfil SCADIVO é enviado pelo SSO e atualizado também para contas já existentes. Assim, o Portal é a fonte de idioma para usuários autenticados.
- O tema substitui a marca quebrada do cabeçalho pelo wordmark SCADIVO, aplica a paleta da marca e esconde a assinatura de plataforma no rodapé. Instale e aplique o tema antes de abrir o fórum aos visitantes; confira também cabeçalho, login, rodapé, conteúdo inicial e seletor de idiomas.
- O pacote do tema fica pronto para importar no fórum após o primeiro administrador concluir a configuração local. A configuração do nome, do aviso inicial e dos tópicos padrão é aplicada pelo container; a aplicação visual do tema continua pendente dessa etapa.

## Configuração necessária

Crie uma chave aleatória local com pelo menos 32 bytes e monte-a no volume externo `scadivo-community-homolog-secret`, legível somente pelo usuário da API de homologação. Configure exatamente a mesma chave em `discourse_connect_secret` no Discourse local.

O fórum de homologação fica exposto somente em `127.0.0.1:18085`. Para o teste local direto pelo navegador integrado, `run` mantém `force_https=false`, o mínimo das senhas em 8 e a porta canônica em 18085 para os links não perderem a porta publicada; não use esta configuração em um serviço acessível pela rede. Em `Development`, o Portal aceita HTTP apenas quando o fórum e o callback apontam para loopback (`localhost`/`127.0.0.1`); nos demais ambientes, HTTPS continua obrigatório. Configure o fórum como `http://localhost:18085` e o endpoint do Portal local como `http://localhost:18084/portal/api/community/sso`. Ative DiscourseConnect somente nessa instância descartável e mantenha o login local habilitado como recuperação durante o teste. Em **Administração → Configurações → Login**, configure `logout redirect` como `http://localhost:18084/portal/#community-logout`. Assim, ao sair do fórum, o navegador volta ao Portal e encerra também a sessão do Portal, sem expor uma chave administrativa à aplicação. O logout iniciado pelo Portal encerra somente a sessão do Portal; terminar também a sessão do fórum exigiria uma integração administrativa separada, ainda não habilitada.

## Estado e validação desta prova de conceito

Em 23/10/2026, a instância local concluiu o fluxo com uma conta sintética `example.test`: cadastro, confirmação capturada pelo Mailpit, abertura direta de `/session/sso`, redirecionamento ao login do portal, autenticação e retorno ao fórum. Uma segunda entrada pelo link do fórum manteve a mesma conta. Naquele teste, o portal enviava um apelido pseudônimo.

Em 24/10/2026, habilitei `auth_overrides_username` e `auth_overrides_name` somente na Comunidade local. Com outra conta sintética, validei o cadastro e a confirmação pelo Mailpit, o login do Portal pelo nome de usuário escolhido e a criação do perfil no fórum com o nome de usuário e o nome de apresentação do Portal. Depois alterei os dois nomes no Portal e entrei novamente: a conta já existente no fórum refletiu os novos valores. O SSO também envia o idioma preferido. Empresa/escola, endereço, telefone, senha e papéis administrativos/de moderação não são enviados. Nenhum serviço de produção foi alterado.

O `logout redirect` foi configurado na instância local. O retorno `#community-logout` foi validado no navegador integrado: encerra a sessão do portal, remove o desafio SSO pendente e apresenta o login do portal, sem recomeçar a autenticação automaticamente.

O cadastro inicial, a conferência visual e o ciclo SSO completo foram validados por HTTP somente no loopback. A validação visual do ciclo fórum → portal → fórum via HTTPS continua pendente porque o certificado interno do Caddy não é confiável pelo navegador integrado; nenhum aviso foi ignorado. Os testes restantes para produção incluem logout/reentrada no fórum, conta suspensa/desativada, falhas e recuperação, além de backup/restauração, moderação, privacidade, monitoramento e roteamento público.

O portal agora dispõe de suspensão/reativação administrativa de contas comuns. Os testes de integração cobrem revogação da sessão existente, recusa de login e de nova autorização SSO, preservação do perfil, auditoria e novo login após reativar. Contas proprietárias são protegidas desse controle. Isso não valida a suspensão aplicada pelo próprio Discourse nem encerra sessões já abertas no fórum.

Não usar esta configuração como Compose de produção nem abrir o fórum publicamente com base apenas nesta prova de autenticação.
