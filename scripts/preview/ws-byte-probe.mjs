// DIAGNOSTIC ONLY — MUST NEVER MERGE.
// Passive WebSocket byte probe for the Wave 14 post-C26 stability investigation.
// It changes no product state and deliberately records no authentication material.

import http from 'node:http';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const [, , portText, statusPath, label = 'ws'] = process.argv;
const port = Number(portText);
const cookie = process.env.ELITESCADA_DIAG_AUTH_COOKIE ?? '';
delete process.env.ELITESCADA_DIAG_AUTH_COOKIE;

if (!Number.isInteger(port) || port <= 0 || !statusPath || !cookie) {
  console.error('ws-byte-probe requires <port> <status-path> <label> and ELITESCADA_DIAG_AUTH_COOKIE');
  process.exit(2);
}

fs.mkdirSync(path.dirname(statusPath), { recursive: true });

const state = {
  label,
  port,
  startedAtUtc: new Date().toISOString(),
  everConnected: false,
  connected: false,
  handshakeStatus: null,
  connectedAtUtc: null,
  closedAtUtc: null,
  lastDataAtUtc: null,
  bytesReceived: 0,
  dataEvents: 0,
  error: null
};

function persist() {
  const temp = `${statusPath}.tmp`;
  fs.writeFileSync(temp, `${JSON.stringify(state)}\n`, { mode: 0o600 });
  fs.renameSync(temp, statusPath);
}

persist();

const key = crypto.randomBytes(16).toString('base64');
const request = http.request({
  host: '127.0.0.1',
  port,
  path: '/ws/tags',
  method: 'GET',
  headers: {
    Connection: 'Upgrade',
    Upgrade: 'websocket',
    'Sec-WebSocket-Version': '13',
    'Sec-WebSocket-Key': key,
    Cookie: cookie
  }
});

let socketRef = null;

request.on('upgrade', (response, socket, head) => {
  socketRef = socket;
  state.handshakeStatus = response.statusCode ?? 101;
  state.everConnected = true;
  state.connected = true;
  state.connectedAtUtc = new Date().toISOString();
  state.error = null;
  persist();

  const onData = (chunk) => {
    if (!chunk || chunk.length === 0) return;
    state.bytesReceived += chunk.length;
    state.dataEvents += 1;
    state.lastDataAtUtc = new Date().toISOString();
    persist();
  };

  if (head?.length) onData(head);
  socket.on('data', onData);
  socket.on('error', (error) => {
    state.error = error?.message ?? String(error);
    state.connected = false;
    state.closedAtUtc = new Date().toISOString();
    persist();
  });
  socket.on('close', () => {
    state.connected = false;
    state.closedAtUtc = new Date().toISOString();
    persist();
  });
});

request.on('response', (response) => {
  state.handshakeStatus = response.statusCode ?? null;
  state.error = `upgrade rejected with HTTP ${state.handshakeStatus}`;
  persist();
  response.resume();
});

request.on('error', (error) => {
  state.error = error?.message ?? String(error);
  state.connected = false;
  state.closedAtUtc = new Date().toISOString();
  persist();
});

request.end();

const heartbeat = setInterval(persist, 1000);
heartbeat.unref();

function shutdown() {
  clearInterval(heartbeat);
  state.connected = false;
  state.closedAtUtc = new Date().toISOString();
  persist();
  try { socketRef?.destroy(); } catch {}
  process.exit(0);
}

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
