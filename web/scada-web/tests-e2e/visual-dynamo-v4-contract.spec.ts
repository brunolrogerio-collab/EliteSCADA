import { expect, test } from '@playwright/test';
import type { EngineeringPackageView, VisualAssetEngineering, VisualElementEngineering } from '../src/engineering/types';
import { replaceDynamoInPackage } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import { listDynamicPropertyDestinations } from '../src/engineering/visual-editor/dynamic-property-editor/visualDynamicAuthoringModel';
import { resolveVisualDynamicState } from '../src/engineering/visual-editor/visualDynamicRuntime';
import { projectDynamoRuntimeElements } from '../src/runtime/visual-navigation/dynamoRuntimeBindingProjection';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterValueEngineering
} from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';

const svgElement: VisualElementEngineering = {
  id: '71000000-0000-0000-0000-000000000001',
  key: 'pump-artwork',
  type: 'core.svgSymbol',
  properties: {
    x: 0,
    y: 0,
    width: 120,
    height: 90,
    fillColor: '#777777',
    strokeColor: '#222222',
    strokeWidth: 1,
    opacity: 1,
    assetRef: { assetId: 'asset:72000000-0000-0000-0000-000000000001' }
  },
  bindings: [
    {
      key: 'fillColor',
      kind: 'Property',
      target: '{dynamoParameter:runningColor}',
      metadata: { dynamoParameter: 'runningColor' }
    },
    {
      key: 'strokeWidth',
      kind: 'Property',
      target: '{dynamoParameter:lineWidth}',
      metadata: { dynamoParameter: 'lineWidth' }
    },
    {
      key: 'strokeColor',
      kind: 'Tag',
      target: '{dynamoParameter:strokeTag}',
      metadata: { dynamoParameter: 'strokeTag' }
    }
  ]
};

test('SVG-backed Dynamo runtime projects scalar public parameters without a parallel renderer', () => {
  const parameters = new Map<string, DynamoParameterValueEngineering>([
    ['runningColor', { key: 'runningColor', kind: 'String', value: '#00AA00', version: 1 }],
    ['lineWidth', { key: 'lineWidth', kind: 'Number', value: 4, version: 1 }],
    ['strokeTag', {
      key: 'strokeTag',
      kind: 'TagReference',
      tagReference: { tagId: '73000000-0000-0000-0000-000000000001' },
      version: 1
    }]
  ]);

  const projected = projectDynamoRuntimeElements([svgElement], parameters, null);
  const symbol = projected[0]!;

  expect(symbol.type).toBe('core.svgSymbol');
  expect(symbol.properties?.fillColor).toBe('#00AA00');
  expect(symbol.properties?.strokeWidth).toBe(4);
  expect(symbol.properties?.assetRef).toEqual(svgElement.properties?.assetRef);
  expect(symbol.bindings).toHaveLength(1);
  expect(symbol.bindings?.[0]).toMatchObject({
    key: 'strokeColor',
    kind: 'Tag',
    tagReference: { tagId: '73000000-0000-0000-0000-000000000001' }
  });
});

test('SVG scalar paint properties are offered through the canonical dynamic-property editor', () => {
  const destinations = listDynamicPropertyDestinations(svgElement);
  const byKey = new Map(destinations.map(destination => [destination.propertyKey, destination]));

  expect(byKey.get('fillColor')?.sourceModes).toContain('DirectBinding');
  expect(byKey.get('strokeColor')?.sourceModes).toContain('DirectBinding');
  expect(byKey.get('strokeWidth')?.sourceModes).toContain('DirectBinding');
  expect(byKey.get('opacity')?.sourceModes).toContain('DirectBinding');
  expect(byKey.get('assetRef')).toBeUndefined();
});


