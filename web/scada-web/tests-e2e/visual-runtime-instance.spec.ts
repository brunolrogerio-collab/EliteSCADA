import { test, expect } from '@playwright/test';
import {
  RuntimeVisualInstance,
  RuntimeVisualInstanceError,
  type RuntimeVisualDefinitionProjection
} from '../src/visual-runtime/runtimeVisualInstance';
import type {
  VisualRuntimePropertyDefinitionPort,
  VisualRuntimePropertyRegistryPort,
  VisualRuntimePropertyValidation
} from '../src/visual-runtime/runtimeVisualPropertyPort';

const definitions: VisualRuntimePropertyDefinitionPort[] = [
  {
    key: 'width',
    defaultValue: 80,
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: true,
    animatable: true
  },
  {
    key: 'opacity',
    defaultValue: 1,
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: true,
    animatable: true
  },
  {
    key: 'engineeringOnly',
    defaultValue: 'locked',
    runtimeReadable: false,
    runtimeWritable: false,
    supportsBinding: false,
    animatable: false
  },
  {
    key: 'assetRef',
    defaultValue: { assetId: 'default-asset' },
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: false,
    animatable: false
  }
];

const registry: VisualRuntimePropertyRegistryPort = {
  find(propertyKey) {
    return definitions.find(item => item.key === propertyKey);
  },
  validate(propertyKey, value): VisualRuntimePropertyValidation {
    if (!definitions.some(item => item.key === propertyKey)) {
      return { valid: false, code: 'TEST_UNREGISTERED', reason: 'Property is not registered.' };
    }
    if (propertyKey === 'width') {
      return typeof value === 'number' && Number.isFinite(value) && value >= 0
        ? { valid: true }
        : { valid: false, code: 'TEST_WIDTH_INVALID', reason: 'Width must be a finite non-negative number.' };
    }
    if (propertyKey === 'opacity') {
      return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1
        ? { valid: true }
        : { valid: false, code: 'TEST_OPACITY_INVALID', reason: 'Opacity must be between 0 and 1.' };
    }
    if (propertyKey === 'engineeringOnly') {
      return typeof value === 'string'
        ? { valid: true }
        : { valid: false, code: 'TEST_STRING_INVALID', reason: 'Value must be a string.' };
    }
    const candidate = value as { assetId?: unknown } | null;
    return candidate !== null && typeof candidate === 'object' && typeof candidate.assetId === 'string'
      ? { valid: true }
      : { valid: false, code: 'TEST_ASSET_INVALID', reason: 'Asset reference requires a stable assetId.' };
  }
};

function definition(baseProperties: Readonly<Record<string, unknown>> = {}): RuntimeVisualDefinitionProjection {
  return {
    objectId: 'object-1',
    key: 'pumpSymbol',
    objectType: 'symbol',
    baseProperties
  };
}

function instance(
  runtimeInstanceId: string,
  baseProperties: Readonly<Record<string, unknown>> = {}
): RuntimeVisualInstance {
  return new RuntimeVisualInstance({
    definition: definition(baseProperties),
    registry,
    runtimeInstanceId,
    visualContextInstanceId: 'screen-instance-1'
  });
}

test('effective property precedence is animation > script > binding > engineering > default', () => {
  const runtime = instance('runtime-precedence', { width: 100 });

  expect(runtime.readEffective('width')).toEqual({ propertyKey: 'width', value: 100, source: 'engineering' });
  runtime.setBindingValue('width', 110);
  expect(runtime.readEffective('width').source).toBe('binding');
  runtime.setScriptOverride('width', 120);
  expect(runtime.readEffective('width').source).toBe('script');
  runtime.setAnimationOverride('width', 130);
  expect(runtime.readEffective('width')).toEqual({ propertyKey: 'width', value: 130, source: 'animation' });

  runtime.clearAnimationOverride('width');
  expect(runtime.readEffective('width').source).toBe('script');
  runtime.clearScriptOverride('width');
  expect(runtime.readEffective('width').source).toBe('binding');
  runtime.clearBindingValue('width');
  expect(runtime.readEffective('width').source).toBe('engineering');

  expect(runtime.readEffective('opacity')).toEqual({ propertyKey: 'opacity', value: 1, source: 'default' });
});

