import { expect, test } from '@playwright/test';

test('Equipment can be created without Template and attach/detach preserves identity', async ({ page }) => {
  const equipmentId = '83000000-0000-0000-0000-000000000001';
  const templateId = '83000000-0000-0000-0000-000000000002';
  const workspace = {
    projectKey: 'equipment-h0-e2e',
    projectName: 'Equipment H0 E2E',
    baseRevision: 1,
    checkedOutAtUtc: new Date().toISOString(),
    lastSavedAtUtc: new Date().toISOString(),
    isDirty: false,
    changeVersion: 7,
    tagCount: 0,
    alarmCount: 0,
    dataSourceCount: 0,
    templateCount: 0,
    equipmentCount: 0,
    dynamoCount: 0,
    screenCount: 0,
    popupCount: 0,
    securityRoleCount: 0
  };
  let engineering: any = {
    schema: 'scada.engineering',
    schemaVersion: 15,
    exportedAt: new Date().toISOString(),
    tags: [],
    alarms: [],
    dataSources: [],
    templates: [],
    equipment: [],
    dynamos: [],
    screens: [],
    popups: [],
    securityRoles: [],
    commands: []
  };
  const applied: any[] = [];

  await page.route('**/api/engineering/workspace', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(workspace) }));
  await page.route('**/api/engineering/export/json', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(engineering) }));
  await page.route('**/api/engineering/import/json/preview', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        mode: 'CreateAndUpdate',
        createCount: 1,
        updateCount: 0,
        skipCount: 0,
        errorCount: 0,
        items: [],
        canApply: true
      })
    }));
  await page.route('**/api/engineering/import/json/apply', async route => {
    const candidate = route.request().postDataJSON() as any;
    const incoming = candidate.equipment?.[0];
    if (incoming) incoming.id ??= equipmentId;
    engineering = candidate;
    applied.push(structuredClone(candidate));

    // Make one Template available after the device-first create, emulating a
    // later authoring/import operation without changing Equipment identity.
    if (applied.length === 1) {
      engineering.templates = [{
        id: templateId,
        key: 'sensor.standard',
        name: 'Sensor Standard',
        bindings: []
      }];
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ mode: 'CreateAndUpdate', created: 1, updated: 0, skipped: 0, issues: [] })
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /Equipamentos|Equipment|Equipo/i }).click();

  const panel = page.getByTestId('equipment-faceplate-workspace');
  await expect(panel).toBeVisible();
  const name = panel.locator('input').first();
  await name.fill('Sensor Sala');
  await panel.locator('select').selectOption('');
  await panel.getByRole('button', { name: /Criar equipamento|Create equipment|Crear equipo/i }).click();

  await expect.poll(() => applied.length).toBe(1);
  expect(applied[0].equipment[0].templateId).toBeNull();
  expect(applied[0].equipment[0].templateKey).toBeNull();
  const stablePath = applied[0].equipment[0].path;

  await expect(panel.locator('select option')).toHaveCount(2);
  await panel.locator('select').selectOption(templateId);
  await panel.getByRole('button', { name: /Salvar alterações|Save changes|Guardar cambios/i }).click();

  await expect.poll(() => applied.length).toBe(2);
  expect(applied[1].equipment[0].id).toBe(equipmentId);
  expect(applied[1].equipment[0].path).toBe(stablePath);
  expect(applied[1].equipment[0].templateId).toBe(templateId);
  expect(applied[1].equipment[0].templateKey).toBe('sensor.standard');

  await panel.locator('select').selectOption('');
  await panel.getByRole('button', { name: /Salvar alterações|Save changes|Guardar cambios/i }).click();

  await expect.poll(() => applied.length).toBe(3);
  expect(applied[2].equipment[0].id).toBe(equipmentId);
  expect(applied[2].equipment[0].path).toBe(stablePath);
  expect(applied[2].equipment[0].templateId).toBeNull();
  expect(applied[2].equipment[0].templateKey).toBeNull();
});

test('Equipment create action remains available when no Templates exist', async () => {
  const source = await import('node:fs/promises').then(fs =>
    fs.readFile(new URL('../src/engineering/EquipmentFaceplateWorkspace.tsx', import.meta.url), 'utf8'));

  expect(source).not.toContain('disabled={busy || templates.length === 0}');
  expect(source).not.toContain('templateRequired');
  expect(source).toContain("templateId: template?.id ?? null");
  expect(source).toContain("templateKey: template?.key ?? null");
});
