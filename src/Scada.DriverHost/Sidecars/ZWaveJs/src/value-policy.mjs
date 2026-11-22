const DISALLOWED_VALUE_COMMAND_CLASSES = new Set([
  76,  // Door Lock Logging
  91,  // Central Scene (transient events)
  98,  // Door Lock
  99,  // User Code
  102, // Barrier Operator
  113, // Notification (event/access-control surface)
  131, // User Credential
]);

const MAX_SAFE = Number.MAX_SAFE_INTEGER;
const MAX_ADDRESS_COMPONENT_BYTES = 256;

export function parseValueAddress(address) {
  if (typeof address !== "string" || address.length > 1024) throw new Error("VALUE_ADDRESS_INVALID");
  const parts = address.split("/");
  if (parts.length !== 12 || parts[0] !== "node" || parts[2] !== "endpoint" || parts[4] !== "cc" ||
      parts[6] !== "property" || parts[9] !== "key") throw new Error("VALUE_ADDRESS_INVALID");

  const nodeId = parseBoundedInteger(parts[1], 1, 232, "VALUE_NODE_ID_INVALID");
  const endpoint = parseBoundedInteger(parts[3], 0, 127, "VALUE_ENDPOINT_INVALID");
  const commandClass = parseBoundedInteger(parts[5], 1, 255, "VALUE_COMMAND_CLASS_INVALID");
  const property = parseTypedToken(parts[7], parts[8], "VALUE_PROPERTY_INVALID");
  const propertyKey = parts[10] === "none" && parts[11] === "-"
    ? undefined
    : parseTypedToken(parts[10], parts[11], "VALUE_PROPERTY_KEY_INVALID");

  if (DISALLOWED_VALUE_COMMAND_CLASSES.has(commandClass)) throw new Error("VALUE_COMMAND_CLASS_OUT_OF_SCOPE");
  return { nodeId, endpoint, valueId: { endpoint, commandClass, property, ...(propertyKey === undefined ? {} : { propertyKey }) } };
}

function parseTypedToken(kind, token, code) {
  if (kind === "number") {
    if (!/^-?(0|[1-9][0-9]*)$/.test(token)) throw new Error(code);
    const number = Number(token);
    if (!Number.isSafeInteger(number)) throw new Error(code);
    return number;
  }
  if (kind === "string") {
    let value;
    try { value = decodeURIComponent(token); } catch { throw new Error(code); }
    if (!isPortableValueString(value) || encodeURIComponent(value) !== token) throw new Error(code);
    return value;
  }
  throw new Error(code);
}

function isPortableValueString(value) {
  if (typeof value !== "string" || value.length === 0 || Buffer.byteLength(value, "utf8") > MAX_ADDRESS_COMPONENT_BYTES) return false;
  try { encodeURIComponent(value); return true; } catch { return false; }
}

function encodeTypedValue(value) {
  if (typeof value === "number" && Number.isSafeInteger(value)) return `number/${value}`;
  if (isPortableValueString(value)) return `string/${encodeURIComponent(value)}`;
  return null;
}

export function formatValueAddress(nodeId, valueId) {
  if (!Number.isInteger(nodeId) || nodeId < 1 || nodeId > 232 || !valueId ||
      !Number.isInteger(valueId.endpoint ?? 0) || !Number.isInteger(valueId.commandClass) ||
      valueId.commandClass < 1 || valueId.commandClass > 255) return null;
  const endpoint = valueId.endpoint ?? 0;
  if (endpoint < 0 || endpoint > 127) return null;
  const property = encodeTypedValue(valueId.property);
  const propertyKey = valueId.propertyKey === undefined ? "none/-" : encodeTypedValue(valueId.propertyKey);
  if (!property || !propertyKey) return null;
  return `node/${nodeId}/endpoint/${endpoint}/cc/${valueId.commandClass}/property/${property}/key/${propertyKey}`;
}

function parseBoundedInteger(value, minimum, maximum, code) {
  if (!/^(0|[1-9][0-9]*)$/.test(value)) throw new Error(code);
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < minimum || parsed > maximum) throw new Error(code);
  return parsed;
}

