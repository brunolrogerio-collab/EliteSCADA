import React from 'react';
import { createRoot } from 'react-dom/client';
import { HighAvailabilityAdminWorkspace } from '../src/engineering/ha/HighAvailabilityAdminWorkspace';
import type { HaLocale } from '../src/engineering/ha/types';

const root = document.getElementById('root');
if (!root) throw new Error('HA admin harness root not found.');

const requested = new URLSearchParams(window.location.search).get('locale');
const locale: HaLocale = requested === 'en' || requested === 'es' ? requested : 'pt-BR';

createRoot(root).render(
  <React.StrictMode>
    <HighAvailabilityAdminWorkspace locale={locale} />
  </React.StrictMode>
);
