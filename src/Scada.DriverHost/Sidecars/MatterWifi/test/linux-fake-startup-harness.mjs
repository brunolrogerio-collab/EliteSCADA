import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import { spawn } from 'node:child_process';
import { cp, lstat, mkdtemp, mkdir, rm, stat, writeFile } from 'node:fs/promises';
import net from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const sidecarRoot = fileURLToPath(new URL('../', import.meta.url));
const artifactPath = process.env.MATTER_TEST_ARTIFACT_PATH;
const point = Object.freeze({
  tagId: '11111111-1111-4111-8111-111111111111',
  nodeId: '42',
  address: '1/6/0',
  dataType: 'Boolean',
  writable: true,
});

function lineCollector(stream) {
  let buffer = '';
  const lines = [];
  const waiters = [];
  stream.setEncoding('utf8');
  stream.on('data', chunk => {
    buffer += chunk;
    while (true) {
      const newline = buffer.indexOf('\n');
      if (newline < 0) break;
      const line = buffer.slice(0, newline).trimEnd();
      buffer = buffer.slice(newline + 1);
      lines.push(line);
      for (let index = waiters.length - 1; index >= 0; index--) {
        if (waiters[index].matches(line)) {
          const [{ resolve }] = waiters.splice(index, 1);
          resolve(line);
        }
      }
    }
  });
  return {
    lines,
    waitFor(matches, timeoutMs = 15_000) {
      const existing = lines.find(matches);
      if (existing !== undefined) return Promise.resolve(existing);
      return new Promise((resolve, reject) => {
        const waiter = { matches, resolve: value => { clearTimeout(timer); resolve(value); } };
        const timer = setTimeout(() => {
          const index = waiters.indexOf(waiter);
          if (index >= 0) waiters.splice(index, 1);
          reject(new Error(`Timed out waiting for process output. Captured: ${lines.join(' | ')}`));
        }, timeoutMs);
        waiters.push(waiter);
      });
    },
  };
}

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

async function writeModule(directory, packageName, source) {
  const packageDirectory = join(directory, 'node_modules', ...packageName.split('/'));
  await mkdir(packageDirectory, { recursive: true });
  await writeFile(join(packageDirectory, 'package.json'), JSON.stringify({
    name: packageName,
    type: 'module',
    exports: './index.mjs',
  }));
  await writeFile(join(packageDirectory, 'index.mjs'), source);
}

function waitForRpcFrame(queue, waiters, timeoutMs = 5_000) {
  if (queue.length) return Promise.resolve(queue.shift());
  return new Promise((resolve, reject) => {
    const waiter = value => { clearTimeout(timer); resolve(value); };
    const timer = setTimeout(() => {
      const index = waiters.indexOf(waiter);
      if (index >= 0) waiters.splice(index, 1);
      reject(new Error('Timed out waiting for authenticated sidecar RPC frame.'));
    }, timeoutMs);
    waiters.push(waiter);
  });
}

