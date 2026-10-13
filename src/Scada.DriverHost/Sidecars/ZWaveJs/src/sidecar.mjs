import { chmod, mkdir } from "node:fs/promises";
import { createInterface } from "node:readline";
import { createServer, connect as connectTcp } from "node:net";
import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import { Driver } from "zwave-js";
import { ZwavejsServer } from "@zwave-js/server";
import { formatValueAddress, normalizeValue, parseValueAddress, sameValue, validateValueMetadata, validateWriteValue } from "./value-policy.mjs";
import { createValueUpdateRouter, readFirstLineBuffer, routeInitialPersistentValues, startLoopbackZWaveServer, WriteReportTracker } from "./sidecar-contract.mjs";
import { ZWaveServerClient } from "./zwave-server-client.mjs";

const ARTIFACT_ID = "elitescada.zwave.js";
const ARTIFACT_VERSION = "1.0.0";
const RUNTIME_COMPATIBILITY = "node-22.13.0-or-24-plus";
const MAX_RPC_FRAME_BYTES = 65_536;
const DRIVER_READY_TIMEOUT_MS = 180_000;
const WRITE_REPORT_TIMEOUT_MS = 25_000;
const logSilencer = Object.freeze({ error() {}, warn() {}, info() {}, debug() {} });

function decodeKey(keyBytes, name) {
  if (!Array.isArray(keyBytes) || keyBytes.length !== 16 || keyBytes.some(byte => !Number.isInteger(byte) || byte < 0 || byte > 255)) {
    throw new Error(`PROTECTED_KEY_INVALID:${name}`);
  }
  return Buffer.from(keyBytes);
}

function parseExpectedHomeId(value) {
  const match = /^homeid:0x([0-9a-f]{8})$/i.exec(value ?? "");
  if (!match) throw new Error("NVM_IDENTITY_INVALID");
  return Number.parseInt(match[1], 16) >>> 0;
}

export function parseBootstrap(frame) {
  let raw;
  let parsed;
  let securityKeys;
  let transferred = false;
  try {
    if (!Buffer.isBuffer(frame) || frame.length > 2 * 1024 * 1024) throw new Error("BOOTSTRAP_INVALID");
    raw = frame.toString("utf8");
    parsed = JSON.parse(raw);
    const config = parsed?.configuration;
    if (parsed?.type !== "bootstrap" || typeof config?.serialPath !== "string" || !config.serialPath ||
        typeof config?.cacheDirectory !== "string" || !config.cacheDirectory ||
        typeof config?.nvmIdentity !== "string" || !Array.isArray(parsed.points) || parsed.points.length < 1 || parsed.points.length > 512) {
      throw new Error("BOOTSTRAP_INVALID");
    }
    const points = parsed.points.map(point => {
      if (typeof point?.tagId !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(point.tagId) ||
          typeof point.address !== "string" || typeof point.dataType !== "string" || typeof point.writable !== "boolean") {
        throw new Error("POINT_MANIFEST_INVALID");
      }
      const address = parseValueAddress(point.address);
      return { ...point, ...address };
    });
    if (new Set(points.map(point => point.tagId)).size !== points.length ||
        new Set(points.map(point => point.address)).size !== points.length) throw new Error("POINT_MANIFEST_DUPLICATE");
    if (parsed.keys !== undefined && (typeof parsed.keys !== "object" || parsed.keys === null || Array.isArray(parsed.keys))) {
      throw new Error("PROTECTED_KEY_INVALID");
    }
    const keyMap = new Map([
      ["s0Legacy", "S0_Legacy"],
      ["s2AccessControl", "S2_AccessControl"],
      ["s2Authenticated", "S2_Authenticated"],
      ["s2Unauthenticated", "S2_Unauthenticated"],
    ]);
    securityKeys = {};
    for (const [inputName, bytes] of Object.entries(parsed.keys ?? {})) {
      const optionName = keyMap.get(inputName);
      if (!optionName) throw new Error("PROTECTED_KEY_NAME_INVALID");
      securityKeys[optionName] = decodeKey(bytes, inputName);
    }
    const result = { configuration: config, expectedHomeId: parseExpectedHomeId(config.nvmIdentity), points, securityKeys };
    transferred = true;
    return result;
  } finally {
    frame?.fill?.(0);
    raw = undefined;
    for (const key of Object.values(parsed?.keys ?? {})) if (Array.isArray(key)) key.fill(0);
    if (parsed && Object.hasOwn(parsed, "keys")) parsed.keys = undefined;
    if (!transferred) for (const key of Object.values(securityKeys ?? {})) key.fill(0);
  }
}

