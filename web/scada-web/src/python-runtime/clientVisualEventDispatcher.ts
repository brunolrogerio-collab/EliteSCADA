import type {
  ScriptEngineeringContext,
  ScriptVisualEventReference
} from '../engineering/scripts/scriptEngineeringTypes';
import type { RuntimeVisualInstance } from '../visual-runtime/runtimeVisualInstance';
import {
  createVisualPythonPropertyCapabilityProvider
} from '../visual-runtime/visualPythonPropertyCapabilityProvider';
import type { VisualTweenFrameClock } from '../visual-runtime/runtimeVisualTween';
import {
  createClientVisualPythonCapabilityProvider,
  type ClientVisualPythonVisualPropertyProvider
} from './createClientVisualPythonCapabilityProvider';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonDispatchResult,
  type ClientVisualPythonRuntimeOptions
} from './clientVisualPythonRuntime';

export type ClientVisualObjectInteractionRequest = Readonly<{
  visualDefinitionId: string;
  objectId: string;
  eventKey: string;
  context: ScriptEngineeringContext;
}>;

export type ClientVisualEventDispatchRecord = Readonly<{
  reference: ScriptVisualEventReference;
  result: ClientVisualPythonDispatchResult;
}>;

export type ClientVisualPythonRuntimeHandle = Pick<
  ClientVisualPythonRuntime,
  'dispatchEvent' | 'dispose'
>;

export type ClientVisualPythonRuntimeFactory = (
  options: ClientVisualPythonRuntimeOptions
) => ClientVisualPythonRuntimeHandle;

export type ClientVisualEventDispatcherOptions = Readonly<{
  instances: ReadonlyMap<string, RuntimeVisualInstance>;
  onVisualStateChanged?: () => void;
  frameClock?: VisualTweenFrameClock;
  runtimeFactory?: ClientVisualPythonRuntimeFactory;
}>;

/**
 * Canonical Wave 10 composition point for visual object interactions.
 *
 * The dispatcher consumes persisted ScriptVisualEventReference records, creates
 * Client Visual Python runtimes only through the accepted sandbox bridge, and
 * binds the generic visual-property capability surface to the concrete Runtime
 * Visual Instance that owns the interaction.
 *
 * Visual state is never persisted here. Script and Animation stay transient
 * Runtime layers over the authoritative Engineering definition.
 */
export class ClientVisualEventDispatcher {
  private readonly instances: ReadonlyMap<string, RuntimeVisualInstance>;
  private readonly visualProviders = new Map<string, ClientVisualPythonVisualPropertyProvider>();
  private readonly runtimeFactory: ClientVisualPythonRuntimeFactory;
  private sequence = 0;

  constructor(options: ClientVisualEventDispatcherOptions) {
    this.instances = options.instances;
    this.runtimeFactory = options.runtimeFactory ?? (runtimeOptions => new ClientVisualPythonRuntime(runtimeOptions));

    const notify = options.onVisualStateChanged ?? (() => undefined);
    const baseClock = options.frameClock ?? browserFrameClock();
    const clock = createInvalidatingClock(baseClock, notify);

    for (const [objectId, instance] of this.instances) {
      const provider = createVisualPythonPropertyCapabilityProvider(instance, { clock });
      this.visualProviders.set(objectId, createInvalidatingVisualProvider(provider, notify));
    }
  }

  async dispatchObjectInteraction(
    request: ClientVisualObjectInteractionRequest
  ): Promise<readonly ClientVisualEventDispatchRecord[]> {
    if (!request.visualDefinitionId.trim() || !request.objectId.trim() || !request.eventKey.trim()) {
      return Object.freeze([]);
    }

    // Wave 10 canonical objectInteraction currently exposes Click only. Do not
    // guess future interaction subtypes from an eventKind that cannot encode them.
    if (request.eventKey.toLocaleLowerCase('en-US') !== 'click') {
      return Object.freeze([]);
    }

    const references = request.context.visualEventReferences
      .filter(reference =>
        reference.visualDefinitionId === request.visualDefinitionId &&
        reference.visualObjectId === request.objectId &&
        reference.eventKind === 'objectInteraction')
      .sort(compareReferences);

    if (references.length === 0) return Object.freeze([]);

    const instance = this.instances.get(request.objectId);
    const visualProvider = this.visualProviders.get(request.objectId);
    if (!instance || !visualProvider || instance.isDisposed) {
      return Object.freeze(references.map(reference => faultedRecord(
        reference,
        'Canonical Runtime Visual Instance is unavailable for this interaction.'
      )));
    }

    const results: ClientVisualEventDispatchRecord[] = [];
    for (const reference of references) {
      results.push(await this.dispatchReference(request, reference, instance, visualProvider));
    }
    return Object.freeze(results);
  }

  dispose(): void {
    for (const instance of this.instances.values()) instance.dispose();
    this.visualProviders.clear();
  }

