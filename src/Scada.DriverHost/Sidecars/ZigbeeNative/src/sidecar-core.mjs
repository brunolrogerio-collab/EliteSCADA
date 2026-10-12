export const ARTIFACT_ID = 'elitescada.zigbee.native';
export const ARTIFACT_VERSION = '1.0.0';
export const RUNTIME_VERSION = '24.21.0';
export const SCHEMA_VERSION = 1;

const IEEE_PATTERN = /^(?:0x)?([0-9a-f]{16})$/i;

export function normalizeIeee(value) {
  const match = typeof value === 'string' ? IEEE_PATTERN.exec(value) : null;
  if (!match) throw new Error('INVALID_IEEE_ADDRESS');
  return match[1].toLowerCase();
}

export function pointKey(ieee, endpoint) {
  if (!Number.isInteger(endpoint) || endpoint < 1 || endpoint > 240) throw new Error('INVALID_ENDPOINT');
  return `${normalizeIeee(ieee)}/${endpoint}`;
}

export function parseRuntimeConfiguration(env, runtimeVersion = process.version) {
  if (runtimeVersion !== `v${RUNTIME_VERSION}`) throw new Error('NODE_RUNTIME_VERSION_MISMATCH');

  const networkKeyText = env.ZIGBEE_NETWORK_KEY;
  if (typeof networkKeyText !== 'string' || !/^[0-9a-f]{32}$/i.test(networkKeyText)) {
    throw new Error('INVALID_PROTECTED_NETWORK_KEY');
  }

  const serialPath = env.ZIGBEE_SERIAL_PATH;
  if (typeof serialPath !== 'string' || !serialPath.trim() || /[\r\n\0]/.test(serialPath)) {
    throw new Error('INVALID_SERIAL_PATH');
  }

  const panId = Number.parseInt(env.ZIGBEE_PAN_ID, 10);
  if (!Number.isInteger(panId) || panId < 1 || panId > 0xfffe) throw new Error('INVALID_PAN_ID');

  const extendedPanIdText = env.ZIGBEE_EXTENDED_PAN_ID;
  if (typeof extendedPanIdText !== 'string' || !/^[0-9a-f]{16}$/i.test(extendedPanIdText)) {
    throw new Error('INVALID_EXTENDED_PAN_ID');
  }

  const channel = Number.parseInt(env.ZIGBEE_CHANNEL, 10);
  if (!Number.isInteger(channel) || channel < 11 || channel > 26) throw new Error('INVALID_CHANNEL');

  const baudRate = Number.parseInt(env.ZIGBEE_BAUD_RATE ?? '115200', 10);
  if (!Number.isInteger(baudRate) || baudRate < 9600 || baudRate > 1_000_000) throw new Error('INVALID_BAUD_RATE');

  const rpcPort = Number.parseInt(env.ZIGBEE_RPC_PORT, 10);
  if (!Number.isInteger(rpcPort) || rpcPort < 1 || rpcPort > 65535) throw new Error('INVALID_RPC_PORT');
  if (env.ZIGBEE_RPC_HOST !== '127.0.0.1') throw new Error('RPC_MUST_USE_LOOPBACK');
  if (typeof env.ZIGBEE_RPC_TOKEN !== 'string' || !/^[0-9a-f]{64}$/i.test(env.ZIGBEE_RPC_TOKEN)) {
    throw new Error('INVALID_RPC_TOKEN');
  }

  const databasePath = env.ZIGBEE_DATABASE_PATH;
  if (typeof databasePath !== 'string' || !databasePath.trim() || /[\r\n\0]/.test(databasePath)) {
    throw new Error('INVALID_DATABASE_PATH');
  }

  let allowedPoints;
  try {
    allowedPoints = JSON.parse(env.ZIGBEE_ALLOWED_POINTS);
  } catch {
    throw new Error('INVALID_ALLOWED_POINTS');
  }
  if (!Array.isArray(allowedPoints) || allowedPoints.length < 1 || allowedPoints.length > 512) {
    throw new Error('INVALID_ALLOWED_POINTS');
  }

  const normalizedAllowedPoints = new Map();
  for (const point of allowedPoints) {
    const key = pointKey(point.ieee, point.endpoint);
    if (normalizedAllowedPoints.has(key)) throw new Error('DUPLICATE_ALLOWED_POINT');
    normalizedAllowedPoints.set(key, { ieee: normalizeIeee(point.ieee), endpoint: point.endpoint });
  }

  return {
    networkKey: Array.from(Buffer.from(networkKeyText, 'hex')),
    serialPath: serialPath.trim(),
    panId,
    extendedPanId: Array.from(Buffer.from(extendedPanIdText, 'hex')),
    channel,
    baudRate,
    rpcPort,
    rpcHost: '127.0.0.1',
    rpcToken: env.ZIGBEE_RPC_TOKEN,
    databasePath,
    allowedPoints: normalizedAllowedPoints,
  };
}

export function assertAllowedRequest(request, allowedPoints) {
  if (!request || request.type !== 'request' || typeof request.id !== 'string' || request.id.length > 128) {
    throw new Error('INVALID_REQUEST');
  }
  if (request.method !== 'read' && request.method !== 'write') throw new Error('METHOD_NOT_ALLOWED');
  const key = pointKey(request.ieee, request.endpoint);
  if (!allowedPoints.has(key)) throw new Error('POINT_NOT_CONFIGURED');
  if (request.method === 'write' && typeof request.value !== 'boolean') throw new Error('BOOLEAN_VALUE_REQUIRED');
  return { ...allowedPoints.get(key), key, id: request.id, method: request.method, value: request.value };
}

export function hasStateExposure(definition, access, requiredMask = access.STATE) {
  if (!definition || !Array.isArray(definition.exposes)) return false;
  return definition.exposes.some((expose) => {
    const features = Array.isArray(expose.features) ? expose.features : [expose];
    return features.some((feature) =>
      feature && (feature.property === 'state' || feature.name === 'state') &&
      Number.isInteger(feature.access) && (feature.access & requiredMask) === requiredMask);
  });
}

export function mapOnOffObservation(message, allowedPoints) {
  if (!message || (message.type !== 'attributeReport' && message.type !== 'readResponse')) return null;
  if (message.cluster !== 'genOnOff' || !message.device || !message.endpoint) return null;
  if (!Object.hasOwn(message.data ?? {}, 'onOff')) return null;
  const rawValue = message.data.onOff;
  if (rawValue !== true && rawValue !== false && rawValue !== 0 && rawValue !== 1) return null;
  const ieee = normalizeIeee(message.device.ieeeAddr);
  const endpoint = message.endpoint.ID;
  const key = pointKey(ieee, endpoint);
  if (!allowedPoints.has(key)) return null;
  return {
    ieee,
    endpoint,
    value: rawValue === true || rawValue === 1,
    source: message.type === 'attributeReport' ? 'device-report' : 'device-readback',
    observedAt: new Date().toISOString(),
  };
}
