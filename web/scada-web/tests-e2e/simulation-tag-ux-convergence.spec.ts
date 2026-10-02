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

test('Simulation TAG type changes converge profile metadata and retire legacy duplicate binding authority', () => {
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
});

test('Simulation profile changes preserve only parameters consumed by the selected behavior', () => {
  const constant = updateSimulationSignal(legacySimulationTag(), 'Constant');
  expect(constant.metadata?.['simulation.signalType']).toBe('Constant');
  expect(constant.metadata?.['simulation.constantValue']).toBe('7');
  expect(constant.metadata?.['simulation.minimum']).toBeUndefined();
  expect(constant.metadata?.['simulation.maximum']).toBeUndefined();
  expect(constant.metadata?.['simulation.periodSeconds']).toBeUndefined();
  expect(constant.metadata?.['simulation.step']).toBeUndefined();

  const staleDateTime = legacySimulationTag({
    dataType: 'dateTime',
    metadata: {
      'simulation.signalType': 'Sine',
      'simulation.minimum': '100',
      'simulation.maximum': '0',
      'simulation.periodSeconds': '0'
    }
  });
  const normalized = normalizeSimulationProfile(staleDateTime);
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
