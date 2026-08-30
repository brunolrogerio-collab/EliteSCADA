import { test, expect } from '@playwright/test';
import type { ScriptEngineeringContext } from '../src/engineering/scripts/scriptEngineeringTypes';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonRuntimeEnvironment
} from '../src/python-runtime/clientVisualPythonRuntime';
import { ClientVisualEventDispatcher } from '../src/python-runtime/clientVisualEventDispatcher';
import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from '../src/python-runtime/pythonRuntimeContracts';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse
} from '../src/python-runtime/clientVisualPythonWorkerTransport';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  projectVisualEngineeringDefinition,
  RuntimeVisualInstance,
  type VisualTweenFrameClock
} from '../src/visual-runtime';

const visualDefinitionId = '11111111-1111-1111-1111-111111111111';
const objectId = '22222222-2222-2222-2222-222222222222';
const scriptId = '33333333-3333-3333-3333-333333333333';

const environment: ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated: () => true,
  createInterruptBuffer: () => new SharedArrayBuffer(1),
  pyodideIndexUrl: () => 'http://127.0.0.1:5173/pyodide/'
};

class ManualFrameClock implements VisualTweenFrameClock {
  private current = 0;
  private sequence = 0;
  private readonly callbacks = new Map<number, (timestampMs: number) => void>();

  now(): number {
    return this.current;
  }

  requestFrame(callback: (timestampMs: number) => void): number {
    const handle = ++this.sequence;
    this.callbacks.set(handle, callback);
    return handle;
  }

  cancelFrame(handle: number): void {
    this.callbacks.delete(handle);
  }

  advanceTo(timestampMs: number): void {
    this.current = timestampMs;
    const callbacks = [...this.callbacks.values()];
    this.callbacks.clear();
    for (const callback of callbacks) callback(timestampMs);
  }
}

class TweenRequestWorker {
  private readonly messageListeners = new Set<(event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void>();
  private pendingDispatch: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }> | null = null;

  lastHandlerName: string | null = null;
  lastApiResponse: Extract<PythonWorkerRequest, { kind: 'api-response' }> | null = null;
  terminated = false;

  postMessage(payload: ClientVisualPythonPrivateWorkerRequest): void {
    if ('kind' in payload && payload.kind === 'engine-bootstrap') {
      queueMicrotask(() => this.emit({
        kind: 'engine-ready',
        generation: payload.generation,
        identity: payload.identity
      }));
      return;
    }

    if (!('bridgeVersion' in payload) || payload.bridgeVersion !== CLIENT_VISUAL_PYTHON_BRIDGE_VERSION) return;
    const message = payload.message;

    switch (message.kind) {
      case 'initialize-script':
        queueMicrotask(() => this.emitBridge({
          kind: 'ready',
          requestId: message.requestId,
          identity: message.identity
        }));
        return;
      case 'dispatch-event':
        this.pendingDispatch = message;
        this.lastHandlerName = message.handlerName;
        queueMicrotask(() => this.emitBridge({
          kind: 'api-request',
          requestId: `api-${message.executionId}`,
          executionId: message.executionId,
          identity: message.identity,
          capability: 'visualTween.request',
          operation: 'request',
          arguments: {
            targetReference: objectId,
            propertyKey: 'x',
            targetValue: 100,
            durationMs: 100,
            easing: 'linear'
          }
        }));
        return;
      case 'api-response': {
        this.lastApiResponse = message;
        const dispatch = this.pendingDispatch;
        this.pendingDispatch = null;
        if (!dispatch) return;
        queueMicrotask(() => this.emitBridge({
          kind: 'execution-result',
          requestId: dispatch.requestId,
          executionId: dispatch.executionId,
          identity: dispatch.identity,
          status: message.ok ? 'completed' : 'faulted',
          durationMs: 1,
          sanitizedError: message.ok ? undefined : 'Capability request failed.'
        }));
        return;
      }
      case 'dispose-script':
        queueMicrotask(() => this.emitBridge({
          kind: 'disposed',
          requestId: message.requestId,
          identity: message.identity
        }));
        return;
      case 'compile-source':
      case 'cancel-execution':
        return;
    }
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message') {
      this.messageListeners.add(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
    }
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type === 'message') {
      this.messageListeners.delete(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
    }
  }

