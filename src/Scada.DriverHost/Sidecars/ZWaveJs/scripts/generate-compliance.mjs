import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(fileURLToPath(new URL("..", import.meta.url)));
const lockPath = join(root, "package-lock.json");
const lockBytes = await readFile(lockPath);
const canonicalLockBytes = Buffer.from(lockBytes.toString("utf8").replace(/\r\n/g, "\n"), "utf8");
const lock = JSON.parse(canonicalLockBytes);
const npmCli = process.env.npm_execpath;
if (!npmCli) throw new Error("NPM_CLI_PATH_REQUIRED: run through npm run generate:compliance");

const sbomResult = spawnSync(process.execPath, [npmCli, "sbom", "--package-lock-only", "--omit=dev", "--sbom-format=cyclonedx"], {
  cwd: root,
  encoding: "utf8",
  maxBuffer: 32 * 1024 * 1024,
});
if (sbomResult.status !== 0) throw new Error(`SBOM_GENERATION_FAILED:${(sbomResult.stderr || "").slice(-1000)}`);
const bom = JSON.parse(sbomResult.stdout);
bom.$schema = "http://cyclonedx.org/schema/bom-1.6.schema.json";
bom.specVersion = "1.6";
bom.metadata ??= {};
bom.metadata.component ??= {};
bom.metadata.component.type = "application";
bom.metadata.component.name = "EliteSCADA managed Z-Wave JS sidecar";
bom.metadata.component.version = "1.0.0";
bom.metadata.component.properties = [
  ...(bom.metadata.component.properties ?? []).filter(property => !property.name.startsWith("elitescada:")),
  { name: "elitescada:runtime", value: "Node.js 22.13+ or 24+ on Windows x64 or Linux x64" },
  { name: "elitescada:dependency-lock", value: "package-lock.json; exact production dependency closure" },
  { name: "elitescada:package-lock-sha256", value: createHash("sha256").update(canonicalLockBytes).digest("hex") },
];
await writeFile(join(root, "SBOM.cdx.json"), `${JSON.stringify(bom, null, 2)}\n`, "utf8");

const production = Object.entries(lock.packages ?? {})
  .filter(([path, info]) => path.startsWith("node_modules/") && info.dev !== true && info.devOptional !== true)
  .map(([path, info]) => ({ path, name: info.name ?? path.slice(path.lastIndexOf("node_modules/") + "node_modules/".length), version: info.version, license: info.license }))
  .filter((item, index, all) => all.findIndex(candidate => candidate.name === item.name && candidate.version === item.version && candidate.license === item.license) === index)
  .sort((left, right) => left.name.localeCompare(right.name) || left.version.localeCompare(right.version));
if (!production.length || production.some(item => !item.name || !item.version || typeof item.license !== "string" || !item.license)) {
  throw new Error("PRODUCTION_LOCK_LICENSE_METADATA_INCOMPLETE");
}

const licenseDirectory = join(root, "licenses");
await mkdir(licenseDirectory, { recursive: true });
const licenseTexts = new Map();
for (const item of production) {
  const directory = join(root, item.path);
  let files = [];
  try { files = await readdir(directory, { withFileTypes: true }); } catch { /* Lock-only optional package can be absent locally. */ }
  const licenseFile = files.find(file => file.isFile() && /^(license|licence|copying|notice)(\.|$)/i.test(file.name));
  if (!licenseFile || licenseTexts.has(item.license)) continue;
  licenseTexts.set(item.license, await readFile(join(directory, licenseFile.name), "utf8"));
}
for (const license of new Set(production.map(item => item.license))) {
  const text = licenseTexts.get(license);
  if (!text) throw new Error(`LICENSE_TEXT_NOT_FOUND:${license}`);
  const safeName = license.replace(/[^A-Za-z0-9.-]/g, "-");
  await writeFile(join(licenseDirectory, `LICENSE-${safeName}.txt`), text.endsWith("\n") ? text : `${text}\n`, "utf8");
}

const rows = production.map(item => `| \`${item.name}\` | \`${item.version}\` | \`${item.license}\` | [license](licenses/LICENSE-${item.license.replace(/[^A-Za-z0-9.-]/g, "-")}.txt) |`);
const notices = [
  "# Third-party notices",
  "",
  "The managed Z-Wave JS sidecar uses the exact production dependency closure pinned by `package-lock.json`. This inventory contains package names, versions, declared SPDX license identifiers, and corresponding upstream license text. Optional native serial bindings are selected by the supported Windows x64 and Linux x64 deployment platforms.",
  "",
  `Package-lock SHA-256: \`${createHash("sha256").update(canonicalLockBytes).digest("hex")}\`. The complete CycloneDX inventory is in [SBOM.cdx.json](SBOM.cdx.json). No controller network key, DSK, NVM backup, commissioned-node data, or project data is part of these artifacts.`,
  "",
  "| Package | Version | Declared license | Text |",
  "|---|---:|---|---|",
  ...rows,
  "",
].join("\n");
await writeFile(join(root, "THIRD-PARTY-NOTICES.md"), notices, "utf8");
console.log(JSON.stringify({ productionPackages: production.length, licenses: [...new Set(production.map(item => item.license))].sort(), packageLockSha256: createHash("sha256").update(canonicalLockBytes).digest("hex") }, null, 2));
