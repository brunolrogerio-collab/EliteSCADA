import { test, expect } from '@playwright/test';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonRuntimeEnvironment
} from '../src/python-runtime/clientVisualPythonRuntime';
import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  CLIENT_VISUAL_PYTHON_POLICY,
  type PythonRuntimeIdentity,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from '../src/python-runtime/pythonRuntimeContracts';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse
} from '../src/python-runtime/clientVisualPythonWorkerTransport';

const identity: PythonRuntimeIdentity = {
  scriptId: '11111111-1111-1111-1111-111111111111',
  runtimeInstanceId: 'runtime-a'
};

const environment: ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated: () => true,
  createInterruptBuffer: () => new SharedArrayBuffer(1),
  pyodideIndexUrl: () => 'http://127.0.0.1:5173/pyodide/'
};

class FakePythonWorker {
  private readonly messageListeners = new Set<(event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void>();
  private readonly errorListeners = new Set<(event: Event) => void>();
  private pendingCapabilityDispatch: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }> | null = null;

  terminated = false;
  hangDispatch = false;
  requestTagRead = false;
  sendStaleExecutionFirst = false;
  bootstrapInterruptBuffer: SharedArrayBuffer | null = null;
  lastApiResponse: Extract<PythonWorkerRequest, { kind: 'api-response' }> | null = null;
  dispatchCount = 0;

  postMessage(payload: ClientVisualPythonPrivateWorkerRequest): void {
    if ('kind' in payload && payload.kind === 'engine-bootstrap') {
      this.bootstrapInterruptBuffer = payload.interruptBuffer;
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
      case 'compile-source':
        queueMicrotask(() => this.emitBridge({
          kind: 'compile-result',
          requestId: message.requestId,
          identity: message.identity,
          diagnostics: message.source.includes('broken(')
            ? [{ severity: 'error', code: 'PYTHON_SYNTAX_ERROR', message: 'Invalid Python syntax.', line: 2, column: 4 }]
            : []
        }));
        return;
      case 'dispatch-event':
        this.dispatchCount++;
        if (this.hangDispatch) return;
        if (this.requestTagRead) {
          this.pendingCapabilityDispatch = message;
          queueMicrotask(() => this.emitBridge({
            kind: 'api-request',
            requestId: `api-${message.executionId}`,
            executionId: message.executionId,
            identity: message.identity,
            capability: 'tag.read',
            operation: 'read',
            arguments: { reference: 'Plant.Level' }
          }));
          return;
        }
        if (this.sendStaleExecutionFirst) {
          this.sendStaleExecutionFirst = false;
          queueMicrotask(() => {
            this.emitBridge({
              kind: 'execution-result',
              requestId: message.requestId,
              executionId: message.executionId,
              identity: { ...message.identity, runtimeInstanceId: 'stale-runtime' },
              status: 'completed',
              durationMs: 1
            });
            this.emitCompleted(message);
          });
          return;
        }
        queueMicrotask(() => this.emitCompleted(message));
        return;
      case 'api-response':
        this.lastApiResponse = message;
        if (!this.pendingCapabilityDispatch) return;
        {
          const dispatch = this.pendingCapabilityDispatch;
          this.pendingCapabilityDispatch = null;
          queueMicrotask(() => this.emitBridge({
            kind: 'execution-result',
            requestId: dispatch.requestId,
            executionId: dispatch.executionId,
            identity: dispatch.identity,
            status: message.ok ? 'completed' : 'faulted',
            durationMs: 2,
            sanitizedError: message.ok ? undefined : 'Capability request failed.'
          }));
        }
        return;
      case 'dispose-script':
        queueMicrotask(() => this.emitBridge({
          kind: 'disposed',
          requestId: message.requestId,
          identity: message.identity
        }));
        return;
      case 'cancel-execution':
        return;
    }
  }

  terminate(): void {
    this.terminated = true;
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    const callback = listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void;
    if (type === 'message') this.messageListeners.add(callback);
    if (type === 'error') this.errorListeners.add(callback as unknown as (event: Event) => void);
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    const callback = listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void;
    if (type === 'message') this.messageListeners.delete(callback);
    if (type === 'error') this.errorListeners.delete(callback as unknown as (event: Event) => void);
  }

  private emitCompleted(message: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }>) {
    this.emitBridge({
      kind: 'execution-result',
      requestId: message.requestId,
      executionId: message.executionId,
      identity: message.identity,
      status: 'completed',
      durationMs: 1.5
    });
  }

  private emitBridge(message: PythonWorkerResponse) {
    const envelope: PythonWorkerEnvelope<PythonWorkerResponse> = {
      bridgeVersion: CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
      message
    };
    this.emit(envelope);
  }

  private emit(payload: ClientVisualPythonPrivateWorkerResponse) {
    const event = { data: payload } as MessageEvent<ClientVisualPythonPrivateWorkerResponse>;
    for (const listener of this.messageListeners) listener(event);
  }
}

