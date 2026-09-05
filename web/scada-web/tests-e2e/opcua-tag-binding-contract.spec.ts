import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  tagBindingSchemaIdentity,
  type DataSourceTypeDefinition
} from '../src/engineering/DataSourceCatalogEditor.logic';

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

test('OPC UA binding schema identity is independent from Data Source configuration identity', () => {
  const identity = tagBindingSchemaIdentity(opcUaType());

  expect(identity).toEqual({
    schemaId: 'tag.binding.schema',
    schemaVersion: 3
  });
});

test('OPC UA browser builder consumes canonical TAG schema and preserves settings only on matching identity', async () => {
  const source = await readFile(
    new URL('../src/engineering/OpcUaTagBrowser.tsx', import.meta.url),
    'utf8');
  const start = source.indexOf('export function buildOpcUaTagBinding(');
  const end = source.indexOf('function buildBulkCandidate(', start);
  expect(start).toBeGreaterThanOrEqual(0);
  expect(end).toBeGreaterThan(start);
  const builder = source.slice(start, end);

  expect(builder).toContain('const bindingSchema = tagBindingSchemaIdentity(type);');
  expect(builder).toContain('current?.schemaId === bindingSchema.schemaId');
  expect(builder).toContain("new Set(['samplinginterval', 'queuesize', 'discardoldest'])");
  expect(builder).toContain('schemaId: bindingSchema.schemaId');
  expect(builder).toContain('schemaVersion: bindingSchema.schemaVersion');
  expect(builder).toContain('portableAddress');
});
