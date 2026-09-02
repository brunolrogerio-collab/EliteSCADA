import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  buildDataSourceCandidate,
  incompatibleDataSourceConfiguration,
  newDataSourceDraft,
  removeIncompatibleDataSourceConfiguration,
  settingsForType,
  switchDataSourceType,
  validateDataSourceDraft,
  type DataSourceTypeDefinition
} from '../src/engineering/DataSourceCatalogEditor.logic';
import type { DataSourceEngineering, EngineeringPackageView } from '../src/engineering/types';

const modbus: DataSourceTypeDefinition = {
  typeKey: 'modbus.tcp',
  displayName: 'Modbus TCP',
  kind: 'communicationDriver',
  capabilities: {
    supportsConnectionTest: false,
    supportsDiscovery: false,
    supportsBrowse: false,
    supportsFileImport: false,
    supportsReconcile: false,
    supportsSharedTransportInfrastructure: false
  },
  configurationSchema: {
    schemaId: 'modbus',
    schemaVersion: 1,
    dataSourceFields: [
      { key: 'host', valueKind: 'host', required: true, displayName: 'Host', allowedValues: [], advanced: false, expectedFormat: 'DNS name, IPv4 or IPv6 address', exampleValue: '192.168.1.10' },
      { key: 'port', valueKind: 'port', required: false, displayName: 'Port', defaultValue: '502', allowedValues: [], minimum: 1, maximum: 65535, advanced: false }
    ],
    tagBindingFields: []
  }
};

const opcUa: DataSourceTypeDefinition = {
  typeKey: 'opc-ua',
  displayName: 'OPC UA',
  kind: 'communicationDriver',
  capabilities: {
    supportsConnectionTest: true,
    supportsDiscovery: true,
    supportsBrowse: true,
    supportsFileImport: false,
    supportsReconcile: true,
    supportsSharedTransportInfrastructure: false
  },
  configurationSchema: {
    schemaId: 'opcua',
    schemaVersion: 2,
    dataSourceFields: [
      { key: 'endpointUrl', valueKind: 'string', required: true, displayName: 'Endpoint URL', allowedValues: [], advanced: false, exampleValue: 'opc.tcp://192.168.1.10:4840' },
      { key: 'securityMode', valueKind: 'enum', required: true, displayName: 'Security mode', defaultValue: 'SignAndEncrypt', allowedValues: ['None', 'Sign', 'SignAndEncrypt'], advanced: false },
      { key: 'sessionTimeout', valueKind: 'duration', required: false, displayName: 'Session timeout', defaultValue: '00:01:00', allowedValues: [], advanced: true, expectedFormat: '[d.]hh:mm:ss[.fffffff]' },
      { key: 'passwordSecretReference', valueKind: 'secretReference', required: false, displayName: 'Password secret reference', defaultValue: 'secrets/opc/password', allowedValues: [], advanced: true }
    ],
    tagBindingFields: []
  }
};

function packageWith(source: DataSourceEngineering): EngineeringPackageView {
  return {
    schema: 'elitescada.engineering',
    schemaVersion: 1,
    exportedAt: '2026-09-02T00:00:00Z',
    tags: [],
    alarms: [],
    dataSources: [source]
  };
}

