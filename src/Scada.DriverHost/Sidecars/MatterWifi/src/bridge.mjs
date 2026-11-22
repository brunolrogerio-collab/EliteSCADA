import { connect as connectTcp } from 'node:net';
import { TextDecoder } from 'node:util';

const MAX_FRAME_BYTES = 65_536;
const MAX_POINTS = 512;
const MAX_STRING_LENGTH = 4096;
const MAX_SAFE_BIGINT = BigInt(Number.MAX_SAFE_INTEGER);
const MIN_INT64 = -(1n << 63n);
const MAX_INT64 = (1n << 63n) - 1n;

function fail(code) {
  throw new Error(code);
}

function canonicalInteger(value, maximum, allowZero = true) {
  if (!/^(0|[1-9][0-9]*)$/.test(value)) return false;
  const parsed = BigInt(value);
  return (allowZero || parsed !== 0n) && parsed <= maximum && parsed.toString() === value;
}

function parseAddress(value) {
  if (typeof value !== 'string' || value.length > 48) return null;
  const parts = value.split('/');
  if (parts.length !== 3 || !canonicalInteger(parts[0], 65_535n) ||
      !canonicalInteger(parts[1], 0xffff_ffffn) ||
      !canonicalInteger(parts[2], 0xffff_ffffn)) return null;
  return value;
}

export function parseMatterPointManifest(points) {
  if (!Array.isArray(points) || points.length < 1 || points.length > MAX_POINTS) {
    fail('MATTER_POINT_MANIFEST_INVALID');
  }
  const normalized = points.map(point => {
    if (!point || typeof point !== 'object' || Array.isArray(point) ||
        typeof point.tagId !== 'string' ||
        !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(point.tagId) ||
        typeof point.nodeId !== 'string' || !canonicalInteger(point.nodeId, (1n << 64n) - 1n, false) ||
        !parseAddress(point.address) ||
        !['Boolean', 'Int16', 'Int32', 'Int64', 'Float', 'Double', 'String'].includes(point.dataType) ||
        typeof point.writable !== 'boolean') {
      fail('MATTER_POINT_MANIFEST_INVALID');
    }
    return Object.freeze({
      tagId: point.tagId.toLowerCase(),
      nodeId: point.nodeId,
      address: point.address,
      dataType: point.dataType,
      writable: point.writable,
    });
  });
  if (new Set(normalized.map(point => point.tagId)).size !== normalized.length ||
      new Set(normalized.map(point => point.nodeId + '/' + point.address)).size !== normalized.length) {
    fail('MATTER_POINT_MANIFEST_DUPLICATE');
  }
  return normalized;
}

function normalizeValue(value, dataType) {
  switch (dataType) {
    case 'Boolean':
      if (typeof value !== 'boolean') fail('MATTER_VALUE_TYPE_INVALID');
      return value;
    case 'Int16':
      if (!Number.isInteger(value) || value < -32768 || value > 32767) fail('MATTER_VALUE_RANGE_INVALID');
      return value;
    case 'Int32':
      if (!Number.isInteger(value) || value < -2147483648 || value > 2147483647) fail('MATTER_VALUE_RANGE_INVALID');
      return value;
    case 'Int64': {
      let integer;
      try {
        integer = typeof value === 'bigint' ? value :
          typeof value === 'string' && /^-?(0|[1-9][0-9]*)$/.test(value) ? BigInt(value) :
            Number.isSafeInteger(value) ? BigInt(value) : null;
      } catch { integer = null; }
      if (integer === null || integer < MIN_INT64 || integer > MAX_INT64) fail('MATTER_VALUE_RANGE_INVALID');
      return integer.toString();
    }
    case 'Float':
    case 'Double':
      if (typeof value !== 'number' || !Number.isFinite(value) ||
          (dataType === 'Float' && Math.abs(value) > 3.4028234663852886e38)) fail('MATTER_VALUE_RANGE_INVALID');
      return value;
    case 'String':
      if (typeof value !== 'string' || value.length > MAX_STRING_LENGTH) fail('MATTER_VALUE_TYPE_INVALID');
      return value;
    default:
      fail('MATTER_VALUE_TYPE_UNSUPPORTED');
  }
}

