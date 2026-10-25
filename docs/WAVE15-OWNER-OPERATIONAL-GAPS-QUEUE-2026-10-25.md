# Fila autorizada — melhorias operacionais, 25/10/2026

Decisão direta do Product Owner: registrar os quatro gaps abaixo para a sequência de desenvolvimento e retomar a frente atual de Help/manual. Esta é autorização para a fila, não indicação de que as funções já foram implementadas. Não mudar Preview/DEMO, portas, volumes, autenticação ou HA para registrar o pedido.

## Sequência e estado

1. **Frente ativa:** aprofundamento do Help/manual #424/#425, PR #772. Concluir a redação/revisão em andamento; manter o mapa de profundidade real e os gates existentes.
2. **Fila de produto:** executar os pacotes abaixo na ordem proposta, um por vez, com base exata revalidada e contratos/dados existentes preservados. Não reabrir paralelismo nem pedir novamente autorização para registrar estes mesmos efeitos.
3. Cada entrega atualiza sua Ajuda/manual. Coordenar sua entrada no candidato antes de congelar capturas finais; a auditoria #357 e as imagens reais #425 devem representar a versão efetivamente aceita. Não declarar Help, Preview ou Wave 15 completos enquanto os gates correspondentes não forem satisfeitos.

| Ordem | Issue | Entrega | Estado |
| --- | --- | --- | --- |
| 1 | [#779](https://github.com/brunolrogerio-collab/EliteSCADA/issues/779) | Retenção por tempo/tamanho, sem expiração e arquivamento administrados | AUTHORIZED_QUEUED / NOT_IMPLEMENTED para a ampliação |
| 2 | [#780](https://github.com/brunolrogerio-collab/EliteSCADA/issues/780) | Configuração de portas TCP/UDP/serviços e aplicação administrativa pela UI | AUTHORIZED_QUEUED / NOT_IMPLEMENTED |
| 3 | [#781](https://github.com/brunolrogerio-collab/EliteSCADA/issues/781) | Estações de 1–4 monitores com janelas Runtime independentes | AUTHORIZED_QUEUED / NOT_IMPLEMENTED para o gerenciador |
| 4 | [#782](https://github.com/brunolrogerio-collab/EliteSCADA/issues/782) | Runtime público opcional, anônimo e somente View Only | AUTHORIZED_QUEUED / NOT_IMPLEMENTED para a opção geral |

Parent de produto: #306. Coordenação: #305. As issues contêm escopo, fontes existentes, etapas e aceite; a base final/branch será atribuída ao iniciar cada pacote, sem presumir que o SHA observado hoje será o baseline futuro. Nenhuma wave futura específica é atribuída sem planejamento.

## Armazenamento: ampliar a base existente

Há retenção/agregação em `src/Scada.Historian/Policies/HistorianStoragePolicy.cs` e retenção de Auditoria. A nova demanda é administrar capacidade, políticas e arquivos pela interface de maneira integrada e compreensível, adequada a hardware modesto. Sem expiração não significa armazenamento físico infinito. Distinguir políticas por domínio, captura/retention/agregação/backup, impacto da expiração, arquivo verificado/restaurável, disponibilidade do destino e orçamento de CPU/I/O. Não criar um novo store autoritativo concorrente nem aplicar rotação indiscriminada a Authority/projeto/estado HA.

## Rede: configurar instâncias, sem editar código nem liberar shell genérico

O assistente proposto fica em Instalação > Rede e serviços. Deve usar configuração de implantação por instância, distinguir portas de escuta/publicadas/proxy e serviços externos, validar conflitos/dependências e permitir scripts administrativos predefinidos/revisáveis ou uma operação equivalente de serviço. Preservar a sandbox dos Scripts de aplicação. Windows, Linux e Docker têm mecanismos próprios sob uma experiência comum. Documentar portas que um protocolo não pode mudar livremente e recovery se a mudança cortar o acesso à UI.

## Monitores: solução recomendada

**Uma janela de Runtime por monitor**, em vez de exigir um desenho gigantesco com a soma das larguras. Uma janela pode ser de navegação e as outras exibir telas fixas. O servidor pode ser local ou remoto: os monitores pertencem à estação cliente. Não criar produto separado Windows-only.

Propor Engenharia > Apresentação do Runtime > Estações / Monitores, com perfil da estação, janelas, tela inicial, função de navegação/fixa e apresentação. Separar configuração portátil da aplicação de posicionamento/permissões locais. Abrir/reutilizar janelas por ação explícita; posicionar manualmente e usar tela cheia por janela é a alternativa portátil a validar. Posicionamento automático é aperfeiçoamento opcional, condicionado à API/permissões do navegador, HTTPS e popups; não é requisito para funcionar.

A API de [Window Management do Chrome](https://developer.chrome.com/docs/capabilities/web-apis/window-management) permite posicionamento entre monitores, mas exige permissões. A [documentação MDN](https://developer.mozilla.org/en-US/docs/Web/API/Window_Management_API) aponta disponibilidade limitada. Fontes consultadas em 25/10/2026; não presumir suporte universal ou permissão automática para tela cheia.

O cliente mantém identidade de Runtime por aba em `runtimeSessionAdmissionApi.ts`. A implementação precisa verificar abertura independente, duplicação/opener, reconexão e contagem real de Interactive/View Only antes de fixar o tratamento comercial. Não prometer quatro janelas como um único assento nem isentar as janelas fixas. A #781 exige Help com procedimento real e teste de 1–4 monitores, incluindo alternativa manual, DPI/resolução distintos, perda de monitor e recuperação.

## Runtime público: leitura explicitamente autorizada

Propor Segurança > Runtime público, desligado por padrão, com tela inicial e alcance explícitos. Não desabilitar autenticação geral. O visitante recebe somente apresentação e leituras permitidas, com controle no backend, cotas View Only e revogação observável entre nós. Comandos, escritas, ACK, Engenharia e segredos permanecem bloqueados. Login posterior usa as próprias capacidades/sessão admitida. As adaptações da demo #741 são referência a auditar, não prova de uma opção geral já entregue.

## Retomada

Voltar ao Help/manual imediatamente após o registro. Reutilizar a autorização desta fila e as issues vinculadas quando o pacote correspondente começar. Não publicar funcionalidades futuras na Ajuda como se estivessem disponíveis; a documentação de cada melhoria acompanha sua implementação e os limites comprovados.
