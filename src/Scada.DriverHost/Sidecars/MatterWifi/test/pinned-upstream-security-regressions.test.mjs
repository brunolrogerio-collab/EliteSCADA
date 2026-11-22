import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { ClientInteraction } from '../node_modules/@matter/protocol/dist/esm/action/client/ClientInteraction.js';
import { SessionParameters } from '../node_modules/@matter/protocol/dist/esm/session/SessionParameters.js';
import { TlvPbkdfParamResponse } from '../node_modules/@matter/protocol/dist/esm/session/pase/PaseMessages.js';
import { BLOB_DRIVER_ID, KV_DRIVER_ID, createEncryptedStorageImplementations } from '../src/encrypted-storage.mjs';

test('the pinned protocol package normalizes zero or invalid maxPathsPerInvoke to one', () => {
  for (const value of [0, -1, 1.5, 65_536, Number.NaN]) {
    assert.equal(SessionParameters({ maxPathsPerInvoke: value }).maxPathsPerInvoke, 1, `value ${value}`);
  }

  assert.equal(SessionParameters({ maxPathsPerInvoke: 1 }).maxPathsPerInvoke, 1);
  assert.equal(SessionParameters({ maxPathsPerInvoke: 65_535 }).maxPathsPerInvoke, 65_535);
});

test('the pinned protocol package bounds PBKDF response iterations to the Matter range', () => {
  const response = iterations => ({
    initiatorRandom: new Uint8Array(32),
    responderRandom: new Uint8Array(32),
    responderSessionId: 1,
    pbkdfParameters: { iterations, salt: new Uint8Array(16) },
  });

  for (const iterations of [1_000, 100_000]) {
    assert.doesNotThrow(() => TlvPbkdfParamResponse.validate(response(iterations)), `iterations ${iterations}`);
  }
  for (const iterations of [999, 100_001, 2_147_483_648, 4_294_967_295]) {
    assert.throws(() => TlvPbkdfParamResponse.validate(response(iterations)), undefined, `iterations ${iterations}`);
  }
});

test('a zero invoke-path limit fails before allocating batches or contacting a peer', async () => {
  let peerCalls = 0;
  const interaction = new ClientInteraction({
    environment: {
      has: () => false,
      get: () => ({ randomUint32: 0 }),
      join: () => ({ details: {} }),
    },
    exchangeProvider: {
      maxPathsPerInvoke: 0,
      connect: async () => { peerCalls += 1; },
    },
  });

  const invoke = interaction.invoke({
    largeMessage: false,
    batchDuration: false,
    invokeRequests: [{}, {}],
    commands: new Map([[1, {}], [2, {}]]),
  });

  await assert.rejects(invoke.next(), /Cannot split an invoke into batches of 0 paths/);
  assert.equal(peerCalls, 0);
});

test('the pinned Matter.js storage API reopens real encrypted fabric KV and blob files', async t => {
  const matter = await import('@matter/main');
  const nodejs = await import('@matter/nodejs');
  const directory = await mkdtemp(join(tmpdir(), 'scadivo-matter-pinned-storage-'));
  const secretMarker = 'fabric-secret-restart-marker';
  const blobMarker = Buffer.from('attestation-secret-restart-marker');
  const key = Buffer.alloc(32, 0x39);
  t.after(async () => {
    key.fill(0);
    await rm(directory, { recursive: true, force: true });
  });

  async function openRuntime() {
    const environment = new matter.Environment('matter-pinned-storage-test');
    environment.set(matter.Filesystem, new nodejs.NodeJsFilesystem(directory));
    const storage = new matter.StorageService(environment);
    const implementations = createEncryptedStorageImplementations({
      StorageDriver: matter.StorageDriver,
      BlobStorageDriver: matter.BlobStorageDriver,
      fileStorageDriver: nodejs.FileStorageDriver,
      directoryBlobStorageDriver: nodejs.DirectoryBlobStorageDriver,
      key,
      serialize: matter.toJson,
      deserialize: matter.fromJson,
    });
    storage.registerDriver(implementations.kvImplementation);
    storage.registerBlobDriver(implementations.blobImplementation);
    storage.configuredDriver = KV_DRIVER_ID;
    storage.configuredBlobDriver = BLOB_DRIVER_ID;
    return { storage, implementations };
  }

  const first = await openRuntime();
  const firstKv = await first.storage.open('matter/fabric-state');
  const firstFabric = firstKv.createContext('fabric').createContext('controller');
  await firstFabric.set('credentials', { marker: secretMarker, fabricId: 'test-fabric' });
  const firstBlob = await first.storage.openBlobStorage('matter/fabric-blobs');
  await firstBlob.driver.writeBlobFromStream(['fabric'], 'attestation', new ReadableStream({
    start(controller) { controller.enqueue(blobMarker); controller.close(); },
  }));
  await firstKv.close();
  await firstBlob.close();
  first.implementations.destroy();

  async function findMarker(path, marker) {
    for (const entry of await readdir(path, { withFileTypes: true })) {
      const itemPath = join(path, entry.name);
      if (entry.isDirectory()) {
        if (await findMarker(itemPath, marker)) return true;
      } else if (entry.isFile() && (await readFile(itemPath)).includes(marker)) return true;
    }
    return false;
  }
  assert.equal(await findMarker(directory, Buffer.from(secretMarker)), false);
  assert.equal(await findMarker(directory, blobMarker), false);

  const reopened = await openRuntime();
  const reopenedKv = await reopened.storage.open('matter/fabric-state');
  const reopenedFabric = reopenedKv.createContext('fabric').createContext('controller');
  assert.deepEqual(await reopenedFabric.get('credentials'), { marker: secretMarker, fabricId: 'test-fabric' });
  const reopenedBlob = await reopened.storage.openBlobStorage('matter/fabric-blobs');
  assert.deepEqual(Buffer.from(await (await reopenedBlob.driver.openBlob(['fabric'], 'attestation')).arrayBuffer()), blobMarker);
  await reopenedKv.close();
  await reopenedBlob.close();
  reopened.implementations.destroy();
});
