import { expect, test, type APIRequestContext } from '@playwright/test';
import {
  buildEeeFoundationPackage,
  EEE_IDS,
  EEE_PATHS,
  EEE_PROJECT_NAME
} from './c11-eee-demo-foundation';
import { EEE_SECURITY_ROLES } from './c11-eee-demo-security';

// The shared Wave11 host is deliberately bound to this CI project key. The
// Engineering package itself is project-key agnostic; final Preview persistence
// uses the canonical eee-demo key recorded by the C11 implementation contract.
const runtimeProjectKey = 'e2e-wave11';

test('C11 canonical EEE foundation lives through normal Engineering, Script, Alarm, Event, Historian and Command contracts', async ({ request }) => {
  const original = await loadWorking(request);

  try {
    const candidate = buildEeeFoundationPackage(original);
    candidate.securityRoles = structuredClone(EEE_SECURITY_ROLES);

    // Engineering owns the authored project graph. The Runtime HMI projection
    // intentionally does not expose Sources, TAGs, Events or Commands, so prove
    // those canonical entities here and exercise their Runtime authorities below.
    expect(candidate.dataSources).toEqual(expect.arrayContaining([
      expect.objectContaining({ id: EEE_IDS.source, key: 'eee.sim.server-memory', driver: 'builtin.memory.server' })
    ]));
    expect(candidate.tags).toHaveLength(Object.keys(EEE_IDS.tags).length);
    expect(candidate.operationalEvents).toHaveLength(Object.keys(EEE_IDS.events).length);
    expect(candidate.commands).toHaveLength(Object.keys(EEE_IDS.commands).length);

    await previewAndApply(request, candidate, 'C11 EEE foundation');
    const saved = await savePublishActivate(request, `${EEE_PROJECT_NAME} — Wave11 foundation harness`);

    const activeResponse = await request.get('/api/runtime/application');
    expect(activeResponse.ok(), `Active application failed: HTTP ${activeResponse.status()} ${await activeResponse.text()}`).toBeTruthy();
    const active = await activeResponse.json() as any;
    expect(active.projectKey).toBe(runtimeProjectKey);
    expect(active.revision).toBe(saved.revision);
    expect(active.package.scripts).toEqual(expect.arrayContaining([
      expect.objectContaining({ id: EEE_IDS.script, path: 'scripts/eee-process.py', scope: 'server', enabled: true })
    ]));

    await expect.poll(async () => {
      const script = await loadEeeScriptDiagnostics(request);
      return script?.diagnostics?.executionCount ?? 0;
    }, { timeout: 15_000 }).toBeGreaterThan(1);

    // With no pumps running, deterministic normal inflow must increase level.
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.levelPct)).value), { timeout: 10_000 })
      .toBeGreaterThan(45);

    // Boundary setup uses the normal protected TAG write API only. The Script is
    // still the sole authority deciding which pump starts and how process values evolve.
    await writeTag(request, EEE_IDS.tags.levelPct, 66);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value), { timeout: 10_000 })
      .toBe(true);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.p01FlowM3h)).value), { timeout: 10_000 })
      .toBeGreaterThan(30);

    await executeCommand(request, EEE_IDS.commands.highDemandEnable);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.highDemand)).value), { timeout: 10_000 })
      .toBe(true);
    await writeTag(request, EEE_IDS.tags.levelPct, 82);
    await expect.poll(async () => {
      const p01 = Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value);
      const p02 = Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value);
      return p01 && p02;
    }, { timeout: 10_000 }).toBe(true);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.totalFlowM3h)).value), { timeout: 10_000 })
      .toBeGreaterThanOrEqual(70);

    await executeCommand(request, EEE_IDS.commands.highDemandDisable);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.highDemand)).value), { timeout: 10_000 })
      .toBe(false);
    await writeTag(request, EEE_IDS.tags.levelPct, 34);
    await expect.poll(async () => {
      const p01 = Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value);
      const p02 = Boolean((await readCurrent(request, EEE_PATHS.p02Running)).value);
      return !p01 && !p02;
    }, { timeout: 10_000 }).toBe(true);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.cycleCount)).value), { timeout: 10_000 })
      .toBeGreaterThanOrEqual(1);
    await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.dutyPump)).value), { timeout: 10_000 })
      .toBe(2);

    // Manual operation still goes through canonical Commands -> request TAG -> Script.
    await executeCommand(request, EEE_IDS.commands.autoDisable);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.autoMode)).value), { timeout: 10_000 })
      .toBe(false);
    await executeCommand(request, EEE_IDS.commands.p01Start);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value), { timeout: 10_000 })
      .toBe(true);

    await executeCommand(request, EEE_IDS.commands.injectP01Fault);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Fault)).value), { timeout: 10_000 })
      .toBe(true);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Running)).value), { timeout: 10_000 })
      .toBe(false);
    await expect.poll(async () => await activeAlarmExists(request, EEE_IDS.alarms.p01Fault), { timeout: 10_000 })
      .toBe(true);

    await executeCommand(request, EEE_IDS.commands.resetFaults);
    await expect.poll(async () => Boolean((await readCurrent(request, EEE_PATHS.p01Fault)).value), { timeout: 10_000 })
      .toBe(false);

    // C13 canonical quality path: retain a meaningful value but publish Unavailable.
    await executeCommand(request, EEE_IDS.commands.badQualityEnable);
    await expect.poll(async () => String((await readCurrent(request, EEE_PATHS.p01PressureBar)).quality ?? '').toLowerCase(), { timeout: 10_000 })
      .toBe('unavailable');
    await executeCommand(request, EEE_IDS.commands.badQualityDisable);
    await expect.poll(async () => String((await readCurrent(request, EEE_PATHS.p01PressureBar)).quality ?? '').toLowerCase(), { timeout: 10_000 })
      .toBe('good');

    const history = await request.get(`/api/history/${EEE_IDS.tags.levelPct}?limit=50`);
    expect(history.ok(), `Historian query failed: HTTP ${history.status()} ${await history.text()}`).toBeTruthy();
    const historyRows = await history.json() as any[];
    expect(historyRows.length).toBeGreaterThan(0);

    await expect.poll(async () => await operationalEventExists(request, 'P01'), { timeout: 15_000 }).toBe(true);

    const script = await loadEeeScriptDiagnostics(request);
    const diagnosticContext = JSON.stringify(script?.diagnostics ?? null);
    expect(script, 'EEE Server Script missing from Active runtime diagnostics').toBeTruthy();
    expect(script.diagnostics.executionCount, diagnosticContext).toBeGreaterThan(1);
    expect(script.diagnostics.completedCount, diagnosticContext).toBeGreaterThan(1);
    expect(script.diagnostics.faultedCount, diagnosticContext).toBe(0);
    expect(script.diagnostics.timeoutCount, diagnosticContext).toBe(0);
    expect(script.diagnostics.cancelledCount, diagnosticContext).toBe(0);
  } finally {
    await previewAndApply(request, original, 'C11 cleanup');
    await savePublishActivate(request, 'Wave 11 E2E — restored after C11 foundation');
  }
});

