import type { TagBindingSchemaIdentity } from './DataSourceCatalogEditor.logic';
import type { TagSourceAwareEngineering as Tag } from './TagSourceSelector.logic';
import type { TagPhysicalValueTransformEngineering } from './types';

type Settings = Record<string, string>;

const formAddressDrivers = new Set(['knx.ip', 'zigbee.native', 'siemens.s7.iso', 'rockwell.logix.eip']);
const addressOnlyDrivers = new Set(['zwave.js.controller', 'matter.wifi']);

export function usesManualBindingAddress(driver: string): boolean {
  return !formAddressDrivers.has(driver);
}

export function supportsAddressOnlyBinding(driver: string): boolean {
  return addressOnlyDrivers.has(driver);
}

export function buildTagBinding(driver: string, tag: Tag, identity: TagBindingSchemaIdentity, fields: Settings): Tag | null {
  let address = tag.address?.trim() ?? '';
  let settings = fields;
  let valueTransform: TagPhysicalValueTransformEngineering | null | undefined;
  let addressSelector = tag.addressSelector;
  switch (driver) {
    case 'knx.ip': {
      const { writeGroupAddress, stateGroupAddress, dpt } = fields;
      if (!validKnxAddress(writeGroupAddress) || !validKnxAddress(stateGroupAddress)) return null;
      const normalized = (value: string) => value.split('/').map(Number).join('/');
      address = normalized(writeGroupAddress);
      const feedback = normalized(stateGroupAddress);
      if (!tag.readOnly && address === feedback) return null;
      settings = { stateGroupAddress: feedback, dpt };
      break;
    }
    case 'zigbee.native': {
      const { ieeeAddress, endpoint, cluster, attribute } = fields;
      if (!/^(?:0x)?[0-9a-f]{16}$/i.test(ieeeAddress)) return null;
      address = '0x' + ieeeAddress.toLowerCase().replace(/^0x/, '') + '/' + Number(endpoint) + '/' + cluster + '/' + attribute;
      settings = {};
      break;
    }
    case 'siemens.s7.iso': {
      const { area, dbNumber, byteOffset, bitOffset, valueType, stringLength, writable, valueOrder } = fields;
      address = [
        's7iso:v1', 'area=' + area, 'db=' + Number(dbNumber ?? 0), 'byte=' + Number(byteOffset),
        'bit=' + Number(bitOffset ?? 0), 'type=' + valueType, 'string=' + Number(stringLength ?? 0),
        'writable=' + (writable === 'true')
      ].join(';');
      settings = {};
      valueTransform = { contractVersion: 1, byteSwap: valueOrder === 'ByteSwap' || valueOrder === 'ByteAndWordSwap',
        wordSwap: valueOrder === 'WordSwap' || valueOrder === 'ByteAndWordSwap' };
      break;
    }
    case 'rockwell.logix.eip': {
      const { scope, programName, symbolPath, nativeType, externalAccess, constant, bitIndex } = fields;
      if (Boolean(programName) !== (scope === 'Program')) return null;
      const parts = ['logix:v1', 'scope=' + scope.toLowerCase()];
      if (programName) parts.push('program=' + encodeURIComponent(programName));
      parts.push('symbol=' + encodeURIComponent(symbolPath), 'native=' + nativeType.toLowerCase(),
        'access=' + (externalAccess ?? 'Unknown').toLowerCase(), 'constant=' + (constant === 'true'));
      address = parts.join(';');
      addressSelector = bitIndex === undefined ? null : { kind: 'bit', index: Number(bitIndex) };
      settings = {};
      break;
    }
    default:
      valueTransform = tag.communicationBinding?.schemaId === identity.schemaId
        ? tag.communicationBinding.valueTransform : undefined;
  }

  if (!address) return null;
  return {
    ...tag, address, addressSelector,
    communicationBinding: { contractVersion: 1, ...identity, portableAddress: address, settings, valueTransform }
  };
}

export function bindingFormValues(driver: string, tag: Tag): Settings {
  const binding = tag.communicationBinding;
  if (!binding) return {};
  const address = binding.portableAddress;
  const settings = binding.settings ?? {};
  switch (driver) {
    case 'knx.ip':
      return { ...settings, writeGroupAddress: address };
    case 'zigbee.native': {
      const [ieeeAddress, endpoint, cluster, attribute] = address.split('/');
      return { ieeeAddress, endpoint, cluster, attribute };
    }
    case 'siemens.s7.iso': {
      const parts = addressFields(address);
      const { byteSwap, wordSwap } = binding.valueTransform ?? {};
      return {
        area: parts.area, dbNumber: parts.db, byteOffset: parts.byte, bitOffset: parts.bit,
        valueType: parts.type, stringLength: parts.string, writable: parts.writable,
        valueOrder: byteSwap ? (wordSwap ? 'ByteAndWordSwap' : 'ByteSwap') : (wordSwap ? 'WordSwap' : 'Normal')
      };
    }
    case 'rockwell.logix.eip': {
      const parts = addressFields(address);
      return {
        scope: enumValue(parts.scope, ['Controller', 'Program']),
        programName: parts.program, symbolPath: parts.symbol,
        nativeType: enumValue(parts.native, ['Bool', 'Sint', 'Int', 'Dint', 'Lint', 'Real', 'Lreal', 'String']),
        externalAccess: enumValue(parts.access, ['Unknown', 'ReadOnly', 'ReadWrite', 'None']),
        constant: parts.constant,
        bitIndex: tag.addressSelector?.kind === 'bit' ? String(tag.addressSelector.index) : ''
      };
    }
    default:
      return settings;
  }
}

function validKnxAddress(value: string): boolean {
  if (!/^\d{1,2}\/\d{1,2}\/\d{1,3}$/.test(value)) return false;
  const [main, middle, sub] = value.split('/').map(Number);
  return main <= 31 && middle <= 7 && sub <= 255;
}

function addressFields(address: string): Settings {
  const fields: Settings = {};
  for (const part of address.split(';').slice(1)) {
    const at = part.indexOf('=');
    if (at <= 0) continue;
    try {
      fields[part.slice(0, at).toLowerCase()] = decodeURIComponent(part.slice(at + 1));
    } catch {
      // A malformed saved value remains available in the manual address field.
    }
  }
  return fields;
}

function enumValue(value: string | undefined, options: readonly string[]): string {
  return options.find(option => option.toLowerCase() === value?.toLowerCase()) ?? '';
}
