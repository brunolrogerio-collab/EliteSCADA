import React, { useEffect, useMemo, useState } from 'react';
import { useAppShellLocale } from '../appShellI18n';
import { setStoredLocale, type EngineeringLocale } from '../engineering/i18n';
import './contextual-help.css';

type HelpSection = Readonly<{
  heading: string;
  body: string;
  code?: string | null;
}>;

type HelpTopic = Readonly<{
  id: string;
  category: string;
  title: string;
  summary: string;
  sections: readonly HelpSection[];
}>;

type HelpCatalog = Readonly<{
  locale: EngineeringLocale;
  supportedLocales: readonly EngineeringLocale[];
  topics: readonly HelpTopic[];
  serverScriptApi: readonly string[];
}>;

const ui = {
  'pt-BR': {
    title: 'Ajuda do EliteSCADA',
    subtitle: 'Manual local e contextual',
    language: 'Idioma',
    topics: 'Tópicos',
    loading: 'Carregando ajuda local...',
    error: 'Não foi possível carregar o manual local.',
    empty: 'Tópico de ajuda não encontrado.',
    api: 'API de Server Script suportada nesta compilação'
  },
  en: {
    title: 'EliteSCADA Help',
    subtitle: 'Local contextual manual',
    language: 'Language',
    topics: 'Topics',
    loading: 'Loading local help...',
    error: 'The local manual could not be loaded.',
    empty: 'Help topic not found.',
    api: 'Server Script API supported by this build'
  },
  es: {
    title: 'Ayuda de EliteSCADA',
    subtitle: 'Manual local y contextual',
    language: 'Idioma',
    topics: 'Temas',
    loading: 'Cargando ayuda local...',
    error: 'No fue posible cargar el manual local.',
    empty: 'Tema de ayuda no encontrado.',
    api: 'API de Server Script soportada en esta compilación'
  }
} as const;

export function ContextualHelpApp() {
  const locale = useAppShellLocale();
  const text = ui[locale];
  const requestedTopic = new URLSearchParams(window.location.search).get('topic');
  const [catalog, setCatalog] = useState<HelpCatalog | null>(null);
  const [error, setError] = useState(false);

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
    if (requestedTopic) {
      const match = catalog.topics.find(topic => topic.id === requestedTopic);
      if (match) return match;
    }
    return catalog.topics.find(topic => topic.id === 'runtime.overview') ?? catalog.topics[0] ?? null;
  }, [catalog, requestedTopic]);

  const selectLocale = (nextLocale: EngineeringLocale) => {
    setStoredLocale(nextLocale);
    document.documentElement.lang = nextLocale;
  };

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
          <strong>{text.topics}</strong>
          {catalog.topics.map(topic => (
            <a
              key={topic.id}
              href={`/help?topic=${encodeURIComponent(topic.id)}`}
              className={selected?.id === topic.id ? 'active' : undefined}
              aria-current={selected?.id === topic.id ? 'page' : undefined}
            >
              <span>{topic.title}</span>
              <small>{topic.id}</small>
            </a>
          ))}
        </nav>

        <article className="help-page__content" data-help-topic={selected?.id ?? ''}>
          {selected ? <>
            <span className="help-page__topic-id">{selected.id}</span>
            <h2>{selected.title}</h2>
            <p className="help-page__summary">{selected.summary}</p>
            {selected.sections.map((section, index) => (
              <section key={`${selected.id}-${index}`}>
                <h3>{section.heading}</h3>
                <p className="help-page__body">{section.body}</p>
                {section.code ? <pre><code>{section.code}</code></pre> : null}
              </section>
            ))}
            {selected.id === 'scripts.server' ? <section>
              <h3>{text.api}</h3>
              <ul>{catalog.serverScriptApi.map(name => <li key={name}><code>{name}</code></li>)}</ul>
            </section> : null}
          </> : <p>{text.empty}</p>}
        </article>
      </div>
    </main>
  );
}
