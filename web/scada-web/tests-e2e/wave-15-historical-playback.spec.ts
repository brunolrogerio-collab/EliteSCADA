import { expect, test, type Page, type Route } from '@playwright/test';
import { readFile } from 'node:fs/promises';

const ANALOG_ID = '11111111-1111-4111-8111-111111111111';
const DIGITAL_ID = '22222222-2222-4222-8222-222222222222';
const GAP_ID = '33333333-3333-4333-8333-333333333333';
const COMMAND_ID = '44444444-4444-4444-8444-444444444444';
const SCREEN_HOME_ID = '55555555-5555-4555-8555-555555555555';
const SCREEN_SECONDARY_ID = '66666666-6666-4666-8666-666666666666';
const POPUP_ID = '77777777-7777-4777-8777-777777777777';
const DYNAMO_ID = '88888888-8888-4888-8888-888888888888';
const DYNAMO_INSTANCE_ID = '99999999-9999-4999-8999-999999999999';
const DYNAMO_CHILD_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const tagCatalog = [
  {
    id: ANALOG_ID, name: 'Analog', path: 'Plant.Analog', dataType: 'Double',
    engineeringUnit: 'bar', readOnly: false,
    current: { tagId: ANALOG_ID, value: 999, timestamp: '2026-10-02T14:00:00Z', quality: 'Good' }
  },
  {
    id: DIGITAL_ID, name: 'Digital', path: 'Plant.Digital', dataType: 'Boolean',
    readOnly: false,
    current: { tagId: DIGITAL_ID, value: true, timestamp: '2026-10-02T14:00:00Z', quality: 'Good' }
  },
  {
    id: GAP_ID, name: 'Gap', path: 'Plant.Gap', dataType: 'Double',
    readOnly: false,
    current: { tagId: GAP_ID, value: 777, timestamp: '2026-10-02T14:00:00Z', quality: 'Good' }
  }
];

const valueDisplay = (
  id: string,
  key: string,
  tagId: string,
  path: string,
  dataType: string,
  x: number,
  y: number
) => ({
  id, key, type: 'core.valueDisplay',
  properties: { x, y, width: 220, height: 58, text: '—', visible: true },
  bindings: [{
    key: 'text', kind: 'tag', target: path, direction: 'read',
    metadata: { sourceDataType: dataType, decimalPlaces: dataType === 'Double' ? '1' : undefined },
    tagReference: { tagId }
  }]
});

const button = (
  id: string,
  key: string,
  text: string,
  x: number,
  y: number,
  action: Record<string, unknown>
) => ({
  id, key, type: 'core.button',
  properties: { x, y, width: 220, height: 52, text, visible: true },
  actions: [{ eventKey: 'click', version: 1, ...action }]
});

const runtimeProjection = {
  mode: 'engineering',
  projectKey: 'w15-historical-playback',
  projectName: 'W15 Historical Playback',
  revision: 445,
  activatedAtUtc: '2026-10-02T14:20:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 16,
    exportedAt: '2026-10-02T14:20:00Z',
    startupScreenId: SCREEN_HOME_ID,
    tags: tagCatalog.map(({ current, ...tag }) => tag),
    alarms: [],
    commands: [{ id: COMMAND_ID, key: 'cmd.test', name: 'Test', kind: 'WriteValue', value: '1', enabled: true }],
    screens: [
      {
        id: SCREEN_HOME_ID,
        key: 'home',
        name: 'Home',
        elements: [
          valueDisplay('10000000-0000-4000-8000-000000000001', 'analog-home', ANALOG_ID, 'Plant.Analog', 'Double', 40, 40),
          valueDisplay('10000000-0000-4000-8000-000000000002', 'digital-home', DIGITAL_ID, 'Plant.Digital', 'Boolean', 40, 110),
          valueDisplay('10000000-0000-4000-8000-000000000003', 'gap-home', GAP_ID, 'Plant.Gap', 'Double', 40, 180),
          button('10000000-0000-4000-8000-000000000004', 'open-popup', 'Abrir popup', 300, 40, {
            kind: 'OpenPopup', targetKey: 'details'
          }),
          button('10000000-0000-4000-8000-000000000005', 'navigate-secondary', 'Ir secundária', 300, 110, {
            kind: 'NavigateScreen', targetKey: 'secondary'
          }),
          button('10000000-0000-4000-8000-000000000006', 'write-tag', 'Escrever TAG', 300, 180, {
            kind: 'SetTagValue', targetKey: ANALOG_ID, parameters: { value: 42 }
          }),
          button('10000000-0000-4000-8000-000000000007', 'execute-command', 'Executar comando', 300, 250, {
            kind: 'ExecuteCommand', targetKey: null, commandId: COMMAND_ID, parameters: null
          }),
          {
            id: DYNAMO_INSTANCE_ID,
            key: 'history-dynamo',
            type: 'dynamo',
            dynamoKey: 'history.value',
            dynamoDefinitionId: DYNAMO_ID,
            properties: { x: 600, y: 40, width: 260, height: 100 }
          }
        ]
      },
      {
        id: SCREEN_SECONDARY_ID,
        key: 'secondary',
        name: 'Secondary',
        elements: [
          valueDisplay('20000000-0000-4000-8000-000000000001', 'analog-secondary', ANALOG_ID, 'Plant.Analog', 'Double', 40, 40)
        ]
      }
    ],
    popups: [{
      id: POPUP_ID,
      key: 'details',
      name: 'Details',
      x: 900,
      y: 160,
      properties: { width: '340', height: '180' },
      elements: [
        valueDisplay('30000000-0000-4000-8000-000000000001', 'digital-popup', DIGITAL_ID, 'Plant.Digital', 'Boolean', 20, 20)
      ]
    }],
    dynamos: [{
      id: DYNAMO_ID,
      key: 'history.value',
      name: 'Historical value',
      elements: [
        valueDisplay(DYNAMO_CHILD_ID, 'analog-dynamo', ANALOG_ID, 'Plant.Analog', 'Double', 0, 0)
      ]
    }],
    templates: [],
    equipment: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: []
  }
} as const;

