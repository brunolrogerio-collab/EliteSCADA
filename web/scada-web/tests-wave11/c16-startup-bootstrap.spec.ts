import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

test('C16 working package carries an explicit persisted Startup Screen before lifecycle activation', async ({ request }) => {
  const firstProject = await request.post('/api/engineering/persistence/projects/first', {
    data: { projectKey: 'e2e-wave11', projectName: 'Wave 11 E2E' }
  });
  expect(firstProject.ok(), await firstProject.text()).toBeTruthy();

  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json();
  const fixture = JSON.parse(await readFile(
    new URL('./demo-workspace.json', import.meta.url), 'utf8'
  ));
  const overview = fixture.screens.find((screen: { key: string }) => screen.key === 'demo.overview');
  expect(overview?.id).toBeTruthy();

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json();
  const candidate = {
    ...fixture,
    authorityPolicyReference: working.authorityPolicyReference,
    startupScreenId: overview.id
  };
  const preview = await request.post('/api/engineering/import/json/preview', { data: candidate });
  const previewBody = await preview.text();
  expect(preview.ok(), previewBody).toBeTruthy();
  expect(JSON.parse(previewBody).canApply, previewBody).toBe(true);

  const applied = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(workspace.changeVersion) },
    data: candidate
  });
  expect(applied.ok(), await applied.text()).toBeTruthy();

  const persistedResponse = await request.get('/api/engineering/export/json');
  expect(persistedResponse.ok()).toBeTruthy();
  const persisted = await persistedResponse.json() as any;
  expect(persisted.startupScreenId).toBe(overview.id);
});