async function main() {
  assert.equal(process.platform, 'linux', 'Run this harness only inside the isolated Linux container.');
  assert.equal(process.arch, 'x64', 'The preview bootstrap is qualified only for Linux x64.');
  assert.equal(process.env.MATTER_FAKE_ONLY, '1', 'This harness must be explicitly marked fake-only.');

  const staging = await mkdtemp(join(tmpdir(), 'elitescada-matter-linux-fake-'));
  const stateDirectory = join(staging, 'state');
  const socketPath = join(staging, 'matter.sock');
  const token = randomBytes(32).toString('hex');
  const startupKey = [...randomBytes(32)];
  let rpcSocket;
  let child;
  let fakeHost;
  let exitPromise;
  const rpcFailure = deferred();
  const authReady = deferred();
  void rpcFailure.promise.catch(() => {});
  const rpcFrames = [];
  const rpcWaiters = [];

  try {
    await cp(join(sidecarRoot, 'src'), join(staging, 'src'), { recursive: true });
    await mkdir(join(staging, 'node_modules'), { recursive: true });

    await writeModule(staging, '@matter/main', `
      export class StorageDriver {}
      export class BlobStorageDriver {}
      export class StorageService {}
      const service = {
        registerDriver(value) { this.kvDriver = value; },
        registerBlobDriver(value) { this.blobDriver = value; },
      };
      export const Environment = { default: { get(type) {
        if (type !== StorageService) throw new Error('FAKE_STORAGE_SERVICE_UNEXPECTED');
        return service;
      } } };
      export const toJson = value => JSON.stringify(value);
      export const fromJson = value => JSON.parse(value);
      export const testStorage = service;
    `);

    await writeModule(staging, '@matter/nodejs', `
      class MemoryKeyValueStorage {
        values = new Map();
        get initialized() { return true; }
        async get(contexts, key) { return this.values.get(JSON.stringify([contexts, key])); }
        async set(contexts, keyOrValues, value) {
          if (typeof keyOrValues === 'string') this.values.set(JSON.stringify([contexts, keyOrValues]), value);
          else for (const [key, item] of Object.entries(keyOrValues)) this.values.set(JSON.stringify([contexts, key]), item);
        }
        async keys(contexts) { return [...this.values.keys()].map(value => JSON.parse(value)).filter(item => JSON.stringify(item[0]) === JSON.stringify(contexts)).map(item => item[1]); }
        async valuesFor(contexts) { return Object.fromEntries(await Promise.all((await this.keys(contexts)).map(async key => [key, await this.get(contexts, key)]))); }
        async contexts() { return []; }
        async clearAll(contexts) { for (const key of await this.keys(contexts)) this.values.delete(JSON.stringify([contexts, key])); }
        async delete(contexts, key) { this.values.delete(JSON.stringify([contexts, key])); }
        async close() {}
      }
      export class FileStorageDriver {
        static id = 'file';
        static async create() { return new MemoryKeyValueStorage(); }
      }
      export class DirectoryBlobStorageDriver {
        static id = 'dir';
        static async create() { return { initialized: true, async close() {}, async has() { return false; }, async delete() {}, async keys() { return []; }, async contexts() { return []; }, async clearAll() {}, async openBlob() { return new Blob([]); }, async writeBlobFromStream() {} }; }
      }
    `);

    await writeModule(staging, '@matter-server/ws-client', `
      import net from 'node:net';
      export class MatterClient {
        constructor(url, factory) {
          if (!url.startsWith('ws+unix:///')) throw new Error('FAKE_UNIX_SOCKET_URL_REQUIRED');
          if (typeof factory !== 'function') throw new Error('FAKE_WEBSOCKET_FACTORY_REQUIRED');
          this.nodes = { '42': { node_id: 42n, attributes: { '1/6/0': true } } };
          this.socket = null;
        }
        async startListening() {
          this.socket = net.createConnection(process.env.MATTER_FAKE_UNIX_SOCKET);
          await new Promise((resolve, reject) => {
            this.socket.once('connect', resolve);
            this.socket.once('error', reject);
          });
        }
        async readAttribute(nodeId, address) {
          return { [address]: this.nodes[nodeId.toString()].attributes[address] };
        }
        async writeAttribute(nodeId, address, value) {
          this.nodes[nodeId.toString()].attributes[address] = value;
        }
        disconnect() { this.socket?.destroy(); }
      }
    `);
    await writeModule(staging, 'ws', 'export default class FakeWebSocket {}');

    await writeFile(join(staging, 'fake-matter-server.mjs'), `
      import assert from 'node:assert/strict';
      import net from 'node:net';
      import { rm } from 'node:fs/promises';
      import { Environment, StorageService, testStorage } from '@matter/main';
      const args = process.argv.slice(2);
      const socketIndex = args.findIndex(value => value === '--listen-address');
      const socketPath = socketIndex >= 0 ? args[socketIndex + 1] : null;
      assert.ok(socketPath?.startsWith('/'));
      const storage = Environment.default.get(StorageService);
      assert.equal(storage, testStorage);
      assert.equal(storage.kvDriver?.id, 'elitescada-matter-aes256gcm-kv-v1');
      assert.equal(storage.blobDriver?.id, 'elitescada-matter-aes256gcm-blob-v1');
      assert.equal(storage.configuredDriver, 'elitescada-matter-aes256gcm-kv-v1');
      assert.equal(storage.configuredBlobDriver, 'elitescada-matter-aes256gcm-blob-v1');
      const kv = await storage.kvDriver.create({ namespace: 'startup-probe' });
      await kv.set(['probe'], 'value', { safe: true });
      const raw = await kv.inner.get(['probe'], 'value');
      assert.ok(raw.startsWith('elitescada-matter-storage:v1:'));
      assert.deepEqual(await kv.get(['probe'], 'value'), { safe: true });
      process.stderr.write('FAKE_STORAGE_PROTECTED_OK\\n');
      const server = net.createServer(socket => socket.on('data', () => {}));
      await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(socketPath, resolve);
      });
      process.once('SIGTERM', () => {
        server.close(async () => {
          await rm(socketPath, { force: true });
          process.exit(0);
        });
      });
      process.stderr.write('FAKE_UNIX_SERVER_LISTENING\\n');
    `);

    fakeHost = net.createServer(socket => {
      rpcSocket = socket;
      let buffer = '';
      let authenticated = false;
      socket.setEncoding('utf8');
      socket.on('data', chunk => {
        buffer += chunk;
        while (true) {
          const newline = buffer.indexOf('\n');
          if (newline < 0) break;
          const line = buffer.slice(0, newline);
          buffer = buffer.slice(newline + 1);
          try {
            const frame = JSON.parse(line);
            if (!authenticated) {
              assert.equal(frame.type, 'hello');
              assert.equal(frame.protocolVersion, 1);
              assert.equal(frame.token, token);
              authenticated = true;
              socket.write('{"type":"welcome","protocolVersion":1}\n');
              authReady.resolve();
              continue;
            }
            const waiter = rpcWaiters.shift();
            if (waiter) waiter(frame);
            else rpcFrames.push(frame);
          } catch (error) {
            rpcFailure.reject(error);
            socket.destroy();
          }
        }
      });
      socket.on('error', error => rpcFailure.reject(error));
    });
    await new Promise((resolve, reject) => {
      fakeHost.once('error', reject);
      fakeHost.listen(0, '127.0.0.1', resolve);
    });
    const rpcPort = fakeHost.address().port;

    child = spawn(process.execPath, [
      '--import', join(staging, 'src', 'bootstrap.mjs'),
      join(staging, 'fake-matter-server.mjs'),
      '--storage-path', stateDirectory,
      '--listen-address', socketPath,
    ], {
      cwd: staging,
      env: {
        ...process.env,
        MATTER_FAKE_ONLY: '1',
        MATTER_FAKE_UNIX_SOCKET: socketPath,
        MATTER_POINT_MANIFEST: JSON.stringify([point]),
        MATTER_RPC_HOST: '127.0.0.1',
        MATTER_RPC_PORT: String(rpcPort),
        MATTER_RPC_TOKEN: token,
      },
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    const stdout = lineCollector(child.stdout);
    const stderr = lineCollector(child.stderr);
    exitPromise = new Promise(resolve => child.once('exit', (code, signal) => resolve({ code, signal })));
    child.stdin.write(JSON.stringify({ type: 'bootstrap', schemaVersion: 1, keyBytes: startupKey }) + '\n');
    startupKey.fill(0);

    const protectedStorageMarker = await stderr.waitFor(line => line === 'FAKE_STORAGE_PROTECTED_OK');
    assert.equal(protectedStorageMarker, 'FAKE_STORAGE_PROTECTED_OK');
    await stderr.waitFor(line => line === 'FAKE_UNIX_SERVER_LISTENING');
    const readyLine = await stdout.waitFor(line => {
      try { return JSON.parse(line)?.type === 'ready'; } catch { return false; }
    });
    const ready = JSON.parse(readyLine);
    assert.deepEqual(ready, {
      type: 'ready',
      artifactId: 'elitescada.matter.wifi',
      artifactVersion: '1.0.0',
      runtimeVersion: 'node-22.13.0-or-24-plus',
      schemaVersion: 1,
    });
    await Promise.race([
      authReady.promise,
      rpcFailure.promise.then(error => { throw error; }),
    ]);
    assert.equal((await stat(stateDirectory)).mode & 0o777, 0o700);
    assert.equal((await lstat(socketPath)).isSocket(), true);

    const observation = await waitForRpcFrame(rpcFrames, rpcWaiters);
    assert.deepEqual({ type: observation.type, tagId: observation.tagId, value: observation.value }, {
      type: 'observation', tagId: point.tagId, value: true,
    });
    rpcSocket.write(JSON.stringify({ id: '22222222222222222222222222222222', op: 'read', ...point }) + '\n');
    const reply = await Promise.race([
      waitForRpcFrame(rpcFrames, rpcWaiters),
      rpcFailure.promise.then(error => { throw error; }),
    ]);
    assert.deepEqual({ id: reply.id, ok: reply.ok, value: reply.value }, {
      id: '22222222222222222222222222222222', ok: true, value: true,
    });

    child.stdin.write('stop\n');
    const exit = await Promise.race([
      exitPromise,
      new Promise((_, reject) => setTimeout(() => reject(new Error('Fake Linux sidecar did not stop cleanly.')), 5_000)),
    ]);
    assert.equal(exit.code, 0, `Fake Linux sidecar exit was ${JSON.stringify(exit)}; stderr: ${stderr.lines.join(' | ')}`);
    assert.ok(stdout.lines.some(line => line === readyLine));

    const report = {
      status: 'PASS',
      scope: 'development-test-only; fake Matter Server; no Matter network; no commissioning; not distributable',
      platform: `${process.platform}/${process.arch}`,
      nodeVersion: process.versions.node,
      image: process.env.MATTER_TEST_IMAGE ?? 'not-recorded',
      checks: [
        'Linux x64 bootstrap and Node runtime gate',
        'stdin-only bootstrap key accepted and consumed',
        'encrypted KV implementation registered before fake server entrypoint',
        'storage directory protected with owner-only permissions',
        'fake Matter Server Unix domain socket readiness',
        'authenticated loopback RPC handshake',
        'initial TAG observation and read request/reply',
        'managed stop command closes the bridge and exits cleanly',
      ],
      ready,
      matterServerCandidateProcessStarted: false,
      matterNetworkUsed: false,
      completedAtUtc: new Date().toISOString(),
    };
    if (artifactPath) {
      await mkdir(dirname(artifactPath), { recursive: true });
      await writeFile(artifactPath, JSON.stringify(report, null, 2) + '\n', { mode: 0o600 });
    }
    process.stdout.write(`MATTER_FAKE_LINUX_STARTUP_PASS ${JSON.stringify(report)}\n`);
  } finally {
    startupKey.fill(0);
    rpcSocket?.destroy();
    if (child && child.exitCode === null) {
      child.kill('SIGKILL');
      if (exitPromise) await exitPromise;
    }
    if (fakeHost?.listening) await new Promise(resolve => fakeHost.close(() => resolve()));
    await rm(staging, { recursive: true, force: true });
  }
}

await main();
