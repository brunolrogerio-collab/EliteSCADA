import { expect, test } from '@playwright/test';

const locales = {
  'pt-BR': {
    createProfile: 'Novo perfil', profileKey: 'Chave estável', profileName: 'Nome de exibição',
    signalKey: 'Chave do sinal', signalRole: 'Texto de papel legado', tagReference: 'Referência do TAG', addSignal: 'Adicionar sinal',
    equipment: 'Equipamento', profile: 'Perfil', assignProfile: 'Associar perfil', newStage: 'Nova etapa', stageKey: 'Chave estável',
    stageName: 'Nome de exibição', stageAssignment: 'Associação', addToStage: 'Adicionar à etapa', moveUp: 'Mover para cima', moveDown: 'Mover para baixo',
    save: 'Salvar configuração de processo', noProfiles: 'Nenhum perfil ainda. Crie um para definir referências estáticas de sinais.',
    noEquipment: 'Nenhum equipamento estável disponível.', noTags: 'Nenhum TAG estável disponível', missingTag: 'Referência de TAG ausente',
    missingEquipment: 'Equipamento ausente', missingProfile: 'Perfil ausente', referenceMissing: 'Referência ausente'
  },
  en: {
    createProfile: 'New profile', profileKey: 'Stable key', profileName: 'Display name',
    signalKey: 'Signal key', signalRole: 'Legacy domain role text', tagReference: 'TAG reference', addSignal: 'Add signal',
    equipment: 'Equipment', profile: 'Profile', assignProfile: 'Assign profile', newStage: 'New stage', stageKey: 'Stable key',
    stageName: 'Display name', stageAssignment: 'Assignment', addToStage: 'Add to stage', moveUp: 'Move up', moveDown: 'Move down',
    save: 'Save process configuration', noProfiles: 'No profiles yet. Create one to define static signal references.',
    noEquipment: 'No stable Equipment is available.', noTags: 'No stable TAGs available', missingTag: 'Missing TAG reference',
    missingEquipment: 'Missing Equipment', missingProfile: 'Missing profile', referenceMissing: 'Reference is missing'
  },
  es: {
    createProfile: 'Nuevo perfil', profileKey: 'Clave estable', profileName: 'Nombre',
    signalKey: 'Clave de señal', signalRole: 'Texto de rol heredado', tagReference: 'Referencia TAG', addSignal: 'Añadir señal',
    equipment: 'Equipo', profile: 'Perfil', assignProfile: 'Asignar perfil', newStage: 'Nueva etapa', stageKey: 'Clave estable',
    stageName: 'Nombre', stageAssignment: 'Asignación', addToStage: 'Añadir a etapa', moveUp: 'Mover arriba', moveDown: 'Mover abajo',
    save: 'Guardar configuración', noProfiles: 'Aún no hay perfiles. Cree uno para definir referencias estáticas.',
    noEquipment: 'No hay equipos estables.', noTags: 'No hay TAGs estables', missingTag: 'Falta la referencia TAG',
    missingEquipment: 'Equipo inexistente', missingProfile: 'Perfil inexistente', referenceMissing: 'Falta la referencia'
  }
} as const;

