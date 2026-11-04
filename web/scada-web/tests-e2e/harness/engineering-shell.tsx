import React from 'react';
import { createRoot } from 'react-dom/client';
import { EngineeringApp } from '../../src/engineering/EngineeringApp';
window.history.replaceState(null, '', '/engineering/intelligence');
createRoot(document.getElementById('root')!).render(<EngineeringApp/>);
