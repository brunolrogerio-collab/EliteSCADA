import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  CLIENT_VISUAL_PYTHON_POLICY,
  hasMatchingPythonRuntimeIdentity,
  type PythonRuntimeIdentity,
  type PythonSourceDiagnostic,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from './pythonRuntimeContracts';
import {
  dispatchClientVisualPythonCapability,
  type ClientVisualPythonCapabilityProvider
} from './clientVisualPythonCapabilities';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse
} from './clientVisualPythonWorkerTransport';

export type ClientVisualPythonWorkerFactory = () => Worker;

export type ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated(): boolean;
  createInterruptBuffer(): SharedArrayBuffer;
  pyodideIndexUrl(): string;
};

export type ClientVisualPythonRuntimeOptions = {
  identity: PythonRuntimeIdentity;
  source: string;
  handlerNames: readonly string[];
  capabilityProvider: ClientVisualPythonCapabilityProvider;
  workerFactory?: ClientVisualPythonWorkerFactory;
  environment?: ClientVisualPythonRuntimeEnvironment;
};

export type ClientVisualPythonCompileResult = {
  diagnostics: PythonSourceDiagnostic[];
  superseded: boolean;
};

export type ClientVisualPythonDispatchStatus =
  | 'completed'
  | 'cancelled'
  | 'timed-out'
  | 'faulted'
  | 'throttled'
  | 'coalesced'
  | 'rejected-queue-full';

export type ClientVisualPythonDispatchResult = {
  status: ClientVisualPythonDispatchStatus;
  executionId?: string;
  durationMs?: number;
  sanitizedError?: string;
};

export class ClientVisualPythonRuntimeError extends Error {
  constructor(
    public readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'ClientVisualPythonRuntimeError';
  }
}

type PendingResponse = {
  generation: number;
  expectedKinds: ReadonlySet<PythonWorkerResponse['kind']>;
  resolve(response: PythonWorkerResponse): void;
  reject(error: Error): void;
};

type CompileRequest = {
  source: string;
  resolve(result: ClientVisualPythonCompileResult): void;
  reject(error: Error): void;
};

type DispatchRequest = {
  handlerName: string;
  eventKey: string;
  payload: unknown;
  signal?: AbortSignal;
  resolve(result: ClientVisualPythonDispatchResult): void;
};

type BudgetOutcome<T> = {
  value: T;
  timedOut: boolean;
  cancelled: boolean;
};

const defaultEnvironment: ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated: () => globalThis.crossOriginIsolated === true,
  createInterruptBuffer: () => new SharedArrayBuffer(1),
  pyodideIndexUrl: () => {
    if (!globalThis.location) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_RUNTIME_LOCATION_UNAVAILABLE',
        'Browser location is required to resolve self-hosted Pyodide assets.'
      );
    }
    return new URL('/pyodide/', globalThis.location.href).toString();
  }
};

export class ClientVisualPythonRuntime implements AsyncDisposable {
  private readonly identity: PythonRuntimeIdentity;
  private readonly source: string;
  private readonly handlerNames: string[];
  private readonly capabilityProvider: ClientVisualPythonCapabilityProvider;
  private readonly workerFactory: ClientVisualPythonWorkerFactory;
  private readonly environment: ClientVisualPythonRuntimeEnvironment;
  private readonly pendingResponses = new Map<string, PendingResponse>();
  private readonly dispatchQueue: DispatchRequest[] = [];

  private worker: Worker | null = null;
  private workerGeneration = 0;
  private interruptView: Uint8Array | null = null;
  private engineReadyPromise: Promise<void> | null = null;
  private engineReadyResolve: (() => void) | null = null;
  private engineReadyReject: ((error: Error) => void) | null = null;
  private initialized = false;
  private pumping = false;
  private activeExecutionId: string | null = null;
  private activeCompile: CompileRequest | null = null;
  private queuedCompile: CompileRequest | null = null;
  private consecutiveFailures = 0;
  private disposed = false;
  private requestSequence = 0;

