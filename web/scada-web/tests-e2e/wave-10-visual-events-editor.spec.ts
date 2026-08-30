import { expect, test, type APIRequestContext, type Locator } from '@playwright/test';
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
  [key: string]: unknown;
};

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

test('mounted Events editor persists click and canonical timer/TAG-bit associations through Preview/Apply and reload', async ({ page, request }) => {
  test.setTimeout(90_000);

  const exported = await request.get('/api/engineering/export/json');
  expect(exported.ok()).toBeTruthy();
  const originalProject = await exported.json() as EngineeringPackage;
  const screen = originalProject.screens?.find(candidate => Boolean(candidate.id));
  expect(screen, 'seeded demo must expose one persisted Screen for Wave 10 acceptance').toBeTruthy();

  const tag = originalProject.tags?.find(candidate => Boolean(candidate.id) && ['int16', 'int32', 'int64'].includes(candidate.dataType.toLowerCase()));
  expect(tag, 'seeded demo must expose one stable-ID integral TAG for bit selector acceptance').toBeTruthy();

  const originalObjectIds = new Set(
    flatten(screen!.elements ?? []).flatMap(element => element.id ? [element.id] : []));
  const script = makeScript(crypto.randomUUID(), `scripts/wave10-events-${crypto.randomUUID()}.py`, tag!.id!);
  let created = false;
  let fixtureApplied = false;

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();

    await page.locator('[data-object-type="core.rectangle"]').click();
    const rectangle = page.locator('[data-canvas-object-type="core.rectangle"]').last();
    await expect(rectangle).toBeVisible();
    await rectangle.click();

    const visualApply = page.getByTestId('visual-editor-apply');
    await expect(visualApply).toBeDisabled();
    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(visualApply).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await visualApply.click();
    fixtureApplied = true;

    const visualObject = await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const project = await response.json() as EngineeringPackage;
      const persistedScreen = project.screens?.find(candidate => candidate.id === screen!.id);
      return flatten(persistedScreen?.elements ?? []).find(element =>
        element.type === 'core.rectangle' && Boolean(element.id) && !originalObjectIds.has(element.id!)) ?? null;
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
      const project = await response.json() as EngineeringPackage;
      const persistedScreen = project.screens!.find(candidate => candidate.id === screen!.id)!;
      return flatten(persistedScreen.elements ?? []).find(element =>
        element.type === 'core.rectangle' && Boolean(element.id) && !originalObjectIds.has(element.id!))!;
    });

    const before = await workspace(request);
    const createPackage = buildCanonicalScriptPackage(script, []);
    const createPreview = await preview(request, createPackage, 'CreateOnly');
    expect(createPreview.canApply).toBeTruthy();
    expect((await apply(request, createPackage, 'CreateOnly', before.changeVersion)).ok()).toBeTruthy();
    created = true;

    await page.reload();
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();
    await page.locator(`[data-canvas-object-id="${visualObject.id}"]`).click();

    const editor = page.getByTestId('visual-events-editor');
    await expect(editor).toBeVisible();

    await editor.getByLabel('Event', { exact: true }).selectOption('objectInteraction');
    await editor.getByLabel('Script', { exact: true }).selectOption(script.id);
    await editor.getByLabel('Entry point', { exact: true }).selectOption('on_click');
    await previewAndApply(editor);

    const clickReference = (await loadVisualReferences(request)).find(reference =>
      reference.visualDefinitionId === screen!.id &&
      reference.visualObjectId === visualObject.id &&
      reference.scriptId === script.id &&
      reference.entryPoint === 'on_click' &&
      reference.eventKind === 'objectInteraction');
    expect(clickReference, 'mounted Events editor must persist the click association').toBeTruthy();

    const updatedScript: ScriptEngineeringDefinition = {
      ...script,
      entryPoints: script.entryPoints.map(entryPoint => {
        if (entryPoint.eventKind === 'timer') {
          return { ...entryPoint, timerIntervalMs: 250 };
        }
        if (entryPoint.eventKind === 'tagChanged') {
          return {
            ...entryPoint,
            tagReference: { tagId: tag!.id!, selector: { kind: 'bit', index: 7 } }
          };
        }
        return entryPoint;
      })
    };

    const timerReference: ScriptVisualEventReference = {
      visualDefinitionId: screen!.id!,
      visualObjectId: null,
      eventKind: 'timer',
      scriptId: script.id,
      entryPoint: 'on_timer',
      targetReference: null,
      tagReference: null,
      timerIntervalMs: 250
    };
    const tagReference: ScriptVisualEventReference = {
      visualDefinitionId: screen!.id!,
      visualObjectId: visualObject.id!,
      eventKind: 'tagChanged',
      scriptId: script.id,
      entryPoint: 'on_tag',
      targetReference: null,
      tagReference: { tagId: tag!.id!, selector: { kind: 'bit', index: 7 } },
      timerIntervalMs: null
    };

    const updateVersion = await workspace(request);
    const updatePackage = buildCanonicalScriptPackage(updatedScript, [clickReference!, timerReference, tagReference]);
    const updatePreview = await preview(request, updatePackage, 'UpdateExisting');
    expect(updatePreview.canApply).toBeTruthy();
    expect((await apply(request, updatePackage, 'UpdateExisting', updateVersion.changeVersion)).ok()).toBeTruthy();

    await page.reload();
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();
    await page.locator(`[data-canvas-object-id="${visualObject.id}"]`).click();
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
        visualObjectId: visualObject.id,
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
        visualObjectId: visualObject.id,
        eventKind: 'tagChanged',
        scriptId: script.id,
        entryPoint: 'on_tag',
        tagReference: { tagId: tag!.id, selector: { kind: 'bit', index: 7 } }
      })
    ]));
  } finally {
    if (created) await removeScriptAndOwnedReferences(request, script);
    if (fixtureApplied) {
      const restore = await request.post('/api/engineering/import/json/apply', {
        headers: jsonHeaders,
        data: originalProject
      });
      expect(restore.ok()).toBeTruthy();
    }
  }
});

async function previewAndApply(editor: Locator): Promise<void> {
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
  const response = await request.get('/api/engineering/script-visual-event-references');
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