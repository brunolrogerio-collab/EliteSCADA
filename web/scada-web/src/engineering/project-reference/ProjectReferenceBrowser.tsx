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

type ReferenceTreeNode = {
  name: string;
  path: string;
  children: Map<string, ReferenceTreeNode>;
  items: ProjectReferenceDescriptor[];
};

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
  const groups = useMemo(() => FAMILY_ORDER.map(family => {
    const items = filtered.filter(item => item.family === family);
    return { family, items, tree: buildReferenceTree(items) };
  }).filter(group => group.items.length > 0), [filtered]);

  const renderItem = (item: ProjectReferenceDescriptor) => {
    const selectable = isSelectable ? isSelectable(item) : true;
    const selected = selectedReference === item.reference;
    const leaf = item.pathSegments[item.pathSegments.length - 1] || item.label;
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
        <span className="project-reference-node-label">{leaf === item.label ? item.label : `${leaf} · ${item.label}`}</span>
        <code>{item.reference}</code>
        <small>{item.dataType}{item.engineeringUnit ? ` · ${item.engineeringUnit}` : ''}</small>
      </button>
    </li>;
  };

  const renderBranch = (node: ReferenceTreeNode): React.ReactNode => (
    <li key={node.path} className="project-reference-branch">
      <details open>
        <summary role="treeitem">{node.name}</summary>
        <ul role="group">
          {[...node.children.values()].map(child => renderBranch(child))}
          {node.items.map(renderItem)}
        </ul>
      </details>
    </li>
  );

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
        <details key={group.family} open className="project-reference-family">
          <summary>{projectReferenceFamilyLabel(group.family, locale)} <small>{group.items.length}</small></summary>
          <ul role="group">
            {[...group.tree.children.values()].map(child => renderBranch(child))}
            {group.tree.items.map(renderItem)}
          </ul>
        </details>
      ))}
    </div>
  </section>;
}

export function buildReferenceTree(items: readonly ProjectReferenceDescriptor[]): ReferenceTreeNode {
  const root: ReferenceTreeNode = { name: '', path: '', children: new Map(), items: [] };
  for (const item of items) {
    const segments = item.pathSegments.length > 0 ? item.pathSegments : [item.label];
    const branches = segments.slice(0, -1);
    let node = root;
    for (const segment of branches) {
      const name = segment.trim() || '—';
      const path = node.path ? `${node.path}/${name}` : name;
      let child = node.children.get(name);
      if (!child) {
        child = { name, path, children: new Map(), items: [] };
        node.children.set(name, child);
      }
      node = child;
    }
    node.items.push(item);
  }
  sortTree(root);
  return root;
}

function sortTree(node: ReferenceTreeNode): void {
  node.items.sort((left, right) => left.reference.localeCompare(right.reference));
  const sortedChildren = [...node.children.entries()].sort(([left], [right]) => left.localeCompare(right));
  node.children.clear();
  for (const [key, child] of sortedChildren) {
    sortTree(child);
    node.children.set(key, child);
  }
}

function browserCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Project references',
    hint: 'Browse canonical project/runtime sources by family and project path.',
    search: 'Search',
    searchPlaceholder: 'Name, path, type or provider',
    empty: 'No matching references.',
    incompatible: 'This source is not compatible with the selected destination.'
  };
  if (locale === 'es') return {
    title: 'Referencias del proyecto',
    hint: 'Explore fuentes canónicas del proyecto/runtime por familia y ruta.',
    search: 'Buscar',
    searchPlaceholder: 'Nombre, path, tipo o proveedor',
    empty: 'No hay referencias coincidentes.',
    incompatible: 'Esta fuente no es compatible con el destino seleccionado.'
  };
  return {
    title: 'Referências do projeto',
    hint: 'Navegue pelas fontes canônicas do projeto/runtime por família e caminho.',
    search: 'Pesquisar',
    searchPlaceholder: 'Nome, path, tipo ou provedor',
    empty: 'Nenhuma referência encontrada.',
    incompatible: 'Esta fonte não é compatível com o destino selecionado.'
  };
}
