import assert from "node:assert/strict";
import { EventEmitter, once } from "node:events";
import { PassThrough } from "node:stream";
import test from "node:test";
import { WebSocketServer } from "ws";
import { ZWaveServerClient } from "../src/zwave-server-client.mjs";
import { formatValueAddress, validateValueMetadata } from "../src/value-policy.mjs";
import { createValueUpdateRouter, readFirstLineBuffer, routeInitialPersistentValues, startLoopbackZWaveServer, WriteReportTracker } from "../src/sidecar-contract.mjs";
import { parseBootstrap } from "../src/sidecar.mjs";

test("bootstrap reads a bounded CRLF frame before later stop input", async () => {
  const stream = new PassThrough();
  const framePromise = readFirstLineBuffer(stream, 64);
  stream.write(Buffer.from("bootstrap\r\n", "utf8"));
  const frame = await framePromise;
  assert.equal(frame.toString("utf8"), "bootstrap");
  frame.fill(0);
  const restPromise = once(stream, "data");
  stream.write(Buffer.from("stop\n", "utf8"));
  stream.resume();
  const [rest] = await restPromise;
  assert.equal(rest.toString("utf8"), "stop\n");
  stream.destroy();
});

test("bootstrap rejects extra input in the bootstrap frame", async () => {
  const stream = new PassThrough();
  const framePromise = readFirstLineBuffer(stream, 64);
  stream.write(Buffer.from("bootstrap\nextra\n", "utf8"));
  await assert.rejects(framePromise, /BOOTSTRAP_TRAILING_DATA/);
  stream.destroy();
});

test("bootstrap frame over limit fails closed", async () => {
  const stream = new PassThrough();
  const framePromise = readFirstLineBuffer(stream, 4);
  stream.write(Buffer.from("12345\n"));
  await assert.rejects(framePromise, /BOOTSTRAP_TOO_LARGE/);
  stream.destroy();
});

test("bootstrap parse wipes serialized key bytes while retaining only the driver key buffer", () => {
  const originalKey = Array.from({ length: 16 }, (_, index) => index);
  const frame = Buffer.from(JSON.stringify({
    type: "bootstrap",
    configuration: { serialPath: "COM7", cacheDirectory: "C:/test-cache", nvmIdentity: "homeid:0x12345678" },
    points: [{
      tagId: "6fb05c0a-8ec5-4ba8-9d62-c7ea6a4c3e9c",
      address: "node/5/endpoint/0/cc/37/property/string/Current%20value/key/none/-",
      dataType: "Int32",
      writable: true,
    }],
    keys: { s0Legacy: originalKey },
  }));
  const bootstrap = parseBootstrap(frame);
  assert.ok(frame.every(byte => byte === 0));
  assert.deepEqual([...bootstrap.securityKeys.S0_Legacy], [...originalKey]);
  assert.equal(bootstrap.expectedHomeId, 0x12345678);
  assert.equal(bootstrap.points[0].valueId.property, "Current value");
  for (const key of Object.values(bootstrap.securityKeys)) key.fill(0);
});

test("failed bootstrap parsing clears keys already decoded", () => {
  const frame = Buffer.from(JSON.stringify({
    type: "bootstrap",
    configuration: { serialPath: "COM7", cacheDirectory: "C:/test-cache", nvmIdentity: "invalid" },
    points: [{
      tagId: "6fb05c0a-8ec5-4ba8-9d62-c7ea6a4c3e9c",
      address: "node/5/endpoint/0/cc/37/property/string/value/key/none/-",
      dataType: "Int32",
      writable: true,
    }],
    keys: { s0Legacy: Array(16).fill(7) },
  }));
  assert.throws(() => parseBootstrap(frame), /NVM_IDENTITY_INVALID/);
  assert.ok(frame.every(byte => byte === 0));
});

test("upstream binds to the successful candidate port, retrying only an occupied probe port", async () => {
  const ports = [43111, 43112];
  const attempts = [];
  const servers = [];
  const started = await startLoopbackZWaveServer({
    reservePort: async () => ports.shift(),
    createServer: port => {
      const server = {
        start: async () => {
          attempts.push(port);
          if (attempts.length === 1) throw Object.assign(new Error("occupied"), { code: "EADDRINUSE" });
        },
        destroy: async () => {},
      };
      servers.push(server);
      return server;
    },
  });
  assert.deepEqual(attempts, [43111, 43112]);
  assert.equal(started.server, servers[1]);
  assert.equal(started.port, 43112);
  assert.equal("port" in started.server, false);
});

test("upstream does not retry non-bind startup failures", async () => {
  let attempts = 0;
  await assert.rejects(startLoopbackZWaveServer({
    reservePort: async () => 43111,
    createServer: () => ({
      start: async () => { attempts++; throw Object.assign(new Error("failed"), { code: "EACCES" }); },
      destroy: async () => {},
    }),
  }), /LOCAL_UPSTREAM_START_FAILED/);
  assert.equal(attempts, 1);
});

