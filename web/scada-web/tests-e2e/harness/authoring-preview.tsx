import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { FactoryArtworkLibrary } from '../../src/engineering/FactoryArtworkLibrary';
import { DynamoScriptWizard } from '../../src/engineering/visual-editor/dynamo/DynamoScriptWizard';
import { DynamoAuthoringCatalogProvider } from '../../src/engineering/visual-editor/DynamoAuthoringCatalogContext';
import type { EngineeringSnapshot } from '../../src/engineering/types';
import type { VisualEditorBindingSourceCatalogItem } from '../../src/engineering/visual-editor/visualEditorContracts';
import { CanonicalVisualRenderer } from '../../src/engineering/visual-editor/CanonicalVisualRenderer';
import type { DynamoEngineering } from '../../src/engineering/types';
import { normalizeDynamoDefinitionParameterContract } from '../../src/runtime/visual-navigation/dynamoParameterWireContract';
import { expandRuntimeDynamoVisuals, isExpandedRuntimeDynamo } from '../../src/runtime/visual-navigation/runtimeDynamoVisualProjection';
import type { VisualElementEngineering } from '../../src/engineering/types';
import '../../src/styles.css';

const tagId = '15000000-0000-0000-0000-000000000010';
const outputId = '15000000-0000-0000-0000-000000000011';
function AuthoringContracts() {
  const [refreshes, setRefreshes] = useState(0);
  const [binding, setBinding] = useState('');
  const [copied, setCopied] = useState(false);
  const applied = async () => { setRefreshes(value => value + 1); setCopied(true); };
  const snapshot = { workspace: { changeVersion: 7 }, package: { visualAssets: copied ? [{ key: 'factory.motor-001' }] : [] } } as unknown as EngineeringSnapshot;
  return <>
    <FactoryArtworkLibrary snapshot={snapshot} locale="pt-BR" onApplied={applied}/>
    <DynamoAuthoringCatalogProvider definitions={[]} commands={[]} visualAssets={[]}
      tags={[{ id: tagId, path: 'Plant.Running', name: 'Motor em operação', dataType: 'Boolean' }]} onApplied={applied}>
      <DynamoScriptWizard screenId="15000000-0000-0000-0000-000000000012" objectId="15000000-0000-0000-0000-000000000013"
        parameter={{ key: 'runningSignal', name: 'Running', kind: 'ValueSource', valueSourceType: 'Boolean', version: 1 }} disabled={false}
        sources={[
          { family: 'clientMemory', label: 'Saída booleana', dataType: 'Boolean', target: 'Client.Run', tagReference: { tagId: outputId } },
          { family: 'clientMemory', label: 'Saída numérica incompatível', dataType: 'Double', target: 'Client.Number', tagReference: { tagId: '15000000-0000-0000-0000-000000000014' } }
        ] as VisualEditorBindingSourceCatalogItem[]}
        onSet={value => setBinding(JSON.stringify(value))}/>
    </DynamoAuthoringCatalogProvider>
    <output data-testid="refreshes">{refreshes}</output><output data-testid="output-binding">{binding}</output>
  </>;
}
function Gallery() {
  const [definitions, setDefinitions] = useState<DynamoEngineering[]>([]);
  const [stage, setStage] = useState(0);
  const [runtime, setRuntime] = useState(false);
  useEffect(() => { void fetch('/review/catalog').then(response => response.json()).then(result => setDefinitions(result.dynamos.map(normalizeDynamoDefinitionParameterContract))); }, []);
  const instances: VisualElementEngineering[] = definitions.map((definition, index) => ({
    id: `gallery-${index}`, key: definition.key, type: 'dynamo', dynamoKey: definition.key,
    properties: { x: (index % 6) * 260 + 10, y: Math.floor(index / 6) * 200 + 10, width: 230, height: 170 },
    dynamoParameters: [{ key: 'animationEnabled', kind: 'Boolean', value: false }, { key: 'fixedState', kind: 'Number', value: stage }]
  }));
  const elements = runtime ? expandRuntimeDynamoVisuals(instances, definitions) : instances;
  return <><h1>26 dínamos do catálogo integrado</h1><label>Estado fixo<select aria-label="Gallery state" value={stage} onChange={e => setStage(Number(e.target.value))}>{[0,1,2,3,4].map(value => <option key={value}>{value}</option>)}</select></label>
    <label>Runtime projection<input aria-label="Runtime projection" type="checkbox" checked={runtime} onChange={e => setRuntime(e.target.checked)}/></label>
    <output data-testid="expanded-count">{elements.filter(isExpandedRuntimeDynamo).length}</output>
    <div style={{ position: 'relative', width: 1560, height: 1050 }}>
      <CanonicalVisualRenderer emptyLabel="Loading" liveBindings={false} showTechnicalFallbackText={false} dynamoDefinitions={definitions} visualAssetUrl={id => `/review/artwork/${id}`}
        elements={elements}/>
    </div></>;
}
function MediaComposition() {
  const [runtime, setRuntime] = useState(false);
  const [page, setPage] = useState(2);
  const [zoom, setZoom] = useState(125);
  const [toolbar, setToolbar] = useState(false);
  return <><label>Runtime preview<input aria-label="Runtime preview" type="checkbox" checked={runtime} onChange={e => setRuntime(e.target.checked)}/></label>
    <label>PDF page<input aria-label="PDF page" type="number" value={page} onChange={e => setPage(Number(e.target.value))}/></label>
    <label>PDF zoom<input aria-label="PDF zoom" type="number" value={zoom} onChange={e => setZoom(Number(e.target.value))}/></label>
    <label>PDF toolbar<input aria-label="PDF toolbar" type="checkbox" checked={toolbar} onChange={e => setToolbar(e.target.checked)}/></label>
    <CanonicalVisualRenderer emptyLabel="Empty" liveBindings={false} operatorTimeRangeControls={runtime} visualAssetUrl={id => `/review/media/${id}`} elements={[
      { id: '15000000-0000-0000-0000-000000000020', key: 'Project PDF', type: 'core.pdfViewer', properties: { x: 10, y: 60, width: 600, height: 400, assetRef: { assetId: 'asset:15000000-0000-0000-0000-000000000021' }, pdfInitialPage: page, pdfZoom: zoom, pdfToolbarVisible: toolbar } },
      { id: '15000000-0000-0000-0000-000000000022', key: 'Project video', type: 'core.videoPlayer', properties: { x: 650, y: 60, width: 480, height: 270, assetRef: { assetId: 'asset:15000000-0000-0000-0000-000000000023' }, mediaMuted: true, mediaAutoplay: false, mediaControls: true } },
      { id: '15000000-0000-0000-0000-000000000024', key: 'Camera source', type: 'core.videoPlayer', properties: { x: 650, y: 360, width: 480, height: 270, mediaSourceId: '15000000-0000-0000-0000-000000000495', mediaAutoplay: false } }
    ]}/>
  </>;
}
const query = new URLSearchParams(location.search);
createRoot(document.getElementById('root')!).render(query.has('gallery') ? <Gallery/> : query.has('media') ? <MediaComposition/> : <AuthoringContracts/>);
