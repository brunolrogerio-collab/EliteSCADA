import { expect, test } from '@playwright/test';

const sourceId = '22222222-2222-4222-8222-222222222222';
const tagId = '11111111-1111-4111-8111-111111111111';
const cases = [
  { driver: 'knx.ip', schema: 'elitescada.driver.knx.ip', fields: { writeGroupAddress: '1/2/3', stateGroupAddress: '1/2/4', dpt: '9.001' }, address: '1/2/3', settings: { stateGroupAddress: '1/2/4', dpt: '9.001' }, dataType: 'double' },
  { driver: 'zigbee.native', schema: 'elitescada.driver.zigbee.native', fields: { ieeeAddress: '0x00124b0000000001', endpoint: '1', cluster: 'genOnOff', attribute: 'onOff' }, address: '0x00124b0000000001/1/genOnOff/onOff', settings: {}, dataType: 'boolean' },
  { driver: 'zwave.js.controller', schema: 'elitescada.driver.zwave.js.controller', fields: {}, address: 'node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-', settings: {}, dataType: 'boolean' },
  { driver: 'matter.wifi', schema: 'elitescada.driver.matter.wifi', fields: {}, address: 'node/123/endpoint/1/cluster/6/attribute/0', settings: {}, dataType: 'boolean' },
  { driver: 'siemens.s7.iso', schema: 'siemens.s7.iso.binding', fields: { area: 'DataBlock', dbNumber: '1', byteOffset: '2', valueType: 'Float32', valueOrder: 'ByteAndWordSwap' }, address: 's7iso:v1;area=DataBlock;db=1;byte=2;bit=0;type=Float32;string=0;writable=false', settings: {}, dataType: 'float' },
  { driver: 'rockwell.logix.eip', schema: 'elitescada.driver.rockwell.logix.eip', fields: { scope: 'Program', programName: 'Main', symbolPath: 'MotorFlags[1]', nativeType: 'Dint', bitIndex: '5' }, address: 'logix:v1;scope=program;program=Main;symbol=MotorFlags%5B1%5D;native=dint;access=unknown;constant=false', settings: {}, dataType: 'boolean' }
];
const options: Record<string, string[]> = {
  area: ['DataBlock'], valueType: ['Float32'], valueOrder: ['Normal', 'ByteAndWordSwap'],
  scope: ['Controller', 'Program'], nativeType: ['Dint'], dpt: ['9.001'],
  cluster: ['genOnOff'], attribute: ['onOff']
};

const locales = ['pt-BR', 'en', 'es'];

