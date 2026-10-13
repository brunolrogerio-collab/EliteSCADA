import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:net';
import {
  connectAuthenticatedRpc,
  connectMatterUnixSocket,
  createMatterBridge,
  parseMatterPointManifest,
  serveMatterRpcRequests,
  validateRpcFrame,
} from '../src/bridge.mjs';

const BOOL_TAG = 'b919b75f-bf84-4e82-8e32-e9aac36fb919';
const INT64_TAG = 'c929b75f-bf84-4e82-8e32-e9aac36fb929';
const BOOL_POINT = Object.freeze({
  tagId: BOOL_TAG,
  nodeId: '18446744073709551614',
  address: '1/6/0',
  dataType: 'Boolean',
  writable: true,
});
const INT64_POINT = Object.freeze({
  tagId: INT64_TAG,
  nodeId: '18446744073709551614',
  address: '1/6/1',
  dataType: 'Int64',
  writable: false,
});

function fakeClient(initial = {}) {
  const values = new Map(initial);
  const calls = [];
  const client = {
    nodes: {
      ['18446744073709551614']: {
        node_id: 18446744073709551614n,
        attributes: Object.fromEntries(values),
      },
    },
    onRawEvent: null,
    async startListening() { calls.push(['start']); },
    async readAttribute(nodeId, address) {
      calls.push(['read', nodeId.toString(), address]);
      return { [address]: values.get(address) };
    },
    async writeAttribute(nodeId, address, value) {
      calls.push(['write', nodeId.toString(), address, value]);
      values.set(address, value);
    },
    disconnect() { calls.push(['disconnect']); },
    emit(event) { client.onRawEvent?.(event); },
    calls,
    values,
  };
  return client;
}

function request(point, overrides = {}) {
  return {
    id: '0123456789abcdef0123456789abcdef',
    op: 'read',
    tagId: point.tagId,
    nodeId: point.nodeId,
    address: point.address,
    dataType: point.dataType,
    writable: point.writable,
    ...overrides,
  };
}

test('Matter point manifest validates canonical node, attribute path, types, and duplicate protection', () => {
  assert.deepEqual(parseMatterPointManifest([BOOL_POINT]), [BOOL_POINT]);
  assert.throws(() => parseMatterPointManifest([{ ...BOOL_POINT, nodeId: '01' }]), /MATTER_POINT_MANIFEST_INVALID/);
  assert.throws(() => parseMatterPointManifest([{ ...BOOL_POINT, address: '1/06/0' }]), /MATTER_POINT_MANIFEST_INVALID/);
  assert.throws(() => parseMatterPointManifest([{ ...BOOL_POINT, dataType: 'DateTime' }]), /MATTER_POINT_MANIFEST_INVALID/);
  assert.throws(() => parseMatterPointManifest([BOOL_POINT, { ...BOOL_POINT, tagId: INT64_TAG }]), /MATTER_POINT_MANIFEST_DUPLICATE/);
});

test('Matter bridge emits initial and subscribed values for only manifest-bound attributes', async () => {
  const client = fakeClient([['1/6/0', true], ['1/6/2', 'ignored']]);
  const events = [];
  const bridge = createMatterBridge({ client, points: [BOOL_POINT], send: value => events.push(value) });

  await bridge.start();
  assert.equal(events.length, 1);
  assert.deepEqual(
    { tagId: events[0].tagId, value: events[0].value },
    { tagId: BOOL_TAG, value: true },
  );

  client.emit({ event: 'attribute_updated', data: [18446744073709551614n, '1/6/0', false] });
  client.emit({ event: 'attribute_updated', data: [7n, '1/6/0', true] });
  assert.equal(events.length, 2);
  assert.equal(events[1].value, false);
  await bridge.close();
  assert.deepEqual(client.calls.at(-1), ['disconnect']);
});

test('Matter bridge limits requests to the exact point manifest and returns fresh read values', async () => {
  const client = fakeClient([['1/6/0', true]]);
  const replies = [];
  const bridge = createMatterBridge({ client, points: [BOOL_POINT], send: value => replies.push(value) });
  await bridge.start();
  replies.length = 0;

  await bridge.handleRequest(request(BOOL_POINT));
  await bridge.handleRequest(request(BOOL_POINT, { address: '1/6/2' }));
  await bridge.handleRequest(request(BOOL_POINT, { op: 'remove_node' }));
  assert.deepEqual(replies.map(reply => reply.errorCode ?? reply.value), [
    true,
    'MATTER_POINT_BINDING_MISMATCH',
    'MATTER_OPERATION_UNSUPPORTED',
  ]);
  assert.equal(client.calls.filter(call => call[0] === 'read').length, 1);
});

test('Matter writes are serialized per TAG and require a read-back confirmation', async () => {
  const client = fakeClient([['1/6/0', false]]);
  const replies = [];
  const bridge = createMatterBridge({ client, points: [BOOL_POINT], send: value => replies.push(value) });
  await bridge.start();
  replies.length = 0;

  const write = value => bridge.handleRequest(request(BOOL_POINT, { op: 'write', value }));
  await Promise.all([write(true), write(false)]);
  assert.deepEqual(client.calls.filter(call => call[0] === 'write').map(call => call[3]), [true, false]);
  assert.deepEqual(replies.map(reply => reply.value), [true, false]);
  assert.ok(replies.every(reply => reply.ok));
});

