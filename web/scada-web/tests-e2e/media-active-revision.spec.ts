import { createHash, randomUUID } from 'node:crypto';
import { expect, test, type APIRequestContext } from '@playwright/test';
import { admitInteractiveRuntimeSession, terminateRuntimeSession } from './runtimeSessionLease';
import { createE2eJwt } from './jwt';

// Real API + PostgreSQL + Runtime activation. No page.route or mocked Active package.
test('persisted Active media and PDF identities survive draft changes and render in Runtime', async ({ page, request }) => {
  test.setTimeout(90_000);
  let runtimeHeaders = await admitInteractiveRuntimeSession(request);
  const runtimeGet = async (url: string) => {
    // Activation invalidates the previous revision-bound lease by design.
    runtimeHeaders = await admitInteractiveRuntimeSession(request);
    return request.get(url, { headers: runtimeHeaders });
  };
  const original = await (await request.get('/api/engineering/export/json')).json();
  const projectResponse = await runtimeGet('/api/runtime/application');
  expect(projectResponse.ok(), await projectResponse.text()).toBeTruthy();
  const project = await projectResponse.json();
  const projectKey = project.projectKey;
  expect(projectKey).toBeTruthy();
  const sourceId = randomUUID();
  let assetId: string | undefined;
  const source = { id: sourceId, key: `e2e-media-${sourceId}`, name: 'Persisted camera', protocol: 'hls', endpoint: 'https://camera.example.test/live.m3u8', enabled: true };
  const pdf = createPdf();
  try {
    const imported = await request.post(`/api/engineering/visual-assets/import?key=e2e-active-pdf-${sourceId}&name=Active%20PDF&fileName=active.pdf`, {
      headers: { ...(await version(request)), 'content-type': 'application/pdf' }, data: pdf
    });
    expect(imported.ok(), await imported.text()).toBeTruthy();
    const { asset } = await imported.json();
    assetId = asset.id;
    const working = await (await request.get('/api/engineering/export/json')).json();
    const screen = working.screens[0];
    const packageData = {
      ...working, mediaSources: [...(working.mediaSources ?? []), source],
      screens: working.screens.map((item: any) => item.id === screen.id ? { ...item, elements: [
        { id: randomUUID(), key: 'persisted-pdf', type: 'core.pdfViewer', properties: {
          x: 20, y: 20, width: 600, height: 400, assetRef: { assetId: `asset:${assetId}` }, pdfInitialPage: 1, pdfZoom: 125, pdfToolbarVisible: false
        } },
        { id: randomUUID(), key: 'persisted-camera', type: 'core.videoPlayer', properties: {
          x: 650, y: 20, width: 400, height: 260, mediaSourceId: sourceId, mediaAutoplay: false, mediaMuted: true, mediaControls: true
        } }
      ] } : item)
    };
    await apply(request, packageData);
    // A draft source/asset is never available through Runtime before publication.
    expect((await runtimeGet(`/api/runtime/media-sources/${sourceId}/info`)).status()).toBe(404);
    expect((await runtimeGet(`/api/runtime/visual-assets/${assetId}/content`)).status()).toBe(404);
    const revision = await activate(request, projectKey);

    const info = await runtimeGet(`/api/runtime/media-sources/${sourceId}/info`);
    expect(info.status()).toBe(200);
    expect(await info.json()).toMatchObject({ id: sourceId, name: 'Persisted camera', protocol: 'hls', state: 'ready' });
    expect(await info.text()).not.toContain(source.endpoint);
    const payload = await runtimeGet(`/api/runtime/visual-assets/${assetId}/content`);
    expect(payload.status()).toBe(200);
    expect(payload.headers()['content-type']).toContain('application/pdf');
    expect(await payload.body()).toEqual(pdf);
    expect(payload.headers().etag).toBe(`"${createHash('sha256').update(pdf).digest('hex')}"`);

    // Change Working without publishing: the existing Active source and bytes must remain.
    await apply(request, { ...packageData, mediaSources: packageData.mediaSources.map((item: any) => item.id === sourceId ? { ...item, enabled: false } : item) });
    expect((await runtimeGet(`/api/runtime/media-sources/${sourceId}/info`)).status()).toBe(200);
    const projection = await (await runtimeGet('/api/runtime/application')).json();
    expect(projection.revision).toBe(revision);
    expect(projection.package.screens[0].elements.map((item: any) => item.type)).toEqual(['core.pdfViewer', 'core.videoPlayer']);

    await page.context().setExtraHTTPHeaders({ Authorization: `Bearer ${createE2eJwt('e2e-developer', ['developer'], 'E2E Developer')}`, ...runtimeHeaders });
    await page.goto('/');
    await expect(page.getByTestId('runtime-engineering-canvas')).toBeVisible();
    const frame = page.locator('iframe[title="persisted-pdf"]');
    await expect(frame).toBeVisible();
    await expect(frame).toHaveAttribute('src', new RegExp(`/api/runtime/visual-assets/${assetId}/content#page=1&zoom=125&toolbar=0`));
    await expect(page.locator('.visual-editor-video-player video')).toBeVisible();
    expect((await runtimeGet(`/api/runtime/visual-assets/${randomUUID()}/content`)).status()).toBe(404);

    await activate(request, projectKey);
    expect((await runtimeGet(`/api/runtime/media-sources/${sourceId}/info`)).status()).toBe(404);
  } finally {
    // Restore the shared fixture through the same public workspace and lifecycle paths.
    await apply(request, original);
    await request.delete(`/api/engineering/media-sources/${sourceId}`, { headers: await version(request) });
    if (assetId) await request.delete(`/api/engineering/visual-assets/${assetId}`, { headers: await version(request) });
    await activate(request, projectKey);
    await terminateRuntimeSession(request, await admitInteractiveRuntimeSession(request));
  }
});

