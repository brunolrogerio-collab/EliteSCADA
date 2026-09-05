import { expect, test, type Page } from '@playwright/test';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonRuntimeEnvironment
} from '../src/python-runtime/clientVisualPythonRuntime';
import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  type PythonRuntimeIdentity,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from '../src/python-runtime/pythonRuntimeContracts';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse
} from '../src/python-runtime/clientVisualPythonWorkerTransport';

const clientMemoryDefinitions = [
  {
    dataSourceKey: 'client-python-e2e',
    name: 'Client Python E2E',
    tags: [
      {
        id: '77777777-7777-7777-7777-777777777777',
        name: 'Sandbox Value',
        path: 'Client.SandboxValue',
        dataType: 'Int32',
        readOnly: false,
        initialValue: 7
      }
    ]
  }
];

async function installClientMemoryFixture(page: Page) {
  await page.route('**/api/internal-memory/client/definitions', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(clientMemoryDefinitions)
    });
  });
}

test('real Pyodide sandbox boots same-origin, exposes only intended capabilities and preserves client-local state', async ({ page }) => {
  test.slow();
  await installClientMemoryFixture(page);
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const runtimeModule = await importModule('/src/python-runtime/clientVisualPythonRuntime.ts');
    const providerModule = await importModule('/src/python-runtime/createClientVisualPythonCapabilityProvider.ts');
    const memoryModule = await importModule('/src/runtime/clientMemory.ts');

    const tagsResponse = await fetch('/api/tags');
    if (!tagsResponse.ok) throw new Error(`TAG list failed with HTTP ${tagsResponse.status}.`);
    const tags = await tagsResponse.json() as Array<{ path?: string }>;
    const tagPath = tags.find(tag => typeof tag.path === 'string' && tag.path.trim())?.path;
    if (!tagPath) throw new Error('No Runtime TAG was available for Client Visual Python acceptance.');

    const storeA = new memoryModule.ClientMemoryStore();
    const storeB = new memoryModule.ClientMemoryStore();
    await storeA.initialize();
    await storeB.initialize();

    const source = `
import elite_scada


def sandbox_probe(event):
    import js
    denied_js = (
        "fetch", "XMLHttpRequest", "WebSocket", "EventSource",
        "indexedDB", "caches", "document", "localStorage", "sessionStorage",
        "globalThis"
    )
    exposed_js = [name for name in denied_js if hasattr(js, name)]
    if exposed_js:
        raise RuntimeError("Denied JavaScript authority is visible")

    denied_bridge = (
        "server_memory_read", "server_memory_write",
        "shared_tag_write", "filesystem", "shell", "database", "driver",
        "credential", "fetch"
    )
    exposed_bridge = [name for name in denied_bridge if hasattr(elite_scada, name)]
    if exposed_bridge:
        raise RuntimeError("Denied EliteSCADA capability is visible")
    if not hasattr(elite_scada, "tag_write"):
        raise RuntimeError("Official TAG write capability is not visible")

    try:
        import micropip
    except ImportError:
        pass
    else:
        raise RuntimeError("micropip is available to Client Visual Python")

    try:
        import pyodide.http
    except ImportError:
        pass
    else:
        raise RuntimeError("pyodide.http is available to Client Visual Python")


def run_js_escape_probe(event):
    try:
        from pyodide.code import run_js
        exposed = run_js("typeof globalThis")
    except BaseException:
        return None
    if str(exposed) == "object":
        raise RuntimeError("pyodide.code.run_js exposed JavaScript global scope")


async def tag_probe(event):
    snapshot = await elite_scada.tag_read(${JSON.stringify(tagPath)})
    if snapshot is None:
        raise RuntimeError("TAG snapshot was not returned")


async def memory_probe(event):
    before = await elite_scada.client_memory_read("Client.SandboxValue")
    if before != 7:
        raise RuntimeError("Unexpected Client Memory initial value")
    await elite_scada.client_memory_write("Client.SandboxValue", 41)
    after = await elite_scada.client_memory_read("Client.SandboxValue")
    if after != 41:
        raise RuntimeError("Client Memory write did not remain visible to the owning client")


def fault_probe(event):
    raise ValueError("LEAK-ME /private/secret token=abc123")
`;

    const runtime = new runtimeModule.ClientVisualPythonRuntime({
      identity: {
        scriptId: '88888888-8888-8888-8888-888888888888',
        runtimeInstanceId: 'dynamic-sandbox-a'
      },
      source,
      handlerNames: ['sandbox_probe', 'run_js_escape_probe', 'tag_probe', 'memory_probe', 'fault_probe'],
      capabilityProvider: providerModule.createClientVisualPythonCapabilityProvider({ memoryStore: storeA })
    });

    let otherRuntime: any = null;
    try {
      const pyodideAsset = await fetch('/pyodide/pyodide.mjs');
      await runtime.initialize();

      const compileOk = await runtime.compileSource('def valid():\n    return 1\n');
      const compileBad = await runtime.compileSource('def broken(:\n    return 1\n');
      const sandboxProbe = await runtime.dispatchEvent('sandbox_probe', 'security:surface', null);
      const tagProbe = await runtime.dispatchEvent('tag_probe', 'capability:tag-read', null);
      const memoryProbe = await runtime.dispatchEvent('memory_probe', 'capability:client-memory', null);

      const runJsEscapeProbe = await runtime.dispatchEvent('run_js_escape_probe', 'security:run-js', null);
      runtime.resetThrottle();

      const faultResults = [];
      for (let index = 0; index < 5; index++) {
        faultResults.push(await runtime.dispatchEvent('fault_probe', `fault:${index}`, null));
      }
      const throttled = await runtime.dispatchEvent('fault_probe', 'fault:throttled', null);

      otherRuntime = new runtimeModule.ClientVisualPythonRuntime({
        identity: {
          scriptId: '99999999-9999-9999-9999-999999999999',
          runtimeInstanceId: 'dynamic-sandbox-b'
        },
        source: 'def ok(event):\n    return None\n',
        handlerNames: ['ok'],
        capabilityProvider: {}
      });
      const otherRuntimeResult = await otherRuntime.dispatchEvent('ok', 'isolation:healthy-script', null);
      const health = await fetch('/health');

      return {
        crossOriginIsolated: globalThis.crossOriginIsolated === true,
        pyodideAssetStatus: pyodideAsset.status,
        pyodideAssetSameOrigin: new URL('/pyodide/pyodide.mjs', location.href).origin === location.origin,
        tagPath,
        compileOk,
        compileBad,
        sandboxProbe,
        tagProbe,
        memoryProbe,
        runJsEscapeProbe,
        memoryA: storeA.read('Client.SandboxValue'),
        memoryB: storeB.read('Client.SandboxValue'),
        faultStatuses: faultResults.map((item: { status: string }) => item.status),
        faultErrors: faultResults.map((item: { sanitizedError?: string }) => item.sanitizedError ?? ''),
        throttled: throttled.status,
        otherRuntime: otherRuntimeResult.status,
        healthStatus: health.status
      };
    } finally {
      if (otherRuntime) await otherRuntime.dispose();
      await runtime.dispose();
    }
  });

  expect(result.crossOriginIsolated).toBe(true);
  expect(result.pyodideAssetStatus).toBe(200);
  expect(result.pyodideAssetSameOrigin).toBe(true);

  expect(result.compileOk).toEqual({ diagnostics: [], superseded: false });
  expect(result.compileBad.superseded).toBe(false);
  expect(result.compileBad.diagnostics).toHaveLength(1);
  expect(result.compileBad.diagnostics[0]).toMatchObject({
    severity: 'error',
    code: 'PYTHON_SYNTAX_ERROR',
    line: 1
  });
  expect(result.compileBad.diagnostics[0].column).toBeGreaterThanOrEqual(1);

  expect(result.sandboxProbe.status).toBe('completed');
  expect(result.tagProbe.status).toBe('completed');
  expect(result.memoryProbe.status).toBe('completed');
  expect(result.memoryA).toBe(41);
  expect(result.memoryB).toBe(7);

  // Security regression guard: Client Visual Python must not recover arbitrary Worker JavaScript scope.
  expect(result.runJsEscapeProbe.status).toBe('completed');

  expect(result.faultStatuses).toEqual(['faulted', 'faulted', 'faulted', 'faulted', 'faulted']);
  for (const error of result.faultErrors) {
    expect(error).toContain('ValueError');
    expect(error).not.toContain('LEAK-ME');
    expect(error).not.toContain('/private/secret');
    expect(error).not.toContain('abc123');
  }
  expect(result.throttled).toBe('throttled');
  expect(result.otherRuntime).toBe('completed');
  expect(result.healthStatus).toBe(200);
});

