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