  terminate(): void {
    this.terminated = true;
  }

  private emitBridge(message: PythonWorkerResponse): void {
    const envelope: PythonWorkerEnvelope<PythonWorkerResponse> = {
      bridgeVersion: CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
      message
    };
    this.emit(envelope);
  }

  private emit(payload: ClientVisualPythonPrivateWorkerResponse): void {
    const event = { data: payload } as MessageEvent<ClientVisualPythonPrivateWorkerResponse>;
    for (const listener of this.messageListeners) listener(event);
  }
}

function createInstance(): RuntimeVisualInstance {
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.rectangle);
  const definition = projectVisualEngineeringDefinition({
    objectId,
    key: 'PumpBox',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    baseProperties: { x: 0 }
  }, schema);

  return new RuntimeVisualInstance({
    definition,
    schema,
    runtimeInstanceId: 'runtime-pump-box'
  });
}

function createContext(): ScriptEngineeringContext {
  return {
    workspace: { isDirty: false, changeVersion: 7 },
    scripts: [{
      id: scriptId,
      path: 'Screens/PumpBox.py',
      name: 'PumpBox Click',
      scope: 'clientVisual',
      source: 'async def on_click(event):\n    return None\n',
      enabled: true,
      language: 'Python',
      languageVersion: '3',
      entryPoints: [{
        eventKind: 'objectInteraction',
        handlerName: 'on_click',
        targetReference: null,
        tagReference: null,
        timerIntervalMs: null
      }],
      dependencies: [],
      metadata: {}
    }],
    visualEventReferences: [{
      visualDefinitionId,
      visualObjectId: objectId,
      eventKind: 'objectInteraction',
      scriptId,
      entryPoint: 'on_click',
      targetReference: null,
      tagReference: null,
      timerIntervalMs: null
    }]
  };
}

test('Wave 10 click association dispatches through Python bridge into tween and stable Script final value', async () => {
  const instance = createInstance();
  const clock = new ManualFrameClock();
  const worker = new TweenRequestWorker();
  let invalidations = 0;

  const dispatcher = new ClientVisualEventDispatcher({
    instances: new Map([[objectId, instance]]),
    frameClock: clock,
    onVisualStateChanged: () => { invalidations++; },
    runtimeFactory: options => new ClientVisualPythonRuntime({
      ...options,
      workerFactory: () => worker as unknown as Worker,
      environment
    })
  });

  const records = await dispatcher.dispatchObjectInteraction({
    visualDefinitionId,
    objectId,
    eventKey: 'click',
    context: createContext()
  });

  expect(records).toHaveLength(1);
  expect(records[0].result.status).toBe('completed');
  expect(worker.lastHandlerName).toBe('on_click');
  expect(worker.lastApiResponse?.ok).toBe(true);
  expect(instance.readEffective('x')).toMatchObject({ value: 0, source: 'animation' });

  clock.advanceTo(50);
  expect(instance.readEffective('x')).toMatchObject({ value: 50, source: 'animation' });

  clock.advanceTo(100);
  expect(instance.readEffective('x')).toMatchObject({ value: 100, source: 'script' });
  expect(invalidations).toBeGreaterThanOrEqual(3);
  expect(worker.terminated).toBe(true);

  dispatcher.dispose();
});

test('Wave 10 dispatcher fails closed when persisted reference cannot resolve an enabled Client Visual Script', async () => {
  const instance = createInstance();
  const context = createContext();
  context.scripts[0] = { ...context.scripts[0], enabled: false };

  const dispatcher = new ClientVisualEventDispatcher({
    instances: new Map([[objectId, instance]]),
    frameClock: new ManualFrameClock()
  });

  const records = await dispatcher.dispatchObjectInteraction({
    visualDefinitionId,
    objectId,
    eventKey: 'click',
    context
  });

  expect(records).toHaveLength(1);
  expect(records[0].result.status).toBe('faulted');
  expect(records[0].result.sanitizedError).toContain('unavailable or disabled');
  expect(instance.readEffective('x')).toMatchObject({ value: 0, source: 'engineering' });

  dispatcher.dispose();
});
