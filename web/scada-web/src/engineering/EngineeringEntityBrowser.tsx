import { useId, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import {
  filterEngineeringEntities,
  selectAdjacentEngineeringEntityKey,
  type EngineeringEntityBrowserFilter,
  type EngineeringEntityNavigationDirection
} from './EngineeringEntityBrowser.logic';
import './engineering-entity-browser.css';

export type EngineeringEntityBrowserMessages = {
  searchLabel: string;
  searchPlaceholder: string;
  filterLabel: string;
  allFilterLabel: string;
  listLabel: string;
  detailLabel: string;
  loadingTitle: string;
  loadingDescription?: string;
  emptyTitle: string;
  emptyDescription?: string;
  noMatchesTitle: string;
  noMatchesDescription?: string;
  detailEmptyTitle: string;
  detailEmptyDescription?: string;
  formatResultSummary: (visibleCount: number, totalCount: number) => string;
};

export type EngineeringEntityBrowserProps<T> = {
  items: readonly T[];
  selectedKey: string | null;
  onSelectionChange: (key: string, item: T) => void;
  getKey: (item: T) => string;
  getLabel: (item: T) => string;
  getDescription?: (item: T) => string | null | undefined;
  getSearchText?: (item: T) => readonly string[];
  renderItemMeta?: (item: T) => ReactNode;
  renderDetail: (item: T) => ReactNode;
  filters?: readonly EngineeringEntityBrowserFilter<T>[];
  loading?: boolean;
  messages: EngineeringEntityBrowserMessages;
  className?: string;
};

export function EngineeringEntityBrowser<T>({
  items,
  selectedKey,
  onSelectionChange,
  getKey,
  getLabel,
  getDescription,
  getSearchText,
  renderItemMeta,
  renderDetail,
  filters = [],
  loading = false,
  messages,
  className
}: EngineeringEntityBrowserProps<T>) {
  const browserId = useId();
  const buttonRefs = useRef(new Map<string, HTMLButtonElement>());
  const [query, setQuery] = useState('');
  const [activeFilterKey, setActiveFilterKey] = useState('');

  const activeFilter = filters.find(filter => filter.key === activeFilterKey);
  const visibleItems = useMemo(
    () => filterEngineeringEntities(
      items,
      query,
      activeFilter,
      item => getSearchText?.(item) ?? [getLabel(item), getDescription?.(item) ?? '']
    ),
    [activeFilter, getDescription, getLabel, getSearchText, items, query]
  );

  const visibleKeys = useMemo(() => visibleItems.map(getKey), [getKey, visibleItems]);
  const selectedItem = useMemo(
    () => items.find(item => getKey(item) === selectedKey) ?? null,
    [getKey, items, selectedKey]
  );
  const selectedVisibleIndex = selectedKey ? visibleKeys.indexOf(selectedKey) : -1;
  const tabbableIndex = selectedVisibleIndex >= 0 ? selectedVisibleIndex : 0;
  const rootClassName = ['engineering-entity-browser', className].filter(Boolean).join(' ');

  const selectAndFocus = (direction: EngineeringEntityNavigationDirection) => {
    const nextKey = selectAdjacentEngineeringEntityKey(visibleKeys, selectedKey, direction);
    if (!nextKey) {
      return;
    }

    const nextItem = visibleItems.find(item => getKey(item) === nextKey);
    if (!nextItem) {
      return;
    }

    onSelectionChange(nextKey, nextItem);
    requestAnimationFrame(() => buttonRefs.current.get(nextKey)?.focus());
  };

  const handleItemKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    let direction: EngineeringEntityNavigationDirection | null = null;

    switch (event.key) {
      case 'ArrowDown':
        direction = 'next';
        break;
      case 'ArrowUp':
        direction = 'previous';
        break;
      case 'Home':
        direction = 'first';
        break;
      case 'End':
        direction = 'last';
        break;
      default:
        return;
    }

    event.preventDefault();
    selectAndFocus(direction);
  };

  const renderBrowserState = (title: string, description?: string) => (
    <div className="engineering-entity-browser__state" role="status">
      <strong>{title}</strong>
      {description && <span>{description}</span>}
    </div>
  );

  return (
    <section className={rootClassName} aria-busy={loading || undefined}>
      <div className="engineering-entity-browser__master">
        <div className="engineering-entity-browser__toolbar">
          <label className="engineering-entity-browser__search" htmlFor={`${browserId}-search`}>
            <span>{messages.searchLabel}</span>
            <input
              id={`${browserId}-search`}
              type="search"
              value={query}
              placeholder={messages.searchPlaceholder}
              onChange={event => setQuery(event.target.value)}
              onKeyDown={event => {
                if (event.key === 'Escape' && query) {
                  event.preventDefault();
                  setQuery('');
                }
              }}
            />
          </label>

          {filters.length > 0 && (
            <label className="engineering-entity-browser__filter" htmlFor={`${browserId}-filter`}>
              <span>{messages.filterLabel}</span>
              <select
                id={`${browserId}-filter`}
                value={activeFilter?.key ?? ''}
                onChange={event => setActiveFilterKey(event.target.value)}
              >
                <option value="">{messages.allFilterLabel}</option>
                {filters.map(filter => (
                  <option key={filter.key} value={filter.key}>{filter.label}</option>
                ))}
              </select>
            </label>
          )}
        </div>

        <div className="engineering-entity-browser__summary" aria-live="polite">
          {messages.formatResultSummary(visibleItems.length, items.length)}
        </div>

        {loading ? renderBrowserState(messages.loadingTitle, messages.loadingDescription) :
          items.length === 0 ? renderBrowserState(messages.emptyTitle, messages.emptyDescription) :
            visibleItems.length === 0 ? renderBrowserState(messages.noMatchesTitle, messages.noMatchesDescription) : (
              <div className="engineering-entity-browser__list" role="listbox" aria-label={messages.listLabel}>
                {visibleItems.map((item, index) => {
                  const key = getKey(item);
                  const selected = key === selectedKey;
                  const description = getDescription?.(item);

                  return (
                    <button
                      key={key}
                      ref={node => {
                        if (node) {
                          buttonRefs.current.set(key, node);
                        } else {
                          buttonRefs.current.delete(key);
                        }
                      }}
                      type="button"
                      role="option"
                      aria-selected={selected}
                      className={`engineering-entity-browser__item${selected ? ' is-selected' : ''}`}
                      tabIndex={index === tabbableIndex ? 0 : -1}
                      onClick={() => onSelectionChange(key, item)}
                      onKeyDown={handleItemKeyDown}
                    >
                      <span className="engineering-entity-browser__item-copy">
                        <strong title={getLabel(item)}>{getLabel(item)}</strong>
                        {description && <span title={description}>{description}</span>}
                      </span>
                      {renderItemMeta && (
                        <span className="engineering-entity-browser__item-meta">{renderItemMeta(item)}</span>
                      )}
                    </button>
                  );
                })}
              </div>
            )}
      </div>

      <div className="engineering-entity-browser__detail" aria-label={messages.detailLabel}>
        {selectedItem
          ? renderDetail(selectedItem)
          : renderBrowserState(messages.detailEmptyTitle, messages.detailEmptyDescription)}
      </div>
    </section>
  );
}
