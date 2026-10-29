# Banco de dados: configuração, migração e recuperação

## Banco de dados: configurar, migrar e recuperar

Entenda o banco ativo antes de validar outro destino ou iniciar uma migração.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-DATABASE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=database.topology; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Onde entrar e qual problema esta tela resolve

Esta tela define onde o SCADIVO guarda seus dados. Abra Engenharia > Banco de dados com permissão para administrar o sistema (SystemAdmin).

Local Managed é o banco local administrado pela instalação. Remote é um PostgreSQL que você prepara em outro destino. Se o banco local está saudável e atende à instalação, você pode continuar usando-o sem preencher Remote.

Antes de mudar algo, leia Estado atual e Próxima ação. Escolher um banco define armazenamento; não configura, por si só, a redundância HA entre dois servidores.

### Uso e retenção do Historian

Na seção **Uso e retenção do Historian**, confira os tamanhos medidos na base usada pelo Historian. **Banco do Historian** inclui todos os dados daquela base; **Historian total** reúne as amostras brutas e os agregados que o provider conseguiu medir. Se a topologia usa bancos separados, esta tela mede apenas o banco do Historian; não soma o banco principal nem representa o espaço livre do volume do servidor. Espaço livre e crescimento diário só aparecem quando há uma medição confiável. Se a tela disser que não foram informados, isso não significa zero.

**Sem expiração automática** significa que o SCADIVO não programou uma data para remover amostras brutas. Não quer dizer que o disco seja ilimitado. Para guardar as amostras por um período, desmarque a opção e informe a quantidade de dias. Uma política mais curta pode apagar amostras antigas; marque a confirmação apenas depois de revisar o período e o impacto. A política do TimescaleDB executa a remoção, então ela pode não acontecer no instante em que você salva.

Na primeira aplicação, se nenhuma política do SCADIVO estiver registrada, o formulário pede confirmação antes de assumir as políticas Timescale de retenção e atualização dos agregados. Faixas de agregação sem configuração registrada serão desativadas, e regras externas não registradas serão perdidas. Confirme com quem administra o banco antes de prosseguir.

#### Definir um limite por tamanho

O limite em **GiB** controla apenas o espaço medido dos dados brutos do Historian, incluindo os blocos, índices e dados auxiliares do TimescaleDB. Ele não limita agregados, alarmes, eventos, Auditoria, banco principal nem espaço livre do servidor. Os agregados continuam usando a própria retenção.

Em **Ao alcançar o limite**, escolha **Somente avisar** para receber o aviso sem apagar dados. A opção **Remover blocos completos mais antigos** exige a confirmação de possível perda permanente de amostras. A cada minuto, a manutenção pode excluir até quatro intervalos completos antigos e sempre preserva o intervalo mais recente. Como os blocos têm tamanho e duração próprios, o limite é uma meta: o uso pode ultrapassá-lo até a próxima execução e ainda ficar acima quando só restar o intervalo protegido. Não é um teto instantâneo nem uma garantia de espaço livre no volume.

O limite por tamanho e a retenção por dias podem coexistir; vence a primeira condição que remover as amostras. Confira o valor medido depois da manutenção e planeje a capacidade com margem. **Arquivamento e restauração verificada ainda não estão disponíveis nesta tela.**

### Prepare o destino

Peça ao administrador os dados do destino: endereço do servidor, banco, conta, permissões e certificados. Faça backup e combine uma janela de manutenção antes de migrar. A migração pode pausar gravações.

A conexão parte do serviço SCADIVO. Por isso, abrir um endereço no navegador não prova que o serviço o alcança. Em Docker, localhost indica o próprio contêiner; use o nome ou endereço que o serviço realmente consegue acessar.

Da mesma forma, o arquivo de certificado deve existir no servidor em que o serviço roda, seja Windows, Linux ou contêiner. Um caminho disponível só no computador do navegador não atende esse requisito.

### Migrar dados para outro banco

1. Confira qual banco está ativo e tenha um backup disponível.
2. Preencha o destino Remote e use Validar destino. A validação verifica conexão, autenticação, proteção TLS e compatibilidade; conseguir conectar não basta para permitir a troca.
3. Prepare a migração e confira o plano: origem, destino e dados incluídos. Após preparar, a configuração fica bloqueada.
4. Na janela de manutenção, use Iniciar migração. Gravações duráveis podem ficar pausadas durante a operação.
5. Aguarde a cópia e a verificação automática. Leia o resultado antes de seguir.
6. Só após verificação bem-sucedida, use Ativar banco Remote e confirme o destino.
7. Se for solicitado reinício, reinicie ou reabra o serviço conforme sua instalação. Antes de retomar, confira banco ativo, saúde, projeto e revisão.

