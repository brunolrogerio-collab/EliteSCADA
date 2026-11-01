import React from 'react';
import { createRoot } from 'react-dom/client';
import { RuntimeAlarmCenter } from '../src/runtime/RuntimeAlarmCenter';
import { setActiveRuntimeSession } from '../src/runtime/runtimeSessionAdmissionApi';

const root = document.getElementById('root');
if (!root) throw new Error('Alarm suppression harness root not found.');

setActiveRuntimeSession({
  sessionId: 'runtime-session-e2e',
  clientInstanceId: 'runtime-client-e2e',
  headers: { 'X-EliteSCADA-Runtime-Session': 'runtime-session-e2e' },
  requestedClass: 'interactive',
  grantedClass: 'interactive',
  admissionReasonCode: null,
  capacityReasonCode: null
});

createRoot(root).render(
  <React.StrictMode>
    <RuntimeAlarmCenter locale="pt-BR" refreshIntervalMs={60_000} />
  </React.StrictMode>
);
