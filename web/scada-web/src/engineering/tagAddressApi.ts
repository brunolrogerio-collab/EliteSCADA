import type { ModbusAddressBuildResult } from './TagAddressAssistant.logic';

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

  return await response.json() as ModbusAddressBuildResult;
}
