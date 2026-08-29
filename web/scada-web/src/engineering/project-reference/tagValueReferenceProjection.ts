import type { TagValueReferenceEngineering } from '../types';

export type TagValueProjection = Readonly<{
  ok: boolean;
  value?: unknown;
  dataType: string;
  detail?: string;
}>;

/**
 * Browser-side projection of the canonical TAG value-reference selector.
 * Core remains the domain authority; this helper mirrors its fail-closed bit
 * semantics for UI/runtime projections that consume authoritative TAG samples.
 */
export function projectTagValueReference(
  reference: TagValueReferenceEngineering | null | undefined,
  sourceDataType: string,
  value: unknown
): TagValueProjection {
  const selector = reference?.selector;
  if (!selector) return Object.freeze({ ok: true, value, dataType: sourceDataType });

  if (selector.kind !== 'bit' || !Number.isInteger(selector.index) || selector.index < 0) {
    return unavailable('Invalid canonical TAG bit selector.');
  }

  const width = integerBitWidth(sourceDataType);
  if (width === null || selector.index >= width) {
    return unavailable(`Bit ${selector.index} is not valid for TAG data type '${sourceDataType}'.`);
  }

  const integer = integerLikeToBigInt(value);
  if (integer === null) {
    return unavailable('The authoritative integer TAG value cannot be represented safely for bit projection.');
  }

  return Object.freeze({
    ok: true,
    value: ((integer >> BigInt(selector.index)) & 1n) === 1n,
    dataType: 'Boolean'
  });
}

export function tagValueReferenceMatchesRuntimeTag(
  reference: TagValueReferenceEngineering | null | undefined,
  friendlyTarget: string,
  runtimeTag: Readonly<{ id: string; path: string }>
): boolean {
  const tagId = reference?.tagId?.trim();
  return tagId
    ? normalizeIdentity(tagId) === normalizeIdentity(runtimeTag.id)
    : friendlyTarget === runtimeTag.path;
}

export function integerBitWidth(dataType: string): number | null {
  const normalized = dataType.trim().toLowerCase();
  return normalized === 'int16' ? 16
    : normalized === 'int32' ? 32
      : normalized === 'int64' ? 64
        : null;
}

function integerLikeToBigInt(value: unknown): bigint | null {
  if (typeof value === 'bigint') return value;
  if (typeof value === 'number') {
    if (!Number.isFinite(value) || !Number.isInteger(value) || !Number.isSafeInteger(value)) return null;
    return BigInt(value);
  }
  if (typeof value === 'string' && /^[+-]?\d+$/.test(value.trim())) {
    try { return BigInt(value.trim()); } catch { return null; }
  }
  return null;
}

function normalizeIdentity(value: string): string {
  return value.trim().toLocaleLowerCase();
}

function unavailable(detail: string): TagValueProjection {
  return Object.freeze({ ok: false, dataType: 'Boolean', detail });
}
