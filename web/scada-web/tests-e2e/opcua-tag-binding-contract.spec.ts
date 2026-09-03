import { expect, test } from '@playwright/test';
import type { DataSourceTypeDefinition } from '../src/engineering/DataSourceCatalogEditor.logic';
import { buildOpcUaTagBinding } from '../src/engineering/OpcUaTagBrowser';

const capabilities = {
  supportsConnectionTest: true,
  supportsDiscovery: true,
  supportsBrowse: true,
  supportsFileImport: false,
  supportsReconcile: true,
  supportsSharedTransportInfrastructure: false
};

function opcUaType(): DataSourceTypeDefinition {
  return {
    typeKey: 'opc-ua',
    displayName: 'OPC UA',
    kind: 'communication',
    capabilities,
    configurationSchema: {
      schemaId: 'source.configuration.schema',
      schemaVersion: 7,
      dataSourceFields: [],
      tagBindingFields: [
        {
          key: 'samplingInterval',
          valueKind: 'duration',
          required: false,
          displayName: 'Sampling interval',
          defaultValue: '00:00:01',
          allowedValues: [],
          advanced: true
        },
        {
          key: 'queueSize',
          valueKind: 'integer',
          required: false,
          displayName: 'Queue size',
          defaultValue: '1',
          allowedValues: [],
          advanced: true
        },
        {
          key: 'discardOldest',
          valueKind: 'boolean',
          required: false,
          displayName: 'Discard oldest',
          defaultValue: 'true',
          allowedValues: [],
          advanced: true
        }
      ]
    },
    tagBindingSchemaId: 'tag.binding.schema',
    tagBindingSchemaVersion: 3
  };
}

test('OPC UA binding uses backend TAG schema identity rather than Data Source configuration identity', () => {
  const binding = buildOpcUaTagBinding(opcUaType(), 'node=ns%3D2%3Bs%3DTemperature');

  expect(binding).not.toBeNull();
  expect(binding?.schemaId).toBe('tag.binding.schema');
  expect(binding?.schemaVersion).toBe(3);
  expect(binding?.settings).toEqual({
    samplingInterval: '00:00:01',
    queueSize: '1',
    discardOldest: 'true'
  });
});

test('OPC UA binding preserves existing settings only when the TAG schema identity matches', () => {
  const type = opcUaType();
  const portableAddress = 'node=ns%3D2%3Bs%3DPressure';

  const matching = buildOpcUaTagBinding(type, portableAddress, {
    contractVersion: 1,
    schemaId: 'tag.binding.schema',
    schemaVersion: 3,
    portableAddress,
    settings: { queueSize: '20' }
  });
  expect(matching?.settings?.queueSize).toBe('20');

  const sourceSchemaOnly = buildOpcUaTagBinding(type, portableAddress, {
    contractVersion: 1,
    schemaId: 'source.configuration.schema',
    schemaVersion: 7,
    portableAddress,
    settings: { queueSize: '99' }
  });
  expect(sourceSchemaOnly?.settings?.queueSize).toBe('1');
});