function normalizeNodeId(value) {
  if (typeof value === 'bigint') return value.toString();
  if (typeof value === 'number' && Number.isSafeInteger(value) && value >= 1) return String(value);
  if (typeof value === 'string' && canonicalInteger(value, (1n << 64n) - 1n, false)) return value;
  return null;
}

function responseValue(result, address) {
  if (!result || typeof result !== 'object' || Array.isArray(result) || !Object.hasOwn(result, address)) {
    fail('MATTER_ATTRIBUTE_RESPONSE_INVALID');
  }
  return result[address];
}

export function createMatterBridge({ client, points, send, commandTimeoutMs = 15_000 }) {
  if (!client || typeof client.startListening !== 'function' ||
      typeof client.readAttribute !== 'function' || typeof client.writeAttribute !== 'function' ||
      typeof send !== 'function') throw new TypeError('Matter bridge dependencies are incomplete.');
  const manifest = parseMatterPointManifest(points);
  const byTagId = new Map(manifest.map(point => [point.tagId, point]));
  const byNodeAndAddress = new Map(manifest.map(point => [point.nodeId + '/' + point.address, point]));
  const writeTails = new Map();
  let started = false;
  let closed = false;

  const emit = (point, value) => {
    if (closed) return;
    try {
      send({
        type: 'observation',
        tagId: point.tagId,
        value: normalizeValue(value, point.dataType),
        observedAt: new Date().toISOString(),
      });
    } catch {
      // Attribute values outside the explicitly supported process-value types are dropped.
    }
  };

  const handleRawEvent = event => {
    if (!event || typeof event !== 'object') return;
    if (event.event === 'attribute_updated' && Array.isArray(event.data) && event.data.length === 3) {
      const [rawNodeId, address, value] = event.data;
      const point = byNodeAndAddress.get(normalizeNodeId(rawNodeId) + '/' + address);
      if (point) emit(point, value);
      return;
    }
    if ((event.event === 'node_added' || event.event === 'node_updated') &&
        event.data && typeof event.data === 'object') {
      const nodeId = normalizeNodeId(event.data.node_id);
      const attributes = event.data.attributes;
      if (!nodeId || !attributes || typeof attributes !== 'object' || Array.isArray(attributes)) return;
      for (const [address, value] of Object.entries(attributes)) {
        const point = byNodeAndAddress.get(nodeId + '/' + address);
        if (point) emit(point, value);
      }
    }
  };

  const readPoint = async point => normalizeValue(
    responseValue(await client.readAttribute(BigInt(point.nodeId), point.address, commandTimeoutMs), point.address),
    point.dataType);

  const withWriteLock = async (point, operation) => {
    const previous = writeTails.get(point.tagId) ?? Promise.resolve();
    let release;
    const current = new Promise(resolve => { release = resolve; });
    writeTails.set(point.tagId, current);
    await previous;
    try { return await operation(); }
    finally {
      release();
      if (writeTails.get(point.tagId) === current) writeTails.delete(point.tagId);
    }
  };

  const handleRequest = async request => {
    if (!request || typeof request !== 'object' || Array.isArray(request) ||
        typeof request.id !== 'string' || !/^[0-9a-f]{32}$/i.test(request.id)) return;
    if (!started || closed) {
      send({ id: request.id, ok: false, errorCode: 'MATTER_BRIDGE_NOT_READY' });
      return;
    }
    if (!['read', 'write'].includes(request.op) || typeof request.tagId !== 'string') {
      send({ id: request.id, ok: false, errorCode: 'MATTER_OPERATION_UNSUPPORTED' });
      return;
    }
    const point = byTagId.get(request.tagId.toLowerCase());
    if (!point || request.nodeId !== point.nodeId || request.address !== point.address ||
        request.dataType !== point.dataType || request.writable !== point.writable) {
      send({ id: request.id, ok: false, errorCode: 'MATTER_POINT_BINDING_MISMATCH' });
      return;
    }
    if (request.op === 'read') {
      try {
        send({ id: request.id, ok: true, value: await readPoint(point) });
      } catch {
        send({ id: request.id, ok: false, errorCode: 'MATTER_READ_FAILED' });
      }
      return;
    }
    if (!point.writable) {
      send({ id: request.id, ok: false, errorCode: 'MATTER_VALUE_READ_ONLY' });
      return;
    }
    let dispatched = false;
    try {
      await withWriteLock(point, async () => {
        const requested = normalizeValue(request.value, point.dataType);
        const matterValue = point.dataType === 'Int64' ? BigInt(requested) : requested;
        const writePromise = client.writeAttribute(BigInt(point.nodeId), point.address, matterValue, commandTimeoutMs);
        dispatched = true;
        await writePromise;
        const observed = await readPoint(point);
        if (observed !== requested) fail('MATTER_WRITE_READBACK_MISMATCH');
        send({ id: request.id, ok: true, value: observed });
      });
    } catch {
      send({
        id: request.id,
        ok: false,
        ...(dispatched ? { uncertain: true } : {}),
        errorCode: dispatched ? 'MATTER_WRITE_UNCONFIRMED' : 'MATTER_WRITE_FAILED',
      });
    }
  };

  async function start() {
    if (started || closed) throw new Error('MATTER_BRIDGE_LIFECYCLE_INVALID');
    client.onRawEvent = handleRawEvent;
    await client.startListening();
    started = true;
    for (const node of Object.values(client.nodes ?? {})) {
      const nodeId = normalizeNodeId(node?.node_id);
      const attributes = node?.attributes;
      if (!nodeId || !attributes || typeof attributes !== 'object' || Array.isArray(attributes)) continue;
      for (const [address, value] of Object.entries(attributes)) {
        const point = byNodeAndAddress.get(nodeId + '/' + address);
        if (point) emit(point, value);
      }
    }
  }

  async function close() {
    if (closed) return;
    closed = true;
    try { client.disconnect?.(); } catch { /* Best effort close. */ }
  }

  return Object.freeze({ start, handleRequest, close, points: manifest });
}

