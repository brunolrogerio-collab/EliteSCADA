# Importação e conversão de dínamos do Elipse E3

## Estado atual

Os dínamos convertidos de exports E3 foram removidos do conteúdo inicial e não
são mais adicionados automaticamente a projetos novos. Ao carregar um snapshot
antigo, o bootstrap remove seletivamente definições com
`importedDynamoLibrary=true` ou `assetOrigin=elipse-e3-import`; os 72 dínamos
nativos e definições personalizadas são mantidos.

Em snapshots existentes, a remoção é aplicada ao workspace de trabalho no
checkout e fica marcada como alteração; uma nova revisão persistida é criada
quando o usuário salvar. O histórico publicado não é reescrito pela limpeza.

O conversor e o conjunto de desenhos-fonte convertidos foram preservados como
material de referência. Assim, uma importação futura pode ser explicitamente
retomada sem reconstruir o processo nem semear os objetos em todo projeto.

## Pipeline preservado

1. Os CSVs do Elipse E3 são normalizados para
   `src/Scada.Api/Runtime/ImportedE3DynamoLibraryData.json`.
2. `ImportedE3DynamoLibrary.Create()` lê os registros normalizados e
   `BuildDefinition()` converte cada forma em um objeto vetorial canônico do
   EliteSCADA.
3. Primitivas mapeadas: retângulo, elipse, linha, polígono preenchido, arco,
   curva Bezier e texto. Geometrias fechadas mantêm preenchimento/cor; arcos e
   Beziers continuam editáveis no canvas.
4. A hierarquia de `DrawGroup` é reconstruída em `core.group`; posições são
   relativas ao grupo, e sequência de desenho/`zIndex` conserva a ordem de
   sobreposição exportada.
5. Camadas de estado E3 podem virar elementos vetoriais com parâmetros de
   referência a TAG e visibilidade vinculada. Endereços originais de TAG e
   caminhos do projeto-fonte não são copiados; o mapeamento operacional deve ser
   conferido e definido no projeto de destino.
6. Identidades dos elementos são derivadas de forma estável do dínamo e da
   sequência, facilitando repetibilidade de conversões.

## Como retomar uma importação explicitamente

Não recolocar `ImportedE3DynamoLibrary.Create()` nos seeds de
`EngineeringWorkspace`, no salvamento do primeiro projeto ou no bootstrap de
projetos. Para reintegrar uma seleção, adicionar um fluxo explícito de
pré-visualização/seleção e inserir apenas as definições aprovadas no workspace;
revisar parâmetros e vínculos de TAG antes de salvar.

Antes de alterar ou substituir o JSON, manter uma cópia versionada dos exports
originais/normalizados e executar `ImportedE3DynamoLibraryTests` para conferir
hierarquia, ordem, cores, preenchimento, curvas e identidades.
