import { expect, test } from '@playwright/test';
import {
  normalizeSimulationProfile,
  simulationFieldVisibility,
  simulationSignalOptions,
  updateSimulationDataType,
  updateSimulationSignal
} from '../src/engineering/SimulationTagEditor.logic';
import type { TagSourceAwareEngineering } from '../src/engineering/TagSourceSelector.logic';

test.use({ locale: 'pt-BR' });

test('mounted Data Source editor rebuilds driver-specific fields instead of reusing incompatible settings', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();

  const editor = page.getByTestId('schema-data-source-editor');
  await expect(editor).toBeVisible();
  await editor.locator('header button').first().click();

  const typePicker = page.getByTestId('data-source-type');
  await expect(typePicker).toHaveValue('');
  await typePicker.selectOption('modbus.tcp');
  await expect(page.getByTestId('data-source-setting-host')).toBeVisible();
  await page.getByTestId('data-source-setting-host').fill('10.0.0.50');

  await typePicker.selectOption('builtin.simulation');
  await expect(page.getByTestId('data-source-setting-host')).toHaveCount(0);
  const scanInterval = page.getByTestId('data-source-setting-scanIntervalMilliseconds');
  await expect(scanInterval).toBeVisible();
  await expect(scanInterval).toHaveValue('500');
});


