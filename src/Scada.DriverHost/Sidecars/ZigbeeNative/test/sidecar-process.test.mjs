import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { EventEmitter, once } from 'node:events';
import { createInterface } from 'node:readline';
import { createServer } from 'node:net';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const fixtureDirectory = dirname(fileURLToPath(import.meta.url));
const packageDirectory = dirname(fixtureDirectory);
const fixturePath = join(fixtureDirectory, 'fixtures', 'sidecar-fake-coordinator.mjs');

function waitForRpc(messages, events, predicate) {
  const existing = messages.find(predicate);
  if (existing) return Promise.resolve(existing);

  return new Promise((resolve, reject) => {
    const finish = (error, message) => {
      clearTimeout(timer);
      events.removeListener('message', onMessage);
      if (error) reject(error); else resolve(message);
    };
    const onMessage = (message) => {
      if (predicate(message)) finish(null, message);
    };
    const timer = setTimeout(() => finish(new Error('Timed out waiting for sidecar RPC message.')), 5_000);
    events.on('message', onMessage);
    const arrivedBeforeListener = messages.find(predicate);
    if (arrivedBeforeListener) finish(null, arrivedBeforeListener);
  });
}

test('sidecar child starts with a fake coordinator, authenticates loopback RPC, and drains on stop', { timeout: 10_000 }, async (t) => {
  const stateDirectory = mkdtempSync(join(tmpdir(), 'zigbee-native-process-'));
  const auditPath = join(stateDirectory, 'coordinator-audit.json');
  const token = 'ab'.repeat(32);
  const server = createServer();
  let rpcSocket;
  let hello;
  const rpcMessages = [];
  const rpcEvents = new EventEmitter();
  let child;
  let childExited = false;
  t.after(async () => {
    if (child && !childExited) {
      child.kill('SIGKILL');
      await once(child, 'exit').catch(() => {});
    }
    if (server.listening) server.close();
    rmSync(stateDirectory, { recursive: true, force: true });
  });

  server.on('connection', (socket) => {
    rpcSocket = socket;
    const lines = createInterface({ input: socket, crlfDelay: Infinity });
    lines.on('line', (line) => {
      let message;
      try { message = JSON.parse(line); } catch { return; }
      if (!hello) {
        hello = message;
        socket.write(`${JSON.stringify({ type: 'welcome', schemaVersion: 1 })}\n`);
        return;
      }
      rpcMessages.push(message);
      rpcEvents.emit('message', message);
    });
  });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });

  const port = server.address().port;
  child = spawn(process.execPath, [fixturePath], {
    cwd: packageDirectory,
    env: {
      ...process.env,
      ZIGBEE_NETWORK_KEY: '00112233445566778899aabbccddeeff',
      ZIGBEE_SERIAL_PATH: '/dev/fake-zigbee-coordinator',
      ZIGBEE_PAN_ID: '4660',
      ZIGBEE_EXTENDED_PAN_ID: '00124b0001abcdef',
      ZIGBEE_CHANNEL: '15',
      ZIGBEE_BAUD_RATE: '115200',
      ZIGBEE_RPC_HOST: '127.0.0.1',
      ZIGBEE_RPC_PORT: String(port),
      ZIGBEE_RPC_TOKEN: token,
      ZIGBEE_DATABASE_PATH: join(stateDirectory, 'herdsman.db'),
      ZIGBEE_ALLOWED_POINTS: JSON.stringify([{ ieee: '00124b0001abcdef', endpoint: 1 }]),
      ZIGBEE_TEST_AUDIT_PATH: auditPath,
    },
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  const stdout = [];
  const stderr = [];
  const stdoutLines = createInterface({ input: child.stdout, crlfDelay: Infinity });
  const ready = new Promise((resolve, reject) => {
    stdoutLines.on('line', (line) => {
      stdout.push(line);
      let message;
      try { message = JSON.parse(line); } catch { return; }
      if (message.type === 'ready') resolve(message);
      if (message.type === 'error') reject(new Error(`Sidecar failed to start: ${message.code}`));
    });
    child.once('error', reject);
    child.once('exit', (code) => {
      childExited = true;
      reject(new Error(`Sidecar exited before readiness (code ${code}); stderr: ${stderr.join('')}`));
    });
  });
  child.stderr.on('data', (chunk) => stderr.push(chunk.toString()));
  const exit = new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code, signal) => {
      childExited = true;
      resolve({ code, signal });
    });
  });

  const readyMessage = await ready;
  assert.deepEqual(readyMessage, {
    type: 'ready',
    artifactId: 'elitescada.zigbee.native',
    artifactVersion: '1.0.0',
    runtimeVersion: '24.21.0',
    schemaVersion: 1,
    message: 'Existing TI zStack coordinator started with network mutation guards enabled.',
  });
  assert.deepEqual(hello, {
    type: 'hello',
    token,
    artifactId: 'elitescada.zigbee.native',
    artifactVersion: '1.0.0',
    schemaVersion: 1,
  });

  const sendRequest = async (request) => {
    const response = waitForRpc(rpcMessages, rpcEvents, (message) =>
      message.type === 'response' && message.id === request.id);
    rpcSocket.write(`${JSON.stringify(request)}\n`);
    return await response;
  };

  const initialRead = await sendRequest({
    type: 'request', id: 'read-initial', method: 'read', ieee: '00124b0001abcdef', endpoint: 1,
  });
  assert.equal(initialRead.ok, true);
  assert.equal(initialRead.accepted, false);
  assert.equal(initialRead.value, false);
  assert.match(initialRead.observedAt, /^\d{4}-\d{2}-\d{2}T/);

  const deviceReport = waitForRpc(rpcMessages, rpcEvents, (message) =>
    message.type === 'report' && message.ieee === '00124b0001abcdef' && message.endpoint === 1 && message.value === true);
  const writeOn = await sendRequest({
    type: 'request', id: 'write-on', method: 'write', ieee: '00124b0001abcdef', endpoint: 1, value: true,
  });
  assert.equal(writeOn.ok, true);
  assert.equal(writeOn.accepted, true);
  assert.equal(writeOn.value, true);
  assert.match(writeOn.observedAt, /^\d{4}-\d{2}-\d{2}T/);
  const report = await deviceReport;
  assert.equal(report.type, 'report');
  assert.equal(report.ieee, '00124b0001abcdef');
  assert.equal(report.endpoint, 1);
  assert.equal(report.value, true);
  assert.equal(report.source, 'device-report');
  assert.match(report.observedAt, /^\d{4}-\d{2}-\d{2}T/);

  const readAfterWrite = await sendRequest({
    type: 'request', id: 'read-after-write', method: 'read', ieee: '00124b0001abcdef', endpoint: 1,
  });
  assert.equal(readAfterWrite.ok, true);
  assert.equal(readAfterWrite.accepted, false);
  assert.equal(readAfterWrite.value, true);

  const unconfiguredPoint = await sendRequest({
    type: 'request', id: 'unconfigured-point', method: 'read', ieee: '00124b0001abcdef', endpoint: 2,
  });
  assert.deepEqual(unconfiguredPoint, {
    type: 'response',
    id: 'unconfigured-point',
    ok: false,
    accepted: false,
    errorCode: 'POINT_NOT_CONFIGURED',
  });

  child.stdin.write('stop\n');
  assert.deepEqual(await exit, { code: 0, signal: null });
  assert.equal(rpcSocket?.destroyed, true);
  assert.deepEqual(rpcMessages.filter((message) => message.type === 'response').map((message) => message.id), [
    'read-initial', 'write-on', 'read-after-write', 'unconfigured-point',
  ]);
  assert.equal(rpcMessages.filter((message) => message.type === 'report').length, 1);

  const audit = JSON.parse(readFileSync(auditPath, 'utf8'));
  assert.deepEqual(audit, {
    networkKeyLength: 16,
    panId: 4660,
    extendedPanId: '00124b0001abcdef',
    channelList: [15],
    networkKeyDistribute: false,
    serialAdapter: 'zstack',
    serialPath: '/dev/fake-zigbee-coordinator',
    baudRate: 115200,
    rtscts: false,
    databasePath: join(stateDirectory, 'herdsman.db'),
    started: true,
    databaseSaved: true,
    adapterListenersRemoved: true,
    adapterStopped: true,
    commands: [{ cluster: 'genOnOff', command: 'on', payload: {}, options: { sendPolicy: 'immediate' } }],
    reads: [false, true, true].map((value) => ({
      cluster: 'genOnOff', attributes: ['onOff'], options: { sendPolicy: 'immediate' }, value,
    })),
  });
  assert.deepEqual(audit.commands, [{
    cluster: 'genOnOff', command: 'on', payload: {}, options: { sendPolicy: 'immediate' },
  }]);
  assert.deepEqual(audit.reads.map((read) => read.value), [false, true, true]);
  assert.equal(audit.reads.every((read) => read.cluster === 'genOnOff' && read.attributes.includes('onOff')),
    true);
  assert.equal(stderr.join(''), '');
  assert.equal(stdout.join('\n').includes('00112233445566778899aabbccddeeff'), false);
});