test('real Pyodide execution stays bounded across timeout, cancellation, queue flood and disposal', async ({ page }) => {
  test.slow();
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const runtimeModule = await importModule('/src/python-runtime/clientVisualPythonRuntime.ts');

    const runtime = new runtimeModule.ClientVisualPythonRuntime({
      identity: {
        scriptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        runtimeInstanceId: 'dynamic-bounded-runtime'
      },
      source: 'def loop(event):\n    while True:\n        pass\n',
      handlerNames: ['loop'],
      capabilityProvider: {}
    });

    await runtime.initialize();

    const timeoutStarted = performance.now();
    const timedOut = await runtime.dispatchEvent('loop', 'timeout:infinite-loop', null);
    const timeoutElapsedMs = performance.now() - timeoutStarted;

    const compileAfterTimeout = await runtime.compileSource('def recovered():\n    return 1\n');

    const cancellation = new AbortController();
    const cancelStarted = performance.now();
    const cancelling = runtime.dispatchEvent('loop', 'cancel:explicit', null, cancellation.signal);
    setTimeout(() => cancellation.abort(), 20);
    const cancelled = await cancelling;
    const cancelElapsedMs = performance.now() - cancelStarted;

    const active = runtime.dispatchEvent('loop', 'queue:active', null);
    await new Promise(resolve => setTimeout(resolve, 20));

    const queued = Array.from({ length: 128 }, (_, index) =>
      runtime.dispatchEvent('loop', `queue:${index}`, { index })
    );
    const queuedCountAtCapacity = runtime.queuedEventCount;

    const replacement = runtime.dispatchEvent('loop', 'queue:7', { index: 700 });
    const coalesced = await queued[7];
    const queuedCountAfterCoalesce = runtime.queuedEventCount;
    const overflow = await runtime.dispatchEvent('loop', 'queue:overflow', null);

    const disposeStarted = performance.now();
    await runtime.dispose();
    const disposeElapsedMs = performance.now() - disposeStarted;

    const activeResult = await active;
    const replacementResult = await replacement;
    const queuedStatuses = await Promise.all(queued.map(async promise => (await promise).status));

    let postDisposeCode = '';
    try {
      await runtime.compileSource('x = 1\n');
    } catch (error) {
      postDisposeCode = error && typeof error === 'object' && 'code' in error
        ? String((error as { code?: unknown }).code ?? '')
        : '';
    }

    return {
      timedOut,
      timeoutElapsedMs,
      compileAfterTimeout,
      cancelled,
      cancelElapsedMs,
      queuedCountAtCapacity,
      coalesced,
      queuedCountAfterCoalesce,
      overflow,
      disposeElapsedMs,
      activeResult,
      replacementResult,
      queuedStatuses,
      postDisposeCode
    };
  });

  expect(result.timedOut.status).toBe('timed-out');
  expect(result.timeoutElapsedMs).toBeLessThan(1_500);
  expect(result.compileAfterTimeout).toEqual({ diagnostics: [], superseded: false });

  expect(result.cancelled.status).toBe('cancelled');
  expect(result.cancelElapsedMs).toBeLessThan(1_000);

  expect(result.queuedCountAtCapacity).toBe(128);
  expect(result.coalesced.status).toBe('coalesced');
  expect(result.queuedCountAfterCoalesce).toBe(128);
  expect(result.overflow.status).toBe('rejected-queue-full');

  expect(result.disposeElapsedMs).toBeLessThan(1_000);
  expect(['cancelled', 'faulted', 'timed-out']).toContain(result.activeResult.status);
  expect(result.replacementResult.status).toBe('cancelled');
  expect(result.queuedStatuses.filter(status => status === 'coalesced')).toHaveLength(1);
  expect(result.queuedStatuses.filter(status => status === 'cancelled')).toHaveLength(127);
  expect(result.postDisposeCode).toBe('PYTHON_RUNTIME_DISPOSED');
});

