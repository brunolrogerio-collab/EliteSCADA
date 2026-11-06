import { expect, test } from '@playwright/test';
import * as THREE from 'three';
import {
  clampModel3dPitch, DEFAULT_MODEL3D_PITCH_DEG, DEFAULT_MODEL3D_YAW_DEG,
  frameModel3dCamera, normalizeModel3d, wrapModel3dYaw
} from '../src/engineering/visual-editor/model3dViewGeometry';
import { applyVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import type { ScreenEngineering } from '../src/engineering/types';
import { BUILTIN_VISUAL_OBJECT_TYPES, decodeVisualEngineeringProperties, getBuiltinVisualObjectSchema, VISUAL_PROPERTY_KEYS } from '../src/visual-runtime';

test('DEV-873 canonical 3D view schema validates bounds, defaults and Runtime policy independent of 2D rotation', () => {
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.model3d);
  for (const key of ['viewYawDeg', 'viewPitchDeg', 'runtimeViewInteractionEnabled']) {
    expect(schema.declares(key)).toBe(true);
    expect(schema.getRequired(key).runtimeWritable).toBe(false);
    expect(schema.getRequired(key).supportsBinding).toBe(false);
  }
  expect(schema.getRequired(VISUAL_PROPERTY_KEYS.viewYawDeg).defaultValue).toBe(DEFAULT_MODEL3D_YAW_DEG);
  expect(schema.getRequired(VISUAL_PROPERTY_KEYS.viewPitchDeg).defaultValue).toBe(DEFAULT_MODEL3D_PITCH_DEG);
  expect(schema.getRequired(VISUAL_PROPERTY_KEYS.runtimeViewInteractionEnabled).defaultValue).toBe(false);
  expect(schema.validate('viewYawDeg', -180).ok).toBe(true);
  expect(schema.validate('viewYawDeg', 180).ok).toBe(true);
  expect(schema.validate('viewYawDeg', 181).ok).toBe(false);
  expect(schema.validate('viewPitchDeg', -85).ok).toBe(true);
  expect(schema.validate('viewPitchDeg', 85).ok).toBe(true);
  expect(schema.validate('viewPitchDeg', -85.1).ok).toBe(false);
  expect(schema.validate('runtimeViewInteractionEnabled', 'true').ok).toBe(false);
  expect(schema.getRequired('rotation').defaultValue).toBe(0);
  const values = decodeVisualEngineeringProperties({
    rotation: 33, viewYawDeg: -65, viewPitchDeg: 20, runtimeViewInteractionEnabled: true
  }, schema);
  expect(values).toMatchObject({ rotation: 33, viewYawDeg: -65, viewPitchDeg: 20, runtimeViewInteractionEnabled: true });
});

test('DEV-873 scales off-origin glTF contents around their actual bounding box center', () => {
  const input = new THREE.Group();
  const translated = new THREE.Mesh(new THREE.BoxGeometry(2, 4, 8), new THREE.MeshBasicMaterial());
  translated.position.set(110, -35, 29);
  input.add(translated);
  input.position.set(10, 20, -5);
  const original = new THREE.Box3().setFromObject(input);
  expect(original.getCenter(new THREE.Vector3()).length()).toBeGreaterThan(30);
  const { object, bounds } = normalizeModel3d(input);
  const center = bounds.getCenter(new THREE.Vector3());
  expect(center.length()).toBeLessThan(1e-5);
  const size = bounds.getSize(new THREE.Vector3());
  expect(Math.max(size.x, size.y, size.z)).toBeCloseTo(2.2, 5);
  expect(object.children[0].children[0]).toBe(input);
  translated.geometry.dispose();
  (translated.material as THREE.Material).dispose();
});

test('DEV-873 repeat resize and various camera angles fit all off-origin bounding box corners', () => {
  const offOrigin = new THREE.Mesh(new THREE.BoxGeometry(1, 5, 18), new THREE.MeshBasicMaterial());
  offOrigin.position.set(-150, 80, 41);
  const { object, bounds } = normalizeModel3d(offOrigin);
  expect(bounds.getCenter(new THREE.Vector3()).length()).toBeLessThan(1e-5);
  const camera = new THREE.PerspectiveCamera(38, 1, 0.01, 1000);
  const corners: THREE.Vector3[] = [];
  const b = bounds;
  for (const x of [b.min.x, b.max.x])
    for (const y of [b.min.y, b.max.y])
      for (const z of [b.min.z, b.max.z]) corners.push(new THREE.Vector3(x, y, z));
  for (const [width, height] of [[120, 700], [1200, 150], [300, 300], [70, 470], [1200, 300], [300, 300]] as const) {
    for (const [yaw, pitch] of [[40.1, 25.8], [180, 85], [-180, -85], [95, -20]] as const) {
      frameModel3dCamera(camera, bounds, width, height, yaw, pitch);
      expect(camera.aspect).toBeCloseTo(width / height, 10);
      expect(camera.near).toBeGreaterThan(0);
      for (const corner of corners) {
        const projected = corner.clone().project(camera);
        expect(Math.abs(projected.x)).toBeLessThan(0.995);
        expect(Math.abs(projected.y)).toBeLessThan(0.995);
        expect(projected.z).toBeGreaterThan(-1);
        expect(projected.z).toBeLessThan(1);
      }
    }
  }
  expect(wrapModel3dYaw(230)).toBe(-130);
  expect(clampModel3dPitch(120)).toBe(85);
  expect(clampModel3dPitch(-90)).toBe(-85);
  offOrigin.geometry.dispose();
  (offOrigin.material as THREE.Material).dispose();
  object.clear();
});

