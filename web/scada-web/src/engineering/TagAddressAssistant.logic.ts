import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';

export type ModbusAddressBuildResult = Readonly<{
  address: string;
  metadata: Record<string, string>;
  addressSelector?: { kind: 'bit' | string; index: number } | null;
  writableArea: boolean;
  canonicalReferenceBase: 'zeroBased' | string;
}>;

const MODBUS_METADATA_KEYS = new Set([
  'modbus.area',
  'modbus.unitId',
  'modbus.valueType',
  'modbus.wordOrder',
  'modbus.scale',
  'modbus.offset'
].map(key => key.toLowerCase()));

export function applyModbusAddressBuild(
  tag: TagSourceAwareEngineering,
  result: ModbusAddressBuildResult
): TagSourceAwareEngineering {
  const metadata = Object.fromEntries(
    Object.entries(tag.metadata ?? {}).filter(([key]) => !MODBUS_METADATA_KEYS.has(key.toLowerCase())));
  Object.assign(metadata, result.metadata);

  return {
    ...tag,
    address: result.address,
    metadata,
    addressSelector: result.addressSelector ?? null
  };
}

export function parseCanonicalModbusAddress(address: string | null | undefined): {
  area: 'coil' | 'discrete' | 'holding' | 'input';
  reference: string;
} | null {
  const match = /^(coil|discrete|holding|input):(\d+)$/i.exec(address?.trim() ?? '');
  if (!match) return null;
  const normalized = match[1].toLowerCase() as 'coil' | 'discrete' | 'holding' | 'input';
  return { area: normalized, reference: match[2] };
}

export function metadataValue(tag: TagSourceAwareEngineering, key: string): string {
  const entry = Object.entries(tag.metadata ?? {}).find(([candidate]) => candidate.toLowerCase() === key.toLowerCase());
  return entry?.[1] ?? '';
}
