import type { DataSourceEngineering, TagEngineering } from './types';

export type TagPhysicalValueTransformEngineering = Readonly<{
  contractVersion?: number;
  byteSwap?: boolean;
  wordSwap?: boolean;
}>;

export type CommunicationTagBindingEngineering = Readonly<{
  contractVersion: number;
  schemaId: string;
  schemaVersion: number;
  portableAddress: string;
  settings?: Record<string, string> | null;
  valueTransform?: TagPhysicalValueTransformEngineering | null;
}>;

export type TagSourceReference = Readonly<{
  status: 'none' | 'resolved' | 'legacy-resolved' | 'unresolved';
  source: DataSourceEngineering | null;
  reference: string | null;
}>;

export type TagSourceAwareEngineering = TagEngineering & {
  dataSourceId?: string | null;
  communicationBinding?: CommunicationTagBindingEngineering | null;
};

export function resolveTagDataSource(
  tag: TagSourceAwareEngineering,
  sources: readonly DataSourceEngineering[]
): TagSourceReference {
  const dataSourceId = tag.dataSourceId?.trim();
  if (dataSourceId) {
    const source = sources.find(candidate =>
      candidate.id?.toLowerCase() === dataSourceId.toLowerCase()) ?? null;
    return source
      ? { status: 'resolved', source, reference: dataSourceId }
      : { status: 'unresolved', source: null, reference: dataSourceId };
  }

  const sourceKey = tag.source?.trim();
  if (!sourceKey) return { status: 'none', source: null, reference: null };

  const source = sources.find(candidate =>
    candidate.key.toLowerCase() === sourceKey.toLowerCase()) ?? null;
  return source
    ? { status: 'legacy-resolved', source, reference: sourceKey }
    : { status: 'unresolved', source: null, reference: sourceKey };
}

export function assignTagDataSource(
  tag: TagSourceAwareEngineering,
  source: DataSourceEngineering | null
): TagSourceAwareEngineering {
  if (!source) {
    return {
      ...tag,
      dataSourceId: null,
      source: null,
      communicationBinding: null
    };
  }

  const stableId = tag.dataSourceId?.trim();
  const legacyKey = tag.source?.trim();
  const sourceChanged = stableId
    ? stableId.toLowerCase() !== source.id?.toLowerCase()
    : legacyKey
      ? legacyKey.toLowerCase() !== source.key.toLowerCase()
      : true;

  return {
    ...tag,
    dataSourceId: source.id ?? null,
    source: source.key,
    address: sourceChanged ? null : tag.address,
    addressSelector: sourceChanged ? null : tag.addressSelector,
    communicationBinding: sourceChanged ? null : tag.communicationBinding
  };
}

export function updateManualTagAddress(
  tag: TagSourceAwareEngineering,
  value: string | null
): TagSourceAwareEngineering {
  const address = value?.trim() ? value : null;
  if (!tag.communicationBinding) return { ...tag, address };
  if (!address) return { ...tag, address: null, communicationBinding: null };
  return {
    ...tag,
    address,
    communicationBinding: {
      ...tag.communicationBinding,
      portableAddress: address
    }
  };
}

export function filterTagDataSources(
  sources: readonly DataSourceEngineering[],
  query: string
): DataSourceEngineering[] {
  const normalized = query.trim().toLowerCase();
  const ordered = [...sources].sort((left, right) =>
    left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }) ||
    left.key.localeCompare(right.key, undefined, { sensitivity: 'base' }));
  if (!normalized) return ordered;
  return ordered.filter(source =>
    `${source.name} ${source.key} ${source.driver}`.toLowerCase().includes(normalized));
}

export function tagDataSourceOptionIdentity(source: DataSourceEngineering): string {
  return source.id ? `id:${source.id}` : `key:${source.key}`;
}
