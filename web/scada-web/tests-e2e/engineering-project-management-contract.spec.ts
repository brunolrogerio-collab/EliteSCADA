import { randomUUID } from 'node:crypto';
import { expect, request as playwrightRequest, test } from '@playwright/test';
import {
  canonicalJsonCandidateIdentity,
  canonicalTextFingerprint,
  mergeModeLabel,
  previewTokenMatches,
  PROJECT_PORTABILITY_MERGE_MODES,
  projectPortabilityFileFingerprint
} from '../src/engineering/EngineeringProjectManagementWorkspace.logic';
import type { PortabilityPreviewToken } from '../src/engineering/projectPortabilityTypes';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';
const packageMediaType = 'application/vnd.elitescada.project-package';

test('project portability exposes only the canonical merge modes', () => {
  expect(PROJECT_PORTABILITY_MERGE_MODES).toEqual([
    'CreateOnly',
    'UpdateExisting',
    'CreateAndUpdate'
  ]);
  expect(mergeModeLabel('CreateAndUpdate', 'pt-BR')).toBe('Criar e atualizar');
  expect(mergeModeLabel('UpdateExisting', 'en')).toBe('Update existing');
  expect(mergeModeLabel('CreateOnly', 'es')).toBe('Crear solo nuevos');
});

test('canonical JSON inspection reports identity without inventing missing metadata', () => {
  expect(canonicalJsonCandidateIdentity(JSON.stringify({
    schema: 'scada.engineering',
    schemaVersion: 9,
    tags: []
  }))).toEqual({ validJson: true, schema: 'scada.engineering', schemaVersion: 9 });

  expect(canonicalJsonCandidateIdentity(JSON.stringify({ tags: [] }))).toEqual({
    validJson: true,
    schema: null,
    schemaVersion: null
  });
  expect(canonicalJsonCandidateIdentity('{broken')).toEqual({
    validJson: false,
    schema: null,
    schemaVersion: null
  });
});

test('Preview is bound to exact source fingerprint and merge mode', () => {
  const file = new File(['{"schema":"scada.engineering"}'], 'project.json', {
    type: 'application/json',
    lastModified: 123
  });
  const source = canonicalTextFingerprint(file, '{"schema":"scada.engineering"}');
  const token: PortabilityPreviewToken = {
    sourceFingerprint: source,
    mode: 'CreateAndUpdate',
    expectedChangeVersion: 42,
    preview: {
      mode: 'CreateAndUpdate',
      createCount: 1,
      updateCount: 2,
      skipCount: 0,
      errorCount: 0,
      items: [],
      canApply: true
    }
  };

  expect(previewTokenMatches(token, source, 'CreateAndUpdate')).toBeTruthy();
  expect(previewTokenMatches(token, source, 'CreateOnly')).toBeFalsy();
  expect(previewTokenMatches(token, `${source}-changed`, 'CreateAndUpdate')).toBeFalsy();
  expect(projectPortabilityFileFingerprint(file)).toBe('project.json::30::123');
});

test('flat Engineering tables export and preview round-trip through CSV and XLSX contracts', async ({ request }) => {
  const entities = ['tags', 'alarms', 'datasources', 'gateway-routes', 'operational-events'] as const;
  for (const entity of entities) {
    for (const format of ['csv', 'xlsx'] as const) {
      const exportResponse = await request.get(`/api/engineering/export/tabular/${entity}/${format}`);
      expect(exportResponse.ok(), `${entity}.${format} export`).toBeTruthy();
      const contentType = exportResponse.headers()['content-type'] ?? '';
      expect(contentType).toContain(format === 'csv' ? 'text/csv' : 'spreadsheetml.sheet');
      expect(exportResponse.headers()['content-disposition']).toContain(`scada-${entity}.${format}`);
      const bytes = await exportResponse.body();
      expect(bytes.byteLength).toBeGreaterThan(0);

      const previewResponse = await request.post(
        `/api/engineering/import/tabular/${entity}/${format}/preview?mode=CreateAndUpdate`,
        {
          headers: {
            'content-type': format === 'csv' ? 'text/csv; charset=utf-8' : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
          },
          data: bytes
        }
      );
      expect(previewResponse.ok(), `${entity}.${format} preview: ${await previewResponse.text()}`).toBeTruthy();
      const preview = await previewResponse.json() as { canApply: boolean; errorCount: number };
      expect(preview.canApply, `${entity}.${format} preview can apply`).toBeTruthy();
      expect(preview.errorCount, `${entity}.${format} preview errors`).toBe(0);
    }
  }
});

