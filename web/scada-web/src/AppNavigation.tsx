import React from 'react';
import './app-navigation.css';

type ShellLocale = 'pt-BR' | 'en' | 'es';

type ShellCopy = {
  subtitle: string;
  currentArea: string;
  runtime: string;
  runtimeDescription: string;
  engineering: string;
  engineeringDescription: string;
  audit: string;
  auditDescription: string;
};

const localeKey = 'elitescada.engineering.locale';

const copy: Record<ShellLocale, ShellCopy> = {
  'pt-BR': {
    subtitle: 'Plataforma industrial',
    currentArea: 'Área atual',
    runtime: 'Runtime',
    runtimeDescription: 'Operação',
    engineering: 'Engineering',
    engineeringDescription: 'Projeto',
    audit: 'Auditoria',
    auditDescription: 'Rastreabilidade'
  },
  en: {
    subtitle: 'Industrial platform',
    currentArea: 'Current area',
    runtime: 'Runtime',
    runtimeDescription: 'Operations',
    engineering: 'Engineering',
    engineeringDescription: 'Project',
    audit: 'Audit',
    auditDescription: 'Traceability'
  },
  es: {
    subtitle: 'Plataforma industrial',
    currentArea: 'Área actual',
    runtime: 'Runtime',
    runtimeDescription: 'Operación',
    engineering: 'Engineering',
    engineeringDescription: 'Proyecto',
    audit: 'Auditoría',
    auditDescription: 'Trazabilidad'
  }
};

function resolveLocale(): ShellLocale {
  const stored = window.localStorage.getItem(localeKey);
  if (stored === 'pt-BR' || stored === 'en' || stored === 'es') return stored;
  const browser = navigator.language.toLowerCase();
  if (browser.startsWith('en')) return 'en';
  if (browser.startsWith('es')) return 'es';
  return 'pt-BR';
}

export function AppNavigation() {
  const text = copy[resolveLocale()];
  const path = window.location.pathname;
  const activeHref = path.startsWith('/audit')
    ? '/audit'
    : path.startsWith('/engineering')
      ? '/engineering'
      : '/';
  const links = [
    { href: '/', label: text.runtime, description: text.runtimeDescription },
    { href: '/engineering', label: text.engineering, description: text.engineeringDescription },
    { href: '/audit', label: text.audit, description: text.auditDescription }
  ];
  const active = links.find(link => link.href === activeHref) ?? links[0];

  return (
    <header className="app-bar">
      <a className="app-brand" href="/" aria-label="EliteSCADA Runtime">
        <span className="app-brand-mark" aria-hidden="true">E</span>
        <span className="app-brand-copy">
          <strong>EliteSCADA</strong>
          <small>{text.subtitle}</small>
        </span>
      </a>

      <nav className="app-navigation" aria-label="EliteSCADA">
        {links.map(link => {
          const isActive = activeHref === link.href;
          return (
            <a
              key={link.href}
              href={link.href}
              className={isActive ? 'active' : undefined}
              aria-current={isActive ? 'page' : undefined}
            >
              <span>{link.label}</span>
              <small>{link.description}</small>
            </a>
          );
        })}
      </nav>

      <div className="app-context">
        <span>{text.currentArea}</span>
        <strong>{active.label}</strong>
      </div>
    </header>
  );
}
