import { expect, test } from '@playwright/test';
import type { EngineeringPackageView, VisualElementEngineering } from '../src/engineering/types';
import { replaceDynamoInPackage } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import { listDynamicPropertyDestinations } from '../src/engineering/visual-editor/dynamic-property-editor/visualDynamicAuthoringModel';
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
