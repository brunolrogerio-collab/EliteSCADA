# Revisões da Ajuda após mudanças do produto

Pedido do proprietário em 25/10/2026: marcar os trechos que precisarão de revisão quando uma funcionalidade pendente for implementada. O marcador não declara a funcionalidade entregue nem retira a limitação atual.

O inventário é [PRODUCT-CHANGE-REVIEW-MARKERS.json](PRODUCT-CHANGE-REVIEW-MARKERS.json). Cada entrada identifica issue, tópico, campos, condição de retomada e conferência exigida. O gerador insere comentários HTML `HELP-REVIEW-AFTER-PRODUCT-*` nos capítulos correspondentes em pt-BR/en/es. Esses comentários são pesquisáveis na auditoria e não aparecem como instruções ao usuário. A Ajuda instalada mantém explicações legíveis do comportamento atual.

| Mudança de produto | Rever no Help/manual | Critério para atualizar |
| --- | --- | --- |
| [#751 — atraso de ativação](https://github.com/brunolrogerio-collab/EliteSCADA/issues/751) | Campo de atraso e exemplo de configuração, capítulo 17 | Temporização implementada e validada; explicar início, cancelamento e qualidade conforme o comportamento aceito |
| [#753 — dispensar ACK](https://github.com/brunolrogerio-collab/EliteSCADA/issues/753) | Campo Requer reconhecimento e regras de estado/lista/sons, capítulo 17 | Testar opções com/sem ACK, retorno e novo ciclo antes de mudar as afirmações |
| [#785 — seleção Home Assistant](https://github.com/brunolrogerio-collab/EliteSCADA/issues/785) | Seleção HAB e Zigbee2MQTT, capítulos 22/23 | Interface envia seleção explícita, valida vazios/inexistentes e mantém prévia/incorporação controladas |
| [#786 — autoria de vínculo por driver](https://github.com/brunolrogerio-collab/EliteSCADA/issues/786) | Autoria KNX, Zigbee nativo, Z-Wave, Siemens, Logix e Matter condicional, capítulos 22–26 | Objeto emitido pelo componente real projeta identidade, ajustes e transformação/seletor físico compatíveis, passa no planner estrito e persiste ao reabrir; endereço isolado não substitui o vínculo |
| [#787 — inventário Zigbee2MQTT](https://github.com/brunolrogerio-collab/EliteSCADA/issues/787) | Formato de funções e exemplo externo, capítulo 23 | Parser/provider/Runtime consomem definition.exposes e preservam limites/identidades |
| [#779 — armazenamento/arquivo](https://github.com/brunolrogerio-collab/EliteSCADA/issues/779) | Banco de Dados, Historian e indicadores de Audit, capítulos 14/07/18 | Controles realmente entregues, com procedimento de dimensionamento/arquivamento/restauração e limites verificados |
| [#792 — qualificação Matter](https://github.com/brunolrogerio-collab/EliteSCADA/issues/792) | Disponibilidade, dependências e operações da versão em teste, capítulo 26 | Revalidar o artefato exato e o subconjunto aceito; um fechamento upstream não comprova suporte a comissionamento, Thread, HA ou produção |

Na auditoria posterior, pesquisar `HELP-REVIEW-AFTER-PRODUCT-`, conferir a versão aceita e suas evidências, corrigir a fonte canônica `system-guides.json`, gerar os capítulos e verificar os três idiomas, entradas contextuais, exemplos e imagens afetadas. Fechar uma issue, por si só, não valida a documentação. Ao resolver a revisão, retirar a entrada ativa e registrar o commit/evidência no controle de entrega existente. Novas dependências de funcionalidades futuras devem receber o mesmo marcador quando forem encontradas.

Os demais recursos da fila #780–#782 ainda não são ensinados como disponíveis. Ao escrever seus guias ou encontrar trechos que dependam deles, registrar tópicos/campos específicos neste inventário. Não criar texto de uso para uma interface ainda inexistente.

## Programa de correções pós-licenciamento

O [plano #788](https://github.com/brunolrogerio-collab/EliteSCADA/issues/788) e o [registro no repositório](../WAVE15-POST-LICENSING-CORRECTIONS-PLAN-2026-10-26.md) incluem #789 supressão por alarme/área/subárea, #790 CheckBox/ComboBox, #791 auditoria funcional de Intelligence e #792 revalidação Matter. São tarefas autorizadas na fila; não são instruções de funcionalidades já disponíveis.

Cada entrega identifica os tópicos/campos existentes afetados ou cria os guias dos novos controles, com exemplos, procedimentos e capturas reais. Os marcadores detalhados entram junto dos alvos válidos; não inserir referências a tópicos inexistentes só para preencher a fila. Para Intelligence, o resultado da auditoria determina quais campos e percursos precisam de correção. Para Matter, uma correção upstream declarada não basta para remover a indicação experimental do produto. Help/manual devem acompanhar o candidato aceito, mantendo a distinção entre futuro, implementado e validado.
