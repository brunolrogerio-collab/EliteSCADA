import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  hasMatchingPythonRuntimeIdentity,
  type ClientVisualPythonCapability,
  type PythonRuntimeIdentity,
  type PythonSourceDiagnostic,
  type PythonWorkerEnvelope,
  type PythonWorkerRequest,
  type PythonWorkerResponse
} from './pythonRuntimeContracts';
import type {
  ClientVisualPythonPrivateWorkerRequest,
  ClientVisualPythonPrivateWorkerResponse,
  PythonEngineBootstrapRequest
} from './clientVisualPythonWorkerTransport';

type PyodideGlobals = {
  set(key: string, value: unknown): void;
  get(key: string): unknown;
  delete(key: string): boolean;
};

type PyodideLike = {
  globals: PyodideGlobals;
  runPython(code: string, options?: { globals?: unknown }): unknown;
  runPythonAsync(code: string, options?: { globals?: unknown }): Promise<unknown>;
  registerJsModule(name: string, module: Record<string, unknown>): void;
  setInterruptBuffer(buffer: Uint8Array): void;
};

type PyodideModule = {
  loadPyodide(options: { indexURL: string; jsglobals: object }): Promise<PyodideLike>;
};

type PythonProxyLike = {
  toJs?: (options?: { dict_converter?: (entries: Iterable<readonly [PropertyKey, unknown]>) => unknown }) => unknown;
  destroy?: () => void;
};

type ActiveExecution = {
  requestId: string;
  executionId: string;
  identity: PythonRuntimeIdentity;
};

type PendingApiRequest = {
  resolve(value: unknown): void;
  reject(error: Error): void;
  executionId: string;
};

const workerScope = globalThis as typeof globalThis & {
  postMessage(message: ClientVisualPythonPrivateWorkerResponse): void;
  location: Location;
};

let pyodide: PyodideLike | null = null;
let runtimeGlobals: unknown = null;
let identity: PythonRuntimeIdentity | null = null;
let generation = 0;
let interruptView: Uint8Array | null = null;
let activeExecution: ActiveExecution | null = null;
let disposed = false;
let apiSequence = 0;
const pendingApiRequests = new Map<string, PendingApiRequest>();

workerScope.addEventListener('message', event => {
  void handleMessage(event as MessageEvent<ClientVisualPythonPrivateWorkerRequest>);
});

async function handleMessage(event: MessageEvent<ClientVisualPythonPrivateWorkerRequest>) {
  const payload = event.data;

  if (isBootstrapRequest(payload)) {
    await bootstrap(payload);
    return;
  }

  if (!isBridgeEnvelope(payload)) return;
  if (payload.bridgeVersion !== CLIENT_VISUAL_PYTHON_BRIDGE_VERSION) return;

  const message = payload.message;
  if (!identity || !hasMatchingPythonRuntimeIdentity(identity, message.identity)) return;

  switch (message.kind) {
    case 'initialize-script':
      await initializeScript(message);
      return;
    case 'compile-source':
      await compileSource(message);
      return;
    case 'dispatch-event':
      await dispatchEvent(message);
      return;
    case 'api-response':
      handleApiResponse(message);
      return;
    case 'cancel-execution':
      cancelExecution(message.executionId);
      return;
    case 'dispose-script':
      disposeScript(message.requestId);
      return;
  }
}

function isBootstrapRequest(value: ClientVisualPythonPrivateWorkerRequest): value is PythonEngineBootstrapRequest {
  return typeof value === 'object' && value !== null && 'kind' in value && value.kind === 'engine-bootstrap';
}

function isBridgeEnvelope(
  value: ClientVisualPythonPrivateWorkerRequest
): value is PythonWorkerEnvelope<PythonWorkerRequest> {
  return typeof value === 'object' && value !== null && 'bridgeVersion' in value && 'message' in value;
}

