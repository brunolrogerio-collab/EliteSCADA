import React from 'react';
import './app-navigation.css';

const links = [
  { href: '/', label: 'Runtime' },
  { href: '/engineering', label: 'Engineering', ariaLabel: 'Project workspace' },
  { href: '/audit', label: 'Audit' }
];

export function AppNavigation() {
  const path = window.location.pathname;
  const activeHref = path.startsWith('/audit')
    ? '/audit'
    : path.startsWith('/engineering')
      ? '/engineering'
      : '/';

  return (
    <nav className="app-navigation" aria-label="EliteSCADA">
      {links.map(link => (
        <a
          key={link.href}
          href={link.href}
          aria-label={link.ariaLabel}
          className={activeHref === link.href ? 'active' : undefined}
          aria-current={activeHref === link.href ? 'page' : undefined}
        >
          {link.label}
        </a>
      ))}
    </nav>
  );
}
