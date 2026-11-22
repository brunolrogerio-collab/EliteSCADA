import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('mounted Media Sources screen protects credentials and never exports their values', async ({ page, request }) => {
  const key = `e2e-media-${randomUUID().slice(0, 8)}`;
  const secret = `e2e-token-${randomUUID()}`;
  let createdId: string | null = null;

  try {
    await cleanupE2eMediaSources(request);
    await page.goto('/engineering');
    await page.getByRole('button', { name: 'Fontes de mídia' }).click();
    const editor = page.getByTestId('media-source-workspace');
    await expect(editor).toBeVisible();
    await editor.getByRole('button', { name: 'Nova fonte' }).click();
    await editor.getByTestId('media-source-name').fill('Camera E2E');
    await editor.getByTestId('media-source-key').fill(key);
    await editor.getByTestId('media-source-protocol').selectOption('mjpeg');
    await editor.getByTestId('media-source-endpoint').fill('https://camera.example.test/live.mjpg');
    const createResponsePromise = page.waitForResponse(response =>
      response.request().method() === 'POST' && response.url().endsWith('/api/engineering/media-sources'));
    await editor.getByRole('button', { name: 'Criar fonte' }).click();
    const createResponse = await createResponsePromise;
    expect(createResponse.status(), `Media source create failed: ${await createResponse.text()}`).toBe(201);

    const created = await createResponse.json() as { id?: string };
    createdId = created.id ?? null;
    expect(createdId).toBeTruthy();
    const credentials = page.getByTestId('media-source-credentials');
    await expect(credentials).toBeVisible();
    await credentials.getByTestId('media-source-credential-mode').selectOption('bearer');
    await credentials.getByTestId('media-source-bearer').fill(secret);
    await credentials.getByRole('button', { name: 'Salvar credencial protegida' }).click();
    await expect(credentials).toContainText('Há uma credencial salva no cofre protegido do host');

    const exported = await request.get('/api/engineering/export/json');
    expect(exported.ok()).toBeTruthy();
    expect(await exported.text()).not.toContain(secret);
  } finally {
    await cleanupE2eMediaSources(request, createdId);
  }
});

async function cleanupE2eMediaSources(request: import('@playwright/test').APIRequestContext, preferredId?: string | null) {
  const response = await request.get('/api/engineering/media-sources');
  if (!response.ok()) return;
  const sources = await response.json() as Array<{ id?: string; key: string }>;
  const ids = new Set(sources
    .filter(source => source.id && (source.key.startsWith('e2e-media-') || source.id === preferredId))
    .map(source => source.id!));
  for (const id of ids) {
    const workspace = await request.get('/api/engineering/workspace');
    if (!workspace.ok()) continue;
    const descriptor = await workspace.json() as { changeVersion: number };
    await request.delete(`/api/engineering/media-sources/${id}`, {
      headers: { 'x-elitescada-workspace-version': String(descriptor.changeVersion) }
    });
  }
}