async function bootstrap(request: PythonEngineBootstrapRequest) {
  if (pyodide || disposed) return;

  identity = { ...request.identity };
  generation = request.generation;
  interruptView = new Uint8Array(request.interruptBuffer);

  try {
    const indexUrl = requireSameOriginPyodideUrl(request.pyodideIndexUrl);
    const moduleUrl = new URL('pyodide.mjs', indexUrl).toString();
    const module = await import(/* @vite-ignore */ moduleUrl) as PyodideModule;

    pyodide = await module.loadPyodide({
      indexURL: indexUrl.toString(),
      jsglobals: Object.freeze({})
    });
    pyodide.setInterruptBuffer(interruptView);

    disableDeniedBrowserAuthorities();
    installDeniedPythonImportGuard();
    pyodide.registerJsModule('elite_scada', createEliteScadaBridge());

    workerScope.postMessage({
      kind: 'engine-ready',
      generation,
      identity: { ...identity }
    });
  } catch {
    workerScope.postMessage({
      kind: 'engine-bootstrap-failed',
      generation,
      identity: { ...request.identity },
      sanitizedError: 'Client Visual Python engine could not be initialized safely.'
    });
  }
}

function requireSameOriginPyodideUrl(value: string): URL {
  const url = new URL(value, workerScope.location.href);
  if (url.origin !== workerScope.location.origin) {
    throw new Error('Cross-origin Pyodide assets are not allowed.');
  }
  if (!url.pathname.endsWith('/')) url.pathname += '/';
  url.search = '';
  url.hash = '';
  return url;
}

function disableDeniedBrowserAuthorities() {
  const deniedNetwork = () => Promise.reject(new Error('Arbitrary network access is denied.'));
  const deniedConstructor = function deniedBrowserAuthority() {
    throw new Error('Browser authority is denied in Client Visual Python.');
  };

  lockGlobal('fetch', deniedNetwork);
  lockGlobal('XMLHttpRequest', deniedConstructor);
  lockGlobal('WebSocket', deniedConstructor);
  lockGlobal('EventSource', deniedConstructor);
  lockGlobal('indexedDB', undefined);
  lockGlobal('caches', undefined);
}

function lockGlobal(name: string, value: unknown) {
  try {
    Object.defineProperty(globalThis, name, {
      configurable: false,
      enumerable: false,
      writable: false,
      value
    });
  } catch {
    throw new Error(`Denied browser authority '${name}' could not be isolated.`);
  }
}

function installDeniedPythonImportGuard() {
  requirePyodide().runPython(`
import sys

class _EliteScadaDeniedImportFinder:
    _blocked = ("micropip", "pyodide", "pyodide_js")

    def find_spec(self, fullname, path=None, target=None):
        if any(fullname == item or fullname.startswith(item + ".") for item in self._blocked):
            raise ImportError("Module is unavailable in the EliteSCADA Client Visual sandbox.")
        return None

for _module_name in tuple(sys.modules):
    if any(_module_name == item or _module_name.startswith(item + ".") for item in _EliteScadaDeniedImportFinder._blocked):
        sys.modules.pop(_module_name, None)

sys.meta_path.insert(0, _EliteScadaDeniedImportFinder())
`);
}

function createEliteScadaBridge(): Record<string, unknown> {
  return Object.freeze({
    tag_read: (reference: unknown) => requestCapability('tag.read', 'read', { reference: normalizeBridgeValue(reference) }),
    client_memory_read: (reference: unknown) => requestCapability('clientMemory.read', 'read', { reference: normalizeBridgeValue(reference) }),
    client_memory_write: (reference: unknown, value: unknown) => requestCapability('clientMemory.write', 'write', {
      reference: normalizeBridgeValue(reference),
      value: normalizeBridgeValue(value)
    }),
    visual_property_read: (targetReference: unknown, propertyKey: unknown) => requestCapability('visualProperty.read', 'read', {
      targetReference: normalizeBridgeValue(targetReference),
      propertyKey: normalizeBridgeValue(propertyKey)
    }),
    visual_property_write: (targetReference: unknown, propertyKey: unknown, value: unknown) => requestCapability('visualProperty.write', 'write', {
      targetReference: normalizeBridgeValue(targetReference),
      propertyKey: normalizeBridgeValue(propertyKey),
      value: normalizeBridgeValue(value)
    }),
    visual_tween_request: (argumentsValue: unknown) => requestCapability('visualTween.request', 'request', normalizeBridgeValue(argumentsValue)),
    backend_operation_request: (operation: unknown, argumentsValue: unknown) => requestCapability(
      'backendOperation.request',
      String(normalizeBridgeValue(operation)),
      normalizeBridgeValue(argumentsValue)
    )
  });
}