### Conectar a banco existente não é migrar

Use Conectar a banco existente (sem cópia) somente quando o destino já estiver preparado com o mesmo projeto e a mesma revisão ativa exigidos pela instalação. Esse caminho não copia nem mescla registros. Para levar os dados do banco atual ao novo destino, use a migração.

Leia a confirmação antes de trocar. Se projeto ou revisão forem incompatíveis, a operação é bloqueada. Não esvazie nem altere o banco para contornar essa conferência.

### Reverter e voltar ao banco local

Se aparecer Recovery/Rollback necessário, a operação pede recuperação ou reversão. Leia o diagnóstico e use Reverter operação quando estiver disponível.

Voltar para Local Managed verifica e reativa o banco local preservado; pode pedir reinício. Essa volta não mescla automaticamente os dados gravados no Remote depois da troca. O banco anterior é preservado, mas isso não é o mesmo que restaurar um backup. Também não apaga o Remote.

Depois de voltar, confira quais dados, projeto e revisão estão realmente ativos antes de retomar a operação.

### Campos básicos e avançados

Para preparar Remote, comece pelo endereço, usuário e senha fornecidos pelo administrador. Os demais campos podem ficar nos valores preenchidos quando forem adequados à instalação. Abra Configurações avançadas para consultar ou mudar esses detalhes. A tabela abaixo explica cada escolha; os exemplos não são credenciais reais.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host / FQDN | Nome DNS ou endereço do PostgreSQL alcançável pelo serviço. Não é a URL da interface web. | db.example.invalid |  |
| Usuário / identidade | Conta PostgreSQL provisionada para a aplicação. Não é seu login na Authority do SCADIVO. | Conta fornecida pelo administrador |  |
| Senha / segredo | Usado no envio e não recuperado na leitura do estado. Sumir do formulário após enviar não significa perder a credencial. Nunca cole em chamado ou captura. | Preencher apenas no formulário protegido |  |
| Database | Nome do banco de destino, não nome do projeto. O valor inicial é elitescada; confirme com o administrador. | elitescada |  |
| Porta | Porta do PostgreSQL. O valor inicial é 5432; é diferente da porta HTTPS da aplicação. | 5432 |  |
| Modo TLS | TLS protege a conexão. Require exige criptografia; VerifyCa também confere a cadeia da autoridade certificadora; VerifyFull confere ainda o nome do servidor. Prefer e Disable não garantem a mesma proteção. Use o modo combinado com TI; não desative TLS só para fazer um erro desaparecer. | Require ou VerifyFull com CA válida |  |
| Caminho do certificado CA | Arquivo de CA legível pelo serviço; exigido pelo formulário para VerifyCa/VerifyFull. Caminho do computador do navegador não atende um serviço em outro host/contêiner. | Caminho provisionado no servidor |  |
| Timeout (s) | Tempo de espera da conexão; o valor inicial é 15 segundos. Aumentar não corrige usuário, DNS ou certificado errado. | 15 s |  |
| Historian usa Primary / dedicado | Com a opção marcada, o histórico gravado, chamado Historian, usa o banco principal. Desmarque apenas se preparou um banco dedicado ao histórico. Nesse caso preencha seu endereço completo e valide também a compatibilidade desse destino. | Usar Primary na configuração simples |  |

### Erros que merecem ações diferentes

Comece pela mensagem recebida. Sem conexão, confira nome/endereço, porta e acesso a partir do serviço. Autenticação recusada pede revisão da conta PostgreSQL e da senha. Erro TLS pede conferir autoridade certificadora, nome do servidor e validade do certificado.

Em incompatibilidade, leia os requisitos de versão PostgreSQL, TimescaleDB quando exigido e schema, a estrutura de dados esperada pelo produto. Em fase inválida, atualize o estado e siga Próxima ação; não inicie outra cópia enquanto uma já está ocorrendo.

Com banco indisponível, o Runtime pode ficar degradado. O produto não deve apresentar gravações em memória como se fossem persistidas no banco. Para suporte, registre horário, fase e ID da operação; não inclua senha.

Para retenção de amostras, arquivos programados, rotação e restauração do Historian, consulte [Captura e consulta do Historian](07-HISTORIAN-CAPTURA-CONSULTA.md). Arquivos do Historian são administrados pela API e não fazem parte do backup de configuração do projeto.

[Abrir este guia na Ajuda instalada](/help?topic=database.topology)
