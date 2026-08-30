import React from 'react';
import { createRoot } from 'react-dom/client';
import type { ScriptEngineeringContext } from '../../src/engineering/scripts/scriptEngineeringTypes';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonRuntimeEnvironment,
  type ClientVisualPythonRuntimeOptions
} from '../../src/python-runtime/clientVisualPythonRuntime';
import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from '../../src/python-runtime/pythonRuntimeContracts';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse
} from '../../src/python-runtime/clientVisualPythonWorkerTransport';
import { RuntimeVisualDefinitionRenderer } from '../../src/runtime/visual-navigation/RuntimeVisualDefinitionRenderer';

const SCREEN_ID = 'screen-wave10-runtime';
const OBJECT_ID = 'rectangle-wave10-runtime';
const SCRIPT_ID = 'script-wave10-runtime';

const environment: ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated: () => true,
  createInterruptBuffer: () => new ArrayBuffer(1) as SharedArrayBuffer,
  pyodideIndexUrl: () => '/pyodide/'
};

class TweenPythonWorker {
  private readonly messageListeners = new Set<(event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void>();
  private pendingDispatch: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }> | null = null;

  constructor(private readonly host: HTMLElement) {}

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
        this.host.dataset.pythonHandler = message.handlerName;
        this.pendingDispatch = message;
        queueMicrotask(() => this.emitBridge({
          kind: 'api-request',
          requestId: `api-${message.executionId}`,
          executionId: message.executionId,
          identity: message.identity,
          capability: 'visualTween.request',
          operation: 'request',
          arguments: {
            targetReference: OBJECT_ID,
            propertyKey: 'x',
            targetValue: 120,
            durationMs: 400,
            easing: 'linear'
          }
        }));
        return;
      case 'api-response': {
        const dispatch = this.pendingDispatch;
        if (!dispatch) return;
        this.pendingDispatch = null;
        queueMicrotask(() => this.emitBridge({
          kind: 'execution-result',
          requestId: dispatch.requestId,
          executionId: dispatch.executionId,
          identity: dispatch.identity,
          status: message.ok ? 'completed' : 'faulted',
          durationMs: 2,
          sanitizedError: message.ok ? undefined : 'Visual tween request failed.'
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

  terminate(): void {}

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type !== 'message') return;
    this.messageListeners.add(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type !== 'message') return;
    this.messageListeners.delete(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
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

export function mountWave10RuntimeAcceptanceHarness(host: HTMLElement): void {
  const context: ScriptEngineeringContext = {
    workspace: {
      projectKey: 'wave10-runtime-acceptance',
      projectName: 'Wave 10 Runtime Acceptance',
      baseRevision: 1,
      isDirty: false,
      changeVersion: 1
    },
    scripts: [{
      id: SCRIPT_ID,
      path: 'Client/Visual/Wave10RuntimeAcceptance.py',
      name: 'Wave 10 Runtime Acceptance',
      scope: 'clientVisual',
      source: 'async def on_click(event):\n    return None\n',
      enabled: true,
      language: 'python',
      languageVersion: '3.12',
      entryPoints: [{ eventKind: 'objectInteraction', handlerName: 'on_click' }],
      dependencies: [],
      metadata: {}
    }],
    visualEventReferences: [{
      visualDefinitionId: SCREEN_ID,
      visualObjectId: OBJECT_ID,
      eventKind: 'objectInteraction',
      scriptId: SCRIPT_ID,
      entryPoint: 'on_click',
      targetReference: null,
      tagReference: null,
      timerIntervalMs: null
    }]
  };

  const runtimeFactory = (options: ClientVisualPythonRuntimeOptions) => new ClientVisualPythonRuntime({
    ...options,
    workerFactory: () => new TweenPythonWorker(host) as unknown as Worker,
    environment
  });

  const root = createRoot(host);
  root.render(<RuntimeVisualDefinitionRenderer
    visualDefinitionId={SCREEN_ID}
    runtimeContextId="screen:wave10-runtime-acceptance"
    elements={[{
      id: OBJECT_ID,
      key: 'Wave10Rectangle',
      type: 'core.rectangle',
      properties: {
        x: 0,
        y: 0,
        width: 80,
        height: 48,
        fillColor: '#446688'
      }
    }]}
    emptyLabel="No visual objects"
    scriptContext={context}
    runtimeFactory={runtimeFactory}
    onScriptDispatch={records => {
      host.dataset.scriptStatus = records[0]?.result.status ?? 'none';
    }}
  />);
}
