import type { DataSourceEngineering, TagEngineering } from './types';

export type TagSourceReference = Readonly<{
  status: 'none' | 'resolved' | 'legacy-resolved' | 'unresolved';
  source: DataSourceEngineering | null;
  reference: string | null;
}>;

export type TagSourceAwareEngineering = TagEngineering & {
  dataSourceId?: string | null;
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
  if (!source) return { ...tag, dataSourceId: null, source: null };
  return {
    ...tag,
    dataSourceId: source.id ?? null,
    source: source.key
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