export async function connectMatterUnixSocket(socketPath, { MatterClient, WebSocket }) {
  if (typeof socketPath !== 'string' || !socketPath.startsWith('/') || socketPath.length > 100) {
    fail('MATTER_UNIX_SOCKET_INVALID');
  }
  const client = new MatterClient(
    'ws+unix://' + socketPath + ':/ws',
    url => new WebSocket(url),
  );
  client.commandTimeout = 15_000;
  return client;
}

export async function connectAuthenticatedRpc({ host, port, token, connect = connectTcp }) {
  if (host !== '127.0.0.1' || !Number.isInteger(port) || port < 1 || port > 65_535 ||
      typeof token !== 'string' || !/^[0-9a-f]{64}$/.test(token)) {
    fail('MATTER_RPC_CONFIGURATION_INVALID');
  }
  const socket = connect({ host, port });
  socket.setNoDelay(true);
  let buffer = Buffer.alloc(0);
  let closedError = null;
  const queue = [];
  const waiters = [];
  const deliver = frame => {
    const waiter = waiters.shift();
    if (waiter) waiter(frame);
    else queue.push(frame);
  };
  socket.on('data', chunk => {
    buffer = Buffer.concat([buffer, chunk]);
    if (buffer.length > MAX_FRAME_BYTES && !buffer.includes(0x0a)) {
      closedError = new Error('MATTER_RPC_FRAME_INVALID');
      socket.destroy();
      return;
    }
    while (true) {
      const newline = buffer.indexOf(0x0a);
      if (newline < 0) break;
      const frame = buffer.subarray(0, newline);
      buffer = buffer.subarray(newline + 1);
      try {
        deliver(validateRpcFrame(frame));
      } catch {
        closedError = new Error('MATTER_RPC_FRAME_INVALID');
        socket.destroy();
        return;
      }
    }
  });
  socket.on('error', () => {
    closedError ??= new Error('MATTER_RPC_CONNECTION_FAILED');
    while (waiters.length) waiters.shift()(null);
  });
  socket.on('close', () => {
    closedError ??= new Error('MATTER_RPC_CONNECTION_CLOSED');
    while (waiters.length) waiters.shift()(null);
  });
  await new Promise((resolveConnect, rejectConnect) => {
    if (socket.connecting === false) return resolveConnect();
    socket.once('connect', resolveConnect);
    socket.once('error', () => rejectConnect(new Error('MATTER_RPC_CONNECTION_FAILED')));
  });

  const nextFrame = () => queue.length ? Promise.resolve(queue.shift())
    : closedError ? Promise.resolve(null)
      : new Promise(resolveFrame => waiters.push(resolveFrame));
  const send = message => {
    const encoded = Buffer.from(JSON.stringify(message) + '\n', 'utf8');
    if (encoded.length > MAX_FRAME_BYTES) {
      encoded.fill(0);
      fail('MATTER_RPC_FRAME_INVALID');
    }
    socket.write(encoded, () => encoded.fill(0));
  };

  send({ type: 'hello', protocolVersion: 1, token });
  const welcome = await Promise.race([
    nextFrame(),
    new Promise((_, reject) => setTimeout(() => reject(new Error('MATTER_RPC_HANDSHAKE_TIMEOUT')), 10_000)),
  ]);
  if (!welcome || welcome.type !== 'welcome' || welcome.protocolVersion !== 1) {
    socket.destroy();
    throw closedError ?? new Error('MATTER_RPC_HANDSHAKE_REJECTED');
  }
  return Object.freeze({ socket, nextFrame, send, close: () => socket.destroy() });
}