test('invalid Gateway CSV preview and Apply leave the Engineering workspace unchanged', async ({ request }) => {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number };
  const gatewaysBeforeResponse = await request.get('/api/engineering/export/tabular/gateway-routes/csv');
  expect(gatewaysBeforeResponse.ok()).toBeTruthy();
  const gatewaysBefore = await gatewaysBeforeResponse.text();

  const invalidRouteCsv = [
    'Id,Key,Name,SourceTagId,SourceTagPath,DestinationTagId,DestinationTagPath',
    `${randomUUID()},invalid-api-route,Invalid API route,${randomUUID()},Plant.MissingSource,${randomUUID()},Plant.MissingDestination`
  ].join('\n');
  const headers = { 'content-type': 'text/csv; charset=utf-8' };
  const previewResponse = await request.post(
    '/api/engineering/import/tabular/gateway-routes/csv/preview?mode=CreateAndUpdate',
    { headers, data: invalidRouteCsv }
  );
  expect(previewResponse.ok()).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number };
  expect(preview.canApply).toBeFalsy();
  expect(preview.errorCount).toBeGreaterThan(0);

  const applyResponse = await request.post(
    '/api/engineering/import/tabular/gateway-routes/csv/apply?mode=CreateAndUpdate',
    {
      headers: {
        ...headers,
        'x-elitescada-workspace-version': String(workspaceBefore.changeVersion)
      },
      data: invalidRouteCsv
    }
  );
  expect(applyResponse.status()).toBe(400);
  const rejectedApply = await applyResponse.json() as { canApply: boolean; errorCount: number };
  expect(rejectedApply.canApply).toBeFalsy();
  expect(rejectedApply.errorCount).toBeGreaterThan(0);

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
  expect(workspaceAfter.changeVersion).toBe(workspaceBefore.changeVersion);
  const gatewaysAfterResponse = await request.get('/api/engineering/export/tabular/gateway-routes/csv');
  expect(gatewaysAfterResponse.ok()).toBeTruthy();
  expect(await gatewaysAfterResponse.text()).toBe(gatewaysBefore);
});

test('unsupported tabular CSV headers are rejected before Preview or Apply can mutate the workspace', async ({ request }) => {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number };
  const tagsBeforeResponse = await request.get('/api/engineering/export/tabular/tags/csv');
  expect(tagsBeforeResponse.ok()).toBeTruthy();
  const tagsBefore = await tagsBeforeResponse.text();

  const unsupportedCsv = [
    'Id,Name,Path,DataType,FutureEngineeringField',
    `${randomUUID()},Unsupported import,Plant.UnsupportedImport,Float,discard-me`
  ].join('\n');
  const headers = { 'content-type': 'text/csv; charset=utf-8' };
  const previewResponse = await request.post(
    '/api/engineering/import/tabular/tags/csv/preview?mode=CreateAndUpdate',
    { headers, data: unsupportedCsv }
  );
  expect(previewResponse.status()).toBe(400);
  const previewError = await previewResponse.json() as { error: string };
  expect(previewError.error).toContain('FutureEngineeringField');
  expect(previewError.error).toContain('unsupported column');

  const applyResponse = await request.post(
    '/api/engineering/import/tabular/tags/csv/apply?mode=CreateAndUpdate',
    {
      headers: {
        ...headers,
        'x-elitescada-workspace-version': String(workspaceBefore.changeVersion)
      },
      data: unsupportedCsv
    }
  );
  expect(applyResponse.status()).toBe(400);
  const applyError = await applyResponse.json() as { error: string };
  expect(applyError.error).toContain('FutureEngineeringField');
  expect(applyError.error).toContain('unsupported column');

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
  expect(workspaceAfter.changeVersion).toBe(workspaceBefore.changeVersion);
  const tagsAfterResponse = await request.get('/api/engineering/export/tabular/tags/csv');
  expect(tagsAfterResponse.ok()).toBeTruthy();
  expect(await tagsAfterResponse.text()).toBe(tagsBefore);
});