const generationIdentity: PythonRuntimeIdentity = {
  scriptId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  runtimeInstanceId: 'generation-runtime'
};

const generationEnvironment: ClientVisualPythonRuntimeEnvironment = {
  isCrossOriginIsolated: () => true,
  createInterruptBuffer: () => new SharedArrayBuffer(1),
  pyodideIndexUrl: () => 'http://127.0.0.1:5173/pyodide/'
};

class GenerationWorker {
  private readonly messageListeners = new Set<(event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void>();
  compileRequest: Extract<PythonWorkerRequest, { kind: 'compile-source' }> | null = null;
  terminated = false;

  constructor(private readonly hangDispatch: boolean) {}

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
    if (message.kind === 'initialize-script') {
      queueMicrotask(() => this.emitBridge({
        kind: 'ready',
        requestId: message.requestId,
        identity: message.identity
      }));
      return;
    }
    if (message.kind === 'dispatch-event' && !this.hangDispatch) {
      queueMicrotask(() => this.emitBridge({
        kind: 'execution-result',
        requestId: message.requestId,
        executionId: message.executionId,
        identity: message.identity,
        status: 'completed',
        durationMs: 1
      }));
      return;
    }
    if (message.kind === 'compile-source') {
      this.compileRequest = message;
      return;
    }
    if (message.kind === 'dispose-script') {
      queueMicrotask(() => this.emitBridge({
        kind: 'disposed',
        requestId: message.requestId,
        identity: message.identity
      }));
    }
  }

