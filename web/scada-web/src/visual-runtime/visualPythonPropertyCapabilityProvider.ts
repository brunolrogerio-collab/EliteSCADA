import type {
  ClientVisualPythonCapabilityContext,
  ClientVisualPythonCapabilityProvider
} from '../python-runtime/clientVisualPythonCapabilities';
import {
  RuntimeVisualInstance,
  RuntimeVisualInstanceError,
  type RuntimeVisualPropertyState
} from './runtimeVisualInstance';

export type VisualPythonPropertyCapabilityProvider = Pick<
  ClientVisualPythonCapabilityProvider,
  'readVisualProperty' | 'writeVisualProperty' | 'clearVisualProperty'
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
 */
export function createVisualPythonPropertyCapabilityProvider(
  instance: RuntimeVisualInstance
): VisualPythonPropertyCapabilityProvider {
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
