import { expect, test, type Browser, type BrowserContext, type Page } from '@playwright/test';
import { createE2eJwt } from '../tests-e2e/jwt';

const projectKey = 'e2e-wave11';
const serverSourceKey = 'memory.server.c17';
const clientSourceKey = 'memory.client.c17';
const serverTagPath = 'C17.Server.Value';
const clientTagPath = 'C17.Client.Value';
const runtimeClientToken = createE2eJwt('wave11-c17-runtime-client', ['developer'], 'Wave 11 C17 Runtime Client');

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

test('Internal Memory is authored through normal Engineering UI and survives Save Publish Activate', async ({ page, request, browser }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as any;

  try {
    await createDataSource(page, 'Server Memory C17', serverSourceKey, 'builtin.memory.server');
    await createDataSource(page, 'Client Memory C17', clientSourceKey, 'builtin.memory.client');

    await createMemoryTag(page, {
      name: 'Server Value C17',
      path: serverTagPath,
      dataType: 'double',
      sourceKey: serverSourceKey,
      writable: true,
      historian: true
    });
    await createMemoryTag(page, {
      name: 'Client Value C17',
      path: clientTagPath,
      dataType: 'string',
      sourceKey: clientSourceKey,
      writable: true,
      historian: false
    });

    await setMemoryInitialValue(page, serverTagPath, '12.5');
    await setMemoryInitialValue(page, clientTagPath, 'client-default');

    const workingResponse = await request.get('/api/engineering/export/json');
    expect(workingResponse.ok()).toBeTruthy();
    const working = await workingResponse.json() as any;
    const serverSource = working.dataSources.find((source: any) => source.key === serverSourceKey);
    const clientSource = working.dataSources.find((source: any) => source.key === clientSourceKey);
    const serverTag = working.tags.find((tag: any) => tag.path === serverTagPath);
    const clientTag = working.tags.find((tag: any) => tag.path === clientTagPath);

    expect(serverSource?.driver).toBe('builtin.memory.server');
    expect(clientSource?.driver).toBe('builtin.memory.client');
    expect(serverTag?.address ?? null).toBeNull();
    expect(clientTag?.address ?? null).toBeNull();
    expect(serverTag?.initialValue).toMatchObject({ dataType: 'double', value: 12.5 });
    expect(clientTag?.initialValue).toMatchObject({ dataType: 'string', value: 'client-default' });
    expect(serverTag?.historian?.enabled).toBe(true);
    expect(serverTag?.id).toBeTruthy();
    expect(clientTag?.id).toBeTruthy();

    const saved = await savePublishActivate(request, 'Wave 14 C17 Memory Lifecycle');

    const runtimeResponse = await request.get('/api/runtime/application');
    expect(runtimeResponse.ok()).toBeTruthy();
    const runtime = await runtimeResponse.json() as any;
    expect(runtime.mode).toBe('engineering');
    expect(runtime.projectKey).toBe(projectKey);
    expect(runtime.revision).toBe(saved.revision);

    await expect.poll(async () => {
      const response = await request.get(`/api/tags/by-path/${encodeURIComponent(serverTagPath)}`);
      if (!response.ok()) return null;
      const payload = await response.json() as any;
      return payload.current?.value ?? null;
    }).toBe(12.5);

    const writeResponse = await request.post(`/api/tags/${serverTag.id}/write`, { data: { value: 42.25 } });
    expect(writeResponse.status()).toBe(202);
    await expect.poll(async () => {
      const response = await request.get(`/api/tags/by-path/${encodeURIComponent(serverTagPath)}`);
      if (!response.ok()) return null;
      const payload = await response.json() as any;
      return payload.current?.value ?? null;
    }).toBe(42.25);

    await expect.poll(async () => {
      const response = await request.get(`/api/history/${serverTag.id}?limit=50`);
      if (!response.ok()) return 0;
      const samples = await response.json() as any[];
      return samples.length;
    }, { timeout: 15_000 }).toBeGreaterThan(0);

    // Re-activation recreates the server-owned Memory runtime. The retained value
    // must win over the Engineering default when the retained type is compatible.
    const reactivate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
    expect(reactivate.ok(), `Re-activate failed: ${reactivate.status()} ${await reactivate.text()}`).toBeTruthy();
    await expect.poll(async () => {
      const response = await request.get(`/api/tags/by-path/${encodeURIComponent(serverTagPath)}`);
      if (!response.ok()) return null;
      const payload = await response.json() as any;
      return payload.current?.value ?? null;
    }).toBe(42.25);

    const clientDefinitionsResponse = await request.get('/api/internal-memory/client/definitions');
    expect(clientDefinitionsResponse.ok()).toBeTruthy();
    const clientDefinitions = await clientDefinitionsResponse.json() as any[];
    expect(clientDefinitions.some(source => source.dataSourceKey === clientSourceKey)).toBeTruthy();
    expect(clientDefinitions.some(source => source.dataSourceKey === serverSourceKey)).toBeFalsy();
    expect(clientDefinitions.flatMap(source => source.tags).find(tag => tag.path === clientTagPath)?.initialValue).toBe('client-default');

    const firstContext = await createRuntimeClientContext(browser);
    const secondContext = await createRuntimeClientContext(browser);
    try {
      const first = await firstContext.newPage();
      const second = await secondContext.newPage();
      await Promise.all([first.goto('/'), second.goto('/')]);

      await expect.poll(() => readClientMemory(first, clientTagPath)).toBe('client-default');
      await expect.poll(() => readClientMemory(second, clientTagPath)).toBe('client-default');

      const firstWrite = await first.evaluate(async ({ path }) => {
        const module = await import('/src/runtime/clientMemory.ts');
        await module.clientMemory.ensureInitialized();
        module.clientMemory.write(path, 'client-one');
        return module.clientMemory.read(path);
      }, { path: clientTagPath });
      expect(firstWrite).toBe('client-one');
      expect(await readClientMemory(second, clientTagPath)).toBe('client-default');
    } finally {
      await Promise.allSettled([firstContext.close(), secondContext.close()]);
    }
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', { data: originalPackage });
    expect(restore.ok(), `Restore apply failed: ${restore.status()} ${await restore.text()}`).toBeTruthy();
    await savePublishActivate(request, 'Wave 14 C17 cleanup');
  }
});

