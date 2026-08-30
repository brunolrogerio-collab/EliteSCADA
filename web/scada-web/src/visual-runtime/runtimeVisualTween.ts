import { RuntimeVisualInstance, RuntimeVisualInstanceError } from './runtimeVisualInstance';

export const VISUAL_TWEEN_POLICY = Object.freeze({
  maxDurationMs: 60_000,
  maxRepeatCount: 100,
  maxTotalDurationMs: 300_000
});

export type VisualTweenEasing = 'linear' | 'easeIn' | 'easeOut' | 'easeInOut';
export type VisualTweenConflictBehavior = 'replaceExisting' | 'rejectIfActive';
export type VisualTweenCompletionReason = 'completed' | 'cancelled' | 'replaced' | 'faulted';

export type VisualTweenRequest = Readonly<{
  targetReference: string;
  propertyKey: string;
  targetValue: unknown;
  durationMs: number;
  easing?: VisualTweenEasing;
  repeatCount?: number;
  pingPong?: boolean;
  conflictBehavior?: VisualTweenConflictBehavior;
}>;

export type VisualTweenAccepted = Readonly<{
  accepted: true;
  handle: string;
  propertyKey: string;
  visualRuntimeInstanceId: string;
}>;

export type VisualTweenCompletion = Readonly<{
  handle: string;
  propertyKey: string;
  visualRuntimeInstanceId: string;
  reason: VisualTweenCompletionReason;
  completedAtMs: number;
  diagnostic?: string;
}>;

export class RuntimeVisualTweenError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly propertyKey?: string
  ) {
    super(message);
    this.name = 'RuntimeVisualTweenError';
  }
}

export interface VisualTweenFrameClock {
  now(): number;
  requestFrame(callback: (timestampMs: number) => void): number;
  cancelFrame(handle: number): void;
}

export type RuntimeVisualTweenSchedulerOptions = Readonly<{
  clock?: VisualTweenFrameClock;
  handleFactory?: () => string;
  onCompleted?: (completion: VisualTweenCompletion) => void;
}>;

type ActiveTween = {
  handle: string;
  propertyKey: string;
  startValue: number | string;
  targetValue: number | string;
  durationMs: number;
  easing: VisualTweenEasing;
  repeatCount: number;
  pingPong: boolean;
  startedAtMs: number;
  frameHandle?: number;
  unregisterDisposer?: () => void;
};

export class RuntimeVisualTweenScheduler {
  private readonly clock: VisualTweenFrameClock;
  private readonly handleFactory: () => string;
  private readonly onCompleted?: (completion: VisualTweenCompletion) => void;
  private readonly activeByProperty = new Map<string, ActiveTween>();
  private readonly activeByHandle = new Map<string, ActiveTween>();

  constructor(
    private readonly instance: RuntimeVisualInstance,
    options: RuntimeVisualTweenSchedulerOptions = {}
  ) {
    this.clock = options.clock ?? browserFrameClock();
    this.handleFactory = options.handleFactory ?? createTweenHandle;
    this.onCompleted = options.onCompleted;
  }

