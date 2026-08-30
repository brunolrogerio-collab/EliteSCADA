import React from 'react';
import { createRoot } from 'react-dom/client';
import { HistoricalDataBrowserRuntime } from '../src/runtime/historical-browser/HistoricalDataBrowserRuntime';

const root = document.getElementById('root');
if (!root) throw new Error('Historical Browser harness root not found.');

createRoot(root).render(
  <React.StrictMode>
    <HistoricalDataBrowserRuntime />
  </React.StrictMode>
);