async function createRuntimeClientContext(browser: Browser): Promise<BrowserContext> {
  return await browser.newContext({
    baseURL: 'http://127.0.0.1:5174',
    locale: 'pt-BR',
    extraHTTPHeaders: {
      Authorization: `Bearer ${runtimeClientToken}`
    }
  });
}

async function createDataSource(page: Page, name: string, key: string, typeKey: string) {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Data Sources/ }).click();
  const editor = page.getByTestId('schema-data-source-editor');
  await expect(editor).toBeVisible();
  await editor.locator('header button').first().click();
  await page.getByTestId('data-source-type').selectOption(typeKey);

  const basicFields = editor.locator('.eng-editor-form-grid').first().locator('input');
  await basicFields.nth(0).fill(name);
  await basicFields.nth(1).fill(key);
  await page.getByTestId('data-source-preview').click();
  await expect(page.getByTestId('data-source-apply')).toBeEnabled();
  await page.getByTestId('data-source-apply').click();
  await page.waitForLoadState('domcontentloaded');
  await expect.poll(async () => {
    const response = await page.request.get('/api/engineering/export/json');
    if (!response.ok()) return false;
    const model = await response.json() as any;
    return model.dataSources.some((source: any) => source.key === key && source.driver === typeKey);
  }).toBeTruthy();
}

async function createMemoryTag(page: Page, options: {
  name: string;
  path: string;
  dataType: string;
  sourceKey: string;
  writable: boolean;
  historian: boolean;
}) {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /^TAGs\b/ }).click();
  await page.getByRole('button', { name: 'Nova TAG' }).click();
  await page.getByLabel('Nome').fill(options.name);
  await page.getByLabel('Path').fill(options.path);
  await page.getByLabel('Tipo de dado').selectOption(options.dataType);

  const sourceSelect = page.getByTestId('tag-source-select');
  const sourceOption = await sourceSelect.locator('option').evaluateAll((items, key) =>
    items.map(item => ({ value: (item as HTMLOptionElement).value, text: item.textContent ?? '' }))
      .find(item => item.text.includes(key))?.value ?? null, options.sourceKey);
  expect(sourceOption).toBeTruthy();
  await sourceSelect.selectOption(sourceOption!);

  // C11-P2-MEM-02: source providers do not have a network address contract.
  await expect(page.getByTestId('tag-address-manual')).toHaveCount(0);
  await expect(page.getByTestId('modbus-address-assistant')).toHaveCount(0);

  const readOnly = page.getByLabel('Somente leitura');
  if (options.writable) await readOnly.uncheck();
  if (options.historian) await page.getByLabel('Histórico habilitado').check();

  await page.getByRole('button', { name: 'Validar preview' }).click();
  await expect(page.getByText('Rascunho válido para aplicação', { exact: true })).toBeVisible();
  await expect(page.getByTestId('engineering-apply')).toBeEnabled();
  await page.getByTestId('engineering-apply').click();
  await page.waitForLoadState('domcontentloaded');

  await expect.poll(async () => {
    const response = await page.request.get('/api/engineering/export/json');
    if (!response.ok()) return false;
    const model = await response.json() as any;
    return model.tags.some((tag: any) => tag.path === options.path && tag.source === options.sourceKey);
  }).toBeTruthy();
}

async function setMemoryInitialValue(page: Page, path: string, value: string) {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /^TAGs\b/ }).click();
  const panel = page.getByTestId('memory-engineering-panel');
  await expect(panel).toBeVisible();
  await panel.locator('select').first().selectOption({ label: path });
  await page.getByTestId('memory-initial-value').fill(value);
  await page.getByTestId('memory-initial-preview').click();
  await expect(page.getByTestId('memory-initial-apply')).toBeEnabled();
  await page.getByTestId('memory-initial-apply').click();
  await page.waitForLoadState('domcontentloaded');
}

async function savePublishActivate(request: any, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${projectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save failed: ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };

  const publish = await request.post(`/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish failed: ${publish.status()} ${await publish.text()}`).toBeTruthy();

  const activate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate failed: ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function readClientMemory(page: Page, path: string): Promise<unknown> {
  return await page.evaluate(async ({ memoryPath }) => {
    const module = await import('/src/runtime/clientMemory.ts');
    try {
      await module.clientMemory.ensureInitialized();
      return module.clientMemory.read(memoryPath);
    } catch {
      return null;
    }
  }, { memoryPath: path });
}
