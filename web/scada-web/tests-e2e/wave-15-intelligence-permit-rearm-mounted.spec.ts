import { expect, test } from '@playwright/test';

const scopeId = '15000000-0000-0000-0000-000000000301';

const localized = [
  {
    locale: 'pt-BR',
    configuredActive: 'Ativo na configuração',
    permitClosed: 'Ativo configurado · permissão de processo fechada; ative explicitamente após restaurar a autorização (ou após reiniciar, se aplicável)',
    permitArmed: 'Permissão de processo armada',
    activate: 'Ativar',
    pause: 'Pausar'
  },
  {
    locale: 'en',
    configuredActive: 'Configured active',
    permitClosed: 'Configured active · process permit closed; explicitly activate after authorization is restored (or after restart, if applicable)',
    permitArmed: 'Process permit armed',
    activate: 'Activate',
    pause: 'Pause'
  },
  {
    locale: 'es',
    configuredActive: 'Activo configurado',
    permitClosed: 'Activo configurado · permiso de proceso cerrado; actívelo explícitamente tras restaurar la autorización (o tras reiniciar, si corresponde)',
    permitArmed: 'Permiso de proceso habilitado',
    activate: 'Activar',
    pause: 'Pausar'
  }
] as const;

for (const copy of localized) {
test(`active scope with closed permit can be rearmed after authorization recovery (${copy.locale})`, async ({ page }) => {
  const persistedScope = {
    kind: 'Equipment',
    scopeId,
    active: true,
    executionArmed: false,
    permitRevision: 7,
    permitAuthorityPolicyVersion: 3,
    permitHaEpoch: 9
  };
  let activationRequests = 0;
  let attemptedConfigurationWrites = 0;

  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: [],
    workspace: ['EngineeringModify', 'AssetProcessIntelligenceManage']
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/scopes', async route => {
    if (route.request().method() !== 'GET') {
      attemptedConfigurationWrites++;
      await route.fulfill({ status: 500, json: { error: 'Unexpected configuration write' } });
      return;
    }
    await route.fulfill({ json: {
      configuration: { version: 2, scopes: [{ ...persistedScope }], profiles: [], assignments: [], processStages: [] },
      resources: [{ kind: 'Equipment', scopeId, path: 'Plant.Pump01', name: 'Pump P01' }],
      tags: [],
      licensed: true,
      authorityFence: { allowed: true, reasonCode: 'ready', policyVersion: 3, haEpoch: 9 },
      execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0, activePermitCount: persistedScope.executionArmed ? 1 : 0, projectKey: 'test-project', activeRevision: 7, workspaceDirty: false }
    } });
  });
  await page.route(`**/api/engineering/asset-process-intelligence/scopes/equipment/${scopeId}/activate`, async route => {
    expect(route.request().method()).toBe('POST');
    activationRequests++;
    persistedScope.executionArmed = true;
    await route.fulfill({ json: { activated: true } });
  });

  await page.goto(`/tests-e2e/harness/authoring-preview.html?intelligence&locale=${copy.locale}`);
  const scopeRow = page.getByTestId('asset-process-intelligence-workspace').locator('.asset-process-intelligence__scope-panel .asset-process-intelligence__row').first();
  await expect(scopeRow.getByText(copy.configuredActive, { exact: true })).toBeVisible();
  await expect(scopeRow.getByText(copy.permitClosed, { exact: true })).toBeVisible();

  const activate = scopeRow.getByRole('button', { name: copy.activate });
  await expect(activate).toBeEnabled();
  await activate.click();

  await expect(scopeRow.getByText(copy.permitArmed, { exact: true })).toBeVisible();
  await expect(scopeRow.getByRole('button', { name: copy.pause })).toBeEnabled();
  expect(activationRequests).toBe(1);
  expect(attemptedConfigurationWrites).toBe(0);
  expect(persistedScope.active).toBe(true);
  expect(persistedScope.executionArmed).toBe(true);
});
}
