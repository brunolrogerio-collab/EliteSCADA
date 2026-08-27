import React, { useEffect, useState } from 'react';
import './app-navigation.css';

type BuildInfo = {
  version?: string;
  commit?: string;
};

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
  const [build, setBuild] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    void fetch('/build-info.json', { signal: controller.signal, cache: 'no-store' })
      .then(async response => {
        if (!response.ok) return null;
        return await response.json() as BuildInfo;
      })
      .then(info => {
        if (!info) return;
        const version = info.version?.trim();
        const commit = info.commit?.trim();
        const shortCommit = commit && commit !== 'unknown' ? commit.slice(0, 8) : null;
        const label = [version ? `v${version}` : null, shortCommit].filter(Boolean).join(' · ');
        if (label) setBuild(label);
      })
      .catch(error => {
        if ((error as Error).name !== 'AbortError') setBuild(null);
      });
    return () => controller.abort();
  }, []);

  return (
    <nav className="app-navigation" aria-label="EliteSCADA">
      {build && <span className="app-build" title="Identificação do build">{build}</span>}
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
