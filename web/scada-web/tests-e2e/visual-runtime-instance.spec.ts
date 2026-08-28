import { test, expect } from '@playwright/test';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  RuntimeVisualInstance,
  RuntimeVisualInstanceError,
  VisualObjectPropertySchema,
  VisualPropertyRegistry,
  type RuntimeVisualDefinitionProjection,
  type VisualPropertyDefinition
} from '../src/visual-runtime';

const engineeringOnly = {
  key: 'engineeringOnly',
  type: 'string',
  defaultValue: 'locked',
  engineeringEditable: true,
  runtimeReadable: false,
  runtimeWritable: false,
  supportsBinding: false,
  animatable: false
} satisfies VisualPropertyDefinition;

const registry = new VisualPropertyRegistry([
  ...COMMON_VISUAL_PROPERTY_REGISTRY.list(),
  engineeringOnly
]);

const schema = new VisualObjectPropertySchema(
  'symbol',
  ['width', 'opacity', 'engineeringOnly', 'assetRef'],
  registry
);

function definition(baseProperties: Readonly<Record<string, unknown>> = {}): RuntimeVisualDefinitionProjection {
  return {
    objectId: 'object-1',
    key: 'pumpSymbol',
    objectType: 'symbol',
    parentObjectId: null,
    propertyKeys: schema.propertyKeys,
    baseProperties: baseProperties as RuntimeVisualDefinitionProjection['baseProperties'],
    bindings: [],
    scriptEventReferences: [],
    metadata: {}
  };
}

function instance(
  runtimeInstanceId: string,
  baseProperties: Readonly<Record<string, unknown>> = {}
): RuntimeVisualInstance {
  return new RuntimeVisualInstance({
    definition: definition(baseProperties),
    schema,
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
    code: 'number.maximum',
    reason: 'number.maximum'
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
    code: 'number.maximum',
    reason: 'number.maximum'
  });
});

test('runtime policy rejects unregistered, malformed, non-readable, non-writable, non-binding and non-animatable access', () => {
  const runtime = instance('runtime-policy', { engineeringOnly: 'base' });

  expect(() => runtime.readRuntimeReadable('engineeringOnly')).toThrow(/not runtime-readable/);
  expect(() => runtime.readPropertyState('engineeringOnly')).toThrow(/not runtime-readable/);
  expect(runtime.readEffective('engineeringOnly')).toEqual({
    propertyKey: 'engineeringOnly',
    value: 'base',
    source: 'engineering'
  });
  expect(() => runtime.setScriptOverride('engineeringOnly', 'script')).toThrow(/not runtime-writable/);
  expect(() => runtime.clearScriptOverride('engineeringOnly')).toThrow(/not runtime-writable/);
  expect(() => runtime.setBindingValue('engineeringOnly', 'binding')).toThrow(/does not support binding/);
  expect(() => runtime.clearBindingValue('engineeringOnly')).toThrow(/does not support binding/);
  expect(() => runtime.setAnimationOverride('engineeringOnly', 'animation')).toThrow(/not animatable/);
  expect(() => runtime.clearAnimationOverride('engineeringOnly')).toThrow(/not animatable/);
  expect(() => runtime.readEffective('unknown')).toThrow(/not registered/);
  expect(() => runtime.readEffective(' width ')).toThrow(/stable exact key/);
});

test('engineering base snapshot and runtime presentation state remain instance-local and immutable', () => {
  const asset = { assetId: 'asset:a' };
  const base = { width: 95, assetRef: asset };
  const first = instance('runtime-a', base);
  const second = instance('runtime-b', base);

  asset.assetId = 'asset:mutated-outside';
  first.setScriptOverride('width', 140);

  expect(first.readEffective('width')).toEqual({ propertyKey: 'width', value: 140, source: 'script' });
  expect(second.readEffective('width')).toEqual({ propertyKey: 'width', value: 95, source: 'engineering' });
  expect(first.engineeringBaseSnapshot).toEqual({ width: 95, assetRef: { assetId: 'asset:a' } });
  expect(second.engineeringBaseSnapshot).toEqual({ width: 95, assetRef: { assetId: 'asset:a' } });
  expect(base.width).toBe(95);
});

test('constructor rejects schema/definition mismatch and malformed explicit runtime identities', () => {
  expect(() => new RuntimeVisualInstance({
    definition: { ...definition(), objectType: 'other' },
    schema,
    runtimeInstanceId: 'runtime-mismatch'
  })).toThrow(/does not match schema/);

  expect(() => new RuntimeVisualInstance({
    definition: { ...definition(), propertyKeys: ['width'] },
    schema,
    runtimeInstanceId: 'runtime-property-mismatch'
  })).toThrow(/property schema does not match/);

  expect(() => new RuntimeVisualInstance({
    definition: definition(),
    schema,
    runtimeInstanceId: ' runtime-with-space '
  })).toThrow(/stable non-empty token/);

  expect(() => new RuntimeVisualInstance({
    definition: definition(),
    schema,
    runtimeInstanceId: 'runtime-valid',
    visualContextInstanceId: '   '
  })).toThrow(/stable non-empty token/);
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
