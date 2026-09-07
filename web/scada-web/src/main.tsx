import React from 'react';
import { createRoot } from 'react-dom/client';
import { AppNavigation } from './AppNavigation';
import { appShellText, useAppShellLocale } from './appShellI18n';
import { initializeAppTheme } from './appTheme';
import { AuditApp } from './audit';
import { AuthGate } from './auth/AuthGate';
import {
  resolveAppSurfaceAccess,
  useEffectiveCapabilities
} from './auth/effectiveCapabilities';
import { EngineeringLockGate } from './engineering/EngineeringLockGate';
import { ContextualHelpApp } from './help/ContextualHelpApp';
import { LicensingApp } from './licensing/LicensingApp';
import { RuntimeApplicationMount } from './runtime/application/RuntimeApplicationMount';
import { HistoricalDataBrowserRuntime } from './runtime/historical-browser/HistoricalDataBrowserRuntime';
import './styles.css';
import './app-theme.css';
import './runtime/application/runtime-operator.css';

initializeAppTheme();

function RuntimeHistoricalBrowserApp() {
  const locale = useAppShellLocale();
  return (
    <main className="shell runtime-history-page">
      <HistoricalDataBrowserRuntime locale={locale} />
    </main>
  );
}

function ApplicationSurface() {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const { capabilities, loading, error } = useEffectiveCapabilities();
  const path = window.location.pathname;

  if (loading) return <main className="shell app-route-state" aria-busy="true" />;
  if (error || !capabilities) {
    return <main className="shell app-route-state" role="alert">{text.capabilitiesUnavailable}</main>;
  }

  const access = resolveAppSurfaceAccess(capabilities);
  const anySurface = access.runtime || access.engineering || access.audit || access.licensing;

  let allowed = access.runtime;
  let Surface: React.ComponentType = RuntimeApplicationMount;
  if (path.startsWith('/help')) {
    allowed = anySurface;
    Surface = ContextualHelpApp;
  } else if (path.startsWith('/audit')) {
    allowed = access.audit;
    Surface = AuditApp;
  } else if (path.startsWith('/engineering')) {
    // Authority decides whether the user may enter the Engineering route. The backend
    // Engineering Lock then decides whether protected application editors may mount.
    allowed = access.engineering;
    Surface = EngineeringLockGate;
  } else if (path.startsWith('/licensing')) {
    allowed = access.licensing;
    Surface = LicensingApp;
  } else if (path.startsWith('/runtime/history')) {
    allowed = access.runtime && access.history;
    Surface = RuntimeHistoricalBrowserApp;
  }

  if (!allowed) {
    return <main className="shell app-route-state" role="alert">
      <strong>{text.accessDenied}</strong>
      {access.runtime ? <a href="/">{text.runtime}</a> : null}
    </main>;
  }

  return <Surface />;
}

createRoot(document.getElementById('root')!).render(
  <AuthGate>
    <>
      <AppNavigation />
      <ApplicationSurface />
    </>
  </AuthGate>
);
