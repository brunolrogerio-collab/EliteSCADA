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
| Conta e perfil | E-mail, nome de usuário, nome de apresentação, idioma, hash da senha e atributos de verificação. Tipo de perfil, área de atividade e empresa/escola são exigidos no cadastro e alimentam indicadores internos. Telefone, endereço e data de nascimento são opcionais. | Definir quais campos são necessários. Se os campos de indicadores se tornarem opcionais, revisar também as áreas que hoje exigem perfil completo e o destino dos dados já coletados. |
| Sessão, segurança e auditoria | Cookies próprios de sessão e proteção de formulários. A sessão do Portal vence em até oito horas. Tentativas de login e mudanças administrativas têm controles de segurança. | Aprovar retenção de eventos de auditoria e logs de acesso. Inventariar também os cookies e logs do fórum e do proxy. |
| Comunicação | O formulário público e os chamados recebem mensagem e dados de contato. A fila registra destinatário, estado de processamento e referência da comunicação. A opção de marketing é separada. | Aprovar a duração de chamados encerrados, mensagens e registros de entrega. Confirmar quais fornecedores processam e-mail e hospedagem. |
| Comunidade | O SSO usa o identificador da conta, nome de usuário, nome de apresentação, e-mail e idioma. Publicações seguem as permissões da categoria. | Aprovar tratamento de publicações após encerramento da conta, portabilidade e revisão de traduções. Cobrir ambos os sistemas no procedimento de atendimento. |
| Licenças e downloads | Vínculo de conta, organização, aplicação e identificador de equipamento quando necessário. Pedidos e licenças offline têm dados protegidos. Os inícios de download geram eventos de auditoria. A tabela de visitas guarda somente totais diários por site ou Portal. | Definir retenção de licenças encerradas, pedidos e eventos de auditoria. A janela de consulta dos indicadores é independente da retenção dos dados. |
| Recuperação | Backups criptografados no servidor com limpeza a partir de mais de 14 dias pelo script atual. A cópia criptografada externa é automatizada. O script de cópia externa não faz expurgo das cópias concluídas. | Confirmar a janela externa efetiva, a responsabilidade pelo descarte e a reaplicação de exclusões, correções e bloqueios após restauração. |

