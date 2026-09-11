import { expect, test } from '@playwright/test';

test('C16 working package carries an explicit persisted Startup Screen before lifecycle activation', async ({ request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;
  const overview = working.screens?.find((screen: any) => screen.key === 'demo.overview');
  expect(overview?.id).toBeTruthy();

  if (working.startupScreenId !== overview.id) {
    const applyResponse = await request.post('/api/engineering/import/json/apply', {
      data: {
        ...working,
        startupScreenId: overview.id
      }
    });
    expect(
      applyResponse.ok(),
      `Startup Screen bootstrap failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`
    ).toBeTruthy();
  }

  const persistedResponse = await request.get('/api/engineering/export/json');
  expect(persistedResponse.ok()).toBeTruthy();
  const persisted = await persistedResponse.json() as any;
  expect(persisted.startupScreenId).toBe(overview.id);
});
