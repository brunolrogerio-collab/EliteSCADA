import { expect, test } from '@playwright/test';
import {
  applyVisualDefinitionSurfacePatch,
  readVisualDefinitionSurfaceConfig,
  resolveVisualDefinitionSurfaceStyle
} from '../src/engineering/visual-editor/visualDefinitionSurfaceModel';

test('surface patch stores canonical color, asset identity and fit', () => {
  const screen = applyVisualDefinitionSurfacePatch({ properties: {} }, {
    backgroundColor: '#aabbcc',
    backgroundImageAssetId: 'asset-1',
    backgroundImageFit: 'contain'
  });
  expect(screen.properties).toEqual({
    backgroundColor: '#AABBCC',
    backgroundImageAssetId: 'asset-1',
    backgroundImageFit: 'contain'
  });
  expect(readVisualDefinitionSurfaceConfig(screen.properties)).toEqual({
    backgroundColor: '#AABBCC',
    backgroundImageAssetId: 'asset-1',
    backgroundImageFit: 'contain'
  });
});

test('surface patch removes optional background values without sentinel strings', () => {
  const screen = applyVisualDefinitionSurfacePatch({
    properties: { backgroundColor: '#FFFFFF', backgroundImageAssetId: 'asset-1', backgroundImageFit: 'tile' }
  }, {
    backgroundColor: null,
    backgroundImageAssetId: null,
    backgroundImageFit: null
  });
  expect(screen.properties).toEqual({});
});

test('surface style resolves a stable asset URL and deterministic fit', () => {
  const style = resolveVisualDefinitionSurfaceStyle({
    backgroundColor: '#101010',
    backgroundImageAssetId: 'asset-1',
    backgroundImageFit: 'stretch'
  }, assetId => `/api/assets/${assetId}`);
  expect(style).toMatchObject({
    backgroundColor: '#101010',
    backgroundImage: 'url("/api/assets/asset-1")',
    backgroundSize: '100% 100%',
    backgroundRepeat: 'no-repeat',
    backgroundPosition: 'center'
  });
});

test('surface model rejects non-canonical background colors', () => {
  expect(() => applyVisualDefinitionSurfacePatch({ properties: {} }, { backgroundColor: 'red' }))
    .toThrow(/canonical hexadecimal color/);
});
