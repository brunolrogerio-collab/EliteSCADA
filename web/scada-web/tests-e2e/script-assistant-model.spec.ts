import { expect, test } from '@playwright/test';
import type { ClientMemorySourceDefinition } from '../src/runtime/clientMemory';
import type { EngineeringPackageView } from '../src/engineering/types';
import {
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES
} from '../src/python-runtime/pythonRuntimeContracts';
import {
  buildScriptAssistantCatalog,
  filterScriptAssistantCatalog
} from '../src/engineering/scripts/scriptAssistantModel';

const writableTagId = '11111111-1111-1111-1111-111111111111';
const readOnlyTagId = '22222222-2222-2222-2222-222222222222';
const dataSourceId = '33333333-3333-3333-3333-333333333333';

const engineeringPackage = {
  schema: 'elite-scada.project',
  schemaVersion: 1,
  exportedAt: '2026-09-03T12:00:00Z',
  tags: [
    {
      id: writableTagId,
      name: 'Level',
      path: 'Plant.Tank.Level',
      dataType: 'Double',
      source: 'opc-main',
      dataSourceId,
      engineeringUnit: '%',
      description: 'Tank level',
      readOnly: false
    },
    {
      id: readOnlyTagId,
      name: 'Status',
      path: 'Plant.Tank.Status',
      dataType: 'Boolean',
      source: 'opc-main',
      dataSourceId,
      readOnly: true
    }
  ],
  alarms: [],
  dataSources: [
    {
      id: dataSourceId,
      key: 'opc-main',
      name: 'Main OPC UA',
      driver: 'OpcUa',
      enabled: true
    }
  ],
  dynamos: [
    {
      id: 'dynamo-pump',
      key: 'pump',
      name: 'Pump',
      parameters: [
        { key: 'running', kind: 'Boolean', required: true },
        { key: 'equipmentPath', kind: 'EquipmentPath' }
      ],
      elements: [
        { id: 'internal-shape', key: 'internal-shape', type: 'core.rectangle' }
      ]
    }
  ],
  screens: [
    {
      id: 'screen-main',
      key: 'main',
      name: 'Main',
      route: '/main',
      elements: [
        {
          id: 'button-1',
          key: 'StartButton',
          type: 'core.button',
          properties: {
            visible: true,
            text: 'Start',
            unknownLegacyProperty: 'must-not-surface'
          }
        },
        {
          id: 'pump-instance-1',
          key: 'Pump01',
          type: 'core.group',
          dynamoKey: 'pump',
          dynamoParameters: [
            { key: 'running', kind: 'Boolean', value: true }
          ],
          children: [
            { id: 'leaked-child', key: 'leaked-child', type: 'core.rectangle' }
          ]
        }
      ]
    }
  ],
  popups: [
    {
      id: 'popup-detail',
      key: 'detail',
      name: 'Detail',
      elements: [
        { id: 'detail-text', key: 'Title', type: 'core.text', properties: { text: 'Detail' } }
      ]
    }
  ]
} as unknown as EngineeringPackageView;

const clientMemorySources: ClientMemorySourceDefinition[] = [
  {
    dataSourceKey: 'builtin.memory.client',
    name: 'Client Memory',
    tags: [
      {
        id: 'client-mode',
        name: 'Mode',
        path: 'Client.Mode',
        dataType: 'String',
        readOnly: false,
        initialValue: 'Auto'
      }
    ]
  }
];

