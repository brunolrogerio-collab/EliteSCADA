import { createCipheriv, createDecipheriv, randomBytes } from 'node:crypto';

export const KV_DRIVER_ID = 'elitescada-matter-aes256gcm-kv-v1';
export const BLOB_DRIVER_ID = 'elitescada-matter-aes256gcm-blob-v1';

const ENVELOPE_PREFIX = 'elitescada-matter-storage:v1:';
const ENVELOPE_MAGIC = Buffer.from('ESMS', 'ascii');
const ENVELOPE_VERSION = 1;
const NONCE_BYTES = 12;
const TAG_BYTES = 16;
const MAX_VALUE_BYTES = 32 * 1024 * 1024;
export const MAX_BLOB_BYTES = 64 * 1024 * 1024;

function checkedKey(value) {
  const key = Buffer.from(value);
  if (key.length !== 32) {
    key.fill(0);
    throw new TypeError('Matter persistent-state key must contain exactly 32 bytes.');
  }
  return key;
}

function aadFor(kind, namespace, contexts, key) {
  if (typeof namespace !== 'string' || !namespace ||
      !Array.isArray(contexts) || contexts.some(part => typeof part !== 'string') ||
      typeof key !== 'string' || !key) {
    throw new TypeError('Matter storage identity is invalid.');
  }
  return Buffer.from(JSON.stringify(['elitescada-matter-storage', ENVELOPE_VERSION, kind, namespace, contexts, key]), 'utf8');
}

function packEnvelope(kind, namespace, contexts, name, plaintext, key) {
  if (plaintext.length > (kind === 'blob' ? MAX_BLOB_BYTES : MAX_VALUE_BYTES)) {
    throw new RangeError('Matter persisted value exceeds the supported bound.');
  }
  const nonce = randomBytes(NONCE_BYTES);
  const cipher = createCipheriv('aes-256-gcm', key, nonce, { authTagLength: TAG_BYTES });
  cipher.setAAD(aadFor(kind, namespace, contexts, name));
  const ciphertext = Buffer.concat([cipher.update(plaintext), cipher.final()]);
  const envelope = Buffer.concat([
    ENVELOPE_MAGIC,
    Buffer.from([ENVELOPE_VERSION]),
    nonce,
    cipher.getAuthTag(),
    ciphertext,
  ]);
  return kind === 'kv' ? ENVELOPE_PREFIX + envelope.toString('base64') : envelope;
}

function unpackEnvelope(kind, namespace, contexts, name, encoded, key) {
  let envelope;
  if (kind === 'kv') {
    if (typeof encoded !== 'string' || !encoded.startsWith(ENVELOPE_PREFIX)) {
      throw new Error('MATTER_STORAGE_PLAINTEXT_OR_FORMAT_MISMATCH');
    }
    const base64 = encoded.slice(ENVELOPE_PREFIX.length);
    envelope = Buffer.from(base64, 'base64');
    if (envelope.toString('base64') !== base64) {
      envelope.fill(0);
      throw new Error('MATTER_STORAGE_ENVELOPE_INVALID');
    }
  } else {
    envelope = Buffer.from(encoded);
  }

  if (envelope.length < ENVELOPE_MAGIC.length + 1 + NONCE_BYTES + TAG_BYTES ||
      !envelope.subarray(0, ENVELOPE_MAGIC.length).equals(ENVELOPE_MAGIC) ||
      envelope[ENVELOPE_MAGIC.length] !== ENVELOPE_VERSION) {
    envelope.fill(0);
    throw new Error('MATTER_STORAGE_PLAINTEXT_OR_FORMAT_MISMATCH');
  }

  const offset = ENVELOPE_MAGIC.length + 1;
  const nonce = envelope.subarray(offset, offset + NONCE_BYTES);
  const tag = envelope.subarray(offset + NONCE_BYTES, offset + NONCE_BYTES + TAG_BYTES);
  const ciphertext = envelope.subarray(offset + NONCE_BYTES + TAG_BYTES);
  try {
    const decipher = createDecipheriv('aes-256-gcm', key, nonce, { authTagLength: TAG_BYTES });
    decipher.setAAD(aadFor(kind, namespace, contexts, name));
    decipher.setAuthTag(tag);
    return Buffer.concat([decipher.update(ciphertext), decipher.final()]);
  } catch {
    throw new Error('MATTER_STORAGE_AUTHENTICATION_FAILED');
  } finally {
    envelope.fill(0);
  }
}

function readStream(stream, maximumBytes) {
  return (async () => {
    const reader = stream.getReader();
    const chunks = [];
    let size = 0;
    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = Buffer.from(value);
        size += chunk.length;
        if (size > maximumBytes) {
          chunk.fill(0);
          await reader.cancel();
          throw new RangeError('Matter persisted value exceeds the supported bound.');
        }
        chunks.push(chunk);
      }
      const combined = Buffer.concat(chunks, size);
      for (const chunk of chunks) chunk.fill(0);
      return combined;
    } catch (error) {
      for (const chunk of chunks) chunk.fill(0);
      throw error;
    } finally {
      reader.releaseLock();
    }
  })();
}

function oneChunkStream(bytes) {
  return new ReadableStream({
    start(controller) {
      controller.enqueue(new Uint8Array(bytes));
      controller.close();
    },
  });
}

/**
 * Build Matter.js KV and blob drivers which encrypt every value before it reaches
 * the underlying filesystem implementation. The passed Matter.js constructors
 * keep this module independently testable without importing or starting a
 * Matter Server process.
 */
