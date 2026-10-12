import assert from 'node:assert/strict';
import test from 'node:test';
import {
  assertAllowedRequest,
  hasStateExposure,
  mapOnOffObservation,
  normalizeIeee,
  parseRuntimeConfiguration,
  pointKey,
} from '../src/sidecar-core.mjs';
import { installHerdsmanSafetyGuards, requireExistingCoordinatorStrategy } from '../src/safety-guard.mjs';
import { cleanupController, createGracefulShutdown } from '../src/sidecar-lifecycle.mjs';

const env = {
  ZIGBEE_NETWORK_KEY: '00112233445566778899aabbccddeeff',
  ZIGBEE_SERIAL_PATH: '/dev/ttyUSB0',
  ZIGBEE_PAN_ID: '1234',
  ZIGBEE_EXTENDED_PAN_ID: '00124b0001abcdef',
  ZIGBEE_CHANNEL: '15',
  ZIGBEE_BAUD_RATE: '115200',
  ZIGBEE_RPC_HOST: '127.0.0.1',
  ZIGBEE_RPC_PORT: '43210',
  ZIGBEE_RPC_TOKEN: 'a'.repeat(64),
  ZIGBEE_DATABASE_PATH: '/tmp/zigbee.db',
  ZIGBEE_ALLOWED_POINTS: JSON.stringify([{ ieee: '0x00124b0001abcdef', endpoint: 1 }]),
};

test('pins the runtime configuration and normalizes a bounded allowed point', () => {
  const config = parseRuntimeConfiguration(env, 'v24.21.0');
  assert.equal(config.networkKey.length, 16);
  assert.equal(config.panId, 1234);
  assert.equal(config.allowedPoints.has('00124b0001abcdef/1'), true);
  assert.equal(pointKey('0x00124B0001ABCDEF', 1), '00124b0001abcdef/1');
  assert.equal(normalizeIeee('00124B0001ABCDEF'), '00124b0001abcdef');
  assert.throws(() => parseRuntimeConfiguration(env, 'v24.19.0'), /NODE_RUNTIME_VERSION_MISMATCH/);
});

test('fails closed on invalid protected key, network parameters and non-loopback IPC', () => {
  assert.throws(() => parseRuntimeConfiguration({ ...env, ZIGBEE_NETWORK_KEY: 'secret' }, 'v24.21.0'), /INVALID_PROTECTED_NETWORK_KEY/);
  assert.throws(() => parseRuntimeConfiguration({ ...env, ZIGBEE_CHANNEL: '27' }, 'v24.21.0'), /INVALID_CHANNEL/);
  assert.throws(() => parseRuntimeConfiguration({ ...env, ZIGBEE_RPC_HOST: '0.0.0.0' }, 'v24.21.0'), /RPC_MUST_USE_LOOPBACK/);
});

test('allows only configured on/off reads and writes; rejects commissioning and admin RPCs', () => {
  const allowed = new Map([['00124b0001abcdef/1', { ieee: '00124b0001abcdef', endpoint: 1 }]]);
  assert.equal(assertAllowedRequest({ type: 'request', id: '1', method: 'read', ieee: '0x00124b0001abcdef', endpoint: 1 }, allowed).method, 'read');
  assert.equal(assertAllowedRequest({ type: 'request', id: '2', method: 'write', ieee: '00124b0001abcdef', endpoint: 1, value: true }, allowed).value, true);
  for (const method of ['permitJoin', 'removeDevice', 'changeChannel', 'backup', 'restore', 'ota']) {
    assert.throws(() => assertAllowedRequest({ type: 'request', id: '3', method, ieee: '00124b0001abcdef', endpoint: 1 }, allowed), /METHOD_NOT_ALLOWED/);
  }
  assert.throws(() => assertAllowedRequest({ type: 'request', id: '4', method: 'write', ieee: '00124b0001abcdef', endpoint: 1, value: 'on' }, allowed), /BOOLEAN_VALUE_REQUIRED/);
  assert.throws(() => assertAllowedRequest({ type: 'request', id: '5', method: 'read', ieee: '00124b0001abcdef', endpoint: 2 }, allowed), /POINT_NOT_CONFIGURED/);
});

test('accepts only readback/report values from the allowed on/off state point', () => {
  const allowed = new Map([['00124b0001abcdef/1', { ieee: '00124b0001abcdef', endpoint: 1 }]]);
  const definition = { exposes: [{ type: 'switch', features: [{ property: 'state', access: 7 }] }] };
  assert.equal(hasStateExposure(definition, { STATE: 1 }, 1), true);
  assert.equal(hasStateExposure(definition, { STATE: 1, SET: 2 }, 3), true);
  assert.equal(hasStateExposure({ exposes: [{ type: 'switch', features: [{ property: 'state', access: 1 }] }] }, { STATE: 1, SET: 2 }, 3), false);
  const message = { type: 'attributeReport', cluster: 'genOnOff', device: { ieeeAddr: '0x00124b0001abcdef' }, endpoint: { ID: 1 }, data: { onOff: 1 } };
  assert.equal(mapOnOffObservation(message, allowed).value, true);
  assert.equal(mapOnOffObservation({ ...message, type: 'commandOn' }, allowed), null);
  assert.equal(mapOnOffObservation({ ...message, endpoint: { ID: 2 } }, allowed), null);
  assert.equal(mapOnOffObservation({ ...message, data: { onOff: 'on' } }, allowed), null);
});

