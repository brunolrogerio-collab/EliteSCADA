import { expect, test, type APIRequestContext } from '@playwright/test';
import {
  buildCanonicalScriptPackage,
  normalizeScriptDefinition,
  normalizeVisualEventReference
} from '../src/engineering/scripts/ScriptEngineeringWorkspace.logic';
import type {
  CanonicalScriptPackage,
  ScriptEngineeringDefinition,
  ScriptImportPreview,
  ScriptVisualEventReference
} from '../src/engineering/scripts/scriptEngineeringTypes';

const jsonHeaders = { 'content-type': 'application/json; charset=utf-8' };

type VisualElement = {
  id?: string | null;
  key: string;
  type: string;
  children?: VisualElement[] | null;
};

type EngineeringPackage = {
  screens?: Array<{ id?: string | null; key: string; elements?: VisualElement[] | null }>;
  tags?: Array<{ id?: string | null; path: string; dataType: string }>;
};

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

test('mounted Events editor persists click, timer and typed TAG bit through canonical Preview/Apply and reload', async ({ page, request }) => {
  const exported = await request.get('/api/engineering/export/json');
  expect(exported.ok()).toBeTruthy();
  const project = await exported.json() as EngineeringPackage;
  const screen = project.screens?.find(candidate => Boolean(candidate.id) && flatten(candidate.elements ?? []).some(element => Boolean(element.id)));
  expect(screen, 'seeded demo must expose one persisted Screen with a persisted visual object').toBeTruthy();
  const visualObject = flatten(screen!.elements ?? []).find(element => Boolean(element.id));
  expect(visualObject).toBeTruthy();

  const tag = project.tags?.find(candidate => Boolean(candidate.id) && ['int16', 'int32', 'int64'].includes(candidate.dataType.toLowerCase()));
  expect(tag, 'seeded demo must expose one stable-ID integral TAG for bit selector acceptance').toBeTruthy();

  const script = makeScript(crypto.randomUUID(), `scripts/wave10-events-${crypto.randomUUID()}.py`, tag!.id!);
  let created = false;

  try {
    const before = await workspace(request);
    const createPackage = buildCanonicalScriptPackage(script, []);
    const createPreview = await preview(request, createPackage, 'CreateOnly');
    expect(createPreview.canApply).toBeTruthy();
    expect((await apply(request, createPackage, 'CreateOnly', before.changeVersion)).ok()).toBeTruthy();
    created = true;

    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();
    await page.locator(`[data-canvas-object-id="${visualObject!.id}"]`).click();

    const editor = page.getByTestId('visual-events-editor');
    await expect(editor).toBeVisible();

    await editor.getByLabel('Event').selectOption('click');
    await editor.getByLabel('Script').selectOption(script.id);
    await editor.getByLabel('Entry point').selectOption('on_click');
    await previewAndApply(editor);

    await editor.getByLabel('Event').selectOption('timer');
    await editor.getByLabel('Script').selectOption(script.id);
    await editor.getByLabel('Entry point').selectOption('on_timer');
    await editor.getByRole('spinbutton', { name: 'Interval (ms)' }).fill('250');
    await previewAndApply(editor);

    await editor.getByLabel('Event').selectOption('tagChanged');
    await editor.getByLabel('Script').selectOption(script.id);
    await editor.getByLabel('Entry point').selectOption('on_tag');
    await editor.getByLabel('TAG target').selectOption(tag!.id!);
    await editor.getByTestId('visual-events-tag-bit').fill('7');
    await previewAndApply(editor);

    await page.reload();
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();
    await page.locator(`[data-canvas-object-id="${visualObject!.id}"]`).click();
    await expect(page.getByTestId('visual-events-editor')).toBeVisible();

    const persistedScript = await loadScript(request, script.id);
    expect(persistedScript.entryPoints).toEqual(expect.arrayContaining([
      expect.objectContaining({ eventKind: 'objectInteraction', handlerName: 'on_click' }),
      expect.objectContaining({ eventKind: 'timer', handlerName: 'on_timer', timerIntervalMs: 250 }),
      expect.objectContaining({
        eventKind: 'tagChanged', handlerName: 'on_tag',
        tagReference: { tagId: tag!.id, selector: { kind: 'bit', index: 7 } }
      })
    ]));

    const references = await loadVisualReferences(request);
    expect(references).toEqual(expect.arrayContaining([
      expect.objectContaining({
        visualDefinitionId: screen!.id,
        visualObjectId: visualObject!.id,
        eventKind: 'objectInteraction',
        scriptId: script.id,
        entryPoint: 'on_click'
      }),
      expect.objectContaining({
        visualDefinitionId: screen!.id,
        visualObjectId: null,
        eventKind: 'timer',
        scriptId: script.id,
        entryPoint: 'on_timer',
        timerIntervalMs: 250
      }),
      expect.objectContaining({
        visualDefinitionId: screen!.id,
        visualObjectId: visualObject!.id,
        eventKind: 'tagChanged',
        scriptId: script.id,
        entryPoint: 'on_tag',
        tagReference: { tagId: tag!.id, selector: { kind: 'bit', index: 7 } }
      })
    ]));
  } finally {
    if (created) await removeScriptAndOwnedReferences(request, script);
  }
});

