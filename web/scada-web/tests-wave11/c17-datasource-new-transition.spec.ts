import { expect, test } from '@playwright/test';
import {
  NEW_DATA_SOURCE_IDENTITY,
  dataSourceIdentity,
  draftForDataSourceSelection,
  switchDataSourceType,
  type DataSourceTypeDefinition
} from '../src/engineering/DataSourceCatalogEditor.logic';

test('New Data Source transition is isolated from the previously selected persisted Source', () => {
  const previous = {
    id: '40000000-0000-0000-0000-000000000001',
    key: 'builtin.simulation',
    name: 'Built-in Simulation',
    driver: 'builtin.simulation',
    enabled: true,
    metadata: { system: true, owner: 'platform' },
    settings: { inheritedSetting: 'must-not-leak' },
    secretReferences: { inheritedSecret: 'must-not-leak' }
  } as any;

  const fresh = draftForDataSourceSelection(NEW_DATA_SOURCE_IDENTITY, [previous]);
  expect(fresh).toEqual({
    key: '',
    name: '',
    driver: '',
    enabled: true,
    settings: {},
    secretReferences: {}
  });
  expect(fresh && 'id' in fresh).toBe(false);
  expect(fresh && 'metadata' in fresh).toBe(false);

  const memoryType: DataSourceTypeDefinition = {
    typeKey: 'builtin.memory.server',
    displayName: 'Server Memory',
    kind: 'Memory',
    capabilities: {
      supportsConnectionTest: false,
      supportsDiscovery: false,
      supportsBrowse: false,
      supportsFileImport: false,
      supportsReconcile: false,
      supportsSharedTransportInfrastructure: false
    },
    configurationSchema: {
      schemaId: 'builtin.memory.server',
      schemaVersion: 1,
      dataSourceFields: [
        {
          key: 'retention',
          valueKind: 'integer',
          required: false,
          displayName: 'Retention',
          defaultValue: '10',
          allowedValues: [],
          advanced: false
        },
        {
          key: 'credential',
          valueKind: 'secretReference',
          required: false,
          displayName: 'Credential',
          defaultValue: 'memory-default-secret',
          allowedValues: [],
          advanced: true
        }
      ],
      tagBindingFields: []
    }
  };

  const typed = switchDataSourceType(fresh!, memoryType);
  expect(typed.driver).toBe('builtin.memory.server');
  expect(typed.settings).toEqual({ retention: '10' });
  expect(typed.secretReferences).toEqual({ credential: 'memory-default-secret' });
  expect('id' in typed).toBe(false);
  expect('metadata' in typed).toBe(false);
  expect(typed.settings).not.toHaveProperty('inheritedSetting');
  expect(typed.secretReferences).not.toHaveProperty('inheritedSecret');

  const existing = draftForDataSourceSelection(dataSourceIdentity(previous), [previous]);
  expect(existing).toEqual(previous);
  expect(existing).not.toBe(previous);
  expect(existing?.id).toBe(previous.id);
  expect((existing as any)?.metadata).toEqual(previous.metadata);
});
