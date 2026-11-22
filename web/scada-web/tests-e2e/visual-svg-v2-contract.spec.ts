import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';
import {
  normalizeSvgPaintOverrides,
  readSvgPaintMetadata,
  SVG_PAINT_OVERRIDES_PROPERTY
} from '../src/engineering/visual-editor/svgSymbolModel';

test('core.svgSymbol owns normal canvas transforms plus fill stroke and line thickness', () => {
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol);
  for (const key of [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.y,
    VISUAL_PROPERTY_KEYS.width,
    VISUAL_PROPERTY_KEYS.height,
    VISUAL_PROPERTY_KEYS.rotation,
    VISUAL_PROPERTY_KEYS.horizontalFlip,
    VISUAL_PROPERTY_KEYS.verticalFlip,
    VISUAL_PROPERTY_KEYS.opacity,
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.fillColor,
    VISUAL_PROPERTY_KEYS.strokeColor,
    VISUAL_PROPERTY_KEYS.strokeWidth
  ]) {
    expect(schema.declares(key), key).toBe(true);
  }
});

test('SVG palette and semantic slot metadata stay deterministic and instance-local', () => {
  const asset = {
    id: '11111111-1111-1111-1111-111111111111',
    key: 'asset.valve',
    name: 'Valve',
    originalFileName: 'valve.svg',
    mediaType: 'image/svg+xml',
    byteLength: 100,
    sha256: 'a'.repeat(64),
    metadata: {
      'elitescada.svg.palette': '[{"color":"#000000","fill":false,"stroke":true},{"color":"#AABBCC","fill":true,"stroke":false}]',
      'elitescada.svg.slots': '[{"name":"body","fill":true,"stroke":true,"strokeWidth":true}]'
    }
  };
  const metadata = readSvgPaintMetadata(asset);
  expect(metadata.palette.map(entry => entry.color)).toEqual(['#000000', '#AABBCC']);
  expect(metadata.slots).toEqual([{ name: 'body', fill: true, stroke: true, strokeWidth: true }]);

  const overrides = normalizeSvgPaintOverrides({
    version: 1,
    palette: { '#abc': '#123456' },
    slots: { body: { fill: '#654321', stroke: '#111111', strokeWidth: 3.5 } }
  });
  expect(overrides.palette?.['#AABBCC']).toBe('#123456');
  expect(overrides.slots?.body.strokeWidth).toBe(3.5);
  expect(SVG_PAINT_OVERRIDES_PROPERTY).toBe('svgPaintOverrides');
});

test('asset catalog and renderer route SVG through first-class symbol, not core.image', async () => {
  const fs = await import('node:fs/promises');
  const sidebar = await fs.readFile(new URL('../src/engineering/visual-editor/VisualEditorAuthoringSidebar.tsx', import.meta.url), 'utf8');
  const renderer = await fs.readFile(new URL('../src/engineering/visual-editor/CanonicalVisualRenderer.tsx', import.meta.url), 'utf8');
  expect(sidebar).toContain("asset.mediaType === 'image/svg+xml'");
  expect(sidebar).toContain('BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol');
  expect(renderer).toContain('element.type === BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol');
  expect(renderer).toContain('<SvgSymbolVisualElement');
});
