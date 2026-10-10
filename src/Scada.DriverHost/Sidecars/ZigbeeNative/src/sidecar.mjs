import readline from 'node:readline';
import net from 'node:net';
import { pathToFileURL } from 'node:url';
import { Controller, setLogger } from 'zigbee-herdsman';
import * as converters from 'zigbee-herdsman-converters';
import { ZnpAdapterManager } from 'zigbee-herdsman/dist/adapter/z-stack/adapter/manager.js';
import { installHerdsmanSafetyGuards } from './safety-guard.mjs';
import { cleanupController, createGracefulShutdown } from './sidecar-lifecycle.mjs';
import {
  ARTIFACT_ID,
  ARTIFACT_VERSION,
  RUNTIME_VERSION,
  SCHEMA_VERSION,
  assertAllowedRequest,
  hasStateExposure,
  mapOnOffObservation,
  parseRuntimeConfiguration,
  pointKey,
} from './sidecar-core.mjs';

const MAX_FRAME_LENGTH = 65_536;
const nodePackage = await import('zigbee-herdsman/package.json', { with: { type: 'json' } });
const converterPackage = await import('zigbee-herdsman-converters/package.json', { with: { type: 'json' } });

function requirePinnedPackages() {
  if (nodePackage.default.version !== '3.3.2' || converterPackage.default.version !== '23.7.0') {
    throw new Error('ZIGBEE_PACKAGE_VERSION_MISMATCH');
  }
}

function writeJsonLine(stream, value) {
  stream.write(`${JSON.stringify(value)}\n`);
}

async function readJsonLine(reader, timeoutMilliseconds = 15_000) {
  return await new Promise((resolve, reject) => {
    const timer = setTimeout(() => finish(new Error('RPC_TIMEOUT')), timeoutMilliseconds);
    const onLine = (line) => {
      if (line.length > MAX_FRAME_LENGTH) return finish(new Error('RPC_FRAME_TOO_LARGE'));
      try { finish(null, JSON.parse(line)); } catch { finish(new Error('RPC_INVALID_JSON')); }
    };
    const onClose = () => finish(new Error('RPC_CONNECTION_CLOSED'));
    const finish = (error, value) => {
      clearTimeout(timer);
      reader.removeListener('line', onLine);
      reader.removeListener('close', onClose);
      if (error) reject(error); else resolve(value);
    };
    reader.once('line', onLine);
    reader.once('close', onClose);
  });
}

async function connectHost(config) {
  const socket = net.createConnection({ host: config.rpcHost, port: config.rpcPort });
  await new Promise((resolve, reject) => {
    socket.once('connect', resolve);
    socket.once('error', reject);
  });
  socket.setNoDelay(true);
  const reader = readline.createInterface({ input: socket, crlfDelay: Infinity });
  writeJsonLine(socket, {
    type: 'hello',
    token: config.rpcToken,
    artifactId: ARTIFACT_ID,
    artifactVersion: ARTIFACT_VERSION,
    schemaVersion: SCHEMA_VERSION,
  });
  const welcome = await readJsonLine(reader);
  if (welcome?.type !== 'welcome' || welcome.schemaVersion !== SCHEMA_VERSION) {
    throw new Error('RPC_HOST_HANDSHAKE_REJECTED');
  }
  return { socket, reader };
}

function controllerOptions(config) {
  return {
    network: {
      networkKey: config.networkKey,
      panID: config.panId,
      extendedPanID: config.extendedPanId,
      channelList: [config.channel],
      networkKeyDistribute: false,
    },
    serialPort: { adapter: 'zstack', path: config.serialPath, baudRate: config.baudRate, rtscts: false },
    adapter: { disableLED: false },
    databasePath: config.databasePath,
    backupPath: undefined,
    databaseBackupPath: undefined,
  };
}

async function definitionFor(device) {
  if (!device?.modelID) return undefined;
  return await converters.findByDevice(device, false);
}

function getConfiguredEndpoint(controller, request) {
  const device = controller.getDeviceByIeeeAddr(`0x${request.ieee}`);
  if (!device) throw new Error('DEVICE_NOT_IN_COORDINATOR_DATABASE');
  const endpoint = device.getEndpoint(request.endpoint);
  if (!endpoint) throw new Error('ENDPOINT_NOT_IN_COORDINATOR_DATABASE');
  return { device, endpoint };
}

async function readState(controller, request) {
  const { device, endpoint } = getConfiguredEndpoint(controller, request);
  const definition = await definitionFor(device);
  if (!hasStateExposure(definition, converters.access, converters.access.STATE | converters.access.GET)) {
    throw new Error('CONVERTER_HAS_NO_READABLE_STATE');
  }
  const result = await endpoint.read('genOnOff', ['onOff'], { sendPolicy: 'immediate' });
  if (result.onOff !== true && result.onOff !== false && result.onOff !== 0 && result.onOff !== 1) {
    throw new Error('DEVICE_RETURNED_INVALID_STATE');
  }
  return result.onOff === true || result.onOff === 1;
}