test('R1 scalar Dynamo parameters drive BooleanCondition PropertyMap and AnalogFill without fake TAG samples', () => {
  const source = (key: string, valueType: 'Boolean' | 'Number') => ({
    kind: 'Tag' as const,
    valueType,
    target: `{dynamoParameter:${key}}`,
    version: 1
  });

  const element: VisualElementEngineering = {
    id: '74000000-0000-0000-0000-000000000001',
    key: 'dynamic-body',
    type: 'core.rectangle',
    properties: {
      x: 0, y: 0, width: 100, height: 100,
      visible: true,
      fillColor: '#777777',
      opacity: 1
    },
    booleanConditions: [{
      propertyKey: 'visible',
      kind: 'Direct',
      source: source('enabled', 'Boolean'),
      negate: false,
      version: 1
    }],
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: source('state', 'Number'),
      rules: [
        { value: '#777777', minimum: 0, maximum: 1, minimumInclusive: true, maximumInclusive: false },
        { value: '#00AA00', minimum: 1, maximum: 2, minimumInclusive: true, maximumInclusive: true }
      ],
      fallback: '#777777',
      version: 1
    }],
    analogFill: {
      source: source('level', 'Number'),
      inputMinimum: 0,
      inputMaximum: 100,
      fillColor: '#0088FF',
      clamp: true,
      invertScale: false,
      direction: 'BottomToTop',
      version: 1
    }
  };

  const parameters = new Map<string, DynamoParameterValueEngineering>([
    ['enabled', { key: 'enabled', kind: 'Boolean', value: false, version: 1 }],
    ['state', { key: 'state', kind: 'Number', value: 1.5, version: 1 }],
    ['level', { key: 'level', kind: 'Number', value: 50, version: 1 }]
  ]);
  const [projected] = projectDynamoRuntimeElements([element], parameters, null);

  expect(projected.booleanConditions?.[0]?.source.projectedValue).toBe(false);
  expect(projected.propertyMaps?.[0]?.source.projectedValue).toBe(1.5);
  expect(projected.analogFill?.source.projectedValue).toBe(50);

  const resolved = resolveVisualDynamicState(projected, projected.properties ?? {}, new Map());
  expect(resolved.values.visible).toBe(false);
  expect(resolved.values.fillColor).toBe('#00AA00');
  expect(resolved.analogFill?.percent).toBeCloseTo(0.5, 5);
  expect(resolved.diagnostics).toEqual([]);
});

test('R1 TagReference dynamic parameter stays a live canonical TAG source', () => {
  const element: VisualElementEngineering = {
    id: '74000000-0000-0000-0000-000000000002',
    key: 'tag-driven',
    type: 'core.rectangle',
    properties: { x: 0, y: 0, width: 10, height: 10, fillColor: '#777777' },
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: {
        kind: 'Tag',
        valueType: 'Number',
        target: '{dynamoParameter:stateTag}',
        version: 1
      },
      rules: [{ value: '#00AA00', minimum: 1, maximum: 2 }],
      version: 1
    }]
  };
  const tagId = '74000000-0000-0000-0000-000000000003';
  const parameters = new Map<string, DynamoParameterValueEngineering>([
    ['stateTag', { key: 'stateTag', kind: 'TagReference', tagReference: { tagId }, version: 1 }]
  ]);
  const [projected] = projectDynamoRuntimeElements([element], parameters, null);
  expect(projected.propertyMaps?.[0]?.source).toMatchObject({
    target: '{dynamoParameter:stateTag}',
    tagReference: { tagId }
  });
  expect(projected.propertyMaps?.[0]?.source.projectedValue).toBeUndefined();
});

test('R2 semantic SVG slot destinations come only from canonical asset metadata and resolve dynamically', () => {
  const asset: VisualAssetEngineering = {
    id: '72000000-0000-0000-0000-000000000001',
    key: 'asset.semantic',
    name: 'Semantic',
    originalFileName: 'semantic.svg',
    mediaType: 'image/svg+xml',
    byteLength: 10,
    sha256: 'abc',
    metadata: {
      'svg.slots.v1': JSON.stringify([
        { name: 'body', fill: true, stroke: false, strokeWidth: false },
        { name: 'outline', fill: false, stroke: true, strokeWidth: true }
      ])
    }
  };
  const destinations = listDynamicPropertyDestinations(svgElement, asset);
  const keys = destinations.map(destination => destination.propertyKey);
  expect(keys).toContain('svg.slot.body.fill');
  expect(keys).toContain('svg.slot.outline.stroke');
  expect(keys).toContain('svg.slot.outline.strokeWidth');
  expect(keys).not.toContain('svg.slot.body.stroke');

  const element: VisualElementEngineering = {
    ...svgElement,
    bindings: [],
    propertyMaps: [{
      propertyKey: 'svg.slot.body.fill',
      source: {
        kind: 'Tag',
        valueType: 'Number',
        target: '{dynamoParameter:state}',
        version: 1
      },
      rules: [{ value: '#00AA00', minimum: 1, maximum: 2 }],
      fallback: '#777777',
      version: 1
    }, {
      propertyKey: 'svg.slot.outline.strokeWidth',
      source: {
        kind: 'Tag',
        valueType: 'Number',
        target: '{dynamoParameter:lineWidthState}',
        version: 1
      },
      rules: [{ value: 4, minimum: 1, maximum: 2 }],
      fallback: 1,
      version: 1
    }]
  };
  const parameters = new Map<string, DynamoParameterValueEngineering>([
    ['state', { key: 'state', kind: 'Number', value: 1.5, version: 1 }],
    ['lineWidthState', { key: 'lineWidthState', kind: 'Number', value: 1.5, version: 1 }]
  ]);
  const [projected] = projectDynamoRuntimeElements([element], parameters, null);
  const resolved = resolveVisualDynamicState(projected, projected.properties ?? {}, new Map());
  expect(resolved.values['svg.slot.body.fill']).toBe('#00AA00');
  expect(resolved.values['svg.slot.outline.strokeWidth']).toBe(4);
  expect(resolved.diagnostics).toEqual([]);
});