test('mounted Data Source editor exposes catalog failure and recovers only after explicit reload', async ({ page }) => {
  let failCatalog = true;
  await page.route('**/api/engineering/data-source-types', async route => {
    if (failCatalog) {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Service unavailable' })
      });
      return;
    }
    await route.fallback();
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();

  const editor = page.getByTestId('schema-data-source-editor');
  await expect(editor).toBeVisible();
  await editor.locator('header button').first().click();

  const typePicker = page.getByTestId('data-source-type');
  await expect(page.getByTestId('data-source-catalog-error')).toBeVisible();
  await expect(page.getByTestId('data-source-catalog-error')).toContainText('503');
  await expect(typePicker).toBeDisabled();

  failCatalog = false;
  await page.getByTestId('data-source-catalog-reload').click();

  await expect(page.getByTestId('data-source-catalog-error')).toHaveCount(0);
  await expect(typePicker).toBeEnabled();
  const optionValues = await typePicker.locator('option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value));
  expect(optionValues).toContain('builtin.simulation');
  await typePicker.selectOption('builtin.simulation');
  await expect(typePicker).toHaveValue('builtin.simulation');
  await expect(page.getByTestId('data-source-setting-scanIntervalMilliseconds')).toBeVisible();
});


test('R2 structured TAG journey keeps one context and explicit bulk mode', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.getByRole('button', { name: /Demo\.P01\.Frequency/ }).click();
  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Demo.P01.Frequency');
  await expect(actions.getByTestId('engineering-bulk-panel')).toHaveCount(0);
  await expect(actions.getByRole('combobox')).toHaveCount(0);
  const historian = page.getByTestId('tag-historian-disclosure');
  await expect(historian).not.toHaveAttribute('open', '');
  await historian.locator('summary').click();
  await expect(historian).toHaveAttribute('open', '');
  await test.info().attach('r2-tag-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Data Source journey progressively reveals backend advanced fields', async ({ page, request }) => {
  const response = await request.get('/api/engineering/data-source-types');
  expect(response.ok()).toBeTruthy();
  const catalog = await response.json() as {
    dataSourceTypes: Array<{
      typeKey: string;
      configurationSchema?: { dataSourceFields?: Array<{ key: string; advanced: boolean }> };
    }>;
  };
  const type = catalog.dataSourceTypes.find(candidate =>
    candidate.configurationSchema?.dataSourceFields?.some(field => field.advanced));
  expect(type, 'At least one mounted driver should expose an advanced Data Source field').toBeTruthy();
  const advanced = type!.configurationSchema!.dataSourceFields!.find(field => field.advanced)!;

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Fontes de dados/ }).click();
  const editor = page.getByTestId('schema-data-source-editor');
  await editor.getByRole('button', { name: 'Nova Fonte de dados' }).click();
  await editor.getByTestId('data-source-type').selectOption(type!.typeKey);
  const advancedField = editor.getByTestId(`data-source-setting-${advanced.key}`);
  await expect(advancedField).not.toBeVisible();
  const disclosure = editor.getByTestId('data-source-advanced-disclosure');
  await disclosure.locator('summary').click();
  await expect(advancedField).toBeVisible();
  const actions = editor.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('Novo rascunho');
  await expect(actions.getByTestId('engineering-delete')).toBeDisabled();
  await test.info().attach('r2-data-source-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Alarm journey binds actions to current Alarm and discloses behavior on demand', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Alarmes/ }).click();
  await expect(page.locator('.eng-editor-picker')).toHaveCount(1);
  await page.locator('.eng-editor-picker').getByRole('button', { name: /High discharge pressure/ }).click();
  const actions = page.getByTestId('engineering-entity-actions');
  await expect(actions.getByTestId('engineering-current-entity')).toContainText('High discharge pressure');
  await expect(actions.getByRole('combobox')).toHaveCount(0);
  const behavior = page.getByTestId('alarm-behavior-disclosure');
  await expect(behavior).not.toHaveAttribute('open', '');
  await behavior.locator('summary').click();
  await expect(behavior).toHaveAttribute('open', '');
  await test.info().attach('r2-alarm-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});

test('R2 structured Operational Event journey separates core authoring from optional context', async ({ page }) => {
  await page.goto('/engineering');
  const navigation = page.locator('.eng-nav');
  await navigation.getByRole('button', { name: /Eventos Operacionais/ }).click();
  const editor = page.getByTestId('operational-event-engineering');
  await editor.getByTestId('operational-event-new').click();
  await expect(editor.getByLabel('Nome de exibição', { exact: true })).toBeVisible();
  await expect(editor.getByLabel('Identificador', { exact: true })).toBeVisible();
  const context = editor.getByTestId('operational-event-context-disclosure');
  await expect(context).not.toHaveAttribute('open', '');
  await context.locator('summary').click();
  await expect(context.getByLabel('Área', { exact: true })).toBeVisible();
  await expect(context.locator('select')).toBeVisible();
  await test.info().attach('r2-operational-event-first-use', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
});


function legacySimulationTag(overrides: Partial<TagSourceAwareEngineering> = {}): TagSourceAwareEngineering {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Simulated',
    path: 'Simulation.Simulated',
    dataType: 'double',
    source: 'simulation',
    readOnly: true,
    address: 'legacy-simulation-address',
    metadata: {
      'simulation.signalType': 'Sine',
      'simulation.minimum': '0',
      'simulation.maximum': '100',
      'simulation.periodSeconds': '10',
      'simulation.constantValue': '7',
      'simulation.step': '1'
    },
    communicationBinding: {
      contractVersion: 1,
      schemaId: 'builtin.simulation.engineering',
      schemaVersion: 1,
      portableAddress: 'legacy-simulation-address',
      settings: {
        'simulation.signalType': 'Sine',
        'simulation.minimum': '0',
        'simulation.maximum': '100'
      }
    },
    ...overrides
  };
}

test('Simulation TAG policy exposes only type-compatible authoring choices', () => {
  expect(simulationSignalOptions('dateTime')).toEqual(['CurrentTime']);
  expect(simulationSignalOptions('boolean')).toEqual(['BooleanToggle', 'Constant', 'Manual']);
  expect(simulationSignalOptions('double')).toContain('Sine');
  expect(simulationSignalOptions('double')).not.toContain('CurrentTime');
  expect(simulationSignalOptions('double')).not.toContain('BooleanToggle');
  expect(simulationSignalOptions('string')).not.toContain('CurrentTime');
  expect(simulationSignalOptions('enum')).toEqual(['Constant', 'Counter', 'Manual']);
  expect(simulationFieldVisibility('CurrentTime')).toEqual({
    minimum: false,
    maximum: false,
    periodSeconds: false,
    constantValue: false,
    step: false
  });
  expect(simulationFieldVisibility('BooleanToggle')).toMatchObject({
    minimum: false,
    maximum: false,
    periodSeconds: true,
    constantValue: false,
    step: false
  });
});

test('Simulation TAG type changes converge metadata and retire legacy duplicate binding authority', () => {
  const dateTime = updateSimulationDataType(legacySimulationTag(), 'dateTime');
  expect(dateTime.dataType).toBe('dateTime');
  expect(dateTime.metadata?.['simulation.signalType']).toBe('CurrentTime');
  expect(dateTime.metadata?.['simulation.minimum']).toBeUndefined();
  expect(dateTime.metadata?.['simulation.maximum']).toBeUndefined();
  expect(dateTime.metadata?.['simulation.periodSeconds']).toBeUndefined();
  expect(dateTime.metadata?.['simulation.constantValue']).toBeUndefined();
  expect(dateTime.metadata?.['simulation.step']).toBeUndefined();
  expect(dateTime.address).toBeNull();
  expect(dateTime.addressSelector).toBeNull();
  expect(dateTime.communicationBinding).toBeNull();

  const numeric = updateSimulationDataType(dateTime, 'double');
  expect(numeric.metadata?.['simulation.signalType']).toBe('Sine');
  expect(numeric.metadata?.['simulation.minimum']).toBeUndefined();
  expect(numeric.metadata?.['simulation.maximum']).toBeUndefined();
  expect(numeric.metadata?.['simulation.periodSeconds']).toBeUndefined();

  const constant = updateSimulationSignal(legacySimulationTag(), 'Constant');
  expect(constant.metadata?.['simulation.signalType']).toBe('Constant');
  expect(constant.metadata?.['simulation.constantValue']).toBe('7');
  expect(constant.metadata?.['simulation.minimum']).toBeUndefined();
  expect(constant.metadata?.['simulation.maximum']).toBeUndefined();
  expect(constant.metadata?.['simulation.periodSeconds']).toBeUndefined();
  expect(constant.metadata?.['simulation.step']).toBeUndefined();

  const normalized = normalizeSimulationProfile(legacySimulationTag({
    dataType: 'dateTime',
    metadata: {
      'simulation.signalType': 'Sine',
      'simulation.minimum': '100',
      'simulation.maximum': '0',
      'simulation.periodSeconds': '0'
    }
  }));
  expect(normalized.metadata?.['simulation.signalType']).toBe('CurrentTime');
  expect(normalized.metadata?.['simulation.minimum']).toBeUndefined();
  expect(normalized.metadata?.['simulation.maximum']).toBeUndefined();
  expect(normalized.metadata?.['simulation.periodSeconds']).toBeUndefined();
  expect(normalized.address).toBeNull();
  expect(normalized.addressSelector).toBeNull();
  expect(normalized.communicationBinding).toBeNull();
});

test('mounted Simulation TAG editor presents one canonical type-aware authoring surface', async ({ page, request }) => {
  const packageResponse = await request.get('/api/engineering/export/json');
  expect(packageResponse.ok()).toBeTruthy();
  const engineering = await packageResponse.json() as {
    dataSources?: Array<{ id?: string; key: string; driver: string }>;
  };
  const simulationSource = engineering.dataSources?.find(source =>
    source.driver.trim().toLowerCase() === 'builtin.simulation');
  test.skip(!simulationSource, 'Mounted package does not contain a builtin.simulation Data Source.');

  await page.goto('/engineering');
  await page.getByRole('button', { name: /TAGs/ }).click();
  await page.getByRole('button', { name: 'Nova TAG' }).click();
  await page.getByLabel('Nome da TAG').fill('Simulation UX convergence');

  const sourceIdentity = simulationSource!.id ? `id:${simulationSource!.id}` : `key:${simulationSource!.key}`;
  await page.getByTestId('tag-source-select').selectOption(sourceIdentity);

  await expect(page.getByTestId('tag-address-manual')).toHaveCount(0);
  await expect(page.getByTestId('generic-tag-binding-assistant')).toHaveCount(0);

  const simulationEditor = page.getByTestId('tag-simulation-disclosure');
  await expect(simulationEditor).toBeVisible();
  await simulationEditor.locator('summary').click();

  const type = page.getByTestId('tag-data-type');
  const signal = page.getByTestId('simulation-signal-type');
  await expect(type).toHaveValue('double');
  await expect(signal).toHaveValue('Sine');
  await expect(page.getByTestId('simulation-minimum')).toBeVisible();
  await expect(page.getByTestId('simulation-maximum')).toBeVisible();
  await expect(page.getByTestId('simulation-period')).toBeVisible();
  await expect(page.getByTestId('simulation-constant-value')).toHaveCount(0);
  await expect(page.getByTestId('simulation-step')).toHaveCount(0);

  await type.selectOption('dateTime');
  await expect(signal).toHaveValue('CurrentTime');
  expect(await signal.locator('option').allTextContents()).toEqual(['CurrentTime']);
  await expect(page.getByTestId('simulation-minimum')).toHaveCount(0);
  await expect(page.getByTestId('simulation-maximum')).toHaveCount(0);
  await expect(page.getByTestId('simulation-period')).toHaveCount(0);
  await expect(page.getByTestId('simulation-constant-value')).toHaveCount(0);
  await expect(page.getByTestId('simulation-step')).toHaveCount(0);

  await type.selectOption('boolean');
  await expect(signal).toHaveValue('BooleanToggle');
  expect(await signal.locator('option').allTextContents()).toEqual(['BooleanToggle', 'Constant', 'Manual']);
  await expect(page.getByTestId('simulation-period')).toBeVisible();
  await expect(page.getByTestId('simulation-minimum')).toHaveCount(0);
  await expect(page.getByTestId('simulation-maximum')).toHaveCount(0);

  await type.selectOption('double');
  await expect(signal).toHaveValue('Sine');
  expect(await signal.locator('option').allTextContents()).not.toContain('CurrentTime');
  expect(await signal.locator('option').allTextContents()).not.toContain('BooleanToggle');

  await page.getByLabel('Idioma').selectOption('en');
  await expect(simulationEditor.locator('summary')).toContainText('TAG simulation');
  await page.getByLabel('Language').selectOption('es');
  await expect(simulationEditor.locator('summary')).toContainText('Simulación del TAG');
});
