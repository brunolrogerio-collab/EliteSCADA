import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { loadEngineeringSnapshot } from './api';
import {
  resolveInitialLocale,
  setStoredLocale,
  translator,
  type EngineeringLocale,
  type TranslationKey
} from './i18n';
import { AlarmEditor } from './AlarmEditor';
import { DataSourceEditor, TagEditor } from './StructuredEditors';
import type { EngineeringPackageView, EngineeringSnapshot } from './types';
import './engineering.css';

type SectionId =
  | 'overview'
  | 'dataSources'
  | 'tags'
  | 'alarms'
  | 'templates'
  | 'equipment'
  | 'dynamos'
  | 'screens'
  | 'popups'
  | 'historian'
  | 'security'
  | 'diagnostics';

type NavGroup = {
  label: TranslationKey;
  items: Array<{ id: SectionId; label: TranslationKey }>;
};

const navigation: NavGroup[] = [
  { label: 'nav.project', items: [{ id: 'overview', label: 'nav.overview' }] },
  {
    label: 'nav.communication',
    items: [
      { id: 'dataSources', label: 'nav.dataSources' },
      { id: 'tags', label: 'nav.tags' },
      { id: 'alarms', label: 'nav.alarms' }
    ]
  },
  {
    label: 'nav.assets',
    items: [
      { id: 'templates', label: 'nav.templates' },
      { id: 'equipment', label: 'nav.equipment' },
      { id: 'dynamos', label: 'nav.dynamos' }
    ]
  },
  {
    label: 'nav.visualization',
    items: [
      { id: 'screens', label: 'nav.screens' },
      { id: 'popups', label: 'nav.popups' }
    ]
  },
  {
    label: 'nav.historian',
    items: [{ id: 'historian', label: 'nav.historian' }]
  },
  {
    label: 'nav.security',
    items: [{ id: 'security', label: 'nav.security' }]
  },
  {
    label: 'nav.diagnostics',
    items: [{ id: 'diagnostics', label: 'nav.diagnostics' }]
  }
];

