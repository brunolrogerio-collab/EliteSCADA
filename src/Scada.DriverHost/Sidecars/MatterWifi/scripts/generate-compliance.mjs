import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const lockBytes = await readFile(join(root, 'package-lock.json'));
const canonicalLockBytes = Buffer.from(lockBytes.toString('utf8').replace(/\r\n/g, '\n'), 'utf8');
const lock = JSON.parse(canonicalLockBytes);
const npmCli = process.env.npm_execpath;
if (!npmCli) throw new Error('NPM_CLI_PATH_REQUIRED: run through npm run generate:compliance');

const sbomResult = spawnSync(process.execPath, [npmCli, 'sbom', '--package-lock-only', '--omit=dev', '--sbom-format=cyclonedx'], {
  cwd: root,
  encoding: 'utf8',
  maxBuffer: 32 * 1024 * 1024,
});
if (sbomResult.status !== 0) throw new Error(`SBOM_GENERATION_FAILED:${(sbomResult.stderr || '').slice(-1000)}`);
const bom = JSON.parse(sbomResult.stdout);
bom.$schema = 'http://cyclonedx.org/schema/bom-1.6.schema.json';
bom.specVersion = '1.6';

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
  const declared = info.license;
  if (Array.isArray(declared) && declared.join('|') === 'MIT|Apache2') return 'MIT OR Apache-2.0';
  if (typeof declared === 'string') return declared.replace(/^\((.+)\)$/, '$1');
  throw new Error(`LICENSE_METADATA_MISSING:${name}@${info.version}`);
}
function safeLicense(value) {
  return value.replace(/[^A-Za-z0-9.-]/g, '-').replace(/-+/g, '-');
}
function cyclonedxLicense(value) {
  if (/^[A-Za-z0-9.-]+$/.test(value) && value !== 'Public Domain' && value !== 'MIT-X11') {
    return [{ license: { id: value } }];
  }
  if (/\s+(OR|AND)\s+/.test(value)) return [{ expression: value }];
  return [{ license: { name: value } }];
}
async function findLicenseFiles(item) {
  const packageDirectory = join(root, item.path);
  let files = [];
  try { files = await readdir(packageDirectory, { withFileTypes: true }); }
  catch { files = []; }
  const licenseFiles = files.filter(file => file.isFile() && /^(license|licence|copying|notice)([._-]|$)/i.test(file.name));
  const plain = licenseFiles.find(file => /^(license|licence|copying|notice)(\.|$)/i.test(file.name));
  if (plain) return [join(packageDirectory, plain.name)];
  if (item.license.includes(' OR ')) {
    const terms = item.license.split(' OR ').map(term => term.toLowerCase().replace(/[^a-z0-9]/g, ''));
    const matches = terms.map(term => licenseFiles.find(file => file.name.toLowerCase().replace(/[^a-z0-9]/g, '').includes(term)));
    if (matches.every(Boolean)) return matches.map(file => join(packageDirectory, file.name));
  }
  const candidate = licenseFiles[0];
  if (candidate) return [join(packageDirectory, candidate.name)];
  if (item.license === 'BSD-2-Clause' && item.name === '@yarnpkg/lockfile') {
    return [join(root, 'licenses', 'LICENSE-BSD-2-Clause.txt')];
  }
  return undefined;
}

const production = Object.entries(lock.packages ?? {})
  .filter(([path, info]) => path.startsWith('node_modules/') && info.dev !== true && info.devOptional !== true)
  .map(([path, info]) => ({ path, name: packageName(path, info), version: info.version, license: licenseFor(packageName(path, info), info), integrity: info.integrity }))
  .filter((item, index, all) => all.findIndex(candidate => candidate.name === item.name && candidate.version === item.version) === index)
  .sort((left, right) => left.name.localeCompare(right.name) || left.version.localeCompare(right.version));
if (!production.length) throw new Error('PRODUCTION_LOCK_EMPTY');

