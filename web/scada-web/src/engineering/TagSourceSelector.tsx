import React, { useMemo, useState } from 'react';
import type { DataSourceEngineering } from './types';
import type { EngineeringLocale } from './i18n';
import {
  filterTagDataSources,
  resolveTagDataSource,
  tagDataSourceOptionIdentity,
  type TagSourceAwareEngineering
} from './TagSourceSelector.logic';

type Props = {
  tag: TagSourceAwareEngineering;
  sources: readonly DataSourceEngineering[];
  locale: EngineeringLocale;
  onChange: (source: DataSourceEngineering | null) => void;
};

export function TagSourceSelector({ tag, sources, locale, onChange }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const [query, setQuery] = useState('');
  const resolved = resolveTagDataSource(tag, sources);
  const visible = filterTagDataSources(sources, query);
  const selectedIdentity = resolved.source ? tagDataSourceOptionIdentity(resolved.source) : '';
  const selectedVisible = resolved.source && visible.some(source =>
    tagDataSourceOptionIdentity(source) === selectedIdentity);
  const options = resolved.source && !selectedVisible ? [resolved.source, ...visible] : visible;

  const choose = (identity: string) => {
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
        <option value="">{text.none}</option>
        {options.map(source => (
          <option key={tagDataSourceOptionIdentity(source)} value={tagDataSourceOptionIdentity(source)}>
            {source.name} · {source.driver} · {source.key}
          </option>
        ))}
      </select>
      {resolved.status === 'legacy-resolved' && (
        <small>{text.legacy}</small>
      )}
      {resolved.status === 'unresolved' && (
        <small role="alert">{text.unresolved}: {resolved.reference}</small>
      )}
      {sources.length === 0 && <small>{text.empty}</small>}
    </label>
  );
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    label: 'Data Source', search: 'Search configured Data Sources', none: 'No Data Source',
    legacy: 'Legacy key reference. Preview/Apply will migrate it to stable Source identity.',
    unresolved: 'Invalid Source reference', empty: 'No Data Sources are configured in the Working project.'
  };
  if (locale === 'es') return {
    label: 'Data Source', search: 'Buscar Data Sources configurados', none: 'Sin Data Source',
    legacy: 'Referencia heredada por clave. Preview/Apply la migrará a la identidad estable del Source.',
    unresolved: 'Referencia de Source inválida', empty: 'No hay Data Sources configurados en el proyecto Working.'
  };
  return {
    label: 'Data Source', search: 'Pesquisar Data Sources configurados', none: 'Sem Data Source',
    legacy: 'Referência legada por chave. Preview/Apply migrará para a identidade estável do Source.',
    unresolved: 'Referência de Source inválida', empty: 'Nenhum Data Source está configurado no projeto Working.'
  };
}
