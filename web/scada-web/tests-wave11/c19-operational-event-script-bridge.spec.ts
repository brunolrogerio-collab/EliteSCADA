import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Locator } from '@playwright/test';

const projectKey = 'e2e-wave11';
const eventKey = 'c19.runtime.initialize';
const eventName = 'C19 Runtime Initialize';
const eventMessage = 'C19 SCRIPT EVENT';
const eventSource = 'runtime.hmi';
const eventArea = 'Screen-Event-Area';
const scriptPath = 'scripts/c19-operational-event.py';

test('C19 new Operational Event transition installs a pristine draft atomically before new-mode identity', async () => {
  const source = await readFile(
    path.resolve(process.cwd(), 'src/engineering/OperationalEventEditor.tsx'),
    'utf8'
  );

  const chooseStart = source.indexOf('function choose(identity: string)');
  const patchStart = source.indexOf('function patch(', chooseStart);
  expect(chooseStart).toBeGreaterThanOrEqual(0);
  expect(patchStart).toBeGreaterThan(chooseStart);
  const chooseBlock = source.slice(chooseStart, patchStart);

  const freshDraft = chooseBlock.indexOf('setDraft(newOperationalEventDraft());');
  const identitySwap = chooseBlock.indexOf('setSelectedIdentity(identity);');
  expect(freshDraft).toBeGreaterThanOrEqual(0);
  expect(identitySwap).toBeGreaterThan(freshDraft);

  const newEffectStart = source.indexOf('if (selectedIdentity === NEW_IDENTITY)');
  const currentLookup = source.indexOf('const current = selectedIdentity', newEffectStart);
  expect(newEffectStart).toBeGreaterThanOrEqual(0);
  expect(currentLookup).toBeGreaterThan(newEffectStart);
  const newEffectBranch = source.slice(newEffectStart, currentLookup);
  expect(newEffectBranch).not.toContain('setDraft(newOperationalEventDraft());');
});

