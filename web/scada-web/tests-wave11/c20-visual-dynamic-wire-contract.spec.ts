import { expect, test, type APIRequestContext } from '@playwright/test';
import {
  resolveVisualDynamicState,
  visualTagSampleKey,
  type VisualDynamicSample
} from '../src/engineering/visual-editor/visualDynamicRuntime';
import {
  normalizeVisualAnalogFillDirection,
  normalizeVisualBooleanConditionKind,
  normalizeVisualExpressionDependencyKind,
  normalizeVisualExpressionValueType,
  normalizeVisualNumericIntervalMode,
  normalizeVisualValueSourceKind
} from '../src/engineering/visual-editor/visualDynamicWireContract';

const projectKey = 'e2e-wave11';
const objectId = 'c2000000-0000-0000-0000-000000000001';
const objectKey = 'c20-wire-roundtrip';
const frequencyPath = 'Demo.P01.Frequency';

test('C20 normalizes authoring and persisted visual dynamic enum representations and rejects unknown explicit values', () => {
  expect(normalizeVisualValueSourceKind('Tag')).toEqual({ ok: true, value: 'Tag' });
  expect(normalizeVisualValueSourceKind('tag')).toEqual({ ok: true, value: 'Tag' });
  expect(normalizeVisualValueSourceKind('ClientMemory')).toEqual({ ok: true, value: 'ClientMemory' });
  expect(normalizeVisualValueSourceKind('clientMemory')).toEqual({ ok: true, value: 'ClientMemory' });
  expect(normalizeVisualValueSourceKind('Expression')).toEqual({ ok: true, value: 'Expression' });
  expect(normalizeVisualValueSourceKind('expression')).toEqual({ ok: true, value: 'Expression' });

  expect(normalizeVisualExpressionValueType('Boolean')).toEqual({ ok: true, value: 'Boolean' });
  expect(normalizeVisualExpressionValueType('boolean')).toEqual({ ok: true, value: 'Boolean' });
  expect(normalizeVisualExpressionValueType('Number')).toEqual({ ok: true, value: 'Number' });
  expect(normalizeVisualExpressionValueType('number')).toEqual({ ok: true, value: 'Number' });

  expect(normalizeVisualExpressionDependencyKind('Tag')).toEqual({ ok: true, value: 'Tag' });
  expect(normalizeVisualExpressionDependencyKind('tag')).toEqual({ ok: true, value: 'Tag' });
  expect(normalizeVisualExpressionDependencyKind('ClientMemory')).toEqual({ ok: true, value: 'ClientMemory' });
  expect(normalizeVisualExpressionDependencyKind('clientMemory')).toEqual({ ok: true, value: 'ClientMemory' });

  expect(normalizeVisualBooleanConditionKind('Direct')).toEqual({ ok: true, value: 'Direct' });
  expect(normalizeVisualBooleanConditionKind('direct')).toEqual({ ok: true, value: 'Direct' });
  expect(normalizeVisualBooleanConditionKind('NumericInterval')).toEqual({ ok: true, value: 'NumericInterval' });
  expect(normalizeVisualBooleanConditionKind('numericInterval')).toEqual({ ok: true, value: 'NumericInterval' });

  expect(normalizeVisualNumericIntervalMode('Inside')).toEqual({ ok: true, value: 'Inside' });
  expect(normalizeVisualNumericIntervalMode('inside')).toEqual({ ok: true, value: 'Inside' });
  expect(normalizeVisualNumericIntervalMode('Outside')).toEqual({ ok: true, value: 'Outside' });
  expect(normalizeVisualNumericIntervalMode('outside')).toEqual({ ok: true, value: 'Outside' });

  for (const [authoring, wire] of [
    ['BottomToTop', 'bottomToTop'],
    ['TopToBottom', 'topToBottom'],
    ['LeftToRight', 'leftToRight'],
    ['RightToLeft', 'rightToLeft']
  ] as const) {
    expect(normalizeVisualAnalogFillDirection(authoring)).toEqual({ ok: true, value: authoring });
    expect(normalizeVisualAnalogFillDirection(wire)).toEqual({ ok: true, value: authoring });
  }

  expect(normalizeVisualValueSourceKind('bogus').ok).toBe(false);
  expect(normalizeVisualExpressionValueType('bogus').ok).toBe(false);
  expect(normalizeVisualExpressionDependencyKind('bogus').ok).toBe(false);
  expect(normalizeVisualBooleanConditionKind('bogus').ok).toBe(false);
  expect(normalizeVisualNumericIntervalMode('bogus').ok).toBe(false);
  expect(normalizeVisualAnalogFillDirection('bogus').ok).toBe(false);

  const tagId = 'c2000000-0000-0000-0000-000000000010';
  const wireElement = {
    id: objectId,
    key: objectKey,
    type: 'core.rectangle',
    properties: { visible: false, opacity: 1 },
    booleanConditions: [{
      propertyKey: 'visible',
      kind: 'numericInterval',
      source: {
        kind: 'tag',
        valueType: 'number',
        target: frequencyPath,
        tagReference: { tagId }
      },
      minimum: 0,
      maximum: 60,
      minimumInclusive: true,
      maximumInclusive: true,
      intervalMode: 'inside',
      negate: false
    }],
    analogFill: {
      source: {
        kind: 'tag',
        valueType: 'number',
        target: frequencyPath,
        tagReference: { tagId }
      },
      inputMinimum: 0,
      inputMaximum: 60,
      direction: 'bottomToTop',
      clamp: true,
      invertScale: false,
      fillColor: '#00AEEF'
    }
  } as any;
  const samples = new Map<string, VisualDynamicSample>([[
    visualTagSampleKey(tagId),
    {
      reference: frequencyPath,
      tagId,
      value: 30,
      dataType: 'Double',
      quality: 'Good'
    }
  ]]);

  const resolved = resolveVisualDynamicState(wireElement, { visible: false, opacity: 1 }, samples);
  expect(resolved.diagnostics).toEqual([]);
  expect(resolved.values.visible).toBe(true);
  expect(resolved.analogFill?.presentation.percent).toBe(50);
  expect(resolved.analogFill?.presentation.clipPath).toBe('inset(50% 0 0 0)');

  const invalid = structuredClone(wireElement);
  invalid.analogFill.direction = 'diagonalMysticism';
  const rejected = resolveVisualDynamicState(invalid, { visible: false, opacity: 1 }, samples);
  expect(rejected.analogFill).toBeNull();
  expect(rejected.diagnostics).toEqual(expect.arrayContaining([
    expect.objectContaining({ sourceKind: 'AnalogFill', message: expect.stringContaining('unsupported') })
  ]));
});