export function createEncryptedStorageImplementations({
  StorageDriver,
  BlobStorageDriver,
  fileStorageDriver,
  directoryBlobStorageDriver,
  key: keyMaterial,
  serialize,
  deserialize,
}) {
  if (typeof StorageDriver !== 'function' || typeof BlobStorageDriver !== 'function' ||
      !fileStorageDriver?.create || !directoryBlobStorageDriver?.create ||
      typeof serialize !== 'function' || typeof deserialize !== 'function') {
    throw new TypeError('Matter storage driver dependencies are incomplete.');
  }
  const key = checkedKey(keyMaterial);

  class EncryptedKvDriver extends StorageDriver {
    static id = KV_DRIVER_ID;

    constructor(inner, namespace) {
      super();
      this.inner = inner;
      this.namespace = namespace;
    }

    get initialized() { return this.inner.initialized; }
    initialize() { return this.inner.initialized ? undefined : this.inner.initialize(); }
    close() { return this.inner.close(); }
    delete(contexts, name) { return this.inner.delete(contexts, name); }
    has(contexts, name) { return this.get(contexts, name).then(value => value !== undefined); }
    keys(contexts) { return this.inner.keys(contexts); }
    contexts(contexts) { return this.inner.contexts(contexts); }
    clearAll(contexts) { return this.inner.clearAll(contexts); }

    async get(contexts, name) {
      const stored = await this.inner.get(contexts, name);
      if (stored === undefined) return undefined;
      const plaintext = unpackEnvelope('kv', this.namespace, contexts, name, stored, key);
      try {
        return deserialize(plaintext.toString('utf8'));
      } finally {
        plaintext.fill(0);
      }
    }

    async set(contexts, nameOrValues, value) {
      if (typeof nameOrValues === 'string') {
        const plaintext = Buffer.from(serialize(value), 'utf8');
        try {
          const encrypted = packEnvelope('kv', this.namespace, contexts, nameOrValues, plaintext, key);
          await this.inner.set(contexts, nameOrValues, encrypted);
        } finally {
          plaintext.fill(0);
        }
        return;
      }

      if (!nameOrValues || typeof nameOrValues !== 'object' || Array.isArray(nameOrValues)) {
        throw new TypeError('Matter storage values must be an object keyed by storage name.');
      }
      const encryptedValues = Object.create(null);
      try {
        for (const [name, item] of Object.entries(nameOrValues)) {
          const plaintext = Buffer.from(serialize(item), 'utf8');
          try {
            encryptedValues[name] = packEnvelope('kv', this.namespace, contexts, name, plaintext, key);
          } finally {
            plaintext.fill(0);
          }
        }
        await this.inner.set(contexts, encryptedValues);
      } finally {
        for (const name of Object.keys(encryptedValues)) encryptedValues[name] = undefined;
      }
    }

    async values(contexts) {
      const stored = await this.inner.values(contexts);
      const values = Object.create(null);
      for (const [name, item] of Object.entries(stored)) values[name] = await this.get(contexts, name);
      return values;
    }
  }

  class EncryptedBlobDriver extends BlobStorageDriver {
    static id = BLOB_DRIVER_ID;

    constructor(inner, namespace) {
      super();
      this.inner = inner;
      this.namespace = namespace;
    }

    get initialized() { return this.inner.initialized; }
    initialize() { return this.inner.initialized ? undefined : this.inner.initialize(); }
    close() { return this.inner.close(); }
    delete(contexts, name) { return this.inner.delete(contexts, name); }
    has(contexts, name) { return this.inner.has(contexts, name); }
    keys(contexts) { return this.inner.keys(contexts); }
    contexts(contexts) { return this.inner.contexts(contexts); }
    clearAll(contexts) { return this.inner.clearAll(contexts); }

    async openBlob(contexts, name) {
      if (!(await this.inner.has(contexts, name))) return new Blob([]);
      const stored = Buffer.from(await (await this.inner.openBlob(contexts, name)).arrayBuffer());
      try {
        const plaintext = unpackEnvelope('blob', this.namespace, contexts, name, stored, key);
        try { return new Blob([plaintext]); }
        finally { plaintext.fill(0); }
      } finally {
        stored.fill(0);
      }
    }

    async writeBlobFromStream(contexts, name, stream) {
      const plaintext = await readStream(stream, MAX_BLOB_BYTES);
      try {
        const encrypted = packEnvelope('blob', this.namespace, contexts, name, plaintext, key);
        try { await this.inner.writeBlobFromStream(contexts, name, oneChunkStream(encrypted)); }
        finally { encrypted.fill(0); }
      } finally {
        plaintext.fill(0);
      }
    }
  }

  const kvImplementation = {
    id: KV_DRIVER_ID,
    async create(namespace) {
      const inner = await fileStorageDriver.create(namespace, { kind: fileStorageDriver.id, type: 'kv' });
      return new EncryptedKvDriver(inner, namespace.namespace);
    },
  };
  const blobImplementation = {
    id: BLOB_DRIVER_ID,
    async create(namespace) {
      const inner = await directoryBlobStorageDriver.create(namespace, { kind: directoryBlobStorageDriver.id, type: 'blob' });
      return new EncryptedBlobDriver(inner, namespace.namespace);
    },
  };

  return {
    kvImplementation,
    blobImplementation,
    destroy() { key.fill(0); },
  };
}
