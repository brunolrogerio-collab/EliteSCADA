import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import {
  filterProjectReferences,
  projectReferenceFamilyLabel,
  type ProjectReferenceDescriptor,
  type ProjectReferenceFamily
} from './projectReferenceModel';
import './project-reference.css';

export type ProjectReferenceBrowserProps = Readonly<{
  references: readonly ProjectReferenceDescriptor[];
  locale: EngineeringLocale;
  selectedReference?: string | null;
  isSelectable?: (reference: ProjectReferenceDescriptor) => boolean;
  onSelect: (reference: ProjectReferenceDescriptor) => void;
  title?: string;
}>;

const FAMILY_ORDER: readonly ProjectReferenceFamily[] = [
  'tag', 'serverMemory', 'clientMemory', 'system', 'driverDiagnostic', 'asset'
];

export function ProjectReferenceBrowser({
  references,
  locale,
  selectedReference,
  isSelectable,
  onSelect,
  title
}: ProjectReferenceBrowserProps) {
  const copy = browserCopy(locale);
  const [query, setQuery] = useState('');
  const filtered = useMemo(() => filterProjectReferences(references, query), [references, query]);
  const groups = useMemo(() => FAMILY_ORDER.map(family => ({
    family,
    items: filtered.filter(item => item.family === family)
  })).filter(group => group.items.length > 0), [filtered]);

  return <section className="project-reference-browser" data-testid="project-reference-browser">
    <header>
      <strong>{title ?? copy.title}</strong>
      <span>{copy.hint}</span>
    </header>
    <label className="project-reference-search">
      <span>{copy.search}</span>
      <input
        type="search"
        value={query}
        placeholder={copy.searchPlaceholder}
        onChange={event => setQuery(event.currentTarget.value)}
      />
    </label>

    <div className="project-reference-tree" role="tree" aria-label={title ?? copy.title}>
      {groups.length === 0 ? <p>{copy.empty}</p> : groups.map(group => (
        <details key={group.family} open>
          <summary>{projectReferenceFamilyLabel(group.family, locale)} <small>{group.items.length}</small></summary>
          <ul>
            {group.items.map(item => {
              const selectable = isSelectable ? isSelectable(item) : true;
              const selected = selectedReference === item.reference;
              return <li key={`${item.family}:${item.reference}`}>
                <button
                  type="button"
                  role="treeitem"
                  aria-selected={selected}
                  disabled={!selectable}
                  className={selected ? 'selected' : ''}
                  onClick={() => onSelect(item)}
                  title={!selectable ? copy.incompatible : item.reference}
                >
                  <span className="project-reference-node-label">{item.label}</span>
                  <code>{item.reference}</code>
                  <small>{item.dataType}{item.engineeringUnit ? ` · ${item.engineeringUnit}` : ''}</small>
                </button>
              </li>;
            })}
          </ul>
        </details>
      ))}
    </div>
  </section>;
}

function browserCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Project references',
    hint: 'Browse canonical project/runtime sources by family.',
    search: 'Search',
    searchPlaceholder: 'Name, path, type or provider',
    empty: 'No matching references.',
    incompatible: 'This source is not compatible with the selected destination.'
  };
  if (locale === 'es') return {
    title: 'Referencias del proyecto',
    hint: 'Explore fuentes canónicas del proyecto/runtime por familia.',
    search: 'Buscar',
    searchPlaceholder: 'Nombre, path, tipo o proveedor',
    empty: 'No hay referencias coincidentes.',
    incompatible: 'Esta fuente no es compatible con el destino seleccionado.'
  };
  return {
    title: 'Referências do projeto',
    hint: 'Navegue pelas fontes canônicas do projeto/runtime por família.',
    search: 'Pesquisar',
    searchPlaceholder: 'Nome, path, tipo ou provedor',
    empty: 'Nenhuma referência encontrada.',
    incompatible: 'Esta fonte não é compatível com o destino selecionado.'
  };
}
