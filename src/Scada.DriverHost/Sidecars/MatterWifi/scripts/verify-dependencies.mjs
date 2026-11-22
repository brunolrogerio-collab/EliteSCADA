import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const manifest = JSON.parse(await readFile(join(root, 'package.json'), 'utf8'));
const lockBytes = await readFile(join(root, 'package-lock.json'));
const canonicalLockBytes = Buffer.from(lockBytes.toString('utf8').replace(/\r\n/g, '\n'), 'utf8');
const lock = JSON.parse(canonicalLockBytes);
const sbom = JSON.parse(await readFile(join(root, 'SBOM.cdx.json'), 'utf8'));
const notices = await readFile(join(root, 'THIRD-PARTY-NOTICES.md'), 'utf8');
const licenseOverrides = new Map([
  ['matter-server', 'Apache-2.0'],
  ['@matter-server/ble-proxy', 'Apache-2.0'],
  ['@matter-server/custom-clusters', 'Apache-2.0'],
  ['@matter-server/ws-controller', 'Apache-2.0'],
  ['@nornagon/put', 'MIT/X11'],
  ['jsonify', 'Public Domain'],
  ['pause-stream', 'MIT OR Apache-2.0'],
]);
function packageName(path, info) {
  return info.name ?? path.slice(path.lastIndexOf('node_modules/') + 'node_modules/'.length);
}
function licenseFor(name, info) {
  const override = licenseOverrides.get(name);
  if (override) return override;
  if (Array.isArray(info.license) && info.license.join('|') === 'MIT|Apache2') return 'MIT OR Apache-2.0';
  if (typeof info.license === 'string') return info.license.replace(/^\((.+)\)$/, '$1');
  throw new Error(`LICENSE_METADATA_MISSING:${name}@${info.version}`);
}
function safeLicense(value) {
  return value.replace(/[^A-Za-z0-9.-]/g, '-').replace(/-+/g, '-');
}
function bomLicense(component) {
  const entry = component.licenses?.[0];
  return entry?.expression ?? entry?.license?.id ?? entry?.license?.name;
}

const expectedDirect = {
  'matter-server': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter-server/ble-proxy': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter-server/custom-clusters': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter-server/dashboard': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter-server/ws-client': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter-server/ws-controller': '1.5.0-alpha.0-20261013-75a39cb',
  '@matter/main': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/general': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/model': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/node': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/nodejs': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/protocol': '0.18.0-alpha.0-20261008-6066b7499',
  '@matter/types': '0.18.0-alpha.0-20261008-6066b7499',
  ws: '8.22.0',
};
const expectedOverrides = {
  'body-parser': {
    version: '2.4.0',
    integrity: 'sha512-Ifk1KcLrxaGt1occQagQNFNNOqRt1LFuehEnHHzB8Z6cRgIxRU1TBaiIsUiCdA+gy5PL7Y1IX31DNKkRmxtXjQ==',
  },
  qs: {
    version: '6.16.1',
    integrity: 'sha512-5Px6J7rfHBAGUhNQ3es4YzcOdBBw0SDcVtThJml4RdnKw4tKs2epA0kPZ+K8X+WiHktT93RHVcQ/3ym5aTCgtg==',
  },
};
assert.equal(lock.lockfileVersion, 3, 'LOCKFILE_VERSION_MISMATCH');
assert.equal(manifest.engines?.node, '>=22.13.0', 'NODE_ENGINE_POLICY_MISMATCH');
for (const [name, version] of Object.entries(expectedDirect)) {
  assert.equal(manifest.dependencies?.[name], version, `PACKAGE_MANIFEST_PIN_MISMATCH:${name}`);
  assert.equal(lock.packages?.[`node_modules/${name}`]?.version, version, `PACKAGE_LOCK_PIN_MISMATCH:${name}`);
}
for (const [name, expected] of Object.entries(expectedOverrides)) {
  assert.equal(manifest.overrides?.[name], expected.version, `PACKAGE_OVERRIDE_MISMATCH:${name}`);
  const lockedOverride = lock.packages?.[`node_modules/${name}`];
  assert.equal(lockedOverride?.version, expected.version, `PACKAGE_LOCK_OVERRIDE_MISMATCH:${name}`);
  assert.equal(lockedOverride?.integrity, expected.integrity, `PACKAGE_LOCK_OVERRIDE_INTEGRITY_MISMATCH:${name}`);
  const installedOverride = JSON.parse(await readFile(join(root, 'node_modules', name, 'package.json'), 'utf8'));
  assert.equal(installedOverride.version, expected.version, `INSTALLED_OVERRIDE_MISMATCH:${name}`);
}