const licenseTexts = new Map();
for (const item of production) {
  if (licenseTexts.has(item.license)) continue;
  const licenseFiles = await findLicenseFiles(item);
  if (licenseFiles) {
    const texts = await Promise.all(licenseFiles.map(file => readFile(file, 'utf8')));
    const text = licenseFiles.length === 1 ? texts[0] : texts.map((value, index) => `--- ${item.license.split(' OR ')[index]} ---\n\n${value}`).join('\n\n');
    licenseTexts.set(item.license, { text, source: `${item.name}@${item.version}/${licenseFiles.map(file => file.slice(join(root, item.path).length + 1)).join(', ')}` });
  } else if (item.license === 'Public Domain') {
    licenseTexts.set(item.license, {
      text: 'Upstream package metadata declares this package Public Domain. Its exact locked tarball did not include a separate LICENSE/COPYING/NOTICE file; this notice records the declaration and does not create or reproduce license terms.\n',
      source: `${item.name}@${item.version} package metadata`,
    });
  }
}
for (const item of production) {
  if (!licenseTexts.has(item.license)) throw new Error(`LICENSE_TEXT_NOT_FOUND:${item.license}`);
}

const licenseDirectory = join(root, 'licenses');
await mkdir(licenseDirectory, { recursive: true });
for (const [license, { text }] of licenseTexts) {
  const fileName = `LICENSE-${safeLicense(license)}.txt`;
  const normalizedText = text
    .split(/\r?\n/)
    .map(line => line.replace(/[\t ]+$/g, ''))
    .join('\n')
    .replace(/\n+$/g, '');
  await writeFile(join(licenseDirectory, fileName), `${normalizedText}\n`, 'utf8');
}

const lockHash = createHash('sha256').update(canonicalLockBytes).digest('hex');
bom.metadata ??= {};
bom.metadata.component ??= {};
bom.metadata.component.type = 'application';
bom.metadata.component.name = 'SCADIVO experimental Matter Wi-Fi sidecar';
bom.metadata.component.version = '1.0.0';
bom.metadata.component.properties = [
  ...(bom.metadata.component.properties ?? []).filter(property => !property.name.startsWith('scadivo:')),
  { name: 'scadivo:runtime', value: 'Node.js >=22.13; experimental, opt-in, Development/Test only' },
  { name: 'scadivo:dependency-lock', value: 'package-lock.json; exact production dependency closure' },
  { name: 'scadivo:package-lock-sha256', value: lockHash },
  { name: 'scadivo:execution-scope', value: 'No device commissioning, real Matter network, production, or distribution qualification' },
];

const licenseByPackage = new Map(production.map(item => [`${item.name}@${item.version}`, item.license]));
for (const component of bom.components ?? []) {
  const packageId = component['bom-ref'] ?? `${component.name}@${component.version}`;
  const license = licenseByPackage.get(packageId);
  if (!license) throw new Error(`SBOM_COMPONENT_NOT_IN_LOCK:${packageId}`);
  component.licenses = cyclonedxLicense(license);
}

const rows = production.map(item => {
  const fileName = `LICENSE-${safeLicense(item.license)}.txt`;
  return `| \`${item.name}\` | \`${item.version}\` | \`${item.license}\` | [notice](licenses/${fileName}) |`;
});
const notices = [
  '# Third-party notices — experimental Matter Wi-Fi sidecar',
  '',
  'This inventory describes the exact production dependency closure pinned by `package-lock.json`. It supports engineering review only. The Matter sidecar remains opt-in and experimental; these files do not authorize production use, packaging, distribution, certification, commissioning, or connection to real Matter devices.',
  '',
  `Package-lock SHA-256: \`${lockHash}\`. The matching CycloneDX inventory is [SBOM.cdx.json](SBOM.cdx.json). The four Matter Server npm manifests omit their license field; their exact locked tarballs include Apache-2.0 license text. The exact Yarn BSD-2-Clause text is reproduced from its [v1.10.0 source license](https://github.com/yarnpkg/yarn/blob/v1.10.0/LICENSE), because the locked npm tarball omits the file. ` +
    '`@nornagon/put` declares `MIT/X11`, `jsonify` declares `Public Domain` without a separate license text, and `pause-stream` declares dual MIT/Apache licensing. Each notice is linked below; metadata-only declarations are identified as such.',
  '',
  '| Package | Version | Upstream license declaration | Notice |',
  '|---|---:|---|---|',
  ...rows,
  '',
].join('\n');

await writeFile(join(root, 'SBOM.cdx.json'), `${JSON.stringify(bom, null, 2)}\n`, 'utf8');
await writeFile(join(root, 'THIRD-PARTY-NOTICES.md'), notices, 'utf8');
console.log(JSON.stringify({ productionPackageVersions: production.length, licenses: [...licenseTexts.keys()].sort(), packageLockSha256: lockHash }, null, 2));
