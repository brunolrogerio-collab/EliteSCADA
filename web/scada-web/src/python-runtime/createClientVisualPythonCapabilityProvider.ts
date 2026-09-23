import { clientMemory, type ClientMemoryStore } from '../runtime/clientMemory';
import { writeRuntimeTagValue, type RuntimeTagWriteValue } from '../runtime/runtimeTagWriteApi';
import { loadRuntimeTagDetail } from '../runtime/tagInspectorApi';
import type { RuntimeTagDetailResponse } from '../runtime/tagInspectorTypes';
import type { ScriptEngineeringDependency } from '../engineering/scripts/scriptEngineeringTypes';
import type { ClientVisualPythonCapabilityProvider } from './clientVisualPythonCapabilities';

export type ClientVisualPythonTagReader = (reference: string) => Promise<RuntimeTagDetailResponse>;
export type ClientVisualPythonTagWriter = (reference: string, value: RuntimeTagWriteValue) => Promise<void>;

export type ClientVisualPythonVisualPropertyProvider = Pick<
  ClientVisualPythonCapabilityProvider,
  'readVisualProperty' | 'writeVisualProperty' | 'clearVisualProperty' | 'requestVisualTween'
>;

export type ClientVisualPythonCapabilityProviderOptions = {
  tagReader?: ClientVisualPythonTagReader;
  /**
   * Undefined means normal Runtime authority. Null explicitly disables TAG write,
   * which is used by Engineering preview so a handler test cannot change process state.
   */
  tagWriter?: ClientVisualPythonTagWriter | null;
  memoryStore?: ClientMemoryStore;
  visualPropertyProvider?: ClientVisualPythonVisualPropertyProvider;
  /**
   * The persisted dependencies of the Script that owns this Runtime instance.
   * When supplied, readable TAG paths are a presentation aid only: every
   * access must resolve through the declared stable TAG identity.
   */
  tagDependencies?: readonly ScriptEngineeringDependency[];
};

export function createClientVisualPythonCapabilityProvider(
  options: ClientVisualPythonCapabilityProviderOptions = {}
): ClientVisualPythonCapabilityProvider {
  const tagReader = options.tagReader ?? loadRuntimeTagDetail;
  const tagWriter = options.tagWriter === undefined ? writeRuntimeTagValue : options.tagWriter;
  const memoryStore = options.memoryStore ?? clientMemory;
  const visualPropertyProvider = options.visualPropertyProvider;
  const declaredTags = createDeclaredTagResolver(options.tagDependencies);

  return {
    async readTag(reference) {
      const declared = declaredTags?.resolve(reference);
      const detail = await tagReader(declared?.readReference ?? reference);
      verifyExpectedTagIdentity(detail, declared);
      return {
        id: detail.tag.id,
        name: detail.tag.name,
        path: detail.tag.path,
        dataType: detail.tag.dataType,
        engineeringUnit: detail.tag.engineeringUnit ?? null,
        readOnly: detail.tag.readOnly,
        value: detail.current?.value ?? null,
        quality: detail.current?.quality ?? 'Unknown',
        timestamp: detail.current?.timestamp ?? null,
        source: detail.current?.source ?? null,
        sourceTimestamp: detail.current?.sourceTimestamp ?? null,
        serverTimestamp: detail.current?.serverTimestamp ?? null
      };
    },

    writeTag: tagWriter
      ? async (reference, value) => {
          // A previous read is not authority. Re-read through the protected
          // surface so a path reused by another TAG cannot be silently retargeted.
          const declared = declaredTags?.resolve(reference);
          const detail = await tagReader(declared?.readReference ?? reference);
          verifyExpectedTagIdentity(detail, declared);
          const stableReference = declared?.expectedTagId ?? detail.tag.id;
          await tagWriter(stableReference, value);
          return { accepted: true, reference: stableReference };
        }
      : undefined,

    readClientMemory(reference) {
      const value = memoryStore.read(reference);
      if (value === undefined) {
        throw new Error(`Client Memory TAG '${reference}' is not available in this Runtime Client.`);
      }
      return value;
    },

    writeClientMemory(reference, value) {
      memoryStore.write(reference, value);
      return memoryStore.read(reference) ?? null;
    },

    readVisualProperty: visualPropertyProvider?.readVisualProperty
      ? (targetReference, propertyKey, context) =>
          visualPropertyProvider.readVisualProperty!(targetReference, propertyKey, context)
      : undefined,
    writeVisualProperty: visualPropertyProvider?.writeVisualProperty
      ? (targetReference, propertyKey, value, context) =>
          visualPropertyProvider.writeVisualProperty!(targetReference, propertyKey, value, context)
      : undefined,
    clearVisualProperty: visualPropertyProvider?.clearVisualProperty
      ? (targetReference, propertyKey, context) =>
          visualPropertyProvider.clearVisualProperty!(targetReference, propertyKey, context)
      : undefined,
    requestVisualTween: visualPropertyProvider?.requestVisualTween
      ? (argumentsValue, context) =>
          visualPropertyProvider.requestVisualTween!(argumentsValue, context)
      : undefined
  };
}