async function previewAndApply(editor: ReturnType<Parameters<typeof expect>[0]['page'] extends never ? never : any>) {
  await editor.getByTestId('visual-events-preview').click();
  await expect(editor.getByText('Validated Engineering candidate.', { exact: true })).toBeVisible();
  await expect(editor.getByTestId('visual-events-apply')).toBeEnabled();
  await editor.getByTestId('visual-events-apply').click();
  await expect(editor.getByText('Validated Engineering candidate.', { exact: true })).toHaveCount(0);
}

function makeScript(id: string, path: string, tagId: string): ScriptEngineeringDefinition {
  return {
    id,
    path,
    name: 'Wave 10 Events',
    scope: 'clientVisual',
    source: 'def on_click():\n    pass\n\ndef on_timer():\n    pass\n\ndef on_tag():\n    pass\n',
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [
      { eventKind: 'objectInteraction', handlerName: 'on_click', targetReference: null, tagReference: null, timerIntervalMs: null },
      { eventKind: 'timer', handlerName: 'on_timer', targetReference: null, tagReference: null, timerIntervalMs: 1000 },
      { eventKind: 'tagChanged', handlerName: 'on_tag', targetReference: null, tagReference: { tagId, selector: null }, timerIntervalMs: null }
    ],
    dependencies: [],
    description: 'Wave 10 mounted Events editor acceptance fixture',
    metadata: { wave: '10', owner: 'dev1' }
  };
}

function flatten(elements: readonly VisualElement[]): VisualElement[] {
  return elements.flatMap(element => [element, ...flatten(element.children ?? [])]);
}

async function workspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json() as { changeVersion: number };
}

async function loadScript(request: APIRequestContext, scriptId: string): Promise<ScriptEngineeringDefinition> {
  const response = await request.get('/api/engineering/scripts');
  expect(response.ok()).toBeTruthy();
  const scripts = (await response.json() as Array<Record<string, unknown>>).map(normalizeScriptDefinition);
  const script = scripts.find(item => item.id === scriptId);
  expect(script).toBeTruthy();
  return script!;
}

async function loadVisualReferences(request: APIRequestContext): Promise<ScriptVisualEventReference[]> {
  const response = await request.get('/api/engineering/scripts/visual-event-references');
  expect(response.ok()).toBeTruthy();
  return (await response.json() as Array<Record<string, unknown>>).map(normalizeVisualEventReference);
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

async function removeScriptAndOwnedReferences(request: APIRequestContext, script: ScriptEngineeringDefinition): Promise<void> {
  const current = await loadScript(request, script.id).catch(() => null);
  if (!current) return;
  const version = await workspace(request);
  const clearPackage = buildCanonicalScriptPackage(current, []);
  const clearPreview = await preview(request, clearPackage, 'UpdateExisting');
  if (clearPreview.canApply) await apply(request, clearPackage, 'UpdateExisting', version.changeVersion);
  const afterClear = await workspace(request);
  await request.delete(`/api/engineering/scripts/${script.id}`, {
    headers: { 'x-elitescada-workspace-version': String(afterClear.changeVersion) }
  });
}
