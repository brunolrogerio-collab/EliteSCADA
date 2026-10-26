import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useAppShellLocale } from '../appShellI18n';
import { setStoredLocale, type EngineeringLocale } from '../engineering/i18n';
import { filterHelpTopics, helpArea, helpAreas, helpBodyBlocks, helpSubarea, helpSubareas, helpTopicPriority } from './helpNavigation';
import './contextual-help.css';

type HelpSection = Readonly<{
  heading: string;
  body: string;
  code?: string | null;
  fields?: readonly Readonly<{ key: string; label: string; description: string; example: string; constraints: string }>[] | null;
  figure?: Readonly<{ source: string; alt: string; caption: string }> | null;
}>;

type HelpTopic = Readonly<{
  id: string;
  category: string;
  title: string;
  summary: string;
  sections: readonly HelpSection[];
  relatedTopicIds?: readonly string[];
}>;

type HelpCatalog = Readonly<{
  locale: EngineeringLocale;
  supportedLocales: readonly EngineeringLocale[];
  topics: readonly HelpTopic[];
  serverScriptApi: readonly string[];
  serverScriptApiDetails?: readonly Readonly<{
    name: string;
    signature: string;
    parameters: string;
    result: string;
    safety: string;
    example: string;
  }>[];
}>;

const ui = {
  'pt-BR': {
    title: 'Ajuda do SCADIVO',
    subtitle: 'Ajuda local e contextual',
    language: 'Idioma',
    topics: 'Tópicos',
    loading: 'Carregando ajuda local...',
    error: 'Não foi possível carregar a ajuda local.',
    empty: 'Tópico de ajuda não encontrado.',
    api: 'Referência das funções de Server Script', parameters: 'Parâmetros', result: 'Resultado', safety: 'Cuidados', related: 'Continue por aqui',
    search: 'O que você quer fazer?', searchHint: 'Busque uma tarefa, campo ou comando', clear: 'Limpar busca', noResults: 'Nenhum tópico corresponde à busca. Tente uma palavra mais simples ou o nome do campo.', outline: 'Neste artigo', field: 'Campo', meaning: 'Para que serve e como preencher', example: 'Exemplo', constraints: 'Valores e limites', technical: 'Referência técnica', results: 'tópicos encontrados',
    assets: 'Templates e equipamentos', administration: 'Banco de dados e HA', start: 'Primeiros passos e projeto', data: 'Fontes de dados e TAGs', drivers: 'Conexão com equipamentos', visual: 'Telas, popups e objetos', scripts: 'Scripts e memória', intelligence: 'Inteligência de ativos e processos', history: 'Histórico, tendências e relatórios', events: 'Alarmes, eventos e auditoria', security: 'Usuários e permissões', licensing: 'Licenciamento', runtime: 'Operação e diagnóstico',
    guides: 'Guias e procedimentos', shapes: 'Formas e texto', media: 'Imagens e mídia', controls: 'Valores, controles e navegadores', gauges: 'Medidores', analysisObjects: 'Objetos de análise',
    driverBasics: 'Preparar a conexão', driverPlcs: 'CLPs industriais', driverProtocols: 'Modbus, MQTT e OPC UA', driverTelecontrol: 'Telecontrole',
    driverBuildings: 'Automação predial e residencial', driverRadios: 'Zigbee e Z-Wave', driverReference: 'Referência detalhada dos drivers'
  },
  en: {
    title: 'SCADIVO Help',
    subtitle: 'Local contextual help',
    language: 'Language',
    topics: 'Topics',
    loading: 'Loading local help...',
    error: 'The local help could not be loaded.',
    empty: 'Help topic not found.',
    api: 'Server Script function reference', parameters: 'Parameters', result: 'Result', safety: 'Considerations', related: 'Continue here',
    search: 'What do you want to do?', searchHint: 'Search for a task, field or command', clear: 'Clear search', noResults: 'No topics match your search. Try a simpler word or the field name.', outline: 'In this article', field: 'Field', meaning: 'Purpose and how to fill it', example: 'Example', constraints: 'Values and limits', technical: 'Technical reference', results: 'topics found',
    assets: 'Templates and equipment', administration: 'Database and HA', start: 'First steps and projects', data: 'Data Sources and TAGs', drivers: 'Equipment connections', visual: 'Screens, popups and objects', scripts: 'Scripts and memory', intelligence: 'Asset and Process Intelligence', history: 'History, trends and reports', events: 'Alarms, events and audit', security: 'Users and permissions', licensing: 'Licensing', runtime: 'Operation and diagnostics',
    guides: 'Guides and procedures', shapes: 'Shapes and text', media: 'Images and media', controls: 'Values, controls and browsers', gauges: 'Gauges', analysisObjects: 'Analysis objects',
    driverBasics: 'Prepare the connection', driverPlcs: 'Industrial PLCs', driverProtocols: 'Modbus, MQTT and OPC UA', driverTelecontrol: 'Telecontrol',
    driverBuildings: 'Building and home automation', driverRadios: 'Zigbee and Z-Wave', driverReference: 'Detailed driver reference'
  },
  es: {
    title: 'Ayuda de SCADIVO',
    subtitle: 'Ayuda local y contextual',
    language: 'Idioma',
    topics: 'Temas',
    loading: 'Cargando ayuda local...',
    error: 'No fue posible cargar la ayuda local.',
    empty: 'Tema de ayuda no encontrado.',
    api: 'Referencia de funciones de Server Script', parameters: 'Parámetros', result: 'Resultado', safety: 'Cuidados', related: 'Continúe por aquí',
    search: '¿Qué desea hacer?', searchHint: 'Busque una tarea, campo o comando', clear: 'Limpiar búsqueda', noResults: 'Ningún tema coincide. Pruebe una palabra más sencilla o el nombre del campo.', outline: 'En este artículo', field: 'Campo', meaning: 'Para qué sirve y cómo completarlo', example: 'Ejemplo', constraints: 'Valores y límites', technical: 'Referencia técnica', results: 'temas encontrados',
    assets: 'Plantillas y equipos', administration: 'Base de datos y HA', start: 'Primeros pasos y proyectos', data: 'Fuentes de datos y TAGs', drivers: 'Conexión con equipos', visual: 'Pantallas, popups y objetos', scripts: 'Scripts y memoria', intelligence: 'Inteligencia de activos y procesos', history: 'Histórico, tendencias e informes', events: 'Alarmas, eventos y auditoría', security: 'Usuarios y permisos', licensing: 'Licenciamiento', runtime: 'Operación y diagnóstico',
    guides: 'Guías y procedimientos', shapes: 'Formas y texto', media: 'Imágenes y multimedia', controls: 'Valores, controles y navegadores', gauges: 'Medidores', analysisObjects: 'Objetos de análisis',
    driverBasics: 'Preparar la conexión', driverPlcs: 'PLCs industriales', driverProtocols: 'Modbus, MQTT y OPC UA', driverTelecontrol: 'Telecontrol',
    driverBuildings: 'Automatización de edificios y hogares', driverRadios: 'Zigbee y Z-Wave', driverReference: 'Referencia detallada de drivers'
  }
} as const;

