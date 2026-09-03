import { expect, test } from '@playwright/test';

const projectKey = 'e2e-wave03';
const runtimeTagId = '63000000-0000-0000-0000-000000000001';
const runtimeSourceId = '63000000-0000-0000-0000-000000000002';
const runtimeSourceKey = 'memory.server.wave03';
const runtimeTagPath = 'Wave03.RuntimeValue';

test('Wave 03 integrated composition publishes, activates and operates through mounted product surfaces', async ({ page, request }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
  });

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as { changeVersion: number };

  const exportResponse = await request.get('/api/engineering/export/json');
  expect(exportResponse.ok()).toBeTruthy();
  const engineering = await exportResponse.json() as any;

  const activatableEngineering = {
    ...engineering,
    exportedAt: new Date().toISOString(),
    dataSources: [
      ...(engineering.dataSources ?? [])
        .filter((source: any) => source.id !== runtimeSourceId && source.key !== runtimeSourceKey)
        .map((source: any) => ({
          ...source,
          enabled: false
        })),
      {
        id: runtimeSourceId,
        key: runtimeSourceKey,
        name: 'Wave 03 Server Memory',
        driver: 'builtin.memory.server',
        enabled: true
      }
    ],
    tags: [
      ...(engineering.tags ?? [])
        .filter((tag: any) => tag.id !== runtimeTagId && tag.path !== runtimeTagPath),
      {
        id: runtimeTagId,
        name: 'RuntimeValue',
        path: runtimeTagPath,
        dataType: 'int32',
        source: runtimeSourceKey,
        readOnly: false,
        description: 'Wave 03 persisted Runtime acceptance value',
        initialValue: { dataType: 'int32', value: 42 }
      }
    ],
    alarms: (engineering.alarms ?? []).map((alarm: any) => ({
      ...alarm,
      enabled: false
    })),
    commands: (engineering.commands ?? []).map((command: any) => ({
      ...command,
      enabled: false
    })),
    gateways: (engineering.gateways ?? []).map((gateway: any) => ({
      ...gateway,
      enabled: false
    }))
  };

  const preview = await request.post('/api/engineering/import/json/preview', {
    data: activatableEngineering
  });
  expect(preview.ok()).toBeTruthy();
  const previewResult = await preview.json() as { canApply?: boolean; errorCount?: number };
  expect(previewResult.errorCount ?? 0).toBe(0);
  expect(previewResult.canApply ?? true).toBeTruthy();

  const applied = await request.post('/api/engineering/import/json/apply', {
    headers: {
      'x-elitescada-workspace-version': String(workspace.changeVersion)
    },
    data: activatableEngineering
  });
  expect(applied.ok()).toBeTruthy();

  const seeded = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 03 E2E' }
  });
  expect(seeded.ok()).toBeTruthy();

  await page.goto('/engineering');

  const lifecycle = page.locator('.eng-lifecycle-workspace');
  await expect(lifecycle).toHaveCount(1);
  await expect(lifecycle).toBeVisible();
  await expect(lifecycle.getByRole('heading', { name: 'Ciclo do Engineering' })).toBeVisible();
  await expect(lifecycle).toContainText('Wave 03 E2E');
  await expect(lifecycle).toContainText(/r\d+/);

  const publish = lifecycle.getByRole('button', { name: 'Publicar', exact: true }).first();
  await expect(publish).toBeEnabled();
  await publish.click();

  const publishConfirmation = lifecycle.getByRole('dialog');
  await expect(publishConfirmation).toContainText('Publicar a revisão?');
  await publishConfirmation.getByRole('button', { name: 'Publicar revisão' }).click();
  await expect(lifecycle).toContainText('Published');

  const activate = lifecycle.getByRole('button', { name: 'Ativar Published' });
  await expect(activate).toBeEnabled();
  await activate.click();

  const activationConfirmation = lifecycle.getByRole('dialog');
  await expect(activationConfirmation).toContainText('Ativar a revisão Published?');
  await activationConfirmation.getByRole('button', { name: 'Ativar Published' }).click();

  await expect(lifecycle.getByRole('status')).toContainText('A revisão Published foi ativada com sucesso.');
  await expect(lifecycle).toContainText('Coincide com Active durável');

  const runtimeState = await request.get(`/api/engineering/persistence/${projectKey}/runtime`);
  expect(runtimeState.ok()).toBeTruthy();
  const runtime = await runtimeState.json() as {
    consistent: boolean;
    durable: { activeRevision?: number | null };
    live: { revision?: number | null; tagCount?: number };
  };
  expect(runtime.consistent).toBeTruthy();
  expect(runtime.durable.activeRevision).toBeTruthy();
  expect(runtime.live.revision).toBe(runtime.durable.activeRevision);
  expect(runtime.live.tagCount).toBe(1);

  const activeTagsResponse = await request.get('/api/tags');
  expect(activeTagsResponse.ok()).toBeTruthy();
  const activeTags = await activeTagsResponse.json() as Array<{ id: string; path: string }>;
  expect(activeTags).toHaveLength(1);
  expect(activeTags[0]).toMatchObject({ id: runtimeTagId, path: runtimeTagPath });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await page.getByRole('button', { name: 'Alarmes', exact: true }).click();
  const alarmOverlay = page.locator('.runtime-operator-overlay');
  await expect(alarmOverlay).toBeVisible();
  await expect(alarmOverlay.getByRole('heading', { name: 'Central de alarmes' })).toBeVisible();
  await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);

  await page.goto('/engineering/diagnostics/tag-monitor');
  const tagMonitor = page.getByTestId('engineering-tag-monitor');
  await expect(tagMonitor).toBeVisible();
  await expect(tagMonitor).toHaveAttribute('data-active-runtime-project', projectKey);
  await expect(tagMonitor).toHaveAttribute('data-active-runtime-revision', String(runtime.live.revision));

  const inspector = tagMonitor.locator('.runtime-tag-inspector');
  await expect(inspector).toBeVisible();
  await expect(inspector.getByRole('listbox', { name: 'Inspector de TAGs' }).getByText(runtimeTagPath, { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(inspector.getByText('42', { exact: true }).first()).toBeVisible();
});