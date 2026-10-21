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
    screenOrientation: null,
    backgroundMode: 'image',
    backgroundColor: '#AABBCC',
    backgroundImageAssetId: 'asset-1',
    backgroundImageFit: 'contain'
  });
});

test('surface config treats absent fit as canonical cover without materializing it', () => {
  expect(readVisualDefinitionSurfaceConfig({})).toEqual({
    screenOrientation: null,
    backgroundMode: 'theme',
    backgroundColor: null,
    backgroundImageAssetId: null,
    backgroundImageFit: 'cover'
  });
  expect(readVisualDefinitionSurfaceConfig(undefined)).toEqual({
    screenOrientation: null,
    backgroundMode: 'theme',
    backgroundColor: null,
    backgroundImageAssetId: null,
    backgroundImageFit: 'cover'
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
  expect(readVisualDefinitionSurfaceConfig(screen.properties).backgroundImageFit).toBe('cover');
});

test('screen orientation is explicit, canonical, and can return to Runtime default', () => {
  const portrait = applyVisualDefinitionSurfacePatch({ properties: { retained: 'yes' } }, { screenOrientation: 'portrait' });
  expect(portrait.properties).toEqual({ retained: 'yes', screenOrientation: 'portrait' });
  expect(readVisualDefinitionSurfaceConfig(portrait.properties).screenOrientation).toBe('portrait');
  expect(applyVisualDefinitionSurfacePatch(portrait, { screenOrientation: null }).properties).toEqual({ retained: 'yes' });
  expect(() => readVisualDefinitionSurfaceConfig({ screenOrientation: 'diagonal' }))
    .toThrow(/Unknown visual definition screen orientation/);
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

test('theme background follows the active light or dark application theme', () => {
  const screen = applyVisualDefinitionSurfacePatch({ properties: {} }, { backgroundMode: 'theme' });
  expect(screen.properties?.backgroundMode).toBe('theme');
  expect(resolveVisualDefinitionSurfaceStyle(screen.properties)).toEqual({ backgroundColor: 'var(--app-bg, #0b0f14)' });
});

test('legacy definitions infer their prior authored background mode', () => {
  expect(readVisualDefinitionSurfaceConfig({ backgroundColor: '#223344' }).backgroundMode).toBe('color');
  expect(readVisualDefinitionSurfaceConfig({ backgroundImageAssetId: 'asset-1' }).backgroundMode).toBe('image');
  expect(readVisualDefinitionSurfaceConfig({ backgroundMode: 'theme', backgroundColor: '#223344' }).backgroundMode).toBe('theme');
});

test('surface model rejects non-canonical background colors and unknown fit values', () => {
  expect(() => applyVisualDefinitionSurfacePatch({ properties: {} }, { backgroundColor: 'red' }))
    .toThrow(/canonical hexadecimal color/);
  expect(() => readVisualDefinitionSurfaceConfig({ backgroundImageFit: 'squish' }))
    .toThrow(/Unknown visual definition background fit/);
});