async function requestCapability(
  capability: ClientVisualPythonCapability,
  operation: string,
  argumentsValue: unknown
): Promise<unknown> {
  if (!CLIENT_VISUAL_PYTHON_CAPABILITIES.includes(capability)) {
    throw new Error('Capability is not part of Client Visual Python bridge v1.');
  }
  if (!activeExecution || !identity || disposed) {
    throw new Error('Client Visual Python API calls require an active script execution.');
  }

  const requestId = `api-${generation}-${++apiSequence}`;
  const executionId = activeExecution.executionId;
  const promise = new Promise<unknown>((resolve, reject) => {
    pendingApiRequests.set(requestId, { resolve, reject, executionId });
  });

  postBridgeResponse({
    kind: 'api-request',
    requestId,
    executionId,
    identity: { ...identity },
    capability,
    operation,
    arguments: argumentsValue
  });

  return await promise;
}

function handleApiResponse(message: Extract<PythonWorkerRequest, { kind: 'api-response' }>) {
  const pending = pendingApiRequests.get(message.requestId);
  if (!pending || !activeExecution || pending.executionId !== activeExecution.executionId) return;

  pendingApiRequests.delete(message.requestId);
  if (message.ok) pending.resolve(message.value ?? null);
  else pending.reject(new Error('Requested EliteSCADA capability failed.'));
}

async function compileSource(message: Extract<PythonWorkerRequest, { kind: 'compile-source' }>) {
  const diagnostics = getCompileDiagnostics(message.source);
  postBridgeResponse({
    kind: 'compile-result',
    requestId: message.requestId,
    identity: { ...message.identity },
    diagnostics
  });
}

function getCompileDiagnostics(source: string): PythonSourceDiagnostic[] {
  const engine = requirePyodide();
  engine.globals.set('__elitescada_compile_source', source);

  try {
    const raw = engine.runPython(`
import json
try:
    compile(__elitescada_compile_source, "<EliteSCADA Script>", "exec")
    json.dumps([])
except SyntaxError as exc:
    json.dumps([{
        "severity": "error",
        "code": "PYTHON_SYNTAX_ERROR",
        "message": str(exc.msg or "Invalid Python syntax."),
        "line": max(1, int(exc.lineno or 1)),
        "column": max(1, int(exc.offset or 1)),
        "endLine": max(1, int(exc.end_lineno or exc.lineno or 1)),
        "endColumn": max(1, int(exc.end_offset or exc.offset or 1))
    }])
`);
    return parseDiagnostics(raw);
  } catch {
    return [{
      severity: 'error',
      code: 'PYTHON_COMPILE_FAILED',
      message: 'Python source could not be compiled safely.',
      line: 1,
      column: 1
    }];
  } finally {
    engine.globals.delete('__elitescada_compile_source');
  }
}

async function initializeScript(message: Extract<PythonWorkerRequest, { kind: 'initialize-script' }>) {
  const engine = requirePyodide();
  const diagnostics = getCompileDiagnostics(message.source);
  if (diagnostics.length > 0) {
    postBridgeResponse({
      kind: 'diagnostic',
      requestId: message.requestId,
      identity: { ...message.identity },
      diagnostic: diagnostics[0]
    });
    return;
  }

  engine.globals.set('__elitescada_source', message.source);
  engine.globals.set('__elitescada_handlers_json', JSON.stringify(message.handlerNames));

  try {
    const raw = await engine.runPythonAsync(`
import json, traceback
__elitescada_runtime_globals = {"__name__": "__elitescada_script__"}
try:
    exec(compile(__elitescada_source, "<EliteSCADA Script>", "exec"), __elitescada_runtime_globals, __elitescada_runtime_globals)
    _required_handlers = json.loads(__elitescada_handlers_json)
    _missing_handlers = [name for name in _required_handlers if not callable(__elitescada_runtime_globals.get(name))]
    if _missing_handlers:
        json.dumps({"ok": False, "code": "PYTHON_HANDLER_MISSING", "line": 1})
    else:
        json.dumps({"ok": True})
except KeyboardInterrupt:
    json.dumps({"ok": False, "code": "PYTHON_INITIALIZE_CANCELLED", "line": 1})
except BaseException as exc:
    _frames = traceback.extract_tb(exc.__traceback__)
    _script_frames = [frame for frame in _frames if frame.filename == "<EliteSCADA Script>"]
    _line = _script_frames[-1].lineno if _script_frames else 1
    json.dumps({"ok": False, "code": "PYTHON_INITIALIZE_FAULT", "line": max(1, int(_line)), "type": type(exc).__name__})
`);

    const outcome = parseJsonObject(raw);
    if (outcome.ok !== true) {
      postBridgeResponse({
        kind: 'diagnostic',
        requestId: message.requestId,
        identity: { ...message.identity },
        diagnostic: {
          severity: 'error',
          code: typeof outcome.code === 'string' ? outcome.code : 'PYTHON_INITIALIZE_FAULT',
          message: buildSanitizedFaultMessage('Script initialization failed', outcome.type),
          line: safePositiveInteger(outcome.line),
          column: 1
        }
      });
      return;
    }

    runtimeGlobals = engine.globals.get('__elitescada_runtime_globals');
    postBridgeResponse({
      kind: 'ready',
      requestId: message.requestId,
      identity: { ...message.identity }
    });
  } finally {
    engine.globals.delete('__elitescada_source');
    engine.globals.delete('__elitescada_handlers_json');
  }
}

