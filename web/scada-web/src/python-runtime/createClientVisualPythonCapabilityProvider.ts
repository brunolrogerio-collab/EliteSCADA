import { clientMemory, type ClientMemoryStore } from '../runtime/clientMemory';
import { loadRuntimeTagDetail } from '../runtime/tagInspectorApi';
import type { RuntimeTagDetailResponse } from '../runtime/tagInspectorTypes';
import type { ClientVisualPythonCapabilityProvider } from './clientVisualPythonCapabilities';

export type ClientVisualPythonTagReader = (reference: string) => Promise<RuntimeTagDetailResponse>;

export type ClientVisualPythonCapabilityProviderOptions = {
  tagReader?: ClientVisualPythonTagReader;
  memoryStore?: ClientMemoryStore;
};

export function createClientVisualPythonCapabilityProvider(
  options: ClientVisualPythonCapabilityProviderOptions = {}
): ClientVisualPythonCapabilityProvider {
  const tagReader = options.tagReader ?? loadRuntimeTagDetail;
  const memoryStore = options.memoryStore ?? clientMemory;

  return {
    async readTag(reference) {
      const detail = await tagReader(reference);
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
    }
  };
}
