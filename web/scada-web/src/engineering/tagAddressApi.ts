import type { ModbusAddressBuildResult } from './TagAddressAssistant.logic';
import { loadTagBindingSchema } from './TagBindingSchema';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type ModbusAddressBuildRequest = Readonly<{
  area: string;
  reference: number;
  referenceBase: 'zeroBased' | 'oneBased';
  unitId?: number | null;
  valueType?: string | null;
  wordOrder?: string | null;
  scale?: number | null;
  offset?: number | null;
  bitIndex?: number | null;
}>;

export async function buildModbusTagAddress(
  request: ModbusAddressBuildRequest
): Promise<ModbusAddressBuildResult> {
  const schemaPromise = loadTagBindingSchema('modbus.tcp');
  const response = await fetch(`${API}/api/engineering/tag-address/modbus/build`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(request)
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `${response.status} ${response.statusText}`);
  }

  const [result, bindingSchema] = await Promise.all([
    response.json() as Promise<ModbusAddressBuildResult>,
    schemaPromise
  ]);
  return { ...result, bindingSchema };
}
