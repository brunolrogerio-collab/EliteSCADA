import { test, expect } from '@playwright/test';
import {
  RuntimeVisualInstance,
  RuntimeVisualTweenError,
  RuntimeVisualTweenScheduler,
  VisualObjectPropertySchema,
  type RuntimeVisualDefinitionProjection,
  type VisualTweenCompletion,
  type VisualTweenFrameClock
} from '../src/visual-runtime';

class ManualFrameClock implements VisualTweenFrameClock {
  private time = 0;
  private sequence = 0;
  private readonly pending = new Map<number, (timestampMs: number) => void>();

  now(): number { return this.time; }
  requestFrame(callback: (timestampMs: number) => void): number {
    const handle = ++this.sequence;
    this.pending.set(handle, callback);
    return handle;
  }
  cancelFrame(handle: number): void { this.pending.delete(handle); }
  stepTo(timestampMs: number): void {
    this.time = timestampMs;
    const callbacks = [...this.pending.values()];
    this.pending.clear();
    for (const callback of callbacks) callback(timestampMs);
  }
}

const schema = new VisualObjectPropertySchema('symbol', ['x', 'opacity', 'fillColor', 'visible']);

function createInstance(runtimeInstanceId = 'runtime-tween'): RuntimeVisualInstance {
  const definition: RuntimeVisualDefinitionProjection = {
    objectId: 'object-1', key: 'pump', objectType: 'symbol', parentObjectId: null,
    propertyKeys: schema.propertyKeys,
    baseProperties: { x: 0, opacity: 1, fillColor: '#000000', visible: true },
    bindings: [], scriptEventReferences: [], metadata: {}
  };
  return new RuntimeVisualInstance({ definition, schema, runtimeInstanceId });
}

test('numeric tween has deterministic intermediate and stable final script value', () => {
  const clock = new ManualFrameClock();
  const completions: VisualTweenCompletion[] = [];
  const instance = createInstance();
  const scheduler = new RuntimeVisualTweenScheduler(instance, {
    clock, handleFactory: () => 'tween-1', onCompleted: completion => completions.push(completion)
  });

  scheduler.start({ targetReference: 'pump', propertyKey: 'x', targetValue: 100, durationMs: 1000 });
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 0, source: 'animation' });
  clock.stepTo(500);
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 50, source: 'animation' });
  clock.stepTo(1000);
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 100, source: 'script' });
  expect(completions[0].reason).toBe('completed');
});

test('replacement starts from the visible animation value and marks old tween replaced', () => {
  const clock = new ManualFrameClock();
  const completions: VisualTweenCompletion[] = [];
  const instance = createInstance('runtime-replace');
  let handle = 0;
  const scheduler = new RuntimeVisualTweenScheduler(instance, {
    clock, handleFactory: () => `tween-${++handle}`, onCompleted: completion => completions.push(completion)
  });

  scheduler.start({ targetReference: 'object-1', propertyKey: 'x', targetValue: 100, durationMs: 1000 });
  clock.stepTo(400);
  expect(instance.readEffective('x').value).toBe(40);
  scheduler.start({ targetReference: 'pump', propertyKey: 'x', targetValue: 80, durationMs: 600 });
  expect(completions[0].reason).toBe('replaced');
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 40, source: 'animation' });
  clock.stepTo(700);
  expect(instance.readEffective('x').value).toBe(60);
  clock.stepTo(1000);
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 80, source: 'script' });
});

test('cancellation removes animation authority and reveals prior script value', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-cancel');
  instance.setScriptOverride('opacity', 0.8);
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => 'tween-cancel' });
  const accepted = scheduler.start({ targetReference: 'pump', propertyKey: 'opacity', targetValue: 0.2, durationMs: 1000 });
  clock.stepTo(500);
  expect(instance.readEffective('opacity').source).toBe('animation');
  expect(scheduler.cancel(accepted.handle)).toBe(true);
  expect(instance.readEffective('opacity')).toEqual({ propertyKey: 'opacity', value: 0.8, source: 'script' });
});

test('rejectIfActive fails closed without disturbing running tween', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-reject');
  let handle = 0;
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => `tween-${++handle}` });
  scheduler.start({ targetReference: 'pump', propertyKey: 'x', targetValue: 100, durationMs: 1000 });
  clock.stepTo(250);
  expect(() => scheduler.start({
    targetReference: 'pump', propertyKey: 'x', targetValue: 200, durationMs: 1000,
    conflictBehavior: 'rejectIfActive'
  })).toThrow(RuntimeVisualTweenError);
  expect(instance.readEffective('x').value).toBe(25);
});

test('color tween interpolates RGBA deterministically', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-color');
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => 'tween-color' });
  scheduler.start({ targetReference: 'pump', propertyKey: 'fillColor', targetValue: '#FFFFFF80', durationMs: 1000 });
  clock.stepTo(500);
  expect(instance.readEffective('fillColor')).toEqual({ propertyKey: 'fillColor', value: '#808080C0', source: 'animation' });
  clock.stepTo(1000);
  expect(instance.readEffective('fillColor')).toEqual({ propertyKey: 'fillColor', value: '#FFFFFF80', source: 'script' });
});

test('unsupported combinations and unbounded duration fail closed', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-invalid');
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => 'tween-invalid' });
  expect(() => scheduler.start({ targetReference: 'pump', propertyKey: 'visible', targetValue: false, durationMs: 1000 })).toThrow();
  expect(() => scheduler.start({ targetReference: 'pump', propertyKey: 'x', targetValue: 10, durationMs: 60001 })).toThrow(/duration must be/);
  expect(() => scheduler.start({ targetReference: 'other', propertyKey: 'x', targetValue: 10, durationMs: 100 })).toThrow(/outside Runtime Visual Instance/);
});

test('easeIn with ping-pong repeat has explicit final semantics', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-easing');
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => 'tween-easing' });
  scheduler.start({
    targetReference: 'pump', propertyKey: 'x', targetValue: 100, durationMs: 1000,
    easing: 'easeIn', repeatCount: 1, pingPong: true
  });
  clock.stepTo(500);
  expect(instance.readEffective('x').value).toBe(25);
  clock.stepTo(1500);
  expect(instance.readEffective('x').value).toBe(25);
  clock.stepTo(2000);
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 0, source: 'script' });
});

test('instance disposal cancels owned tween without resurrecting runtime state', () => {
  const clock = new ManualFrameClock();
  const instance = createInstance('runtime-dispose');
  const scheduler = new RuntimeVisualTweenScheduler(instance, { clock, handleFactory: () => 'tween-dispose' });
  scheduler.start({ targetReference: 'pump', propertyKey: 'x', targetValue: 100, durationMs: 1000 });
  clock.stepTo(200);
  instance.dispose();
  clock.stepTo(800);
  expect(instance.readEffective('x')).toEqual({ propertyKey: 'x', value: 0, source: 'engineering' });
});