test('DEV-873 authored 3D view passes canonical property mutations and JSON reopening without modifying 2D rotation', () => {
  const initial: ScreenEngineering = {
    id: 'screen-model-873', key: 'model-screen', name: 'Model screen',
    elements: [{ id: 'model-873', key: 'tank-3d', type: 'core.model3d', properties: { rotation: 18, width: 240, height: 180 } }]
  };
  let working = initial;
  for (const [propertyKey, value] of [['viewYawDeg', -125], ['viewPitchDeg', 52], ['runtimeViewInteractionEnabled', true]] as const) {
    working = applyVisualEditorMutationIntent(working, {
      kind: 'property.set', objectIds: ['model-873'], propertyKey, value
    });
  }
  const reopening = JSON.parse(JSON.stringify(working)) as ScreenEngineering;
  const props = reopening.elements![0].properties!;
  expect(props).toMatchObject({ rotation: 18, width: 240, height: 180, viewYawDeg: -125, viewPitchDeg: 52, runtimeViewInteractionEnabled: true });
  const schema = getBuiltinVisualObjectSchema('core.model3d');
  expect(decodeVisualEngineeringProperties(props, schema)).toMatchObject({ rotation: 18, viewYawDeg: -125, viewPitchDeg: 52 });
  expect(initial.elements![0].properties).not.toHaveProperty('viewYawDeg');
});

test('DEV-873 canvas edit mode isolates orbit from move and persists the initial 3D view', async ({ page }) => {
  await page.goto('/tests-e2e/model3d-view-harness.html?mode=editor');
  await expect(page.getByTestId('canvas-model3d-view-toggle')).toBeVisible();
  await expect(page.getByTestId('canvas-model3d-view-toggle')).toHaveAttribute('aria-pressed', 'false');
  await page.getByTestId('canvas-model3d-view-toggle').click();
  await expect(page.getByTestId('canvas-model3d-view-toggle')).toHaveAttribute('aria-pressed', 'true');
  const box = await page.locator('[data-canvas-object-id="model-873"]').boundingBox();
  expect(box).not.toBeNull();
  const x = box!.x + box!.width / 2, y = box!.y + box!.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 80, y - 40, { steps: 5 });
  await page.mouse.up();
  const props = JSON.parse(await page.getByTestId('model3d-draft').innerText());
  expect(props.viewYawDeg).toBeGreaterThan(60);
  expect(props.viewPitchDeg).toBeGreaterThan(35);
  expect(props.x).toBe(40);
  expect(props.y).toBe(50);
  expect(props.rotation).toBe(18);
  await page.locator('.visual-editor-canvas__surface').press('Escape');
  await expect(page.getByTestId('canvas-model3d-view-toggle')).toHaveAttribute('aria-pressed', 'false');
  await page.getByTestId('model3d-reset-view').click();
  const restored = JSON.parse(await page.getByTestId('model3d-draft').innerText());
  expect(restored.viewYawDeg).toBeUndefined();
  expect(restored.viewPitchDeg).toBeUndefined();
});

test('DEV-873 Runtime orbit is temporary, gated and resets to the published angles on reopening', async ({ page }) => {
  await page.goto('/tests-e2e/model3d-view-harness.html?mode=runtime&allow=true');
  const viewer = page.locator('.visual-editor-object-model3d');
  await expect(viewer).toHaveAttribute('data-model3d-runtime-interactive', 'true');
  await expect(viewer).toHaveAttribute('data-model3d-ready', 'true', { timeout: 15000 });
  await expect(viewer).toHaveAttribute('data-model3d-view-yaw', '40.1');
  const box = await viewer.boundingBox();
  expect(box).not.toBeNull();
  await page.mouse.move(box!.x + 50, box!.y + 50);
  await page.mouse.down();
  await page.mouse.move(box!.x + 170, box!.y + 20, { steps: 5 });
  await page.mouse.up();
  const yaw = Number(await viewer.getAttribute('data-model3d-view-yaw'));
  expect(yaw).toBeGreaterThan(70);
  await expect(page.getByTestId('runtime-command-count')).toHaveText('0');
  await page.getByTestId('runtime-reopen').click();
  await expect(viewer).toHaveAttribute('data-model3d-ready', 'true', { timeout: 15000 });
  await expect(viewer).toHaveAttribute('data-model3d-view-yaw', '40.1');
  await page.getByTestId('runtime-narrow').click();
  await expect(viewer).toHaveAttribute('data-model3d-frame-width', '100');
  await page.getByTestId('runtime-wide').click();
  await expect(viewer).toHaveAttribute('data-model3d-frame-width', '600');
  await page.goto('/tests-e2e/model3d-view-harness.html?mode=runtime&allow=false');
  const passive = page.locator('.visual-editor-object-model3d');
  await expect(passive).toHaveAttribute('data-model3d-runtime-interactive', 'false');
  await expect(passive).toHaveAttribute('data-model3d-ready', 'true', { timeout: 15000 });
  const pbox = await passive.boundingBox();
  await page.mouse.move(pbox!.x + 30, pbox!.y + 30);
  await page.mouse.down();
  await page.mouse.move(pbox!.x + 160, pbox!.y + 50);
  await page.mouse.up();
  await expect(passive).toHaveAttribute('data-model3d-view-yaw', '40.1');
});
