import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import type { AlarmEngineering, VisualElementEngineering, VisualEngineeringPropertyValue } from '../src/engineering/types';
import type { VisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorContracts';
import { CheckBoxVisualElement, ComboBoxVisualElement } from '../src/engineering/visual-editor/ControlVisualElements';
import { PropertyInspector } from '../src/engineering/visual-editor/property-inspector/PropertyInspector';
import { visualTagSampleKey, type VisualDynamicSample } from '../src/engineering/visual-editor/visualDynamicRuntime';

const root = document.getElementById('root');
if (!root) throw new Error('Visual controls harness root not found.');

const params = new URLSearchParams(window.location.search);
const sourceMode = params.get('source') ?? 'tag';
const tagId = 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e';
const alarmDefinitionId = 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e';
const target = sourceMode === 'expression' ? 'Pump01.IsAvailable' : 'Plant.Pump01.Enabled';
const bindingKind = sourceMode === 'expression' ? 'Expression' : sourceMode === 'clientMemory' ? 'ClientMemory' : sourceMode === 'property' ? 'Property' : 'Tag';
const locale = params.get('locale') === 'en' || params.get('locale') === 'es' ? params.get('locale') as 'en' | 'es' : 'pt-BR';
const processValueWriteEnabled = params.get('processWrite') !== 'false';
const alarmShelveEnabled = params.get('alarmShelve') !== 'false';
const controlMode = params.get('control') === 'combo' ? 'combo' : 'checkBox';
const isComboBox = controlMode === 'combo';
const sampleValue: string | boolean = isComboBox
  ? (sourceMode === 'missing' ? 'REMOTE' : 'AUTO')
  : sourceMode === 'expression';

function Harness() {
  const [value, setValue] = useState<string | boolean>(sampleValue);
  const sample: VisualDynamicSample = {
    reference: target,
    ...(bindingKind === 'Tag' ? { tagId } : {}),
    value,
    dataType: isComboBox ? 'Enum' : 'Boolean',
    quality: sourceMode === 'bad' ? 'Bad' : 0,
    timestamp: new Date().toISOString(),
    readOnly: sourceMode === 'readOnly'
  };
  const element: VisualElementEngineering = {
    id: `${controlMode}-test`, key: isComboBox ? 'pump-mode' : 'pump-available', type: isComboBox ? 'core.comboBox' : 'core.checkBox',
    bindings: sourceMode === 'suppression' ? [] : [{
      key: isComboBox ? 'text' : 'checked', kind: bindingKind, target,
      direction: bindingKind === 'Tag' && sourceMode !== 'readOnly' ? 'readWrite' : 'read',
      ...(bindingKind === 'Tag' ? { tagReference: { tagId } } : {})
    }],
    ...(isComboBox ? { properties: {
      comboBoxOptions: [
        { value: 'AUTO', label: 'Automático' },
        { value: 'MANUAL', label: 'Manual' }
      ]
    } } : null),
    ...(sourceMode === 'suppression' ? { properties: {
      checkBoxConfig: { mode: 'alarmSuppression', alarmDefinitionId, reasonCode: 'maintenance', duration: '4h' }
    } } : {})
  };
  const values: Readonly<Record<string, VisualEngineeringPropertyValue>> = {
    checked: false, interactionEnabled: true, enabled: true,
    text: isComboBox ? 'Modo da bomba' : 'Bomba disponível'
  };
  const samples = new Map([[bindingKind === 'Tag' ? visualTagSampleKey(tagId) : target, sample]]);
  const onTagWrite = async (writtenTagId: string, nextValue: string | number | boolean) => {
    const state = window as typeof window & { __controlWrites?: Array<{ tagId: string; value: unknown }> };
    state.__controlWrites ??= [];
    state.__controlWrites.push({ tagId: writtenTagId, value: nextValue });
    setValue(nextValue as string | boolean);
  };
  const onMutationIntent = (intent: VisualEditorMutationIntent) => {
    const state = window as typeof window & { __controlMutations?: VisualEditorMutationIntent[] };
    state.__controlMutations ??= [];
    state.__controlMutations.push(intent);
  };
  const alarms: readonly AlarmEngineering[] = [
    { id: alarmDefinitionId, name: 'Motor · Pressão alta', type: 'High', priority: 'High' },
    { id: '4d4a1f16-1d42-41d4-b6a9-7ceadf4a4b10', name: 'Motor · Temperatura alta', type: 'High', priority: 'High' }
  ];

  if (params.get('surface') === 'inspector') {
    return <PropertyInspector selectedElements={[element]} alarms={alarms} onMutationIntent={onMutationIntent} showEvents={false} />;
  }

  return <main>
    {isComboBox
      ? <ComboBoxVisualElement
          element={element} values={values} liveSamples={samples} style={{ width: 240, height: 48 }}
          locale={locale} processValueWriteEnabled={processValueWriteEnabled} alarmShelveEnabled={alarmShelveEnabled}
          onTagWrite={onTagWrite}
        />
      : <CheckBoxVisualElement
          element={element} values={values} liveSamples={samples} style={{ width: 240, height: 48 }}
          locale={locale} processValueWriteEnabled={processValueWriteEnabled} alarmShelveEnabled={alarmShelveEnabled}
          onTagWrite={onTagWrite}
        />}
  </main>;
}

createRoot(root).render(<React.StrictMode><Harness /></React.StrictMode>);
