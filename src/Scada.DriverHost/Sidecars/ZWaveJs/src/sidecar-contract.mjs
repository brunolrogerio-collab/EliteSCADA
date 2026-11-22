import { formatValueAddress, sameValue } from "./value-policy.mjs";

export function readFirstLineBuffer(readable, maxBytes = 2 * 1024 * 1024) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    let settled = false;
    const clear = () => {
      for (const chunk of chunks) chunk.fill(0);
      chunks.length = 0;
    };
    const finish = error => {
      if (settled) return;
      settled = true;
      readable.off("data", onData);
      readable.off("end", onEnd);
      readable.off("error", onError);
      readable.pause?.();
      if (error) {
        clear();
        reject(error);
        return;
      }
      let result = Buffer.concat(chunks, size);
      clear();
      if (result.at(-1) === 0x0d) {
        const withoutCarriageReturn = Buffer.from(result.subarray(0, -1));
        result.fill(0);
        result = withoutCarriageReturn;
      }
      resolve(result);
    };
    const onData = chunk => {
      const owned = Buffer.from(chunk);
      const newline = owned.indexOf(0x0a);
      const before = newline < 0 ? owned : owned.subarray(0, newline);
      if (size + before.length > maxBytes) {
        owned.fill(0);
        finish(new Error("BOOTSTRAP_TOO_LARGE"));
        return;
      }
      if (before.length) {
        const copy = Buffer.from(before);
        chunks.push(copy);
        size += copy.length;
      }
      if (newline >= 0) {
        readable.pause?.();
        const remainder = owned.subarray(newline + 1);
        if (remainder.length) {
          owned.fill(0);
          finish(new Error("BOOTSTRAP_TRAILING_DATA"));
          return;
        }
        owned.fill(0);
        finish();
      } else {
        owned.fill(0);
      }
    };
    const onEnd = () => finish(new Error("BOOTSTRAP_MISSING"));
    const onError = () => finish(new Error("BOOTSTRAP_READ_FAILED"));
    readable.on("data", onData);
    readable.once("end", onEnd);
    readable.once("error", onError);
    readable.resume?.();
  });
}
export async function startLoopbackZWaveServer({ createServer, reservePort, maxAttempts = 5 }) {
  if (typeof createServer !== "function" || typeof reservePort !== "function" ||
      !Number.isInteger(maxAttempts) || maxAttempts < 1 || maxAttempts > 10) {
    throw new Error("LOCAL_UPSTREAM_CONFIGURATION_INVALID");
  }
  let lastBindError;
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    const port = await reservePort();
    if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error("LOCAL_UPSTREAM_PORT_INVALID");
    const server = createServer(port);
    try {
      await server.start(false);
      return { server, port };
    } catch (error) {
      try { await server.destroy(); } catch { /* A partially started server is best-effort closed. */ }
      if (error?.code !== "EADDRINUSE") throw new Error("LOCAL_UPSTREAM_START_FAILED");
      lastBindError = error;
    }
  }
  const error = new Error("LOCAL_UPSTREAM_BIND_FAILED");
  if (lastBindError) error.cause = lastBindError;
  throw error;
}

export function decodeNodeValueUpdatedEvent(envelope) {
  if (envelope?.source !== "node" || envelope.event !== "value updated" ||
      !Number.isInteger(envelope.nodeId) || !envelope.args || typeof envelope.args !== "object" ||
      envelope.args.source === "driver" || !Object.hasOwn(envelope.args, "newValue")) return undefined;
  const address = formatValueAddress(envelope.nodeId, envelope.args);
  return address ? { address, newValue: envelope.args.newValue } : undefined;
}

export function createValueUpdateRouter(pointsByAddress, onValueUpdate) {
  if (!(pointsByAddress instanceof Map) || typeof onValueUpdate !== "function") {
    throw new Error("VALUE_UPDATE_ROUTER_CONFIGURATION_INVALID");
  }
  return envelope => {
    const update = decodeNodeValueUpdatedEvent(envelope);
    if (!update) return false;
    const point = pointsByAddress.get(update.address);
    if (!point) return false;
    onValueUpdate(point, update.newValue);
    return true;
  };
}

export function routeInitialPersistentValues(state, pointsByAddress, onValueUpdate) {
  if (!(pointsByAddress instanceof Map) || typeof onValueUpdate !== "function" ||
      !Array.isArray(state?.nodes) || state.nodes.length > 232) return 0;
  let visited = 0;
  let routed = 0;
  for (const node of state.nodes) {
    if (!Number.isInteger(node?.nodeId) || !Array.isArray(node.values)) continue;
    for (const value of node.values) {
      if (++visited > 100_000) throw new Error("INITIAL_STATE_VALUE_LIMIT_EXCEEDED");
      if (!Object.hasOwn(value ?? {}, "value") || value.value === undefined || value.value === null) continue;
      const address = formatValueAddress(node.nodeId, value);
      const point = address ? pointsByAddress.get(address) : undefined;
      if (!point) continue;
      onValueUpdate(point, value.value);
      routed++;
    }
  }
  return routed;
}

export class WriteReportTracker {
  #waiters = new Map();
  #sequence = 0;

  get sequence() { return this.#sequence; }

  waitFor(tagId, expected, timeoutMs, afterSequence = this.#sequence) {
    if (typeof tagId !== "string" || !Number.isFinite(timeoutMs) || timeoutMs < 1) {
      throw new Error("WRITE_REPORT_TRACKER_ARGUMENT_INVALID");
    }
    if (!Number.isSafeInteger(afterSequence) || afterSequence < 0 || afterSequence > this.#sequence) {
      throw new Error("WRITE_REPORT_TRACKER_SEQUENCE_INVALID");
    }
    let timer;
    let settled = false;
    let settle;
    const promise = new Promise(resolve => { settle = resolve; });
    const waiters = this.#waiters.get(tagId) ?? new Set();
    const entry = {
      expected,
      afterSequence,
      resolve: value => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        waiters.delete(entry);
        if (waiters.size) this.#waiters.set(tagId, waiters);
        else this.#waiters.delete(tagId);
        settle(value);
      },
    };
    waiters.add(entry);
    this.#waiters.set(tagId, waiters);
    timer = setTimeout(() => entry.resolve(undefined), timeoutMs);
    return { promise, cancel: () => entry.resolve(undefined) };
  }

  observe(tagId, value) {
    const sequence = ++this.#sequence;
    for (const entry of [...(this.#waiters.get(tagId) ?? [])]) {
      if (sequence > entry.afterSequence && sameValue(value, entry.expected)) entry.resolve(value);
    }
  }

  cancelAll() {
    for (const entries of [...this.#waiters.values()]) {
      for (const entry of [...entries]) entry.resolve(undefined);
    }
  }
}
