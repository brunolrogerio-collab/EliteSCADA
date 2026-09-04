import { expect, test } from '@playwright/test';

test('C16 working package carries an explicit persisted Startup Screen before lifecycle activation', async ({ request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;
  const overview = working.screens?.find((screen: any) => screen.key === 'demo.overview');
  expect(overview?.id).toBeTruthy();

  // The historical Wave 11 lifecycle fixture replaces all Data Sources with one
  // legacy Server Memory source that intentionally has no stable Id. Remove any
  // stale DataSourceId inherited from the current Demo package before that later
  // replacement so the fixture uses the supported legacy Source-key association
  // coherently instead of presenting a mismatched stable identity to the compiler.
  const normalizedTags = (working.tags ?? []).map((tag: any) => ({
    ...tag,
    dataSourceId: null
  }));
  const needsLegacyFixtureNormalization = (working.tags ?? [])
    .some((tag: any) => tag.dataSourceId != null);

  if (working.startupScreenId !== overview.id || needsLegacyFixtureNormalization) {
    const applyResponse = await request.post('/api/engineering/import/json/apply', {
      data: {
        ...working,
        startupScreenId: overview.id,
        tags: normalizedTags
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
  expect((persisted.tags ?? []).every((tag: any) => tag.dataSourceId == null)).toBeTruthy();
});
