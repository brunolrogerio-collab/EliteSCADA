import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const runtimeRendererSource = readFileSync(
  join(process.cwd(), 'src/runtime/visual-navigation/RuntimeVisualDefinitionRenderer.tsx'),
  'utf8'
);

const runtimeProjectionSource = readFileSync(
  join(process.cwd(), 'src/runtime/visual-navigation/runtimeDynamoVisualProjection.ts'),
  'utf8'
);

test('Runtime anchors each semantic Dynamo state indicator to the rendered public instance root', () => {
  expect(runtimeRendererSource).toContain("root.querySelectorAll<HTMLElement>('[data-object-id]')");
  expect(runtimeRendererSource).toContain('const host = hosts.get(indicator.objectId)');
  expect(runtimeRendererSource).toContain('createPortal(<span');
});

test('Dynamo state projection does not maintain a second x/y geometry model', () => {
  expect(runtimeProjectionSource).not.toContain('parentScaleX');
  expect(runtimeProjectionSource).not.toContain('parentScaleY');
  expect(runtimeProjectionSource).not.toContain('x: x +');
  expect(runtimeProjectionSource).not.toContain('y: y +');
});

test('semantic state remains textual and not color-only', () => {
  expect(runtimeRendererSource).toContain('>{indicator.label}</span>');
  expect(runtimeRendererSource).toContain('role="status"');
  expect(runtimeRendererSource).toContain('data-dynamo-state={indicator.state}');
});
