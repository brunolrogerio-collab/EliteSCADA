import { expect, test } from '@playwright/test';

const memoryDefinitions = [
  {
    dataSourceKey: 'memory.client.e2e',
    name: 'Client Memory E2E',
    tags: [
      {
        id: '61000000-0000-0000-0000-000000000001',
        name: 'SelectedPump',
        path: 'UI.SelectedPump',
        dataType: 'String',
        readOnly: false,
        initialValue: 'P01'
      },
      {
        id: '61000000-0000-0000-0000-000000000002',
        name: 'Counter64',
        path: 'UI.Counter64',
        dataType: 'Int64',
        readOnly: false,
        initialValue: '9223372036854775807'
      }
    ]
  }
];

test('Client Memory is isolated per opened runtime page and preserves exact Int64 values', async ({ context }) => {
  const first = await context.newPage();
  const second = await context.newPage();

  for (const page of [first, second]) {
    await page.route('**/api/internal-memory/client/definitions', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(memoryDefinitions) }));
  }

  await Promise.all([first.goto('/'), second.goto('/')]);
  await expect(first.getByText(/2 Client Memory/)).toBeVisible();
  await expect(second.getByText(/2 Client Memory/)).toBeVisible();

  const firstValue = await first.evaluate(async () => {
    const module = await import('/src/runtime/clientMemory.ts');
    module.clientMemory.write('UI.SelectedPump', 'P02');
    return module.clientMemory.read('UI.SelectedPump');
  });
  expect(firstValue).toBe('P02');

  const secondValue = await second.evaluate(async () => {
    const module = await import('/src/runtime/clientMemory.ts');
    return module.clientMemory.read('UI.SelectedPump');
  });
  expect(secondValue).toBe('P01');

  const exactInt64 = await first.evaluate(async () => {
    const module = await import('/src/runtime/clientMemory.ts');
    return module.clientMemory.read('UI.Counter64');
  });
  expect(exactInt64).toBe('9223372036854775807');

  const invalidInt64Rejected = await first.evaluate(async () => {
    const module = await import('/src/runtime/clientMemory.ts');
    try {
      module.clientMemory.write('UI.Counter64', 'not-an-int64');
      return false;
    } catch {
      return true;
    }
  });
  expect(invalidInt64Rejected).toBeTruthy();
});

test('Engineering TAG view previews and applies typed Internal Memory initial value', async ({ page }) => {
  const workspace = {
    projectKey: 'memory-ui-e2e',
    projectName: 'Memory UI E2E',
    baseRevision: 1,
    checkedOutAtUtc: new Date().toISOString(),
    lastSavedAtUtc: new Date().toISOString(),
    isDirty: false,
    changeVersion: 7,
    tagCount: 1,
    alarmCount: 0,
    dataSourceCount: 1,
    templateCount: 0,
    equipmentCount: 0,
    dynamoCount: 0,
    screenCount: 0,
    popupCount: 0,
    securityRoleCount: 0
  };
  const engineering = {
    schema: 'scada.engineering',
    schemaVersion: 8,
    exportedAt: new Date().toISOString(),
    tags: [
      {
        id: '62000000-0000-0000-0000-000000000001',
        name: 'SelectedPump',
        path: 'UI.SelectedPump',
        dataType: 'string',
        source: 'memory.client.e2e',
        readOnly: false,
        initialValue: { dataType: 'string', value: 'P01' }
      }
    ],
    alarms: [],
    dataSources: [
      {
        id: '62000000-0000-0000-0000-000000000002',
        key: 'memory.client.e2e',
        name: 'Client Memory E2E',
        driver: 'builtin.memory.client',
        enabled: true
      }
    ],
    templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [], commands: []
  };

  let previewBody: any = null;
  let applyBody: any = null;
  let applyVersion: string | null = null;

  await page.route('**/api/engineering/workspace', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(workspace) }));
  await page.route('**/api/engineering/export/json', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(engineering) }));
  await page.route('**/api/engineering/import/json/preview', async route => {
    previewBody = route.request().postDataJSON();
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        mode: 'CreateAndUpdate', createCount: 0, updateCount: 1, skipCount: 0,
        errorCount: 0, items: [], canApply: true
      })
    });
  });
  await page.route('**/api/engineering/import/json/apply', async route => {
    applyBody = route.request().postDataJSON();
    applyVersion = route.request().headers()['x-elitescada-workspace-version'] ?? null;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ mode: 'CreateAndUpdate', created: 0, updated: 1, skipped: 0, issues: [] })
    });
  });

  await page.goto('/engineering');
  await page.getByRole('button', { name: /^TAGs\b/ }).click();

  const panel = page.getByTestId('memory-engineering-panel');
  await expect(panel).toBeVisible();
  await expect(panel).toContainText('builtin.memory.client');

  const input = page.getByTestId('memory-initial-value');
  await expect(input).toHaveValue('P01');
  await input.fill('P02');
  await page.getByTestId('memory-initial-preview').click();

  await expect.poll(() => previewBody?.tags?.[0]?.initialValue?.value ?? null).toBe('P02');
  await expect(page.getByTestId('memory-initial-apply')).toBeEnabled();
  await page.getByTestId('memory-initial-apply').click();

  await expect.poll(() => applyBody?.tags?.[0]?.initialValue?.value ?? null).toBe('P02');
  expect(applyVersion).toBe('7');
});