test('C19 authors an Operational Event normally and Server Script Initialize emits it through C14 into the C18 Event Browser', async ({ page, request }) => {
  await page.addInitScript(() => {
    localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
  });

  await page.goto('/engineering');
  const nav = page.locator('.eng-nav');
  await nav.getByRole('button', { name: /Eventos Operacionais/ }).click();

  const editor = page.getByTestId('operational-event-engineering');
  await expect(editor).toBeVisible();
  await expect(editor.getByRole('heading', { name: 'Eventos Operacionais' })).toBeVisible();

  const locale = page.locator('#engineering-locale');
  await locale.selectOption('en');
  await expect(editor.getByRole('heading', { name: 'Operational Events' })).toBeVisible();
  await locale.selectOption('es');
  await expect(editor.getByRole('heading', { name: 'Eventos Operacionales' })).toBeVisible();
  await locale.selectOption('pt-BR');
  await expect(editor.getByRole('heading', { name: 'Eventos Operacionais' })).toBeVisible();

  await editor.getByTestId('operational-event-new').click();
  await editor.getByLabel('Nome', { exact: true }).fill(eventName);
  await editor.getByLabel('Chave', { exact: true }).fill(eventKey);
  await editor.getByLabel('Tipo', { exact: true }).fill('state-change');
  await editor.getByLabel('Categoria', { exact: true }).fill('operation');
  await editor.getByLabel('Origem', { exact: true }).fill(eventSource);
  await editor.getByLabel('Área', { exact: true }).fill(eventArea);
  await editor.getByLabel('Mensagem padrão', { exact: true }).fill('C19 authored default');

  await editor.getByTestId('operational-event-preview').click();
  await expect(editor.getByText('Candidato de Engineering válido', { exact: true })).toBeVisible();
  await expect(editor.getByTestId('operational-event-apply')).toBeEnabled();
  await editor.getByTestId('operational-event-apply').click();

  const eventDefinition = await expect.poll(async () => {
    const working = await loadWorking(request);
    const definition = (working.operationalEvents ?? []).find((item: any) => item.key === eventKey);
    return definition?.id ? definition : null;
  }, { timeout: 15_000 }).not.toBeNull().then(async () => {
    const working = await loadWorking(request);
    return (working.operationalEvents ?? []).find((item: any) => item.key === eventKey) as any;
  });

  expect(eventDefinition.id).toBeTruthy();
  expect(eventDefinition.name).toBe(eventName);
  expect(eventDefinition.source).toBe(eventSource);
  expect(eventDefinition.area).toBe(eventArea);

  await installServerScript(request, eventDefinition.id);

  const workspaceBeforeSave = await loadWorkspace(request);
  expect(workspaceBeforeSave.isDirty).toBe(true);

  await nav.getByRole('button').first().click();
  const lifecycle = page.locator('.eng-lifecycle-workspace');
  await expect(lifecycle).toBeVisible();

  const actions = lifecycle.locator('.eng-lifecycle-workspace__action-buttons');
  const saveButton = actions.getByRole('button').first();
  await expect(saveButton).toBeEnabled();
  await saveButton.click();

  await expect.poll(async () => (await loadWorkspace(request)).isDirty).toBe(false);
  const savedWorkspace = await loadWorkspace(request);
  expect(savedWorkspace.baseRevision).toBeTruthy();
  const savedRevision = savedWorkspace.baseRevision!;

  const revisionRow = lifecycle.locator('.eng-lifecycle-workspace__revision-row').filter({ hasText: `r${savedRevision}` }).first();
  await expect(revisionRow).toBeVisible();
  const publishButton = revisionRow.locator('.eng-lifecycle-workspace__row-actions').getByRole('button').nth(1);
  await expect(publishButton).toBeEnabled();
  await publishButton.click();
  await confirmLifecycleAction(lifecycle);

  await expect.poll(async () => {
    const response = await request.get(`/api/engineering/persistence/${projectKey}/lifecycle`);
    if (!response.ok()) return null;
    return (await response.json() as { publishedRevision: number | null }).publishedRevision;
  }).toBe(savedRevision);

  const activateButton = actions.getByRole('button').nth(1);
  await expect(activateButton).toBeEnabled();
  await activateButton.click();
  await confirmLifecycleAction(lifecycle);

  await expect.poll(async () => {
    const response = await request.get('/api/runtime/application');
    if (!response.ok()) return null;
    return (await response.json() as { revision: number }).revision;
  }, { timeout: 15_000 }).toBe(savedRevision);

  // Runtime projection activation and Server Script Initialize are asynchronous. Wait
  // for the canonical host diagnostics to reach a terminal Initialize state before
  // classifying success/fault/timeout/cancellation. This preserves fail-closed
  // diagnostics without racing the executor immediately after revision activation.
  await expect.poll(async () => {
    const diagnostics = await loadRuntimeDiagnostics(request);
    const script = diagnostics.runtime?.serverScripts?.scripts?.find((item: any) => item.path === scriptPath);
    if (!script) return 0;
    const state = script.diagnostics;
    return state.completedCount + state.faultedCount + state.timeoutCount + state.cancelledCount;
  }, { timeout: 15_000, message: 'C19 Initialize did not reach a terminal runtime diagnostic state' }).toBeGreaterThan(0);

  const runtimeDiagnostics = await loadRuntimeDiagnostics(request);
  const c19Script = runtimeDiagnostics.runtime?.serverScripts?.scripts?.find((script: any) => script.path === scriptPath);
  const diagnosticContext = JSON.stringify(runtimeDiagnostics.runtime?.serverScripts ?? null);
  expect(c19Script, `C19 Server Script is absent from the Active runtime diagnostics: ${diagnosticContext}`).toBeTruthy();
  expect(c19Script.diagnostics.executionCount, `C19 Initialize never executed: ${diagnosticContext}`).toBeGreaterThan(0);
  expect(c19Script.diagnostics.faultedCount, `C19 Initialize faulted: ${diagnosticContext}`).toBe(0);
  expect(c19Script.diagnostics.timeoutCount, `C19 Initialize timed out: ${diagnosticContext}`).toBe(0);
  expect(c19Script.diagnostics.cancelledCount, `C19 Initialize was cancelled: ${diagnosticContext}`).toBe(0);
  expect(c19Script.diagnostics.completedCount, `C19 Initialize did not complete: ${diagnosticContext}`).toBeGreaterThan(0);

  await expect.poll(async () => await historicalEventExists(request), { timeout: 15_000 }).toBe(true);

  const working = await loadWorking(request);
  const overview = working.screens?.find((screen: any) => screen.key === 'demo.overview');
  const c18EventBrowser = flatten(overview?.elements ?? []).find((element: any) =>
    element.type === 'core.eventBrowser' &&
    element.properties?.browserConfig?.area === eventArea &&
    element.properties?.browserConfig?.source === eventSource
  );
  expect(c18EventBrowser?.id, 'C18 must leave a canonical Event Browser for C19 consumption').toBeTruthy();

  await page.goto('/');
  const browser = page.locator(`[data-runtime-object-id="${c18EventBrowser.id}"]`);
  await expect(browser).toBeVisible();
  await expect(browser).toHaveAttribute('data-browser-state', 'ready');
  await expect(browser).toContainText(eventMessage);
});

