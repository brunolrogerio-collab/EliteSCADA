export class StorageDriver {
  get initialized() { return true; }
}

export class BlobStorageDriver {
  get initialized() { return true; }
}

export class FakeNamespace {
  constructor(namespace) { this.namespace = namespace; }
}

export class MemoryDriver extends StorageDriver {
  static id = 'file';
  static lastCreated;

  constructor(namespace) {
    super();
    this.namespace = namespace;
    this.store = new Map();
    MemoryDriver.lastCreated = this;
  }

  static async create(namespace) { return new MemoryDriver(namespace); }
  async get(contexts, key) { return this.store.get(JSON.stringify([contexts, key])); }
  async set(contexts, keyOrValues, value) {
    if (typeof keyOrValues === 'string') this.store.set(JSON.stringify([contexts, keyOrValues]), value);
    else for (const [key, item] of Object.entries(keyOrValues)) this.store.set(JSON.stringify([contexts, key]), item);
  }
  async values(contexts) {
    const result = Object.create(null);
    for (const [identity, value] of this.store.entries()) {
      const [storedContexts, name] = JSON.parse(identity);
      if (JSON.stringify(storedContexts) === JSON.stringify(contexts)) result[name] = value;
    }
    return result;
  }
  async delete(contexts, key) { this.store.delete(JSON.stringify([contexts, key])); }
  async keys(contexts) {
    return [...this.store.keys()].flatMap(identity => {
      const [storedContexts, key] = JSON.parse(identity);
      return JSON.stringify(storedContexts) === JSON.stringify(contexts) ? [key] : [];
    });
  }
  async contexts() { return []; }
  async clearAll(contexts) {
    for (const key of await this.keys(contexts)) await this.delete(contexts, key);
  }
  async close() {}
}

export class MemoryBlobDriver extends BlobStorageDriver {
  static id = 'dir';
  static lastCreated;

  constructor(namespace) {
    super();
    this.namespace = namespace;
    this.store = new Map();
    MemoryBlobDriver.lastCreated = this;
  }

  static async create(namespace) { return new MemoryBlobDriver(namespace); }
  async has(contexts, key) { return this.store.has(JSON.stringify([contexts, key])); }
  async openBlob(contexts, key) { return new Blob([this.store.get(JSON.stringify([contexts, key])) ?? new Uint8Array()]); }
  async writeBlobFromStream(contexts, key, stream) {
    const chunks = [];
    const reader = stream.getReader();
    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(Buffer.from(value));
      }
      this.store.set(JSON.stringify([contexts, key]), Buffer.concat(chunks));
    } finally { reader.releaseLock(); }
  }
  async delete(contexts, key) { this.store.delete(JSON.stringify([contexts, key])); }
  async keys(contexts) {
    return [...this.store.keys()].flatMap(identity => {
      const [storedContexts, key] = JSON.parse(identity);
      return JSON.stringify(storedContexts) === JSON.stringify(contexts) ? [key] : [];
    });
  }
  async contexts() { return []; }
  async clearAll(contexts) {
    for (const key of await this.keys(contexts)) await this.delete(contexts, key);
  }
  async close() {}
}