export function validateRpcFrame(frame) {
  if (!Buffer.isBuffer(frame) || frame.length === 0 || frame.length > MAX_FRAME_BYTES) {
    fail('MATTER_RPC_FRAME_INVALID');
  }
  let request;
  try { request = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(frame)); }
  catch { fail('MATTER_RPC_FRAME_INVALID'); }
  if (!request || typeof request !== 'object' || Array.isArray(request)) fail('MATTER_RPC_FRAME_INVALID');
  return request;
}

export async function serveMatterRpcRequests(rpc, bridge) {
  if (!rpc || typeof rpc.nextFrame !== 'function' || typeof rpc.send !== 'function' ||
      !bridge || typeof bridge.handleRequest !== 'function') {
    throw new TypeError('Matter RPC session dependencies are incomplete.');
  }
  const pending = new Set();
  while (true) {
    const request = await rpc.nextFrame();
    if (!request) break;
    if (pending.size >= 32) {
      if (typeof request.id === 'string' && /^[0-9a-f]{32}$/i.test(request.id)) {
        rpc.send({ id: request.id, ok: false, errorCode: 'MATTER_RPC_OVERLOADED' });
      }
      continue;
    }
    let operation;
    operation = Promise.resolve().then(() => bridge.handleRequest(request)).catch(() => {
      if (typeof request?.id === 'string' && /^[0-9a-f]{32}$/i.test(request.id)) {
        try { rpc.send({ id: request.id, ok: false, errorCode: 'MATTER_RPC_REQUEST_FAILED' }); } catch {}
      }
    }).finally(() => pending.delete(operation));
    pending.add(operation);
  }
  await Promise.allSettled(pending);
}
