# Application Management, Recovery e Troubleshooting

## Export .escadapkg

`.escadapkg` é portabilidade self-contained da aplicação. Não é backup de Authority, licença ou banco/histórico.

Inspect examina conteúdo/metadados sem mutação. Preview mostra o resultado esperado. Apply incorpora o conteúdo validado em Engineering; Apply não é Activate.

**Ajuda relacionada:** `/help?topic=packages.escadapkg`.

## Import

### Procedimento: importar aplicação com segurança

**Objetivo:** incorporar um pacote validado em Engineering sem pular lifecycle.

**Pré-requisitos:** pacote compatível e sessão autorizada.

**Passos:**
1. Selecione o `.escadapkg`.
2. Execute Inspect quando disponível.
3. Execute Preview.
4. Corrija issues reportadas.
5. Execute Apply somente para o candidato validado.
6. Salve a aplicação resultante.
7. Publique e ative separadamente quando apropriado.

**Resultado esperado:** Working recebe o conteúdo importado; Published/Active não mudam implicitamente.

**Troubleshooting:** se Working mudou após Preview, reexecute a validação antes do Apply.

## Reusable libraries

`.escadalib` é diferente de `.escadapkg`.

Associar Library disponibiliza recursos compatíveis no catálogo, mas não altera Working por si só. Usar um recurso incorpora o item selecionado e o closure validado de dependências; o conteúdo passa a pertencer ao projeto.

Desassociar remove disponibilidade no catálogo e não apaga conteúdo já incorporado. Runtime e Active não dependem de um arquivo `.escadalib` externo.

**Ajuda relacionada:** `/help?topic=libraries.reusable-resources`.

## Authority backup

Authority backup preserva identidades locais e assignments suportados em envelope protegido. Não inclui plaintext passwords, tokens, cookies, sessões, licença, aplicação ou dados de Historian.

A senha do backup de Authority é exclusiva desse artefato e não serve como login nem Engineering Lock.

## Restore-first

Uma instalação realmente vazia oferece Restore backup antes de obrigar a criação de usuário/projeto descartável.

Recovery preserva autoridades separadas:

1. aplicação (`.escadapkg`);
2. Security Authority;
3. database/Historian;
4. Licensing como preocupação opcional separada.

### Procedimento: recovery completo em instalação vazia

**Objetivo:** restaurar Authority e aplicação sem criar identidade/projeto provisórios.

**Pré-requisitos:** instalação comprovadamente vazia; backup de Authority e senha; `.escadapkg`; licença opcional.

**Passos de alto nível comprovados pelo contrato:**
1. selecione os artefatos;
2. execute Inspect/Preview de todos os inputs antes da mutação;
3. revalide a condição de instalação vazia;
4. restaure Authority atomicamente;
5. aplique o pacote validado em Working;
6. salve a revisão raiz;
7. publique;
8. confirme que o project key configurado para Runtime corresponde à aplicação restaurada;
9. ative pela operação normal;
10. instale a licença opcional pelo serviço de Licensing;
11. valide as invariantes finais.

**Resultado esperado:** existe Authority utilizável, aplicação salva/publicada/Active e Runtime consistente com a revisão Active.

**Importante:** database/Historian recovery continua uma autoridade separada e deve ter estado explicitamente conhecido.

## Application detach / neutral installation / A -> B -> A switching

A estrutura desses procedimentos está reservada no TOC, mas os passos finais dependem do fechamento da experiência de instalação e switching. Não use instruções antigas ou operações manuais de store como substituto.

## Troubleshooting workflow

Use a mesma ordem antes de criar workaround:

1. reproduza o problema;
2. identifique a autoridade/camada que deveria fornecer o estado;
3. compare backend e UI;
4. valide identidade/capabilities;
5. valide lifecycle/Active;
6. para dados, valide Data Source -> Driver -> binding -> quality/timestamp;
7. para histórico, valide ingestão antes de apresentação;
8. para Scripts, valide referência, sandbox, timeout e fault isolation;
9. para Licensing, diferencie Demo, Valid e Invalid;
10. para recovery, identifique o estágio que falhou e não declare sucesso parcial como sucesso completo.

### Startup

Confirme saúde do backend e disponibilidade da UI. Em instalação vazia, diferencie criação de nova instalação de Restore-first.

### Login

Falha de login deve ser tratada em Authority. Engineering Lock e licença não são credenciais de login.

### Driver communication

Use apenas capabilities declaradas do Driver e verifique configuração, endpoint, conexão, binding e diagnósticos.

### TAG quality

Não force Good. Corrija a origem da quality.

### Runtime

Confirme revisão Active, entitlement e autoridade da sessão.

### Scripts

Verifique dependências declaradas, API pública, sandbox e diagnósticos da instância.

### Licensing

- ausência de licença: Demo;
- licença válida: entitlement do tier;
- licença presente mas inválida: Run bloqueado.

### Authority

Não restaure tokens/sessões. Após restore, identidades restauradas autenticam pelo mecanismo normal.

### Import/restore

Preview é obrigatório antes de Apply onde o fluxo o oferece. Apply não substitui Save/Publish/Activate.

**Ajuda relacionada:** `/help?topic=recovery.backup-system-recovery`, `/help?topic=troubleshooting.overview`, `/help?topic=diagnostics.overview`.
