import React from 'react';
import { appShellText, useAppShellLocale } from './appShellI18n';
import { useAppTheme } from './appTheme';
import { UserSessionMenu } from './auth/UserSessionMenu';
import {
  resolveAppSurfaceAccess,
  useEffectiveCapabilities
} from './auth/effectiveCapabilities';
import './app-navigation.css';

type ShellLink = Readonly<{
  href: string;
  label: string;
  description: string;
}>;

const helpText = {
  'pt-BR': { label: 'Ajuda', description: 'Manual local' },
  en: { label: 'Help', description: 'Local manual' },
  es: { label: 'Ayuda', description: 'Manual local' }
} as const;

function contextualHelpTopic(path: string) {
  if (path.startsWith('/engineering')) return 'engineering.overview';
  if (path.startsWith('/audit')) return 'audit.overview';
  if (path.startsWith('/licensing')) return 'licensing.overview';
  if (path.startsWith('/runtime/history')) return 'runtime.history';
  return 'runtime.overview';
}

export function AppNavigation() {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const { theme, selectTheme } = useAppTheme();
  const { capabilities, loading } = useEffectiveCapabilities();
  const path = window.location.pathname;
  const access = resolveAppSurfaceAccess(capabilities);

  const links: ShellLink[] = [];
  if (access.runtime) links.push({ href: '/', label: text.runtime, description: text.runtimeDescription });
  if (access.engineering) links.push({ href: '/engineering', label: text.engineering, description: text.engineeringDescription });
  if (access.audit) links.push({ href: '/audit', label: text.audit, description: text.auditDescription });
  if (access.licensing) links.push({ href: '/licensing', label: text.licensing, description: text.licensingDescription });
  if (access.runtime || access.engineering || access.audit || access.licensing) {
    links.push({
      href: `/help?topic=${contextualHelpTopic(path)}`,
      label: helpText[locale].label,
      description: helpText[locale].description
    });
  }

  const activeHref = path.startsWith('/help')
    ? '/help'
    : path.startsWith('/licensing')
      ? '/licensing'
      : path.startsWith('/audit')
        ? '/audit'
        : path.startsWith('/engineering')
          ? '/engineering'
          : '/';
  const activeRuntimeHref = path.startsWith('/runtime/history') ? '/runtime/history' : '/';
  const active = activeHref === '/help'
    ? { href: '/help', label: helpText[locale].label, description: helpText[locale].description }
    : links.find(link => link.href === activeHref) ?? links[0];
  const privilegedShell = access.engineering || access.audit || access.licensing;
  const runtimeOnly = access.runtime && !privilegedShell;

  return (
    <>
      <header
        className={`app-bar${runtimeOnly ? ' app-bar--runtime-only' : ''}`}
        data-capabilities-loading={loading || undefined}
      >
        <a className="app-brand" href={access.runtime ? '/' : access.engineering ? '/engineering' : access.licensing ? '/licensing' : '#'} aria-label="EliteSCADA">
          <span className="app-brand-mark" aria-hidden="true">E</span>
          <span className="app-brand-copy"><strong>EliteSCADA</strong><small>{text.subtitle}</small></span>
        </a>
        <nav className="app-navigation" aria-label="EliteSCADA">
          {links.map(link => {
            const normalizedHref = link.href.startsWith('/help') ? '/help' : link.href;
            const isActive = activeHref === normalizedHref;
            return <a
              key={link.href}
              href={link.href}
              className={isActive ? 'active' : undefined}
              aria-current={isActive ? 'page' : undefined}
            ><span>{link.label}</span><small>{link.description}</small></a>;
          })}
        </nav>
        <div className="app-shell-actions">
          {!runtimeOnly && active ? <div className="app-context"><span>{text.currentArea}</span><strong>{active.label}</strong></div> : null}
          <label className="app-theme-control">
            <span className="sr-only">{text.theme}</span>
            <select
              aria-label={text.theme}
              value={theme}
              onChange={event => selectTheme(event.target.value === 'light' ? 'light' : 'dark')}
            >
              <option value="dark">{text.themeDark}</option>
              <option value="light">{text.themeLight}</option>
            </select>
          </label>
          <UserSessionMenu locale={locale} />
        </div>
      </header>
      {activeHref === '/' && access.runtime && access.history && (
        <nav className="runtime-view-navigation" aria-label="Runtime views">
          <a href="/" className={activeRuntimeHref === '/' ? 'active' : undefined} aria-current={activeRuntimeHref === '/' ? 'page' : undefined}>{text.runtimeOverview}</a>
          <a href="/runtime/history" className={activeRuntimeHref === '/runtime/history' ? 'active' : undefined} aria-current={activeRuntimeHref === '/runtime/history' ? 'page' : undefined}>{text.runtimeHistory}</a>
        </nav>
      )}
    </>
  );
}
