# Alta disponibilidade: configuração e operação

## HA: configurar dois servidores e acompanhar a operação

Distinga configuração salva, servidor ativo, parceiro pronto e proteção contra execução duplicada.

### O que preparar antes

HA significa Alta Disponibilidade: dois servidores podem assumir o Runtime de forma controlada. Abra Engenharia > Alta disponibilidade. Precisa de licença HA Runtime e de HighAvailabilityObserve para observar; salvar e operar também exigem HighAvailabilityTransfer. Confirme rede entre serviços, autenticação, relógios e armazenamento de cada nó. Bancos compartilhados ou separados precisam ter sua topologia conferida; marcar HA não cria sozinho replicação nem prova disponibilidade de banco.

### Preencha somente o que depende da instalação

A tela distingue Este servidor e Servidor parceiro. As identidades internas e o transporte automático não precisam ser inventados pelo operador.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço principal de cada servidor | Endpoint alcançável pelos serviços. O endereço detectado no navegador só serve se o parceiro também o alcançar; localhost em dois hosts não é o mesmo servidor. | Endereços reais dos dois serviços |  |
| Endereço externo alternativo | Abra somente quando a rede exigir endereço anunciado diferente. Sem necessidade, mantenha o automático para evitar destinos inconsistentes. | Sem override numa rede simples |  |
| Servidor preferencial | Preferência para início/failback; não promove um nó à força e não garante que ele esteja pronto. | Este servidor ou parceiro |  |
| Autenticação entre servidores | Configure o segredo compartilhado pelo fluxo protegido de ambos os nós. O valor enviado é limpo da tela; confira o estado de autenticação, sem copiar o segredo para capturas. | Configurar autenticação |  |
| Proteção e referência compartilhada | Fencing impede que dois nós executem como Active. A referência precisa representar o mesmo armazenamento externo compartilhado visto por ambos; dois arquivos locais com nomes iguais não são uma referência comum. | Recurso compartilhado provisionado pela instalação |  |
| Failover automático | Permite a transição automática elegível e exige proteção habilitada. A opção não dispensa prontidão do parceiro, dados disponíveis e condições de segurança. | Habilitar após conferir prontidão |  |

### Procedimento de preparação

1. Confira o modo Em execução.
2. Prepare HA, informe os dois endereços e escolha o preferencial.
3. Configure autenticação e proteção compartilhada; resolva os itens da Prontidão para HA.
4. Salve configuração desejada. Salvo não significa Em execução.
5. Se exigido, planeje reinício dos serviços e confira a configuração realmente carregada.
6. Atualize o estado e confirme Active efetivo, parceiro, banco e proteção. Standby não deve ser tratado como Ready Standby.
7. Antes de usar no processo, faça o ensaio manual acordado com o responsável pela instalação; os testes do manual não substituem essa avaliação.

### Leia os estados antes de operar

Active efetivo é quem está autorizado a executar, não apenas o nó preferido. Synchronizing ainda está recebendo/verificando estado. Ready Standby indica a condição de prontidão avaliada pelo backend; Standby sozinho não a prova. Promoting/Demoting são transições: aguarde o resultado. Stale significa observação antiga; Isolated, Faulted, degradado ou autoridade ambígua exigem diagnóstico. Banco disponível e peer alcançável são informações diferentes. Não esconda uma falha de banco aumentando tolerâncias de rede.

### Escolha a operação certa

Switchover é transferência planejada a partir do Active efetivo para parceiro elegível. Failback pede retorno ao preferencial quando permitido. Recovery reconcilia uma falha/bloqueio. Forçar ressincronização é recuperação específica, disponível quando não há Active e a autoridade anterior foi cercada; não é um botão para declarar um parceiro pronto. Leia alvo, confirmação e resultado; depois confira o histórico e quem ficou Active. Uma solicitação rejeitada não deve ser repetida até a causa mudar.

### Ajustes avançados: significado e limites

Os valores já preenchidos são o ponto de partida. Ajustar tempos altera a proteção e a reação a falhas; faça isso apenas com análise da rede, relógios e operação. Não use os limites do formulário como recomendação de desempenho.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Freshness (s) | Idade máxima de uma observação considerada recente. | Manter valor preenchido | 1–120 s |
| Lease (s) | Validade da autoridade de execução antes de expirar; não é licença comercial. | Manter valor preenchido | 3–120 s |
| Poll (ms) | Intervalo de consulta da referência compartilhada; menor aumenta frequência de consultas. | Manter valor preenchido | 100–10000 ms |
| Witness máximo (s) | Idade máxima da evidência de parceiro pronto. Não cria prontidão por si só. | Manter valor preenchido | Pelo menos Lease; até 600 s |
| Margem de clock (s) | Margem para diferença entre relógios. Não substitui sincronização de tempo. | Manter valor preenchido | 0 até piso(Lease/3) |

### Se houver problema

403: confira permissão de observação/operação. Configuração salva diferente da executada: confira reinício pendente. Peer indisponível: endereços, rede e autenticação dos serviços. Referência indisponível: investigue o armazenamento externo compartilhado. Parceiro não pronto: consulte sincronização, banco e diagnóstico, sem forçar promoção. Acompanhe um problema por vez e registre horário, ação, resultado e nó efetivo; nunca o segredo.

[Abrir este guia na Ajuda instalada](/help?topic=ha.configuration-operation)
