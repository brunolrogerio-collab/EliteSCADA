import React from 'react';
import { createRoot } from 'react-dom/client';
import { AppNavigation } from './AppNavigation';
import { appShellText, useAppShellLocale } from './appShellI18n';
import { initializeAppTheme } from './appTheme';
import { AuditApp } from './audit';
import { AuthGate } from './auth/AuthGate';
import {
  hasRuntimeCapability,
  resolveAppSurfaceAccess,
  useEffectiveCapabilities
} from './auth/effectiveCapabilities';
import { EngineeringLockGate } from './engineering/EngineeringLockGate';
import { DatabaseTopologyApp } from './database-topology';
import { ContextualHelpApp } from './help/ContextualHelpApp';
import { LicensingApp } from './licensing/LicensingApp';
import { RuntimeApplicationMount } from './runtime/application/RuntimeApplicationMount';
import { RuntimeSessionClassProvider } from './runtime/application/RuntimeSessionClassPanel';
import { HistoricalDataBrowserRuntime } from './runtime/historical-browser/HistoricalDataBrowserRuntime';
import { RuntimeReportCenter } from './runtime/reports/RuntimeReportCenter';
import { useMobileRuntime } from './runtime/useMobileRuntime';
import './styles.css';
import './visual-runtime/visual-editor-fonts.css';
import './app-theme.css';
import './engineering/engineering-control-states.css';
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

function RuntimeReportsApp() {
  const locale = useAppShellLocale();
  return <RuntimeReportCenter locale={locale} />;
}

function ApplicationSurface() {
  const mobile = useMobileRuntime();
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const { capabilities, loading, error } = useEffectiveCapabilities();
  const path = window.location.pathname;

  if (loading) return <main className="shell app-route-state" aria-busy="true" />;
  if (error || !capabilities) {
    return <main className="shell app-route-state" role="alert">{text.capabilitiesUnavailable}</main>;
  }

  const access = resolveAppSurfaceAccess(capabilities);
  const databaseAdmin = hasRuntimeCapability(capabilities, 'SystemAdmin');
  const anySurface = access.runtime || access.engineering || access.audit || access.licensing || databaseAdmin;
  const privilegedShell = access.engineering || access.audit || access.licensing || databaseAdmin;
  // Mobile is a Runtime presentation, not an authorization bypass. Privileged
  // accounts on mobile do not mount desktop authoring or administration screens.
  if (mobile && access.runtime) return <RuntimeApplicationMount showHistoryNavigation={access.history} showFullscreenControl={false}/>;

  let allowed = access.runtime;
  let Surface: React.ComponentType = RuntimeApplicationMount;
  if (path.startsWith('/admin/database') || path.startsWith('/engineering/database-topology')) {
    allowed = databaseAdmin;
    Surface = DatabaseTopologyApp;
  } else if (path.startsWith('/help')) {
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
  } else if (path.startsWith('/runtime/reports')) {
    allowed = access.runtime && access.history;
    Surface = RuntimeReportsApp;
  }

  if (!allowed) {
    return <main className="shell app-route-state" role="alert">
      <strong>{text.accessDenied}</strong>
      {access.runtime ? <a href="/">{text.runtime}</a> : null}
    </main>;
  }

  if (path === '/') return <RuntimeApplicationMount
    showHistoryNavigation={access.history}
    showFullscreenControl={privilegedShell}
  />;
  return <Surface />;
}

createRoot(document.getElementById('root')!).render(
  <AuthGate>
    <RuntimeSessionClassProvider>
      <>
        <AppNavigation />
        <ApplicationSurface />
      </>
    </RuntimeSessionClassProvider>
  </AuthGate>
);