test('R3 portable Command parameter resolves definition action to canonical project Command identity', () => {
  const commandId = '75000000-0000-0000-0000-000000000001';
  const element: VisualElementEngineering = {
    id: '75000000-0000-0000-0000-000000000002',
    key: 'start-button',
    type: 'core.button',
    properties: { x: 0, y: 0, width: 80, height: 30, text: 'Start' },
    actions: [{
      eventKey: 'click',
      kind: 'ExecuteCommand',
      commandParameterKey: 'startCommand',
      version: 1
    }]
  };
  const parameters = new Map<string, DynamoParameterValueEngineering>([
    ['startCommand', { key: 'startCommand', kind: 'Command', commandId, version: 1 }]
  ]);
  const [projected] = projectDynamoRuntimeElements([element], parameters, null);
  expect(projected.actions?.[0]).toMatchObject({
    eventKey: 'click',
    kind: 'ExecuteCommand',
    commandId,
    commandParameterKey: null
  });
});

test('new Dynamo authoring persists the existing public parameter contract and SVG composition', () => {
  const parameters: readonly DynamoParameterDefinitionEngineering[] = Object.freeze([
    { key: 'runningColor', kind: 'String', required: false, defaultValue: '#777777', version: 1 },
    { key: 'strokeTag', kind: 'TagReference', required: true, version: 1 }
  ]);
  const model = ({
    dynamos: [],
    visualAssets: []
  } as unknown) as EngineeringPackageView;
  const draft = {
    key: 'dynamo.svg-pump',
    name: 'SVG Pump',
    route: null,
    elements: [svgElement],
    properties: {},
    context: {},
    metadata: {}
  };

  const next = replaceDynamoInPackage(model, null, draft, parameters);
  const created = next.dynamos?.[0];

  expect(created?.key).toBe('dynamo.svg-pump');
  expect(created?.parameters).toEqual(parameters);
  expect(created?.elements?.[0]?.type).toBe('core.svgSymbol');
  expect(created?.elements?.[0]?.properties?.assetRef).toEqual(svgElement.properties?.assetRef);
});

test('Dynamo authoring stays on canonical editor and renderer surfaces', async () => {
  const fs = await import('node:fs/promises');
  const workspace = await fs.readFile(
    new URL('../src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx', import.meta.url),
    'utf8'
  );
  const sidebar = await fs.readFile(
    new URL('../src/engineering/visual-editor/VisualEditorAuthoringSidebar.tsx', import.meta.url),
    'utf8'
  );
  const dynamoPalette = await fs.readFile(
    new URL('../src/engineering/visual-editor/DynamoLibraryPalette.tsx', import.meta.url),
    'utf8'
  );
  const renderer = await fs.readFile(
    new URL('../src/engineering/visual-editor/CanonicalVisualRenderer.tsx', import.meta.url),
    'utf8'
  );

  expect(workspace).toContain('<DynamoDefinitionParametersEditor');
  expect(workspace).toContain('dynamoParameterKey: parameter.key');
  expect(sidebar).toContain('<DynamoLibraryPalette');
  expect(dynamoPalette).toContain("kind: 'dynamo.add'");
  expect(renderer).toContain('<SvgSymbolVisualElement');
  expect(workspace).not.toContain('DynamoSvgRenderer');
});