test('invalid layer values fail closed and keep the lower authoritative value', () => {
  const runtime = instance('runtime-invalid', { opacity: 0.75 });

  expect(() => runtime.setBindingValue('opacity', 2)).toThrow(RuntimeVisualInstanceError);
  expect(runtime.readEffective('opacity')).toEqual({ propertyKey: 'opacity', value: 0.75, source: 'engineering' });

  const diagnostic = runtime.getPropertyDiagnostic('opacity');
  expect(diagnostic.validationFailures).toContainEqual({
    propertyKey: 'opacity',
    layer: 'binding',
    code: 'TEST_OPACITY_INVALID',
    reason: 'Opacity must be between 0 and 1.'
  });

  runtime.setBindingValue('opacity', 0.6);
  expect(runtime.getPropertyDiagnostic('opacity').validationFailures).toEqual([]);
});

test('invalid Engineering base value falls through to registry default with source diagnostics', () => {
  const runtime = instance('runtime-invalid-engineering', { opacity: 2 });

  expect(runtime.readEffective('opacity')).toEqual({ propertyKey: 'opacity', value: 1, source: 'default' });
  expect(runtime.getPropertyDiagnostic('opacity').validationFailures).toContainEqual({
    propertyKey: 'opacity',
    layer: 'engineering',
    code: 'TEST_OPACITY_INVALID',
    reason: 'Opacity must be between 0 and 1.'
  });
});

test('runtime policy rejects unregistered, non-readable, non-writable, non-binding and non-animatable access', () => {
  const runtime = instance('runtime-policy', { engineeringOnly: 'base' });

  expect(() => runtime.readRuntimeReadable('engineeringOnly')).toThrow(/not runtime-readable/);
  expect(() => runtime.setScriptOverride('engineeringOnly', 'script')).toThrow(/not runtime-writable/);
  expect(() => runtime.clearScriptOverride('engineeringOnly')).toThrow(/not runtime-writable/);
  expect(() => runtime.setBindingValue('engineeringOnly', 'binding')).toThrow(/does not support binding/);
  expect(() => runtime.clearBindingValue('engineeringOnly')).toThrow(/does not support binding/);
  expect(() => runtime.setAnimationOverride('engineeringOnly', 'animation')).toThrow(/not animatable/);
  expect(() => runtime.clearAnimationOverride('engineeringOnly')).toThrow(/not animatable/);
  expect(() => runtime.readEffective('unknown')).toThrow(/not registered/);
});

test('engineering base snapshot and runtime presentation state remain instance-local and immutable', () => {
  const asset = { assetId: 'asset-a' };
  const base = { width: 95, assetRef: asset };
  const first = instance('runtime-a', base);
  const second = instance('runtime-b', base);

  asset.assetId = 'mutated-outside';
  first.setScriptOverride('width', 140);

  expect(first.readEffective('width')).toEqual({ propertyKey: 'width', value: 140, source: 'script' });
  expect(second.readEffective('width')).toEqual({ propertyKey: 'width', value: 95, source: 'engineering' });
  expect(first.engineeringBaseSnapshot).toEqual({ width: 95, assetRef: { assetId: 'asset-a' } });
  expect(second.engineeringBaseSnapshot).toEqual({ width: 95, assetRef: { assetId: 'asset-a' } });
  expect(base.width).toBe(95);
});

test('dispose clears runtime layers, runs owned cleanup once and prevents further writes', () => {
  const runtime = instance('runtime-dispose', { width: 100 });
  let cleanupCount = 0;
  runtime.registerDisposer(() => { cleanupCount++; });
  runtime.setBindingValue('width', 110);
  runtime.setScriptOverride('width', 120);
  runtime.setAnimationOverride('width', 130);

  runtime.dispose();
  runtime.dispose();

  expect(cleanupCount).toBe(1);
  expect(runtime.isDisposed).toBe(true);
  expect(runtime.readEffective('width')).toEqual({ propertyKey: 'width', value: 100, source: 'engineering' });
  expect(runtime.getPropertyDiagnostic('width').disposed).toBe(true);
  expect(() => runtime.setScriptOverride('width', 150)).toThrow(/is disposed/);
  expect(() => runtime.setBindingValue('width', 150)).toThrow(/is disposed/);
  expect(() => runtime.setAnimationOverride('width', 150)).toThrow(/is disposed/);
  expect(() => runtime.registerDisposer(() => undefined)).toThrow(/is disposed/);
});
