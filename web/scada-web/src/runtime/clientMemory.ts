export type ClientMemoryTagDefinition = {
  id: string;
  name: string;
  path: string;
  dataType: string;
  readOnly: boolean;
  initialValue: unknown;
};

export type ClientMemorySourceDefinition = {
  dataSourceKey: string;
  name: string;
  tags: ClientMemoryTagDefinition[];
};

type ClientMemoryEntry = {
  definition: ClientMemoryTagDefinition;
  value: unknown;
};

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class ClientMemoryStore {
  private readonly byId = new Map<string, ClientMemoryEntry>();
  private readonly idByPath = new Map<string, string>();
  private sources: ClientMemorySourceDefinition[] = [];

  async initialize(signal?: AbortSignal): Promise<number> {
    const response = await fetch(`${API}/api/internal-memory/client/definitions`, {
      credentials: 'same-origin',
      signal
    });
    if (!response.ok) throw new Error(`Client Memory definitions request failed with HTTP ${response.status}.`);

    const sources = await response.json() as ClientMemorySourceDefinition[];
    this.clear();
    this.sources = sources.map(source => ({ ...source, tags: source.tags.map(tag => ({ ...tag })) }));

    for (const source of this.sources) {
      for (const definition of source.tags) {
        const normalizedPath = definition.path.toLowerCase();
        if (this.byId.has(definition.id)) throw new Error(`Duplicate Client Memory TAG id '${definition.id}'.`);
        if (this.idByPath.has(normalizedPath)) throw new Error(`Duplicate Client Memory TAG path '${definition.path}'.`);
        validateClientValue(definition.dataType, definition.initialValue);
        this.byId.set(definition.id, { definition, value: cloneValue(definition.initialValue) });
        this.idByPath.set(normalizedPath, definition.id);
      }
    }

    return this.byId.size;
  }

  get size(): number { return this.byId.size; }

  snapshotSources(): ClientMemorySourceDefinition[] {
    return this.sources.map(source => ({ ...source, tags: source.tags.map(tag => ({ ...tag })) }));
  }

  read(pathOrId: string): unknown {
    return cloneValue(this.resolve(pathOrId)?.value);
  }

  write(pathOrId: string, value: unknown): void {
    const entry = this.resolve(pathOrId);
    if (!entry) throw new Error(`Client Memory TAG '${pathOrId}' was not found in this runtime client.`);
    if (entry.definition.readOnly) throw new Error(`Client Memory TAG '${entry.definition.path}' is read-only.`);
    validateClientValue(entry.definition.dataType, value);
    entry.value = cloneValue(value);
  }

  reset(pathOrId: string): void {
    const entry = this.resolve(pathOrId);
    if (!entry) throw new Error(`Client Memory TAG '${pathOrId}' was not found in this runtime client.`);
    entry.value = cloneValue(entry.definition.initialValue);
  }

  clear(): void {
    this.byId.clear();
    this.idByPath.clear();
    this.sources = [];
  }

  private resolve(pathOrId: string): ClientMemoryEntry | undefined {
    const direct = this.byId.get(pathOrId);
    if (direct) return direct;
    const id = this.idByPath.get(pathOrId.toLowerCase());
    return id ? this.byId.get(id) : undefined;
  }
}

export const clientMemory = new ClientMemoryStore();

function validateClientValue(dataType: string, value: unknown) {
  const normalized = dataType.toLowerCase();
  const fail = () => { throw new TypeError(`Client Memory value is incompatible with data type '${dataType}'.`); };

  switch (normalized) {
    case 'boolean':
      if (typeof value !== 'boolean') fail();
      return;
    case 'int16':
      if (typeof value !== 'number' || !Number.isInteger(value) || value < -32768 || value > 32767) fail();
      return;
    case 'int32':
    case 'enum':
      if (typeof value !== 'number' || !Number.isInteger(value) || value < -2147483648 || value > 2147483647) fail();
      return;
    case 'int64':
      if (typeof value === 'number') {
        if (!Number.isSafeInteger(value)) fail();
        return;
      }
      if (typeof value === 'string') {
        if (!/^-?(0|[1-9][0-9]*)$/.test(value)) fail();
        const parsed = BigInt(value);
        if (parsed < -9223372036854775808n || parsed > 9223372036854775807n) fail();
        return;
      }
      fail();
      return;
    case 'float':
    case 'double':
      if (typeof value !== 'number' || !Number.isFinite(value)) fail();
      return;
    case 'string':
      if (typeof value !== 'string') fail();
      return;
    case 'datetime':
      if (typeof value !== 'string' || Number.isNaN(Date.parse(value))) fail();
      return;
    default:
      fail();
  }
}

function cloneValue<T>(value: T): T {
  if (value === undefined || value === null || typeof value !== 'object') return value;
  return structuredClone(value);
}
