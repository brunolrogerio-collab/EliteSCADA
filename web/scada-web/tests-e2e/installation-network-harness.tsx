import React from 'react';
import { createRoot } from 'react-dom/client';
import { InstallationNetworkWorkspace } from '../src/engineering/InstallationNetworkWorkspace';
import '../src/app-theme.css';

const root = document.getElementById('root');
if (!root) throw new Error('Installation Network harness root not found.');

const requestedLocale = new URLSearchParams(window.location.search).get('locale');
const locale = requestedLocale === 'pt-BR' || requestedLocale === 'es' ? requestedLocale : 'en';
window.localStorage.setItem('elitescada.engineering.locale', locale);
document.documentElement.lang = locale;

createRoot(root).render(
  <React.StrictMode>
    <InstallationNetworkWorkspace locale={locale} isSystemAdmin={true} />
  </React.StrictMode>
);