export function ContextualHelpApp() {
  const locale = useAppShellLocale();
  const text = ui[locale];
  const [requestedTopic, setRequestedTopic] = useState(() => new URLSearchParams(window.location.search).get('topic'));
  const [catalog, setCatalog] = useState<HelpCatalog | null>(null);
  const [error, setError] = useState(false);
  const [query, setQuery] = useState('');
  const topicHeading = useRef<HTMLHeadingElement>(null);
  const pendingTopicFocus = useRef(false);
  const visibleTopics = useMemo(() => filterHelpTopics(catalog?.topics ?? [], query), [catalog, query]);

  useEffect(() => {
    const restoreTopic = () => {
      pendingTopicFocus.current = false;
      setRequestedTopic(new URLSearchParams(window.location.search).get('topic'));
    };
    window.addEventListener('popstate', restoreTopic);
    return () => window.removeEventListener('popstate', restoreTopic);
  }, []);

  const openTopic = (event: React.MouseEvent<HTMLAnchorElement>, topicId: string) => {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    if (topicId !== requestedTopic) {
      pendingTopicFocus.current = true;
      window.history.pushState(null, '', `/help?topic=${encodeURIComponent(topicId)}`);
    } else {
      topicHeading.current?.focus({ preventScroll: true });
      topicHeading.current?.scrollIntoView({ block: 'start' });
    }
    setRequestedTopic(topicId);
  };

  useEffect(() => {
    let cancelled = false;
    setError(false);
    fetch(`/api/help?locale=${encodeURIComponent(locale)}`, { credentials: 'same-origin' })
      .then(async response => {
        if (!response.ok) throw new Error(`Help HTTP ${response.status}`);
        return await response.json() as HelpCatalog;
      })
      .then(value => {
        if (!cancelled) setCatalog(value);
      })
      .catch(() => {
        if (!cancelled) setError(true);
      });
    return () => { cancelled = true; };
  }, [locale]);

  const selected = useMemo(() => {
    if (!catalog) return null;
    if (requestedTopic) return catalog.topics.find(topic => topic.id === requestedTopic) ?? null;
    return catalog.topics.find(topic => topic.id === 'help.find-your-way') ?? catalog.topics[0] ?? null;
  }, [catalog, requestedTopic]);

  useEffect(() => {
    if (!topicHeading.current || !pendingTopicFocus.current) return;
    pendingTopicFocus.current = false;
    topicHeading.current?.focus({ preventScroll: true });
    topicHeading.current?.scrollIntoView({ block: 'start' });
  }, [selected?.id]);

  const selectLocale = (nextLocale: EngineeringLocale) => {
    setStoredLocale(nextLocale);
    document.documentElement.lang = nextLocale;
  };

  const seenBodies = new Set(selected ? [selected.summary.trim()] : []);
  const articleSections = selected?.sections.filter(section => {
    if (section.code || section.fields?.length || section.figure) return true;
    const body = section.body.trim();
    if (seenBodies.has(body)) return false;
    seenBodies.add(body);
    return true;
  }) ?? [];

  if (error) return <main className="shell help-page" role="alert">{text.error}</main>;
  if (!catalog) return <main className="shell help-page" aria-busy="true">{text.loading}</main>;

  return (
    <main className="shell help-page" data-help-locale={locale}>
      <header className="help-page__header">
        <div>
          <span className="help-page__eyebrow">{text.subtitle}</span>
          <h1>{text.title}</h1>
        </div>
        <label className="help-page__locale">
          <span>{text.language}</span>
          <select
            aria-label={text.language}
            value={locale}
            onChange={event => selectLocale(event.target.value as EngineeringLocale)}
          >
            <option value="pt-BR">Português</option>
            <option value="en">English</option>
            <option value="es">Español</option>
          </select>
        </label>
      </header>

      <div className="help-page__layout">
        <nav className="help-page__topics" aria-label={text.topics}>
          <label className="help-page__search"><strong>{text.search}</strong>
            <input type="search" placeholder={text.searchHint} value={query} onChange={event => setQuery(event.target.value)} />
          </label>
          {query ? <><button type="button" onClick={() => setQuery('')}>{text.clear}</button><p role="status">{visibleTopics.length} {text.results}</p></> : null}
          {!visibleTopics.length ? <p>{text.noResults}</p> : helpAreas.map(area => {
            const topics = visibleTopics.filter(topic => helpArea(topic.category) === area)
              .sort((left, right) => helpTopicPriority(left.id) - helpTopicPriority(right.id) || left.title.localeCompare(right.title, locale));
            if (!topics.length) return null;
            const subareas = helpSubareas.filter(subarea => topics.some(topic => helpSubarea(topic) === subarea));
            const topicLinks = (subarea: typeof subareas[number]) => topics.filter(topic => helpSubarea(topic) === subarea).map(topic => <a key={topic.id} href={`/help?topic=${encodeURIComponent(topic.id)}`} onClick={event => openTopic(event, topic.id)}
              className={selected?.id === topic.id ? 'active' : undefined} aria-current={selected?.id === topic.id ? 'page' : undefined}>
              {topic.title}
            </a>);
            return <details key={area} open={!!query || (!!selected && helpArea(selected.category) === area)} className="help-page__area">
              <summary>{text[area]} <span aria-hidden="true">({topics.length})</span></summary>
              {subareas.map(subarea => area === 'drivers' ? <details key={subarea} data-help-group={subarea} className="help-page__subarea help-page__driver-group"
                open={!!query || subarea === 'driverBasics' || (!!selected && topics.some(topic => topic.id === selected.id && helpSubarea(topic) === subarea))}>
                <summary>{text[subarea]} <span aria-hidden="true">({topics.filter(topic => helpSubarea(topic) === subarea).length})</span></summary>
                {topicLinks(subarea)}
              </details> : <div key={subarea} className="help-page__subarea">
                {subareas.length > 1 ? <h3>{text[subarea as keyof typeof text]}</h3> : null}
                {topicLinks(subarea)}
              </div>)}
            </details>;
          })}
        </nav>

        <article className="help-page__content" data-help-topic={selected?.id ?? ''}>
          {selected ? <>
            <h2 ref={topicHeading} tabIndex={-1}>{selected.title}</h2>
            <p className="help-page__summary">{selected.summary}</p>
            <nav className="help-page__outline" aria-label={text.outline}><strong>{text.outline}</strong><ul>
              {articleSections.map((section, index) => <li key={index}><a href={`#help-section-${index}`}>{section.heading}</a></li>)}
            </ul></nav>
            {articleSections.map((section, index) => (
              <section id={`help-section-${index}`} key={`${selected.id}-${index}`}>
                <h3>{section.heading}</h3>
                {helpBodyBlocks(section.body).map((block, blockIndex) => block.ordered
                  ? <ol className="help-page__steps" key={blockIndex}>{block.lines.map((line, lineIndex) => <li key={lineIndex}>{inlineHelpText(line)}</li>)}</ol>
                  : <p className="help-page__body" key={blockIndex}>{inlineHelpText(block.lines[0])}</p>)}
                {section.code ? <pre><code>{section.code}</code></pre> : null}
                {section.fields?.length ? <div className="help-page__table-scroll" tabIndex={0} role="region" aria-label={section.heading}>
                  <table role="table"><thead role="rowgroup"><tr role="row"><th role="columnheader">{text.field}</th><th role="columnheader">{text.meaning}</th><th role="columnheader">{text.example}</th></tr></thead><tbody role="rowgroup">
                    {section.fields.map(field => <tr key={field.key} role="row"><th scope="row" role="rowheader">{field.label}<details><summary>{text.technical}</summary><code>{field.key}</code></details></th>
                      <td role="cell">{field.description}{field.constraints ? <p className="help-page__constraints">{field.constraints}</p> : null}</td><td role="cell" data-label={text.example}>{field.example}</td></tr>)}
                  </tbody></table>
                </div> : null}
                {section.figure ? <figure><img src={section.figure.source} alt={section.figure.alt} loading="lazy" /><figcaption>{section.figure.caption}</figcaption></figure> : null}
              </section>
            ))}
            {selected.relatedTopicIds?.length ? <section>
              <h3>{text.related}</h3>
              <ul>
                {selected.relatedTopicIds.map(topicId => {
                  const related = catalog.topics.find(topic => topic.id === topicId);
                  return related ? <li key={topicId}><a href={`/help?topic=${encodeURIComponent(topicId)}`} onClick={event => openTopic(event, topicId)}>{related.title}</a></li> : null;
                })}
              </ul>
            </section> : null}
            {selected.id === 'scripts.server' ? <section>
              <h3>{text.api}</h3>
              {catalog.serverScriptApiDetails?.length ? <div className="help-page__script-api" data-testid="server-script-api-details">
                {catalog.serverScriptApiDetails.map(api => <article key={api.name}>
                  <h4><code>{api.signature}</code></h4>
                  <dl>
                    <dt>{text.parameters}</dt><dd>{api.parameters}</dd>
                    <dt>{text.result}</dt><dd>{api.result}</dd>
                    <dt>{text.safety}</dt><dd>{api.safety}</dd>
                  </dl>
                  <pre><code>{api.example}</code></pre>
                </article>)}
              </div> : <ul>{catalog.serverScriptApi.map(name => <li key={name}><code>{name}</code></li>)}</ul>}
            </section> : null}
          </> : <p>{text.empty}</p>}
        </article>
      </div>
    </main>
  );
}

function inlineHelpText(value: string): React.ReactNode {
  return value.split(/(`[^`]+`)/g).map((part, index) => part.startsWith('`') && part.endsWith('`')
    ? <code key={index}>{part.slice(1, -1)}</code> : part);
}
