# #855 — composição guiada de painéis

Continuação da auditoria #791, candidata sobre #854. A prova anterior de publicação/reinício da visão básica não equivale ao aceite funcional de um dashboard analítico completo.

## Implementação candidata

- O gerador central oferece por TAG os seis objetos genéricos aceitos e valor numérico. Reutiliza escalas válidas do TAG ou exige mínimo/máximo explícitos. Faixas e meta exigem valores do autor dentro da escala. O tema é escolhido separadamente. Nada altera os valores, unidades ou métodos analíticos.
- Para uma associação de equipamento, as seleções existentes de desempenho e desvio contextual podem compor vistas independentes. Não é mais necessário configurar ambas para gerar a que já está disponível. Cada Tela recebe navegação somente para as vistas efetivamente geradas.
- O formulário explica análises não configuradas e fornece acesso à associação. Referência, condições e score conjunto experimental são identificados como consultas disponíveis na Engenharia, sem prometer uma vista Runtime que não existe.
- Help e manual pt-BR/en/es acompanham os campos. O reparo do termo inglês de fundo também elimina a falha do guard editorial no CI anterior a3e0234b.

## Evidência deste incremento

- 22/22 testes locais: seis layouts com escala/tema/unidade, bloqueio sem limites, faixas/meta persistidas, composição parcial com desempenho e sem desvio, vínculos entre Telas, geração pelo formulário montado e gauge no editor canônico.
- Build Web passou. Help focal 4/4 passou. Alinhamento do manual/Help: 69 arquivos.
- A primeira tentativa do teste montado esqueceu de abrir o detalhe da medição antes de escolher o objeto. Corrigiu-se a interação do teste; o formulário não sofreu alteração para contornar a falha.

## Pendências de aceite da #855

| Ponto | Situação |
| --- | --- |
| Escolha dos seis layouts, temas e limites | Implementado na candidata; falta prova visual persistida dos seis layouts no painel completo |
| Equipamento com somente uma análise configurada | Composição independente implementada; falta prova de consulta real pelo Runtime com essa composição |
| Desempenho e desvio com resultados visíveis e dados sintéticos | Prova completa ainda pendente; os testes de geração não comprovam o resultado da consulta |
| Etapas do processo | Visão de TAGs/widgets disponível; composição central das vistas de membros ainda pendente. Ações especializadas existentes permanecem na seção de etapas |
| Referência, condições e score conjunto no Runtime | Consumidores não entregues; consultas da Engenharia não são equivalência funcional no Runtime |
| Aceite visual, figuras finais, integração | Pendentes; não fechar #855/#791 com este incremento |

Preservar contratos congelados, campo adiado, ausência de KPI agregado, segurança/licenciamento e plataforma Windows/Linux. Não alterar Preview/DEMO nem iniciar HA-D2.