test('C20 persisted camelCase dynamics survive Save Publish Activate and drive Active Runtime Analog Fill', async ({ page, request }) => {
  const originalPackage = await loadWorking(request);

  try {
    const candidate = structuredClone(originalPackage);
    const screen = candidate.screens?.find((item: any) => item.key === 'demo.overview');
    const frequencyTag = candidate.tags?.find((tag: any) => tag.path === frequencyPath);
    expect(screen?.id, 'C20 depends on the canonical Wave11 overview screen').toBeTruthy();
    expect(frequencyTag?.id, `C20 depends on ${frequencyPath}`).toBeTruthy();
    expect(frequencyTag.readOnly).toBe(false);

    screen.elements = (screen.elements ?? []).filter((element: any) => element.id !== objectId && element.key !== objectKey);
    screen.elements.push({
      id: objectId,
      key: objectKey,
      type: 'core.rectangle',
      properties: {
        x: 1500,
        y: 40,
        width: 180,
        height: 240,
        zIndex: 320,
        visible: false,
        opacity: 1,
        fillStyle: 'solid',
        fillColor: '#0F172A',
        strokeColor: '#E2E8F0',
        strokeWidth: 2,
        strokeStyle: 'solid',
        cornerRadius: 8
      },
      booleanConditions: [{
        propertyKey: 'visible',
        kind: 'NumericInterval',
        source: {
          kind: 'Tag',
          valueType: 'Number',
          target: frequencyTag.path,
          tagReference: { tagId: frequencyTag.id }
        },
        minimum: 0,
        maximum: 60,
        minimumInclusive: true,
        maximumInclusive: true,
        intervalMode: 'Inside',
        negate: false,
        version: 1
      }],
      analogFill: {
        source: {
          kind: 'Tag',
          valueType: 'Number',
          target: frequencyTag.path,
          tagReference: { tagId: frequencyTag.id }
        },
        inputMinimum: 0,
        inputMaximum: 60,
        direction: 'BottomToTop',
        clamp: true,
        invertScale: false,
        fillColor: '#00AEEF',
        version: 1
      }
    });

    const beforePreview = await loadWorkspace(request);
    const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
    expect(previewResponse.ok(), `C20 preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`).toBeTruthy();
    const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
    expect(preview.canApply, JSON.stringify(preview.items ?? [], null, 2)).toBe(true);
    expect(preview.errorCount).toBe(0);

    const afterPreview = await loadWorkspace(request);
    expect(afterPreview.changeVersion).toBe(beforePreview.changeVersion);

    const applyResponse = await request.post('/api/engineering/import/json/apply', {
      headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
      data: candidate
    });
    expect(applyResponse.ok(), `C20 apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();

    const saved = await savePublishActivate(request, 'Wave 14 C20 visual dynamic wire contract');

    const activeResponse = await request.get('/api/runtime/application');
    expect(activeResponse.ok(), `C20 Active projection failed: HTTP ${activeResponse.status()} ${await activeResponse.text()}`).toBeTruthy();
    const active = await activeResponse.json() as any;
    expect(active.projectKey).toBe(projectKey);
    expect(active.revision).toBe(saved.revision);

    const activeScreen = active.package.screens.find((item: any) => item.key === 'demo.overview');
    const persisted = activeScreen.elements.find((element: any) => element.id === objectId);
    expect(persisted?.booleanConditions?.[0]).toMatchObject({
      kind: 'numericInterval',
      intervalMode: 'inside',
      source: { kind: 'tag', valueType: 'number' }
    });
    expect(persisted?.analogFill).toMatchObject({
      direction: 'bottomToTop',
      source: { kind: 'tag', valueType: 'number' }
    });

    await writeTag(request, frequencyTag.id, 30);

    await page.goto('/');
    await expect(page.getByTestId('runtime-engineering-application')).toHaveAttribute('data-runtime-revision', String(saved.revision));
    const mounted = page.locator(`[data-object-id="${objectId}"]`);
    await expect(mounted).toBeVisible();
    await expect(mounted).toHaveAttribute('data-dynamic-state', 'available');
    const fill = mounted.getByTestId('visual-analog-fill');
    await expect(fill).toBeVisible();
    await expect(fill).toHaveAttribute('data-fill-percent', '50');

    await writeTag(request, frequencyTag.id, 15);
    await expect(fill).toHaveAttribute('data-fill-percent', '25');

    await writeTag(request, frequencyTag.id, 75);
    await expect(mounted).toBeHidden();
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', { data: originalPackage });
    expect(restore.ok(), `C20 cleanup apply failed: HTTP ${restore.status()} ${await restore.text()}`).toBeTruthy();
    await savePublishActivate(request, 'Wave 14 C20 cleanup');
  }
});

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok(), `Working export failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok(), `Workspace load failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  return await response.json();
}

async function savePublishActivate(request: APIRequestContext, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${projectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save failed: HTTP ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };

  const publish = await request.post(`/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish failed: HTTP ${publish.status()} ${await publish.text()}`).toBeTruthy();

  const activate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate failed: HTTP ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function writeTag(request: APIRequestContext, tagId: string, value: number) {
  const response = await request.post(`/api/tags/${encodeURIComponent(tagId)}/write`, { data: { value } });
  expect(response.status(), `TAG write failed: HTTP ${response.status()} ${await response.text()}`).toBe(202);
}