async function installServerScript(request: APIRequestContext, eventDefinitionId: string) {
  const before = await loadWorkspace(request);
  const working = await loadWorking(request);
  const scriptId = randomUUID();
  const script = {
    id: scriptId,
    path: scriptPath,
    name: 'C19 Operational Event bridge',
    scope: 'server',
    source: [
      'def on_initialize():',
      `    emit_operational_event("${eventDefinitionId}", "${eventMessage}", {"origin": "c19", "phase": "initialize"})`,
      ''
    ].join('\n'),
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [{
      eventKind: 'initialize',
      handlerName: 'on_initialize',
      targetReference: null,
      tagReference: null,
      timerIntervalMs: null
    }],
    dependencies: [],
    description: 'Generic C19 proof: canonical C14 Operational Event emission from Server Script.',
    metadata: { c19: 'true' }
  };

  const candidate = structuredClone(working);
  candidate.scripts = [...(candidate.scripts ?? []).filter((item: any) => item.path !== scriptPath), script];

  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
  expect(previewResponse.ok(), `C19 Script preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number };
  expect(preview.canApply).toBe(true);
  expect(preview.errorCount).toBe(0);

  const afterPreview = await loadWorkspace(request);
  expect(afterPreview.changeVersion).toBe(before.changeVersion);

  const applyResponse = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
    data: candidate
  });
  expect(applyResponse.ok(), `C19 Script Apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

  await expect.poll(async () => {
    const refreshed = await loadWorking(request);
    return refreshed.scripts?.some((item: any) => item.id === scriptId && item.path === scriptPath) ?? false;
  }).toBe(true);
}

async function loadRuntimeDiagnostics(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/diagnostics/runtime');
  expect(response.ok(), `Runtime diagnostics failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  return await response.json();
}

async function historicalEventExists(request: APIRequestContext): Promise<boolean> {
  const response = await request.post('/api/historical/query', {
    data: {
      version: 1,
      datasetKey: 'operational.events',
      timeRange: { kind: 'relative', durationSeconds: 300, anchor: 'now' },
      filters: [
        { field: 'source', operator: 'contains', values: [{ kind: 'string', value: eventSource }] },
        { field: 'area', operator: 'contains', values: [{ kind: 'string', value: eventArea }] }
      ],
      orderBy: [{ field: 'timestamp', direction: 'descending' }],
      page: { limit: 20 }
    }
  });
  if (!response.ok()) return false;
  const body = await response.json() as { rows?: Array<{ cells?: Record<string, { value?: string | null }> }> };
  return (body.rows ?? []).some(row =>
    Object.values(row.cells ?? {}).some(value => value?.value === eventMessage)
  );
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number; baseRevision: number | null; isDirty: boolean }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function confirmLifecycleAction(lifecycle: Locator) {
  const confirmation = lifecycle.locator('.eng-lifecycle-workspace__confirmation');
  await expect(confirmation).toBeVisible();
  await confirmation.locator('.eng-lifecycle-workspace__critical').click();
}

function flatten(elements: readonly any[]): any[] {
  const result: any[] = [];
  for (const element of elements) {
    result.push(element);
    result.push(...flatten(element.children ?? []));
  }
  return result;
}