class FakePeerWebSocket extends EventEmitter {
  static OPEN = 1;
  static CLOSED = 3;
  static latest;
  requests = [];

  constructor() {
    super();
    this.readyState = 0;
    FakePeerWebSocket.latest = this;
    queueMicrotask(() => {
      this.readyState = FakePeerWebSocket.OPEN;
      this.emit("open");
      this.#message({
        type: "version", serverVersion: "3.11.0", driverVersion: "15.31.1",
        minSchemaVersion: 1, maxSchemaVersion: 46, homeId: 0x12345678,
      });
    });
  }

  send(raw) {
    const request = JSON.parse(raw);
    this.requests.push(request);
    if (request.command === "initialize" || request.command === "start_listening") {
      queueMicrotask(() => this.#message({
        type: "result", messageId: request.messageId, success: true,
        result: request.command === "start_listening" ? { state: { homeId: 0x12345678, nodes: [] } } : {},
      }));
    } else if (request.command === "node.poll_value") {
      queueMicrotask(() => this.#message({ type: "result", messageId: request.messageId, success: true, result: { value: true } }));
    }
  }

  emitValueUpdated(event) { this.#message({ type: "event", event }); }
  close() { this.readyState = FakePeerWebSocket.CLOSED; queueMicrotask(() => this.emit("close")); }
  terminate() { this.close(); }
  #message(value) { this.emit("message", Buffer.from(JSON.stringify(value), "utf8")); }
}

test("pinned upstream value event shape routes observation and confirms pending write report", async () => {
  const client = new ZWaveServerClient("ws://127.0.0.1:3000", { WebSocketImpl: FakePeerWebSocket });
  await client.connect(0x12345678);
  const tagId = "6fb05c0a-8ec5-4ba8-9d62-c7ea6a4c3e9c";
  const point = {
    tagId,
    address: formatValueAddress(5, {
      endpoint: 0,
      commandClass: 37,
      property: "Current value / 🚀",
      propertyKey: "Target #1",
    }),
  };
  const observations = [];
  const tracker = new WriteReportTracker();
  const router = createValueUpdateRouter(new Map([[point.address, point]]), (routedPoint, value) => {
    observations.push({ tagId: routedPoint.tagId, value });
    tracker.observe(routedPoint.tagId, value);
  });
  const confirmation = tracker.waitFor(tagId, 42, 1000);
  client.onEvent = router;
  FakePeerWebSocket.latest.emitValueUpdated({
    source: "node",
    event: "value updated",
    nodeId: 5,
    args: {
      commandClass: 37,
      endpoint: 0,
      property: "Current value / 🚀",
      propertyKey: "Target #1",
      prevValue: 41,
      newValue: 42,
      source: "node",
    },
  });
  assert.equal(await confirmation.promise, 42);
  assert.deepEqual(observations, [{ tagId, value: 42 }]);

  FakePeerWebSocket.latest.emitValueUpdated({
    source: "node", event: "value updated", nodeId: 5,
    args: { commandClass: 37, property: "Current value / 🚀", propertyKey: "Target #1", newValue: 43, source: "driver" },
  });
  assert.deepEqual(observations, [{ tagId, value: 42 }]);
  await client.close();
});

test("independent loopback WebSocket peer completes pinned protocol calls and never replays a lost write", async () => {
  const peer = await startWebSocketPeer({
    serverVersion: "3.11.0", driverVersion: "15.31.1",
    minSchemaVersion: 1, maxSchemaVersion: 46, homeId: 0x12345678,
  }, (socket, request, requests) => {
    requests.push(request);
    if (request.command === "initialize") {
      socket.send(JSON.stringify({ type: "result", messageId: request.messageId, success: true, result: {} }));
    } else if (request.command === "start_listening") {
      socket.send(JSON.stringify({ type: "result", messageId: request.messageId, success: true,
        result: { state: { homeId: 0x12345678, nodes: [{ nodeId: 5, values: [] }] } } }));
    } else if (request.command === "node.poll_value") {
      socket.send(JSON.stringify({ type: "result", messageId: request.messageId, success: true, result: { value: true } }));
    } else if (request.command === "node.set_value") {
      socket.close(1011, "simulated peer loss after dispatch");
    }
  });
  const client = new ZWaveServerClient(peer.url, { timeoutMs: 1000 });
  try {
    const connected = await client.connect(0x12345678);
    assert.equal(connected.version.homeId, 0x12345678);
    assert.deepEqual(connected.state.nodes.map(node => node.nodeId), [5]);
    assert.equal(await client.pollValue(5, { endpoint: 0, commandClass: 37, property: "currentValue" }), true);
    await assert.rejects(
      client.setValue(5, { endpoint: 0, commandClass: 37, property: "targetValue" }, false),
      /ZWAVE_WS_CONNECTION_(CLOSED|FAILED)/,
    );
    assert.equal(peer.requests.filter(request => request.command === "node.set_value").length, 1);
  } finally {
    await client.close();
    await peer.close();
  }
});

