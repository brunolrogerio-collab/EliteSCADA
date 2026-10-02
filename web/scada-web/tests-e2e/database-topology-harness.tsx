import React from 'react';
import { createRoot } from 'react-dom/client';
import { DatabaseTopologyApp } from '../src/database-topology';

const root = document.getElementById('root');
if (!root) throw new Error('Database Topology harness root not found.');

const requestedLocale = new URLSearchParams(window.location.search).get('locale');
const locale = requestedLocale === 'pt-BR' || requestedLocale === 'es' ? requestedLocale : 'en';
window.localStorage.setItem('elitescada.engineering.locale', locale);
document.documentElement.lang = locale;

createRoot(root).render(
  <React.StrictMode>
    <DatabaseTopologyApp />
  </React.StrictMode>
);