  start(request: VisualTweenRequest): VisualTweenAccepted {
    const normalized = validateRequest(request);
    assertTarget(this.instance, normalized.targetReference);
    const active = this.activeByProperty.get(normalized.propertyKey);

    if (active && normalized.conflictBehavior === 'rejectIfActive') {
      throw new RuntimeVisualTweenError(
        'VISUAL_TWEEN_ALREADY_ACTIVE',
        `Visual property '${normalized.propertyKey}' already has an active animation.`,
        normalized.propertyKey
      );
    }

    const current = this.instance.readRuntimeReadable(normalized.propertyKey).value;
    assertInterpolatable(normalized.propertyKey, current, normalized.targetValue);

    if (active) this.finish(active, 'replaced', false);

    this.instance.setAnimationOverride(normalized.propertyKey, current);

    const tween: ActiveTween = {
      handle: this.handleFactory(),
      propertyKey: normalized.propertyKey,
      startValue: current as number | string,
      targetValue: normalized.targetValue as number | string,
      durationMs: normalized.durationMs,
      easing: normalized.easing,
      repeatCount: normalized.repeatCount,
      pingPong: normalized.pingPong,
      startedAtMs: this.clock.now()
    };

    if (!tween.handle || tween.handle !== tween.handle.trim()) {
      this.instance.clearAnimationOverride(tween.propertyKey);
      throw new RuntimeVisualTweenError(
        'VISUAL_TWEEN_HANDLE_INVALID',
        'Tween handle factory returned an invalid stable handle.',
        tween.propertyKey
      );
    }

    tween.unregisterDisposer = this.instance.registerDisposer(() => this.cancel(tween.handle));
    this.activeByProperty.set(tween.propertyKey, tween);
    this.activeByHandle.set(tween.handle, tween);
    tween.frameHandle = this.clock.requestFrame(timestamp => this.advance(tween.handle, timestamp));

    return Object.freeze({
      accepted: true,
      handle: tween.handle,
      propertyKey: tween.propertyKey,
      visualRuntimeInstanceId: this.instance.runtimeInstanceId
    });
  }

  cancel(handle: string): boolean {
    const tween = this.activeByHandle.get(handle);
    if (!tween) return false;
    this.finish(tween, 'cancelled', false);
    return true;
  }

  cancelProperty(propertyKey: string): boolean {
    const tween = this.activeByProperty.get(propertyKey);
    if (!tween) return false;
    this.finish(tween, 'cancelled', false);
    return true;
  }

  private advance(handle: string, timestampMs: number): void {
    const tween = this.activeByHandle.get(handle);
    if (!tween || this.instance.isDisposed) return;

    try {
      const elapsed = Math.max(0, timestampMs - tween.startedAtMs);
      const iteration = Math.min(Math.floor(elapsed / tween.durationMs), tween.repeatCount);
      const iterationElapsed = elapsed - (iteration * tween.durationMs);
      const rawProgress = Math.min(1, iterationElapsed / tween.durationMs);
      const reversed = tween.pingPong && iteration % 2 === 1;
      const directionalProgress = reversed ? 1 - rawProgress : rawProgress;
      const progress = ease(directionalProgress, tween.easing);

      this.instance.setAnimationOverride(
        tween.propertyKey,
        interpolate(tween.startValue, tween.targetValue, progress)
      );

      const totalIterations = tween.repeatCount + 1;
      const totalDuration = tween.durationMs * totalIterations;
      if (elapsed >= totalDuration) {
        const finalValue = tween.pingPong && tween.repeatCount % 2 === 1
          ? tween.startValue
          : tween.targetValue;
        this.instance.setAnimationOverride(tween.propertyKey, finalValue);
        this.instance.setScriptOverride(tween.propertyKey, finalValue);
        this.finish(tween, 'completed', true);
        return;
      }

      tween.frameHandle = this.clock.requestFrame(timestamp => this.advance(handle, timestamp));
    } catch (reason) {
      this.finish(tween, 'faulted', false, sanitizeDiagnostic(reason));
    }
  }

  private finish(
    tween: ActiveTween,
    reason: VisualTweenCompletionReason,
    finalAlreadyCommitted: boolean,
    diagnostic?: string
  ): void {
    if (this.activeByHandle.get(tween.handle) !== tween) return;

    this.activeByHandle.delete(tween.handle);
    if (this.activeByProperty.get(tween.propertyKey) === tween) {
      this.activeByProperty.delete(tween.propertyKey);
    }
    if (tween.frameHandle !== undefined) this.clock.cancelFrame(tween.frameHandle);
    tween.unregisterDisposer?.();
    tween.unregisterDisposer = undefined;

    if (!this.instance.isDisposed) {
      try {
        this.instance.clearAnimationOverride(tween.propertyKey);
      } catch {
      }
    }

    this.onCompleted?.(Object.freeze({
      handle: tween.handle,
      propertyKey: tween.propertyKey,
      visualRuntimeInstanceId: this.instance.runtimeInstanceId,
      reason,
      completedAtMs: this.clock.now(),
      diagnostic: finalAlreadyCommitted ? undefined : diagnostic
    }));
  }
}