const production = Object.entries(lock.packages ?? {})
  .filter(([path, info]) => path.startsWith('node_modules/') && info.dev !== true && info.devOptional !== true)
  .map(([path, info]) => ({ path, name: packageName(path, info), version: info.version, license: licenseFor(packageName(path, info), info), integrity: info.integrity }))
  .filter((item, index, all) => all.findIndex(candidate => candidate.name === item.name && candidate.version === item.version) === index);
assert.ok(production.length > 0, 'PRODUCTION_LOCK_EMPTY');
assert.ok(production.every(item => item.version && item.integrity?.startsWith('sha512-')), 'PRODUCTION_LOCK_INTEGRITY_MISSING');

const lockHash = createHash('sha256').update(canonicalLockBytes).digest('hex');
assert.equal(sbom.bomFormat, 'CycloneDX', 'SBOM_FORMAT_MISMATCH');
assert.equal(sbom.specVersion, '1.6', 'SBOM_VERSION_MISMATCH');
assert.equal(sbom.metadata?.component?.properties?.find(property => property.name === 'scadivo:package-lock-sha256')?.value, lockHash, 'SBOM_LOCK_HASH_MISMATCH');
const expectedComponents = new Map(production.map(item => [`${item.name}@${item.version}`, item.license]));
const actualComponents = new Map((sbom.components ?? []).map(component => [component['bom-ref'] ?? `${component.name}@${component.version}`, bomLicense(component)]));
assert.equal(actualComponents.size, expectedComponents.size, 'SBOM_COMPONENT_COUNT_MISMATCH');
for (const [packageId, license] of expectedComponents) {
  assert.equal(actualComponents.get(packageId), license, `SBOM_PACKAGE_OR_LICENSE_MISMATCH:${packageId}`);
  const name = packageId.slice(0, packageId.lastIndexOf('@'));
  const version = packageId.slice(packageId.lastIndexOf('@') + 1);
  assert.ok(notices.includes(`| \`${name}\` | \`${version}\` |`), `THIRD_PARTY_NOTICE_MISSING:${packageId}`);
  const fileName = `LICENSE-${safeLicense(license)}.txt`;
  await readFile(join(root, 'licenses', fileName));
}
assert.ok(notices.includes(`Package-lock SHA-256: \`${lockHash}\``), 'NOTICES_LOCK_HASH_MISMATCH');
assert.ok(notices.includes('do not authorize production use'), 'EXPERIMENTAL_DISTRIBUTION_NOTICE_MISSING');

const matterProtocol = JSON.parse(await readFile(join(root, 'node_modules', '@matter', 'protocol', 'package.json'), 'utf8'));
const matterServer = JSON.parse(await readFile(join(root, 'node_modules', 'matter-server', 'package.json'), 'utf8'));
assert.equal(matterProtocol.version, expectedDirect['@matter/protocol'], 'INSTALLED_PROTOCOL_PIN_MISMATCH');
assert.equal(matterServer.version, expectedDirect['matter-server'], 'INSTALLED_SERVER_PIN_MISMATCH');
console.log(JSON.stringify({
  runtime: process.versions.node,
  packageLockSha256: lockHash,
  productionPackageVersions: production.length,
  sbom: `${sbom.bomFormat} ${sbom.specVersion}`,
  notices: 'complete',
  exactPins: 'verified',
  securityOverrides: Object.fromEntries(Object.entries(expectedOverrides).map(([name, item]) => [name, item.version])),
  licensingOverrides: [...licenseOverrides.keys()],
}, null, 2));