test('tabular import/export can be operated from the Engineering project-management screen', async ({ page, request }) => {
  await page.goto('/engineering');
  const advancedTools = page.locator('details.eng-overview-advanced');
  await advancedTools.locator('summary').click();

  const card = page.getByTestId('project-tabular-portability');
  await expect(card).toBeVisible();

  const [xlsxDownload] = await Promise.all([
    page.waitForEvent('download'),
    card.getByTestId('project-tabular-export-xlsx').click()
  ]);
  expect(xlsxDownload.suggestedFilename()).toBe('scada-tags.xlsx');

  const exportResponse = await request.get('/api/engineering/export/tabular/tags/csv');
  expect(exportResponse.ok()).toBeTruthy();
  const source = await exportResponse.body();
  await card.locator('input[type="file"]').setInputFiles({
    name: 'tags-round-trip.csv',
    mimeType: 'text/csv',
    buffer: source
  });

  const previewResponsePromise = page.waitForResponse(response =>
    response.url().includes('/api/engineering/import/tabular/tags/csv/preview')
  );
  await card.getByTestId('project-tabular-preview').click();
  const previewResponse = await previewResponsePromise;
  const previewBody = await previewResponse.json() as {
    canApply?: boolean;
    errorCount?: number;
  };
  expect(previewBody.canApply).toBeTruthy();
  expect(previewBody.errorCount).toBe(0);
  const preview = card.getByTestId('project-portability-preview-result');
  await expect(preview).toBeVisible();
  await expect(preview.locator('.eng-project-management__preview-counts div').nth(3).locator('dd')).toHaveText('0');

  await card.getByTestId('project-tabular-apply').click();
  const confirmation = page.getByRole('dialog');
  await expect(confirmation).toBeVisible();
  await confirmation.getByRole('button', { name: /Aplicar tabela validada|Apply validated table|Aplicar tabla validada/i }).click();
  await expect(page.getByRole('status')).toContainText(/aplicada|applied/i);
});

test('project package restore requires authorization and rejects stale Workspace versions', async ({ request }) => {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as {
    projectKey?: string | null;
    projectName?: string | null;
    changeVersion: number;
  };
  expect(workspace.projectKey).toBeTruthy();
  expect(workspace.projectName).toBeTruthy();

  const query = new URLSearchParams({
    projectKey: workspace.projectKey!,
    projectName: workspace.projectName!
  });
  const exportResponse = await request.get(`/api/project-package/export?${query.toString()}`);
  expect(exportResponse.ok()).toBeTruthy();
  expect(exportResponse.headers()['content-type']).toContain(packageMediaType);
  const packageBytes = await exportResponse.body();
  expect(packageBytes.byteLength).toBeGreaterThan(0);

  const previewResponse = await request.post('/api/project-package/import/preview?mode=CreateAndUpdate', {
    headers: { 'content-type': packageMediaType },
    data: packageBytes
  });
  expect(previewResponse.ok()).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number };
  expect(preview.canApply).toBeTruthy();
  expect(preview.errorCount).toBe(0);

  const anonymous = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: '' }
  });
  try {
    const response = await anonymous.post('/api/project-package/import/apply?mode=CreateAndUpdate', {
      headers: {
        'content-type': packageMediaType,
        'x-elitescada-workspace-version': String(workspace.changeVersion)
      },
      data: packageBytes
    });
    expect(response.status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }

  const operatorToken = createE2eJwt('e2e-operator', ['operator'], 'E2E Operator');
  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });
  try {
    const response = await operator.post('/api/project-package/import/apply?mode=CreateAndUpdate', {
      headers: {
        'content-type': packageMediaType,
        'x-elitescada-workspace-version': String(workspace.changeVersion)
      },
      data: packageBytes
    });
    expect(response.status()).toBe(403);
  } finally {
    await operator.dispose();
  }

  const staleVersion = workspace.changeVersion + 1000;
  const staleRestore = await request.post('/api/project-package/import/apply?mode=CreateAndUpdate', {
    headers: {
      'content-type': packageMediaType,
      'x-elitescada-workspace-version': String(staleVersion)
    },
    data: packageBytes
  });
  expect(staleRestore.status()).toBe(409);
  const conflict = await staleRestore.json() as {
    expectedChangeVersion: number;
    currentChangeVersion: number;
    error: string;
  };
  expect(conflict.expectedChangeVersion).toBe(staleVersion);
  expect(conflict.currentChangeVersion).toBe(workspace.changeVersion);
  expect(conflict.error).toContain('changed after preview');

  const workspaceAfterResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterResponse.ok()).toBeTruthy();
  const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
  expect(workspaceAfter.changeVersion).toBe(workspace.changeVersion);
});
