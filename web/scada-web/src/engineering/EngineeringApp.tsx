import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { getProductIdentity, type ProductIdentityView } from '../productInfoApi';
import { loadEngineeringSnapshot } from './api';
import {
  resolveInitialLocale,
  setStoredLocale,
  translator,
  type EngineeringLocale,
  type TranslationKey
} from './i18n';
import { AlarmEditor } from './AlarmEditor';
import { BrandingEngineeringWorkspace } from './BrandingEngineeringWorkspace';
import { CommunicationDiagnosticsPanel } from './CommunicationDiagnosticsPanel';
import { DevelopmentMonitorWorkspace } from './development-monitor/DevelopmentMonitorWorkspace';
import { EngineeringTagMonitorWorkspace } from './diagnostics/EngineeringTagMonitorWorkspace';
import { EngineeringLifecycleWorkspace } from './EngineeringLifecycleWorkspace';
import { EngineeringProjectManagementWorkspace } from './EngineeringProjectManagementWorkspace';
import { InstallationSwitchingWorkspace } from './InstallationSwitchingWorkspace';
import { MediaSourceEngineeringWorkspace } from './MediaSourceEngineeringWorkspace';
import { MobileRuntimeEngineeringWorkspace } from './MobileRuntimeEngineeringWorkspace';
import { OperationalEventEditor, operationalEventCount } from './OperationalEventEditor';
import { ReportDesignerWorkspace } from './reports/ReportDesignerWorkspace';
import { reportCollection } from './reports/reportDesignerModel';
import { ReusableLibraryWorkspace } from './ReusableLibraryWorkspace';
import { ScriptEngineeringWorkspace } from './scripts/ScriptEngineeringWorkspace';
import { DataSourceEditor, TagEditor } from './StructuredEditors';
import { GatewayEngineeringPanel } from './GatewayEngineeringPanel';
import { HighAvailabilityAdminWorkspace } from './ha/HighAvailabilityAdminWorkspace';
import { UserAdministration } from './UserAdministration';
import { PopupVisualEditorWorkspace } from './visual-editor/PopupVisualEditorWorkspace';
import { VisualEditorWorkspace } from './visual-editor/VisualEditorWorkspace';
import { VisualAssetManagementWorkspace } from './VisualAssetManagementWorkspace';
import { EquipmentFaceplateWorkspace } from './EquipmentFaceplateWorkspace';
import type { DynamoEngineering, EngineeringPackageView, EngineeringSnapshot, EquipmentEngineering, TemplateEngineering } from './types';
import { CanonicalVisualPreview } from './visual-editor/CanonicalVisualPreview';
import { selectDefaultDynamoCatalog } from './visual-editor/dynamoLibraryModel';
import { listUserVisualAssets } from './visualAssetCatalogModel';
import { hasRuntimeCapability, useEffectiveCapabilities } from '../auth/effectiveCapabilities';
import './engineering.css';
import './object-catalog.css';

type SectionId =
  | 'overview'
  | 'installation'
  | 'branding'
  | 'mobile'
  | 'scripts'
  | 'libraries'
  | 'dataSources'
  | 'mediaSources'
  | 'gateway'
  | 'tags'
  | 'alarms'
  | 'operationalEvents'
  | 'templates'
  | 'equipment'
  | 'dynamos'
  | 'visualAssets'
  | 'screens'
  | 'popups'
  | 'historian'
  | 'reports'
  | 'security'
  | 'highAvailability'
  | 'databaseTopology'
  | 'monitor'
  | 'tagMonitor'
  | 'diagnostics'
  | 'information';

type NavItem = { id: SectionId; label?: TranslationKey; literalLabel?: Record<EngineeringLocale, string>; href?: string };
type NavGroup = { label: TranslationKey; items: NavItem[] };

const tagMonitorPath = '/engineering/diagnostics/tag-monitor';
const librariesPath = '/engineering/libraries';

