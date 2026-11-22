import { expect, test } from '@playwright/test';

const scopeId = '15000000-0000-0000-0000-000000000301';

const localized = [
  {
    locale: 'pt-BR',
    configuredActive: 'Ativo na configuração',
    recoveryPending: 'Conferindo a ativação salva com a licença, Authority, revisão e HA atuais. Aguarde ou atualize a tela; ainda não é preciso ativar de novo.',
    permitClosed: 'A ativação salva não pôde ser recuperada. Confira a licença, a revisão ativa e o estado da Authority/HA. Se estiver tudo correto e quiser executar, selecione Ativar.',
    permitArmed: 'Pronto para consultar',
    activate: 'Ativar',
    pause: 'Pausar'
  },
  {
    locale: 'en',
    configuredActive: 'Configured active',
    recoveryPending: 'Checking the saved activation against the current license, Authority, revision and HA state. Wait or refresh; no action is needed yet.',
    permitClosed: 'The saved activation could not be recovered. Check the license, active revision and Authority/HA state. If they are correct and you want to run it, select Activate.',
    permitArmed: 'Ready to query',
    activate: 'Activate',
    pause: 'Pause'
  },
  {
    locale: 'es',
    configuredActive: 'Activo configurado',
    recoveryPending: 'Comprobando la activación guardada con la licencia, Authority, revisión y HA actuales. Espere o actualice la pantalla; todavía no necesita activarla de nuevo.',
    permitClosed: 'No se pudo recuperar la activación guardada. Revise la licencia, la revisión activa y el estado de Authority/HA. Si todo está correcto y quiere ejecutarla, seleccione Activar.',
    permitArmed: 'Listo para consultar',
    activate: 'Activar',
    pause: 'Pausar'
  }
] as const;

for (const copy of localized) for (const restrictedManager of [false, true]) {
test(`active scope with closed permit can be rearmed after authorization recovery (${copy.locale}, restricted=${restrictedManager})`, async ({ page }) => {
  const persistedScope = {
    kind: 'Equipment',
    scopeId,
    active: true,
    executionArmed: false,
    canManage: true,
    permitRevision: 7,
    permitAuthorityPolicyVersion: 3,
    permitHaEpoch: 9
  };
  let activationRequests = 0;
  let attemptedConfigurationWrites = 0;
  let startupRecoveryPending = true;

  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: [],
    workspace: restrictedManager ? ['EngineeringView'] : ['EngineeringView', 'EngineeringModify', 'AssetProcessIntelligenceManage']
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog', route => route.fulfill({ json: {
    supportedSemanticContractVersions: [1, 2],
    semanticCatalogs: [{ catalogVersion: 1, roles: [] }, { catalogVersion: 2, roles: [] }],
    controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: [] }]
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
      startupRecoveryPending,
      authorityFence: { allowed: true, reasonCode: 'ready', policyVersion: 3, haEpoch: 9 },
      execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0, activePermitCount: persistedScope.executionArmed ? 1 : 0, projectKey: 'test-project', activeRevision: 7, workspaceDirty: false }
    } });
  });
  await page.route(`**/api/engineering/asset-process-intelligence/scopes/equipment/${scopeId}/activate`, async route => {
    expect(route.request().method()).toBe('POST');
    expect(route.request().headers()['x-intelligence-project-key']).toBe('test-project');
    expect(route.request().headers()['x-intelligence-active-revision']).toBe('7');
    activationRequests++;
    persistedScope.executionArmed = true;
    await route.fulfill({ json: { activated: true } });
  });

  await page.goto(`/tests-e2e/harness/authoring-preview.html?intelligence&locale=${copy.locale}`);
  const scopeRow = page.getByTestId('asset-process-intelligence-workspace').locator('.asset-process-intelligence__scope-panel .asset-process-intelligence__row').first();
  await expect(scopeRow.getByText(copy.configuredActive, { exact: true })).toBeVisible();
  await expect(scopeRow.getByText(copy.recoveryPending, { exact: true })).toBeVisible();
  await expect(scopeRow.getByRole('button', { name: copy.activate })).toBeDisabled();

  startupRecoveryPending = false;
  await page.reload();
  await expect(scopeRow.getByText(copy.permitClosed, { exact: true })).toBeVisible();

  const activate = scopeRow.getByRole('button', { name: copy.activate });
  await expect(activate).toBeEnabled();
  if (restrictedManager) await expect(scopeRow.getByRole('checkbox')).toBeDisabled();
  await activate.click();

  await expect(scopeRow.getByText(copy.permitArmed, { exact: true })).toBeVisible();
  await expect(scopeRow.getByRole('button', { name: copy.pause })).toBeEnabled();
  expect(activationRequests).toBe(1);
  expect(attemptedConfigurationWrites).toBe(0);
  expect(persistedScope.active).toBe(true);
  expect(persistedScope.executionArmed).toBe(true);
});
}
