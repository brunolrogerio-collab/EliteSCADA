import type {
  ClientVisualPythonCapabilityContext,
  ClientVisualPythonCapabilityProvider
} from '../python-runtime/clientVisualPythonCapabilities';
import {
  RuntimeVisualInstance,
  RuntimeVisualInstanceError,
  type RuntimeVisualPropertyState
} from './runtimeVisualInstance';
import {
  RuntimeVisualTweenScheduler,
  type RuntimeVisualTweenSchedulerOptions,
  type VisualTweenAccepted,
  type VisualTweenRequest
} from './runtimeVisualTween';

export type VisualPythonPropertyCapabilityProvider = Pick<
  ClientVisualPythonCapabilityProvider,
  'readVisualProperty' | 'writeVisualProperty' | 'clearVisualProperty' | 'requestVisualTween'
>;

export type VisualPythonPropertyWriteAcknowledgement = Readonly<{
  accepted: true;
  propertyKey: string;
  visualRuntimeInstanceId: string;
}>;

/**
 * Bind the generic Client Visual Python capability dispatcher to one concrete
 * Runtime Visual Instance without exposing renderer, DOM or React authority.
 *
 * Read policy is enforced by RuntimeVisualInstance.readPropertyState().
 * Write/clear policy is enforced by the RuntimeVisualInstance Script layer.
 * Tween execution uses the canonical Animation layer and commits only its stable
 * final value to the Script layer.
 */
export function createVisualPythonPropertyCapabilityProvider(
  instance: RuntimeVisualInstance,
  tweenOptions: RuntimeVisualTweenSchedulerOptions = {}
): VisualPythonPropertyCapabilityProvider {
  const tweenScheduler = new RuntimeVisualTweenScheduler(instance, tweenOptions);

  return Object.freeze({
    readVisualProperty(
      targetReference: string,
      propertyKey: string,
      context: ClientVisualPythonCapabilityContext
    ): RuntimeVisualPropertyState {
      assertCurrentVisualTarget(instance, targetReference, context);
      return instance.readPropertyState(propertyKey);
    },

    writeVisualProperty(
      targetReference: string,
      propertyKey: string,
      value: unknown,
      context: ClientVisualPythonCapabilityContext
    ): VisualPythonPropertyWriteAcknowledgement {
      assertCurrentVisualTarget(instance, targetReference, context);
      instance.setScriptOverride(propertyKey, value);
      return acknowledgement(instance, propertyKey);
    },

    clearVisualProperty(
      targetReference: string,
      propertyKey: string,
      context: ClientVisualPythonCapabilityContext
    ): VisualPythonPropertyWriteAcknowledgement {
      assertCurrentVisualTarget(instance, targetReference, context);
      instance.clearScriptOverride(propertyKey);
      return acknowledgement(instance, propertyKey);
    },

    requestVisualTween(
      argumentsValue: unknown,
      context: ClientVisualPythonCapabilityContext
    ): VisualTweenAccepted {
      const request = requireTweenRequest(argumentsValue);
      assertCurrentVisualTarget(instance, request.targetReference, context);
      return tweenScheduler.start(request);
    }
  });
}

function acknowledgement(
  instance: RuntimeVisualInstance,
  propertyKey: string
): VisualPythonPropertyWriteAcknowledgement {
  return Object.freeze({
    accepted: true,
    propertyKey,
    visualRuntimeInstanceId: instance.runtimeInstanceId
  });
}

function requireTweenRequest(value: unknown): VisualTweenRequest {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_REQUEST_INVALID',
      'Visual tween request must be an object.'
    );
  }

  const request = value as Record<string, unknown>;
  return {
    targetReference: requireString(request, 'targetReference'),
    propertyKey: requireString(request, 'propertyKey'),
    targetValue: requireOwn(request, 'targetValue'),
    durationMs: requireNumber(request, 'durationMs'),
    easing: optionalString(request, 'easing') as VisualTweenRequest['easing'],
    repeatCount: optionalNumber(request, 'repeatCount'),
    pingPong: optionalBoolean(request, 'pingPong'),
    conflictBehavior: optionalString(request, 'conflictBehavior') as VisualTweenRequest['conflictBehavior']
  };
}

function requireOwn(value: Record<string, unknown>, key: string): unknown {
  if (!Object.hasOwn(value, key)) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' is required.`
    );
  }
  return value[key];
}

function requireString(value: Record<string, unknown>, key: string): string {
  const candidate = requireOwn(value, key);
  if (typeof candidate !== 'string') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' must be a string.`
    );
  }
  return candidate;
}

function requireNumber(value: Record<string, unknown>, key: string): number {
  const candidate = requireOwn(value, key);
  if (typeof candidate !== 'number') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' must be a number.`
    );
  }
  return candidate;
}

function optionalString(value: Record<string, unknown>, key: string): string | undefined {
  if (!Object.hasOwn(value, key) || value[key] === null || value[key] === undefined) return undefined;
  if (typeof value[key] !== 'string') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' must be a string when provided.`
    );
  }
  return value[key] as string;
}

function optionalNumber(value: Record<string, unknown>, key: string): number | undefined {
  if (!Object.hasOwn(value, key) || value[key] === null || value[key] === undefined) return undefined;
  if (typeof value[key] !== 'number') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' must be a number when provided.`
    );
  }
  return value[key] as number;
}

function optionalBoolean(value: Record<string, unknown>, key: string): boolean | undefined {
  if (!Object.hasOwn(value, key) || value[key] === null || value[key] === undefined) return undefined;
  if (typeof value[key] !== 'boolean') {
    throw new RuntimeVisualInstanceError(
      'VISUAL_TWEEN_ARGUMENT_INVALID',
      `Visual tween argument '${key}' must be boolean when provided.`
    );
  }
  return value[key] as boolean;
}

function assertCurrentVisualTarget(
  instance: RuntimeVisualInstance,
  targetReference: string,
  context: ClientVisualPythonCapabilityContext
): void {
  if (instance.isDisposed) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_INSTANCE_DISPOSED',
      `Runtime visual instance '${instance.runtimeInstanceId}' is disposed.`
    );
  }

  if (!context.visualRuntimeInstanceId || context.visualRuntimeInstanceId !== instance.runtimeInstanceId) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_INSTANCE_CONTEXT_MISMATCH',
      'Client Visual Python execution does not own the requested Runtime Visual Instance.'
    );
  }

  if (targetReference !== instance.objectId && targetReference !== instance.objectKey) {
    throw new RuntimeVisualInstanceError(
      'VISUAL_RUNTIME_TARGET_OUTSIDE_CONTEXT',
      `Visual target '${targetReference}' is outside the current Runtime Visual Instance context.`
    );
  }
}