test('Script Assistant consumes stable TAG identity and Source GUID metadata without generating free-text TAG references', () => {
  const catalog = buildScriptAssistantCatalog(engineeringPackage, clientMemorySources);
  const writable = catalog.tags.find(tag => tag.id === writableTagId)!;
  const readOnly = catalog.tags.find(tag => tag.id === readOnlyTagId)!;

  expect(writable.canonicalReference).toBe(writableTagId);
  expect(writable.dataSourceId).toBe(dataSourceId);
  expect(writable.sourceIdentityStatus).toBe('stable');
  expect(writable.driver).toBe('OpcUa');
  expect(writable.snippets.find(snippet => snippet.kind === 'tag-read')?.code).toContain(writableTagId);
  expect(writable.snippets.find(snippet => snippet.kind === 'tag-read')?.code).not.toContain('Plant.Tank.Level');
  expect(writable.snippets.find(snippet => snippet.kind === 'tag-write')).toMatchObject({ enabled: true });
  expect(readOnly.snippets.find(snippet => snippet.kind === 'tag-write')).toMatchObject({
    enabled: false,
    reason: 'TAG is read-only.'
  });
});

test('visual object browser exposes only canonical schema properties and keeps Dynamo internals encapsulated', () => {
  const catalog = buildScriptAssistantCatalog(engineeringPackage, clientMemorySources);
  const screen = catalog.screens[0];
  const button = screen.objects.find(object => object.key === 'StartButton')!;
  const dynamo = screen.objects.find(object => object.key === 'Pump01')!;

  expect(button.events).toEqual(['Click']);
  expect(button.properties.some(property => property.key === 'visible')).toBe(true);
  expect(button.properties.some(property => property.key === 'text')).toBe(true);
  expect(button.properties.some(property => property.key === 'unknownLegacyProperty')).toBe(false);

  const visible = button.properties.find(property => property.key === 'visible')!;
  expect(visible).toMatchObject({
    type: 'boolean',
    currentValue: true,
    runtimeReadable: true,
    runtimeWritable: true
  });
  expect(visible.snippets.find(snippet => snippet.kind === 'visual-property-write')?.code)
    .toContain('visual_property_write');
  expect(visible.snippets.find(snippet => snippet.kind === 'visual-property-write')?.code)
    .toContain('button-1');

  expect(dynamo.publicDynamoParameters.map(parameter => parameter.key)).toEqual(['running', 'equipmentPath']);
  expect(dynamo.children).toEqual([]);
  expect(JSON.stringify(dynamo)).not.toContain('internal-shape');
  expect(JSON.stringify(dynamo)).not.toContain('leaked-child');
});

test('Client Memory remains a separate authority with its own read/write snippets', () => {
  const catalog = buildScriptAssistantCatalog(engineeringPackage, clientMemorySources);
  const memory = catalog.clientMemory[0];

  expect(memory.sourceKey).toBe('builtin.memory.client');
  expect(memory.path).toBe('Client.Mode');
  expect(memory.snippets.find(snippet => snippet.kind === 'client-memory-read')?.code)
    .toContain('client_memory_read');
  expect(memory.snippets.find(snippet => snippet.kind === 'client-memory-write')?.code)
    .toContain('client_memory_write');
  expect(catalog.tags.some(tag => tag.path === memory.path)).toBe(false);
});

test('capability catalog advertises only official product capabilities while reserved host operations remain protocol-only', () => {
  const catalog = buildScriptAssistantCatalog(engineeringPackage, clientMemorySources);
  const advertised = catalog.capabilities.map(item => item.capability);

  expect(advertised).toEqual([...CLIENT_VISUAL_PYTHON_CAPABILITIES]);
  expect(advertised).toContain('tag.write');
  expect(catalog.capabilities.find(item => item.capability === 'tag.write')?.pythonApi).toBe('elite_scada.tag_write');
  expect(advertised).not.toContain('backendOperation.request');
  expect(CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES).toContain('backendOperation.request');

  const filtered = filterScriptAssistantCatalog(catalog, 'visible');
  expect(filtered.screens).toHaveLength(1);
  expect(filtered.screens[0].objects).toHaveLength(1);
  expect(filtered.screens[0].objects[0].key).toBe('StartButton');
  expect(filtered.screens[0].objects[0].properties.map(property => property.key)).toEqual(['visible']);
});