const navigation: NavGroup[] = [
  { label: 'nav.project', items: [
    { id: 'overview', label: 'nav.overview' },
    { id: 'installation', literalLabel: { 'pt-BR': 'Instalação', en: 'Installation', es: 'Instalación' } },
    { id: 'branding', literalLabel: { 'pt-BR': 'Cabeçalho', en: 'Header', es: 'Encabezado' } },
    { id: 'mobile', literalLabel: { 'pt-BR': 'Mobile', en: 'Mobile', es: 'Móvil' } },
    { id: 'scripts' },
    { id: 'libraries', literalLabel: { 'pt-BR': 'Bibliotecas', en: 'Libraries', es: 'Bibliotecas' } }
  ] },
  { label: 'nav.communication', items: [
    { id: 'dataSources', label: 'nav.dataSources' },
    { id: 'mediaSources', literalLabel: { 'pt-BR': 'Fontes de mídia', en: 'Media sources', es: 'Fuentes multimedia' } },
    { id: 'tags', label: 'nav.tags' },
    { id: 'gateway', literalLabel: { 'pt-BR': 'TAG Gateway', en: 'TAG Gateway', es: 'TAG Gateway' } },
    { id: 'alarms', label: 'nav.alarms' },
    { id: 'operationalEvents', literalLabel: { 'pt-BR': 'Eventos Operacionais', en: 'Operational Events', es: 'Eventos Operacionales' } }
  ] },
  { label: 'nav.assets', items: [{ id: 'templates', label: 'nav.templates' }, { id: 'equipment', label: 'nav.equipment' }, { id: 'dynamos', label: 'nav.dynamos' }, { id: 'visualAssets', literalLabel: { 'pt-BR': 'Assets visuais', en: 'Visual assets', es: 'Assets visuales' } }] },
  { label: 'nav.visualization', items: [{ id: 'screens', label: 'nav.screens' }, { id: 'popups', label: 'nav.popups' }] },
  { label: 'nav.historian', items: [
    { id: 'historian', label: 'nav.historian' },
    { id: 'reports', literalLabel: { 'pt-BR': 'Relatórios', en: 'Reports', es: 'Informes' } }
  ] },
  { label: 'nav.security', items: [
    { id: 'security', label: 'nav.security' },
    { id: 'highAvailability', literalLabel: { 'pt-BR': 'Alta disponibilidade', en: 'High Availability', es: 'Alta disponibilidad' } },
    { id: 'databaseTopology', literalLabel: { 'pt-BR': 'Banco de dados', en: 'Database', es: 'Base de datos' }, href: '/engineering/database-topology' }
  ] },
  { label: 'nav.diagnostics', items: [
    { id: 'monitor', literalLabel: { 'pt-BR': 'Monitoramento', en: 'Development Monitor', es: 'Monitor de Desarrollo' } },
    { id: 'tagMonitor', literalLabel: { 'pt-BR': 'TAG Monitor', en: 'TAG Monitor', es: 'TAG Monitor' } },
    { id: 'diagnostics', label: 'nav.diagnostics' },
    { id: 'information', literalLabel: { 'pt-BR': 'Informações', en: 'Information', es: 'Información' } }
  ] }
];

