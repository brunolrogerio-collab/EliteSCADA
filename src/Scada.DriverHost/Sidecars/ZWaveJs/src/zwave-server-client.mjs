import WebSocket from "ws";

const EXPECTED_SERVER_VERSION = "3.11.0";
const EXPECTED_DRIVER_VERSION = "15.31.1";
// Schema 46 retains explicit stateful/secret metadata while excluding the
// controller DSK field introduced in schema 47 from the initial state dump.
const EXPECTED_SCHEMA_VERSION = 46;
const MAX_FRAME_BYTES = 16 * 1024 * 1024;

export class ZWaveServerClient {
  #socket;
  #pending = new Map();
  #sequence = 0;
  #closed = false;
  #connectionError;
  #versionResolve;
  #versionReject;

  constructor(url, { timeoutMs = 45_000, WebSocketImpl = WebSocket } = {}) {
    this.url = url;
    this.timeoutMs = timeoutMs;
    this.WebSocketImpl = WebSocketImpl;
    this.onEvent = () => {};
  }

  async connect(expectedHomeId) {
    const socket = new this.WebSocketImpl(this.url, { maxPayload: MAX_FRAME_BYTES, handshakeTimeout: 10_000 });
    this.#socket = socket;
    socket.on("message", data => this.#onMessage(data));
    socket.on("error", () => this.#failPending(new Error("ZWAVE_WS_CONNECTION_FAILED")));
    socket.on("close", () => {
      this.#closed = true;
      this.#failPending(new Error("ZWAVE_WS_CONNECTION_CLOSED"));
    });

    await new Promise((resolve, reject) => {
      if (socket.readyState === WebSocket.OPEN) return resolve();
      const onOpen = () => { cleanup(); resolve(); };
      const onError = () => { cleanup(); reject(new Error("ZWAVE_WS_CONNECTION_FAILED")); };
      const cleanup = () => { socket.off("open", onOpen); socket.off("error", onError); };
      socket.once("open", onOpen);
      socket.once("error", onError);
    });
    const version = await this.#waitForVersion();
    if (version.serverVersion !== EXPECTED_SERVER_VERSION || version.driverVersion !== EXPECTED_DRIVER_VERSION ||
        version.maxSchemaVersion !== EXPECTED_SCHEMA_VERSION || version.minSchemaVersion > EXPECTED_SCHEMA_VERSION ||
        !Number.isInteger(version.homeId) || (version.homeId >>> 0) !== (expectedHomeId >>> 0)) {
      await this.close();
      throw new Error("ZWAVE_UPSTREAM_VERSION_OR_CONTROLLER_IDENTITY_MISMATCH");
    }
    await this.#call("initialize", {
      schemaVersion: EXPECTED_SCHEMA_VERSION,
      additionalUserAgentComponents: { EliteSCADA: "1.0.0" },
    });
    const listening = await this.#call("start_listening", {});
    return { version, state: listening?.state ?? null };
  }

  async getNodeState(nodeId) {
    const response = await this.#call("node.get_state", { nodeId });
    return response?.state;
  }

  getValueMetadata(nodeId, valueId) {
    return this.#call("node.get_value_metadata", { nodeId, valueId });
  }

  async pollValue(nodeId, valueId) {
    const response = await this.#call("node.poll_value", { nodeId, valueId });
    return response?.value;
  }

  setValue(nodeId, valueId, value) {
    return this.#call("node.set_value", { nodeId, valueId, value });
  }

  async close() {
    if (!this.#socket) return;
    const socket = this.#socket;
    this.#socket = undefined;
    this.#closed = true;
    this.#failPending(new Error("ZWAVE_WS_CONNECTION_CLOSED"));
    if (socket.readyState === WebSocket.CLOSED) return;
    await new Promise(resolve => {
      const timer = setTimeout(() => { try { socket.terminate(); } catch {} resolve(); }, 1500);
      socket.once("close", () => { clearTimeout(timer); resolve(); });
      try { socket.close(1000, "EliteSCADA sidecar stop"); } catch { clearTimeout(timer); resolve(); }
    });
  }

  async #waitForVersion() {
    if (this.version) return this.version;
    if (this.#connectionError) throw this.#connectionError;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => finish(new Error("ZWAVE_WS_VERSION_TIMEOUT")), this.timeoutMs);
      const finish = error => {
        clearTimeout(timer);
        this.#versionResolve = undefined;
        this.#versionReject = undefined;
        error ? reject(error) : resolve(this.version);
      };
      this.#versionResolve = () => finish();
      this.#versionReject = error => finish(error);
      if (this.version) finish();
    });
  }

  async #call(command, args) {
    if (this.#closed || !this.#socket || this.#socket.readyState !== WebSocket.OPEN) throw new Error("ZWAVE_WS_NOT_CONNECTED");
    const messageId = `elitescada-${++this.#sequence}`;
    const completion = new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.#pending.delete(messageId);
        reject(new Error("ZWAVE_WS_REQUEST_TIMEOUT"));
      }, this.timeoutMs);
      this.#pending.set(messageId, { resolve, reject, timer });
    });
    try {
      this.#socket.send(JSON.stringify({ command, messageId, ...args }));
    } catch {
      const pending = this.#pending.get(messageId);
      if (pending) {
        clearTimeout(pending.timer);
        this.#pending.delete(messageId);
      }
      throw new Error("ZWAVE_WS_SEND_FAILED");
    }
    return completion;
  }

  #onMessage(data) {
    if (Buffer.byteLength(data) > MAX_FRAME_BYTES) return this.#failPending(new Error("ZWAVE_WS_FRAME_TOO_LARGE"));
    let message;
    try { message = JSON.parse(data.toString()); }
    catch { return this.#failPending(new Error("ZWAVE_WS_FRAME_INVALID")); }
    if (message?.type === "version") {
      this.version = message;
      this.#versionResolve?.();
      return;
    }
    if (message?.type === "event") {
      try { this.onEvent(message.event); } catch { /* Event consumers fail closed independently. */ }
      return;
    }
    if (message?.type !== "result" || typeof message.messageId !== "string") return;
    const pending = this.#pending.get(message.messageId);
    if (!pending) return;
    clearTimeout(pending.timer);
    this.#pending.delete(message.messageId);
    if (message.success === true) pending.resolve(message.result ?? {});
    else pending.reject(new Error("ZWAVE_WS_UPSTREAM_OPERATION_FAILED"));
  }

  #failPending(error) {
    this.#connectionError ??= error;
    this.#versionReject?.(error);
    for (const pending of this.#pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.#pending.clear();
  }
}

export const zwaveProtocolContract = Object.freeze({
  serverVersion: EXPECTED_SERVER_VERSION,
  driverVersion: EXPECTED_DRIVER_VERSION,
  schemaVersion: EXPECTED_SCHEMA_VERSION,
});
