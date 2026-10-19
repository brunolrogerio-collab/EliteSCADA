import React from 'react';
import { createRoot } from 'react-dom/client';
import type { VisualElementEngineering } from '../../src/engineering/types';
import { CanonicalVisualRenderer } from '../../src/engineering/visual-editor/CanonicalVisualRenderer';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../src/visual-runtime';
import type { VisualLiveScalarSample } from '../../src/engineering/visual-editor/visualEditorLiveValues';
import '../../src/styles.css';

const measurementTypes = [
  BUILTIN_VISUAL_OBJECT_TYPES.measurementDial,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementRing,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementBar,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementThermometer,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementBands,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget
] as const;

const elements: VisualElementEngineering[] = measurementTypes.map((type, index) => ({
  id: `measurement-${index}`,
  key: `Measurement ${index + 1}`,
  type,
  bindings: [{ key: VISUAL_PROPERTY_KEYS.value, kind: 'Tag', target: 'Area.Pressure', metadata: { engineeringUnit: 'bar' } }],
  properties: {
    [VISUAL_PROPERTY_KEYS.x]: 12,
    [VISUAL_PROPERTY_KEYS.y]: index * 120 + 12,
    [VISUAL_PROPERTY_KEYS.width]: 240,
    [VISUAL_PROPERTY_KEYS.height]: 100,
    [VISUAL_PROPERTY_KEYS.minimum]: 0,
    [VISUAL_PROPERTY_KEYS.maximum]: 100,
    [VISUAL_PROPERTY_KEYS.measurementUnit]: 'bar',
    [VISUAL_PROPERTY_KEYS.measurementTheme]: 'high-contrast',
    [VISUAL_PROPERTY_KEYS.measurementQualityVisible]: true,
    [VISUAL_PROPERTY_KEYS.measurementFreshnessVisible]: true,
    [VISUAL_PROPERTY_KEYS.measurementStaleAfterSeconds]: 30,
    ...(type === BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget ? { [VISUAL_PROPERTY_KEYS.measurementTarget]: 65 } : {}),
    ...(type === BUILTIN_VISUAL_OBJECT_TYPES.measurementBands ? {
      [VISUAL_PROPERTY_KEYS.measurementBandOneEnd]: 30,
      [VISUAL_PROPERTY_KEYS.measurementBandTwoEnd]: 70
    } : {})
  }
}));

const params = new URLSearchParams(window.location.search);
const persistedElements = readPersistedElements(params.get('elements'));
const renderedElements = applyHarnessOverrides(persistedElements ?? elements, params);
const qualityParameter = params.get('quality') ?? 'Good';
const numericQuality = qualityParameter.trim() !== '' && Number.isFinite(Number(qualityParameter))
  ? Number(qualityParameter)
  : qualityParameter;
const state = params.get('state');
const sample: VisualLiveScalarSample = Object.freeze({
  reference: 'Area.Pressure', value: 42.5, dataType: 'Number', quality: numericQuality,
  state, timestamp: '2999-01-01T00:00:00.000Z'
});
const bindingSamples = new Map<string, VisualLiveScalarSample>(collectBindingTargets(renderedElements).map(reference => [reference, { ...sample, reference }]));
const runtime = params.get('mode') === 'runtime';

createRoot(document.getElementById('root')!).render(
  <main style={{ position: 'relative', width: 300, height: 740 }}>
    <CanonicalVisualRenderer
      elements={renderedElements}
      emptyLabel="Empty"
      liveBindings={false}
      bindingSamples={bindingSamples}
      locale="en"
      operatorTimeRangeControls={runtime}
    />
  </main>
);

function readPersistedElements(value: string | null): VisualElementEngineering[] | null {
  if (!value) return null;
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed) ? parsed as VisualElementEngineering[] : null;
  } catch {
    return null;
  }
}

function collectBindingTargets(elements: readonly VisualElementEngineering[]): string[] {
  const references = new Set<string>();
  const visit = (element: VisualElementEngineering) => {
    for (const binding of element.bindings ?? []) {
      if (binding.key === VISUAL_PROPERTY_KEYS.value && binding.kind.toLowerCase() === 'tag' && binding.target.trim()) {
        references.add(binding.target);
      }
    }
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  return [...references];
}

function applyHarnessOverrides(elements: readonly VisualElementEngineering[], parameters: URLSearchParams): VisualElementEngineering[] {
  const theme = parameters.get('theme');
  const unit = parameters.get('unit');
  const trackColor = parameters.get('trackColor');
  const fillColor = parameters.get('fillColor');
  const bandOneEnd = readFiniteNumber(parameters.get('bandOneEnd'));
  const bandTwoEnd = readFiniteNumber(parameters.get('bandTwoEnd'));
  return elements.map(element => ({
    ...element,
    properties: {
      ...element.properties,
      ...(theme ? { [VISUAL_PROPERTY_KEYS.measurementTheme]: theme } : {}),
      ...(unit ? { [VISUAL_PROPERTY_KEYS.measurementUnit]: unit } : {}),
      ...(trackColor ? { [VISUAL_PROPERTY_KEYS.trackColor]: trackColor } : {}),
      ...(fillColor ? { [VISUAL_PROPERTY_KEYS.thumbColor]: fillColor } : {}),
      ...(bandOneEnd !== null ? { [VISUAL_PROPERTY_KEYS.measurementBandOneEnd]: bandOneEnd } : {}),
      ...(bandTwoEnd !== null ? { [VISUAL_PROPERTY_KEYS.measurementBandTwoEnd]: bandTwoEnd } : {})
    }
  }));
}

function readFiniteNumber(value: string | null): number | null {
  if (value === null || value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
