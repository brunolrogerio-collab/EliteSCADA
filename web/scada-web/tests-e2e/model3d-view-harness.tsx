import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { VisualEditorCanvas } from '../src/engineering/visual-editor/canvas/VisualEditorCanvas';
import { PropertyInspector } from '../src/engineering/visual-editor/property-inspector/PropertyInspector';
import { C07VisualEditorI18nProvider } from '../src/engineering/visual-editor/c07VisualEditorI18n';
import { applyVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import { Model3dVisualElement } from '../src/engineering/visual-editor/Model3dVisualElement';
import type { ScreenEngineering } from '../src/engineering/types';
import type { VisualEditorUiIntent } from '../src/engineering/visual-editor/visualEditorContracts';

const params = new URLSearchParams(location.search);
const root = document.getElementById('root');
if (!root) throw new Error('DEV-873 3D harness root missing.');
function sampleModelUrl() {
  const coordinates = new Float32Array([-2, -1, 0, 2, -1, 0, 0, 3, 0]);
  const bytes = new Uint8Array(coordinates.buffer);
  const binary = btoa(String.fromCharCode(...bytes));
  const gltf = {
    asset: { version: '2.0' },
    buffers: [{ byteLength: bytes.length, uri: 'data:application/octet-stream;base64,' + binary }],
    bufferViews: [{ buffer: 0, byteOffset: 0, byteLength: bytes.length, target: 34962 }],
    accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [-2, -1, 0], max: [2, 3, 0] }],
    meshes: [{ primitives: [{ attributes: { POSITION: 0 }, mode: 4 }] }],
    nodes: [{ mesh: 0, translation: [40, -20, 8] }],
    scenes: [{ nodes: [0] }],
    scene: 0
  };
  return 'data:model/gltf+json;base64,' + btoa(JSON.stringify(gltf));
}
const MODEL_URL = sampleModelUrl();

function EditorHarness() {
  const [screen, setScreen] = useState<ScreenEngineering>(() => ({
    id: 'screen-model-873', key: 'model-screen', name: 'Model screen',
    elements: [{ id: 'model-873', type: 'core.model3d', key: 'model-873', properties: {
      x: 40, y: 50, width: 240, height: 180, rotation: 18
    } }]
  }));
  const [selection, setSelection] = useState<string[]>(['model-873']);
  const [viewport, setViewport] = useState({ zoom: 1, panX: 0, panY: 0 });
  const element = screen.elements![0];
  const onUiIntent = (intent: VisualEditorUiIntent) => {
    if (intent.kind === 'selection.change') setSelection([...intent.objectIds]);
    else setViewport(intent.viewport);
  };
  const onMutationIntent: React.ComponentProps<typeof VisualEditorCanvas>['onMutationIntent'] =
    intent => setScreen(current => applyVisualEditorMutationIntent(current, intent));
  return <main>
    <output data-testid="model3d-draft">{JSON.stringify(element.properties)}</output>
    <div className="editor-harness-surface">
      <VisualEditorCanvas screen={screen} selectedObjectIds={selection} viewport={viewport}
        onUiIntent={onUiIntent} onMutationIntent={onMutationIntent} locale="pt-BR" />
    </div>
    <C07VisualEditorI18nProvider locale="pt-BR">
      <PropertyInspector selectedElements={[element]} onMutationIntent={onMutationIntent} showEvents={false} />
    </C07VisualEditorI18nProvider>
  </main>;
}

function RuntimeHarness() {
  const [mount, setMount] = useState(0);
  const [size, setSize] = useState({ width: 320, height: 220 });
  const [commands, setCommands] = useState(0);
  const allowed = params.get('allow') === 'true';
  return <main>
    <button data-testid="runtime-reopen" onClick={() => setMount(n => n + 1)}>Reopen screen</button>
    <button data-testid="runtime-narrow" onClick={() => setSize({ width: 100, height: 500 })}>Narrow</button>
    <button data-testid="runtime-wide" onClick={() => setSize({ width: 600, height: 100 })}>Wide</button>
    <output data-testid="runtime-command-count">{commands}</output>
    <div style={{ ...size, border: '1px solid #333' }} onClick={() => setCommands(n => n + 1)}>
      <Model3dVisualElement key={mount} src={MODEL_URL} label="Model 873" viewYawDeg={40.1}
        viewPitchDeg={25.8} runtimeInteractionEnabled={allowed} />
    </div>
  </main>;
}
createRoot(root).render(params.get('mode') === 'runtime' ? <RuntimeHarness/> : <EditorHarness/>);
