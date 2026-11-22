import React from 'react';
import { createRoot } from 'react-dom/client';
import { LicensingApp } from '../src/licensing/LicensingApp';
import '../src/styles.css';
import '../src/app-theme.css';

const root = document.getElementById('root');
if (!root) throw new Error('Licensing harness root not found.');
createRoot(root).render(<LicensingApp />);