for (const locale of ['pt-BR', 'en', 'es'] as const) {
  test(`Stage 2 creates, reuses, assigns and orders static metadata (${locale})`, async ({ page }) => {
    const copy = locales[locale];
    let configuration = { version: 2, scopes: [], profiles: [], assignments: [], processStages: [] } as Record<string, any>;
    let saved: Record<string, any> | null = null;
    await mockWorkspace(page, () => configuration, next => { saved = next; configuration = { ...configuration, ...next }; });

    await page.goto(`/tests-e2e/harness/authoring-preview.html?intelligence&locale=${locale}`);
    const workspace = page.getByTestId('intelligence-stage2-authoring');
    await expect(workspace.getByRole('heading', { name: 'Reusable profiles', exact: false })).toBeVisible().catch(async () => {
      await expect(workspace.getByRole('heading', { name: /perfis reutilizáveis|perfiles reutilizables/i })).toBeVisible();
    });
    await workspace.getByRole('button', { name: copy.createProfile }).click();
    await workspace.getByLabel(copy.profileKey).fill('pump-service');
    await workspace.getByLabel(copy.profileName).fill('Pump service');
    await workspace.getByLabel(copy.signalKey).fill('temperature');
    await workspace.getByLabel(copy.signalRole).fill('thermal');
    await workspace.getByLabel(copy.tagReference).selectOption('15000000-0000-0000-0000-000000000101');
    await workspace.getByRole('button', { name: copy.addSignal }).click();

    const equipment = workspace.getByRole('combobox', { name: copy.equipment, exact: true });
    const profile = workspace.getByRole('combobox', { name: copy.profile, exact: true });
    await equipment.selectOption('15000000-0000-0000-0000-000000000301');
    await profile.selectOption({ label: 'Pump service' });
    await workspace.getByRole('button', { name: copy.assignProfile }).click();
    await equipment.selectOption('15000000-0000-0000-0000-000000000302');
    await profile.selectOption({ label: 'Pump service' });
    await workspace.getByRole('button', { name: copy.assignProfile }).click();

    await workspace.getByRole('button', { name: copy.newStage }).click();
    const firstStageId = await selectedStageId(workspace);
    let stageEditor = workspace.locator('.asset-process-intelligence__editor').last();
    await stageEditor.getByLabel(copy.stageKey).fill('feed');
    await stageEditor.getByLabel(copy.stageName).fill('Feed');
    await chooseFirstAssignment(workspace.getByRole('combobox', { name: copy.stageAssignment, exact: true }));
    await workspace.getByRole('button', { name: copy.addToStage }).click();
    await chooseFirstAssignment(workspace.getByRole('combobox', { name: copy.stageAssignment, exact: true }));
    await workspace.getByRole('button', { name: copy.addToStage }).click();

    await workspace.getByRole('button', { name: copy.newStage }).click();
    stageEditor = workspace.locator('.asset-process-intelligence__editor').last();
    await stageEditor.getByLabel(copy.stageKey).fill('treatment');
    await stageEditor.getByLabel(copy.stageName).fill('Treatment');
    await chooseFirstAssignment(workspace.getByRole('combobox', { name: copy.stageAssignment, exact: true }));
    await workspace.getByRole('button', { name: copy.addToStage }).click();
    const stageChoices = workspace.locator('.asset-process-intelligence__stage-choice');
    await stageChoices.nth(1).getByRole('button', { name: copy.moveUp }).click();
    await workspace.getByRole('option', { name: /feed/i }).click();
    stageEditor = workspace.locator('.asset-process-intelligence__editor').last();
    await stageEditor.locator('.asset-process-intelligence__list .asset-process-intelligence__row').first().getByRole('button', { name: copy.moveDown }).click();

    await workspace.getByRole('button', { name: copy.save, exact: true }).first().click();
    await expect.poll(() => saved).not.toBeNull();
    const payload = saved as unknown as {
      profiles: Array<{ key: string; name: string; signals: Array<{ tagId: string; signalRole: string }> }>;
      assignments: Array<{ equipmentId: string; profileId: string }>;
      processStages: Array<{ key: string; order: number; links: Array<{ assignmentId: string; order: number }> }>;
    };
    expect(payload.profiles).toHaveLength(1);
    expect(payload.profiles[0]).toMatchObject({ key: 'pump-service', name: 'Pump service' });
    expect(payload.profiles[0].signals[0]).toMatchObject({ tagId: '15000000-0000-0000-0000-000000000101', signalRole: 'thermal' });
    expect(payload.assignments).toHaveLength(2);
    expect(new Set(payload.assignments.map(item => item.profileId)).size).toBe(1);
    expect(payload.processStages.map(item => item.key)).toEqual(['treatment', 'feed']);
    const feed = payload.processStages.find(item => item.key === 'feed')!;
    expect(feed.links.map(item => item.order)).toEqual([0, 1]);
    expect(feed.links[0].assignmentId).toBe(payload.assignments[1].id);
    expect(firstStageId).toBeTruthy();
  });

  test(`Stage 2 displays empty and missing-reference states (${locale})`, async ({ page }) => {
    const copy = locales[locale];
    let configuration = { version: 2, scopes: [], profiles: [], assignments: [], processStages: [] } as Record<string, any>;
    await mockWorkspace(page, () => configuration, next => { configuration = { ...configuration, ...next }; }, { emptyResources: true });
    await page.goto(`/tests-e2e/harness/authoring-preview.html?intelligence&locale=${locale}&empty`);
    const workspace = page.getByTestId('intelligence-stage2-authoring');
    await expect(workspace.getByText(copy.noProfiles)).toBeVisible();
    await expect(workspace.getByText(copy.noEquipment)).toBeVisible();
    await workspace.getByRole('button', { name: copy.createProfile }).click();
    await expect(workspace.getByLabel(copy.tagReference)).toBeDisabled();
    await expect(workspace.getByLabel(copy.tagReference).getByRole('option', { name: copy.noTags })).toBeAttached();

    configuration = {
      version: 2,
      scopes: [],
      profiles: [{ id: '15000000-0000-0000-0000-000000000201', key: 'orphan-profile', name: 'Orphan', signals: [{ id: '15000000-0000-0000-0000-000000000202', key: 'missing', signalRole: 'process', tagId: '15000000-0000-0000-0000-000000000299', required: true }] }],
      assignments: [{ id: '15000000-0000-0000-0000-000000000203', equipmentId: '15000000-0000-0000-0000-000000000298', profileId: '15000000-0000-0000-0000-000000000297' }],
      processStages: [{ id: '15000000-0000-0000-0000-000000000204', key: 'orphan-stage', name: 'Orphan stage', order: 0, links: [{ assignmentId: '15000000-0000-0000-0000-000000000296', order: 0 }] }]
    };
    await page.reload();
    const loaded = page.getByTestId('intelligence-stage2-authoring');
    await expect(loaded.getByText(copy.missingTag)).toBeVisible();
    await expect(loaded.getByText(copy.missingEquipment, { exact: false }).first()).toBeVisible();
    await expect(loaded.getByText(copy.referenceMissing, { exact: false }).first()).toBeVisible();
  });
}