for (const [index, scenario] of cases.entries()) {
  test.describe(scenario.driver, () => {
    test.use({ locale: locales[index % locales.length] });

    test('saves and reopens the binding', async ({ page }, testInfo) => {
      const fields = Object.entries(scenario.fields).filter((entry): entry is [string, string] => entry[1] !== undefined);
      const draft = {
        id: tagId, name: 'Test point', path: 'Plant.Point', dataType: scenario.dataType,
        source: 'test-source', dataSourceId: sourceId, readOnly: scenario.driver !== 'knx.ip'
      };
      const initial = {
        schema: 'scada.engineering', schemaVersion: 20, exportedAt: '2026-11-20T00:00:00Z',
        tags: [draft], alarms: [], dataSources: [
          { id: sourceId, key: 'test-source', name: 'Test source', driver: scenario.driver, enabled: true, settings: {} }
        ]
      };
      let stored = initial;
      let changeVersion = 1;
      await page.route('**/api/**', route => route.fulfill({ json: {} }));
      await page.route('**/api/auth/config', route => route.fulfill({ json: { authenticationEnabled: true } }));
      await page.route('**/api/auth/me', route => route.fulfill({ json: {
        subjectId: 'tag-binding-e2e', username: 'tag-binding-e2e', displayName: 'TAG binding test',
        roles: ['developer'], identityProvider: 'local'
      } }));
      await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
        authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
        authenticationEnabled: true, runtime: ['View'],
        workspace: ['EngineeringView', 'EngineeringModify', 'SystemAdmin']
      } }));
      await page.route('**/api/engineering/lock/status', route => route.fulfill({ json: { configured: false, locked: false } }));
      await page.route('**/api/engineering/workspace', route => route.fulfill({ json: {
        projectKey: 'tags-786', projectName: 'Binding test', baseRevision: 1, changeVersion, tagCount: 1, dataSourceCount: 1
      } }));
      await page.route('**/api/engineering/export/json', route => route.fulfill({ json: stored }));
      await page.route('**/api/engineering/data-source-types', route => route.fulfill({ json: {
        dataSourceTypes: [{
          typeKey: scenario.driver, displayName: scenario.driver, kind: 'communicationDriver',
          tagBindingSchemaId: scenario.schema, tagBindingSchemaVersion: 1, capabilities: {},
          configurationSchema: {
            schemaId: 'test-config', schemaVersion: 1, dataSourceFields: [],
            tagBindingFields: fields.map(([key]) => ({
              key, valueKind: options[key] ? 'enum' : 'string', allowedValues: options[key] ?? [], required: true
            }))
          }
        }]
      } }));
      let candidate: typeof initial | undefined;
      await page.route('**/api/engineering/import/json/preview', route => {
        candidate = route.request().postDataJSON();
        return route.fulfill({ json: {
          mode: 'Preview', canApply: true, createCount: 0, updateCount: 1, skipCount: 0, errorCount: 0, items: []
        } });
      });
      await page.route('**/api/engineering/import/json/apply', route => {
        expect(route.request().postDataJSON()).toEqual(candidate);
        stored = route.request().postDataJSON();
        changeVersion++;
        return route.fulfill({ json: { mode: 'CreateAndUpdate', created: 0, updated: 1, skipped: 0, issues: [] } });
      });

      await page.goto('/engineering/tags');
      const manual = page.getByTestId('tag-address-manual');
      const applyBinding = page.getByTestId('generic-tag-binding-apply');
      if (!fields.length) await manual.fill(scenario.address);
      for (const [key, value] of fields) {
        const field = page.getByTestId('generic-tag-binding-' + key);
        if (options[key]) await field.selectOption(value);
        else await field.fill(value);
      }
      if (scenario.driver === 'knx.ip') {
        await page.getByTestId('generic-tag-binding-stateGroupAddress').fill('01/2/3');
        await applyBinding.click();
        await expect(page.getByTestId('generic-tag-binding-assistant').getByRole('alert')).toBeVisible();
        await expect(manual).toHaveValue('');
        await page.getByTestId('generic-tag-binding-stateGroupAddress').fill('1/2/4');
      }
      await applyBinding.click();
      await expect(manual).toHaveValue(scenario.address);
      await page.getByTestId('engineering-preview').click();
      await page.getByTestId('engineering-apply').click();
      await expect.poll(() => changeVersion).toBe(2);
      expect(stored.tags[0]).toMatchObject({
        address: scenario.address,
        communicationBinding: {
          contractVersion: 1, schemaId: scenario.schema, schemaVersion: 1,
          portableAddress: scenario.address, settings: scenario.settings
        }
      });
      const saved = stored.tags[0] as typeof draft & {
        communicationBinding: { settings: Record<string, string>; valueTransform?: unknown }; addressSelector?: unknown;
      };
      expect(saved.communicationBinding.settings).toEqual(scenario.settings);
      if (scenario.driver === 'siemens.s7.iso') {
        expect(saved.communicationBinding.valueTransform).toEqual({ contractVersion: 1, byteSwap: true, wordSwap: true });
      }
      if (scenario.driver === 'rockwell.logix.eip') expect(saved.addressSelector).toEqual({ kind: 'bit', index: 5 });
      await testInfo.attach(scenario.driver, { body: JSON.stringify(saved), contentType: 'application/json' });
      await page.reload();
      await expect(manual).toHaveValue(scenario.address);
      for (const [key, value] of fields) {
        await expect(page.getByTestId('generic-tag-binding-' + key)).toHaveValue(value);
      }
    });
  });
}
