# Preparação para o lançamento do Portal SCADIVO

## Piloto atual

O cadastro com confirmação e a recuperação de senha estão disponíveis no Portal. A Comunidade usa a conta do Portal. Suporte, indicadores, licenciamento e administração usam uma base separada da demo. O catálogo de downloads aguarda os artefatos oficiais. Os pagamentos continuam fora do piloto.

A conferência de 19/11/2026 confirmou a sessão anônima da API em HTTPS, o cadastro e envio configurados, o redirecionamento do fórum para SSO e a presença de SPF e DMARC no DNS. Os testes Chromium do Portal passaram 19/19 e a integração PostgreSQL passou 22/22. Na homologação descartável com a imagem de produção c15ac5f9, o Mailpit recebeu os e-mails de confirmação e recuperação em espanhol. O teste confirmou a recusa de login antes da confirmação, links de uso único, acesso pelo nome de usuário e pelo e-mail, rejeição da senha antiga e recusa de indicadores para membro comum. O aceite de uma mensagem de monitoramento pelo SMTP em produção está documentado na [#741](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741#issuecomment-7011534654).

## Entrega de e-mails e recuperação fora do VPS

Em produção, a confirmação e a recuperação chegaram ao Gmail com SPF, DKIM e DMARC aprovados. Após a publicação da #899, o Portal vinculou duas cópias de uma devolução simulada a um único envio real. A caixa administrativa recebeu um único aviso de devolução. [Entrega no Gmail](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741#issuecomment-7019810542) · [Teste em produção](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741#issuecomment-7020649631)

Em 19/11/2026, a cópia criptografada externa foi conferida por SHA-256 e restaurada em PostgreSQL descartável no Windows. A API reconstruída do commit `5f998de6` abriu com as chaves da cópia, aplicou a migração da #899 e ficou saudável em cerca de 10 segundos a partir do início do ensaio local. A página do Portal, a sessão anônima e a recusa de acesso anônimo à administração foram verificadas. As contagens de contas, FAQ, licenças, pedidos offline e e-mails foram preservadas. O tempo medido começa com a imagem e a cópia já disponíveis no computador.

O ensaio usou rede isolada, com e-mail, SSO da Comunidade e assinatura online desativados. A cópia não tinha pedido ou licença offline com conteúdo protegido para testar a leitura desse conteúdo. O ensaio confirma a recuperação da API e da base. A retomada dos serviços externos durante uma queda do VPS continua pendente.

## Inventário para o aviso de privacidade e a retenção

| Área | Dados e finalidade observados | Decisão para revisão |
| --- | --- | --- |
| Conta e perfil | E-mail, nome de usuário, nome de apresentação, idioma, hash da senha e atributos de verificação. Tipo de perfil, área de atividade e empresa/escola apoiam indicadores agregados. Telefone, endereço e nascimento são opcionais. | Confirmar necessidade de cada campo, duração da conta inativa, descarte de cadastros pendentes e tratamento dos campos já coletados. |
| Sessão, segurança e auditoria | Cookies próprios de sessão e proteção de formulários. A sessão do Portal vence em até oito horas. Tentativas de login e mudanças administrativas têm controles de segurança. | Aprovar retenção de eventos de auditoria e logs de acesso. Inventariar também os cookies e logs do fórum e do proxy. |
| Comunicação | O formulário público e os chamados recebem mensagem e dados de contato. A fila registra destinatário, estado de processamento e referência da comunicação. A opção de marketing é separada. | Aprovar a duração de chamados encerrados, mensagens e registros de entrega. Confirmar quais fornecedores processam e-mail e hospedagem. |
| Comunidade | O SSO usa o identificador da conta, nome de usuário, nome de apresentação, e-mail e idioma. Publicações seguem as permissões da categoria. | Aprovar tratamento de publicações após encerramento da conta, portabilidade e revisão de traduções. Cobrir ambos os sistemas no procedimento de atendimento. |
| Licenças e downloads | Vínculo de conta, organização, aplicação e equipamento quando necessário. Pedidos e licenças offline têm dados protegidos. O catálogo conta inícios de download por item. Site e Portal registram visitas diárias agregadas. | Definir retenção de licenças encerradas, pedidos e artefatos de auditoria, preservando somente o necessário. Aprovar a janela dos indicadores agregados. |
| Recuperação | Backups criptografados no servidor com limpeza a partir de mais de 14 dias pelo script atual. Existe cópia criptografada fora do servidor, no Windows. | Confirmar a janela da cópia externa e como aplicar solicitações pendentes após restauração. Definir o procedimento e o responsável pelo descarte. |

A tabela registra finalidades verificadas e decisões em aberto. Prazos para os registros ativos ainda precisam de aprovação antes da implementação da limpeza. Referências para revisão do aviso e do atendimento ao titular. [LGPD](https://www.gov.br/mme/pt-br/acesso-a-informacao/acoes-e-programas/governanca/programa-de-integridade/normativos/l13709_lgpd.pdf) · [Direitos dos titulares na ANPD](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados-1/direito-dos-titulares)

## Minutas do piloto para revisão

### Aviso de privacidade

O site, o Portal e a Comunidade SCADIVO são operados por **[identificar controlador e contato oficial de privacidade]**. Este aviso explica como são usados os dados de quem visita, cria uma conta ou pede atendimento.

Usamos as informações da conta para autenticar o acesso, enviar mensagens de confirmação e recuperação, prestar suporte e administrar licenças. O tipo de perfil, a área de atuação e o vínculo informado nos ajudam a entender o público por meio de indicadores agregados. Os campos identificados como opcionais podem ser deixados em branco. As mensagens operacionais da conta e a opção de receber comunicações de marketing são tratadas separadamente.

Ao entrar na Comunidade com sua conta SCADIVO, o fórum recebe os dados de identificação necessários para vincular o perfil e respeitar o idioma escolhido. Os dados publicados ficam visíveis conforme a categoria. A tradução, quando disponível, é uma versão derivada da publicação. O original permanece disponível.

Usamos cookies para manter a sessão, proteger formulários e lembrar escolhas de idioma na Comunidade. A contagem de visitas do site e do Portal é apresentada em totais diários. **[Confirmar informações de cookies do proxy e do fórum, fornecedores, países envolvidos, finalidades, bases aplicáveis e prazos de retenção.]**

Para pedir confirmação de tratamento, acesso, correção ou exercer os demais direitos aplicáveis, use **[canal oficial e procedimento de atendimento]**. Quando aplicável, confirme a identidade para proteger a conta antes de responder ao pedido.

### Termos de uso do piloto

O Portal SCADIVO oferece conta, suporte, acesso à Comunidade e gerenciamento de licenças. Mantenha os dados da sua conta atualizados e proteja suas credenciais. Ao usar uma organização, aja dentro das permissões concedidas por ela.

Os direitos de uso do SCADIVO seguem a licença emitida para o titular. A modalidade, os recursos e a validade são os definidos na licença. Licenças vinculadas à conta podem exigir renovação online. A licença offline por máquina depende do vínculo ao equipamento e das condições da emissão.

Na Comunidade, siga as [regras de convivência](https://community.scadivo.com/t/regras-da-comunidade-scadivo/4). Compartilhe material que você tem direito de publicar e mantenha dados privados fora de tópicos públicos. Valide orientações técnicas em ambiente controlado antes de aplicá-las a instalações reais. A equipe pode moderar publicações conforme as regras da Comunidade.

Use **[canal oficial]** para suporte, dúvidas e solicitações relativas à conta. **[Confirmar identificação do operador, atualização dos termos, procedimento de suspensão/contestação, tratamento de menores e regras comerciais aplicáveis antes da publicação.]**

## Etapas verificáveis antes de ampliar o piloto

- Revisar as minutas, aprovar identidade e canais oficiais, decidir os prazos por categoria e testar, com conta sintética, um pedido de acesso, correção e encerramento envolvendo Portal e fórum.
- Repetir as verificações de confirmação, recuperação, rejeição e devolução se houver troca de provedor, remetente ou configuração de e-mail.
- Ensaiar a retomada em outro host com proxy/TLS, e-mail, SSO e assinatura online, usando parâmetros de teste. Validar a recuperação de um pedido e de uma licença offline com dados sintéticos. Definir a responsabilidade pela cópia externa e pela detecção de queda do próprio VPS.
- Medir capacidade em homologação com volume representativo. Registrar latência, taxa de erro, filas e uso de CPU/memória. Repetir caso um tradutor passe a compartilhar o servidor.
- Acompanhar artefatos oficiais em #363/#408. Validar downloads reais e o ciclo de licenças no produto. O lançamento comercial depende ainda de planos, regras de conversão, checkout, conciliação e ciclo de cancelamento/reembolso.

As minutas ficam em revisão. A publicação depende da confirmação do responsável e dos valores em aberto.