type NormalizedTweenRequest = VisualTweenRequest & Required<Pick<
  VisualTweenRequest,
  'easing' | 'repeatCount' | 'pingPong' | 'conflictBehavior'
>>;

function validateRequest(request: VisualTweenRequest): NormalizedTweenRequest {
  if (!request || typeof request !== 'object') {
    throw new RuntimeVisualTweenError('VISUAL_TWEEN_REQUEST_INVALID', 'Tween request must be an object.');
  }
  if (!request.targetReference || request.targetReference !== request.targetReference.trim()) {
    throw new RuntimeVisualTweenError('VISUAL_TWEEN_TARGET_INVALID', 'Tween target reference must be a stable exact value.');
  }
  if (!request.propertyKey || request.propertyKey !== request.propertyKey.trim()) {
    throw new RuntimeVisualTweenError('VISUAL_TWEEN_PROPERTY_INVALID', 'Tween property key must be a stable exact value.');
  }
  if (!Number.isFinite(request.durationMs) || request.durationMs <= 0 || request.durationMs > VISUAL_TWEEN_POLICY.maxDurationMs) {
    throw new RuntimeVisualTweenError(
      'VISUAL_TWEEN_DURATION_INVALID',
      `Tween duration must be > 0 and <= ${VISUAL_TWEEN_POLICY.maxDurationMs} ms.`,
      request.propertyKey
    );
  }

  const repeatCount = request.repeatCount ?? 0;
  if (!Number.isInteger(repeatCount) || repeatCount < 0 || repeatCount > VISUAL_TWEEN_POLICY.maxRepeatCount) {
    throw new RuntimeVisualTweenError(
      'VISUAL_TWEEN_REPEAT_INVALID',
      `Tween repeatCount must be an integer from 0 to ${VISUAL_TWEEN_POLICY.maxRepeatCount}.`,
      request.propertyKey
    );
  }
  if (request.durationMs * (repeatCount + 1) > VISUAL_TWEEN_POLICY.maxTotalDurationMs) {
    throw new RuntimeVisualTweenError(
      'VISUAL_TWEEN_TOTAL_DURATION_INVALID',
      `Tween total duration must be <= ${VISUAL_TWEEN_POLICY.maxTotalDurationMs} ms.`,
      request.propertyKey
    );
  }

  const easing = request.easing ?? 'linear';
  if (!(['linear', 'easeIn', 'easeOut', 'easeInOut'] as const).includes(easing)) {
    throw new RuntimeVisualTweenError('VISUAL_TWEEN_EASING_INVALID', `Unsupported tween easing '${String(easing)}'.`, request.propertyKey);
  }

  const conflictBehavior = request.conflictBehavior ?? 'replaceExisting';
  if (!(['replaceExisting', 'rejectIfActive'] as const).includes(conflictBehavior)) {
    throw new RuntimeVisualTweenError(
      'VISUAL_TWEEN_CONFLICT_BEHAVIOR_INVALID',
      `Unsupported tween conflict behavior '${String(conflictBehavior)}'.`,
      request.propertyKey
    );
  }

  if (typeof (request.pingPong ?? false) !== 'boolean') {
    throw new RuntimeVisualTweenError('VISUAL_TWEEN_PING_PONG_INVALID', 'Tween pingPong must be boolean.', request.propertyKey);
  }

  return {
    ...request,
    easing,
    repeatCount,
    pingPong: request.pingPong ?? false,
    conflictBehavior
  };
}