async function installShell(page: Page) {
  await page.routeWebSocket('**/ws/tags', () => {
    // Keep the canonical Live socket open. Historical assertions are driven by
    // deterministic HTTP fixtures; an unrelated socket close must not mark
    // otherwise valid Live samples as Disconnected.
  });
  await page.route('**/api/auth/config', route => route.fulfill({
    json: {
      authenticationEnabled: true, localLoginEnabled: true,
      initialAdministratorRequired: false, initialAdministratorSetupAvailable: false,
      initialAdministratorBlockedReason: null,
      passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
    }
  }));
  await page.route('**/api/auth/me', route => route.fulfill({
    json: { subjectId: 'w15-user', username: 'w15', displayName: 'W15', roles: ['developer'], identityProvider: 'local' }
  }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 'w15' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: true,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringView', 'EngineeringModify', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.route('**/api/runtime/application', route => route.fulfill({ json: runtimeProjection }));
}

function historicalRow(
  id: string,
  path: string,
  value: { kind: string; value: string | null },
  quality: string | null,
  timestamp: string,
  provenance: { kind: string; reason?: string }
) {
  return {
    data: {
      cells: {
        'tag.id': { kind: 'guid', value: id },
        'tag.path': { kind: 'string', value: path },
        quality: quality === null ? { kind: 'null', value: null } : { kind: 'enum', value: quality },
        value,
        timestamp: { kind: 'dateTime', value: timestamp }
      }
    },
    provenance: {
      kind: provenance.kind,
      sourceTimestampsUtc: provenance.kind === 'gap' ? [] : [timestamp],
      reason: provenance.reason ?? null
    }
  };
}

async function fulfillHistorical(route: Route, targetIndex: Map<string, number>) {
  const payload = route.request().postDataJSON() as any;
  const definition = payload.definition;
  const target = String(definition.historianRetrieval.targetUtc);
  if (!targetIndex.has(target)) targetIndex.set(target, targetIndex.size);
  const instantIndex = targetIndex.get(target) ?? 0;
  const mode = String(definition.historianRetrieval.mode);
  const ids = (definition.query.filters?.[0]?.values ?? []).map((value: any) => String(value.value));
  const rows: any[] = [];

  if (mode === 'interpolated' && ids.includes(ANALOG_ID)) {
    rows.push(historicalRow(
      ANALOG_ID,
      'Plant.Analog',
      { kind: 'double', value: instantIndex === 0 ? '12.5' : '33.5' },
      'Good',
      target,
      { kind: 'interpolated' }
    ));
  }
  if (mode === 'atOrBefore' && ids.includes(DIGITAL_ID)) {
    rows.push(historicalRow(
      DIGITAL_ID,
      'Plant.Digital',
      { kind: 'boolean', value: 'false' },
      'Good',
      '2026-10-02T13:40:00Z',
      { kind: 'measured' }
    ));
  }
  if (ids.includes(GAP_ID)) {
    rows.push(historicalRow(
      GAP_ID,
      'Plant.Gap',
      { kind: 'null', value: null },
      null,
      target,
      { kind: 'gap', reason: 'no-observation-at-or-before-target' }
    ));
  }

  await route.fulfill({
    json: {
      version: 1,
      queryId: definition.id,
      queryKey: definition.key,
      datasetKey: 'historian.samples',
      retrievalMode: mode,
      columns: [],
      rows,
      fromUtc: definition.query.timeRange.fromUtc,
      toUtc: definition.query.timeRange.toUtc,
      nextCursor: null,
      resultLimit: Math.max(1, ids.length)
    }
  });
}

test('W15 Historical Playback projects past state read-only across Screen Popup Dynamo and returns cleanly to Live', async ({ page }) => {
  test.setTimeout(45_000);
  await installShell(page);

  let liveAnalog = 999;
  let tagWriteRequests = 0;
  let commandRequests = 0;
  let engineeringMutations = 0;
  const targetIndex = new Map<string, number>();

  await page.route('**/api/tags', route => route.fulfill({
    json: tagCatalog.map(tag => tag.id === ANALOG_ID
      ? { ...tag, current: { ...tag.current, value: liveAnalog } }
      : tag)
  }));
  await page.route('**/api/historical/data-query', route => fulfillHistorical(route, targetIndex));
  await page.route('**/api/tags/*/write', route => {
    tagWriteRequests++;
    return route.fulfill({ status: 204 });
  });
  await page.route('**/api/commands/*/execute', route => {
    commandRequests++;
    return route.fulfill({ status: 204 });
  });
  await page.route('**/api/engineering/**', async route => {
    if (route.request().method() !== 'GET') engineeringMutations++;
    await route.fallback();
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  const navigator = page.getByTestId('runtime-visual-navigator');
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'live');
  await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000001"]')).toContainText(/999/);

  await page.getByTestId('runtime-enter-historical-playback').click();
  const panel = page.getByTestId('runtime-historical-playback-panel');
  await expect(panel).toBeVisible();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'historical-playback');
  await expect(panel.locator('[data-playback-load-state="ready"]')).toBeVisible();

  const firstAt = await runtime.getAttribute('data-runtime-historical-at');
  expect(firstAt).toBeTruthy();

  await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000001"]')).toContainText(/12[,.]5/);
  await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000002"]')).toContainText(/Falso|False|Falso/);
  await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000003"]')).toContainText('—');
  await expect(panel.locator('[data-playback-gap-count]')).toHaveAttribute('data-playback-gap-count', /[1-9]\d*/);

  const dynamo = page.locator(`[data-object-id="${DYNAMO_INSTANCE_ID}"]`);
  await expect(dynamo).toContainText(/12[,.]5/);
  await expect(dynamo).toHaveAttribute('data-dynamic-state', 'available');

  // This fixture intentionally has no public Dynamo state parameter. The
  // rendered historical value is Good/available, while the semantic state
  // overlay must remain fail-closed because it cannot infer a public state
  // sample. Preserve that truthful boundary instead of hiding the indicator.
  const dynamoIndicator = dynamo.getByTestId('runtime-dynamo-state-indicator');
  await expect(dynamoIndicator).toHaveAttribute('data-dynamo-quality', 'unknown');
  await expect(dynamoIndicator).toHaveAttribute('data-dynamo-state', 'bad-quality');
  await expect(dynamoIndicator).toContainText('BAD QUALITY');

  await page.getByRole('button', { name: 'Abrir popup' }).click();
  const popup = navigator.locator('[data-popup-key="details"]');
  await expect(popup).toBeVisible();
  await expect(popup).toContainText(/Falso|False/);
  await expect(runtime).toHaveAttribute('data-runtime-historical-at', firstAt!);

  await page.getByRole('button', { name: 'Escrever TAG' }).click();
  await expect(page.getByTestId('runtime-visual-diagnostic')).toHaveAttribute('data-diagnostic-code', 'HISTORICAL_PLAYBACK_READ_ONLY');
  expect(tagWriteRequests).toBe(0);

  await page.getByRole('button', { name: 'Executar comando' }).click();
  await expect(page.getByTestId('runtime-visual-diagnostic')).toHaveAttribute('data-diagnostic-code', 'HISTORICAL_PLAYBACK_READ_ONLY');
  expect(commandRequests).toBe(0);

  await page.getByRole('button', { name: 'Ir secundária' }).click();
  await expect(navigator).toHaveAttribute('data-active-screen-key', 'secondary');
  await expect(runtime).toHaveAttribute('data-runtime-historical-at', firstAt!);
  await expect(page.locator('[data-object-id="20000000-0000-4000-8000-000000000001"]')).toContainText(/12[,.]5/);
  await expect(popup).toBeVisible();

  const instant = panel.getByRole('slider', { name: /Instante histórico|Historical instant|Instante histórico/ });
  await instant.fill('750');
  await expect.poll(async () => runtime.getAttribute('data-runtime-historical-at')).not.toBe(firstAt);
  await expect(panel.locator('[data-playback-load-state="ready"]')).toBeVisible();
  await expect(page.locator('[data-object-id="20000000-0000-4000-8000-000000000001"]')).toContainText(/33[,.]5/);
  expect(targetIndex.size).toBeGreaterThanOrEqual(2);

  liveAnalog = 1001;
  await expect(page.locator('[data-object-id="20000000-0000-4000-8000-000000000001"]')).toContainText(/33[,.]5/);

  await page.getByTestId('runtime-exit-historical-playback').click();
  await expect(runtime).toHaveAttribute('data-runtime-temporal-mode', 'live');
  await expect(runtime).not.toHaveAttribute('data-runtime-historical-at');
  await expect(page.locator('[data-object-id="20000000-0000-4000-8000-000000000001"]')).toContainText(/1001/, { timeout: 6_000 });

  liveAnalog = 1002;
  await expect(page.locator('[data-object-id="20000000-0000-4000-8000-000000000001"]')).toContainText(/1002/, { timeout: 6_000 });

  expect(engineeringMutations).toBe(0);
  expect(tagWriteRequests).toBe(0);
  expect(commandRequests).toBe(0);
});

test('Playback source collection includes Screen Popup Dynamo parameters and does not mutate Engineering', async () => {
  const { collectHistoricalPlaybackRequests } = await import('../src/runtime/historical-playback/HistoricalPlaybackContext');
  const project = structuredClone(runtimeProjection.package) as any;
  project.screens[0].elements.push({
    id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
    key: 'parameterized-dynamo',
    type: 'dynamo',
    dynamoKey: 'history.value',
    dynamoDefinitionId: DYNAMO_ID,
    dynamoParameters: [{
      key: 'source',
      kind: 'TagReference',
      tagReference: { tagId: DIGITAL_ID },
      version: 1
    }]
  });
  const before = JSON.stringify(project);
  const requests = collectHistoricalPlaybackRequests(project);
  const ids = requests.map(request => request.tagReference?.tagId).filter(Boolean);

  expect(ids).toContain(ANALOG_ID);
  expect(ids).toContain(DIGITAL_ID);
  expect(ids).toContain(GAP_ID);
  expect(JSON.stringify(project)).toBe(before);
});

test('Playback mutation guard blocks TAG write and command before any Runtime lease/fetch', async () => {
  const guard = await import('../src/runtime/historical-playback/runtimeHistoricalPlaybackGuard');
  const { writeRuntimeTagValue } = await import('../src/runtime/runtimeTagWriteApi');
  const { executeRuntimeCommand } = await import('../src/runtime/visual-navigation/runtimeCommandApi');
  let calls = 0;
  const fetcher = async () => {
    calls++;
    return new Response(null, { status: 204 });
  };

  guard.setRuntimeHistoricalPlaybackActive(true);
  try {
    await expect(writeRuntimeTagValue(ANALOG_ID, 12, fetcher)).rejects.toThrow(/Historical Playback/);
    await expect(executeRuntimeCommand(COMMAND_ID, fetcher)).rejects.toThrow(/Historical Playback/);
    expect(calls).toBe(0);
  } finally {
    guard.setRuntimeHistoricalPlaybackActive(false);
  }
});


test('Playback reuses the canonical renderer and suppresses Client Visual Script interaction dispatch', async () => {
  const renderer = await readFile(new URL('../src/runtime/visual-navigation/RuntimeVisualDefinitionRenderer.tsx', import.meta.url), 'utf8');
  const canonical = await readFile(new URL('../src/engineering/visual-editor/CanonicalVisualRenderer.tsx', import.meta.url), 'utf8');
  const guard = await readFile(new URL('../src/runtime/historical-playback/runtimeHistoricalPlaybackGuard.ts', import.meta.url), 'utf8');

  expect(renderer).toContain('if (playbackActive) return;');
  expect(renderer).toContain('tagWriter: playbackActive ? null : undefined');
  expect(renderer).toContain('bindingSamples={playbackActive ? playback?.samples');
  expect(canonical).toContain('const resolvedSamples = bindingSamples ?? liveSamples;');
  expect(guard).toContain('assertRuntimeProcessMutationAllowed');
});