export function validateValueMetadata(valueId, metadata, tagType, writable) {
  if (!valueId || typeof metadata !== "object" || metadata === null) throw new Error("VALUE_METADATA_REQUIRED");
  if (DISALLOWED_VALUE_COMMAND_CLASSES.has(valueId.commandClass)) throw new Error("VALUE_COMMAND_CLASS_OUT_OF_SCOPE");
  if (metadata.secret !== false) throw new Error("VALUE_SECRET_OUT_OF_SCOPE");
  if (metadata.stateful !== true) throw new Error("VALUE_TRANSIENT_OUT_OF_SCOPE");
  if (metadata.readable !== true) throw new Error("VALUE_NOT_READABLE");

  const expected = tagType === "Boolean" ? "boolean"
    : tagType === "String" ? "string"
      : ["Int16", "Int32", "Int64", "Float", "Double"].includes(tagType) ? "number" : null;
  if (!expected || metadata.type !== expected) throw new Error("VALUE_TYPE_MISMATCH");
  if (metadata.type === "string" && tagType === "String" && metadata.maxLength > 4096) throw new Error("VALUE_STRING_LIMIT_EXCEEDED");
  if (writable && (metadata.writeable !== true || valueId.commandClass === 112)) {
    throw new Error(valueId.commandClass === 112 ? "CONFIGURATION_WRITE_OUT_OF_SCOPE" : "VALUE_NOT_WRITABLE");
  }
  return metadata;
}

export function normalizeValue(value, tagType, metadata) {
  if (value === undefined || value === null) throw new Error("VALUE_MISSING");
  switch (tagType) {
    case "Boolean":
      if (typeof value !== "boolean") throw new Error("VALUE_TYPE_MISMATCH");
      return value;
    case "String":
      if (typeof value !== "string" || value.length > Math.min(Number.isInteger(metadata?.maxLength) ? metadata.maxLength : 4096, 4096)) {
        throw new Error("VALUE_TYPE_MISMATCH");
      }
      return value;
    case "Int16":
      if (!Number.isInteger(value) || value < -32768 || value > 32767) throw new Error("VALUE_RANGE_INVALID");
      return value;
    case "Int32":
      if (!Number.isInteger(value) || value < -2147483648 || value > 2147483647) throw new Error("VALUE_RANGE_INVALID");
      return value;
    case "Int64":
      if (!Number.isSafeInteger(value) || Math.abs(value) > MAX_SAFE) throw new Error("VALUE_RANGE_INVALID");
      return value;
    case "Float":
    case "Double":
      if (typeof value !== "number" || !Number.isFinite(value)) throw new Error("VALUE_RANGE_INVALID");
      return value;
    default:
      throw new Error("VALUE_TYPE_UNSUPPORTED");
  }
}

export function validateWriteValue(value, tagType, metadata) {
  const normalized = normalizeValue(value, tagType, metadata);
  if (typeof normalized === "number") {
    if (Number.isFinite(metadata?.min) && normalized < metadata.min) throw new Error("VALUE_BELOW_DEVICE_MINIMUM");
    if (Number.isFinite(metadata?.max) && normalized > metadata.max) throw new Error("VALUE_ABOVE_DEVICE_MAXIMUM");
    if (Array.isArray(metadata?.allowed) && metadata.allowed.length > 0 && !metadata.allowed.some(entry => {
      if (typeof entry?.value === "number") return entry.value === normalized;
      if (typeof entry?.from !== "number" || typeof entry?.to !== "number") return false;
      const step = typeof entry.step === "number" && entry.step > 0 ? entry.step : 1;
      return normalized >= entry.from && normalized <= entry.to && Math.abs((normalized - entry.from) / step - Math.round((normalized - entry.from) / step)) < 1e-9;
    })) throw new Error("VALUE_NOT_IN_ALLOWED_SET");
  }
  return normalized;
}

export function sameValue(left, right) {
  return typeof left === typeof right && Object.is(left, right);
}

export function isAllowedReadCommandClass(commandClass) {
  return Number.isInteger(commandClass) && commandClass >= 1 && commandClass <= 255 &&
    !DISALLOWED_VALUE_COMMAND_CLASSES.has(commandClass);
}
