import React from 'react';
import { createRoot } from 'react-dom/client';
import { RuntimeAlarmCenter } from '../src/runtime/RuntimeAlarmCenter';
import { loadRuntimeAlarms } from '../src/runtime/alarmCenterApi';
import { HistoricalDataBrowserRuntime } from '../src/runtime/historical-browser/HistoricalDataBrowserRuntime';

const root = document.getElementById('root');
if (!root) throw new Error('Historical Browser harness root not found.');

const params = new URLSearchParams(window.location.search);
const requestedLocale = params.get('locale');
const saved = params.has('saved');
const locale = requestedLocale === 'pt-BR' || requestedLocale === 'es' ? requestedLocale : 'en';

createRoot(root).render(
  <React.StrictMode>
    {params.has('alarm-center') ? <RuntimeAlarmCenter locale={locale} refreshIntervalMs={0}
      loader={signal => loadRuntimeAlarms(true, signal, 'high-only')} /> : <HistoricalDataBrowserRuntime locale={locale}
      savedQueries={saved ? [{ key: 'shared-measurements', name: 'Shared measurements' }] : undefined}
      alarmViews={saved ? [{ key: 'high-only', name: 'High only' }] : undefined}
      alarmViewKey={saved ? 'high-only' : undefined} />}
  </React.StrictMode>
);
