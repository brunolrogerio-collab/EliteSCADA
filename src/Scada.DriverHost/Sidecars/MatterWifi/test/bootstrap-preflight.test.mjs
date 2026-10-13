import test from 'node:test';
import assert from 'node:assert/strict';
import { PassThrough } from 'node:stream';
import { mkdtemp, mkdir, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { BLOB_DRIVER_ID, KV_DRIVER_ID } from '../src/encrypted-storage.mjs';
import { hardenStorageTree, inspectStorageDirectory } from '../src/storage-preflight.mjs';
import { parseBootstrapLine, readStartupLine } from '../src/startup-protocol.mjs';

async function withTempDirectory(run) {
  const directory = await mkdtemp(join(tmpdir(), 'elitescada-matter-preflight-'));
  try { await run(directory); }
  finally { await rm(directory, { recursive: true, force: true }); }
}

function inputStream(value) {
  const input = new PassThrough();
  queueMicrotask(() => input.end(value));
  return input;
}

test('bootstrap input accepts one bounded newline-terminated payload and wipes the parsed key array', async () => {
  const keyBytes = Array.from({ length: 32 }, (_, index) => index);
  const input = inputStream(`${JSON.stringify({ type: 'bootstrap', schemaVersion: 1, keyBytes })}\n`);
  const line = await readStartupLine(input);
  const key = parseBootstrapLine(line);

  assert.deepEqual([...key], keyBytes);
  assert.ok(line.every(value => value === 0));
  key.fill(0);
});

test('bootstrap input rejects truncation, trailing payloads, and overlong input', async () => {
  await assert.rejects(readStartupLine(inputStream('{"type":"bootstrap"}')), /MATTER_STARTUP_INPUT_INCOMPLETE/);
  await assert.rejects(readStartupLine(inputStream('{}\nextra')), /MATTER_STARTUP_INPUT_TRAILING_BYTES/);
  await assert.rejects(readStartupLine(inputStream('x'.repeat(64 * 1024 + 1))), /MATTER_STARTUP_INPUT_TOO_LARGE/);
});

test('bootstrap payload rejects malformed UTF-8, extra fields, and invalid key bytes', () => {
  assert.throws(() => parseBootstrapLine(Buffer.from([0xff])), /MATTER_STARTUP_INPUT_INVALID/);
  assert.throws(() => parseBootstrapLine(Buffer.from(JSON.stringify({ type: 'bootstrap', schemaVersion: 1, keyBytes: Array(32).fill(1), ignored: 'secret' }))), /MATTER_STARTUP_INPUT_INVALID/);
  assert.throws(() => parseBootstrapLine(Buffer.from(JSON.stringify({ type: 'bootstrap', schemaVersion: 1, keyBytes: Array(31).fill(1) }))), /MATTER_STARTUP_INPUT_INVALID/);
  assert.throws(() => parseBootstrapLine(Buffer.from(JSON.stringify({ type: 'bootstrap', schemaVersion: 1, keyBytes: [...Array(31).fill(1), 256] }))), /MATTER_STARTUP_INPUT_INVALID/);
});

test('storage preflight permits empty and recognized encrypted storage trees', async () => {
  await withTempDirectory(async root => {
    await inspectStorageDirectory(root);
    const kv = join(root, 'matter', 'controller');
    const blob = join(root, 'matter', 'blobs');
    await mkdir(kv, { recursive: true });
    await mkdir(blob, { recursive: true });
    await writeFile(join(kv, 'driver.json'), JSON.stringify({ kind: KV_DRIVER_ID, type: 'kv' }));
    await writeFile(join(kv, 'values.json'), 'encrypted-envelope');
    await writeFile(join(blob, 'driver.json'), JSON.stringify({ kind: BLOB_DRIVER_ID, type: 'blob' }));
    await writeFile(join(blob, 'fabrics.bin'), Buffer.from('encrypted-envelope'));
    await inspectStorageDirectory(root);
  });
});

test('storage preflight rejects legacy/plaintext, malformed, and unowned files before migration', async () => {
  await withTempDirectory(async root => {
    const namespace = join(root, 'namespace');
    await mkdir(namespace);
    await writeFile(join(namespace, 'driver.json'), JSON.stringify({ kind: 'file', type: 'kv' }));
    await assert.rejects(inspectStorageDirectory(root), /MATTER_STORAGE_LEGACY_OR_INCOMPATIBLE/);
  });

  await withTempDirectory(async root => {
    const namespace = join(root, 'namespace');
    await mkdir(namespace);
    await writeFile(join(namespace, 'driver.json'), '{broken');
    await assert.rejects(inspectStorageDirectory(root), /MATTER_STORAGE_DESCRIPTOR_INVALID/);
  });

  await withTempDirectory(async root => {
    await writeFile(join(root, 'unknown-state.json'), 'plaintext');
    await assert.rejects(inspectStorageDirectory(root), /MATTER_STORAGE_UNOWNED_OR_PLAINTEXT_STATE/);
  });
});

test('storage preflight rejects a symlink at the root and inside an owned namespace', async t => {
  await withTempDirectory(async root => {
    const target = join(root, 'target');
    const rootLink = join(root, 'root-link');
    const nestedLink = join(root, 'nested-link');
    await mkdir(target);
    try {
      await symlink(target, rootLink, 'junction');
      await assert.rejects(inspectStorageDirectory(rootLink), /MATTER_STORAGE_DIRECTORY_INVALID/);
      await symlink(target, nestedLink, 'junction');
    } catch (error) {
      if (error?.code === 'EPERM' || error?.code === 'EACCES' || error?.code === 'ENOTSUP') {
        t.skip('symlink creation is not permitted in this environment');
        return;
      }
      throw error;
    }
    await assert.rejects(inspectStorageDirectory(root), /MATTER_STORAGE_SYMLINK_FORBIDDEN/);
  });
});

test('storage permissions are restricted before the sidecar starts', async () => {
  if (process.platform === 'win32') return;
  await withTempDirectory(async root => {
    const child = join(root, 'namespace');
    await mkdir(child);
    const descriptor = join(child, 'driver.json');
    await writeFile(descriptor, '{}');
    await hardenStorageTree(root);
    const { mode: directoryMode } = await (await import('node:fs/promises')).stat(root);
    const { mode: fileMode } = await (await import('node:fs/promises')).stat(descriptor);
    assert.equal(directoryMode & 0o777, 0o700);
    assert.equal(fileMode & 0o777, 0o600);
  });
});