export function EngineeringApp() {
  const [locale, setLocale] = useState<EngineeringLocale>(() => resolveInitialLocale());
  const [section, setSection] = useState<SectionId>('overview');
  const [snapshot, setSnapshot] = useState<EngineeringSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const t = useMemo(() => translator(locale), [locale]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSnapshot(await loadEngineeringSnapshot());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const changeLocale = (next: EngineeringLocale) => {
    setLocale(next);
    setStoredLocale(next);
    document.documentElement.lang = next;
  };

  return (
    <main className="eng-shell">
      <header className="eng-topbar">
        <div className="eng-brand">
          <div className="eng-mark" aria-hidden="true">E</div>
          <div>
            <strong>{t('app.title')}</strong>
            <span>{t('app.subtitle')}</span>
          </div>
        </div>

        <div className="eng-top-actions">
          <a className="eng-runtime-link" href="/">{t('app.runtime')}</a>
          <div className="eng-locale">
            <label htmlFor="engineering-locale">{t('locale.label')}</label>
            <select
              id="engineering-locale"
              aria-label={t('locale.label')}
              value={locale}
              onChange={event => changeLocale(event.target.value as EngineeringLocale)}
            >
              <option value="pt-BR">{t('locale.pt-BR')}</option>
              <option value="en">{t('locale.en')}</option>
              <option value="es">{t('locale.es')}</option>
            </select>
          </div>
        </div>
      </header>

      <div className="eng-body">
        <aside className="eng-sidebar" aria-label={t('app.engineering')}>
          <div className="eng-project-chip">
            <span>{t('workspace.project')}</span>
            <strong>{snapshot?.workspace.projectName ?? snapshot?.workspace.projectKey ?? 'Demo Project'}</strong>
          </div>

          <nav className="eng-nav">
            {navigation.map(group => (
              <div className="eng-nav-group" key={group.label}>
                <span className="eng-nav-label">{t(group.label)}</span>
                {group.items.map(item => (
                  <button
                    key={item.id}
                    type="button"
                    className={section === item.id ? 'active' : ''}
                    onClick={() => setSection(item.id)}
                  >
                    <NavIcon section={item.id} />
                    <span>{t(item.label)}</span>
                    {snapshot && <small>{sectionCount(snapshot.package, item.id)}</small>}
                  </button>
                ))}
              </div>
            ))}
          </nav>
        </aside>

        <section className="eng-workspace">
          <WorkspaceBar snapshot={snapshot} t={t} locale={locale} />

          {loading && (
            <div className="eng-state-card">
              <div className="eng-spinner" />
              <strong>{t('app.loading')}</strong>
            </div>
          )}

          {!loading && error && (
            <div className="eng-state-card error">
              <strong>{t('app.loadError')}</strong>
              <span>{error}</span>
              <button type="button" onClick={() => void load()}>{t('app.retry')}</button>
            </div>
          )}

          {!loading && snapshot && (
            <EngineeringSection section={section} snapshot={snapshot} t={t} locale={locale} />
          )}
        </section>
      </div>
    </main>
  );
}

function WorkspaceBar({
  snapshot,
  t,
  locale
}: {
  snapshot: EngineeringSnapshot | null;
  t: ReturnType<typeof translator>;
  locale: EngineeringLocale;
}) {
  const workspace = snapshot?.workspace;
  const engineeringPackage = snapshot?.package;
  return (
    <div className="eng-workspace-bar">
      <div>
        <span>{t('workspace.schema')}</span>
        <strong>{engineeringPackage ? `${engineeringPackage.schema} v${engineeringPackage.schemaVersion}` : '—'}</strong>
      </div>
      <div>
        <span>{t('workspace.revision')}</span>
        <strong>{workspace?.baseRevision ?? t('workspace.unsaved')}</strong>
      </div>
      <div>
        <span>{t('workspace.status')}</span>
        <strong className={workspace?.isDirty ? 'eng-dirty' : ''}>
          {workspace?.isDirty ? t('workspace.dirty') : t('workspace.clean')}
        </strong>
      </div>
      <div>
        <span>{t('workspace.exportedAt')}</span>
        <strong>{engineeringPackage?.exportedAt ? formatDate(engineeringPackage.exportedAt, locale) : '—'}</strong>
      </div>
    </div>
  );
}

function EngineeringSection({
  section,
  snapshot,
  t,
  locale
}: {
  section: SectionId;
  snapshot: EngineeringSnapshot;
  t: ReturnType<typeof translator>;
  locale: EngineeringLocale;
}) {
  const model = snapshot.package;

  if (section === 'overview') return <Overview snapshot={snapshot} t={t} />;
  if (section === 'historian') return <HistorianSection model={model} t={t} />;
  if (section === 'security') return <SecuritySection model={model} t={t} />;
  if (section === 'diagnostics') return <DiagnosticsSection model={model} t={t} locale={locale} />;

  switch (section) {
    case 'dataSources':
      return <DataSourceEditor model={model} locale={locale} />;
    case 'tags':
      return <TagEditor model={model} locale={locale} />;
    case 'alarms':
      return <AlarmEditor model={model} locale={locale} />;
    case 'templates':
      return <EntitySection
        title={t('nav.templates')}
        items={model.templates ?? []}
        t={t}
        columns={[
          { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
          { key: 'name', title: t('table.name'), render: item => item.name },
          { key: 'bindings', title: t('table.bindings'), render: item => item.bindings?.length ?? 0 }
        ]}
      />;
    case 'equipment':
      return <EntitySection
        title={t('nav.equipment')}
        items={model.equipment ?? []}
        t={t}
        columns={[
          { key: 'path', title: t('table.path'), render: item => <Code>{item.path}</Code> },
          { key: 'name', title: t('table.name'), render: item => item.name },
          { key: 'template', title: t('table.template'), render: item => item.templateKey ? <Code>{item.templateKey}</Code> : '—' },
          { key: 'bindings', title: t('table.bindings'), render: item => item.bindings?.length ?? 0 }
        ]}
      />;
    case 'dynamos':
      return <EntitySection
        title={t('nav.dynamos')}
        items={model.dynamos ?? []}
        t={t}
        columns={[
          { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
          { key: 'name', title: t('table.name'), render: item => item.name },
          { key: 'template', title: t('table.template'), render: item => item.templateKey ? <Code>{item.templateKey}</Code> : '—' },
          { key: 'bindings', title: t('table.bindings'), render: item => item.bindings?.length ?? 0 }
        ]}
      />;
    case 'screens':
      return <EntitySection
        title={t('nav.screens')}
        items={model.screens ?? []}
        t={t}
        columns={[
          { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
          { key: 'name', title: t('table.name'), render: item => item.name },
          { key: 'route', title: t('table.route'), render: item => item.route ? <Code>{item.route}</Code> : '—' },
          { key: 'elements', title: t('section.count'), render: item => item.elements?.length ?? 0 }
        ]}
      />;
    case 'popups':
      return <EntitySection
        title={t('nav.popups')}
        items={model.popups ?? []}
        t={t}
        columns={[
          { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
          { key: 'name', title: t('table.name'), render: item => item.name },
          { key: 'template', title: t('table.template'), render: item => item.templateKey ? <Code>{item.templateKey}</Code> : '—' },
          { key: 'elements', title: t('section.count'), render: item => item.elements?.length ?? 0 }
        ]}
      />;
  }
}

function Overview({ snapshot, t }: { snapshot: EngineeringSnapshot; t: ReturnType<typeof translator> }) {
  const model = snapshot.package;
  const entities: Array<{ label: TranslationKey; value: number; section: SectionId }> = [
    { label: 'entity.tags', value: model.tags.length, section: 'tags' },
    { label: 'entity.alarms', value: model.alarms.length, section: 'alarms' },
    { label: 'entity.dataSources', value: model.dataSources?.length ?? 0, section: 'dataSources' },
    { label: 'entity.templates', value: model.templates?.length ?? 0, section: 'templates' },
    { label: 'entity.equipment', value: model.equipment?.length ?? 0, section: 'equipment' },
    { label: 'entity.dynamos', value: model.dynamos?.length ?? 0, section: 'dynamos' },
    { label: 'entity.screens', value: model.screens?.length ?? 0, section: 'screens' },
    { label: 'entity.popups', value: model.popups?.length ?? 0, section: 'popups' },
    { label: 'entity.securityRoles', value: model.securityRoles?.length ?? 0, section: 'security' }
  ];

  return (
    <div className="eng-section">
      <SectionHeader title={t('overview.title')} description={t('overview.description')} t={t} />
      <div className="eng-overview-grid">
        <section className="eng-panel eng-entity-panel">
          <h2>{t('overview.entities')}</h2>
          <div className="eng-entity-grid">
            {entities.map(entity => (
              <div className="eng-entity-card" key={entity.section}>
                <strong>{entity.value}</strong>
                <span>{t(entity.label)}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="eng-panel">
          <h2>{t('overview.lifecycle')}</h2>
          <div className="eng-lifecycle" aria-label={t('overview.lifecycle')}>
            {['Working', 'Revision', 'Published', 'Active'].map((stage, index) => (
              <React.Fragment key={stage}>
                <span>{stage}</span>
                {index < 3 && <b>→</b>}
              </React.Fragment>
            ))}
          </div>
          <p>{t('overview.lifecycleHint')}</p>
        </section>

        <section className="eng-panel eng-wide-panel">
          <h2>{t('overview.next')}</h2>
          <p>{t('overview.nextHint')}</p>
          <div className="eng-flow">
            <span>parse</span><b>→</b><span>validate</span><b>→</b><span>preview</span><b>→</b><span>apply</span>
          </div>
        </section>
      </div>
    </div>
  );
}

function HistorianSection({ model, t }: { model: EngineeringPackageView; t: ReturnType<typeof translator> }) {
  const tags = model.tags.filter(tag => tag.historian?.enabled);
  return (
    <EntitySection
      title={t('historian.title')}
      description={t('historian.description')}
      items={tags}
      t={t}
      columns={[
        { key: 'path', title: t('table.path'), render: item => <Code>{item.path}</Code> },
        { key: 'strategy', title: t('table.type'), render: item => item.historian?.strategy ?? '—' },
        { key: 'deadband', title: 'Deadband', render: item => item.historian?.deadband ?? '—' },
        { key: 'period', title: 'Period (ms)', render: item => item.historian?.periodMilliseconds ?? '—' }
      ]}
    />
  );
}

function SecuritySection({ model, t }: { model: EngineeringPackageView; t: ReturnType<typeof translator> }) {
  return (
    <EntitySection
      title={t('security.title')}
      description={t('security.description')}
      items={model.securityRoles ?? []}
      t={t}
      columns={[
        { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
        { key: 'name', title: t('table.name'), render: item => item.name },
        {
          key: 'grants',
          title: t('table.grants'),
          render: item => <span className="eng-capability-list">{item.grants?.map(grant => grant.capability).join(', ') || '—'}</span>
        }
      ]}
    />
  );
}

function DiagnosticsSection({
  model,
  t,
  locale
}: {
  model: EngineeringPackageView;
  t: ReturnType<typeof translator>;
  locale: EngineeringLocale;
}) {
  const total = model.tags.length +
    model.alarms.length +
    (model.dataSources?.length ?? 0) +
    (model.templates?.length ?? 0) +
    (model.equipment?.length ?? 0) +
    (model.dynamos?.length ?? 0) +
    (model.screens?.length ?? 0) +
    (model.popups?.length ?? 0) +
    (model.securityRoles?.length ?? 0);

  return (
    <div className="eng-section">
      <SectionHeader title={t('diagnostics.title')} description={t('diagnostics.description')} t={t} />
      <div className="eng-diagnostic-grid">
        <Diagnostic label={t('diagnostics.contract')} value={model.schema} mono />
        <Diagnostic label={t('diagnostics.schemaVersion')} value={String(model.schemaVersion)} />
        <Diagnostic label={t('diagnostics.exportedAt')} value={formatDate(model.exportedAt, locale)} />
        <Diagnostic label={t('diagnostics.totalEntities')} value={String(total)} />
      </div>
    </div>
  );
}

type TableColumn<T> = {
  key: string;
  title: string;
  render: (item: T) => React.ReactNode;
};

function EntitySection<T>({
  title,
  description,
  items,
  columns,
  t
}: {
  title: string;
  description?: string;
  items: T[];
  columns: Array<TableColumn<T>>;
  t: ReturnType<typeof translator>;
}) {
  return (
    <div className="eng-section">
      <SectionHeader title={title} description={description} count={items.length} t={t} />
      <section className="eng-panel eng-table-panel">
        {items.length === 0 ? (
          <div className="eng-empty">
            <strong>{t('section.empty')}</strong>
            <span>{t('section.future')}</span>
          </div>
        ) : (
          <div className="eng-table-wrap">
            <table className="eng-table">
              <thead>
                <tr>{columns.map(column => <th key={column.key}>{column.title}</th>)}</tr>
              </thead>
              <tbody>
                {items.map((item, index) => (
                  <tr key={index}>
                    {columns.map(column => <td key={column.key}>{column.render(item)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

function SectionHeader({
  title,
  description,
  count,
  t
}: {
  title: string;
  description?: string;
  count?: number;
  t: ReturnType<typeof translator>;
}) {
  return (
    <header className="eng-section-header">
      <div>
        <span className="eng-eyebrow">{t('section.readOnly')}</span>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      <div className="eng-section-meta">
        {count !== undefined && <strong>{count} {t('section.count')}</strong>}
        <span>{t('app.readOnly')}</span>
      </div>
    </header>
  );
}

function Diagnostic({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="eng-diagnostic-card">
      <span>{label}</span>
      <strong className={mono ? 'mono' : ''}>{value}</strong>
    </div>
  );
}

function Code({ children }: { children: React.ReactNode }) {
  return <code className="eng-code">{children}</code>;
}

function yesNo(value: boolean, t: ReturnType<typeof translator>) {
  return <span className={`eng-bool ${value ? 'yes' : 'no'}`}>{value ? t('common.yes') : t('common.no')}</span>;
}

function sectionCount(model: EngineeringPackageView, section: SectionId): number | string {
  switch (section) {
    case 'dataSources': return model.dataSources?.length ?? 0;
    case 'tags': return model.tags.length;
    case 'alarms': return model.alarms.length;
    case 'templates': return model.templates?.length ?? 0;
    case 'equipment': return model.equipment?.length ?? 0;
    case 'dynamos': return model.dynamos?.length ?? 0;
    case 'screens': return model.screens?.length ?? 0;
    case 'popups': return model.popups?.length ?? 0;
    case 'historian': return model.tags.filter(tag => tag.historian?.enabled).length;
    case 'security': return model.securityRoles?.length ?? 0;
    case 'overview':
    case 'diagnostics': return '•';
  }
}

function formatDate(value: string, locale: EngineeringLocale) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'medium'
  }).format(date);
}

function NavIcon({ section }: { section: SectionId }) {
  const symbols: Record<SectionId, string> = {
    overview: '⌂',
    dataSources: '⇄',
    tags: '#',
    alarms: '!',
    templates: '◇',
    equipment: '□',
    dynamos: '◈',
    screens: '▣',
    popups: '▤',
    historian: '⌁',
    security: '◆',
    diagnostics: '⋯'
  };
  return <i aria-hidden="true">{symbols[section]}</i>;
}
