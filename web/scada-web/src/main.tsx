import React from 'react';
import { createRoot } from 'react-dom/client';
import { AppNavigation } from './AppNavigation';
import { AuditApp } from './audit';
import { AuthGate } from './auth/AuthGate';
import { EngineeringApp } from './engineering/EngineeringApp';
import { LicensingApp } from './licensing/LicensingApp';
import { RuntimeApplicationMount } from './runtime/application/RuntimeApplicationMount';
import { HistoricalDataBrowserRuntime } from './runtime/historical-browser/HistoricalDataBrowserRuntime';
import './styles.css';

function RuntimeHistoricalBrowserApp() {
  return (
    <main className="shell runtime-history-page">
      <HistoricalDataBrowserRuntime />
    </main>
  );
}

const RootApp = window.location.pathname.startsWith('/audit')
  ? AuditApp
  : window.location.pathname.startsWith('/engineering')
    ? EngineeringApp
    : window.location.pathname.startsWith('/licensing')
      ? LicensingApp
      : window.location.pathname.startsWith('/runtime/history')
        ? RuntimeHistoricalBrowserApp
        : RuntimeApplicationMount;

createRoot(document.getElementById('root')!).render(
  <AuthGate>
    <>
      <AppNavigation />
      <RootApp />
    </>
  </AuthGate>
);
