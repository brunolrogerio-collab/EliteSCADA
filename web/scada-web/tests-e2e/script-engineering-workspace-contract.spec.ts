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
import type {
  CanonicalScriptPackage,
  ScriptEngineeringDefinition,
  ScriptImportPreview,
  ScriptMutationPreviewToken
} from '../src/engineering/scripts/scriptEngineeringTypes';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';
const jsonHeaders = { 'content-type': 'application/json; charset=utf-8' };

test.describe.configure({ mode: 'serial' });

test('Script workspace consumes Engineering dark-theme tokens without light surface fallbacks', async () => {
  const css = await readFile(new URL('../src/engineering/scripts/script-engineering-workspace.css', import.meta.url), 'utf8');

  expect(css).toContain('--script-surface: var(--eng-panel, #121922)');
  expect(css).toContain('--script-border: var(--eng-border, #283544)');
  expect(css).toContain('--script-text: var(--eng-text, #e8edf3)');
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
    const createPackage = buildCanonicalScriptPackage(created, []);
    const createPreview = await preview(request, createPackage, 'CreateOnly');
    expect(createPreview.canApply).toBeTruthy();
    expect(createPreview.createCount).toBe(1);

    const createResponse = await apply(request, createPackage, 'CreateOnly', before.changeVersion);
    expect(createResponse.ok()).toBeTruthy();
    exists = true;

    const afterCreate = await workspace(request);
    expect(afterCreate.changeVersion).toBeGreaterThan(before.changeVersion);

    const updated = { ...created, description: 'updated through canonical Preview/Apply' };
    const updatePackage = buildCanonicalScriptPackage(updated, []);
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

test('Script mutation preserves backend authorization boundary', async ({ request }) => {
  const script = makeScript(crypto.randomUUID(), `scripts/auth-${Date.now()}.py`);
  const packageData = buildCanonicalScriptPackage(script, []);
  const current = await workspace(request);

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
    const packageData = multiScriptPackage([target, dependent]);
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

function multiScriptPackage(scripts: ScriptEngineeringDefinition[]): CanonicalScriptPackage {
  const first = buildCanonicalScriptPackage(scripts[0]!, []);
  return {
    ...first,
    scripts: scripts.map(script => buildCanonicalScriptPackage(script, [], first.exportedAt).scripts[0]!)
  };
}

function previewResult(canApply: boolean, createCount: number, updateCount: number): ScriptImportPreview {
  return { mode: 0, createCount, updateCount, skipCount: 0, errorCount: canApply ? 0 : 1, items: [], canApply };
}

async function workspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json() as { changeVersion: number };
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