export function EngineeringApp({ engineeringLockControl }: { engineeringLockControl?: React.ReactNode } = {}) {
  const [locale, setLocale] = useState<EngineeringLocale>(() => resolveInitialLocale());
  const [section, setSection] = useState<SectionId>(() => resolveInitialSection());
  const [snapshot, setSnapshot] = useState<EngineeringSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [navigationCollapsed, setNavigationCollapsed] = useState(false);
  const [productIdentity, setProductIdentity] = useState<ProductIdentityView | null>(null);
  const t = useMemo(() => translator(locale), [locale]);
  const { capabilities } = useEffectiveCapabilities();
  const canAdministerDatabase = hasRuntimeCapability(capabilities, 'SystemAdmin');
  const projectIdentity = snapshot?.workspace.projectName ?? snapshot?.workspace.projectKey
    ?? (loading ? t('workspace.loading') : t('workspace.unavailable'));

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    setSnapshot(null);
    try {
      setSnapshot(await loadEngineeringSnapshot());
    } catch (reason) {
      setSnapshot(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
    }
  }, []);

  const refreshSnapshotInPlace = useCallback(async () => {
    setSnapshot(await loadEngineeringSnapshot());
  }, []);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    const controller = new AbortController();
    void getProductIdentity(controller.signal)
      .then(identity => {
        if (!controller.signal.aborted) setProductIdentity(identity);
      })
      .catch(() => {
        if (!controller.signal.aborted) setProductIdentity(null);
      });
    return () => controller.abort();
  }, []);

  const changeLocale = (next: EngineeringLocale) => {
    setLocale(next);
    setStoredLocale(next);
    document.documentElement.lang = next;
  };

  const selectSection = (next: SectionId) => {
    setSection(next);
    const nextPath = engineeringSectionPath(next);
    if (window.location.pathname !== nextPath) window.history.replaceState(null, '', nextPath);
  };

  return (
    <main className="eng-shell">
      <header className="eng-topbar">
        <div className="eng-brand">
          <div className="eng-mark" aria-hidden="true">E</div>
          <strong className="eng-brand__title">{t('app.title')}</strong>
        </div>
        <div className="eng-context" data-testid="engineering-context-row">
          <strong className="eng-context__task">{currentSectionLabel(section, locale, t)}</strong>
          <span
            className="eng-context__project"
            data-testid="engineering-project-identity"
            title={projectIdentity}
          >
            {projectIdentity}
          </span>
          <span
            className={snapshot?.workspace.isDirty ? 'eng-context__workspace eng-context__workspace--dirty' : 'eng-context__workspace'}
            data-testid="engineering-workspace-state"
          >
            <span>{t('workspace.status')}</span>
            <strong>{loading ? t('workspace.loading') : snapshot ? (snapshot.workspace.isDirty ? t('workspace.dirty') : t('workspace.clean')) : t('workspace.unavailable')}</strong>
          </span>
        </div>
        <div className="eng-top-actions">
          {engineeringLockControl}
          <a className="eng-runtime-link" href="/">{t('app.runtime')}</a>
          <div className="eng-locale">
            <label htmlFor="engineering-locale">{t('locale.label')}</label>
            <select id="engineering-locale" aria-label={t('locale.label')} value={locale} onChange={event => changeLocale(event.target.value as EngineeringLocale)}>
              <option value="pt-BR">{t('locale.pt-BR')}</option>
              <option value="en">{t('locale.en')}</option>
              <option value="es">{t('locale.es')}</option>
            </select>
          </div>
        </div>
      </header>

      <div className={navigationCollapsed ? 'eng-body eng-body--navigation-collapsed' : 'eng-body'}>
        <aside className="eng-sidebar" aria-label={t('app.engineering')}>
          {(section === 'screens' || section === 'popups') ? <button
            type="button"
            className="eng-sidebar__toggle"
            data-testid="engineering-navigation-toggle"
            aria-expanded={!navigationCollapsed}
            aria-label={navigationCollapsed ? editorNavigationLabel(locale, true) : editorNavigationLabel(locale, false)}
            title={navigationCollapsed ? editorNavigationLabel(locale, true) : editorNavigationLabel(locale, false)}
            onClick={() => setNavigationCollapsed(value => !value)}
          ><span aria-hidden="true">{navigationCollapsed ? '›' : '‹'}</span></button> : null}
          <nav className="eng-nav">
            {navigation.map(group => (
              <div className="eng-nav-group" key={group.label}>
                <span className="eng-nav-label">{t(group.label)}</span>
                {group.items
                  .filter(item => item.id !== 'tagMonitor' || snapshot !== null)
                  .filter(item => item.id !== 'databaseTopology' || canAdministerDatabase)
                  .map(item => (
                    item.href ? <a
                      key={item.id}
                      href={item.href}
                      className={window.location.pathname.startsWith(item.href) ? 'active' : ''}
                      aria-current={window.location.pathname.startsWith(item.href) ? 'page' : undefined}
                    >
                      <NavIcon section={item.id}/>
                      <span>{item.literalLabel ? item.literalLabel[locale] : item.label ? t(item.label) : scriptNavLabel(locale)}</span>
                    </a> : <button
                      key={item.id}
                      type="button"
                      className={section === item.id ? 'active' : ''}
                      onClick={() => selectSection(item.id)}
                      disabled={!snapshot}
                    >
                      <NavIcon section={item.id}/>
                      <span>{item.literalLabel ? item.literalLabel[locale] : item.label ? t(item.label) : scriptNavLabel(locale)}</span>
                      {snapshot && <small>{sectionCount(snapshot.package, item.id)}</small>}
                    </button>
                  ))}
              </div>
            ))}
          </nav>
        </aside>

        <section
          className={section === 'screens' || section === 'popups' || section === 'dynamos' ? 'eng-workspace eng-workspace--wide-section' : 'eng-workspace'}
          data-section-layout={section === 'screens' || section === 'popups' || section === 'dynamos' ? 'wide' : 'readable'}
        >
          {loading && <div className="eng-state-card"><div className="eng-spinner"/><strong>{t('app.loading')}</strong></div>}
          {!loading && error && <div className="eng-state-card error" role="alert" data-testid="engineering-load-error"><strong>{t('app.loadError')}</strong><span>{error}</span><button type="button" onClick={() => void load()}>{t('app.retry')}</button></div>}
          {!loading && snapshot && <EngineeringSection section={section} snapshot={snapshot} productIdentity={productIdentity} t={t} locale={locale} onReload={load} onSnapshotRefreshed={refreshSnapshotInPlace}/>}
        </section>
      </div>
    </main>
  );
}

function editorNavigationLabel(locale: EngineeringLocale, collapsed: boolean): string {
  if (locale === 'en') return collapsed ? 'Show Engineering navigation' : 'Hide Engineering navigation';
  if (locale === 'es') return collapsed ? 'Mostrar navegación de Engineering' : 'Ocultar navegación de Engineering';
  return collapsed ? 'Mostrar navegação do Engineering' : 'Ocultar navegação do Engineering';
}

