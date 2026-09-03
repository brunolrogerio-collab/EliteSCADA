import {
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  type ClientVisualPythonCapability
} from '../../python-runtime/pythonRuntimeContracts';
import type { ScriptEngineeringEntryPoint } from '../scripts/scriptEngineeringTypes';

export type PythonApiHelpDescriptor = {
  capability: ClientVisualPythonCapability;
  title: string;
  summary: string;
};

const capabilityHelp: Record<ClientVisualPythonCapability, Omit<PythonApiHelpDescriptor, 'capability'>> = {
  'tag.read': {
    title: 'TAG read',
    summary: 'Read an authorized shared TAG snapshot through the trusted Client Visual bridge.'
  },
  'tag.write': {
    title: 'TAG write',
    summary: 'Request an authorized writable TAG change through the normal Runtime/backend command path.'
  },
  'clientMemory.read': {
    title: 'Client Memory read',
    summary: 'Read a builtin.memory.client value owned by the current Runtime Client.'
  },
  'clientMemory.write': {
    title: 'Client Memory write',
    summary: 'Write a builtin.memory.client value owned by the current Runtime Client.'
  },
  'visualProperty.read': {
    title: 'Visual property read',
    summary: 'Read a declared visual property through the public visual Runtime boundary.'
  },
  'visualProperty.write': {
    title: 'Visual property write',
    summary: 'Set or explicitly clear a Script override only when the visual property contract marks it runtime-writable.'
  },
  'visualTween.request': {
    title: 'Visual tween request',
    summary: 'Request a bounded renderer-owned tween/animation through the public bridge.'
  },
  'backendOperation.request': {
    title: 'Backend operation request',
    summary: 'Request an operation that still passes through normal backend authorization and identity.'
  }
};

export const CLIENT_VISUAL_PYTHON_API_HELP: readonly PythonApiHelpDescriptor[] =
  CLIENT_VISUAL_PYTHON_CAPABILITIES.map(capability => ({
    capability,
    ...capabilityHelp[capability]
  }));

export type PythonEntryPointCompletion = {
  label: string;
  detail: string;
  documentation: string;
  insertText: string;
};

export function buildEntryPointCompletions(
  entryPoints: readonly ScriptEngineeringEntryPoint[]
): PythonEntryPointCompletion[] {
  const seen = new Set<string>();
  const completions: PythonEntryPointCompletion[] = [];

  for (const entryPoint of entryPoints) {
    const handler = entryPoint.handlerName.trim();
    if (!isPythonIdentifier(handler) || seen.has(handler)) continue;
    seen.add(handler);

    const target = entryPoint.targetReference?.trim();
    const context = target
      ? `${entryPoint.eventKind} · ${target}`
      : entryPoint.eventKind;

    completions.push({
      label: handler,
      detail: `EliteSCADA entry point · ${context}`,
      documentation: `Canonical Script entry point for ${context}.`,
      insertText: `def ${handler}():\n\t\${1:pass}`
    });
  }

  return completions.sort((left, right) => left.label.localeCompare(right.label));
}

function isPythonIdentifier(value: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(value);
}
