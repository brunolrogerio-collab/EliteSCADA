# Revisões da Ajuda após mudanças do produto

Pedido do proprietário em 25/10/2026: marcar os trechos que precisarão de revisão quando uma funcionalidade pendente for implementada. O marcador não declara a funcionalidade entregue nem retira a limitação atual.

O inventário é [PRODUCT-CHANGE-REVIEW-MARKERS.json](PRODUCT-CHANGE-REVIEW-MARKERS.json). Cada entrada identifica issue, tópico, campos, condição de retomada e conferência exigida. O gerador insere comentários HTML `HELP-REVIEW-AFTER-PRODUCT-*` nos capítulos correspondentes em pt-BR/en/es. Esses comentários são pesquisáveis na auditoria e não aparecem como instruções ao usuário. A Ajuda instalada mantém explicações legíveis do comportamento atual.

| Mudança de produto | Rever no Help/manual | Critério para atualizar |
| --- | --- | --- |
| [#751 — atraso de ativação](https://github.com/brunolrogerio-collab/EliteSCADA/issues/751) | Campo de atraso e exemplo de configuração, capítulo 17 | Temporização implementada e validada; explicar início, cancelamento e qualidade conforme o comportamento aceito |
| [#753 — dispensar ACK](https://github.com/brunolrogerio-collab/EliteSCADA/issues/753) | Campo Requer reconhecimento e regras de estado/lista/sons, capítulo 17 | Testar opções com/sem ACK, retorno e novo ciclo antes de mudar as afirmações |
| [#785 — seleção Home Assistant](https://github.com/brunolrogerio-collab/EliteSCADA/issues/785) | Seleção HAB e Zigbee2MQTT, capítulos 22/23 | Interface envia seleção explícita, valida vazios/inexistentes e mantém prévia/incorporação controladas |
| [#786 — autoria de vínculo por driver](https://github.com/brunolrogerio-collab/EliteSCADA/issues/786) | Autoria KNX, Zigbee nativo, Z-Wave, Siemens e Logix, capítulos 22/23/24 | Objeto emitido pelo componente real projeta identidade, ajustes e transformação/seletor físico compatíveis, passa no planner estrito e persiste ao reabrir |
| [#787 — inventário Zigbee2MQTT](https://github.com/brunolrogerio-collab/EliteSCADA/issues/787) | Formato de funções e exemplo externo, capítulo 23 | Parser/provider/Runtime consomem definition.exposes e preservam limites/identidades |
| [#779 — armazenamento/arquivo](https://github.com/brunolrogerio-collab/EliteSCADA/issues/779) | Banco de Dados, Historian e indicadores de Audit, capítulos 14/07/18 | Controles realmente entregues, com procedimento de dimensionamento/arquivamento/restauração e limites verificados |

Na auditoria posterior, pesquisar `HELP-REVIEW-AFTER-PRODUCT-`, conferir a versão aceita e suas evidências, corrigir a fonte canônica `system-guides.json`, gerar os capítulos e verificar os três idiomas, entradas contextuais, exemplos e imagens afetadas. Fechar uma issue, por si só, não valida a documentação. Ao resolver a revisão, retirar a entrada ativa e registrar o commit/evidência no controle de entrega existente. Novas dependências de funcionalidades futuras devem receber o mesmo marcador quando forem encontradas.

Os demais recursos da fila #780–#782 ainda não são ensinados como disponíveis. Ao escrever seus guias ou encontrar trechos que dependam deles, registrar tópicos/campos específicos neste inventário. Não criar texto de uso para uma interface ainda inexistente.