function EngineeringSection({ section, snapshot, productIdentity, t, locale, onReload, onSnapshotRefreshed }: {
  section: SectionId;
  snapshot: EngineeringSnapshot;
  productIdentity: ProductIdentityView | null;
  t: ReturnType<typeof translator>;
  locale: EngineeringLocale;
  onReload: () => Promise<void>;
  onSnapshotRefreshed: () => Promise<void>;
}) {
  const model = snapshot.package;
  if (section === 'overview') return <>
    <Overview snapshot={snapshot} t={t}/>
    <EngineeringLifecycleWorkspace locale={locale}/>
    <details className="eng-overview-advanced">
      <summary>{locale === 'pt-BR' ? 'Ferramentas avançadas do projeto' : locale === 'es' ? 'Herramientas avanzadas del proyecto' : 'Advanced project tools'}</summary>
      <EngineeringProjectManagementWorkspace locale={locale}/>
    </details>
  </>;
  if (section === 'installation') return <InstallationSwitchingWorkspace locale={locale} onWorkspaceChanged={onReload}/>;
  if (section === 'branding') return <BrandingEngineeringWorkspace snapshot={snapshot} onApplied={onReload} locale={locale}/>;
  if (section === 'mobile') return <MobileRuntimeEngineeringWorkspace snapshot={snapshot} onApplied={onReload} locale={locale}/>;
  if (section === 'mediaSources') return <MediaSourceEngineeringWorkspace snapshot={snapshot} onApplied={onReload} locale={locale}/>;
  if (section === 'visualAssets') return <VisualAssetManagementWorkspace snapshot={snapshot} locale={locale} onApplied={onReload}/>;
  if (section === 'scripts') return <ScriptEngineeringWorkspace locale={locale}/>;
  if (section === 'libraries') return <ReusableLibraryWorkspace locale={locale} snapshot={snapshot} onReload={onReload}/>;
  if (section === 'historian') return <HistorianSection model={model} t={t}/>;
  if (section === 'reports') return <ReportDesignerWorkspace snapshot={snapshot} locale={locale} onApplied={onReload}/>;
  if (section === 'security') return <SecuritySection model={model} t={t} locale={locale}/>;
  if (section === 'highAvailability') return <HighAvailabilityAdminWorkspace locale={locale}/>;
  if (section === 'monitor') return <DevelopmentMonitorWorkspace snapshot={snapshot} locale={locale}/>;
  if (section === 'tagMonitor') return <EngineeringTagMonitorWorkspace snapshot={snapshot} locale={locale}/>;
  if (section === 'diagnostics') return <DiagnosticsSection model={model} t={t} locale={locale}/>;
  if (section === 'information') return <EngineeringInformation snapshot={snapshot} productIdentity={productIdentity} t={t} locale={locale}/>;

  switch (section) {
    case 'dataSources': return <DataSourceEditor model={model} locale={locale} projectKey={snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace'}/>;
    case 'gateway': return <GatewayEngineeringPanel model={model} locale={locale} projectKey={snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace'}/>;
    case 'tags': return <TagEditor model={model} locale={locale} projectKey={snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace'}/>;
    case 'alarms': return <AlarmEditor model={model} locale={locale} projectKey={snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace'}/>;
    case 'operationalEvents': return <OperationalEventEditor model={model} locale={locale} projectKey={snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace'} onApplied={onReload}/>;
    case 'templates': return <VisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onReload} onAssetImported={onSnapshotRefreshed} definitionKind="template"/>;
    case 'equipment': return <EquipmentFaceplateWorkspace snapshot={snapshot} locale={locale} onApplied={onReload}/>;
    case 'dynamos': return <DynamoCatalogSection items={model.dynamos ?? []} snapshot={snapshot} locale={locale} onApplied={onReload} onSnapshotRefreshed={onSnapshotRefreshed}/>;
    case 'screens': return <VisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onReload} onAssetImported={onSnapshotRefreshed}/>;
    case 'popups': return <PopupVisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onReload} onAssetImported={onSnapshotRefreshed}/>;
    default: return null;
  }
}

function EngineeringInformation({ snapshot, productIdentity, t, locale }: {
  snapshot: EngineeringSnapshot;
  productIdentity: ProductIdentityView | null;
  t: ReturnType<typeof translator>;
  locale: EngineeringLocale;
}) {
  const copy = informationCopy(locale);
  const project = snapshot.workspace.projectName ?? snapshot.workspace.projectKey ?? t('workspace.unavailable');
  const baseRevision = snapshot.workspace.baseRevision === null || snapshot.workspace.baseRevision === undefined
    ? t('workspace.unsaved')
    : String(snapshot.workspace.baseRevision);

  return (
    <div className="eng-section eng-information" data-testid="engineering-information">
      <SectionHeader title={copy.title} description={copy.description} t={t}/>
      <section className="eng-panel eng-information__product" aria-label={copy.product}>
        <span>{copy.product}</span>
        <strong data-testid="engineering-product-version">{productIdentity?.displayVersion ?? copy.productUnavailable}</strong>
      </section>
      <details className="eng-panel eng-information__technical">
        <summary>{copy.technicalDetails}</summary>
        <div className="eng-diagnostic-grid">
          <div className="eng-diagnostic-card eng-information__author-credit"><strong>SISTEMA DESENVOLVIDO POR BRUNO LUIZ ROGERIO</strong></div>
          <Diagnostic label={copy.schema} value={`${snapshot.package.schema} v${snapshot.package.schemaVersion}`} mono/>
          <Diagnostic label={copy.baseRevision} value={baseRevision}/>
          <Diagnostic label={copy.snapshot} value={formatDate(snapshot.package.exportedAt, locale)}/>
          <Diagnostic label={copy.project} value={project}/>
          {productIdentity?.buildCommit ? <Diagnostic label={copy.buildCommit} value={productIdentity.buildCommit} mono/> : null}
        </div>
      </details>
    </div>
  );
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
      <SectionHeader title={t('overview.title')} description={t('overview.description')} t={t}/>
      <div className="eng-overview-grid">
        <section className="eng-panel eng-entity-panel">
          <h2>{t('overview.entities')}</h2>
          <div className="eng-entity-grid">{entities.map(entity => <div className="eng-entity-card" key={entity.section}><strong>{entity.value}</strong><span>{t(entity.label)}</span></div>)}</div>
        </section>
      </div>
    </div>
  );
}

function HistorianSection({ model, t }: { model: EngineeringPackageView; t: ReturnType<typeof translator> }) {
  const tags = model.tags.filter(tag => tag.historian?.enabled);
  return <EntitySection title={t('historian.title')} description={t('historian.description')} items={tags} t={t} columns={[
    { key: 'path', title: t('table.path'), render: item => <Code>{item.path}</Code> },
    { key: 'strategy', title: t('table.type'), render: item => item.historian?.strategy ?? '—' },
    { key: 'deadband', title: 'Deadband', render: item => item.historian?.deadband ?? '—' },
    { key: 'period', title: 'Period (ms)', render: item => item.historian?.periodMilliseconds ?? '—' }
  ]}/>;
}

function SecuritySection({ model, t, locale }: { model: EngineeringPackageView; t: ReturnType<typeof translator>; locale: EngineeringLocale }) {
  return <><EntitySection title={t('security.title')} description={t('security.description')} items={model.securityRoles ?? []} t={t} columns={[
    { key: 'key', title: t('table.key'), render: item => <Code>{item.key}</Code> },
    { key: 'name', title: t('table.name'), render: item => item.name },
    { key: 'grants', title: t('table.grants'), render: item => <span className="eng-capability-list">{item.grants?.map(grant => grant.capability).join(', ') || '—'}</span> }
  ]}/><UserAdministration locale={locale}/></>;
}

function DiagnosticsSection({ model, t, locale }: { model: EngineeringPackageView; t: ReturnType<typeof translator>; locale: EngineeringLocale }) {
  const total = model.tags.length + model.alarms.length + operationalEventCount(model) + (model.dataSources?.length ?? 0) + (model.templates?.length ?? 0) + (model.equipment?.length ?? 0) + (model.dynamos?.length ?? 0) + (model.screens?.length ?? 0) + (model.popups?.length ?? 0) + (model.securityRoles?.length ?? 0);
  return (
    <div className="eng-section">
      <SectionHeader title={t('diagnostics.title')} description={t('diagnostics.description')} t={t}/>
      <div className="eng-diagnostic-grid">
        <Diagnostic label={t('diagnostics.contract')} value={model.schema} mono/>
        <Diagnostic label={t('diagnostics.schemaVersion')} value={String(model.schemaVersion)}/>
        <Diagnostic label={t('diagnostics.exportedAt')} value={formatDate(model.exportedAt, locale)}/>
        <Diagnostic label={t('diagnostics.totalEntities')} value={String(total)}/>
      </div>
      <CommunicationDiagnosticsPanel locale={locale}/>
    </div>
  );
}

type TableColumn<T> = { key: string; title: string; render: (item: T) => React.ReactNode };
function EntitySection<T>({ title, description, items, columns, t, emptyHint, actionHref, actionText }: { title: string; description?: string; items: T[]; columns: Array<TableColumn<T>>; t: ReturnType<typeof translator>; emptyHint?: string; actionHref?: string; actionText?: string }) {
  return (
    <div className="eng-section">
      <SectionHeader title={title} description={description} count={items.length} t={t}/>
      <section className="eng-panel eng-table-panel">
        {items.length === 0 ? (
          <div className="eng-empty"><strong>{t('section.empty')}</strong><span>{emptyHint ?? t('section.future')}</span>{actionHref && actionText ? <a className="secondary" href={actionHref}>{actionText}</a> : null}</div>
        ) : (
          <div className="eng-table-wrap"><table className="eng-table"><thead><tr>{columns.map(column => <th key={column.key}>{column.title}</th>)}</tr></thead><tbody>{items.map((item, index) => <tr key={index}>{columns.map(column => <td key={column.key}>{column.render(item)}</td>)}</tr>)}</tbody></table></div>
        )}
      </section>
    </div>
  );
}

function ObjectCollectionPage<T extends TemplateEngineering | EquipmentEngineering>({ title, items, columns, locale, kind }: {
  title: string;
  items: T[];
  columns: Array<TableColumn<T>>;
  locale: EngineeringLocale;
  kind: 'templates' | 'equipment';
}) {
  const copy = objectCatalogCopy(locale);
  const t = translator(locale);
  return <EntitySection title={title} description={kind === 'templates' ? copy.templatesHint : copy.equipmentHint}
    items={items} columns={columns} t={t}
    emptyHint={kind === 'templates' ? copy.templatesEmpty : copy.equipmentEmpty}
    actionHref="/engineering/screens" actionText={copy.openScreens}/>;
}

function DynamoCatalogSection({ items, snapshot, locale, onApplied, onSnapshotRefreshed }: { items: DynamoEngineering[]; snapshot: EngineeringSnapshot; locale: EngineeringLocale; onApplied: () => Promise<void>; onSnapshotRefreshed: () => Promise<void> }) {
  const copy = objectCatalogCopy(locale);
  const catalogItems = selectDefaultDynamoCatalog(items);
  const [query, setQuery] = useState('');
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const editing = editingKey ? catalogItems.find(item => item.key === editingKey) : null;
  const visible = catalogItems.filter(item => !query.trim() || `${item.name} ${item.key} ${item.templateKey ?? ''}`.toLocaleLowerCase(locale).includes(query.trim().toLocaleLowerCase(locale)));
  if (editing) return <div className="dynamo-catalog__editor">
    <VisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onApplied} onAssetImported={onSnapshotRefreshed} definitionKind="dynamo" initialDefinitionKey={editingKey} onRequestClose={() => setEditingKey(null)}/>
  </div>;
  return <section className="eng-section" data-testid="dynamo-catalog">
    <header className="eng-section-header"><div><span className="eng-eyebrow">{copy.dynamos}</span><h1>{copy.dynamos}</h1><p>{copy.dynamoHint}</p></div><div className="eng-section-meta"><strong>{catalogItems.length} {copy.items}</strong></div></header>
    <div className="eng-panel dynamo-catalog__panel">
      <label className="dynamo-catalog__search"><span>{copy.search}</span><input type="search" value={query} onChange={event => setQuery(event.currentTarget.value)}/></label>
      {visible.length === 0 ? <div className="eng-empty"><strong>{catalogItems.length ? copy.noMatches : copy.noDynamos}</strong><span>{copy.dynamoAuthoringUnavailable}</span></div> : <div className="dynamo-catalog__grid">
        {visible.map(item => <article className="dynamo-catalog__card" key={item.id ?? item.key} data-testid="dynamo-catalog-card" data-dynamo-key={item.key} data-catalog-status={item.metadata?.catalogStatus ?? 'project'}>
          <CanonicalVisualPreview elements={item.elements ?? []} locale={locale} width={dynamoCanvasDimension(item, 'defaultWidth', 160)} height={dynamoCanvasDimension(item, 'defaultHeight', 110)} emptyLabel={copy.noPreview} variant="catalog"/>
          <div><strong>{item.name}</strong><small>{item.elements?.length ?? 0} {copy.objects} · {(item.parameters ?? []).length} {copy.parameters}</small><button type="button" className="secondary" onClick={() => setEditingKey(item.key)}>{copy.editDynamo}</button></div>
        </article>)}
      </div>}
      <p className="dynamo-catalog__notice">{copy.dynamoEditHint}</p>
    </div>
  </section>;
}

