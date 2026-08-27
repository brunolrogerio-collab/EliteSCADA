import { expect, test } from '@playwright/test';

const packageMediaType = 'application/vnd.elitescada.project-package';

test('project package restore rejects an invalid Workspace version header', async ({ request }) => {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as {
    projectKey?: string | null;
    projectName?: string | null;
  };
  expect(workspace.projectKey).toBeTruthy();
  expect(workspace.projectName).toBeTruthy();

  const query = new URLSearchParams({
    projectKey: workspace.projectKey!,
    projectName: workspace.projectName!
  });
  const exported = await request.get(`/api/project-package/export?${query.toString()}`);
  expect(exported.ok()).toBeTruthy();
  const packageBytes = await exported.body();

  const invalid = await request.post('/api/project-package/import/apply?mode=CreateAndUpdate', {
    headers: {
      'content-type': packageMediaType,
      'x-elitescada-workspace-version': 'not-a-version'
    },
    data: packageBytes
  });
  expect(invalid.status()).toBe(400);
  expect((await invalid.json() as { error: string }).error).toContain('Invalid Engineering Workspace version header');
});
