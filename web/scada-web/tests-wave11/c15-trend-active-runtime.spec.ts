import { expect, test } from '@playwright/test';

const projectKey = 'e2e-wave11';
const runtimeSourceKey = 'memory.server.wave11.c15';
const screenTrendId = '00000000-0000-0000-0000-00000000c150';
const popupTrendId = '00000000-0000-0000-0000-00000000c151';
const historyTrendId = '00000000-0000-0000-0000-00000000c152';

function pen(id: string, tagId: string, tagPath: string, label: string, unit: string, color: string, axis: 'left' | 'right') {
  return {
    id,
    tagId,
    tagPath,
    label,
    visible: true,
    unit,
    color,
    lineWidth: 2,
    lineStyle: 'solid',
    axis,
    scale: { mode: 'auto' }
  };
}

test('C15 Trend survives Save Publish Activate in Screen and Popup and mounts live plus real history from Active Runtime', async ({ page, request }) => {
  const workingResponse = await request.get('/api/engineering/export/json');
  expect(workingResponse.ok()).toBeTruthy();
  const working = await workingResponse.json() as any;

  const screen = working.screens.find((candidate: any) => candidate.key === 'demo.overview');
  const popup = working.popups.find((candidate: any) => candidate.key === 'popup.pump.standard') ?? working.popups[0];
  expect(screen).toBeTruthy();
  expect(popup).toBeTruthy();

  const pressureTag = working.tags.find((tag: any) => tag.path === 'Demo.Discharge.Pressure');
  const frequencyTag = working.tags.find((tag: any) => tag.path === 'Demo.P01.Frequency');
  expect(pressureTag?.id).toBeTruthy();
  expect(frequencyTag?.id).toBeTruthy();
  expect(frequencyTag?.readOnly).toBeFalsy();

  const pens = [
    pen('c15-pressure', pressureTag.id, pressureTag.path, 'Pressure', 'bar', '#38BDF8', 'left'),
    pen('c15-frequency', frequencyTag.id, frequencyTag.path, 'Frequency', 'Hz', '#F59E0B', 'right')
  ];
  const historyPens = [
    pen('c15-frequency-history', frequencyTag.id, frequencyTag.path, 'Frequency history', 'Hz', '#22C55E', 'left')
  ];

  working.dataSources = [{
    key: runtimeSourceKey,
    name: 'Wave 11 C15 Server Memory',
    driver: 'builtin.memory.server',
    enabled: true
  }];
  working.tags = working.tags.map((tag: any) => ({
    ...tag,
    source: runtimeSourceKey,
    address: null,
    ...(tag.id === frequencyTag.id
      ? { historian: { enabled: true, strategy: 'change' } }
      : {})
  }));

  screen.elements = screen.elements.filter((element: any) =>
    ![screenTrendId, historyTrendId].includes(element.id)
    && !['c15-trend-active', 'c15-trend-history-active'].includes(element.key));
  screen.elements.push({
    id: screenTrendId,
    key: 'c15-trend-active',
    type: 'core.trend',
    properties: {
      x: 460,
      y: 320,
      width: 420,
      height: 180,
      zIndex: 151,
      visible: true,
      opacity: 1,
      trendMode: 'live',
      trendWindowSeconds: 3600,
      trendRefreshSeconds: 1,
      trendLegendVisible: true,
      trendGridVisible: true,
      trendAxesVisible: true,
      trendQualityVisible: true,
      pens
    }
  });
  screen.elements.push({
    id: historyTrendId,
    key: 'c15-trend-history-active',
    type: 'core.trend',
    properties: {
      x: 900,
      y: 320,
      width: 420,
      height: 180,
      zIndex: 152,
      visible: true,
      opacity: 1,
      trendMode: 'history',
      trendWindowSeconds: 3600,
      trendRefreshSeconds: 30,
      trendLegendVisible: true,
      trendGridVisible: true,
      trendAxesVisible: true,
      trendQualityVisible: true,
      pens: historyPens
    }
  });

  popup.elements = (popup.elements ?? []).filter((element: any) => element.id !== popupTrendId && element.key !== 'c15-popup-trend-active');
  popup.elements.push({
    id: popupTrendId,
    key: 'c15-popup-trend-active',
    type: 'core.trend',
    properties: {
      x: 20,
      y: 120,
      width: 480,
      height: 200,
      zIndex: 151,
      trendMode: 'history',
      trendWindowSeconds: 7200,
      trendRefreshSeconds: 10,
      trendLegendVisible: true,
      trendGridVisible: true,
      trendAxesVisible: true,
      trendQualityVisible: true,
      pens
    }
  });

  const applyResponse = await request.post('/api/engineering/import/json/apply', { data: working });
  expect(applyResponse.ok(), `C15 apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

  const saveResponse = await request.post(`/api/engineering/persistence/${projectKey}/save`, {
    data: { projectName: 'Wave 11 E2E' }
  });
  expect(saveResponse.ok(), `C15 save failed: HTTP ${saveResponse.status()} ${await saveResponse.text()}`).toBeTruthy();
  const saved = await saveResponse.json() as { revision: number };

  const publishResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`,
    { data: {} }
  );
  expect(publishResponse.ok(), `C15 publish failed: HTTP ${publishResponse.status()} ${await publishResponse.text()}`).toBeTruthy();

  const activateResponse = await request.post(
    `/api/engineering/persistence/${projectKey}/published/activate`,
    { data: {} }
  );
  expect(activateResponse.ok(), `C15 activate failed: HTTP ${activateResponse.status()} ${await activateResponse.text()}`).toBeTruthy();

  const activeResponse = await request.get('/api/runtime/application');
  expect(activeResponse.ok()).toBeTruthy();
  const active = await activeResponse.json() as any;
  expect(active.mode).toBe('engineering');
  expect(active.projectKey).toBe(projectKey);
  expect(active.revision).toBe(saved.revision);

  const activeScreen = active.package.screens.find((candidate: any) => candidate.key === 'demo.overview');
  const activeScreenTrend = activeScreen.elements.find((element: any) => element.id === screenTrendId);
  expect(activeScreenTrend?.type).toBe('core.trend');
  expect(activeScreenTrend?.properties).toMatchObject({
    x: 460,
    y: 320,
    width: 420,
    height: 180,
    zIndex: 151,
    trendMode: 'live'
  });
  expect(activeScreenTrend?.properties?.pens).toEqual(pens);
  expect(typeof activeScreenTrend?.properties?.pens).not.toBe('string');

  const activeHistoryTrend = activeScreen.elements.find((element: any) => element.id === historyTrendId);
  expect(activeHistoryTrend?.type).toBe('core.trend');
  expect(activeHistoryTrend?.properties).toMatchObject({
    x: 900,
    y: 320,
    width: 420,
    height: 180,
    zIndex: 152,
    trendMode: 'history'
  });
  expect(activeHistoryTrend?.properties?.pens).toEqual(historyPens);

  const activePopup = active.package.popups.find((candidate: any) => candidate.key === popup.key);
  const activePopupTrend = activePopup.elements.find((element: any) => element.id === popupTrendId);
  expect(activePopupTrend?.type).toBe('core.trend');
  expect(activePopupTrend?.properties).toMatchObject({
    x: 20,
    y: 120,
    width: 480,
    height: 200,
    zIndex: 151,
    trendMode: 'history'
  });
  expect(activePopupTrend?.properties?.pens).toEqual(pens);

  const activeHistorizedFrequency = active.package.tags.find((tag: any) => tag.id === frequencyTag.id);
  expect(activeHistorizedFrequency?.historian).toMatchObject({ enabled: true, strategy: 'change' });

  const runtimeTagsResponse = await request.get('/api/tags');
  expect(runtimeTagsResponse.ok()).toBeTruthy();
  const runtimeTags = await runtimeTagsResponse.json() as Array<{ id: string; path: string; readOnly: boolean }>;
  const activeFrequency = runtimeTags.find(tag => tag.path === frequencyTag.path);
  expect(activeFrequency).toBeTruthy();
  expect(activeFrequency!.id).toBe(frequencyTag.id);
  expect(activeFrequency!.readOnly).toBeFalsy();

  const historicalValue = 47.125;
  const writeResponse = await request.post(`/api/tags/${activeFrequency!.id}/write`, {
    data: { value: historicalValue }
  });
  expect(writeResponse.status(), `C15 historian seed write failed: HTTP ${writeResponse.status()} ${await writeResponse.text()}`).toBe(202);

  await expect.poll(async () => {
    const historyResponse = await request.post('/api/historical/query', {
      data: {
        version: 1,
        datasetKey: 'historian.samples',
        timeRange: { kind: 'relative', durationSeconds: 3600, anchor: 'now' },
        filters: [{
          field: 'tag.id',
          operator: 'in',
          values: [{ kind: 'guid', value: activeFrequency!.id }]
        }],
        orderBy: [{ field: 'timestamp', direction: 'ascending' }],
        page: { limit: 100 }
      }
    });
    if (!historyResponse.ok()) return false;
    const payload = await historyResponse.json() as any;
    return (payload.rows ?? []).some((row: any) => Number(row.cells?.value?.value) === historicalValue);
  }, { timeout: 15_000 }).toBe(true);

  await page.goto('/');
  const activeApplication = page.getByTestId('runtime-engineering-application');
  await expect(activeApplication).toBeVisible();
  await expect(activeApplication).toHaveAttribute('data-runtime-project-key', projectKey);
  await expect(activeApplication).toHaveAttribute('data-runtime-revision', String(saved.revision));

  const mountedLiveTrend = page.locator(`[data-testid="visual-trend"][data-object-id="${screenTrendId}"]`);
  await expect(mountedLiveTrend).toBeVisible();
  await expect(mountedLiveTrend).toHaveAttribute('data-trend-mode', 'live');
  await expect(mountedLiveTrend).toHaveAttribute('data-trend-source', 'runtime-tags');
  await expect(mountedLiveTrend).toHaveAttribute('data-trend-pen-count', '2');
  await expect(mountedLiveTrend.getByTestId('visual-trend-legend')).toContainText('Pressure');
  await expect(mountedLiveTrend.getByTestId('visual-trend-legend')).toContainText('Frequency');

  const mountedHistoryTrend = page.locator(`[data-testid="visual-trend"][data-object-id="${historyTrendId}"]`);
  await expect(mountedHistoryTrend).toBeVisible();
  await expect(mountedHistoryTrend).toHaveAttribute('data-trend-mode', 'history');
  await expect(mountedHistoryTrend).toHaveAttribute('data-trend-source', 'historian');
  await expect(mountedHistoryTrend).toHaveAttribute('data-trend-state', 'ready');
  await expect(mountedHistoryTrend).toHaveAttribute('data-trend-pen-count', '1');
  await expect(mountedHistoryTrend.getByTestId('visual-trend-legend')).toContainText('Frequency history');
  await expect(mountedHistoryTrend.getByTestId('visual-trend-series')).toHaveCount(1);
  await expect(mountedHistoryTrend.getByTestId('visual-trend-empty')).toHaveCount(0);
});