test("independent loopback WebSocket peer rejects incompatible versions and controller identity", async () => {
  const valid = {
    serverVersion: "3.11.0", driverVersion: "15.31.1",
    minSchemaVersion: 1, maxSchemaVersion: 46, homeId: 0x12345678,
  };
  const incompatible = [
    { ...valid, serverVersion: "3.10.0" },
    { ...valid, driverVersion: "15.30.0" },
    { ...valid, minSchemaVersion: 47, maxSchemaVersion: 47 },
    { ...valid, homeId: 0x87654321 },
  ];
  for (const version of incompatible) {
    const peer = await startWebSocketPeer(version, (socket, request, requests) => {
      requests.push(request);
    });
    const client = new ZWaveServerClient(peer.url, { timeoutMs: 1000 });
    try {
      await assert.rejects(client.connect(0x12345678), /ZWAVE_UPSTREAM_VERSION_OR_CONTROLLER_IDENTITY_MISMATCH/);
      assert.equal(peer.requests.length, 0, "no initialization or controller I/O is allowed after an identity mismatch");
    } finally {
      await client.close();
      await peer.close();
    }
  }

  const malformedPeer = await startWebSocketPeer("{malformed-json", () => {});
  const malformedClient = new ZWaveServerClient(malformedPeer.url, { timeoutMs: 1000 });
  try {
    await assert.rejects(malformedClient.connect(0x12345678), /ZWAVE_WS_FRAME_INVALID/);
    assert.equal(malformedPeer.requests.length, 0);
  } finally {
    await malformedClient.close();
    await malformedPeer.close();
  }
});

test("pinned node state seeds only configured TAG values after metadata is resolved", () => {
  const address = formatValueAddress(5, {
    endpoint: 0, commandClass: 37, property: "Current value / 🚀", propertyKey: "Target #1",
  });
  const point = { tagId: "switch-tag", address };
  const state = { nodes: [{
    nodeId: 5,
    values: [
      { endpoint: 0, commandClass: 37, property: "Current value / 🚀", propertyKey: "Target #1", value: true },
      { endpoint: 0, commandClass: 37, property: "unbound", value: false },
    ],
  }] };
  const observations = [];
  const routed = routeInitialPersistentValues(state, new Map([[address, point]]), (routedPoint, value) => {
    observations.push([routedPoint.tagId, value]);
  });
  assert.equal(routed, 1);
  assert.deepEqual(observations, [["switch-tag", true]]);
});

test("pinned client uses the node poll_value command for fresh reads", async () => {
  const client = new ZWaveServerClient("ws://127.0.0.1:3000", { WebSocketImpl: FakePeerWebSocket });
  await client.connect(0x12345678);
  const valueId = { endpoint: 0, commandClass: 37, property: "currentValue" };
  assert.equal(await client.pollValue(5, valueId), true);
  assert.deepEqual(FakePeerWebSocket.latest.requests.at(-1), {
    command: "node.poll_value", messageId: "elitescada-3", nodeId: 5, valueId,
  });
  await client.close();
});

test("only explicitly non-secret persistent values pass metadata validation", () => {
  const valueId = { endpoint: 0, commandClass: 37, property: "currentValue" };
  const metadata = { secret: false, stateful: true, readable: true, writeable: true, type: "boolean" };
  assert.equal(validateValueMetadata(valueId, metadata, "Boolean", true), metadata);
  assert.throws(() => validateValueMetadata(valueId, { ...metadata, stateful: undefined }, "Boolean", false), /VALUE_TRANSIENT_OUT_OF_SCOPE/);
  assert.throws(() => validateValueMetadata(valueId, { ...metadata, secret: undefined }, "Boolean", false), /VALUE_SECRET_OUT_OF_SCOPE/);
  assert.throws(() => validateValueMetadata(valueId, { ...metadata, stateful: false }, "Boolean", false), /VALUE_TRANSIENT_OUT_OF_SCOPE/);
});

test("write confirmation ignores a matching event older than its acknowledgement barrier", async () => {
  const tracker = new WriteReportTracker();
  tracker.observe("tag-1", true);
  const acknowledgementSequence = tracker.sequence;
  const waiter = tracker.waitFor("tag-1", true, 1000, acknowledgementSequence);
  let settled = false;
  waiter.promise.then(() => { settled = true; });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(settled, false);
  tracker.observe("tag-1", true);
  assert.equal(await waiter.promise, true);
});

async function startWebSocketPeer(version, onRequest) {
  const server = new WebSocketServer({ host: "127.0.0.1", port: 0 });
  await once(server, "listening");
  const requests = [];
  server.on("connection", socket => {
    socket.send(typeof version === "string" ? version : JSON.stringify({ type: "version", ...version }));
    socket.on("message", raw => {
      let request;
      try { request = JSON.parse(raw.toString("utf8")); }
      catch { return; }
      onRequest(socket, request, requests);
    });
  });
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("FAKE_UPSTREAM_LISTENER_INVALID");
  return {
    url: `ws://127.0.0.1:${address.port}`,
    requests,
    close: () => new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve())),
  };
}
