import React, { useMemo, useState } from 'react';
import type { DataSourceEngineering } from './types';
import type { EngineeringLocale } from './i18n';
import { c04Text } from './c04I18n';
import {
  filterTagDataSources,
  resolveTagDataSource,
  tagDataSourceOptionIdentity,
  type TagSourceAwareEngineering
} from './TagSourceSelector.logic';

const UNRESOLVED_IDENTITY = '__unresolved-source__';

type Props = {
  tag: TagSourceAwareEngineering;
  sources: readonly DataSourceEngineering[];
  locale: EngineeringLocale;
  onChange: (source: DataSourceEngineering | null) => void;
};

export function TagSourceSelector({ tag, sources, locale, onChange }: Props) {
  const text = useMemo(() => c04Text(locale).tagSource, [locale]);
  const [query, setQuery] = useState('');
  const resolved = resolveTagDataSource(tag, sources);
  const visible = filterTagDataSources(sources, query);
  const selectedIdentity = resolved.status === 'unresolved'
    ? UNRESOLVED_IDENTITY
    : resolved.source ? tagDataSourceOptionIdentity(resolved.source) : '';
  const selectedVisible = resolved.source && visible.some(source =>
    tagDataSourceOptionIdentity(source) === selectedIdentity);
  const options = resolved.source && !selectedVisible ? [resolved.source, ...visible] : visible;

  const choose = (identity: string) => {
    if (identity === UNRESOLVED_IDENTITY) return;
    if (!identity) {
      onChange(null);
      return;
    }
    const source = sources.find(candidate => tagDataSourceOptionIdentity(candidate) === identity) ?? null;
    onChange(source);
  };

  return (
    <label className="eng-editor-field eng-editor-field-wide" data-testid="tag-source-selector">
      <span>{text.label}</span>
      <input
        type="search"
        aria-label={text.search}
        placeholder={text.search}
        value={query}
        onChange={event => setQuery(event.target.value)}
        data-testid="tag-source-search"
      />
      <select
        value={selectedIdentity}
        onChange={event => choose(event.target.value)}
        data-testid="tag-source-select"
        aria-invalid={resolved.status === 'unresolved' ? 'true' : undefined}
      >
        {resolved.status === 'unresolved' && (
          <option value={UNRESOLVED_IDENTITY} disabled>{text.unresolved}: {resolved.reference}</option>
        )}
        <option value="">{text.none}</option>
        {options.map(source => (
          <option key={tagDataSourceOptionIdentity(source)} value={tagDataSourceOptionIdentity(source)}>
            {source.name} · {source.driver} · {source.key}
          </option>
        ))}
      </select>
      {resolved.status === 'legacy-resolved' && <small>{text.legacy}</small>}
      {resolved.status === 'unresolved' && <small role="alert">{text.unresolved}: {resolved.reference}</small>}
      {sources.length === 0 && <small>{text.empty}</small>}
    </label>
  );
}
