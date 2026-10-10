import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
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

test('sidecar child starts with a fake coordinator, authenticates loopback RPC, and drains on stop', { timeout: 10_000 }, async (t) => {
  const stateDirectory = mkdtempSync(join(tmpdir(), 'zigbee-native-process-'));
  const auditPath = join(stateDirectory, 'coordinator-audit.json');
  const token = 'ab'.repeat(32);
  const server = createServer();
  let rpcSocket;
  let hello;
  const rpcMessages = [];
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

  child.stdin.write('stop\n');
  assert.deepEqual(await exit, { code: 0, signal: null });
  assert.equal(rpcSocket?.destroyed, true);
  assert.deepEqual(rpcMessages, []);

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
  });
  assert.equal(stderr.join(''), '');
  assert.equal(stdout.join('\n').includes('00112233445566778899aabbccddeeff'), false);
});