  terminate(): void {
    this.terminated = true;
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type !== 'message') return;
    this.messageListeners.add(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (type !== 'message') return;
    this.messageListeners.delete(listener as (event: MessageEvent<ClientVisualPythonPrivateWorkerResponse>) => void);
  }

  emitCompile(request: Extract<PythonWorkerRequest, { kind: 'compile-source' }>, diagnostics: any[]) {
    this.emitBridge({
      kind: 'compile-result',
      requestId: request.requestId,
      identity: request.identity,
      diagnostics
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

test('stale response from a hard-stopped worker generation cannot satisfy a replacement request', async () => {
  const first = new GenerationWorker(true);
  const second = new GenerationWorker(false);
  const workers = [first, second];
  const runtime = new ClientVisualPythonRuntime({
    identity: generationIdentity,
    source: 'def on_event(event):\n    return None\n',
    handlerNames: ['on_event'],
    capabilityProvider: {},
    workerFactory: () => workers.shift()! as unknown as Worker,
    environment: generationEnvironment
  });

  const timedOut = await runtime.dispatchEvent('on_event', 'generation:timeout', null);
  expect(timedOut.status).toBe('timed-out');
  expect(first.terminated).toBe(true);

  const compilePromise = runtime.compileSource('def replacement():\n    return 1\n');
  await waitUntil(() => second.compileRequest !== null);
  const request = second.compileRequest!;

  let settled = false;
  void compilePromise.then(() => { settled = true; });
  first.emitCompile(request, [{
    severity: 'error',
    code: 'STALE_RESULT',
    message: 'stale',
    line: 1,
    column: 1
  }]);
  await new Promise(resolve => setTimeout(resolve, 0));
  expect(settled).toBe(false);

  second.emitCompile(request, []);
  await expect(compilePromise).resolves.toEqual({ diagnostics: [], superseded: false });
  await runtime.dispose();
});

async function waitUntil(predicate: () => boolean) {
  const deadline = Date.now() + 1_000;
  while (!predicate()) {
    if (Date.now() > deadline) throw new Error('Timed out waiting for test Worker state.');
    await new Promise(resolve => setTimeout(resolve, 1));
  }
}
