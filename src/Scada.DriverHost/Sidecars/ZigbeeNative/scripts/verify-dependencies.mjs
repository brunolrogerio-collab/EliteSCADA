import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const readJson = (file) => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const pkg = readJson('package.json');
const lock = readJson('package-lock.json');
const sbom = readJson('SBOM.cdx.json');
const licenseDir = path.join(root, 'licenses');
const lockBytes = fs.readFileSync(path.join(root, 'package-lock.json'));
const lockHash = crypto.createHash('sha256').update(lockBytes).digest('hex');
const expectedNodeHash = 'fd8e59d5a511510f6a298afb548f18c7d2b1be404d8b4a27d94fbe49f56cb2d6';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

assert(process.version === 'v24.21.0', `Expected Node v24.21.0, got ${process.version}`);
assert(pkg.packageManager === 'npm@11.19.0', 'package.json must pin npm@11.19.0');
assert(pkg.engines?.node === '24.21.0', 'package.json must pin Node 24.21.0');
assert(lock.packages?.['']?.engines?.node === '24.21.0', 'package-lock root must pin Node 24.21.0');
assert(lock.packages?.['']?.dependencies?.['zigbee-herdsman'] === '3.3.2', 'herdsman pin drifted');
assert(lock.packages?.['']?.dependencies?.['zigbee-herdsman-converters'] === '23.7.0', 'converter dataset pin drifted');

const runtime = Object.entries(lock.packages)
  .filter(([name, item]) => name.startsWith('node_modules/') && !item.dev && !item.optional && !item.devOptional)
  .map(([name, item]) => ({ name: name.slice('node_modules/'.length), version: item.version, license: item.license }))
  .sort((left, right) => left.name.localeCompare(right.name));
assert(runtime.length === 24, `Expected 24 production npm packages, got ${runtime.length}`);

const licenseFiles = fs.readdirSync(licenseDir);
const slug = (name) => name.startsWith('@') ? name.replace('/', '__') : name;
const counts = new Map();
const noticeRows = [];
for (const item of runtime) {
  assert(item.license, `Unknown license for ${item.name}@${item.version}`);
  const expectedPrefix = `${slug(item.name)}@${item.version}-`;
  const matches = licenseFiles.filter((file) => file.startsWith(expectedPrefix));
  assert(matches.length === 1, `Expected one notice for ${item.name}@${item.version}; got ${matches.length}`);
  const noticePath = path.join(licenseDir, matches[0]);
  assert(fs.statSync(noticePath).size > 0, `Empty notice file ${matches[0]}`);

  if (item.license === '(MIT OR GPL-2.0)') {
    assert(item.name === 'slip' && item.version === '1.0.2', `Unqualified dual license: ${item.name}@${item.version}`);
    assert(matches[0].includes('MIT-LICENSE'), 'slip must distribute the selected MIT license text');
  } else {
    assert(['MIT', 'ISC'].includes(item.license), `License outside approved set for ${item.name}: ${item.license}`);
  }
  counts.set(item.license, (counts.get(item.license) || 0) + 1);
  const choice = item.license === '(MIT OR GPL-2.0)' ? ' — MIT option selected; GPL-2.0 option not used' : '';
  noticeRows.push(`| \`${item.name}\` | \`${item.version}\` | \`${item.license}\` | [${matches[0]}](licenses/${matches[0]})${choice} |`);
}

const nodeLicense = 'node-v24.21.0-LICENSE.txt';
assert(licenseFiles.includes(nodeLicense), 'Node runtime license bundle is missing');
const nodeRuntime = sbom.components.find((component) => component.name === 'Node.js runtime' && component.version === '24.21.0');
assert(nodeRuntime, 'CycloneDX SBOM is missing the pinned Node runtime');
assert(nodeRuntime.purl === 'pkg:generic/node@24.21.0?arch=x64&distro=linux', 'SBOM Node runtime purl drifted');
assert(nodeRuntime.externalReferences?.some((reference) => reference.hashes?.some((hash) => hash.content === expectedNodeHash)), 'SBOM Node distribution hash is missing');
assert(sbom.metadata.component.properties?.some((property) => property.name === 'org.elitescada:package-lock-sha256' && property.value === lockHash), 'SBOM does not match package-lock.json');

const expectedComponents = new Set(runtime.map((item) => `${item.name}@${item.version}`));
const actualComponents = new Set(sbom.components
  .filter((component) => component.name !== 'Node.js runtime')
  .map((component) => `${component.group ? `${component.group}/` : ''}${component.name}@${component.version}`));
assert(expectedComponents.size === actualComponents.size && [...expectedComponents].every((item) => actualComponents.has(item)), 'CycloneDX npm components do not exactly match production lock');

const slip = sbom.components.find((component) => component.name === 'slip' && component.version === '1.0.2');
assert(slip?.properties?.some((property) => property.name === 'org.elitescada:effective-license-choice' && property.value.startsWith('MIT')), 'SBOM does not record the MIT choice for slip');
assert(slip.evidence?.licenses?.some((evidence) => evidence.license?.id === 'MIT' && evidence.license.text?.url === 'licenses/slip@1.0.2-MIT-LICENSE.txt'), 'SBOM MIT license evidence is missing for slip');

const noticeText = [
  '# Third-party notices',
  '',
  'This sidecar distributes the 24 production npm packages listed below, pinned by `package-lock.json`. Each linked file is the full license text audited for that exact package version. The `slip@1.0.2` package declares `(MIT OR GPL-2.0)`; this artifact selects the MIT option and includes its upstream MIT notice.',
  '',
  '| Package | Version | Declared license | Distribution notice |',
  '|---|---:|---|---|',
  ...noticeRows,
  '',
  '## Node.js runtime',
  '',
  'The qualified runtime is the official Node.js `v24.21.0` Linux x64 distribution. Its complete bundled runtime license and third-party notices are included here:',
  '',
  `- [Node.js and bundled third-party license texts](licenses/${nodeLicense})`,
  '- Official distribution: `https://nodejs.org/dist/v24.21.0/`',
  `- Linux x64 archive SHA-256: \`${expectedNodeHash}\``,
  '',
  '## Audited distribution counts',
  '',
  `- Production npm closure: **${runtime.length}** packages (${counts.get('MIT') || 0} declared MIT, ${counts.get('ISC') || 0} ISC, ${counts.get('(MIT OR GPL-2.0)') || 0} dual-license package with MIT option selected).`,
  '- Unknown licenses: **0**.',
  '- GPL-3.0 packages: **0**.',
  '',
  'This inventory covers the npm runtime lock and the selected Node.js distribution. The CycloneDX SBOM records the npm dependency graph plus Node.js as an external runtime component.',
].join('\n') + '\n';
const noticeFile = path.join(root, 'THIRD-PARTY-NOTICES.md');
if (process.argv.includes('--write')) fs.writeFileSync(noticeFile, noticeText);
else assert(fs.readFileSync(noticeFile, 'utf8') === noticeText, 'THIRD-PARTY-NOTICES.md is out of sync; run npm run verify:dependencies -- --write');

console.log(`PASS: ${runtime.length} npm runtime packages; MIT=${counts.get('MIT') || 0}, ISC=${counts.get('ISC') || 0}, dual=${counts.get('(MIT OR GPL-2.0)') || 0}; lock SHA-256 ${lockHash}`);
