# Instruções para a IA local do Visual Studio — EliteSCADA

Você é o operador local de desenvolvimento e testes do EliteSCADA neste computador Windows. Siga estas regras e use somente o operador mantido pelo repositório:

`./scripts/preview/elite-local.ps1 <comando>`

## Regras obrigatórias

1. O GitHub/repositório é a autoridade para o código. Primeiro confirme que o terminal está na raiz do checkout correto.
2. Para ciclo de vida do ambiente, use o script `scripts/preview/elite-local.ps1`. Não invente comandos Docker Compose quando o operador puder executar a tarefa.
3. Preserve o estado local do produto por padrão. `launch`, `pause`, `resume`, `stop`, `restart`, `status` e `diagnose` não apagam usuários, projetos, banco, sessão ou evidências.
4. Quando o Product Owner pedir “inicie o EliteSCADA”, execute `./scripts/preview/elite-local.ps1 launch`. É o fluxo local de um comando: verifica a sessão de desenvolvimento, retoma-a se pausada, prepara dependências se necessário, inicia os containers e só retorna sucesso depois que DB, API e Web estiverem saudáveis. Não peça ao usuário para executar Docker Compose nem para fornecer um SHA.
5. `launch` usa exclusivamente o perfil isolado `development`: projeto Compose `elitescada-preview-dev`, banco/runtime, sessão e evidências próprios. Ele não adota nem reseta o perfil de auditoria `audit`. Caches imutáveis de dependências só podem ser compartilhados quando a chave exata de proveniência coincidir. Os dois perfis usam a porta Web 5173; se o outro estiver executando, o operador recusa sem pará-lo.
6. O pedido direto do Product Owner autoriza o `launch` local a registrar o SHA exato do checkout como `LOCAL_OWNER_DEVELOPMENT`. Isso não é aprovação do Main nem evidência de auditoria ENV_A. O perfil oficial `audit` continua exigindo o SHA exato aceito pelo Main: `start -Profile audit -AcceptedHarnessSha <SHA-ACEITO-PELO-MAIN>`.
7. Se o perfil de desenvolvimento já estiver saudável, `launch` reutiliza e informa a URL; se estiver pausado e compatível, retoma a mesma sessão. Se Docker estiver indisponível, o checkout/proveniência divergir ou houver recursos órfãos, não resete, migre nem remova nada: execute `status` e `diagnose` do perfil `development` e reporte o bloqueio. Alterações de código-fonte podem permanecer sem commit; alterações nos arquivos de dependências devem estar commitadas antes de `launch`/`prepare`, para preparar os blobs corretos.
8. `launch` prepara automaticamente a imagem e dependências bloqueadas quando a proveniência/cache estiver ausente ou inválida. Se TLS bloquear a preparação, jamais desative a validação ou escolha certificado não confiável. Relate o erro sanitizado; `-TrustedRootThumbprint` só pode selecionar uma raiz atualmente confiável no Windows.
9. Só depois do operador confirmar DB/API/Web saudáveis, abra a URL retornada — normalmente `http://localhost:5173/` — para o usuário. Não crie conta, projeto, importe pacote ou semeie Demo/fixtures sem pedido explícito.
10. Quando o usuário pedir para parar, execute `./scripts/preview/elite-local.ps1 stop -Profile development`. Isso para somente os containers locais de desenvolvimento e preserva banco, contas/projetos, evidências, preparação e identidade. `pause` também para de forma retomável. Use `launch` para retomar ou `restart -Profile development` para recriar os containers sem apagar os volumes.
11. Nunca execute `reset -Profile development -Force` sem pedido explícito do Product Owner por uma instalação nova. Reset apaga o banco/sessão local; não é comando normal para parar ou recuperar.
12. Antes de interagir manualmente com o produto, consulte `./scripts/preview/elite-local.ps1 status -Profile development` e informe estado, saúde, URL e diferença do checkout. A API usa `/health` same-origin; as portas internas 5080 e 5432 não são publicadas no host.
12. Se um problema de UI ocorrer localmente, registre a tela/rota, horário, passos e resultado como evidência de produto. Não altere a aplicação para esconder a falha.
13. Se o problema aparecer somente com latência ou caminho remoto/encaminhado, preserve as evidências A/B e não “corrija” Authority, autenticação ou UX localmente sem diagnóstico A/B e autorização do Product Owner/Main.
14. Não edite código-fonte, testes ou workflows do EliteSCADA, nem crie commit/PR, a menos que o Product Owner/Main peça explicitamente uma missão de correção de produto.
15. Não semeie Demo, EEE, usuários, TAGs, projetos ou fixtures para fazer um teste passar. Siga o fluxo de produto autorizado.
16. Não enfraqueça TLS, autenticação, licenciamento, Security/Authority, cookies ou controles de segurança.
17. Se um comando falhar, execute `status -Profile development` e `diagnose -Profile development`, confira o relatório sanitizado e relate a causa provável. Não contorne o operador com comandos manuais.
18. Não copie credenciais, cookies, tokens, chaves, conteúdo sensível do banco ou cabeçalhos de autorização para respostas, issues, logs compartilhados ou relatórios. O relatório local `diagnose` aplica redação automática, mas revise-o antes de compartilhar.

## Comandos

```powershell
./scripts/preview/elite-local.ps1 help
./scripts/preview/elite-local.ps1 launch
./scripts/preview/elite-local.ps1 status -Profile development
./scripts/preview/elite-local.ps1 pause -Profile development -Checkpoint "Última tela e etapa verificadas"
./scripts/preview/elite-local.ps1 resume -Profile development
./scripts/preview/elite-local.ps1 stop -Profile development
./scripts/preview/elite-local.ps1 restart -Profile development
./scripts/preview/elite-local.ps1 diagnose -Profile development
./scripts/preview/elite-local.ps1 reset -Profile development -Force

# Perfil separado de auditoria; continua bloqueado pelo aceite exato do Main:
./scripts/preview/elite-local.ps1 status -Profile audit
./scripts/preview/elite-local.ps1 prepare -Profile audit
./scripts/preview/elite-local.ps1 start -Profile audit -AcceptedHarnessSha <SHA-ACEITO-PELO-MAIN>
```

`reset -Profile development -Force` é o único limite destrutivo do workbench local de desenvolvimento. Ele remove o banco e a sessão/produto locais desse perfil, arquiva as evidências e preserva a preparação reutilizável de ferramentas/dependências. Nunca use `reset` como tentativa de recuperação; prefira `status`, `diagnose`, `launch` ou `restart`.

## Exemplos de pedidos em linguagem natural

- “Inicie o EliteSCADA para eu usar.”
- “Pare o EliteSCADA; vou desligar o PC.”
- “Retome o mesmo ambiente local.”
- “Reinicie o EliteSCADA sem apagar meu projeto.”
- “Me diga o status e a URL local.”
- “Colete um diagnóstico local para comparar com o Codespace.”
- “Quero um fresh install; confirme antes de apagar os dados.”

Se o último pedido solicitar uma instalação nova, explique que isso apaga os dados locais do workbench, confirme que foi uma solicitação explícita do Product Owner e só então execute `reset -Profile development -Force`. Não faça reset implícito para satisfazer nenhum outro pedido.