async function version(request: APIRequestContext) {
  const descriptor = await (await request.get('/api/engineering/workspace')).json();
  return { 'x-elitescada-workspace-version': String(descriptor.changeVersion) };
}

async function apply(request: APIRequestContext, data: unknown) {
  const preview = await request.post('/api/engineering/import/json/preview', { data });
  expect(preview.ok(), await preview.text()).toBeTruthy();
  const checked = await preview.json();
  expect(checked.canApply, JSON.stringify(checked.items?.filter((item: any) => item.issues?.some((issue: any) => issue.isError)))).toBe(true);
  const response = await request.post('/api/engineering/import/json/apply', { data, headers: await version(request) });
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function activate(request: APIRequestContext, projectKey: string): Promise<number> {
  const root = `/api/engineering/persistence/${encodeURIComponent(projectKey)}`;
  const save = await request.post(`${root}/save`, { data: { projectName: 'E2E Explicit Demo Fixture' } });
  expect(save.ok(), await save.text()).toBeTruthy();
  const { revision } = await save.json();
  const publish = await request.post(`${root}/revisions/${revision}/publish`, { data: {} });
  expect(publish.ok(), await publish.text()).toBeTruthy();
  const activated = await request.post(`${root}/published/activate`, { data: {} });
  expect(activated.ok(), await activated.text()).toBeTruthy();
  expect((await activated.json()).activated).toBe(true);
  return revision;
}

function createPdf(): Buffer {
  const stream = 'BT /F1 24 Tf 30 100 Td (EliteSCADA Active PDF) Tj ET';
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`
  ];
  let text = '%PDF-1.4\n';
  const offsets: number[] = [];
  objects.forEach((body, index) => { offsets.push(text.length); text += `${index + 1} 0 obj\n${body}\nendobj\n`; });
  const xref = text.length;
  text += `xref\n0 6\n0000000000 65535 f \n${offsets.map(offset => `${String(offset).padStart(10, '0')} 00000 n \n`).join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(text, 'ascii');
}
