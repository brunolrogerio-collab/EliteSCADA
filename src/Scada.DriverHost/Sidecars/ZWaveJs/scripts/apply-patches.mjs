import { readFile, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const packageJson = JSON.parse(await readFile(join(root, "node_modules", "zwave-js", "package.json"), "utf8"));
if (packageJson.version !== "15.31.1") {
  throw new Error("ZWAVE_JS_PIN_MISMATCH");
}

const driverPath = join(root, "node_modules", "zwave-js", "build", "esm", "lib", "driver", "Driver.js");
const source = await readFile(driverPath, "utf8");
const marker = "// EliteSCADA v1 is read/write only; do not enable SmartStart from";
const original = [
  "                // Auto-enable smart start inclusion\n",
  "                this.controller.autoProvisionSmartStart();",
].join("");
const replacement = [
  "                // EliteSCADA v1 is read/write only; do not enable SmartStart from\n",
  "                // provisioning entries persisted on the controller.\n",
].join("");
if (source.includes(marker)) {
  if (source.includes(original) || source.includes("this.controller.autoProvisionSmartStart()")) {
    throw new Error("ZWAVE_JS_AUTOPROVISION_PATCH_STATE_INVALID");
  }
  process.exit(0);
}
if (source.split(original).length !== 2) {
  throw new Error("ZWAVE_JS_AUTOPROVISION_PATCH_TARGET_MISMATCH");
}
await writeFile(driverPath, source.replace(original, replacement), "utf8");
