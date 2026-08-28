import { expect, test } from '@playwright/test';

test('integrated runtime resolves visual layers deterministically without mutating Engineering', async ({ page }) => {
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const visual = await importModule('/src/visual-runtime/index.ts');

    if (typeof visual.RuntimeVisualInstance !== 'function') {
      throw new Error('RuntimeVisualInstance is not exported by the integrated Wave 07 public surface.');
    }

    const schema = new visual.VisualObjectPropertySchema(
      'wave07.acceptance',
      ['x', 'visible', 'opacity', 'assetRef']
    );
    const definition = visual.projectVisualEngineeringDefinition({
      objectId: 'object:pump-01',
      key: 'pump01',
      objectType: 'wave07.acceptance',
      baseProperties: {
        x: 10,
        visible: true,
        opacity: 0.8,
        assetRef: { assetId: 'asset:pump-image' }
      }
    }, schema);

    const instance = new visual.RuntimeVisualInstance({
      runtimeInstanceId: 'visual-runtime-a',
      definition,
      schema
    });

    const snapshots: Array<{ value: unknown; source: string }> = [];
    snapshots.push(instance.readPropertyState('x'));

    instance.setBindingValue('x', 20);
    snapshots.push(instance.readPropertyState('x'));

    instance.setScriptOverride('x', 30);
    snapshots.push(instance.readPropertyState('x'));

    instance.setAnimationOverride('x', 40);
    snapshots.push(instance.readPropertyState('x'));

    instance.clearAnimationOverride('x');
    snapshots.push(instance.readPropertyState('x'));

    instance.clearScriptOverride('x');
    snapshots.push(instance.readPropertyState('x'));

    instance.clearBindingValue('x');
    snapshots.push(instance.readPropertyState('x'));

    return {
      snapshots,
      engineeringXAfterRuntimeWrites: definition.baseProperties.x,
      objectId: instance.objectId,
      runtimeInstanceId: instance.runtimeInstanceId
    };
  });

  expect(result.snapshots).toEqual([
    { value: 10, source: 'engineering' },
    { value: 20, source: 'binding' },
    { value: 30, source: 'script' },
    { value: 40, source: 'animation' },
    { value: 30, source: 'script' },
    { value: 20, source: 'binding' },
    { value: 10, source: 'engineering' }
  ]);
  expect(result.engineeringXAfterRuntimeWrites).toBe(10);
  expect(result.objectId).toBe('object:pump-01');
  expect(result.runtimeInstanceId).toBe('visual-runtime-a');
});

test('visual runtime instances remain isolated and disposed instances fail closed', async ({ page }) => {
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const visual = await importModule('/src/visual-runtime/index.ts');

    if (typeof visual.RuntimeVisualInstance !== 'function') {
      throw new Error('RuntimeVisualInstance is not exported by the integrated Wave 07 public surface.');
    }

    const schema = new visual.VisualObjectPropertySchema('wave07.isolation', ['x', 'visible']);
    const definition = visual.projectVisualEngineeringDefinition({
      objectId: 'object:shared-definition',
      key: 'sharedObject',
      objectType: 'wave07.isolation',
      baseProperties: { x: 5, visible: true }
    }, schema);

    const first = new visual.RuntimeVisualInstance({
      runtimeInstanceId: 'visual-instance-1',
      definition,
      schema
    });
    const second = new visual.RuntimeVisualInstance({
      runtimeInstanceId: 'visual-instance-2',
      definition,
      schema
    });

    first.setScriptOverride('x', 101);
    second.setScriptOverride('x', 202);
    const beforeDispose = {
      first: first.readPropertyState('x'),
      second: second.readPropertyState('x'),
      engineering: definition.baseProperties.x
    };

    first.dispose();
    let disposedWriteError = '';
    try {
      first.setScriptOverride('x', 303);
    } catch (error) {
      disposedWriteError = error instanceof Error ? error.message : String(error);
    }

    return {
      beforeDispose,
      firstDisposed: first.isDisposed,
      disposedWriteError,
      secondAfterFirstDispose: second.readPropertyState('x'),
      engineeringAfterDispose: definition.baseProperties.x
    };
  });

  expect(result.beforeDispose).toEqual({
    first: { value: 101, source: 'script' },
    second: { value: 202, source: 'script' },
    engineering: 5
  });
  expect(result.firstDisposed).toBe(true);
  expect(result.disposedWriteError.length).toBeGreaterThan(0);
  expect(result.secondAfterFirstDispose).toEqual({ value: 202, source: 'script' });
  expect(result.engineeringAfterDispose).toBe(5);
});

