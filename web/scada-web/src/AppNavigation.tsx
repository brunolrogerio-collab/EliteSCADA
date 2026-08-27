import React from 'react';
import './app-navigation.css';

const links = [
  { href: '/', label: 'Runtime', description: 'Operação' },
  { href: '/engineering', label: 'Engineering', description: 'Projeto' },
  { href: '/audit', label: 'Audit', description: 'Rastreabilidade' }
];

export function AppNavigation() {
  const path = window.location.pathname;
  const activeHref = path.startsWith('/audit')
    ? '/audit'
    : path.startsWith('/engineering')
      ? '/engineering'
      : '/';

  return (
    <header className="app-bar">
      <a className="app-brand" href="/" aria-label="EliteSCADA Runtime">
        <span className="app-brand-mark" aria-hidden="true">E</span>
        <span className="app-brand-copy">
          <strong>EliteSCADA</strong>
          <small>Industrial Platform</small>
        </span>
      </a>

      <nav className="app-navigation" aria-label="EliteSCADA">
        {links.map(link => {
          const active = activeHref === link.href;
          return (
            <a
              key={link.href}
              href={link.href}
              className={active ? 'active' : undefined}
              aria-current={active ? 'page' : undefined}
            >
              <span>{link.label}</span>
              <small>{link.description}</small>
            </a>
          );
        })}
      </nav>

      <div className="app-context" aria-label="Área atual">
        <span>Área atual</span>
        <strong>{links.find(link => link.href === activeHref)?.label ?? 'Runtime'}</strong>
      </div>
    </header>
  );
}
