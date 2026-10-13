import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const manifest = JSON.parse(await readFile(join(root, "package.json"), "utf8"));
const lock = JSON.parse(await readFile(join(root, "package-lock.json"), "utf8"));
const server = JSON.parse(await readFile(join(root, "node_modules", "@zwave-js", "server", "package.json"), "utf8"));
const core = JSON.parse(await readFile(join(root, "node_modules", "zwave-js", "package.json"), "utf8"));
const ws = JSON.parse(await readFile(join(root, "node_modules", "ws", "package.json"), "utf8"));
const expected = new Map([
  ["node_modules/@zwave-js/server", ["3.11.0", "sha512-qOSHm7agzHKHAdWLTH0LtQeRK61fA+nR/6z/slgWwL5pG1RudfsJS3fhHTqEKqSICUjdAwIqO7wzo6Mod00tpw=="]],
  ["node_modules/zwave-js", ["15.31.1", "sha512-QhX6chJ7nNHpN+FL7SukE/mGsWn0HePIRDZcNgEkWtMdgIZBiKdsQpLMcYLEhap24A7a7o1M0NJoq1lMoz4t4g=="]],
  ["node_modules/ws", ["8.22.0", "sha512-Ydggc987+RO0AnWtZ/7Wq9FtNvcrL1b/RO0ud9mWjUPgDrsAAwQSF51sm2hm1XofbU/4jkpGEsLFsZZxU+1DOg=="]],
]);

for (const [path, [version, integrity]] of expected) {
  const locked = lock.packages?.[path];
  if (locked?.version !== version || locked?.integrity !== integrity) {
    throw new Error(`LOCK_INTEGRITY_MISMATCH:${path}`);
  }
}
if (manifest.dependencies?.["@zwave-js/server"] !== "3.11.0" || manifest.dependencies?.["zwave-js"] !== "15.31.1" ||
    manifest.dependencies?.["ws"] !== "8.22.0" || ws.version !== "8.22.0" || ws.license !== "MIT" ||
    server.version !== "3.11.0" || core.version !== "15.31.1" || core.license !== "MIT" || server.license !== "Apache-2.0" ||
    server.peerDependencies?.["zwave-js"] !== "^15.31.0") {
  throw new Error("UPSTREAM_METADATA_MISMATCH");
}
if (manifest.engines?.node !== "^22.13.0 || >=24") throw new Error("NODE_ENGINE_POLICY_MISMATCH");

const driverPath = join(root, "node_modules", "zwave-js", "build", "esm", "lib", "driver", "Driver.js");
const patched = await readFile(driverPath, "utf8");
if (!patched.includes("EliteSCADA v1 is read/write only") || /this\.controller\.autoProvisionSmartStart\(\)/.test(patched)) {
  throw new Error("ZWAVE_JS_SAFETY_PATCH_MISSING");
}

const [major, minor] = process.versions.node.split(".").map(Number);
if (!(major === 22 && minor >= 13 || major >= 24)) throw new Error("NODE_RUNTIME_UNSUPPORTED");
const lockBytes = await readFile(join(root, "package-lock.json"));
const lockHash = createHash("sha256").update(lockBytes).digest("hex");
const bom = JSON.parse(await readFile(join(root, "SBOM.cdx.json"), "utf8"));
const notices = await readFile(join(root, "THIRD-PARTY-NOTICES.md"), "utf8");
const productionPackages = Object.entries(lock.packages ?? {})
  .filter(([path, info]) => path.startsWith("node_modules/") && info.dev !== true && info.devOptional !== true)
  .map(([path, info]) => `${info.name ?? path.slice(path.lastIndexOf("node_modules/") + "node_modules/".length)}@${info.version}`);
const productionPackageSet = new Set(productionPackages);
const bomPackages = new Set((bom.components ?? []).map(component => `${component.name}@${component.version}`));
if (bom.bomFormat !== "CycloneDX" || bom.specVersion !== "1.6" ||
    bom.metadata?.component?.properties?.find(property => property.name === "elitescada:package-lock-sha256")?.value !== lockHash ||
    productionPackageSet.size !== bomPackages.size || [...productionPackageSet].some(packageId => !bomPackages.has(packageId))) {
  throw new Error("SBOM_DOES_NOT_MATCH_PRODUCTION_LOCK");
}
for (const [path, info] of Object.entries(lock.packages ?? {})) {
  if (!path.startsWith("node_modules/") || info.dev === true || info.devOptional === true) continue;
  const packageName = info.name ?? path.slice(path.lastIndexOf("node_modules/") + "node_modules/".length);
  const packageId = `${packageName}@${info.version}`;
  if (!notices.includes(`| \`${packageId.slice(0, packageId.lastIndexOf("@"))}\` | \`${info.version}\``)) {
    throw new Error(`THIRD_PARTY_NOTICE_MISSING:${packageId}`);
  }
  const licenseFile = join(root, "licenses", `LICENSE-${String(info.license).replace(/[^A-Za-z0-9.-]/g, "-")}.txt`);
  await readFile(licenseFile);
}
console.log(JSON.stringify({
  server: server.version,
  core: core.version,
  node: process.versions.node,
  platform: `${process.platform}-${process.arch}`,
  packageLockSha256: lockHash,
  productionPackages: productionPackages.length,
  sbom: `${bom.bomFormat} ${bom.specVersion}`,
  thirdPartyNotices: "complete",
  integrity: "verified-lock-pins",
  upstreamSafetyPatch: "applied",
  selectedApiSchema: 46,
}, null, 2));