async function readLoopbackPort() {
  const probe = createServer();
  await new Promise((resolve, reject) => {
    probe.once("error", reject);
    probe.listen(0, "127.0.0.1", resolve);
  });
  const port = probe.address().port;
  await new Promise(resolve => probe.close(resolve));
  return port;
}

async function connectRpcClient(host, port, token) {
  if (host !== "127.0.0.1" || !Number.isInteger(port) || port < 1 || port > 65535 || !/^[0-9a-f]{64}$/.test(token ?? "")) {
    throw new Error("RPC_CONFIGURATION_INVALID");
  }
  const socket = connectTcp({ host, port });
  socket.setNoDelay(true);
  await new Promise((resolve, reject) => {
    socket.once("connect", resolve);
    socket.once("error", () => reject(new Error("RPC_CONNECT_FAILED")));
  });
  let buffer = Buffer.alloc(0);
  const queue = [];
  const waiters = [];
  let closedError;
  socket.on("data", chunk => {
    buffer = Buffer.concat([buffer, chunk]);
    if (buffer.length > MAX_RPC_FRAME_BYTES && !buffer.includes(0x0a)) {
      closedError = new Error("RPC_FRAME_TOO_LARGE");
      socket.destroy();
      return;
    }
    while (true) {
      const newline = buffer.indexOf(0x0a);
      if (newline < 0) break;
      const frame = buffer.subarray(0, newline);
      buffer = buffer.subarray(newline + 1);
      if (frame.length === 0 || frame.length > MAX_RPC_FRAME_BYTES) {
        closedError = new Error("RPC_FRAME_INVALID");
        socket.destroy();
        return;
      }
      let message;
      try { message = JSON.parse(frame.toString("utf8")); }
      catch {
        closedError = new Error("RPC_FRAME_INVALID");
        socket.destroy();
        return;
      }
      const waiter = waiters.shift();
      if (waiter) waiter(message);
      else queue.push(message);
    }
  });
  socket.on("error", () => { closedError ??= new Error("RPC_CONNECTION_FAILED"); while (waiters.length) waiters.shift()(null); });
  socket.on("close", () => { closedError ??= new Error("RPC_CONNECTION_CLOSED"); while (waiters.length) waiters.shift()(null); });
  const nextFrame = () => queue.length ? Promise.resolve(queue.shift())
    : closedError ? Promise.resolve(null)
      : new Promise(resolve => waiters.push(resolve));
  const send = frame => {
    const encoded = Buffer.from(`${JSON.stringify(frame)}\n`, "utf8");
    if (encoded.length > MAX_RPC_FRAME_BYTES) throw new Error("RPC_FRAME_TOO_LARGE");
    socket.write(encoded, () => encoded.fill(0));
  };

  send({ type: "hello", protocolVersion: 1, token });
  const welcome = await Promise.race([
    nextFrame(),
    new Promise((_, reject) => setTimeout(() => reject(new Error("RPC_HANDSHAKE_TIMEOUT")), 10_000)),
  ]);
  if (!welcome || welcome.type !== "welcome" || welcome.protocolVersion !== 1) throw closedError ?? new Error("RPC_HANDSHAKE_REJECTED");
  return { socket, nextFrame, send };
}

