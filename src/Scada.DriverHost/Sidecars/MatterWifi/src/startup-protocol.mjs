import { TextDecoder } from 'node:util';

export const MAX_STARTUP_BYTES = 64 * 1024;
const SCHEMA_VERSION = 1;

function fail(code) {
  throw new Error(code);
}

export async function readStartupLine(input) {
  input.pause();
  return await new Promise((resolveLine, rejectLine) => {
    let bytes = Buffer.alloc(0);
    let settled = false;
    const cleanup = () => {
      input.off('data', onData);
      input.off('error', onError);
      input.off('end', onEnd);
      input.pause();
    };
    const reject = error => {
      if (settled) return;
      settled = true;
      cleanup();
      bytes.fill(0);
      rejectLine(error);
    };
    const onData = chunk => {
      const next = Buffer.concat([bytes, Buffer.from(chunk)]);
      bytes.fill(0);
      bytes = next;
      if (bytes.length > MAX_STARTUP_BYTES) {
        reject(new Error('MATTER_STARTUP_INPUT_TOO_LARGE'));
        return;
      }
      const newline = bytes.indexOf(0x0a);
      if (newline < 0) return;
      const line = Buffer.from(bytes.subarray(0, newline));
      if (bytes.length !== newline + 1) {
        line.fill(0);
        reject(new Error('MATTER_STARTUP_INPUT_TRAILING_BYTES'));
        return;
      }
      settled = true;
      cleanup();
      bytes.fill(0);
      resolveLine(line);
    };
    const onError = error => reject(error);
    const onEnd = () => reject(new Error('MATTER_STARTUP_INPUT_INCOMPLETE'));

    input.on('data', onData);
    input.once('error', onError);
    input.once('end', onEnd);
    input.resume();
  });
}

export function parseBootstrapLine(line) {
  let payload;
  try {
    let text;
    try { text = new TextDecoder('utf-8', { fatal: true }).decode(line); }
    catch { fail('MATTER_STARTUP_INPUT_INVALID'); }
    try { payload = JSON.parse(text); }
    catch { fail('MATTER_STARTUP_INPUT_INVALID'); }
    const keys = payload && typeof payload === 'object' && !Array.isArray(payload)
      ? Object.keys(payload).sort()
      : [];
    if (keys.join(',') !== 'keyBytes,schemaVersion,type' ||
        payload.type !== 'bootstrap' || payload.schemaVersion !== SCHEMA_VERSION ||
        !Array.isArray(payload.keyBytes) || payload.keyBytes.length !== 32 ||
        payload.keyBytes.some(value => !Number.isInteger(value) || value < 0 || value > 255)) {
      fail('MATTER_STARTUP_INPUT_INVALID');
    }
    return Buffer.from(payload.keyBytes);
  } finally {
    line.fill(0);
    if (Array.isArray(payload?.keyBytes)) payload.keyBytes.fill(0);
  }
}
