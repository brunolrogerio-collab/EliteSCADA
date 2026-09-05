import { expect, test, type APIRequestContext } from '@playwright/test';
import { normalizeDynamoParameterValue } from '../src/runtime/visual-navigation/dynamoParameterWireContract';
import { expandRuntimeDynamoVisuals } from '../src/runtime/visual-navigation/runtimeDynamoVisualProjection';
import { runtimeDynamoElementIdentity } from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';

const projectKey = 'e2e-wave11';
const definitionId = 'c2100000-0000-4000-8000-000000000001';
const definitionKey = 'c21.dynamo.boolean-indicator';
const childId = 'c2100000-0000-4000-8000-000000000002';
const instanceAId = 'c2100000-0000-4000-8000-000000000101';
const instanceBId = 'c2100000-0000-4000-8000-000000000102';
const tagAId = 'c2100000-0000-4000-8000-000000000201';
const tagBId = 'c2100000-0000-4000-8000-000000000202';
const tagAPath = 'C21.Dynamo.InstanceA.State';
const tagBPath = 'C21.Dynamo.InstanceB.State';

const parameter = (tagId: string, value: unknown = undefined) => ({
  key: 'state', kind: 'tagReference', ...(value === undefined ? {} : { value }),
  tagReference: { tagId, selector: null }, version: 1
});

function dynamoDefinition() {
  return {
    id: definitionId, key: definitionKey, name: 'C21 reusable Boolean indicator',
    templateKey: null, bindings: [], properties: {}, context: {}, metadata: { correction: 'c21' },
    parameters: [{
      key: 'state', kind: 'tagReference', required: true,
      defaultValue: null, defaultTagReference: null, version: 1
    }],
    elements: [{
      id: childId, key: 'indicator', type: 'core.rectangle',
      properties: {
        x: 0, y: 0, width: 120, height: 80, zIndex: 1, visible: false, opacity: 1,
        fillStyle: 'solid', fillColor: '#166534', strokeColor: '#E2E8F0',
        strokeWidth: 2, strokeStyle: 'solid', cornerRadius: 8
      },
      bindings: [{
        key: 'visible', kind: 'tag', target: tagAPath, direction: null,
        metadata: { dynamoParameter: 'state' }, tagReference: { tagId: tagAId, selector: null }
      }]
    }]
  };
}

function dynamoInstance(id: string, key: string, x: number, tagId: string, wireValue: unknown = undefined) {
  return {
    id, key, type: 'dynamo', dynamoKey: definitionKey,
    properties: { x, y: 760, width: 120, height: 80, zIndex: 401, visible: true },
    dynamoParameters: [parameter(tagId, wireValue)], metadata: { correction: 'c21' }
  };
}

function memoryTag(id: string, name: string, path: string, source: any) {
  return {
    id, name, path, dataType: 'boolean', source: source.key, dataSourceId: source.id, address: null,
    engineeringUnit: null, description: 'C21 generic Dynamo TagReference runtime validation', readOnly: false,
    scaleMinimum: null, scaleMaximum: null,
    historian: { enabled: false, strategy: 'none', deadband: null, periodMilliseconds: null, maximumPeriodMilliseconds: null },
    metadata: { correction: 'c21' }, accessPolicy: null,
    initialValue: { dataType: 'boolean', value: false }, addressSelector: null, communicationBinding: null
  };
}

test('C21 converges wire nulls and projects two TagReference instances independently', () => {
  const normalized = normalizeDynamoParameterValue(parameter(tagAId, null) as any);
  expect(normalized.kind).toBe('TagReference');
  expect(normalized.value).toBeUndefined();
  expect(normalized.tagReference?.tagId).toBe(tagAId);

  const expanded = expandRuntimeDynamoVisuals([
    dynamoInstance(instanceAId, 'c21-instance-a', 1450, tagAId, null),
    dynamoInstance(instanceBId, 'c21-instance-b', 1600, tagBId, null)
  ] as any, [dynamoDefinition()] as any);

  expect(expanded[0].children?.[0]?.id).toBe(runtimeDynamoElementIdentity(instanceAId, childId));
  expect(expanded[1].children?.[0]?.id).toBe(runtimeDynamoElementIdentity(instanceBId, childId));
  expect(expanded[0].children?.[0]?.bindings?.[0]?.tagReference?.tagId).toBe(tagAId);
  expect(expanded[1].children?.[0]?.bindings?.[0]?.tagReference?.tagId).toBe(tagBId);

  const rejected = expandRuntimeDynamoVisuals([
    dynamoInstance(instanceAId, 'c21-invalid', 1450, tagAId, true)
  ] as any, [dynamoDefinition()] as any);
  expect(rejected[0].metadata?.['runtime.dynamo.diagnosticCode'])
    .toBe('VISUAL_RUNTIME_DYNAMO_PARAMETER_SHAPE_INVALID');
});