async function main() {
  if (!((process.platform === "win32" || process.platform === "linux") && process.arch === "x64")) throw new Error("PLATFORM_UNSUPPORTED");
  const [major, minor] = process.versions.node.split(".").map(Number);
  if (!(major === 22 && minor >= 13 || major >= 24)) throw new Error("NODE_RUNTIME_UNSUPPORTED");
  let bootstrapFrame = await readFirstLineBuffer(process.stdin);
  const bootstrap = parseBootstrap(bootstrapFrame);
  bootstrapFrame = undefined;
  const input = createInterface({ input: process.stdin, crlfDelay: Infinity });
  const { configuration, expectedHomeId, points, securityKeys } = bootstrap;
  const metadataByTagId = new Map();
  const pointsByAddress = new Map(points.map(point => [point.address, point]));
  const pointsByTagId = new Map(points.map(point => [point.tagId, point]));
  const writeQueues = new Map();
  const writeReports = new WriteReportTracker();
  let driver;
  let upstream;
  let apiClient;
  let rpc;
  let closing = false;

  const emitObservation = (point, value) => {
    try {
      const normalized = normalizeValue(value, point.dataType, metadataByTagId.get(point.tagId));
      rpc?.send({ type: "observation", tagId: point.tagId, value: normalized, observedAt: new Date().toISOString() });
      writeReports.observe(point.tagId, normalized);
    } catch { /* Reject unsupported or secret values without forwarding any part of them. */ }
  };
  const handleZWaveEvent = createValueUpdateRouter(pointsByAddress, emitObservation);

  const withWriteLock = async (point, operation) => {
    const previous = writeQueues.get(point.tagId) ?? Promise.resolve();
    let release;
    const current = new Promise(resolve => { release = resolve; });
    writeQueues.set(point.tagId, current);
    await previous;
    try { return await operation(); }
    finally {
      release();
      if (writeQueues.get(point.tagId) === current) writeQueues.delete(point.tagId);
    }
  };

  const respond = (id, fields) => rpc.send({ id, ...fields });
  const handleRpcRequest = async request => {
    if (!request || typeof request.id !== "string" || !/^[0-9a-f]{32}$/i.test(request.id) ||
        !["read", "write"].includes(request.op) || typeof request.tagId !== "string") return;
    const point = pointsByTagId.get(request.tagId);
    if (!point || request.address !== point.address || request.dataType !== point.dataType || request.writable !== point.writable) {
      respond(request.id, { ok: false, errorCode: "POINT_BINDING_MISMATCH" });
      return;
    }
    const metadata = metadataByTagId.get(point.tagId);
    if (request.op === "write" && (!point.writable || point.commandClass === 112)) {
      respond(request.id, { ok: false, errorCode: "VALUE_NOT_WRITABLE" });
      return;
    }
    if (request.op === "read") {
      try {
        const value = normalizeValue(await apiClient.pollValue(point.nodeId, point.valueId), point.dataType, metadata);
        respond(request.id, { ok: true, value });
      } catch {
        respond(request.id, { ok: false, errorCode: "READ_FAILED" });
      }
      return;
    }
    await withWriteLock(point, async () => {
      let requested;
      let reportWaiter;
      let requestDispatched = false;
      try {
        requested = validateWriteValue(request.value, point.dataType, metadata);
        validateValueMetadata(point.valueId, metadata, point.dataType, true);
        const setPromise = apiClient.setValue(point.nodeId, point.valueId, requested);
        requestDispatched = true;
        const setReply = await setPromise;
        const status = setReply?.result?.status;
        if (![1, 254, 255].includes(status)) {
          respond(request.id, { ok: false, errorCode: "SET_REJECTED" });
          return;
        }
        // The command acknowledgement is our freshness barrier. A matching
        // event that was already in flight before the command cannot confirm it.
        reportWaiter = writeReports.waitFor(point.tagId, requested, WRITE_REPORT_TIMEOUT_MS, writeReports.sequence);
        try {
          const actual = normalizeValue(await apiClient.pollValue(point.nodeId, point.valueId), point.dataType, metadata);
          if (sameValue(actual, requested)) {
            respond(request.id, { ok: true, value: actual });
            return;
          }
        } catch { /* The fresh device report below is a separate confirmation path. */ }
        const observed = await reportWaiter.promise;
        if (observed !== undefined && sameValue(observed, requested)) respond(request.id, { ok: true, value: observed });
        else respond(request.id, { ok: false, uncertain: true, errorCode: "WRITE_NOT_CONFIRMED" });
      } catch (error) {
        reportWaiter?.cancel();
        const code = typeof error?.message === "string" && /^[A-Z0-9_]{1,96}$/.test(error.message) ? error.message : "WRITE_FAILED";
        respond(request.id, { ok: false, ...(requestDispatched ? { uncertain: true } : {}), errorCode: code });
      } finally {
        reportWaiter?.cancel();
      }
    });
  };

  const stop = async () => {
    if (closing) return;
    closing = true;
    writeReports.cancelAll();
    try { await apiClient?.close(); } catch {}
    try { await upstream?.destroy(); } catch {}
    try { await driver?.destroy(); } catch {}
    for (const key of Object.values(securityKeys)) key.fill(0);
    rpc?.socket.destroy();
    input.close();
  };

  try {
    await mkdir(configuration.cacheDirectory, { recursive: true, mode: 0o700 });
    if (process.platform === "linux") await chmod(configuration.cacheDirectory, 0o700);

    let driverReadyResolve;
    let driverReadyReject;
    const driverReady = new Promise((resolve, reject) => { driverReadyResolve = resolve; driverReadyReject = reject; });
    driver = new Driver(configuration.serialPath, {
      storage: { cacheDir: configuration.cacheDirectory, lockDir: configuration.cacheDirectory, throttle: "normal" },
      securityKeys,
      logConfig: { enabled: false, showLogo: false },
      disableOptimisticValueUpdate: true,
      emitValueUpdateAfterSetValue: false,
    });
    driver.on("driver ready", driverReadyResolve);
    driver.on("error", () => driverReadyReject(new Error("DRIVER_START_FAILED")));
    const readyTimeout = new Promise((_, reject) => setTimeout(() => reject(new Error("DRIVER_READY_TIMEOUT")), DRIVER_READY_TIMEOUT_MS));
    await driver.start();
    if (!driver.ready) await Promise.race([driverReady, readyTimeout]);
    if ((driver.controller.homeId >>> 0) !== expectedHomeId) throw new Error("CONTROLLER_NVM_IDENTITY_MISMATCH");

    const startedUpstream = await startLoopbackZWaveServer({
      reservePort: readLoopbackPort,
      createServer: port => new ZwavejsServer(driver, {
        host: "127.0.0.1", port, logger: logSilencer, enableDNSServiceDiscovery: false,
      }),
    });
    upstream = startedUpstream.server;
    const upstreamPort = startedUpstream.port;
    apiClient = new ZWaveServerClient(`ws://127.0.0.1:${upstreamPort}`);
    apiClient.onEvent = handleZWaveEvent;
    const initialState = await apiClient.connect(expectedHomeId);

    for (const point of points) {
      const metadata = await apiClient.getValueMetadata(point.nodeId, point.valueId);
      metadataByTagId.set(point.tagId, validateValueMetadata(point.valueId, metadata, point.dataType, point.writable));
    }

    const rpcPort = Number.parseInt(process.env.ZWAVE_RPC_PORT ?? "", 10);
    rpc = await connectRpcClient(process.env.ZWAVE_RPC_HOST, rpcPort, process.env.ZWAVE_RPC_TOKEN);
    routeInitialPersistentValues(initialState, pointsByAddress, emitObservation);
    console.log(JSON.stringify({
      type: "ready",
      artifactId: ARTIFACT_ID,
      artifactVersion: ARTIFACT_VERSION,
      runtimeVersion: RUNTIME_COMPATIBILITY,
      schemaVersion: 1,
    }));
    rpc.socket.on("error", () => { void stop(); });
    const rpcTask = (async () => {
      let inFlight = 0;
      while (!closing) {
        const request = await rpc.nextFrame();
        if (!request) break;
        if (inFlight >= 128) {
          if (typeof request.id === "string" && /^[0-9a-f]{32}$/i.test(request.id)) {
            respond(request.id, { ok: false, errorCode: "RPC_OVERLOADED" });
          }
          continue;
        }
        inFlight++;
        void handleRpcRequest(request).catch(() => {
          if (typeof request?.id === "string" && /^[0-9a-f]{32}$/i.test(request.id)) {
            try { respond(request.id, { ok: false, errorCode: "RPC_REQUEST_FAILED" }); } catch {}
          }
        }).finally(() => { inFlight--; });
      }
      if (!closing) await stop();
    })();
    for await (const line of input) {
      if (line !== "stop") break;
      break;
    }
    await stop();
    await rpcTask;
  } catch {
    console.error("ZWAVE_SIDECAR_START_FAILED");
    process.exitCode = 1;
  } finally {
    await stop();
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(() => {
    console.error("ZWAVE_SIDECAR_START_FAILED");
    process.exitCode = 1;
  });
}