async function previewAndApply(request: APIRequestContext, candidate: any, label: string) {
  const before = await loadWorkspace(request);
  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
  expect(previewResponse.ok(), `${label} preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
  expect(preview.canApply, `${label} preview issues: ${JSON.stringify(preview.items ?? [], null, 2)}`).toBe(true);
  expect(preview.errorCount).toBe(0);

  const afterPreview = await loadWorkspace(request);
  expect(afterPreview.changeVersion).toBe(before.changeVersion);

  const applyResponse = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
    data: candidate
  });
  expect(applyResponse.ok(), `${label} Apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();
}

async function savePublishActivate(request: APIRequestContext, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${runtimeProjectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save failed: HTTP ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };

  const publish = await request.post(`/api/engineering/persistence/${runtimeProjectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish failed: HTTP ${publish.status()} ${await publish.text()}`).toBeTruthy();

  const activate = await request.post(`/api/engineering/persistence/${runtimeProjectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate failed: HTTP ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadEeeScriptDiagnostics(request: APIRequestContext): Promise<any | null> {
  const response = await request.get('/api/diagnostics/runtime');
  expect(response.ok(), `Runtime diagnostics failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  const body = await response.json() as any;
  return body.runtime?.serverScripts?.scripts?.find((script: any) => script.path === 'scripts/eee-process.py') ?? null;
}

async function readCurrent(request: APIRequestContext, path: string): Promise<{ value?: unknown; quality?: unknown }> {
  const response = await request.get(`/api/tags/by-path/${path}`);
  expect(response.ok(), `TAG ${path} read failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  const body = await response.json() as { current?: { value?: unknown; quality?: unknown } | null };
  return body.current ?? {};
}

async function writeTag(request: APIRequestContext, tagId: string, value: unknown) {
  const response = await request.post(`/api/tags/${tagId}/write`, { data: { value } });
  expect(response.ok(), `TAG ${tagId} write failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
}

async function executeCommand(request: APIRequestContext, commandId: string) {
  const response = await request.post(`/api/commands/${commandId}/execute`);
  expect(response.ok(), `Command ${commandId} failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
}

async function activeAlarmExists(request: APIRequestContext, definitionId: string): Promise<boolean> {
  const response = await request.get('/api/alarms?activeOnly=true');
  if (!response.ok()) return false;
  const alarms = await response.json() as Array<{ definitionId?: string }>;
  return alarms.some(item => item.definitionId?.toLowerCase() === definitionId.toLowerCase());
}

async function operationalEventExists(request: APIRequestContext, marker: string): Promise<boolean> {
  const response = await request.post('/api/historical/query', {
    data: {
      version: 1,
      datasetKey: 'operational.events',
      timeRange: { kind: 'relative', durationSeconds: 300, anchor: 'now' },
      filters: [
        { field: 'source', operator: 'contains', values: [{ kind: 'string', value: 'server-script' }] },
        { field: 'area', operator: 'contains', values: [{ kind: 'string', value: 'EEE' }] }
      ],
      orderBy: [{ field: 'timestamp', direction: 'descending' }],
      page: { limit: 100 }
    }
  });
  if (!response.ok()) return false;
  const body = await response.json() as { rows?: Array<{ cells?: Record<string, { value?: string | null }> }> };
  return (body.rows ?? []).some(row =>
    Object.values(row.cells ?? {}).some(cell => cell?.value?.includes(marker))
  );
}