function assertTarget(instance: RuntimeVisualInstance, targetReference: string): void {
  if (targetReference === instance.objectId || targetReference === instance.objectKey) return;
  throw new RuntimeVisualTweenError(
    'VISUAL_TWEEN_TARGET_OUTSIDE_INSTANCE',
    `Visual target '${targetReference}' is outside Runtime Visual Instance '${instance.runtimeInstanceId}'.`
  );
}

function assertInterpolatable(propertyKey: string, startValue: unknown, targetValue: unknown): void {
  if (typeof startValue === 'number' && typeof targetValue === 'number' &&
      Number.isFinite(startValue) && Number.isFinite(targetValue)) return;
  if (isHexColor(startValue) && isHexColor(targetValue)) return;

  throw new RuntimeVisualTweenError(
    'VISUAL_TWEEN_VALUE_UNSUPPORTED',
    `Visual property '${propertyKey}' requires matching finite-number or #RRGGBB/#RRGGBBAA tween values.`,
    propertyKey
  );
}

function interpolate(startValue: number | string, targetValue: number | string, progress: number): number | string {
  if (typeof startValue === 'number' && typeof targetValue === 'number') {
    return startValue + ((targetValue - startValue) * progress);
  }

  const start = parseColor(startValue as string);
  const target = parseColor(targetValue as string);
  return formatColor(start.map((value, index) =>
    Math.round(value + ((target[index] - value) * progress))
  ));
}

function ease(progress: number, easing: VisualTweenEasing): number {
  const value = Math.max(0, Math.min(1, progress));
  switch (easing) {
    case 'linear': return value;
    case 'easeIn': return value * value;
    case 'easeOut': return 1 - ((1 - value) * (1 - value));
    case 'easeInOut':
      return value < 0.5
        ? 2 * value * value
        : 1 - (Math.pow(-2 * value + 2, 2) / 2);
  }
}

function isHexColor(value: unknown): value is string {
  return typeof value === 'string' && /^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$/.test(value);
}

function parseColor(value: string): [number, number, number, number] {
  const hex = value.slice(1);
  return [
    Number.parseInt(hex.slice(0, 2), 16),
    Number.parseInt(hex.slice(2, 4), 16),
    Number.parseInt(hex.slice(4, 6), 16),
    hex.length === 8 ? Number.parseInt(hex.slice(6, 8), 16) : 255
  ];
}

function formatColor(value: readonly number[]): string {
  const hex = value.map(component =>
    Math.max(0, Math.min(255, component)).toString(16).padStart(2, '0').toUpperCase()
  ).join('');
  return `#${hex}`;
}

function browserFrameClock(): VisualTweenFrameClock {
  return {
    now: () => globalThis.performance?.now?.() ?? Date.now(),
    requestFrame: callback => {
      if (typeof globalThis.requestAnimationFrame !== 'function') {
        throw new RuntimeVisualTweenError(
          'VISUAL_TWEEN_FRAME_CLOCK_UNAVAILABLE',
          'Browser animation-frame scheduling is unavailable.'
        );
      }
      return globalThis.requestAnimationFrame(callback);
    },
    cancelFrame: handle => {
      if (typeof globalThis.cancelAnimationFrame === 'function') {
        globalThis.cancelAnimationFrame(handle);
      }
    }
  };
}

function createTweenHandle(): string {
  const randomUuid = globalThis.crypto?.randomUUID;
  if (typeof randomUuid !== 'function') {
    throw new RuntimeVisualTweenError(
      'VISUAL_TWEEN_HANDLE_FACTORY_UNAVAILABLE',
      'Browser UUID generation is required for visual tween handles.'
    );
  }
  return `tween-${randomUuid.call(globalThis.crypto)}`;
}

function sanitizeDiagnostic(reason: unknown): string {
  if (reason instanceof RuntimeVisualInstanceError || reason instanceof RuntimeVisualTweenError) {
    return `${reason.name}: ${reason.code}`;
  }
  return 'Visual tween execution failed.';
}