function dynamoCanvasDimension(item: DynamoEngineering, property: 'defaultWidth' | 'defaultHeight', fallback: number): number {
  const value = Number(item.properties?.[property]);
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

function objectCatalogCopy(locale: EngineeringLocale) {
  if (locale === 'en') return { readOnlyCatalog: 'Project catalog', templatesHint: 'This page only displays existing definitions; this workspace does not currently create or edit templates.', equipmentHint: 'This page only displays existing equipment definitions; the editor does not currently create or edit equipment here.', templatesEmpty: 'No templates are configured. Template authoring is not available in this workspace yet.', equipmentEmpty: 'No equipment is configured. Equipment authoring is not available in this workspace yet.', dynamos: 'Dynamos', dynamoHint: 'Select a Dynamo to refine its visual definition. Changes are previewed and validated before applying.', dynamoAuthoringUnavailable: 'Dynamo editing is not available.', dynamoEditHint: 'Editing a definition updates the shared Dynamo; screen instances keep their link and reflect the new drawing.', editDynamo: 'Edit drawing', backToDynamos: '← Back to Dynamo library', noDynamos: 'No Dynamo definitions are configured.', noMatches: 'No matching Dynamos.', openScreens: 'Open screen editor', items: 'items', search: 'Search', objects: 'objects', parameters: 'parameters', noPreview: 'No visual preview' } as const;
  if (locale === 'es') return { readOnlyCatalog: 'Catálogo del proyecto', templatesHint: 'Esta página solo muestra definiciones existentes; este espacio aún no permite crear ni editar plantillas.', equipmentHint: 'Esta página solo muestra equipos existentes; el editor aún no permite crearlos ni editarlos aquí.', templatesEmpty: 'No hay plantillas. La creación de plantillas todavía no está disponible en este espacio.', equipmentEmpty: 'No hay equipos. La creación de equipos todavía no está disponible en este espacio.', dynamos: 'Dínamos', dynamoHint: 'Seleccione un Dínamo para mejorar su diseño. Los cambios se validan antes de aplicarlos.', dynamoAuthoringUnavailable: 'La edición de Dínamos no está disponible.', dynamoEditHint: 'Editar una definición actualiza el Dínamo compartido; las instancias de pantalla conservan el vínculo.', editDynamo: 'Editar dibujo', backToDynamos: '← Volver a la biblioteca', noDynamos: 'No hay definiciones Dynamo.', noMatches: 'No hay Dínamos coincidentes.', openScreens: 'Abrir editor de pantallas', items: 'elementos', search: 'Buscar', objects: 'objetos', parameters: 'parámetros', noPreview: 'Vista previa no disponible' } as const;
  return { readOnlyCatalog: 'Catálogo do projeto', templatesHint: 'Esta página apenas exibe definições existentes; este espaço ainda não cria nem edita templates.', equipmentHint: 'Esta página apenas exibe equipamentos existentes; o editor ainda não os cria nem edita por aqui.', templatesEmpty: 'Nenhum template configurado. A criação de templates ainda não está disponível neste espaço.', equipmentEmpty: 'Nenhum equipamento configurado. O editor ainda não cria nem edita equipamentos por aqui.', dynamos: 'Dínamos', dynamoHint: 'Escolha um dínamo para refinar o desenho pela interface. As alterações passam por preview e validação antes de aplicar.', dynamoAuthoringUnavailable: 'Edição de dínamos indisponível.', dynamoEditHint: 'Editar uma definição atualiza o dínamo compartilhado; as instâncias nas telas mantêm o vínculo e recebem o desenho atualizado.', editDynamo: 'Editar desenho', backToDynamos: '← Voltar à biblioteca de dínamos', noDynamos: 'Nenhuma definição de dínamo configurada.', noMatches: 'Nenhum dínamo corresponde à busca.', openScreens: 'Abrir editor de telas', items: 'itens', search: 'Buscar', objects: 'objetos', parameters: 'parâmetros', noPreview: 'Prévia indisponível' } as const;
}

function SectionHeader({ title, description, count, t }: { title: string; description?: string; count?: number; t: ReturnType<typeof translator> }) {
  return <header className="eng-section-header"><div><h1>{title}</h1>{description && <p>{description}</p>}</div>{count !== undefined && <div className="eng-section-meta"><strong>{count} {t('section.count')}</strong></div>}</header>;
}

function Diagnostic({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return <div className="eng-diagnostic-card"><span>{label}</span><strong className={mono ? 'mono' : ''}>{value}</strong></div>;
}
function Code({ children }: { children: React.ReactNode }) { return <code className="eng-code">{children}</code>; }
function BindingInspection({ bindings, t }: { bindings: Array<{ key: string; kind: string; target: string; direction?: string | null }> | null | undefined; t: ReturnType<typeof translator> }) {
  return <details className="eng-binding-inspection">
    <summary>{t('section.inspect')}</summary>
    {!bindings?.length ? <span>{t('section.noBindings')}</span> : <ul>{bindings.map(binding => <li key={`${binding.key}:${binding.target}`}><Code>{binding.key}</Code> <span>{binding.kind}</span> <Code>{binding.target}</Code>{binding.direction ? <small>{binding.direction}</small> : null}</li>)}</ul>}
  </details>;
}
function sectionCount(model: EngineeringPackageView, section: SectionId): number | string {
  switch (section) {
    case 'dataSources': return model.dataSources?.length ?? 0;
    case 'mediaSources': return model.mediaSources?.length ?? 0;
    case 'gateway': return model.gateways?.length ?? 0;
    case 'tags': return model.tags.length;
    case 'alarms': return model.alarms.length;
    case 'operationalEvents': return operationalEventCount(model);
    case 'templates': return model.templates?.length ?? 0;
    case 'equipment': return model.equipment?.length ?? 0;
    case 'dynamos': return selectDefaultDynamoCatalog(model.dynamos ?? []).length;
    case 'visualAssets': return listUserVisualAssets(model.visualAssets).length;
    case 'screens': return model.screens?.length ?? 0;
    case 'popups': return model.popups?.length ?? 0;
    case 'historian': return model.tags.filter(tag => tag.historian?.enabled).length;
    case 'reports': return reportCollection(model).length;
    case 'security': return model.securityRoles?.length ?? 0;
    case 'highAvailability':
    case 'databaseTopology':
    case 'installation':
    case 'branding':
    case 'mobile':
    case 'scripts':
    case 'libraries':
    case 'overview':
    case 'monitor':
    case 'tagMonitor':
    case 'diagnostics':
    case 'information': return '•';
  }
}
function currentSectionLabel(section: SectionId, locale: EngineeringLocale, t: ReturnType<typeof translator>): string {
  for (const group of navigation) {
    const item = group.items.find(candidate => candidate.id === section);
    if (!item) continue;
    if (item.literalLabel) return item.literalLabel[locale];
    if (item.label) return t(item.label);
    return scriptNavLabel(locale);
  }
  return t('app.engineering');
}

function informationCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Information',
    description: 'Product identity and on-demand technical details for the current Engineering workspace.',
    product: 'Product version',
    productUnavailable: 'Product version unavailable',
    technicalDetails: 'Technical details',
    schema: 'Engineering schema',
    baseRevision: 'Base revision',
    snapshot: 'Loaded snapshot',
    project: 'Project',
    buildCommit: 'Build commit'
  };
  if (locale === 'es') return {
    title: 'Información',
    description: 'Identidad del producto y detalles técnicos bajo demanda del área de Engineering actual.',
    product: 'Versión del producto',
    productUnavailable: 'Versión del producto no disponible',
    technicalDetails: 'Detalles técnicos',
    schema: 'Schema de Engineering',
    baseRevision: 'Revisión base',
    snapshot: 'Snapshot cargado',
    project: 'Proyecto',
    buildCommit: 'Commit de build'
  };
  return {
    title: 'Informações',
    description: 'Identidade do produto e detalhes técnicos sob demanda da área de Engineering atual.',
    product: 'Versão do produto',
    productUnavailable: 'Versão do produto indisponível',
    technicalDetails: 'Detalhes técnicos',
    schema: 'Schema de Engineering',
    baseRevision: 'Revisão base',
    snapshot: 'Snapshot carregado',
    project: 'Projeto',
    buildCommit: 'Commit do build'
  };
}