async function executeRequest(controller, allowedPoints, request) {
  const safeRequest = assertAllowedRequest(request, allowedPoints);
  const { device, endpoint } = getConfiguredEndpoint(controller, safeRequest);
  const definition = await definitionFor(device);
  const requiredAccess = safeRequest.method === 'write'
    ? converters.access.STATE | converters.access.SET
    : converters.access.STATE | converters.access.GET;
  if (!hasStateExposure(definition, converters.access, requiredAccess)) {
    throw new Error('CONVERTER_HAS_NO_SUPPORTED_STATE');
  }

  if (safeRequest.method === 'write') {
    await endpoint.command('genOnOff', safeRequest.value ? 'on' : 'off', {}, { sendPolicy: 'immediate' });
  }

  const state = await readState(controller, safeRequest);
  return { type: 'response', id: safeRequest.id, ok: true, accepted: safeRequest.method === 'write', value: state, observedAt: new Date().toISOString() };
}

async function runSidecar(env = process.env, dependencies = {}) {
  requirePinnedPackages();
  const config = parseRuntimeConfiguration(env);
  const ControllerClass = dependencies.Controller ?? Controller;
  const ZnpAdapterManagerClass = dependencies.ZnpAdapterManager ?? ZnpAdapterManager;
  const setHerdsmanLogger = dependencies.setLogger ?? setLogger;
  installHerdsmanSafetyGuards(ControllerClass, ZnpAdapterManagerClass);

  // The stock logger is intentionally not connected to stdout/stderr. Readiness
  // and protocol errors are emitted as short, sanitized codes only.
  setHerdsmanLogger({
    debug() {},
    info() {},
    warning() {},
    error() {},
  });
  converters.setLogger({ debug() {}, info() {}, warning() {}, error() {} });

  let controller;
  let host;
  let stopping = false;
  let requestChain = Promise.resolve();
  try {
    controller = new ControllerClass(controllerOptions(config));
    await controller.start();
    host = await connectHost(config);
    writeJsonLine(process.stdout, {
      type: 'ready',
      artifactId: ARTIFACT_ID,
      artifactVersion: ARTIFACT_VERSION,
      runtimeVersion: RUNTIME_VERSION,
      schemaVersion: SCHEMA_VERSION,
      message: 'Existing TI zStack coordinator started with network mutation guards enabled.',
    });

    const shutdown = createGracefulShutdown(
      controller,
      host,
      () => requestChain,
      () => { stopping = true; },
    );

    const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
    rl.on('line', (line) => {
      if (line === 'stop') {
        void shutdown().then(
          () => { process.exitCode = 0; process.exit(); },
          () => { process.exitCode = 1; process.exit(); },
        );
        return;
      }
    });

    host.reader.on('line', (line) => {
      if (stopping) return;
      requestChain = requestChain.then(() => handleHostLine(line)).catch(() => {});
    });

    async function handleHostLine(line) {
      if (line.length > MAX_FRAME_LENGTH) {
        host.socket.destroy();
        return;
      }
      let message;
      try { message = JSON.parse(line); } catch { return; }

      if (message.type === 'request') {
        try {
          const response = await executeRequest(controller, config.allowedPoints, message);
          writeJsonLine(host.socket, response);
        } catch (error) {
          writeJsonLine(host.socket, {
            type: 'response',
            id: typeof message.id === 'string' ? message.id : '',
            ok: false,
            accepted: false,
            errorCode: typeof error?.message === 'string' && /^[A-Z0-9_]+$/.test(error.message) ? error.message : 'DEVICE_OPERATION_FAILED',
          });
        }
      }
    }

    controller.on('message', async (message) => {
      if (stopping) return;
      try {
        const observation = mapOnOffObservation(message, config.allowedPoints);
        if (!observation) return;
        const definition = await definitionFor(message.device);
        if (stopping) return;
        if (!hasStateExposure(definition, converters.access, converters.access.STATE)) return;
        writeJsonLine(host.socket, { type: 'report', ...observation });
      } catch {
        // Unsupported device messages are ignored; no log includes payloads.
      }
    });

    const stopOnSignal = async () => {
      try { await shutdown(); process.exit(0); }
      catch { process.exit(1); }
    };
    process.once('SIGINT', stopOnSignal);
    process.once('SIGTERM', stopOnSignal);
    process.stdin.resume();
    return { controller, host, shutdown };
  } catch (error) {
    const failures = [error];
    try { host?.socket.destroy(); } catch (cleanupError) { failures.push(cleanupError); }
    try { await cleanupController(controller); } catch (cleanupError) { failures.push(cleanupError); }
    const code = failures.length > 1
      ? 'SIDECAR_START_CLEANUP_FAILED'
      : typeof error?.code === 'string' && /^[A-Z0-9_]+$/.test(error.code)
        ? error.code
        : typeof error?.message === 'string' && /^[A-Z0-9_]+$/.test(error.message)
          ? error.message
          : 'SIDECAR_START_FAILED';
    writeJsonLine(process.stderr, { type: 'error', code });
    if (failures.length > 1) throw new AggregateError(failures, code, { cause: error });
    throw new Error(code, { cause: error });
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  runSidecar().catch(() => { process.exitCode = 1; });
}

export { controllerOptions, executeRequest, parseRuntimeConfiguration, runSidecar };
