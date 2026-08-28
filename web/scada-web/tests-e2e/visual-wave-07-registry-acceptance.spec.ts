import { expect, test } from '@playwright/test';

test('integrated visual registry denies unregistered properties and arbitrary asset locations', async ({ page }) => {
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const visual = await importModule('/src/visual-runtime/index.ts');
    const registry = visual.COMMON_VISUAL_PROPERTY_REGISTRY;
    if (!registry) throw new Error('COMMON_VISUAL_PROPERTY_REGISTRY is not exported by the Wave 07 public visual runtime surface.');

    const assetDefinition = registry.getRequired('assetRef');
    const missing = registry.validate('notRegistered', 123);
    const validAsset = registry.validate('assetRef', {
      assetId: 'asset:project-logo',
      name: 'Project logo',
      mediaType: 'image/png'
    });

    const invalidAssets = [
      registry.validate('assetRef', { assetId: 'https://evil.example/logo.png' }),
      registry.validate('assetRef', { assetId: 'file:///C:/temp/logo.png' }),
      registry.validate('assetRef', { assetId: 'C:\\temp\\logo.png' }),
      registry.validate('assetRef', { assetId: '../logo.png' }),
      registry.validate('assetRef', { assetId: 'asset:logo', url: 'https://evil.example/logo.png' }),
      registry.validate('assetRef', { assetId: 'asset:logo', path: 'C:\\temp\\logo.png' })
    ];

    return {
      missing,
      validAsset,
      invalidAssets,
      assetDefinition: {
        key: assetDefinition.key,
        type: assetDefinition.type,
        runtimeReadable: assetDefinition.runtimeReadable,
        runtimeWritable: assetDefinition.runtimeWritable,
        supportsBinding: assetDefinition.supportsBinding,
        animatable: assetDefinition.animatable
      }
    };
  });

  expect(result.missing).toMatchObject({
    ok: false,
    propertyKey: 'notRegistered',
    code: 'property.unregistered'
  });
  expect(result.validAsset).toMatchObject({
    ok: true,
    propertyKey: 'assetRef',
    value: { assetId: 'asset:project-logo' }
  });
  for (const invalid of result.invalidAssets) {
    expect(invalid).toMatchObject({
      ok: false,
      propertyKey: 'assetRef',
      code: 'assetRef.shape'
    });
  }

  expect(result.assetDefinition).toEqual({
    key: 'assetRef',
    type: 'assetRef',
    runtimeReadable: true,
    runtimeWritable: false,
    supportsBinding: false,
    animatable: false
  });
});

test('integrated registry keeps visual value types explicit and rejects invalid layer candidates', async ({ page }) => {
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const visual = await importModule('/src/visual-runtime/index.ts');
    const registry = visual.COMMON_VISUAL_PROPERTY_REGISTRY;
    if (!registry) throw new Error('COMMON_VISUAL_PROPERTY_REGISTRY is not exported by the Wave 07 public visual runtime surface.');

    return {
      keys: registry.list().map((definition: any) => definition.key),
      opacityBelow: registry.validate('opacity', -0.01),
      opacityAbove: registry.validate('opacity', 1.01),
      widthNan: registry.validate('width', Number.NaN),
      widthInfinity: registry.validate('width', Number.POSITIVE_INFINITY),
      badColor: registry.validate('fillColor', 'red'),
      badEnum: registry.validate('imageFit', 'remote-url'),
      goodEnum: registry.validate('imageFit', 'contain')
    };
  });

  expect(result.keys).toEqual(expect.arrayContaining([
    'x', 'y', 'width', 'height', 'rotation', 'scaleX', 'scaleY', 'zIndex',
    'visible', 'opacity', 'fillColor', 'strokeColor', 'strokeWidth', 'cornerRadius',
    'text', 'textColor', 'fontSize', 'assetRef', 'imageFit'
  ]));
  expect(result.opacityBelow).toMatchObject({ ok: false, code: 'number.minimum' });
  expect(result.opacityAbove).toMatchObject({ ok: false, code: 'number.maximum' });
  expect(result.widthNan).toMatchObject({ ok: false, code: 'number.nonFinite' });
  expect(result.widthInfinity).toMatchObject({ ok: false, code: 'number.nonFinite' });
  expect(result.badColor).toMatchObject({ ok: false, code: 'color.format' });
  expect(result.badEnum).toMatchObject({ ok: false, code: 'enum.value' });
  expect(result.goodEnum).toMatchObject({ ok: true, value: 'contain' });
});
