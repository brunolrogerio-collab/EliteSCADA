import { expect, test } from '@playwright/test';
import {
  projectCanonicalVisualElementForRuntime,
  projectCanonicalVisualTreeForRuntime
} from '../src/engineering/visualEngineeringRuntimeAdapter';
import {
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  VisualPropertyContractError
} from '../src/visual-runtime';

function rectangleSchema() {
  return new VisualObjectPropertySchema('core.rectangle', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.y,
    VISUAL_PROPERTY_KEYS.visible,
    VISUAL_PROPERTY_KEYS.fillColor
  ]);
}

test('canonical visual element projects typed properties and bindings through the shared schema', () => {
  const projected = projectCanonicalVisualElementForRuntime({
    id: '550e8400-e29b-41d4-a716-446655440001',
    key: 'pump01',
    type: 'core.rectangle',
    properties: {
      x: 12.5,
      visible: false,
      fillColor: '#11223344'
    },
    bindings: [
      { key: 'y', kind: 'tag', target: 'Plant/Pump01/Position' }
    ],
    metadata: { area: 'raw-water' }
  }, rectangleSchema());

  expect(projected).toMatchObject({
    objectId: '550e8400-e29b-41d4-a716-446655440001',
    key: 'pump01',
    objectType: 'core.rectangle',
    parentObjectId: null,
    baseProperties: {
      x: 12.5,
      visible: false,
      fillColor: '#11223344'
    },
    bindings: [
      {
        propertyKey: 'y',
        sourceKind: 'binding',
        sourceReference: 'Plant/Pump01/Position'
      }
    ],
    metadata: { area: 'raw-water' }
  });
});

test('canonical visual tree projects parent identity before children', () => {
  const definitions = projectCanonicalVisualTreeForRuntime([
    {
      id: '550e8400-e29b-41d4-a716-446655440010',
      key: 'group01',
      type: 'core.rectangle',
      children: [
        {
          id: '550e8400-e29b-41d4-a716-446655440011',
          key: 'child01',
          type: 'core.rectangle'
        }
      ]
    }
  ], () => rectangleSchema());

  expect(definitions.map(definition => [definition.objectId, definition.parentObjectId])).toEqual([
    ['550e8400-e29b-41d4-a716-446655440010', null],
    ['550e8400-e29b-41d4-a716-446655440011', '550e8400-e29b-41d4-a716-446655440010']
  ]);
});

test('canonical Runtime projection fails closed for missing IDs and unsupported binding kinds', () => {
  expect(() => projectCanonicalVisualElementForRuntime({
    key: 'legacyObject',
    type: 'core.rectangle'
  }, rectangleSchema())).toThrow(VisualPropertyContractError);

  expect(() => projectCanonicalVisualElementForRuntime({
    id: '550e8400-e29b-41d4-a716-446655440020',
    key: 'object01',
    type: 'core.rectangle',
    bindings: [
      { key: 'x', kind: 'mystery', target: 'Plant/Value' }
    ]
  }, rectangleSchema())).toThrow(/not supported/);
});
