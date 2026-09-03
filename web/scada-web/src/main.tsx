import React from 'react';
import { createRoot } from 'react-dom/client';
import { AppNavigation } from './AppNavigation';
import { appShellText, resolveAppShellLocale } from './appShellI18n';
import { initializeAppTheme } from './appTheme';
import { AuditApp } from './audit';
import { AuthGate } from './auth/AuthGate';
import {
  hasRuntimeCapability,
  hasWorkspaceCapability,
  useEffectiveCapabilities
} from './auth/effectiveCapabilities';
import { EngineeringApp } from './engineering/EngineeringApp';
import { LicensingApp } from './licensing/LicensingApp';
import { RuntimeApplicationMount } from './runtime/application/RuntimeApplicationMount';
import { HistoricalDataBrowserRuntime } from './runtime/historical-browser/HistoricalDataBrowserRuntime';
import './styles.css';
import './app-theme.css';
import './runtime/application/runtime-operator.css';

initializeAppTheme();

function RuntimeHistoricalBrowserApp() {
  return (
    <main className="shell runtime-history-page">
      <HistoricalDataBrowserRuntime />
    </main>
  );
}

function ApplicationSurface() {
  const locale = resolveAppShellLocale();
  const text = appShellText(locale);
  const { capabilities, loading, error } = useEffectiveCapabilities();
  const path = window.location.pathname;

  if (loading) return <main className="shell app-route-state" aria-busy="true" />;
  if (error || !capabilities) {
    return <main className="shell app-route-state" role="alert">{text.capabilitiesUnavailable}</main>;
  }

  const canRuntime = hasRuntimeCapability(capabilities, 'View');
  const canEngineering = hasWorkspaceCapability(capabilities, 'EngineeringModify');
  const canSystemAdmin = hasRuntimeCapability(capabilities, 'SystemAdmin');
  const canHistory = hasRuntimeCapability(capabilities, 'TrendUse') || canEngineering || canSystemAdmin;

  let allowed = canRuntime;
  let Surface: React.ComponentType = RuntimeApplicationMount;
  if (path.startsWith('/audit')) {
    allowed = canSystemAdmin;
    Surface = AuditApp;
  } else if (path.startsWith('/engineering')) {
    allowed = canEngineering;
    Surface = EngineeringApp;
  } else if (path.startsWith('/licensing')) {
    allowed = canSystemAdmin;
    Surface = LicensingApp;
  } else if (path.startsWith('/runtime/history')) {
    allowed = canRuntime && canHistory;
    Surface = RuntimeHistoricalBrowserApp;
  }

  if (!allowed) {
    return <main className="shell app-route-state" role="alert">
      <strong>{text.accessDenied}</strong>
      {canRuntime ? <a href="/">{text.runtime}</a> : null}
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
