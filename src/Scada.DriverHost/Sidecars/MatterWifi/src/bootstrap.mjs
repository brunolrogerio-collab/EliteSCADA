import { mkdir, lstat } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { createEncryptedStorageImplementations, KV_DRIVER_ID, BLOB_DRIVER_ID } from './encrypted-storage.mjs';
import { hardenStorageTree, inspectStorageDirectory } from './storage-preflight.mjs';
import { parseBootstrapLine, readStartupLine } from './startup-protocol.mjs';
import { connectAuthenticatedRpc, connectMatterUnixSocket, createMatterBridge, serveMatterRpcRequests } from './bridge.mjs';

const ARTIFACT_ID = 'elitescada.matter.wifi';
const ARTIFACT_VERSION = '1.0.0';
const RUNTIME_VERSION = 'node-22.13.0-or-24-plus';
const SCHEMA_VERSION = 1;
function fail(code) {
  throw new Error(code);
}

async function readBootstrap() {
  const line = await readStartupLine(process.stdin);
  try {
    return { key: parseBootstrapLine(line) };
  } catch (error) {
    line.fill(0);
    throw error;
  }
}

function storagePathFromArgs(argv) {
  for (let index = 0; index < argv.length; index++) {
    const value = argv[index];
    if (value === '--storage-path') return argv[index + 1] ? resolve(argv[index + 1]) : null;
    if (value.startsWith('--storage-path=')) return resolve(value.slice('--storage-path='.length));
  }
  return null;
}

function matterSocketPathFromArgs(argv) {
  const index = argv.findIndex((value, offset) => value === '--listen-address' && argv[offset + 1]?.startsWith('/'));
  return index >= 0 ? argv[index + 1] : null;
}

function assertSupportedNodeRuntime() {
  const [major, minor] = process.versions.node.split('.').map(Number);
  if (!(major === 22 && minor >= 13 || major >= 24)) fail('MATTER_NODE_RUNTIME_UNSUPPORTED');
}

function installStopCommand(stop) {
  let input = '';
  process.stdin.setEncoding('utf8');
  process.stdin.once('error', () => process.kill(process.pid, 'SIGTERM'));
  process.stdin.once('end', () => process.kill(process.pid, 'SIGTERM'));
  process.stdin.on('data', chunk => {
    input += chunk;
    if (input.length > 1024) input = input.slice(-1024);
    const lines = input.split('\n');
    input = lines.pop() ?? '';
    for (const line of lines) {
      if (line === 'stop') {
        process.stdin.pause();
        process.stdin.removeAllListeners('data');
        Promise.resolve().then(stop).finally(() => process.kill(process.pid, 'SIGTERM'));
      }
    }
  });
  process.stdin.resume();
}

async function waitForSocket(socketPath) {
  const timeoutAt = Date.now() + 12_000;
  while (Date.now() < timeoutAt) {
    try {
      const info = await lstat(socketPath);
      if (info.isSocket()) {
        process.stdout.write(`${JSON.stringify({
          type: 'ready',
          artifactId: ARTIFACT_ID,
          artifactVersion: ARTIFACT_VERSION,
          runtimeVersion: RUNTIME_VERSION,
          schemaVersion: SCHEMA_VERSION,
        })}\n`);
        return;
      }
    } catch (error) {
      if (error?.code !== 'ENOENT') throw error;
    }
    await new Promise(resolveDelay => setTimeout(resolveDelay, 50));
  }
  process.stderr.write('MATTER_SERVER_SOCKET_READINESS_TIMEOUT\n');
  process.kill(process.pid, 'SIGTERM');
}

async function startBridgeAfterServer(socketPath, points, onStop) {
  const rpcHost = process.env.MATTER_RPC_HOST;
  const rpcPort = Number.parseInt(process.env.MATTER_RPC_PORT ?? '', 10);
  const rpcToken = process.env.MATTER_RPC_TOKEN;
  const rpc = await connectAuthenticatedRpc({ host: rpcHost, port: rpcPort, token: rpcToken });
  const [{ MatterClient }, { default: WebSocket }] = await Promise.all([
    import('@matter-server/ws-client'),
    import('ws'),
  ]);
  const client = await connectMatterUnixSocket(socketPath, { MatterClient, WebSocket });
  const bridge = createMatterBridge({ client, points, send: rpc.send });
  onStop(bridge.close);
  await bridge.start();
  await serveMatterRpcRequests(rpc, bridge);
  await bridge.close();
  rpc.close();
}

async function main() {
  if (process.platform !== 'linux' || process.arch !== 'x64') {
    fail('MATTER_HOST_UNSUPPORTED');
  }
  assertSupportedNodeRuntime();
  process.umask(0o077);

  const bootstrap = await readBootstrap();
  try {
    const storagePath = storagePathFromArgs(process.argv.slice(2));
    if (!storagePath) fail('MATTER_STORAGE_PATH_REQUIRED');
    await mkdir(storagePath, { recursive: true, mode: 0o700 });
    await inspectStorageDirectory(storagePath);
    await hardenStorageTree(storagePath);

    const [{ Environment, StorageService, StorageDriver, BlobStorageDriver, toJson, fromJson }, nodejs] = await Promise.all([
      import('@matter/main'),
      import('@matter/nodejs'),
    ]);
    const storage = Environment.default.get(StorageService);
    const drivers = createEncryptedStorageImplementations({
      StorageDriver,
      BlobStorageDriver,
      fileStorageDriver: nodejs.FileStorageDriver,
      directoryBlobStorageDriver: nodejs.DirectoryBlobStorageDriver,
      key: bootstrap.key,
      serialize: toJson,
      deserialize: fromJson,
    });
    bootstrap.key.fill(0);
    storage.registerDriver(drivers.kvImplementation);
    storage.registerBlobDriver(drivers.blobImplementation);
    storage.configuredDriver = KV_DRIVER_ID;
    storage.configuredBlobDriver = BLOB_DRIVER_ID;
    process.once('exit', () => drivers.destroy());

    const args = process.argv.slice(2);
    const socketPath = matterSocketPathFromArgs(args);
    if (!socketPath) fail('MATTER_UNIX_SOCKET_REQUIRED');
    const stopActions = [];
    installStopCommand(async () => {
      await Promise.allSettled(stopActions.map(stop => stop()));
    });
    void waitForSocket(socketPath).then(async () => {
      let points;
      try { points = JSON.parse(process.env.MATTER_POINT_MANIFEST ?? 'null'); }
      catch { fail('MATTER_POINT_MANIFEST_INVALID'); }
      if (!Array.isArray(points)) fail('MATTER_POINT_MANIFEST_INVALID');
      await startBridgeAfterServer(socketPath, points, stop => stopActions.push(stop));
    }).catch(() => {
      process.stderr.write('MATTER_SERVER_SOCKET_READINESS_FAILED\n');
      process.kill(process.pid, 'SIGTERM');
    });

    // Node executes the matter-server entrypoint after all --import preloaders return.
  } catch (error) {
    bootstrap.key.fill(0);
    throw error;
  }
}

try {
  await main();
} catch (error) {
  process.stderr.write(`${error?.message && /^[A-Z0-9_]+$/.test(error.message) ? error.message : 'MATTER_BOOTSTRAP_FAILED'}\n`);
  process.exitCode = 1;
}
