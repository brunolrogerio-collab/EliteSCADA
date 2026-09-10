# Wave 14 — registro parcial de auditoria pós-C26

Data: 2026-09-10  
Escopo: registro de evidências já coletadas em modo de auditoria. Este documento não altera código, configuração, processos, GitHub ou estado do produto.

## Referências verificadas

- Issue GitHub #289 e seus comentários de auditoria e revalidação.
- Candidato técnico: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`.
- Pacote EEE Demo: SHA-256 `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`.
- C26 aceito: `08e2530671de10d48933c4b712a1a1abc9e41dce`; C11 canônico: `19d5257d970f53ae798c5fa53946fce07c586452`.
- Handoff de coordenação Wave 14, CURRENT-COORDINATOR-HANDOFF.md e LAST CHANGE.md.

## RECHECK-SIM-PUMP-LEVEL — refutado

A amostragem local autenticada da API exibiu `EEE.P01.LevelPct` diminuindo continuamente (por exemplo, até 61,14...), com processos API e Vite ativos e respostas locais HTTP 200 em aproximadamente 2–4 ms. A simulação e o Runtime não apresentaram sinal de falha durante a captura.

O acesso pelo navegador externo falhou depois da tela de conexão do encaminhamento, em fluxo de autenticação do GitHub Codespaces (`pf-signin` / `ERR_HTTP_RESPONSE_CODE_FAILURE`). A porta 5173 estava encaminhada como privada. Assim, a indisponibilidade observada é classificada, até nova reprodução, como evidência de limitação de encaminhamento/autenticação/proxy do Codespaces; não como defeito confirmado da simulação ou do Runtime.

## UIAUD-289-003 — representação no pacote revalidada

A inspeção somente leitura do pacote EEE Demo mostrou que os estados de bomba não usam propriedades incompatíveis em `core.text`:

- `pump-stopped-plate`, `pump-running-plate` e `pump-fault-plate` são `core.rectangle`, com cor, raio e z-order próprios.
- Os textos `PARADA`, `OPERANDO` e `FALHA` são elementos `core.text` distintos, posicionados acima das respectivas placas.
- Os estados OPERANDO e FALHA têm visibilidade ligada aos TAGs `EEE.P01.Running` e `EEE.P01.Fault`; PARADA é o estado-base sem binding de visibilidade.

Isso refuta a representação histórica inválida que tentava usar `backgroundColor` e `cornerRadius` em `core.text`. A validação visual comportamental no navegador ainda permanece pendente, pois o encaminhamento privado não se manteve disponível.

## UIAUD-289-001 — correlação de compatibilidade legada

A investigação somente leitura no editor indica uma incompatibilidade genérica de tipos persistidos legados:

- O catálogo atual reconhece `core.valueDisplay`.
- O catálogo não contém os identificadores simples `value` nem `dynamo`.
- Referências a esses identificadores ainda existem no caminho legado do editor, incluindo o modelo canônico legado.
- O histórico da issue registra que selecionar `pump01 · DYN` e `current · VALU` aciona erro de schema desconhecido antes de o editor de propriedades poder ser usado.

Hipótese de diagnóstico a confirmar: o fluxo de autoria dinâmica recebe tipos legados e consulta o catálogo atual sem uma normalização/alias ou sem uma degradação segura para objeto desconhecido. A classificação continua GENERIC PRODUCT. Não houve alteração de código nem tentativa de correção.

## Limitações e próximos passos

- A tentativa de autenticação visual no Engineering não concluiu com sucesso nesta automação; esse resultado não foi classificado como defeito de credencial ou de produto.
- Prosseguir com a confirmação do caminho de chamada de schema e, depois, com a correlação de UIAUD-289-002, sempre em modo somente leitura.
- Reproduzir visualmente Runtime/Engineering quando o encaminhamento privado do Codespaces estiver estável; não reiniciar processos de forma especulativa.

## Integridade do trabalho

Nenhum arquivo de produto foi alterado. Não foram criados patches, commits, pull requests ou merges. Este registro não contém senhas, cookies, tokens ou outros segredos.

## Complemento — confirmação do caminho UIAUD-289-001

A leitura do código confirmou que `listDynamicPropertyDestinations(element)` chama `getBuiltinVisualObjectSchema(element.type)` diretamente. `DynamicPropertyEditor` memoriza e executa essa função com dependência de `element.type`, sem uma camada local de normalização ou recuperação para tipo desconhecido.

Combinado com a ausência de `dynamo` e `value` no catálogo atual e com a exceção já registrada na issue, isso confirma o mecanismo: objetos persistidos com esses identificadores legados alcançam a busca atual de schema sem compatibilidade. A origem dos identificadores persistidos e a correção adequada continuam fora do escopo desta auditoria.
