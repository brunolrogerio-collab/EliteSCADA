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
  useEffect(() => { void fetch('/review/catalog').then(response => response.json()).then(result => setDefinitions(result.dynamos.map(normalizeDynamoDefinitionParameterContract))); }, []);
  return <><h1>26 dínamos do catálogo integrado</h1><label>Estado fixo<select aria-label="Gallery state" value={stage} onChange={e => setStage(Number(e.target.value))}>{[0,1,2,3,4].map(value => <option key={value}>{value}</option>)}</select></label>
    <div style={{ position: 'relative', width: 1560, height: 1050 }}>
      <CanonicalVisualRenderer emptyLabel="Loading" liveBindings={false} showTechnicalFallbackText={false} dynamoDefinitions={definitions} visualAssetUrl={id => `/review/artwork/${id}`}
        elements={definitions.map((definition, index) => ({
          id: `gallery-${index}`, key: definition.key, type: 'dynamo', dynamoKey: definition.key,
          properties: { x: (index % 6) * 260 + 10, y: Math.floor(index / 6) * 200 + 10, width: 230, height: 170 },
          dynamoParameters: [{ key: 'animationEnabled', kind: 'Boolean', value: false }, { key: 'fixedState', kind: 'Number', value: stage }]
        }))}/>
    </div></>;
}
createRoot(document.getElementById('root')!).render(new URLSearchParams(location.search).has('gallery') ? <Gallery/> : <AuthoringContracts/>);