test('C21 Active lifecycle subscribes and renders two TagReference instances independently', async ({ page, request }) => {
  const original = await loadWorking(request);
  try {
    const candidate = structuredClone(original);
    const screen = candidate.screens?.find((item: any) => item.key === 'demo.overview');
    expect(screen?.id, 'C21 requires the canonical Wave11 overview').toBeTruthy();
    const source = candidate.dataSources?.find((item: any) =>
      item.enabled !== false && item.driver === 'builtin.memory.server' && item.id && item.key);
    expect(source?.id, 'C21 requires generic Server Memory').toBeTruthy();

    candidate.tags = (candidate.tags ?? []).filter((tag: any) =>
      ![tagAId, tagBId].includes(tag.id) && ![tagAPath, tagBPath].includes(tag.path));
    candidate.tags.push(
      memoryTag(tagAId, 'C21 Instance A State', tagAPath, source),
      memoryTag(tagBId, 'C21 Instance B State', tagBPath, source)
    );
    candidate.dynamos = (candidate.dynamos ?? []).filter((item: any) =>
      item.id !== definitionId && item.key !== definitionKey);
    candidate.dynamos.push(dynamoDefinition());
    screen.elements = (screen.elements ?? []).filter((element: any) =>
      ![instanceAId, instanceBId].includes(element.id));
    screen.elements.push(
      dynamoInstance(instanceAId, 'c21-instance-a', 1450, tagAId),
      dynamoInstance(instanceBId, 'c21-instance-b', 1600, tagBId)
    );
    candidate.startupScreenId = screen.id;

    const before = await loadWorkspace(request);
    const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
    expect(previewResponse.ok(), `C21 preview HTTP ${previewResponse.status()}: ${await previewResponse.text()}`).toBeTruthy();
    const preview = await previewResponse.json() as any;
    expect(preview.canApply, JSON.stringify(preview.items ?? [], null, 2)).toBe(true);
    expect(preview.errorCount).toBe(0);
    const after = await loadWorkspace(request);
    expect(after.changeVersion).toBe(before.changeVersion);

    const apply = await request.post('/api/engineering/import/json/apply', {
      headers: { 'x-elitescada-workspace-version': String(after.changeVersion) }, data: candidate
    });
    expect(apply.ok(), `C21 apply HTTP ${apply.status()}: ${await apply.text()}`).toBeTruthy();
    const saved = await savePublishActivate(request, 'Wave 14 C21 Dynamo TagReference runtime');

    const activeResponse = await request.get('/api/runtime/application');
    expect(activeResponse.ok()).toBeTruthy();
    const active = await activeResponse.json() as any;
    const activeScreen = active.package.screens.find((item: any) => item.id === screen.id);
    const activeA = activeScreen.elements.find((item: any) => item.id === instanceAId);
    const activeB = activeScreen.elements.find((item: any) => item.id === instanceBId);
    expect(activeA.dynamoParameters[0]).toMatchObject({ kind: 'tagReference', value: null, tagReference: { tagId: tagAId } });
    expect(activeB.dynamoParameters[0]).toMatchObject({ kind: 'tagReference', value: null, tagReference: { tagId: tagBId } });

    await writeTag(request, tagAId, true);
    await writeTag(request, tagBId, false);
    await page.goto('/');
    await expect(page.getByTestId('runtime-engineering-application'))
      .toHaveAttribute('data-runtime-revision', String(saved.revision));
    const canvas = page.getByTestId('runtime-engineering-canvas');
    const childA = canvas.locator(`[data-object-id="${runtimeDynamoElementIdentity(instanceAId, childId)}"]`);
    const childB = canvas.locator(`[data-object-id="${runtimeDynamoElementIdentity(instanceBId, childId)}"]`);
    await expect(childA).toBeVisible();
    await expect(childB).toBeHidden();

    await writeTag(request, tagAId, false);
    await writeTag(request, tagBId, true);
    await expect(childA).toBeHidden();
    await expect(childB).toBeVisible();
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', { data: original });
    expect(restore.ok(), `C21 cleanup apply HTTP ${restore.status()}: ${await restore.text()}`).toBeTruthy();
    await savePublishActivate(request, 'Wave 14 C21 cleanup');
  }
});

async function loadWorking(request: APIRequestContext) {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json() as any;
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json() as { changeVersion: number };
}

async function savePublishActivate(request: APIRequestContext, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${projectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save HTTP ${save.status()}: ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };
  const publish = await request.post(`/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish HTTP ${publish.status()}: ${await publish.text()}`).toBeTruthy();
  const activate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate HTTP ${activate.status()}: ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function writeTag(request: APIRequestContext, tagId: string, value: boolean) {
  const response = await request.post(`/api/tags/${encodeURIComponent(tagId)}/write`, { data: { value } });
  expect(response.status(), `TAG write HTTP ${response.status()}: ${await response.text()}`).toBe(202);
}
