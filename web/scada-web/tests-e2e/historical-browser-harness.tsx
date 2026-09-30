import React from 'react';
import { createRoot } from 'react-dom/client';
import { HistoricalDataBrowserRuntime } from '../src/runtime/historical-browser/HistoricalDataBrowserRuntime';

const root = document.getElementById('root');
if (!root) throw new Error('Historical Browser harness root not found.');

const requestedLocale = new URLSearchParams(window.location.search).get('locale');
const locale = requestedLocale === 'pt-BR' || requestedLocale === 'es' ? requestedLocale : 'en';

createRoot(root).render(
  <React.StrictMode>
    <HistoricalDataBrowserRuntime locale={locale} />
  </React.StrictMode>
);