async function dispatchEvent(message: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }>) {
  if (activeExecution || !runtimeGlobals) {
    postExecution(message, 'faulted', 0, 'Script runtime is not ready for another handler.');
    return;
  }

  activeExecution = {
    requestId: message.requestId,
    executionId: message.executionId,
    identity: { ...message.identity }
  };
  if (interruptView) interruptView[0] = 0;

  const startedAt = performance.now();
  try {
    const outcome = await invokeHandler(message.handlerName, message.payload);
    if (outcome.cancelled === true) {
      postExecution(message, 'cancelled', performance.now() - startedAt);
    } else if (outcome.ok === true) {
      postExecution(message, 'completed', performance.now() - startedAt);
    } else {
      postExecution(
        message,
        'faulted',
        performance.now() - startedAt,
        buildSanitizedFaultMessage('Python handler failed', outcome.type, outcome.line)
      );
    }
  } catch {
    postExecution(message, 'faulted', performance.now() - startedAt, 'Python handler failed with a sanitized runtime fault.');
  } finally {
    rejectPendingApiRequests(message.executionId);
    activeExecution = null;
    if (interruptView) interruptView[0] = 0;
  }
}

async function invokeHandler(handlerName: string, payload: unknown): Promise<Record<string, unknown>> {
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(handlerName)) {
    return { ok: false, type: 'InvalidHandlerName', line: 1 };
  }

  const engine = requirePyodide();
  const globals = requireRuntimeGlobals();
  const proxy = globals as PyodideGlobals;
  proxy.set('__elitescada_handler_name', handlerName);
  proxy.set('__elitescada_event_payload', payload);

  try {
    const raw = await engine.runPythonAsync(`
import inspect, json, traceback
try:
    _handler = globals().get(__elitescada_handler_name)
    if not callable(_handler):
        json.dumps({"ok": False, "type": "MissingHandler", "line": 1})
    else:
        _result = _handler(__elitescada_event_payload)
        if inspect.isawaitable(_result):
            await _result
        json.dumps({"ok": True})
except KeyboardInterrupt:
    json.dumps({"ok": False, "cancelled": True})
except BaseException as exc:
    _frames = traceback.extract_tb(exc.__traceback__)
    _script_frames = [frame for frame in _frames if frame.filename == "<EliteSCADA Script>"]
    _line = _script_frames[-1].lineno if _script_frames else 1
    json.dumps({"ok": False, "type": type(exc).__name__, "line": max(1, int(_line))})
`, { globals });
    return parseJsonObject(raw);
  } finally {
    proxy.delete('__elitescada_handler_name');
    proxy.delete('__elitescada_event_payload');
  }
}

function cancelExecution(executionId: string) {
  if (!activeExecution || activeExecution.executionId !== executionId) return;
  if (interruptView) interruptView[0] = 2;
}

function disposeScript(requestId: string) {
  if (disposed) return;
  disposed = true;
  if (interruptView) interruptView[0] = 2;
  rejectPendingApiRequests();
  runtimeGlobals = null;
  postBridgeResponse({
    kind: 'disposed',
    requestId,
    identity: { ...requireIdentity() }
  });
}

function rejectPendingApiRequests(executionId?: string) {
  for (const [requestId, pending] of pendingApiRequests) {
    if (executionId && pending.executionId !== executionId) continue;
    pendingApiRequests.delete(requestId);
    pending.reject(new Error('Script execution no longer owns this capability request.'));
  }
}

