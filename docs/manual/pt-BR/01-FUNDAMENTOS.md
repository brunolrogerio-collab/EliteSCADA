# Parte I — Fundamentos do EliteSCADA

## 1. Sobre o EliteSCADA

EliteSCADA separa claramente a autoria da aplicação, a execução industrial e as autoridades administrativas. O projeto é preparado em Engineering; o Runtime executa a revisão Active; Authority controla identidade e autorização; Licensing controla entitlement comercial; Historian preserva dados históricos conforme suas próprias regras.

Essa separação é operacionalmente importante: editar uma aplicação não altera automaticamente o que está executando, uma licença não concede autorização de segurança e um backup de Authority não substitui um pacote de aplicação ou backup de banco/histórico.

**Ajuda relacionada:** `/help?topic=engineering.overview`, `/help?topic=runtime.overview`.

## 2. Conceitos fundamentais

- **Working**: estado editável de Engineering.
- **Revision**: versão identificável salva a partir de Working.
- **Published**: revisão publicada e elegível para ativação conforme validação.
- **Active**: revisão que constitui a autoridade de aplicação do Runtime.
- **TAG**: identidade estável para dado de processo/engenharia consumido pelo TAG Engine.
- **Data Source**: origem configurada de dados. Drivers de comunicação e source providers internos são categorias distintas.
- **Authority**: identidade, autenticação, roles e autorização efetiva.
- **Engineering Lock**: proteção opcional do conteúdo de Engineering da aplicação; é independente da senha do usuário e da senha do backup de Authority.
- **License**: entitlement comercial ligado à máquina; não substitui autenticação nem autorização.

## 3. Arquitetura do produto para o usuário

O caminho de operação normal mantém autoridade no backend. Interfaces usam capabilities para orientar UX, mas validação e autorização não dependem apenas de controles visíveis no navegador.

Para dados industriais, consumidores trabalham por contratos públicos do TAG Engine/cache/eventos. Uma tela, script ou relatório não deve abrir conexão privada de Driver para contornar Data Source, Authority ou lifecycle.

## 4. Engineering x Runtime

Engineering e Runtime têm responsabilidades diferentes.

Em Engineering, o usuário cria e valida a aplicação. Alterações permanecem em Working até seguirem o lifecycle. No Runtime, a aplicação executada é a revisão Active registrada pelo backend.

### Procedimento: alterar a aplicação sem afetar o Runtime imediatamente

**Objetivo:** preparar uma alteração de Engineering mantendo a revisão Active em execução até uma ativação explícita.

**Pré-requisitos:** sessão com capabilities de Engineering compatíveis e aplicação acessível.

**Passos:**
1. Abra a área de Engineering.
2. Edite o recurso desejado em Working.
3. Valide a alteração antes de persistir/aplicar quando a superfície oferecer Preview.
4. Salve Working conforme o fluxo do produto.
5. Crie/seleciona a Revision correspondente.
6. Publique a revisão quando pronta.
7. Ative apenas quando a mudança estiver autorizada para entrar em operação.

**Resultado esperado:** o Runtime continua associado à revisão Active anterior até o passo explícito de Activate.

**Troubleshooting:** se uma edição parece ter alterado Runtime sem Activate, confirme primeiro qual revisão o backend informa como Active e não confie apenas em estado visual local.

**Ajuda relacionada:** `/help?topic=engineering.lifecycle`.

## 5. Application / Authority / License / Historian

Esses domínios não são intercambiáveis:

| Domínio | Responsabilidade |
|---|---|
| Application | Conteúdo de Engineering e lifecycle da aplicação |
| Authority | Identidades, roles e autorização |
| License | Entitlement comercial, capacidade e condições de Runtime |
| Historian | Persistência e consulta de dados históricos |

Um `.escadapkg` não é backup de Authority nem banco de Historian. A licença é uma quarta preocupação de instalação e pode ser tratada separadamente em recovery.

## 6. Lifecycle do projeto

O fluxo fundamental é:

`Working -> Save -> Revision -> Publish -> Activate`

Save não é Publish. Publish não é Activate. Apply de um Preview/import também não equivale a Activate.

### Resultado operacional

- Working continua sendo a área de autoria.
- Revision fornece identidade durável ao estado salvo.
- Published indica a revisão publicada.
- Active define o que o Runtime executa.

### Erros comuns

- Assumir que salvar muda o Runtime.
- Tratar Preview como mutação.
- Tratar Apply de importação como ativação.
- Usar estado do frontend como autoridade de lifecycle.

**Ajuda relacionada:** `/help?topic=engineering.lifecycle`, `/help?topic=packages.escadapkg`.