test('controlled signal role authoring keeps legacy text and sends complete typed qualifiers', async ({ page }) => {
  let saved: Record<string, any> | null = null;
  const configuration = {
    version: 2,
    scopes: [],
    profiles: [{
      id: '15000000-0000-0000-0000-000000000401',
      key: 'pump',
      name: 'Pump',
      signals: [{
        id: '15000000-0000-0000-0000-000000000402',
        key: 'current',
        signalRole: 'Legacy current text',
        tagId: '15000000-0000-0000-0000-000000000103',
        required: true
      }]
    }],
    assignments: [],
    processStages: []
  } as Record<string, any>;
  await mockWorkspace(page, () => configuration, next => { saved = next; });

  await page.goto('/tests-e2e/harness/authoring-preview.html?intelligence&locale=pt-BR');
  const workspace = page.getByTestId('intelligence-stage2-authoring');
  await expect(workspace.getByText('Legacy current text')).toBeVisible();
  const saveButton = workspace.locator('.asset-process-intelligence__authoring-footer').getByRole('button', { name: 'Salvar configuração de processo' });
  await workspace.getByTestId('intelligence-semantic-role-current').selectOption('pump.electrical_current');
  await expect(workspace.getByText(/Incompleta:/)).toBeVisible();
  await expect(saveButton).toBeDisabled();
  await workspace.getByTestId('intelligence-semantic-currentBasis-current').selectOption('line_rms');
  await expect(workspace.getByText('Anotação controlada completa')).toBeVisible();
  await expect(saveButton).toBeEnabled();
  await saveButton.click();
  await expect.poll(() => saved).not.toBeNull();

  const payload = saved as unknown as {
    semanticContractVersion: number;
    profiles: Array<{ signals: Array<{ signalRole: string; semanticBinding: Record<string, unknown> }> }>;
  };
  expect(payload.semanticContractVersion).toBe(1);
  expect(payload.profiles[0].signals[0]).toMatchObject({
    signalRole: 'Legacy current text',
    semanticBinding: { catalogVersion: 1, roleCode: 'pump.electrical_current', currentBasis: 'line_rms' }
  });
});

async function selectedStageId(workspace: import('@playwright/test').Locator) {
  const select = workspace.locator('.asset-process-intelligence__stage-choice [role="option"]').last();
  await expect(select).toBeVisible();
  return select.getAttribute('aria-selected');
}

async function chooseFirstAssignment(select: import('@playwright/test').Locator) {
  const value = await select.locator('option').nth(1).getAttribute('value');
  if (!value) throw new Error('No process assignment was available to select.');
  await select.selectOption(value);
}

async function mockWorkspace(
  page: import('@playwright/test').Page,
  read: () => Record<string, any>,
  write: (configuration: Record<string, any>) => void,
  options: { emptyResources?: boolean } = {}) {
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
    authenticationEnabled: true,
    runtime: [],
    workspace: ['EngineeringModify', 'AssetProcessIntelligenceManage']
  } }));
  await page.route('**/api/engineering/asset-process-intelligence/scopes', async route => {
    const resources = options.emptyResources ? [] : [
      { kind: 'Equipment', scopeId: '15000000-0000-0000-0000-000000000301', path: 'Plant.Pump01', name: 'Pump P01' },
      { kind: 'Equipment', scopeId: '15000000-0000-0000-0000-000000000302', path: 'Plant.Pump02', name: 'Pump P02' }
    ];
    await route.fulfill({ json: {
      configuration: { version: read().version, scopes: read().scopes, profiles: read().profiles, assignments: read().assignments, processStages: read().processStages },
      resources,
      tags: options.emptyResources ? [] : [
        { id: '15000000-0000-0000-0000-000000000101', name: 'Temperature', path: 'Plant.Pump.Temperature', dataType: 'Double', engineeringUnit: '°C' },
        { id: '15000000-0000-0000-0000-000000000102', name: 'Flow', path: 'Plant.Pump.Flow', dataType: 'Double', engineeringUnit: 'm³/h' },
        { id: '15000000-0000-0000-0000-000000000103', name: 'Current', path: 'Plant.Pump.Current', dataType: 'Double', engineeringUnit: 'A', controlledEngineeringUnit: { catalogVersion: 1, code: 'unit.electric_current.ampere' } }
      ],
      licensed: false,
      execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0, activePermitCount: 0, projectKey: 'test', activeRevision: 1, workspaceDirty: false }
    } });
  });
  await page.route('**/api/engineering/asset-process-intelligence/configuration', async route => {
    const payload = route.request().postDataJSON() as Record<string, any>;
    write(payload);
    await route.fulfill({ json: { configuration: { version: 2, scopes: read().scopes, ...payload }, workspace: {} } });
  });
}