test.describe('backend-driven Data Source form logic', () => {
  test('switching type preserves stable Source identity but discards incompatible settings', () => {
    const original: DataSourceEngineering = {
      id: 'be4ca054-dfec-47cb-919f-291f2d33fab0',
      key: 'line-1',
      name: 'Line 1 PLC',
      driver: modbus.typeKey,
      enabled: true,
      settings: { host: '10.0.0.10', port: '1502', legacySetting: 'must-not-survive' },
      secretReferences: { oldSecret: 'legacy/reference' }
    };

    const switched = switchDataSourceType(original, opcUa);

    expect(switched.id).toBe(original.id);
    expect(switched.key).toBe(original.key);
    expect(switched.name).toBe(original.name);
    expect(switched.driver).toBe(opcUa.typeKey);
    expect(switched.driver).not.toBe(opcUa.displayName);
    expect(switched.settings).toEqual({ securityMode: 'SignAndEncrypt', sessionTimeout: '00:01:00' });
    expect(switched.settings).not.toHaveProperty('host');
    expect(switched.settings).not.toHaveProperty('legacySetting');
    expect(switched.secretReferences).toEqual({ passwordSecretReference: 'secrets/opc/password' });
  });

  test('same-type legacy settings are rejected until explicitly removed', () => {
    const source: DataSourceEngineering = {
      id: 'd4e8ad58-bce1-4a8e-ab3f-0cb9f18913c0',
      key: 'legacy-plc',
      name: 'Legacy PLC',
      driver: modbus.typeKey,
      enabled: true,
      settings: { host: '10.0.0.10', port: '502', retiredOption: 'legacy-value' },
      secretReferences: { host: 'vault://wrong-bucket', retiredSecret: 'vault://retired' }
    };

    expect(incompatibleDataSourceConfiguration(source, modbus)).toEqual({
      settings: ['retiredOption'],
      secretReferences: ['host', 'retiredSecret']
    });
    expect(validateDataSourceDraft(source, modbus)).toEqual(expect.arrayContaining([
      expect.objectContaining({ fieldKey: 'retiredOption', code: 'incompatible' }),
      expect.objectContaining({ fieldKey: 'host', code: 'incompatible' }),
      expect.objectContaining({ fieldKey: 'retiredSecret', code: 'incompatible' })
    ]));

    const cleaned = removeIncompatibleDataSourceConfiguration(source, modbus);
    expect(cleaned.id).toBe(source.id);
    expect(cleaned.driver).toBe(source.driver);
    expect(cleaned.settings).toEqual({ host: '10.0.0.10', port: '502' });
    expect(cleaned.secretReferences).toEqual({});
    expect(validateDataSourceDraft(cleaned, modbus)).toEqual([]);
  });

  test('defaults are split between normal settings and protected references', () => {
    expect(settingsForType(opcUa)).toEqual({
      settings: { securityMode: 'SignAndEncrypt', sessionTimeout: '00:01:00' },
      secretReferences: { passwordSecretReference: 'secrets/opc/password' }
    });
    expect(newDataSourceDraft(opcUa).driver).toBe('opc-ua');
  });

  test('client validation recognizes required, numeric and canonical duration formats', () => {
    const draft = newDataSourceDraft(opcUa);
    draft.name = 'OPC';
    draft.key = 'opc';
    draft.settings = {
      ...(draft.settings ?? {}),
      endpointUrl: 'opc.tcp://10.0.0.5:4840',
      sessionTimeout: 'not-a-duration'
    };

    expect(validateDataSourceDraft(draft, opcUa)).toContainEqual(expect.objectContaining({ fieldKey: 'sessionTimeout', code: 'duration' }));
    draft.settings.sessionTimeout = '00:00:05';
    expect(validateDataSourceDraft(draft, opcUa)).toEqual([]);

    const modbusDraft = newDataSourceDraft(modbus);
    modbusDraft.name = 'PLC';
    modbusDraft.key = 'plc';
    modbusDraft.settings = { host: '10.0.0.10', port: '70000' };
    expect(validateDataSourceDraft(modbusDraft, modbus)).toContainEqual(expect.objectContaining({ fieldKey: 'port', code: 'maximum' }));
  });

  test('candidate update resolves persisted Source by stable id even when its key is renamed', () => {
    const source: DataSourceEngineering = {
      id: 'be4ca054-dfec-47cb-919f-291f2d33fab0', key: 'old-key', name: 'PLC', driver: 'modbus.tcp', enabled: true, settings: { host: '10.0.0.10' }
    };
    const draft = { ...source, key: 'new-key' };
    const candidate = buildDataSourceCandidate(packageWith(source), draft, source.id!, false);

    expect(candidate.dataSources).toHaveLength(1);
    expect(candidate.dataSources?.[0].id).toBe(source.id);
    expect(candidate.dataSources?.[0].key).toBe('new-key');
  });
});

test('normal Data Source flow has no hardcoded driver catalog and uses Preview/Apply CAS', async () => {
  const editor = await readFile(new URL('../src/engineering/DataSourceCatalogEditor.tsx', import.meta.url), 'utf8');
  const structured = await readFile(new URL('../src/engineering/StructuredEditors.tsx', import.meta.url), 'utf8');
  const dataSourceSection = structured.slice(
    structured.indexOf('export function DataSourceEditor'),
    structured.indexOf('export function TagEditor'));

  expect(structured).toContain("import { DataSourceCatalogEditor } from './DataSourceCatalogEditor'");
  expect(dataSourceSection).toContain('<DataSourceCatalogEditor model={model} locale={locale} />');
  expect(dataSourceSection).not.toContain('<EngineeringEntityBrowser');
  expect(editor).toContain('/api/engineering/data-source-types');
  expect(editor).toContain('data-testid="data-source-type"');
  expect(editor).toContain('value={type.typeKey}>{type.displayName}');
  expect(editor).toContain('switchDataSourceType(draft, type)');
  expect(editor).toContain('removeIncompatibleDataSourceConfiguration(draft, currentType)');
  expect(editor).toContain('const before = await loadEngineeringWorkspace()');
  expect(editor).toContain('const after = await loadEngineeringWorkspace()');
  expect(editor).toContain('setValidatedChangeVersion(after.changeVersion)');
  expect(editor).toContain('applyEngineeringPackage(validatedCandidate, validatedChangeVersion)');
  expect(editor).not.toContain('DictionaryEditor');
  expect(editor).not.toMatch(/['"](?:modbus\.tcp|opc-ua|mqtt\.raw|dnp3\.master|builtin\.simulation)['"]/);
});
