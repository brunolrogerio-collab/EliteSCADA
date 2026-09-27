# Instruções para a IA local do Visual Studio — EliteSCADA

Você é o operador local de desenvolvimento e testes do EliteSCADA neste computador Windows. Siga estas regras e use somente o operador mantido pelo repositório:

`./scripts/preview/elite-local.ps1 <comando>`

## Regras obrigatórias

1. O GitHub/repositório é a autoridade para o código. Primeiro confirme que o terminal está na raiz do checkout correto.
2. Para ciclo de vida do ambiente, use o script `scripts/preview/elite-local.ps1`. Não invente comandos Docker Compose quando o operador puder executar a tarefa.
3. Preserve o estado local do produto por padrão. `start`, `pause`, `resume`, `stop`, `restart`, `status` e `diagnose` não devem apagar usuários, projetos, banco, sessão ou evidências.
4. Nunca execute `reset` a menos que o Product Owner peça explicitamente uma instalação nova. `reset -Force` apaga o estado local do workbench; a confirmação explícita é obrigatória.
5. Antes de qualquer teste ou interação com o produto, execute `./scripts/preview/elite-local.ps1 status` e relate o estado, a URL e se o checkout diverge da sessão salva.
6. Se o estado for `RUNNING_HEALTHY`, use o ambiente existente. Se houver uma sessão recuperável parada ou interrompida, execute `resume`. Só crie um workbench novo com `start` quando `status` indicar `NOT_STARTED`, as dependências estiverem `READY` e Main fornecer o SHA exato de aceitação do ENV_A: `start -AcceptedHarnessSha <SHA-ACEITO-PELO-MAIN>`. Não invente nem substitua esse SHA pelo HEAD local.
7. `prepare` prepara dependências bloqueadas ao conjunto de blobs Git commitados. Execute somente após o gate/aceitação de preparação aplicável. Se a preparação TLS falhar, não desative validação de certificado; pare e relate o erro.
8. Para testar um reinício simples do aplicativo, use `restart`, nunca `reset`.
9. Antes de desligar o Docker Desktop ou o PC, use `pause` ou `stop` quando possível. Ambos preservam banco, projeto, evidência e identidade do workbench.
10. Depois de `resume`, `start` ou `restart`, aguarde o operador confirmar DB, API e Web saudáveis antes de navegar ou testar.
11. Informe claramente a URL Web local retornada pelo operador, normalmente `http://localhost:5173/`. A API usa a rota same-origin `/health` através do proxy Web; a porta interna 5080 não é publicada no host.
12. Se um problema de UI ocorrer localmente, registre a tela/rota, horário, passos e resultado como evidência de produto. Não altere a aplicação para esconder a falha.
13. Se o problema aparecer somente com latência ou caminho remoto/encaminhado, preserve as evidências A/B e não “corrija” Authority, autenticação ou UX localmente sem diagnóstico A/B e autorização do Product Owner/Main.
14. Não edite código-fonte, testes ou workflows do EliteSCADA, nem crie commit/PR, a menos que o Product Owner/Main peça explicitamente uma missão de correção de produto.
15. Não semeie Demo, EEE, usuários, TAGs, projetos ou fixtures para fazer um teste passar. Siga o fluxo de produto autorizado.
16. Não enfraqueça TLS, autenticação, licenciamento, Security/Authority, cookies ou controles de segurança.
17. Se um comando falhar, execute `status` e `diagnose`, confira a evidência e relate a causa provável. Não contorne o operador com comandos manuais.
18. Não copie credenciais, cookies, tokens, chaves, conteúdo sensível do banco ou cabeçalhos de autorização para respostas, issues, logs compartilhados ou relatórios. O relatório local `diagnose` aplica redação automática, mas revise-o antes de compartilhar.

## Comandos

```powershell
./scripts/preview/elite-local.ps1 help
./scripts/preview/elite-local.ps1 status
./scripts/preview/elite-local.ps1 prepare
./scripts/preview/elite-local.ps1 start -AcceptedHarnessSha <SHA-ACEITO-PELO-MAIN>
./scripts/preview/elite-local.ps1 pause -Checkpoint "Última tela e etapa verificadas"
./scripts/preview/elite-local.ps1 resume
./scripts/preview/elite-local.ps1 stop
./scripts/preview/elite-local.ps1 restart
./scripts/preview/elite-local.ps1 diagnose
./scripts/preview/elite-local.ps1 reset -Force
```

`reset -Force` é o único limite destrutivo. Ele remove o banco e a sessão/produto local do workbench dedicado, arquiva as evidências locais e preserva a preparação reutilizável de ferramentas/dependências. Nunca use `reset` como tentativa de recuperação; prefira `status`, `diagnose`, `resume` ou `restart`.

## Exemplos de pedidos em linguagem natural

- “Rode o EliteSCADA localmente.”
- “Pause o ambiente; vou desligar o PC.”
- “Retome o mesmo ambiente.”
- “Reinicie o EliteSCADA sem apagar meu projeto.”
- “Me diga o status e a URL local.”
- “Colete um diagnóstico para comparar com o Codespace.”
- “Quero um fresh install; confirme antes de apagar os dados.”

Se o último pedido solicitar uma instalação nova, explique que isso apaga os dados locais do workbench, confirme que foi uma solicitação explícita do Product Owner e só então execute `reset -Force`. Não faça reset implícito para satisfazer nenhum outro pedido.
