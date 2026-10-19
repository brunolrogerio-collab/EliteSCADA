import { expect, test } from '@playwright/test';

const equipmentId = '15000000-0000-0000-0000-000000000301';
const measurementProfileId = '15000000-0000-0000-0000-000000000501';
const measurementSignalId = '15000000-0000-0000-0000-000000000502';
const tagId = '15000000-0000-0000-0000-000000000101';
const assignmentId = '15000000-0000-0000-0000-000000000601';

test('Stage 4E authors and saves a versioned numeric operating context from the assigned profile', async ({ page }) => {
  let configuration: Record<string, any> = {
    version: 3,
    scopes: [],
    profiles: [{
      id: measurementProfileId,
      key: 'pump-process',
      name: 'Pump process',
      signals: [{ id: measurementSignalId, key: 'temperature', signalRole: 'process', tagId, required: true }]
    }],
    assignments: [{ id: assignmentId, equipmentId, profileId: measurementProfileId }],
    processStages: [],
    operatingContextProfiles: []
  };
  let saved: Record<string, any> | null = null;

  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: [],
    workspace: ['EngineeringModify', 'AssetProcessIntelligenceManage']
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog', route => route.fulfill({ json: {
    supportedSemanticContractVersions: [1, 2],
    semanticCatalogs: [{ catalogVersion: 1, roles: [] }, { catalogVersion: 2, roles: [] }],
    controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: [] }]
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/scopes', route => route.fulfill({ json: {
    configuration,
    resources: [{ kind: 'Equipment', scopeId: equipmentId, path: 'Plant.Pump01', name: 'Pump P01' }],
    tags: [{ id: tagId, name: 'Temperature', path: 'Plant.Pump.Temperature', dataType: 'Double', engineeringUnit: '°C' }],
    licensed: true,
    execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0, activePermitCount: 0, projectKey: 'test-project', activeRevision: 1, workspaceDirty: false }
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/configuration', async route => {
    const payload = route.request().postDataJSON() as Record<string, any>;
    saved = payload;
    const previousProfiles = configuration.operatingContextProfiles as Array<Record<string, any>>;
    const versionedContextProfiles = (payload.operatingContextProfiles as Array<Record<string, any>>).map(profile => ({
      ...profile,
      version: previousProfiles.find(previous => previous.id === profile.id)?.version ?? 1
    }));
    configuration = { ...configuration, ...payload, operatingContextProfiles: versionedContextProfiles };
    await route.fulfill({ json: { configuration, workspace: {} } });
  });

  await page.goto('/tests-e2e/harness/authoring-preview.html?intelligence&locale=pt-BR');
  const workspace = page.getByTestId('intelligence-stage2-authoring');
  const contextEditor = workspace.getByTestId('intelligence-context-profiles');

  await contextEditor.getByLabel('Associação de equipamento').selectOption(assignmentId);
  await contextEditor.getByRole('button', { name: 'Novo contexto' }).click();
  await contextEditor.getByLabel('Chave estável').fill('normal-operating');
  await contextEditor.getByLabel('Rótulo do operador').fill('Operação normal');
  await contextEditor.getByLabel('TAG do perfil de medição associado').selectOption(tagId);
  await contextEditor.getByLabel('Mínimo (inclusivo)').fill('2');
  await contextEditor.getByLabel('Máximo (inclusivo)').fill('8');
  await contextEditor.getByRole('button', { name: 'Adicionar predicado' }).click();

  await expect(contextEditor.getByText('Plant.Pump.Temperature')).toBeVisible();
  await workspace.locator('.asset-process-intelligence__authoring-footer')
    .getByRole('button', { name: 'Salvar configuração de processo' }).click();
  await expect.poll(() => saved).not.toBeNull();

  const payload = saved as unknown as {
    operatingContextProfiles: Array<{
      key: string;
      displayLabel: string;
      assignmentId: string;
      predicates: Array<{ tagId: string; kind: string; minimum: number | null; maximum: number | null }>;
    }>;
    expectedOperatingContextProfileVersions: Array<{ id: string; version: number }>;
  };
  expect(payload.expectedOperatingContextProfileVersions).toEqual([]);
  expect(payload.operatingContextProfiles).toHaveLength(1);
  expect(payload.operatingContextProfiles[0]).toMatchObject({
    key: 'normal-operating',
    displayLabel: 'Operação normal',
    assignmentId,
    predicates: [{ tagId, kind: 'NumericInterval', minimum: 2, maximum: 8 }]
  });
  expect(payload.operatingContextProfiles[0]).not.toHaveProperty('version');
  await expect(contextEditor.getByText('Versão salva pelo servidor: v1')).toBeVisible();
  await expect(contextEditor.getByText('Operação normal')).toBeVisible();
});