test('Matter bridge reports uncertain writes after dispatch and rejects read-only writes', async () => {
  const client = fakeClient([['1/6/1', 12n]]);
  client.writeAttribute = async () => { throw new Error('network detail must not escape'); };
  const replies = [];
  const bridge = createMatterBridge({ client, points: [INT64_POINT], send: value => replies.push(value) });
  await bridge.start();
  await bridge.handleRequest(request(INT64_POINT, { op: 'write', value: '13' }));
  assert.equal(replies[1].errorCode, 'MATTER_VALUE_READ_ONLY');
  assert.equal(replies[1].uncertain, undefined);

  const writablePoint = { ...INT64_POINT, writable: true };
  const uncertain = createMatterBridge({
    client,
    points: [writablePoint],
    send: value => replies.push(value),
  });
  await uncertain.start();
  replies.length = 0;
  await uncertain.handleRequest(request(writablePoint, { op: 'write', value: '13' }));
  assert.equal(replies[0].errorCode, 'MATTER_WRITE_UNCONFIRMED');
  assert.equal(replies[0].uncertain, true);
});

test('Matter bridge preserves bounded Int64 values without JavaScript number rounding', async () => {
  const client = fakeClient([['1/6/1', 9223372036854775807n]]);
  const events = [];
  const bridge = createMatterBridge({ client, points: [INT64_POINT], send: value => events.push(value) });
  await bridge.start();
  assert.equal(events[0].value, '9223372036854775807');
  await bridge.handleRequest(request(INT64_POINT));
  assert.equal(events[1].value, '9223372036854775807');
});

test('Matter WebSocket client uses the private Unix socket URL and refuses non-absolute paths', async () => {
  let received;
  class FakeClient {
    constructor(url, factory) { received = { url, factory }; }
  }
  const FakeWebSocket = class {};
  await connectMatterUnixSocket('/run/elitescada/matter.sock', { MatterClient: FakeClient, WebSocket: FakeWebSocket });
  assert.equal(received.url, 'ws+unix:///run/elitescada/matter.sock:/ws');
  assert.ok(received.factory('ws+unix:///run/elitescada/matter.sock:/ws') instanceof FakeWebSocket);
  await assert.rejects(
    connectMatterUnixSocket('127.0.0.1:5580', { MatterClient: FakeClient, WebSocket: FakeWebSocket }),
    /MATTER_UNIX_SOCKET_INVALID/,
  );
});

test('Matter RPC frame parser rejects oversized, malformed, and non-object input', () => {
  assert.deepEqual(validateRpcFrame(Buffer.from('{"id":"x"}')), { id: 'x' });
  assert.throws(() => validateRpcFrame(Buffer.alloc(65_537, 0x61)), /MATTER_RPC_FRAME_INVALID/);
  assert.throws(() => validateRpcFrame(Buffer.from('{broken')), /MATTER_RPC_FRAME_INVALID/);
  assert.throws(() => validateRpcFrame(Buffer.from('[]')), /MATTER_RPC_FRAME_INVALID/);
});

test('fake Matter client exchanges an authenticated loopback read and drains in-flight work on close', async () => {
  const token = 'a'.repeat(64);
  const frames = [];
  const waiters = [];
  const rpcSocket = { current: null };
  const server = createServer(socket => {
    rpcSocket.current = socket;
    let buffer = Buffer.alloc(0);
    let authenticated = false;
    socket.on('data', chunk => {
      buffer = Buffer.concat([buffer, chunk]);
      while (true) {
        const newline = buffer.indexOf(0x0a);
        if (newline < 0) break;
        const frame = JSON.parse(buffer.subarray(0, newline).toString('utf8'));
        buffer = buffer.subarray(newline + 1);
        if (!authenticated) {
          if (frame.type !== 'hello' || frame.protocolVersion !== 1 || frame.token !== token) {
            socket.destroy();
            return;
          }
          authenticated = true;
          socket.write('{"type":"welcome","protocolVersion":1}\n');
          continue;
        }
        const waiter = waiters.shift();
        if (waiter) waiter(frame);
        else frames.push(frame);
      }
    });
  });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();
  let rpc;
  let bridge;
  let requestLoop;
  try {
    rpc = await connectAuthenticatedRpc({ host: '127.0.0.1', port, token });
    const client = fakeClient([['1/6/0', true]]);
    bridge = createMatterBridge({ client, points: [BOOL_POINT], send: rpc.send });
    await bridge.start();
    const initial = await takeServerFrame(frames, waiters);
    assert.deepEqual({ type: initial.type, tagId: initial.tagId, value: initial.value }, {
      type: 'observation', tagId: BOOL_TAG, value: true,
    });

    requestLoop = serveMatterRpcRequests(rpc, bridge);
    rpcSocket.current.write(JSON.stringify(request(BOOL_POINT)) + '\n');
    const response = await takeServerFrame(frames, waiters);
    assert.deepEqual({ id: response.id, ok: response.ok, value: response.value }, {
      id: '0123456789abcdef0123456789abcdef', ok: true, value: true,
    });

    rpcSocket.current.destroy();
    await requestLoop;
    requestLoop = null;
    assert.deepEqual(client.calls.filter(call => call[0] === 'read'), [['read', BOOL_POINT.nodeId, BOOL_POINT.address]]);
  } finally {
    rpcSocket.current?.destroy();
    rpc?.close();
    await bridge?.close();
    if (requestLoop) await requestLoop;
    await new Promise(resolve => server.close(() => resolve()));
  }
});

function takeServerFrame(frames, waiters) {
  if (frames.length) return Promise.resolve(frames.shift());
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Timed out waiting for fake RPC frame.')), 2000);
    waiters.push(frame => {
      clearTimeout(timer);
      resolve(frame);
    });
  });
}
