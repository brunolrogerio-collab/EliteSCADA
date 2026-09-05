import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const runtimeNavigatorSource = readFileSync(
  join(process.cwd(), 'src/runtime/visual-navigation/RuntimeVisualNavigator.tsx'),
  'utf8'
);

test('Runtime projects canonical Screen surface properties using the runtime asset resolver', () => {
  expect(runtimeNavigatorSource).toContain("resolveVisualDefinitionSurfaceStyle(activeScreen.properties, visualAssetUrl)");
});

test('Runtime projects canonical Popup surface properties using the same resolver', () => {
  expect(runtimeNavigatorSource).toContain("resolveVisualDefinitionSurfaceStyle(popup.properties, visualAssetUrl)");
});

test('Runtime surface projection does not introduce an independent background asset URL contract', () => {
  expect(runtimeNavigatorSource).not.toContain('backgroundImageUrl');
  expect(runtimeNavigatorSource).not.toContain('popupBackgroundUrl');
  expect(runtimeNavigatorSource).not.toContain('screenBackgroundUrl');
});