  constructor(options: ClientVisualPythonRuntimeOptions) {
    if (!options.identity.scriptId.trim() || !options.identity.runtimeInstanceId.trim()) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_RUNTIME_IDENTITY_REQUIRED',
        'Script and Runtime Instance identities are required.'
      );
    }
    if (!options.source.trim()) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_RUNTIME_SOURCE_REQUIRED',
        'Canonical Client Visual Python source is required.'
      );
    }

    this.identity = { ...options.identity };
    this.source = options.source;
    this.handlerNames = [...new Set(options.handlerNames)];
    this.capabilityProvider = options.capabilityProvider;
    this.workerFactory = options.workerFactory ?? (() => new Worker(
      new URL('./clientVisualPythonWorker.ts', import.meta.url),
      { type: 'module', name: `elitescada-python-${this.identity.runtimeInstanceId}` }
    ));
    this.environment = options.environment ?? defaultEnvironment;
  }

  get runtimeIdentity(): PythonRuntimeIdentity {
    return { ...this.identity };
  }

  get queuedEventCount(): number {
    return this.dispatchQueue.length;
  }

  get isThrottled(): boolean {
    return this.consecutiveFailures >= CLIENT_VISUAL_PYTHON_POLICY.maxConsecutiveFailuresBeforeThrottle;
  }

  async initialize(): Promise<void> {
    this.throwIfDisposed();
    await this.ensureInitialized();
  }

  compileSource(source: string): Promise<ClientVisualPythonCompileResult> {
    this.throwIfDisposed();

    return new Promise((resolve, reject) => {
      const request: CompileRequest = { source, resolve, reject };
      if (this.activeCompile || this.pumping) {
        if (this.queuedCompile) {
          this.queuedCompile.resolve({ diagnostics: [], superseded: true });
        }
        this.queuedCompile = request;
      } else {
        this.queuedCompile = request;
      }
      void this.pump();
    });
  }

  dispatchEvent(
    handlerName: string,
    eventKey: string,
    payload: unknown,
    signal?: AbortSignal
  ): Promise<ClientVisualPythonDispatchResult> {
    this.throwIfDisposed();

    if (!handlerName.trim() || !eventKey.trim()) {
      return Promise.resolve({
        status: 'faulted',
        sanitizedError: 'Handler name and stable event key are required.'
      });
    }
    if (this.isThrottled) return Promise.resolve({ status: 'throttled' });
    if (signal?.aborted) return Promise.resolve({ status: 'cancelled' });

    return new Promise(resolve => {
      const request: DispatchRequest = { handlerName, eventKey, payload, signal, resolve };
      const coalescedIndex = this.dispatchQueue.findIndex(item => item.eventKey === eventKey);
      if (coalescedIndex >= 0) {
        const replaced = this.dispatchQueue[coalescedIndex];
        this.dispatchQueue[coalescedIndex] = request;
        replaced.resolve({ status: 'coalesced' });
        void this.pump();
        return;
      }

      if (this.dispatchQueue.length >= CLIENT_VISUAL_PYTHON_POLICY.maxQueuedEvents) {
        resolve({ status: 'rejected-queue-full' });
        return;
      }

      this.dispatchQueue.push(request);
      void this.pump();
    });
  }

  resetThrottle(): void {
    this.throwIfDisposed();
    this.consecutiveFailures = 0;
  }

  cancelActiveExecution(): void {
    this.throwIfDisposed();
    if (!this.activeExecutionId) return;
    this.requestInterrupt(this.activeExecutionId);
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;

    while (this.dispatchQueue.length > 0) {
      this.dispatchQueue.shift()!.resolve({ status: 'cancelled' });
    }
    if (this.queuedCompile) {
      this.queuedCompile.resolve({ diagnostics: [], superseded: true });
      this.queuedCompile = null;
    }

    if (this.activeExecutionId) this.requestInterrupt(this.activeExecutionId);

    const worker = this.worker;
    if (worker) {
      const requestId = this.nextRequestId('dispose');
      const response = this.waitForResponse(requestId, ['disposed']);
      this.postBridgeRequest({
        kind: 'dispose-script',
        requestId,
        identity: { ...this.identity }
      });

      await Promise.race([
        response.catch(() => undefined),
        delay(CLIENT_VISUAL_PYTHON_POLICY.hardStopGraceMs)
      ]);
    }

    this.terminateWorker(new ClientVisualPythonRuntimeError(
      'PYTHON_RUNTIME_DISPOSED',
      'Client Visual Python runtime was disposed.'
    ));
  }

  async [Symbol.asyncDispose](): Promise<void> {
    await this.dispose();
  }

  private async pump() {
    if (this.pumping || this.disposed) return;
    this.pumping = true;

    try {
      while (!this.disposed) {
        if (this.queuedCompile) {
          const compile = this.queuedCompile;
          this.queuedCompile = null;
          this.activeCompile = compile;
          try {
            compile.resolve(await this.performCompile(compile.source));
          } catch (error) {
            compile.reject(asRuntimeError(error, 'PYTHON_COMPILE_FAILED', 'Python compile request failed.'));
          } finally {
            this.activeCompile = null;
          }
          continue;
        }

        const next = this.dispatchQueue.shift();
        if (!next) break;
        if (next.signal?.aborted) {
          next.resolve({ status: 'cancelled' });
          continue;
        }
        if (this.isThrottled) {
          next.resolve({ status: 'throttled' });
          continue;
        }

        const result = await this.performDispatch(next);
        next.resolve(result);
      }
    } finally {
      this.pumping = false;
      if (!this.disposed && (this.queuedCompile || this.dispatchQueue.length > 0)) {
        void this.pump();
      }
    }
  }

  private async performCompile(source: string): Promise<ClientVisualPythonCompileResult> {
    await this.ensureEngine();
    const requestId = this.nextRequestId('compile');
    const responsePromise = this.waitForResponse(requestId, ['compile-result', 'diagnostic']);
    this.postBridgeRequest({
      kind: 'compile-source',
      requestId,
      identity: { ...this.identity },
      source
    });

    try {
      const outcome = await this.withExecutionBudget(responsePromise);
      if (outcome.timedOut) {
        return {
          superseded: false,
          diagnostics: [timeoutDiagnostic('PYTHON_COMPILE_TIMEOUT', 'Python compilation exceeded the bounded execution budget.')]
        };
      }

      if (outcome.value.kind === 'compile-result') {
        return { diagnostics: outcome.value.diagnostics, superseded: false };
      }
      if (outcome.value.kind === 'diagnostic') {
        return { diagnostics: [outcome.value.diagnostic], superseded: false };
      }
      return { diagnostics: [], superseded: false };
    } catch (error) {
      if (error instanceof HardStopError) {
        return {
          superseded: false,
          diagnostics: [timeoutDiagnostic('PYTHON_COMPILE_HARD_STOP', 'Python compilation required terminating the sandbox Worker.')]
        };
      }
      throw error;
    }
  }

  private async performDispatch(request: DispatchRequest): Promise<ClientVisualPythonDispatchResult> {
    try {
      await this.ensureInitialized();
    } catch (error) {
      this.recordFailure();
      return {
        status: error instanceof HardStopError ? 'timed-out' : 'faulted',
        sanitizedError: 'Client Visual Python runtime could not initialize safely.'
      };
    }

    const requestId = this.nextRequestId('dispatch');
    const executionId = this.nextRequestId('execution');
    this.activeExecutionId = executionId;
    const responsePromise = this.waitForResponse(requestId, ['execution-result']);

    this.postBridgeRequest({
      kind: 'dispatch-event',
      requestId,
      executionId,
      identity: { ...this.identity },
      handlerName: request.handlerName,
      eventKey: request.eventKey,
      payload: request.payload,
      deadlineEpochMs: Date.now() + CLIENT_VISUAL_PYTHON_POLICY.handlerTimeoutMs
    });

    try {
      const outcome = await this.withExecutionBudget(responsePromise, executionId, request.signal);
      const response = outcome.value;
      if (response.kind !== 'execution-result') {
        this.recordFailure();
        return { status: 'faulted', sanitizedError: 'Sandbox returned an invalid execution response.' };
      }

      const status: ClientVisualPythonDispatchStatus = outcome.timedOut
        ? 'timed-out'
        : outcome.cancelled
          ? 'cancelled'
          : response.status;

      this.recordExecutionStatus(status);
      return {
        status,
        executionId,
        durationMs: response.durationMs,
        sanitizedError: response.sanitizedError
      };
    } catch (error) {
      if (error instanceof HardStopError) {
        const status: ClientVisualPythonDispatchStatus = error.reason === 'cancelled' ? 'cancelled' : 'timed-out';
        this.recordExecutionStatus(status);
        return {
          status,
          executionId,
          sanitizedError: status === 'timed-out'
            ? 'Python handler exceeded its execution budget and the sandbox Worker was terminated.'
            : undefined
        };
      }

      this.recordFailure();
      return {
        status: 'faulted',
        executionId,
        sanitizedError: 'Python handler failed with a sanitized runtime fault.'
      };
    } finally {
      if (this.activeExecutionId === executionId) this.activeExecutionId = null;
    }
  }

  private async ensureInitialized(): Promise<void> {
    if (this.initialized) return;
    await this.ensureEngine();

    const requestId = this.nextRequestId('initialize');
    const responsePromise = this.waitForResponse(requestId, ['ready', 'diagnostic']);
    this.postBridgeRequest({
      kind: 'initialize-script',
      requestId,
      identity: { ...this.identity },
      source: this.source,
      handlerNames: [...this.handlerNames]
    });

    const outcome = await this.withExecutionBudget(responsePromise);
    if (outcome.timedOut) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_INITIALIZE_TIMEOUT',
        'Client Visual Python initialization exceeded its bounded execution budget.'
      );
    }
    if (outcome.value.kind === 'diagnostic') {
      throw new ClientVisualPythonRuntimeError(
        outcome.value.diagnostic.code,
        outcome.value.diagnostic.message
      );
    }
    if (outcome.value.kind !== 'ready') {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_INITIALIZE_INVALID_RESPONSE',
        'Client Visual Python Worker returned an invalid initialization response.'
      );
    }

    this.initialized = true;
  }

  private async ensureEngine(): Promise<void> {
    this.throwIfDisposed();
    if (this.engineReadyPromise) return await this.engineReadyPromise;

    if (!this.environment.isCrossOriginIsolated()) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_CROSS_ORIGIN_ISOLATION_REQUIRED',
        'Client Visual Python requires a cross-origin-isolated Runtime Client.'
      );
    }
    if (typeof SharedArrayBuffer === 'undefined') {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_SHARED_ARRAY_BUFFER_REQUIRED',
        'Client Visual Python interruption requires SharedArrayBuffer support.'
      );
    }

    const worker = this.workerFactory();
    const generation = ++this.workerGeneration;
    this.worker = worker;
    this.interruptView = new Uint8Array(this.environment.createInterruptBuffer());
    if (this.interruptView.byteLength < 1) {
      this.terminateWorker(new ClientVisualPythonRuntimeError(
        'PYTHON_INTERRUPT_BUFFER_INVALID',
        'Client Visual Python interrupt buffer is invalid.'
      ));
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_INTERRUPT_BUFFER_INVALID',
        'Client Visual Python interrupt buffer is invalid.'
      );
    }

    worker.addEventListener('message', event => {
      void this.handleWorkerMessage(event as MessageEvent<ClientVisualPythonPrivateWorkerResponse>, generation);
    });
    worker.addEventListener('error', () => {
      if (generation !== this.workerGeneration || this.disposed) return;
      this.terminateWorker(new ClientVisualPythonRuntimeError(
        'PYTHON_WORKER_FAULT',
        'Client Visual Python Worker failed.'
      ));
    });

    this.engineReadyPromise = new Promise<void>((resolve, reject) => {
      this.engineReadyResolve = resolve;
      this.engineReadyReject = reject;
    });

    const bootstrap: ClientVisualPythonPrivateWorkerRequest = {
      kind: 'engine-bootstrap',
      generation,
      identity: { ...this.identity },
      pyodideIndexUrl: this.environment.pyodideIndexUrl(),
      interruptBuffer: this.interruptView.buffer as SharedArrayBuffer
    };
    worker.postMessage(bootstrap);

    return await this.engineReadyPromise;
  }

  private async handleWorkerMessage(
    event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>,
    receivedGeneration: number
  ) {
    if (receivedGeneration !== this.workerGeneration) return;
    const payload = event.data;

    if (payload.kind === 'engine-ready') {
      if (payload.generation !== receivedGeneration ||
          !hasMatchingPythonRuntimeIdentity(this.identity, payload.identity)) return;
      this.engineReadyResolve?.();
      this.engineReadyResolve = null;
      this.engineReadyReject = null;
      return;
    }

    if (payload.kind === 'engine-bootstrap-failed') {
      if (payload.generation !== receivedGeneration ||
          !hasMatchingPythonRuntimeIdentity(this.identity, payload.identity)) return;
      this.engineReadyReject?.(new ClientVisualPythonRuntimeError(
        'PYTHON_ENGINE_BOOTSTRAP_FAILED',
        payload.sanitizedError
      ));
      this.terminateWorker(new ClientVisualPythonRuntimeError(
        'PYTHON_ENGINE_BOOTSTRAP_FAILED',
        payload.sanitizedError
      ));
      return;
    }

    if (payload.bridgeVersion !== CLIENT_VISUAL_PYTHON_BRIDGE_VERSION) return;
    const message = payload.message;
    if (!hasMatchingPythonRuntimeIdentity(this.identity, message.identity)) return;

    if (message.kind === 'api-request') {
      await this.handleCapabilityRequest(message, receivedGeneration);
      return;
    }

    const pending = this.pendingResponses.get(message.requestId);
    if (!pending || pending.generation !== receivedGeneration || !pending.expectedKinds.has(message.kind)) return;
    this.pendingResponses.delete(message.requestId);
    pending.resolve(message);
  }

  private async handleCapabilityRequest(
    message: Extract<PythonWorkerResponse, { kind: 'api-request' }>,
    receivedGeneration: number
  ) {
    if (this.disposed ||
        receivedGeneration !== this.workerGeneration ||
        !this.activeExecutionId ||
        message.executionId !== this.activeExecutionId) {
      return;
    }

    try {
      const value = await dispatchClientVisualPythonCapability(
        this.capabilityProvider,
        message.capability,
        message.operation,
        message.arguments,
        {
          ...this.identity,
          executionId: message.executionId
        }
      );

      if (this.disposed ||
          receivedGeneration !== this.workerGeneration ||
          message.executionId !== this.activeExecutionId) return;

      this.postBridgeRequest({
        kind: 'api-response',
        requestId: message.requestId,
        identity: { ...this.identity },
        ok: true,
        value
      });
    } catch {
      if (this.disposed ||
          receivedGeneration !== this.workerGeneration ||
          message.executionId !== this.activeExecutionId) return;

      this.postBridgeRequest({
        kind: 'api-response',
        requestId: message.requestId,
        identity: { ...this.identity },
        ok: false,
        error: 'Client Visual capability request failed.'
      });
    }
  }

  private waitForResponse(
    requestId: string,
    expectedKinds: readonly PythonWorkerResponse['kind'][]
  ): Promise<PythonWorkerResponse> {
    const generation = this.workerGeneration;
    return new Promise((resolve, reject) => {
      this.pendingResponses.set(requestId, {
        generation,
        expectedKinds: new Set(expectedKinds),
        resolve,
        reject
      });
    });
  }

  private postBridgeRequest(message: PythonWorkerRequest) {
    const worker = this.worker;
    if (!worker) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_WORKER_UNAVAILABLE',
        'Client Visual Python Worker is unavailable.'
      );
    }

    const envelope: PythonWorkerEnvelope<PythonWorkerRequest> = {
      bridgeVersion: CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
      message
    };
    worker.postMessage(envelope);
  }

  private async withExecutionBudget<T>(
    promise: Promise<T>,
    executionId?: string,
    signal?: AbortSignal
  ): Promise<BudgetOutcome<T>> {
    let timedOut = false;
    let cancelled = false;
    let settled = false;
    let interruptTimer: ReturnType<typeof setTimeout> | undefined;
    let hardStopTimer: ReturnType<typeof setTimeout> | undefined;
    let hardReject: ((error: Error) => void) | undefined;

    const hardStop = new Promise<never>((_, reject) => { hardReject = reject; });
    const beginInterrupt = (reason: 'timed-out' | 'cancelled') => {
      if (settled || timedOut || cancelled) return;
      timedOut = reason === 'timed-out';
      cancelled = reason === 'cancelled';
      this.requestInterrupt(executionId);
      hardStopTimer = setTimeout(() => {
        const error = new HardStopError(reason);
        this.terminateWorker(error);
        hardReject?.(error);
      }, CLIENT_VISUAL_PYTHON_POLICY.hardStopGraceMs);
    };

    const abort = () => beginInterrupt('cancelled');
    signal?.addEventListener('abort', abort, { once: true });
    interruptTimer = setTimeout(
      () => beginInterrupt('timed-out'),
      CLIENT_VISUAL_PYTHON_POLICY.handlerTimeoutMs
    );

    try {
      const value = await Promise.race([promise, hardStop]);
      settled = true;
      return { value, timedOut, cancelled };
    } finally {
      settled = true;
      if (interruptTimer) clearTimeout(interruptTimer);
      if (hardStopTimer) clearTimeout(hardStopTimer);
      signal?.removeEventListener('abort', abort);
    }
  }

  private requestInterrupt(executionId?: string) {
    if (this.interruptView) this.interruptView[0] = 2;
    if (!executionId || !this.worker) return;

    const envelope: PythonWorkerEnvelope<PythonWorkerRequest> = {
      bridgeVersion: CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
      message: {
        kind: 'cancel-execution',
        requestId: this.nextRequestId('cancel'),
        executionId,
        identity: { ...this.identity }
      }
    };
    this.worker.postMessage(envelope);
  }

  private terminateWorker(error: Error) {
    const worker = this.worker;
    this.worker = null;
    this.initialized = false;
    this.interruptView = null;
    this.engineReadyPromise = null;
    this.engineReadyResolve = null;

    this.engineReadyReject?.(error);
    this.engineReadyReject = null;

    for (const [requestId, pending] of this.pendingResponses) {
      if (pending.generation !== this.workerGeneration) continue;
      this.pendingResponses.delete(requestId);
      pending.reject(error);
    }

    worker?.terminate();
  }

  private recordExecutionStatus(status: ClientVisualPythonDispatchStatus) {
    if (status === 'completed') {
      this.consecutiveFailures = 0;
      return;
    }
    if (status === 'faulted' || status === 'timed-out') this.recordFailure();
  }

  private recordFailure() {
    this.consecutiveFailures = Math.min(
      CLIENT_VISUAL_PYTHON_POLICY.maxConsecutiveFailuresBeforeThrottle,
      this.consecutiveFailures + 1
    );
  }

  private nextRequestId(prefix: string): string {
    return `${prefix}-${this.workerGeneration}-${++this.requestSequence}`;
  }

  private throwIfDisposed() {
    if (this.disposed) {
      throw new ClientVisualPythonRuntimeError(
        'PYTHON_RUNTIME_DISPOSED',
        'Client Visual Python runtime is disposed.'
      );
    }
  }
}

class HardStopError extends Error {
  constructor(public readonly reason: 'timed-out' | 'cancelled') {
    super(`Client Visual Python Worker hard-stopped after ${reason}.`);
    this.name = 'HardStopError';
  }
}

function timeoutDiagnostic(code: string, message: string): PythonSourceDiagnostic {
  return { severity: 'error', code, message, line: 1, column: 1 };
}

function asRuntimeError(error: unknown, code: string, fallback: string): Error {
  if (error instanceof Error) return error;
  return new ClientVisualPythonRuntimeError(code, fallback);
}

function delay(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}
