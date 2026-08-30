import { clientMemory } from '../runtime/clientMemory';
import type { PythonEditorDiagnosticSnapshot } from '../engineering/python-editor/pythonEditorDiagnostics';
import {
  ClientVisualPythonRuntime,
  type ClientVisualPythonDispatchResult
} from './clientVisualPythonRuntime';
import { createClientVisualPythonCapabilityProvider } from './createClientVisualPythonCapabilityProvider';

export type EngineeringPythonPreviewRequest = {
  scriptId: string;
  source: string;
  handlerNames: readonly string[];
};

export type EngineeringPythonHandlerPreviewRequest = EngineeringPythonPreviewRequest & {
  handlerName: string;
  payload?: unknown;
  signal?: AbortSignal;
};

export type EngineeringPythonHandlerPreviewResult = ClientVisualPythonDispatchResult & {
  diagnostics: PythonEditorDiagnosticSnapshot;
};

export async function compileEngineeringClientVisualPython(
  request: EngineeringPythonPreviewRequest
): Promise<PythonEditorDiagnosticSnapshot> {
  const runtime = createPreviewRuntime(request, {});
  try {
    const result = await runtime.compileSource(request.source);
    return {
      source: request.source,
      diagnostics: result.diagnostics
    };
  } finally {
    await runtime.dispose();
  }
}

export async function runEngineeringClientVisualPythonHandler(
  request: EngineeringPythonHandlerPreviewRequest
): Promise<EngineeringPythonHandlerPreviewResult> {
  // Client Memory belongs to this browser/runtime client. Loading definitions here is
  // safe Engineering preview preparation and never mutates Active Engineering.
  if (clientMemory.size === 0) {
    try {
      await clientMemory.initialize();
    } catch {
      // Keep the sandbox fail-closed. A script that actually requests Client Memory
      // will receive a sanitized capability failure instead of gaining a fallback.
    }
  }

  const runtime = createPreviewRuntime(
    request,
    createClientVisualPythonCapabilityProvider({ memoryStore: clientMemory })
  );

  try {
    const compiled = await runtime.compileSource(request.source);
    const diagnostics: PythonEditorDiagnosticSnapshot = {
      source: request.source,
      diagnostics: compiled.diagnostics
    };

    if (compiled.diagnostics.some(item => item.severity === 'error')) {
      return {
        status: 'faulted',
        sanitizedError: 'Python source contains compile errors.',
        diagnostics
      };
    }

    await runtime.initialize();
    const result = await runtime.dispatchEvent(
      request.handlerName,
      `engineering-preview:${request.handlerName}`,
      request.payload ?? { preview: true },
      request.signal
    );

    return { ...result, diagnostics };
  } finally {
    await runtime.dispose();
  }
}

function createPreviewRuntime(
  request: EngineeringPythonPreviewRequest,
  capabilityProvider: ConstructorParameters<typeof ClientVisualPythonRuntime>[0]['capabilityProvider']
): ClientVisualPythonRuntime {
  const runtimeInstanceId = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? `engineering-preview-${crypto.randomUUID()}`
    : `engineering-preview-${Date.now()}-${Math.random().toString(16).slice(2)}`;

  return new ClientVisualPythonRuntime({
    identity: {
      scriptId: request.scriptId,
      runtimeInstanceId
    },
    source: request.source,
    handlerNames: [...new Set(request.handlerNames.filter(Boolean))],
    capabilityProvider
  });
}