function formatDate(value: string, locale: EngineeringLocale) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}
function scriptNavLabel(_locale: EngineeringLocale) { return 'Scripts'; }
function NavIcon({ section }: { section: SectionId }) {
  const symbols: Record<SectionId, string> = { overview: '⌂', installation: '⇆', branding: '◐', mobile: '▱', scripts: '</>', libraries: '▱', dataSources: '⇄', mediaSources: '▣', gateway: '⇢', tags: '#', alarms: '!', operationalEvents: '✦', templates: '◇', equipment: '□', dynamos: '◈', visualAssets: '▧', screens: '▣', popups: '▤', historian: '⌁', reports: '▧', security: '◆', highAvailability: '⇄', databaseTopology: '▤', monitor: '◉', tagMonitor: '◫', diagnostics: '⋯', information: 'ⓘ' };
  return <i aria-hidden="true">{symbols[section]}</i>;
}

function resolveInitialSection(): SectionId {
  const path = window.location.pathname;
  if (path.startsWith(tagMonitorPath)) return 'tagMonitor';
  if (path.startsWith(librariesPath)) return 'libraries';
  const section = path.split('/')[2] as SectionId | undefined;
  if (section && navigation.some(group => group.items.some(item => item.id === section))) return section;
  return 'overview';
}

function engineeringSectionPath(section: SectionId): string {
  if (section === 'overview') return '/engineering';
  if (section === 'tagMonitor') return tagMonitorPath;
  if (section === 'libraries') return librariesPath;
  return `/engineering/${section}`;
}
