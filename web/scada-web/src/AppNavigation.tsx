import React from 'react';
import { appShellText, useAppShellLocale } from './appShellI18n';
import { useAppTheme } from './appTheme';
import { UserSessionMenu } from './auth/UserSessionMenu';
import {
  hasRuntimeCapability,
  resolveAppSurfaceAccess,
  useEffectiveCapabilities
} from './auth/effectiveCapabilities';
import { contextualHelpTopic } from './help/contextualHelpTopic';
import { ApplicationBrand, useActiveApplicationBranding } from './branding/ApplicationBranding';
import { databaseTopologyText } from './database-topology/i18n';
import './app-navigation.css';

type ShellLink = Readonly<{
  href: string;
  label: string;
  description?: string;
}>;

const helpText = {
  'pt-BR': { label: 'Ajuda', description: 'Manual local' },
  en: { label: 'Help', description: 'Local manual' },
  es: { label: 'Ayuda', description: 'Manual local' }
} as const;

export function AppNavigation() {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const { theme, selectTheme } = useAppTheme();
  const { capabilities, loading } = useEffectiveCapabilities();
  const branding = useActiveApplicationBranding();
  const path = window.location.pathname;
  const access = resolveAppSurfaceAccess(capabilities);
  const databaseAdmin = hasRuntimeCapability(capabilities, 'SystemAdmin');
  const databaseText = databaseTopologyText(locale);

  if ((path.startsWith('/runtime/history') || path.startsWith('/runtime/reports')) && access.runtime && access.history) {
    return (
      <header
        className={`app-bar app-bar--runtime-only${branding.mode === 'none' ? ' app-bar--branding-none' : ''}`}
        data-capabilities-loading={loading || undefined}
      >
        <ApplicationBrand
          branding={branding}
          defaultSubtitle={text.subtitle}
          href="/"
        />
        <nav className="app-navigation" aria-label="Runtime views">
          <a href="/"><span>{text.runtimeOverview}</span></a>
          <a href="/runtime/history" className={path.startsWith('/runtime/history') ? 'active' : undefined} aria-current={path.startsWith('/runtime/history') ? 'page' : undefined}><span>{text.runtimeHistory}</span></a>
          <a href="/runtime/reports" className={path.startsWith('/runtime/reports') ? 'active' : undefined} aria-current={path.startsWith('/runtime/reports') ? 'page' : undefined}><span>{locale === 'pt-BR' ? 'Relatórios' : locale === 'es' ? 'Informes' : 'Reports'}</span></a>
        </nav>
        <div className="app-shell-actions">
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
          <UserSessionMenu locale={locale} includeRuntimeSessionControls />
        </div>
      </header>
    );
  }

  const links: ShellLink[] = [];
  if (access.runtime) links.push({ href: '/', label: text.runtime, description: text.runtimeDescription });
  if (access.engineering) links.push({ href: '/engineering', label: text.engineering, description: text.engineeringDescription });
  if (access.audit) links.push({ href: '/audit', label: text.audit, description: text.auditDescription });
  if (access.licensing) links.push({ href: '/licensing', label: text.licensing });
  if (access.runtime || access.engineering || access.audit || access.licensing || databaseAdmin) {
    links.push({
      href: `/help?topic=${contextualHelpTopic(path)}`,
      label: helpText[locale].label
    });
  }

  const activeHref = path.startsWith('/help')
    ? '/help'
    : path.startsWith('/admin/database') || path.startsWith('/engineering/database-topology')
      ? '/engineering'
      : path.startsWith('/licensing')
      ? '/licensing'
      : path.startsWith('/audit')
        ? '/audit'
        : path.startsWith('/engineering')
          ? '/engineering'
          : '/';
  const activeRuntimeHref = path.startsWith('/runtime/reports') ? '/runtime/reports' : path.startsWith('/runtime/history') ? '/runtime/history' : '/';
  const privilegedShell = access.engineering || access.audit || access.licensing || databaseAdmin;
  const runtimeOnly = access.runtime && !privilegedShell;

  return (
    <>
      <header
        className={`app-bar${runtimeOnly ? ' app-bar--runtime-only' : ''}${branding.mode === 'none' ? ' app-bar--branding-none' : ''}`}
        data-capabilities-loading={loading || undefined}
      >
        <ApplicationBrand
          branding={branding}
          defaultSubtitle={text.subtitle}
          href={access.runtime ? '/' : access.engineering ? '/engineering' : access.licensing ? '/licensing' : databaseAdmin ? '/admin/database' : '#'}
        />
        <nav className="app-navigation" aria-label="EliteSCADA">
          {links.map(link => {
            const normalizedHref = link.href.startsWith('/help') ? '/help' : link.href;
            const isActive = activeHref === normalizedHref;
            return <a
              key={link.href}
              href={link.href}
              className={isActive ? 'active' : undefined}
              aria-current={isActive ? 'page' : undefined}
            ><span>{link.label}</span>{link.description ? <small>{link.description}</small> : null}</a>;
          })}
        </nav>
        <div className="app-shell-actions">
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
          <UserSessionMenu locale={locale} includeRuntimeSessionControls={path === '/' || path.startsWith('/runtime/history') || path.startsWith('/runtime/reports')} />
        </div>
      </header>
      {databaseAdmin && (path.startsWith('/engineering') || path.startsWith('/admin/database')) ? (
        <nav className="engineering-tools-navigation" aria-label={text.engineering}>
          <a
            href="/engineering/database-topology"
            className={path.startsWith('/engineering/database-topology') || path.startsWith('/admin/database') ? 'active' : undefined}
            aria-current={path.startsWith('/engineering/database-topology') || path.startsWith('/admin/database') ? 'page' : undefined}
          >{databaseText.title}</a>
        </nav>
      ) : null}
      {(path.startsWith('/runtime/history') || path.startsWith('/runtime/reports')) && access.runtime && access.history && (
        <nav className="runtime-view-navigation" aria-label="Runtime views">
          <a href="/" className={activeRuntimeHref === '/' ? 'active' : undefined} aria-current={activeRuntimeHref === '/' ? 'page' : undefined}>{text.runtimeOverview}</a>
          <a href="/runtime/history" className={activeRuntimeHref === '/runtime/history' ? 'active' : undefined} aria-current={activeRuntimeHref === '/runtime/history' ? 'page' : undefined}>{text.runtimeHistory}</a>
          <a href="/runtime/reports" className={activeRuntimeHref === '/runtime/reports' ? 'active' : undefined} aria-current={activeRuntimeHref === '/runtime/reports' ? 'page' : undefined}>{locale === 'pt-BR' ? 'Relatórios' : locale === 'es' ? 'Informes' : 'Reports'}</a>
        </nav>
      )}
    </>
  );
}