test('Python visual capability adapter denies unregistered, unreadable, unwritable and wrong-instance access', async ({ page }) => {
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const visual = await importModule('/src/visual-runtime/index.ts');
    const capabilities = await importModule('/src/python-runtime/clientVisualPythonCapabilities.ts');

    if (typeof visual.RuntimeVisualInstance !== 'function') {
      throw new Error('RuntimeVisualInstance is not exported by the integrated Wave 07 public surface.');
    }

    const customRegistry = new visual.VisualPropertyRegistry([
      ...visual.COMMON_VISUAL_PROPERTY_REGISTRY.list(),
      {
        key: 'internalDebug',
        type: 'string',
        defaultValue: 'hidden',
        engineeringEditable: true,
        runtimeReadable: false,
        runtimeWritable: false,
        supportsBinding: false,
        animatable: false
      }
    ]);
    const schema = new visual.VisualObjectPropertySchema(
      'wave07.python',
      ['x', 'visible', 'assetRef', 'internalDebug'],
      customRegistry
    );
    const definition = visual.projectVisualEngineeringDefinition({
      objectId: 'object:python-target',
      key: 'pythonTarget',
      objectType: 'wave07.python',
      baseProperties: {
        x: 12,
        visible: true,
        assetRef: { assetId: 'asset:python-target' },
        internalDebug: 'not-for-python'
      }
    }, schema);
    const instance = new visual.RuntimeVisualInstance({
      runtimeInstanceId: 'visual-python-instance',
      definition,
      schema
    });

    const provider = {
      readVisualProperty(targetReference: string, propertyKey: string, context: any) {
        assertTarget(targetReference, context);
        const propertyDefinition = schema.getRequired(propertyKey);
        if (!propertyDefinition.runtimeReadable) throw new Error('Visual property is not runtime-readable.');
        return instance.readPropertyState(propertyKey);
      },
      writeVisualProperty(targetReference: string, propertyKey: string, value: unknown, context: any) {
        assertTarget(targetReference, context);
        const propertyDefinition = schema.getRequired(propertyKey);
        if (!propertyDefinition.runtimeWritable) throw new Error('Visual property is not runtime-writable.');
        instance.setScriptOverride(propertyKey, value);
        return instance.readPropertyState(propertyKey);
      }
    };

    function assertTarget(targetReference: string, context: any) {
      if (context.visualRuntimeInstanceId !== instance.runtimeInstanceId) {
        throw new Error('Visual Runtime Instance identity mismatch.');
      }
      if (targetReference !== definition.objectId && targetReference !== definition.key) {
        throw new Error('Visual target is outside the current visual context.');
      }
    }

    const context = {
      scriptId: 'script:wave07-python',
      runtimeInstanceId: 'script-runtime-wave07',
      visualRuntimeInstanceId: 'visual-python-instance',
      executionId: 'execution-wave07'
    };

    const readable = await capabilities.dispatchClientVisualPythonCapability(
      provider,
      'visualProperty.read',
      'read',
      { targetReference: 'object:python-target', propertyKey: 'x' },
      context
    );
    const writable = await capabilities.dispatchClientVisualPythonCapability(
      provider,
      'visualProperty.write',
      'write',
      { targetReference: 'pythonTarget', propertyKey: 'x', value: 77 },
      context
    );

    const denied: Record<string, string> = {};
    for (const [name, capability, operation, argumentsValue, candidateContext] of [
      [
        'unregistered',
        'visualProperty.read',
        'read',
        { targetReference: 'pythonTarget', propertyKey: 'doesNotExist' },
        context
      ],
      [
        'unreadable',
        'visualProperty.read',
        'read',
        { targetReference: 'pythonTarget', propertyKey: 'internalDebug' },
        context
      ],
      [
        'unwritable',
        'visualProperty.write',
        'write',
        { targetReference: 'pythonTarget', propertyKey: 'assetRef', value: { assetId: 'asset:other' } },
        context
      ],
      [
        'wrongInstance',
        'visualProperty.write',
        'write',
        { targetReference: 'pythonTarget', propertyKey: 'x', value: 88 },
        { ...context, visualRuntimeInstanceId: 'visual-other-instance' }
      ],
      [
        'outsideContext',
        'visualProperty.read',
        'read',
        { targetReference: 'object:other', propertyKey: 'x' },
        context
      ]
    ] as const) {
      try {
        await capabilities.dispatchClientVisualPythonCapability(
          provider,
          capability,
          operation,
          argumentsValue,
          candidateContext
        );
        denied[name] = 'unexpected-success';
      } catch (error) {
        denied[name] = error instanceof Error ? error.message : String(error);
      }
    }

    return {
      readable,
      writable,
      denied,
      engineeringX: definition.baseProperties.x,
      effectiveX: instance.readPropertyState('x')
    };
  });

  expect(result.readable).toEqual({ value: 12, source: 'engineering' });
  expect(result.writable).toEqual({ value: 77, source: 'script' });
  for (const [name, message] of Object.entries(result.denied)) {
    expect(message, `${name} must fail closed`).not.toBe('unexpected-success');
    expect(message.length).toBeGreaterThan(0);
  }
  expect(result.engineeringX).toBe(12);
  expect(result.effectiveX).toEqual({ value: 77, source: 'script' });
});