type DeclaredTagReference = Readonly<{
  readReference: string;
  expectedTagId: string;
}>;

type DeclaredTagResolver = Readonly<{
  resolve: (reference: string) => DeclaredTagReference;
}>;

function createDeclaredTagResolver(
  dependencies: readonly ScriptEngineeringDependency[] | undefined
): DeclaredTagResolver | undefined {
  if (dependencies === undefined) return undefined;

  const references = new Map<string, DeclaredTagReference>();
  const rejected = new Set<string>();
  for (const dependency of dependencies) {
    if (dependency.kind !== 'tag') continue;

    const binding = dependency.tagBinding;
    const expectedTagId = binding?.expected?.tagId;
    const visibleReference = binding?.reference?.trim();
    const stableReference = dependency.stableReference.trim();
    const validBinding = binding?.version === 1 &&
      !!visibleReference &&
      typeof expectedTagId === 'string' &&
      isGuid(expectedTagId) &&
      isGuid(stableReference) &&
      sameGuid(expectedTagId, stableReference);

    if (binding && validBinding) {
      addDeclaredReference(references, rejected, visibleReference, {
        readReference: visibleReference,
        expectedTagId
      });
    } else if (!binding && isGuid(stableReference)) {
      // Explicit legacy dependencies remain valid, but only by their declared
      // stable GUID. A readable path is never inferred for legacy data.
      addDeclaredReference(references, rejected, stableReference, {
        readReference: stableReference,
        expectedTagId: stableReference
      });
    }
  }

  return Object.freeze({
    resolve(reference: string): DeclaredTagReference {
      const normalized = normalizeReference(reference);
      const declared = references.get(normalized);
      if (!normalized || rejected.has(normalized) || !declared) {
        throw new Error(`TAG reference '${reference}' is not declared by this Client Visual Script.`);
      }
      return declared;
    }
  });
}

function addDeclaredReference(
  references: Map<string, DeclaredTagReference>,
  rejected: Set<string>,
  reference: string,
  declared: DeclaredTagReference
): void {
  const normalized = normalizeReference(reference);
  const existing = references.get(normalized);
  if (existing && existing.expectedTagId !== declared.expectedTagId) {
    references.delete(normalized);
    rejected.add(normalized);
    return;
  }
  if (!rejected.has(normalized)) references.set(normalized, declared);
}

function normalizeReference(reference: string): string {
  // Mirrors the canonical TAG registry's case-insensitive path ownership. This
  // is a lookup key only; the persisted visible spelling remains unchanged.
  return reference.trim().toLocaleLowerCase('en-US');
}

function verifyExpectedTagIdentity(
  detail: RuntimeTagDetailResponse,
  declared: DeclaredTagReference | undefined
): void {
  if (declared && !sameGuid(detail.tag.id, declared.expectedTagId)) {
    throw new Error(`TAG reference '${declared.readReference}' no longer resolves to its declared stable identity.`);
  }
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

function sameGuid(left: string, right: string): boolean {
  return left.toLocaleLowerCase('en-US') === right.toLocaleLowerCase('en-US');
}