function createRuntime(
  worker: FakePythonWorker,
  capabilityProvider: ConstructorParameters<typeof ClientVisualPythonRuntime>[0]['capabilityProvider'] = {}
) {
  return new ClientVisualPythonRuntime({
    identity,
    source: 'async def on_event(event):\n    return None\n',
    handlerNames: ['on_event'],
    capabilityProvider,
    workerFactory: () => worker as unknown as Worker,
    environment
  });
}

test('client runtime initializes, compiles and serially executes through bridge v1', async () => {
  const worker = new FakePythonWorker();
  const runtime = createRuntime(worker);

  await runtime.initialize();
  const compile = await runtime.compileSource('def ok():\n    return 1\n');
  const execution = await runtime.dispatchEvent('on_event', 'tag:Plant.Level', { value: 12.5 });

  expect(compile).toEqual({ diagnostics: [], superseded: false });
  expect(execution.status).toBe('completed');
  expect(worker.dispatchCount).toBe(1);
  await runtime.dispose();
  expect(worker.terminated).toBe(true);
});

test('trusted capability provider receives permitted TAG read and missing provider fails closed', async () => {
  const permittedWorker = new FakePythonWorker();
  permittedWorker.requestTagRead = true;
  const reads: string[] = [];
  const permittedRuntime = createRuntime(permittedWorker, {
    readTag: reference => {
      reads.push(reference);
      return { value: 42, quality: 'Good' };
    }
  });

  const permitted = await permittedRuntime.dispatchEvent('on_event', 'read-tag', null);
  expect(permitted.status).toBe('completed');
  expect(reads).toEqual(['Plant.Level']);
  expect(permittedWorker.lastApiResponse?.ok).toBe(true);
  await permittedRuntime.dispose();

  const deniedWorker = new FakePythonWorker();
  deniedWorker.requestTagRead = true;
  const deniedRuntime = createRuntime(deniedWorker, {});
  const denied = await deniedRuntime.dispatchEvent('on_event', 'read-tag', null);
  expect(denied.status).toBe('faulted');
  expect(deniedWorker.lastApiResponse?.ok).toBe(false);
  expect(deniedWorker.lastApiResponse?.error).toBe('Client Visual capability request failed.');
  await deniedRuntime.dispose();
});

test('stale runtime identity response is ignored before matching execution result', async () => {
  const worker = new FakePythonWorker();
  worker.sendStaleExecutionFirst = true;
  const runtime = createRuntime(worker);

  const result = await runtime.dispatchEvent('on_event', 'stale-test', null);
  expect(result.status).toBe('completed');
  expect(result.executionId).toBeTruthy();
  await runtime.dispose();
});

test('over-budget execution interrupts then hard-terminates sandbox worker', async () => {
  const worker = new FakePythonWorker();
  worker.hangDispatch = true;
  const runtime = createRuntime(worker);

  const result = await runtime.dispatchEvent('on_event', 'infinite-loop', null);

  expect(result.status).toBe('timed-out');
  expect(worker.terminated).toBe(true);
  expect(worker.bootstrapInterruptBuffer).not.toBeNull();
  expect(new Uint8Array(worker.bootstrapInterruptBuffer!)[0]).toBe(2);
  expect(result.sanitizedError).toContain('sandbox Worker was terminated');
});

test('AbortSignal cancellation uses interrupt path and resolves as cancelled after hard stop', async () => {
  const worker = new FakePythonWorker();
  worker.hangDispatch = true;
  const runtime = createRuntime(worker);
  const cancellation = new AbortController();

  const pending = runtime.dispatchEvent('on_event', 'cancel-me', null, cancellation.signal);
  setTimeout(() => cancellation.abort(), 5);
  const result = await pending;

  expect(result.status).toBe('cancelled');
  expect(worker.terminated).toBe(true);
});

test('event admission is bounded and queued events coalesce by stable event key', async () => {
  const worker = new FakePythonWorker();
  const runtime = createRuntime(worker);
  await runtime.initialize();
  worker.hangDispatch = true;

  const active = runtime.dispatchEvent('on_event', 'active', null);
  await waitUntil(() => worker.dispatchCount === 1);

  const queued = Array.from({ length: CLIENT_VISUAL_PYTHON_POLICY.maxQueuedEvents }, (_, index) =>
    runtime.dispatchEvent('on_event', `queued-${index}`, { index })
  );
  expect(runtime.queuedEventCount).toBe(CLIENT_VISUAL_PYTHON_POLICY.maxQueuedEvents);

  const rejected = await runtime.dispatchEvent('on_event', 'overflow', null);
  expect(rejected.status).toBe('rejected-queue-full');

  const replacement = runtime.dispatchEvent('on_event', 'queued-7', { index: 700 });
  expect((await queued[7]).status).toBe('coalesced');
  expect(runtime.queuedEventCount).toBe(CLIENT_VISUAL_PYTHON_POLICY.maxQueuedEvents);

  await runtime.dispose();
  await active;
  await replacement;
});

async function waitUntil(predicate: () => boolean) {
  const deadline = Date.now() + 1_000;
  while (!predicate()) {
    if (Date.now() > deadline) throw new Error('Timed out waiting for fake worker state.');
    await new Promise(resolve => setTimeout(resolve, 1));
  }
}
