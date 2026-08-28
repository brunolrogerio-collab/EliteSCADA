import { expect, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';
import {
  buildCanonicalScriptPackage,
  normalizeScriptDefinition
} from '../src/engineering/scripts/ScriptEngineeringWorkspace.logic';
import type {
  CanonicalScriptPackage,
  ScriptEngineeringDefinition,
  ScriptImportPreview
} from '../src/engineering/scripts/scriptEngineeringTypes';

const jsonHeaders = { 'content-type': 'application/json; charset=utf-8' };

test('Script source, metadata, entry points and dependencies round-trip through canonical backend Apply', async ({ request }) => {
  const helper = makeScript(crypto.randomUUID(), `scripts/helper-${crypto.randomUUID()}.py`);
  const subject = makeScript(crypto.randomUUID(), `scripts/roundtrip-${crypto.randomUUID()}.py`);
  subject.name = 'Round-trip Script';
  subject.source = 'def initialize():\n    return None\n';
  subject.enabled = false;
  subject.description = 'Preserve this description';
  subject.metadata = { owner: 'wave05', purpose: 'roundtrip' };
  subject.entryPoints = [{ eventKind: 'initialize', handlerName: 'initialize', targetReference: null }];
  subject.dependencies = [{ kind: 'script', stableReference: helper.id }];
  let created = false;

  try {
    const before = await workspace(request);
    const createPackage = multiScriptPackage([helper, subject]);
    const createPreview = await preview(request, createPackage, 'CreateOnly');
    expect(createPreview.canApply).toBeTruthy();
    expect(createPreview.createCount).toBe(2);
    expect((await apply(request, createPackage, 'CreateOnly', before.changeVersion)).ok()).toBeTruthy();
    created = true;

    const afterCreate = await loadScript(request, subject.id);
    expect(afterCreate).toMatchObject({
      id: subject.id,
      path: subject.path,
      name: subject.name,
      scope: 'clientVisual',
      source: subject.source,
      enabled: false,
      language: 'python',
      languageVersion: '3',
      description: subject.description,
      metadata: subject.metadata,
      entryPoints: subject.entryPoints,
      dependencies: subject.dependencies
    });

    const updateBase = await workspace(request);
    const updated: ScriptEngineeringDefinition = {
      ...afterCreate,
      name: 'Round-trip Script Updated',
      source: 'def initialize():\n    value = 1\n    return value\n',
      enabled: true,
      description: 'Updated without losing canonical fields',
      metadata: { ...afterCreate.metadata, revisionHint: 'updated' },
      entryPoints: [{ eventKind: 'initialize', handlerName: 'initialize', targetReference: 'screen-start' }],
      dependencies: [{ kind: 'script', stableReference: helper.id }]
    };
    const updatePackage = buildCanonicalScriptPackage(updated, []);
    const updatePreview = await preview(request, updatePackage, 'UpdateExisting');
    expect(updatePreview.canApply).toBeTruthy();
    expect(updatePreview.updateCount).toBe(1);
    expect((await apply(request, updatePackage, 'UpdateExisting', updateBase.changeVersion)).ok()).toBeTruthy();

    const afterUpdate = await loadScript(request, subject.id);
    expect(afterUpdate).toMatchObject({
      id: subject.id,
      path: updated.path,
      name: updated.name,
      scope: updated.scope,
      source: updated.source,
      enabled: updated.enabled,
      description: updated.description,
      metadata: updated.metadata,
      entryPoints: updated.entryPoints,
      dependencies: updated.dependencies
    });
  } finally {
    if (created) {
      await bestEffortDelete(request, subject.id);
      await bestEffortDelete(request, helper.id);
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