function postExecution(
  message: Extract<PythonWorkerRequest, { kind: 'dispatch-event' }>,
  status: Extract<PythonWorkerResponse, { kind: 'execution-result' }>['status'],
  durationMs: number,
  sanitizedError?: string
) {
  postBridgeResponse({
    kind: 'execution-result',
    requestId: message.requestId,
    executionId: message.executionId,
    identity: { ...message.identity },
    status,
    durationMs: Math.max(0, Math.round(durationMs * 1000) / 1000),
    sanitizedError
  });
}

function postBridgeResponse(message: PythonWorkerResponse) {
  const envelope: PythonWorkerEnvelope<PythonWorkerResponse> = {
    bridgeVersion: CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
    message
  };
  workerScope.postMessage(envelope);
}

function parseDiagnostics(value: unknown): PythonSourceDiagnostic[] {
  if (typeof value !== 'string') throw new Error('Invalid compile diagnostic payload.');
  const parsed = JSON.parse(value) as unknown;
  if (!Array.isArray(parsed)) throw new Error('Invalid compile diagnostic payload.');

  return parsed.map(item => {
    const record = item as Record<string, unknown>;
    return {
      severity: 'error',
      code: typeof record.code === 'string' ? record.code : 'PYTHON_SYNTAX_ERROR',
      message: sanitizeDiagnosticText(record.message),
      line: safePositiveInteger(record.line),
      column: safePositiveInteger(record.column),
      endLine: safeOptionalPositiveInteger(record.endLine),
      endColumn: safeOptionalPositiveInteger(record.endColumn)
    };
  });
}

function parseJsonObject(value: unknown): Record<string, unknown> {
  if (typeof value !== 'string') return { ok: false };
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)
      ? parsed as Record<string, unknown>
      : { ok: false };
  } catch {
    return { ok: false };
  }
}

function normalizeBridgeValue(value: unknown): unknown {
  if (value === null || value === undefined) return value ?? null;
  if (typeof value === 'string' || typeof value === 'boolean') return value;
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw new Error('Non-finite numbers are not supported by the script bridge.');
    return value;
  }

  if (typeof value === 'object') {
    const proxy = value as PythonProxyLike;
    if (typeof proxy.toJs === 'function') {
      const converted = proxy.toJs({ dict_converter: entries => Object.fromEntries(entries) });
      try {
        return normalizeBridgeValue(converted);
      } finally {
        if (converted !== value && typeof proxy.destroy === 'function') proxy.destroy();
      }
    }

    if (Array.isArray(value)) return value.map(normalizeBridgeValue);
    const prototype = Object.getPrototypeOf(value);
    if (prototype === Object.prototype || prototype === null) {
      const result: Record<string, unknown> = {};
      for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
        result[key] = normalizeBridgeValue(item);
      }
      return result;
    }
  }

  throw new Error('Value is not supported by the Client Visual Python bridge.');
}

function sanitizeDiagnosticText(value: unknown): string {
  if (typeof value !== 'string' || !value.trim()) return 'Invalid Python syntax.';
  const flattened = value.replace(/[\r\n\t]+/g, ' ').trim();
  return flattened.length <= 240 ? flattened : `${flattened.slice(0, 237)}...`;
}

function buildSanitizedFaultMessage(prefix: string, type: unknown, line?: unknown): string {
  const safeType = typeof type === 'string' && /^[A-Za-z_][A-Za-z0-9_]{0,63}$/.test(type)
    ? type
    : 'PythonError';
  const safeLine = line === undefined ? undefined : safePositiveInteger(line);
  return safeLine ? `${prefix} (${safeType}) at line ${safeLine}.` : `${prefix} (${safeType}).`;
}

function safePositiveInteger(value: unknown): number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0 ? value : 1;
}

function safeOptionalPositiveInteger(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isInteger(value) && value > 0 ? value : undefined;
}

function requirePyodide(): PyodideLike {
  if (!pyodide || disposed) throw new Error('Client Visual Python engine is not initialized.');
  return pyodide;
}

function requireRuntimeGlobals(): unknown {
  if (!runtimeGlobals) throw new Error('Client Visual Python script is not initialized.');
  return runtimeGlobals;
}

function requireIdentity(): PythonRuntimeIdentity {
  if (!identity) throw new Error('Client Visual Python runtime identity is not initialized.');
  return identity;
}