test('blocks commissioning, restore, coordinator group changes, joins and channel changes before side effects', async () => {
  class FakeManager {
    constructor(strategy) { this.strategy = strategy; this.commissionCalls = 0; }
    async determineStrategy() { return this.strategy; }
    async addToGroup() { this.groupCalls = (this.groupCalls ?? 0) + 1; }
  }
  class FakeController {
    async changeChannel() { this.channelCalls = (this.channelCalls ?? 0) + 1; }
    async onDeviceJoined() { this.joinCalls = (this.joinCalls ?? 0) + 1; }
    async onDeviceJoinedGreenPower() { this.greenPowerJoinCalls = (this.greenPowerJoinCalls ?? 0) + 1; }
  }
  installHerdsmanSafetyGuards(FakeController, FakeManager);

  assert.equal(requireExistingCoordinatorStrategy('startup'), 'startup');
  for (const strategy of ['startCommissioning', 'restoreBackup']) {
    const manager = new FakeManager(strategy);
    await assert.rejects(manager.determineStrategy(), /commissioning and restore are disabled/);
    assert.equal(manager.commissionCalls, 0);
  }
  const manager = new FakeManager('startup');
  await manager.addToGroup(242, 242);
  assert.equal(manager.groupCalls, undefined);
  const controller = new FakeController();
  await controller.onDeviceJoined({});
  await controller.onDeviceJoinedGreenPower({});
  await assert.rejects(controller.changeChannel(15, 20, 0), (error) => error.code === 'NETWORK_CHANGE_FORBIDDEN');
  assert.equal(controller.joinCalls, undefined);
  assert.equal(controller.greenPowerJoinCalls, undefined);
  assert.equal(controller.channelCalls, undefined);
});

test('persists local coordinator state and stops without permit-join or backup administration', async () => {
  const calls = [];
  const controller = {
    backupTimer: setInterval(() => {}, 60_000),
    databaseSaveTimer: setInterval(() => {}, 60_000),
    database: {},
    removeAllListeners: () => calls.push('controller-listeners'),
    databaseSave: () => calls.push('database-save'),
    backup: () => calls.push('backup'),
    permitJoin: () => calls.push('permit-join'),
    adapter: {
      removeAllListeners: () => calls.push('adapter-listeners'),
      stop: async () => calls.push('adapter-stop'),
    },
  };

  await cleanupController(controller);

  assert.deepEqual(calls, ['controller-listeners', 'database-save', 'adapter-listeners', 'adapter-stop']);
});

test('shutdown drains accepted RPC work once and closes the channel even when adapter stop fails', async () => {
  const calls = [];
  let releasePendingRequests;
  const pendingRequests = new Promise((resolve) => { releasePendingRequests = resolve; });
  const stopFailure = new Error('coordinator stop failed');
  const controller = {
    backupTimer: setInterval(() => {}, 60_000),
    databaseSaveTimer: setInterval(() => {}, 60_000),
    database: {},
    removeAllListeners: () => calls.push('controller-listeners'),
    databaseSave: () => calls.push('database-save'),
    adapter: {
      removeAllListeners: () => calls.push('adapter-listeners'),
      stop: async () => { calls.push('adapter-stop'); throw stopFailure; },
    },
  };
  const host = {
    reader: { close: () => calls.push('reader-close') },
    socket: { destroy: () => calls.push('socket-destroy') },
  };
  const shutdown = createGracefulShutdown(
    controller,
    host,
    () => { calls.push('drain-requests'); return pendingRequests; },
    () => calls.push('begin-shutdown'),
  );

  let first;
  try {
    first = shutdown();
    const second = shutdown();
    assert.strictEqual(first, second);
    await new Promise((resolve) => setImmediate(resolve));
    assert.deepEqual(calls, ['begin-shutdown', 'reader-close', 'drain-requests']);

    releasePendingRequests();
    await assert.rejects(first, (error) => error === stopFailure);
    assert.deepEqual(calls, [
      'begin-shutdown',
      'reader-close',
      'drain-requests',
      'controller-listeners',
      'database-save',
      'adapter-listeners',
      'adapter-stop',
      'socket-destroy',
    ]);
  } finally {
    releasePendingRequests();
    await first?.catch(() => {});
  }
});
