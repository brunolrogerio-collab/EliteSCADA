# Banco de dados: configuração, migração e recuperação

## Banco de dados: configurar, migrar e recuperar

Entenda o banco ativo antes de validar outro destino ou iniciar uma migração.

### Onde entrar e qual problema esta tela resolve

Abra Engenharia > Banco de dados. É necessária a capacidade SystemAdmin. Local Managed usa o banco local administrado pela instalação; Remote usa um PostgreSQL configurado. Essa escolha define armazenamento, não cria redundância de servidores HA. Confira Estado atual e Próxima ação antes de preencher qualquer campo. Se Local Managed está saudável e atende sua instalação, não é necessário configurar Remote.

### Prepare o destino

Combine com o administrador o servidor, banco, conta, certificados e permissões. O endereço deve ser acessível pelo serviço SCADIVO, não apenas pelo navegador. Em Docker, localhost dentro do contêiner significa o próprio contêiner; use o nome/endereço realmente alcançável pelo serviço. Em Windows/Linux, caminhos de certificados pertencem ao servidor onde o serviço executa. Faça backup e escolha uma janela de manutenção antes de migrar.

### Campos básicos e avançados

Comece por host, usuário e segredo. Abra Configurações avançadas somente se precisar alterar os valores já preenchidos. Os exemplos são ilustrativos e não são credenciais de uma instalação.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host / FQDN | Nome DNS ou endereço do PostgreSQL alcançável pelo serviço. Não é a URL da interface web. | db.example.invalid |  |
| Usuário / identidade | Conta PostgreSQL provisionada para a aplicação. Não é seu login na Authority do SCADIVO. | Conta fornecida pelo administrador |  |
| Senha / segredo | Usado no envio e não recuperado na leitura do estado. Sumir do formulário após enviar não significa perder a credencial. Nunca cole em chamado ou captura. | Preencher apenas no formulário protegido |  |
| Database | Nome do banco de destino, não nome do projeto. O valor inicial é elitescada; confirme com o administrador. | elitescada |  |
| Porta | Porta do PostgreSQL. O valor inicial é 5432; é diferente da porta HTTPS da aplicação. | 5432 |  |
| Modo TLS | Require exige conexão criptografada. VerifyCa verifica a cadeia da CA; VerifyFull também verifica o nome do servidor. Prefer e Disable não garantem o mesmo nível de proteção. Use o modo acordado com TI, sem desabilitar TLS apenas para apagar um erro. | Require ou VerifyFull com CA válida |  |
| Caminho do certificado CA | Arquivo de CA legível pelo serviço; exigido pelo formulário para VerifyCa/VerifyFull. Caminho do computador do navegador não atende um serviço em outro host/contêiner. | Caminho provisionado no servidor |  |
| Timeout (s) | Tempo de espera da conexão; o valor inicial é 15 segundos. Aumentar não corrige usuário, DNS ou certificado errado. | 15 s |  |
| Historian usa Primary / dedicado | Marcado, o histórico usa o banco principal. Desmarque somente para um destino Historian dedicado e preencha seu endpoint completo; a compatibilidade desse destino também precisa passar. | Usar Primary na configuração simples |  |

### Migrar dados para outro banco

1. Confira o banco ativo e tenha backup.
2. Preencha Remote e selecione Validar destino. O produto testa conexão, autenticação, TLS e compatibilidade; conectividade sozinha não basta.
3. Prepare a migração e confira origem, destino e dados incluídos no plano. A configuração fica bloqueada após preparar.
4. Durante a janela de manutenção, selecione Iniciar migração. Gravações duráveis podem ficar pausadas.
5. Aguarde cópia e verificação automática; confira o resultado.
6. Somente após verificação bem-sucedida, selecione Ativar banco Remote e confirme.
7. Se houver restart necessário, reinicie/reabra o serviço conforme a instalação. Confira novamente topologia ativa, saúde, projeto e revisão antes de retomar a operação.

### Conectar a banco existente não é migrar

Conectar a banco existente (sem cópia) não copia nem mescla registros. Só deve ser usado para o destino já preparado com o mesmo projeto e a mesma revisão ativa exigidos pela instalação. Leia a confirmação. Incompatibilidade de projeto/revisão bloqueia a troca; não esvazie nem altere o banco para contornar a conferência. Para transportar os dados atuais, use o fluxo de migração.

### Reverter e voltar ao banco local

Se a operação indicar Recovery/Rollback necessário, confira diagnóstico e use Reverter operação quando disponível. Voltar para Local Managed valida e reativa o banco local preservado, podendo exigir reinício. Isso não mescla automaticamente gravações feitas depois no Remote com o banco local. O banco anterior é preservado; voltar não equivale a restaurar um backup nem a apagar o Remote. Confira os dados e a revisão realmente ativos após a troca.

### Erros que merecem ações diferentes

Sem conexão: confira DNS, porta e acesso do serviço. Autenticação recusada: confirme a conta PostgreSQL e seu segredo. TLS: confira CA, nome e validade do certificado. Incompatibilidade: leia versão PostgreSQL, capacidade TimescaleDB quando exigida e schema. Fase inválida: atualize o estado e siga Próxima ação; não repita Iniciar durante uma cópia. Banco indisponível: o Runtime pode ficar degradado; o produto não deve fingir persistência em memória. Registre horário, fase e ID da operação para suporte, sem senha.

[Abrir este guia na Ajuda instalada](/help?topic=database.topology)
