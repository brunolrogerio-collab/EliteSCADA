import test from 'node:test';
import assert from 'node:assert/strict';
import { BlobStorageDriver, StorageDriver, FakeNamespace, MemoryDriver, MemoryBlobDriver } from './fake-matter-storage.mjs';
import { createEncryptedStorageImplementations } from '../src/encrypted-storage.mjs';

function createStorage(key = Buffer.alloc(32, 7)) {
  const implementations = createEncryptedStorageImplementations({
    StorageDriver,
    BlobStorageDriver,
    fileStorageDriver: MemoryDriver,
    directoryBlobStorageDriver: MemoryBlobDriver,
    key,
    serialize: value => JSON.stringify(value),
    deserialize: text => JSON.parse(text),
  });
  return { implementations, namespace: new FakeNamespace('matter/test/controller') };
}

test('KV values are encrypted at rest and round-trip through the registered wrapper', async () => {
  const { implementations, namespace } = createStorage();
  const store = await implementations.kvImplementation.create(namespace);
  await store.set(['fabric'], 'credentials', { certificate: 'sensitive-marker', generation: 2 });

  const persisted = MemoryDriver.lastCreated.store.get(JSON.stringify([['fabric'], 'credentials']));
  assert.equal(typeof persisted, 'string');
  assert.ok(persisted.startsWith('elitescada-matter-storage:v1:'));
  assert.equal(persisted.includes('sensitive-marker'), false);
  assert.deepEqual(await store.get(['fabric'], 'credentials'), { certificate: 'sensitive-marker', generation: 2 });
  implementations.destroy();
});

test('KV envelope is bound to namespace/context/key and rejects wrong keys or modified bytes', async () => {
  const first = createStorage(Buffer.alloc(32, 1));
  const firstStore = await first.implementations.kvImplementation.create(first.namespace);
  const firstInner = MemoryDriver.lastCreated;
  await firstStore.set(['fabric'], 'credentials', 'private');

  const stored = firstInner.store.get(JSON.stringify([['fabric'], 'credentials']));
  firstInner.store.set(JSON.stringify([['other'], 'credentials']), stored);
  await assert.rejects(firstStore.get(['other'], 'credentials'), /AUTHENTICATION_FAILED/);

  const second = createStorage(Buffer.alloc(32, 2));
  const wrongKeyStore = await second.implementations.kvImplementation.create(first.namespace);
  MemoryDriver.lastCreated.store.set(JSON.stringify([['fabric'], 'credentials']), stored);
  await assert.rejects(wrongKeyStore.get(['fabric'], 'credentials'), /AUTHENTICATION_FAILED/);

  firstInner.store.set(JSON.stringify([['fabric'], 'credentials']), stored.slice(0, -2) + 'AA');
  await assert.rejects(firstStore.get(['fabric'], 'credentials'));
  first.implementations.destroy();
  second.implementations.destroy();
});

test('batch writes and values() preserve names while decrypting each value', async () => {
  const { implementations, namespace } = createStorage();
  const store = await implementations.kvImplementation.create(namespace);
  await store.set(['node'], { name: 'lamp', endpoint: 1 });
  await store.set(['node'], { state: true, level: 42 });

  assert.deepEqual({ ...await store.values(['node']) }, { name: 'lamp', endpoint: 1, state: true, level: 42 });
  assert.deepEqual({ ...await store.values([]) }, {});
  implementations.destroy();
});

test('legacy/plaintext KV is rejected without decrypt fallback', async () => {
  const { implementations, namespace } = createStorage();
  const store = await implementations.kvImplementation.create(namespace);
  MemoryDriver.lastCreated.store.set(JSON.stringify([['fabric'], 'credentials']), JSON.stringify('plaintext'));
  await assert.rejects(store.get(['fabric'], 'credentials'), /PLAINTEXT_OR_FORMAT_MISMATCH/);
  implementations.destroy();
});

test('Blob data is encrypted, round-trips, and tampering fails authentication', async () => {
  const { implementations, namespace } = createStorage();
  const blobs = await implementations.blobImplementation.create(namespace);
  const payload = Buffer.from('fabric blob secret');
  await blobs.writeBlobFromStream(['fabric'], 'attestation', new ReadableStream({
    start(controller) { controller.enqueue(payload); controller.close(); },
  }));

  const persisted = MemoryBlobDriver.lastCreated.store.get(JSON.stringify([['fabric'], 'attestation']));
  assert.ok(Buffer.isBuffer(persisted));
  assert.equal(persisted.includes(Buffer.from('fabric blob secret')), false);
  assert.equal(await (await blobs.openBlob(['fabric'], 'attestation')).text(), 'fabric blob secret');

  persisted[persisted.length - 1] ^= 1;
  await assert.rejects(blobs.openBlob(['fabric'], 'attestation'), /AUTHENTICATION_FAILED/);
  implementations.destroy();
});