  private async dispatchReference(
    request: ClientVisualObjectInteractionRequest,
    reference: ScriptVisualEventReference,
    instance: RuntimeVisualInstance,
    visualProvider: ClientVisualPythonVisualPropertyProvider
  ): Promise<ClientVisualEventDispatchRecord> {
    const script = request.context.scripts.find(candidate => candidate.id === reference.scriptId);
    if (!script || !script.enabled || script.scope !== 'clientVisual') {
      return faultedRecord(reference, 'Referenced Client Visual Python Script is unavailable or disabled.');
    }

    const declaredEntryPoint = script.entryPoints.some(entryPoint =>
      entryPoint.eventKind === 'objectInteraction' &&
      entryPoint.handlerName === reference.entryPoint);
    if (!declaredEntryPoint) {
      return faultedRecord(reference, 'Referenced Python entry point is not declared for objectInteraction.');
    }

    const runtime = this.runtimeFactory({
      identity: {
        scriptId: script.id,
        runtimeInstanceId: `visual-event-${instance.runtimeInstanceId}-${++this.sequence}`,
        visualRuntimeInstanceId: instance.runtimeInstanceId
      },
      source: script.source,
      handlerNames: [...new Set(script.entryPoints.map(entryPoint => entryPoint.handlerName).filter(Boolean))],
      capabilityProvider: createClientVisualPythonCapabilityProvider({
        visualPropertyProvider: visualProvider
      })
    });

    try {
      const result = await runtime.dispatchEvent(
        reference.entryPoint,
        stableEventKey(request, reference),
        Object.freeze({
          kind: 'objectInteraction',
          eventKey: request.eventKey,
          visualDefinitionId: request.visualDefinitionId,
          visualObjectId: request.objectId,
          visualRuntimeInstanceId: instance.runtimeInstanceId
        })
      );
      return Object.freeze({ reference: cloneReference(reference), result: Object.freeze({ ...result }) });
    } catch {
      return faultedRecord(reference, 'Client Visual Python interaction failed through the sandbox runtime.');
    } finally {
      await runtime.dispose();
    }
  }
}

function createInvalidatingVisualProvider(
  provider: ClientVisualPythonVisualPropertyProvider,
  notify: () => void
): ClientVisualPythonVisualPropertyProvider {
  return Object.freeze({
    readVisualProperty: provider.readVisualProperty,
    writeVisualProperty(targetReference, propertyKey, value, context) {
      const result = provider.writeVisualProperty!(targetReference, propertyKey, value, context);
      notify();
      return result;
    },
    clearVisualProperty(targetReference, propertyKey, context) {
      const result = provider.clearVisualProperty!(targetReference, propertyKey, context);
      notify();
      return result;
    },
    requestVisualTween(argumentsValue, context) {
      const result = provider.requestVisualTween!(argumentsValue, context);
      notify();
      return result;
    }
  });
}

function createInvalidatingClock(
  clock: VisualTweenFrameClock,
  notify: () => void
): VisualTweenFrameClock {
  return Object.freeze({
    now: () => clock.now(),
    requestFrame: callback => clock.requestFrame(timestampMs => {
      try {
        callback(timestampMs);
      } finally {
        notify();
      }
    }),
    cancelFrame: handle => clock.cancelFrame(handle)
  });
}

function browserFrameClock(): VisualTweenFrameClock {
  return {
    now: () => globalThis.performance?.now?.() ?? Date.now(),
    requestFrame(callback) {
      if (typeof globalThis.requestAnimationFrame !== 'function') {
        throw new Error('Browser animation-frame scheduling is unavailable.');
      }
      return globalThis.requestAnimationFrame(callback);
    },
    cancelFrame(handle) {
      globalThis.cancelAnimationFrame?.(handle);
    }
  };
}

function stableEventKey(
  request: ClientVisualObjectInteractionRequest,
  reference: ScriptVisualEventReference
): string {
  return [
    'visual',
    request.visualDefinitionId,
    request.objectId,
    request.eventKey.toLocaleLowerCase('en-US'),
    reference.scriptId,
    reference.entryPoint
  ].join(':');
}

function compareReferences(
  left: ScriptVisualEventReference,
  right: ScriptVisualEventReference
): number {
  return `${left.scriptId}\u0000${left.entryPoint}`.localeCompare(`${right.scriptId}\u0000${right.entryPoint}`, 'en-US');
}

function faultedRecord(
  reference: ScriptVisualEventReference,
  sanitizedError: string
): ClientVisualEventDispatchRecord {
  return Object.freeze({
    reference: cloneReference(reference),
    result: Object.freeze({ status: 'faulted', sanitizedError })
  });
}

function cloneReference(reference: ScriptVisualEventReference): ScriptVisualEventReference {
  return {
    ...reference,
    tagReference: reference.tagReference ? {
      tagId: reference.tagReference.tagId,
      selector: reference.tagReference.selector ? { ...reference.tagReference.selector } : null
    } : null
  };
}