Prazos operacionais abaixo são propostas para aprovação. Antes de defini-los no aviso, confirmar a base legal de cada finalidade, a retenção legal aplicável e quem executará cada descarte. O art. 15 do Marco Civil prevê guarda de seis meses para determinados provedores de aplicações. Conferir o enquadramento do SCADIVO, a configuração do proxy e a distinção entre registros de acesso, auditoria e indicadores agregados. [LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm) · [Marco Civil da Internet](https://www.planalto.gov.br/ccivil_03/_ato2011-2014/2014/lei/l12965.htm) · [Direitos dos titulares na ANPD](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados-1/direito-dos-titulares)

## Minutas do piloto para revisão

### Aviso de privacidade

O site, o Portal e a Comunidade SCADIVO são operados por **[identificar controlador e contato oficial de privacidade]**. Este aviso explica como são usados os dados de quem visita, cria uma conta ou pede atendimento.

Usamos o e-mail, o nome de usuário, o nome de apresentação e o idioma para administrar a conta, autenticar o acesso e enviar mensagens de confirmação e recuperação. O tipo de perfil, a área de atuação e a empresa ou instituição de ensino informada nos ajudam a entender o público por meio de indicadores agregados para a equipe. Na configuração atual, pedimos esses três campos no cadastro. Telefone, endereço e data de nascimento são opcionais. Você pode mudar a preferência por comunicações de marketing no perfil. Mensagens necessárias à operação da conta são administradas separadamente.

Para responder a um contato no site ou a um chamado no Portal, usamos os dados de contato e as informações que você enviar. Para administrar licenças, usamos o vínculo com a conta, a aplicação e, conforme a modalidade, um identificador do equipamento. Registramos os inícios de download por item para administrar o catálogo.

Ao entrar na Comunidade com sua conta SCADIVO, o fórum recebe o identificador da conta, o e-mail verificado, o nome de usuário, o nome de apresentação e o idioma. O que você publica fica visível conforme as permissões da categoria. Uma publicação com tradução também pode ser lida no idioma original.

O Portal usa cookies para manter a sessão e proteger formulários. O site e o Portal podem guardar a preferência de idioma no navegador. A contagem de visitas do site e do Portal registra totais diários por serviço, sem um identificador de visitante nessa contagem. **[Completar o inventário do fórum e do proxy. Informar bases legais por finalidade, prazos de guarda, prestadores de serviço e eventual transferência internacional de dados.]**

Você pode solicitar confirmação e acesso aos seus dados, correção e, nos casos aplicáveis, anonimização, bloqueio, exclusão ou portabilidade. Você também pode pedir informações sobre compartilhamento e revogar consentimentos aplicáveis. Use **[canal oficial]** para solicitações sobre o site, o Portal ou a Comunidade. Podemos confirmar sua identidade para proteger os dados da sua conta. **[Definir atendimento, retenção aplicável ao encerramento e destino das publicações e cópias de segurança.]**

### Termos de uso do piloto

O Portal SCADIVO é operado por **[identificação do operador]** e oferece conta, suporte, acesso à Comunidade e gerenciamento de licenças. Mantenha os dados da sua conta atualizados e proteja suas credenciais. Ao usar uma organização, aja dentro das permissões concedidas por ela. Esta versão dos termos se aplica ao piloto. **[Confirmar público elegível e data de vigência.]**

Os direitos de uso do SCADIVO seguem a licença emitida para o titular. A modalidade, os recursos e a validade são os definidos na licença. Licenças vinculadas à conta podem exigir renovação online. A licença offline por máquina depende do vínculo ao equipamento e das condições da emissão.

Na Comunidade, siga as [regras de convivência](https://community.scadivo.com/t/regras-da-comunidade-scadivo/4). Compartilhe material que você tem direito de publicar e mantenha dados privados fora de tópicos públicos. Valide orientações técnicas em ambiente controlado antes de aplicá-las a instalações reais. A equipe pode moderar publicações conforme as regras da Comunidade.

Use **[canal oficial]** para suporte, dúvidas e solicitações relativas à conta. Em caso de suspensão da conta ou moderação de uma publicação, esse canal também pode receber pedidos de revisão. **[Aprovar critérios, comunicação da decisão e fluxo de revisão e definir como os usuários serão informados de mudanças relevantes nos termos.]**

## Decisões para fechar as minutas

| Decisão | Proposta ou informação necessária |
| --- | --- |
| Responsável e atendimento | Confirmar a pessoa física ou jurídica que opera os três serviços, o nome legal a publicar, CNPJ se aplicável e quem recebe e responde às solicitações. O formulário de contato já tem a categoria Privacidade. Confirmar se será o canal oficial ou indicar um e-mail dedicado. Confirmar também o canal de suporte e contestação. |
| Campos de perfil | Tornar tipo de perfil, área de atuação e empresa/escola facultativos para indicadores. Retirar telefone, endereço e data de nascimento do cadastro geral se faltar uma finalidade específica aprovada. Decidir o destino dos dados já coletados. |
| Retenção e fim da conta | Proposta para discussão: descartar cadastros sem confirmação após 30 dias, revisar contas após 12 meses sem uso com aviso prévio, reter chamados e contatos encerrados por 12 meses e registros de entrega de e-mail por 90 dias. Manter dados necessários a licenças vigentes e definir a janela dos registros após encerramento. Confirmar separadamente auditoria, registros de acesso, indicadores e backup externo. Definir se posts públicos permanecem com autoria anonimizada após o encerramento, avaliando remoção quando aplicável. |
| Público e moderação | Confirmar se o piloto é para adultos ou se inclui menores de idade entre estudantes. Validar os requisitos aplicáveis considerando também o acesso provável por menores. Aprovar quem recebe contestações e como comunicar mudanças relevantes nos termos. [Lei 15.211/2025](https://www.planalto.gov.br/ccivil_03/_ato2023-2026/2025/lei/l15211.htm) |

Antes da publicação, conferir e testar o canal oficial, as solicitações de acesso, correção e encerramento no Portal e na Comunidade, os fornecedores e países envolvidos, os cookies e as rotinas de retenção. Publicar aviso e termos com data e versão, com acesso antes do cadastro e pelos links do site, Portal e Comunidade. Preparar a versão final em PT-BR, EN e ES para o Portal e a Comunidade. Planos, pagamentos, cancelamento comercial e reembolso seguem na preparação do lançamento comercial.

## Etapas verificáveis antes de ampliar o piloto

- Revisar as minutas, aprovar identidade e canais oficiais, decidir os prazos por categoria e testar, com conta sintética, um pedido de acesso, correção e encerramento envolvendo Portal e fórum.
- Repetir as verificações de confirmação, recuperação, rejeição e devolução se houver troca de provedor, remetente ou configuração de e-mail.
- Ensaiar a retomada em outro host com proxy/TLS, e-mail, SSO e assinatura online, usando parâmetros de teste. Validar a recuperação de um pedido e de uma licença offline com dados sintéticos. Definir a responsabilidade pela cópia externa e pela detecção de queda do próprio VPS.
- Medir capacidade em homologação com volume representativo. Registrar latência, taxa de erro, filas e uso de CPU/memória. Repetir caso um tradutor passe a compartilhar o servidor.
- Acompanhar artefatos oficiais em #363/#408. Validar downloads reais e o ciclo de licenças no produto. O lançamento comercial depende ainda de planos, regras de conversão, checkout, conciliação e ciclo de cancelamento/reembolso.

As minutas ficam em revisão. A publicação depende da confirmação do responsável e dos valores em aberto.
