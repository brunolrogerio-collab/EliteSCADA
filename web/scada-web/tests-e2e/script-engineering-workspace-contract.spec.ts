import { readFile } from 'node:fs/promises';
import { expect, request as playwrightRequest, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';
import {
  buildCanonicalScriptPackage,
  canonicalScriptPackageFingerprint,
  normalizeScriptDefinition,
  normalizeVisualEventReference,
  previewTokenMatches,
  scriptMutationMode
} from '../src/engineering/scripts/ScriptEngineeringWorkspace.logic';
import { packageContainsOnlyScriptMutation } from '../src/engineering/scripts/scriptEngineeringApi';
import { buildScriptAssistantVisualValueSnippet } from '../src/engineering/scripts/scriptAssistantModel';
import type {
  AuthorityPolicyReferenceEngineering,
  CanonicalScriptPackage,
  ScriptEngineeringDefinition,
  ScriptImportPreview,
  ScriptMutationPreviewToken
} from '../src/engineering/scripts/scriptEngineeringTypes';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';
const jsonHeaders = { 'content-type': 'application/json; charset=utf-8' };

test.describe.configure({ mode: 'serial' });

test('Script workspace consumes Engineering semantic theme tokens without independent light/dark fallbacks', async () => {
  const css = await readFile(new URL('../src/engineering/scripts/script-engineering-workspace.css', import.meta.url), 'utf8');

  expect(css).toContain('--script-surface: var(--eng-panel, #121922)');
  expect(css).toContain('--script-border: var(--eng-border, #283544)');
  expect(css).toContain('--script-text: var(--eng-text, #e8edf3)');
  expect(css).toContain('--script-control-surface: var(--eng-input-bg, #0d141a)');
  expect(css).toContain('--script-hover: var(--eng-hover, #17232d)');
  expect(css).toContain('--script-focus: var(--eng-focus, #8bd3ff)');
  expect(css).toContain('background: var(--script-control-surface)');
  expect(css).toContain('color: var(--script-text)');
  expect(css).not.toContain('var(--surface, #fff)');
  expect(css).not.toContain('var(--border, #d6dae2)');
  expect(css).not.toContain('var(--border, #c9ced8)');
});

test('wire enums normalize and minimal package preserves owned visual references only', () => {
  const id = '11111111-1111-4111-8111-111111111111';
  const otherId = '22222222-2222-4222-8222-222222222222';
  const raw = {
    id,
    path: 'scripts/pump.py',
    name: 'Pump',
    scope: 0,
    source: 'pass\n',
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [{ eventKind: 2, handlerName: 'on_click', targetReference: 'pump' }],
    dependencies: [{ kind: 4, stableReference: otherId }],
    description: 'test',
    metadata: { owner: 'engineering' }
  };
  const script = normalizeScriptDefinition(raw);
  expect(script.scope).toBe('clientVisual');
  expect(script.entryPoints[0]?.eventKind).toBe('objectInteraction');
  expect(script.dependencies[0]?.kind).toBe('clientMemoryTag');

  const references = [
    normalizeVisualEventReference({ visualDefinitionId: otherId, visualObjectId: null, eventKind: 0, scriptId: id, entryPoint: 'initialize' }),
    normalizeVisualEventReference({ visualDefinitionId: id, visualObjectId: null, eventKind: 0, scriptId: otherId, entryPoint: 'initialize' })
  ];
  const packageData = buildCanonicalScriptPackage(script, references, '2026-08-28T00:00:00.000Z');
  expect(packageContainsOnlyScriptMutation(packageData)).toBeTruthy();
  expect(packageData.scripts).toHaveLength(1);
  expect(packageData.scripts[0]?.scope).toBe('clientVisual');
  expect(packageData.scriptVisualEventReferences).toHaveLength(1);
  expect(packageData.scriptVisualEventReferences[0]?.scriptId).toBe(id);
});

test('stable visual property reference survives canonical Script save/reopen package round-trip', () => {
  const script = makeScript('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'scripts/stable-visual.py');
  script.source = [
    'from elite_scada import visual_property_read, visual_property_write',
    'ref = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/cccccccc-cccc-4ccc-8ccc-cccccccccccc"',
    'value = await visual_property_read(ref, "visible")',
    'await visual_property_write(ref, "visible", False)'
  ].join('\n');
  const packageData = buildCanonicalScriptPackage(script, [], '2026-09-28T00:00:00.000Z');
  const reopened = normalizeScriptDefinition(packageData.scripts[0] as unknown as Record<string, unknown>);
  expect(reopened.source).toBe(script.source);
  expect(reopened.source).toContain('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/cccccccc-cccc-4ccc-8ccc-cccccccccccc');
});

test('Preview token is bound to exact Script package and mutation mode', () => {
  const script = makeScript(crypto.randomUUID(), `scripts/token-${Date.now()}.py`);
  const packageData = buildCanonicalScriptPackage(script, [], '2026-08-28T00:00:00.000Z');
  const token: ScriptMutationPreviewToken = {
    package: packageData,
    packageFingerprint: canonicalScriptPackageFingerprint(packageData),
    mode: 'CreateOnly',
    expectedChangeVersion: 12,
    preview: previewResult(true, 1, 0)
  };
  expect(previewTokenMatches(token, buildCanonicalScriptPackage(script, [], '2027-01-01T00:00:00.000Z'), 'CreateOnly')).toBeTruthy();
  expect(previewTokenMatches(token, buildCanonicalScriptPackage({ ...script, source: 'x = 2\n' }, []), 'CreateOnly')).toBeFalsy();
  expect(previewTokenMatches(token, packageData, 'UpdateExisting')).toBeFalsy();
  expect(scriptMutationMode(script, [])).toBe('CreateOnly');
  expect(scriptMutationMode(script, [script])).toBe('UpdateExisting');
});

test('Script create/update uses Preview and rejects stale Workspace CAS without mutating the update', async ({ request }) => {
  const id = crypto.randomUUID();
  const path = `scripts/e2e-${id}.py`;
  const created = makeScript(id, path);
  let exists = false;

  try {
    const before = await workspace(request);
    const createPackage = buildCanonicalScriptPackage(created, [], undefined, before.authorityPolicyReference);
    const createPreview = await preview(request, createPackage, 'CreateOnly');
    expect(createPreview.canApply).toBeTruthy();
    expect(createPreview.createCount).toBe(1);

    const createResponse = await apply(request, createPackage, 'CreateOnly', before.changeVersion);
    expect(createResponse.ok()).toBeTruthy();
    exists = true;

    const afterCreate = await workspace(request);
    expect(afterCreate.changeVersion).toBeGreaterThan(before.changeVersion);

    const updated = { ...created, description: 'updated through canonical Preview/Apply' };
    const updatePackage = buildCanonicalScriptPackage(updated, [], undefined, afterCreate.authorityPolicyReference);
    const updatePreview = await preview(request, updatePackage, 'UpdateExisting');
    expect(updatePreview.canApply).toBeTruthy();
    expect(updatePreview.updateCount).toBe(1);

    const stale = await apply(request, updatePackage, 'UpdateExisting', before.changeVersion);
    expect(stale.status()).toBe(409);

    const listedBeforeValidUpdate = await request.get('/api/engineering/scripts');
    expect(listedBeforeValidUpdate.ok()).toBeTruthy();
    const rawScripts = await listedBeforeValidUpdate.json() as Array<Record<string, unknown>>;
    const persistedBeforeValidUpdate = rawScripts.map(normalizeScriptDefinition).find(script => script.id === id);
    expect(persistedBeforeValidUpdate?.description ?? null).not.toBe(updated.description);

    const validUpdate = await apply(request, updatePackage, 'UpdateExisting', afterCreate.changeVersion);
    expect(validUpdate.ok()).toBeTruthy();
  } finally {
    if (exists) await bestEffortDelete(request, id);
  }
});


test('Server Script syntax validation uses isolated real parser without mutating Working', async ({ request }) => {
  const before = await workspace(request);

  const valid = await request.post('/api/engineering/scripts/python/validate', {
    data: { source: 'def initialize():\n    return None\n' }
  });
  expect(valid.ok()).toBeTruthy();
  expect(await valid.json()).toEqual({ diagnostics: [] });

  const invalid = await request.post('/api/engineering/scripts/python/validate', {
    data: { source: 'def broken(:\n    return None\n' }
  });
  expect(invalid.ok()).toBeTruthy();
  const payload = await invalid.json() as {
    diagnostics: Array<{ severity: string; code: string; line: number; column: number }>;
  };
  expect(payload.diagnostics).toEqual([
    expect.objectContaining({
      severity: 'error',
      code: 'PY_SYNTAX',
      line: 1,
      column: expect.any(Number)
    })
  ]);

  const after = await workspace(request);
  expect(after.changeVersion).toBe(before.changeVersion);
});

test('Script mutation preserves backend authorization boundary', async ({ request }) => {
  const script = makeScript(crypto.randomUUID(), `scripts/auth-${Date.now()}.py`);
  const current = await workspace(request);
  const packageData = buildCanonicalScriptPackage(script, [], undefined, current.authorityPolicyReference);

  const anonymous = await playwrightRequest.newContext({ baseURL, extraHTTPHeaders: { Authorization: '' } });
  try {
    const response = await anonymous.post('/api/engineering/import/json/apply?mode=CreateOnly', {
      headers: { ...jsonHeaders, 'x-elitescada-workspace-version': String(current.changeVersion) },
      data: JSON.stringify(packageData)
    });
    expect(response.status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }

  const operatorToken = createE2eJwt('script-e2e-operator', ['operator'], 'Script E2E Operator');
  const operator = await playwrightRequest.newContext({ baseURL, extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` } });
  try {
    const response = await operator.post('/api/engineering/import/json/apply?mode=CreateOnly', {
      headers: { ...jsonHeaders, 'x-elitescada-workspace-version': String(current.changeVersion) },
      data: JSON.stringify(packageData)
    });
    expect(response.status()).toBe(403);
  } finally {
    await operator.dispose();
  }

  const listed = await request.get('/api/engineering/scripts');
  const scripts = (await listed.json() as Array<Record<string, unknown>>).map(normalizeScriptDefinition);
  expect(scripts.some(item => item.id === script.id)).toBeFalsy();
});

test('Script delete reports dependent Script and leaves target intact', async ({ request }) => {
  const target = makeScript(crypto.randomUUID(), `scripts/delete-target-${Date.now()}.py`);
  const dependent = makeScript(crypto.randomUUID(), `scripts/delete-dependent-${Date.now()}.py`);
  dependent.dependencies = [{ kind: 'script', stableReference: target.id }];
  let created = false;

  try {
    const before = await workspace(request);
    const packageData = multiScriptPackage([target, dependent], before.authorityPolicyReference);
    const previewResponse = await preview(request, packageData, 'CreateOnly');
    expect(previewResponse.canApply).toBeTruthy();
    expect(previewResponse.createCount).toBe(2);
    const applied = await apply(request, packageData, 'CreateOnly', before.changeVersion);
    expect(applied.ok()).toBeTruthy();
    created = true;

    const afterCreate = await workspace(request);
    const blocked = await request.delete(`/api/engineering/scripts/${target.id}`, {
      headers: { 'x-elitescada-workspace-version': String(afterCreate.changeVersion) }
    });
    expect(blocked.status()).toBe(409);
    const conflict = await blocked.json() as { dependencies: Array<{ entityKind: string; entityId: string; entityKey: string; relation: string }> };
    expect(conflict.dependencies).toEqual(expect.arrayContaining([
      expect.objectContaining({ entityKind: 'script', entityId: dependent.id, entityKey: dependent.path, relation: 'scriptDependency' })
    ]));

    const afterConflict = await workspace(request);
    expect(afterConflict.changeVersion).toBe(afterCreate.changeVersion);
    const listed = await request.get('/api/engineering/scripts');
    const scripts = (await listed.json() as Array<Record<string, unknown>>).map(normalizeScriptDefinition);
    expect(scripts.some(item => item.id === target.id)).toBeTruthy();
  } finally {
    if (created) {
      await bestEffortDelete(request, dependent.id);
      await bestEffortDelete(request, target.id);
    }
  }
});

function makeScript(id: string, path: string): ScriptEngineeringDefinition {
  return {
    id,
    path,
    name: path.split('/').pop()?.replace(/\.py$/, '') ?? 'Script',
    scope: 'clientVisual',
    source: 'pass\n',
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [],
    dependencies: [],
    description: null,
    metadata: {}
  };
}

function multiScriptPackage(
  scripts: ScriptEngineeringDefinition[],
  authorityPolicyReference: AuthorityPolicyReferenceEngineering
): CanonicalScriptPackage {
  const first = buildCanonicalScriptPackage(scripts[0]!, [], undefined, authorityPolicyReference);
  return {
    ...first,
    scripts: scripts.map(script => buildCanonicalScriptPackage(script, [], first.exportedAt, authorityPolicyReference).scripts[0]!)
  };
}

function previewResult(canApply: boolean, createCount: number, updateCount: number): ScriptImportPreview {
  return { mode: 0, createCount, updateCount, skipCount: 0, errorCount: canApply ? 0 : 1, items: [], canApply };
}

async function workspace(request: APIRequestContext): Promise<{
  changeVersion: number;
  authorityPolicyReference: AuthorityPolicyReferenceEngineering;
}> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json() as {
    changeVersion: number;
    authorityPolicyReference: AuthorityPolicyReferenceEngineering;
  };
}

async function preview(
  request: APIRequestContext,
  packageData: CanonicalScriptPackage,
  mode: 'CreateOnly' | 'UpdateExisting'
): Promise<ScriptImportPreview> {
  const response = await request.post(`/api/engineering/import/json/preview?mode=${mode}`, {
    headers: jsonHeaders,
    data: JSON.stringify(packageData)
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as ScriptImportPreview;
}

async function apply(
  request: APIRequestContext,
  packageData: CanonicalScriptPackage,
  mode: 'CreateOnly' | 'UpdateExisting',
  expectedChangeVersion: number
) {
  return await request.post(`/api/engineering/import/json/apply?mode=${mode}`, {
    headers: { ...jsonHeaders, 'x-elitescada-workspace-version': String(expectedChangeVersion) },
    data: JSON.stringify(packageData)
  });
}

async function bestEffortDelete(request: APIRequestContext, scriptId: string): Promise<void> {
  const current = await request.get('/api/engineering/workspace');
  if (!current.ok()) return;
  const descriptor = await current.json() as { changeVersion: number };
  await request.delete(`/api/engineering/scripts/${scriptId}`, {
    headers: { 'x-elitescada-workspace-version': String(descriptor.changeVersion) }
  });
}


function rgbChannels(value: string): [number, number, number] {
  const channels = value.match(/[\d.]+/g)?.slice(0, 3).map(Number);
  if (!channels || channels.length !== 3) throw new Error(`Expected rgb/rgba color, received ${value}`);
  return channels as [number, number, number];
}

function relativeLuminance(value: string): number {
  const [red, green, blue] = rgbChannels(value).map(channel => {
    const normalized = channel / 255;
    return normalized <= 0.04045
      ? normalized / 12.92
      : ((normalized + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrastRatio(foreground: string, background: string): number {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}

test('mounted R2 generated snippet validates, Preview/Applies and reopens unchanged', async ({ page, request }) => {
  test.setTimeout(120_000);
  const unique = crypto.randomUUID();
  const name = `R2 Authoring ${unique.slice(0, 8)}`;
  const path = `scripts/r2-authoring-${unique}.py`;
  const generated = buildScriptAssistantVisualValueSnippet(
    'core.button',
    '00000000-0000-4000-8000-000000000001/00000000-0000-4000-8000-000000000002',
    'visible',
    'write',
    'false'
  );
  expect(generated.enabled).toBeTruthy();
  const source = [
    'async def generated_action():',
    ...generated.code.split('\n').map(line => `    ${line}`),
    ''
  ].join('\n');

  let createdId: string | null = null;
  try {
    await page.addInitScript(() => {
      window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
    });
    await page.goto('/engineering');

    const navigation = page.locator('.eng-nav');
    await navigation.getByRole('button', { name: /Scripts/ }).click();
    await page.getByRole('button', { name: 'Novo Script' }).click();

    const editor = page.locator('main.script-editor');
    await editor.getByLabel('Nome', { exact: true }).fill(name);
    await editor.getByLabel('Path', { exact: true }).fill(path);

    const pythonEditor = page.getByTestId('python-monaco-editor');
    await pythonEditor.locator('.monaco-editor').click();
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.insertText(source);

    await expect(pythonEditor.getByText(/VALID · sintaxe válida/)).toBeVisible({ timeout: 45_000 });
    await expect(page.getByTestId('script-reference-diagnostics')).toBeVisible();

    await editor.getByRole('button', { name: 'Validar / Preview' }).click();
    await expect(editor.getByText(/Preview válido/)).toBeVisible({ timeout: 45_000 });
    await editor.getByRole('button', { name: 'Aplicar Preview' }).click();
    await expect(page.getByText('Script criado no Working.')).toBeVisible({ timeout: 45_000 });

    const listed = await request.get('/api/engineering/scripts');
    expect(listed.ok()).toBeTruthy();
    const scripts = (await listed.json() as Array<Record<string, unknown>>).map(normalizeScriptDefinition);
    const created = scripts.find(script => script.path === path);
    expect(created).toBeTruthy();
    createdId = created!.id;
    expect(created!.source).toBe(source);

    await page.reload();
    await page.locator('.eng-nav').getByRole('button', { name: /Scripts/ }).click();
    await page.locator('.script-list').getByRole('button', { name: new RegExp(name) }).click();
    await expect(page.getByTestId('python-monaco-editor').locator('.view-lines'))
      .toContainText('async def generated_action()', { timeout: 30_000 });
    await expect(page.getByTestId('python-monaco-editor').locator('.view-lines'))
      .toContainText('visual_property_write');
  } finally {
    if (createdId) await bestEffortDelete(request, createdId);
  }
});

test('mounted specialized Engineering surfaces keep dark/light contrast and Monaco follows active theme', async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.app.theme', 'dark');
    window.localStorage.setItem('elitescada.engineering.locale', 'pt-BR');
  });
  await page.goto('/engineering');

  const navigation = page.locator('.eng-nav');
  await navigation.getByRole('button', { name: /Relatórios/ }).click();
  await expect(page.getByTestId('report-designer-workspace')).toBeVisible();

  await navigation.getByRole('button', { name: /Scripts/ }).click();
  await page.getByRole('button', { name: /Novo Script/ }).click();
  const pythonEditor = page.getByTestId('python-monaco-editor');
  const monaco = pythonEditor.locator('.monaco-editor');
  await expect(monaco).toBeVisible();

  const shell = page.locator('.eng-shell');
  await shell.evaluate(element => {
    const fixture = document.createElement('div');
    fixture.dataset.testid = 'theme-state-fixture';
    fixture.style.cssText = 'position:fixed;left:8px;bottom:8px;width:360px;max-height:70vh;overflow:auto;z-index:9999;padding:8px';
    fixture.innerHTML = `
      <section class="eng-editor-section">
        <div class="eng-editor-form-panel">
          <label class="eng-editor-field"><span>Structured</span><input data-testid="theme-structured" value="value" /></label>
        </div>
      </section>
      <section class="eng-mutation-panel">
        <div class="eng-mutation-grid">
          <article class="eng-mutation-card">
            <button data-testid="theme-disabled" disabled>Disabled</button>
            <div class="eng-mutation-warning" data-testid="theme-warning">Warning</div>
            <div class="eng-bulk-preview valid" data-testid="theme-success">Success</div>
            <div class="eng-bulk-preview invalid" data-testid="theme-error">Error</div>
            <button class="gateway-route-row" data-testid="theme-hover"><span>Hover</span></button>
            <button class="gateway-route-row active" data-testid="theme-selected"><span>Selected</span></button>
          </article>
        </div>
      </section>
      <section class="report-designer-workspace">
        <div class="report-designer-shell" data-testid="theme-report-chrome">
          <main class="report-designer-main">
            <div class="report-page" data-testid="theme-report-paper">Paper</div>
          </main>
        </div>
      </section>
    `;
    element.append(fixture);
  });

  const theme = page.getByRole('combobox', { name: 'Tema' });
  const pythonBackgrounds: string[] = [];

  for (const mode of ['dark', 'light'] as const) {
    await theme.selectOption(mode);
    await expect(page.locator('html')).toHaveAttribute('data-app-theme', mode);
    await expect.poll(() => monaco.evaluate(element => element.classList.contains('vs-dark')))
      .toBe(mode === 'dark');

    const pythonColors = await pythonEditor.evaluate(element => {
      const editor = getComputedStyle(element);
      const path = getComputedStyle(element.querySelector('.python-editor__path')!);
      return { foreground: editor.color, background: editor.backgroundColor, muted: path.color };
    });
    expect(contrastRatio(pythonColors.foreground, pythonColors.background)).toBeGreaterThanOrEqual(4.5);
    expect(contrastRatio(pythonColors.muted, pythonColors.background)).toBeGreaterThanOrEqual(4.5);
    pythonBackgrounds.push(pythonColors.background);

    for (const testId of ['theme-disabled', 'theme-warning', 'theme-success', 'theme-error', 'theme-selected', 'theme-report-chrome']) {
      const colors = await page.getByTestId(testId).evaluate(element => {
        const style = getComputedStyle(element);
        return { foreground: style.color, background: style.backgroundColor };
      });
      expect(contrastRatio(colors.foreground, colors.background), `${mode} ${testId}`).toBeGreaterThanOrEqual(4.5);
    }

    const structured = page.getByTestId('theme-structured');
    await structured.focus();
    const focus = await structured.evaluate(element => {
      const style = getComputedStyle(element);
      return { outline: style.outlineColor, token: getComputedStyle(element.closest('.eng-shell')!).getPropertyValue('--eng-focus').trim() };
    });
    const resolvedFocus = await page.evaluate(value => {
      const probe = document.createElement('span');
      probe.style.color = value;
      document.body.append(probe);
      const color = getComputedStyle(probe).color;
      probe.remove();
      return color;
    }, focus.token);
    expect(focus.outline).toBe(resolvedFocus);

    const hover = page.getByTestId('theme-hover');
    await page.mouse.move(0, 0);
    const beforeHover = await hover.evaluate(element => getComputedStyle(element).backgroundColor);
    await hover.scrollIntoViewIfNeeded();
    await hover.hover();
    const afterHover = await hover.evaluate(element => getComputedStyle(element).backgroundColor);
    expect(afterHover).not.toBe(beforeHover);

    const paper = await page.getByTestId('theme-report-paper').evaluate(element => {
      const style = getComputedStyle(element);
      return { foreground: style.color, background: style.backgroundColor };
    });
    expect(paper.background).toBe('rgb(255, 255, 255)');
    expect(contrastRatio(paper.foreground, paper.background)).toBeGreaterThanOrEqual(4.5);
  }

  expect(pythonBackgrounds[0]).not.toBe(pythonBackgrounds[1]);
});
