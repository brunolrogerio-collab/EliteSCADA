import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { EngineeringResourceOrganizer } from '../../src/engineering/EngineeringResourceOrganizer';
import { TagEditor } from '../../src/engineering/SecuredEngineeringEditors';
import type { EngineeringPackageView } from '../../src/engineering/types';
import '../../src/engineering/structured-editors.css';

const kinds = ['screens', 'templates', 'popups', 'tags', 'dataSources', 'mediaSources', 'alarms', 'operationalEvents', 'gatewayRoutes'] as const;
type Resource = { key: string; name: string };

function Harness() {
  const [kind, setKind] = useState<typeof kinds[number]>('tags');
  const [resources, setResources] = useState<Resource[]>([{ key: 'one', name: 'Original' }]);
  const [selected, setSelected] = useState<string | null>('one');
  const [dark, setDark] = useState(false);
  const organizer = <EngineeringResourceOrganizer projectKey="organizer-regression" kind={kind} locale="pt-BR" label="Recursos"
    resources={resources.map(value => ({ identity: value.key, name: value.name, value }))}
    selectedIdentity={selected} onSelect={setSelected}
    onPaste={value => setResources(current => [...current, { key: `copy-${current.length}`, name: `${value.name} (cópia)` }])} />;
  return <main style={{ padding: 20, fontFamily: 'Arial', color: dark ? '#edf4fa' : '#243447' }}>
    <label>Tipo <select aria-label="Tipo" value={kind} onChange={event => setKind(event.currentTarget.value as typeof kind)}>{kinds.map(item => <option key={item}>{item}</option>)}</select></label>
    <label><input type="checkbox" checked={dark} onChange={event => setDark(event.currentTarget.checked)} />Escuro</label>
    <div className="eng-shell" data-theme={dark ? 'dark' : 'light'} style={{ '--eng-text': dark ? '#edf4fa' : '#243447', '--eng-panel': dark ? '#141c25' : '#ffffff', '--eng-input-bg': dark ? '#0b1219' : '#ffffff', '--eng-border': '#9aaabc', '--eng-muted': dark ? '#bacbd9' : '#657080' } as React.CSSProperties}>
      {['tags', 'alarms', 'dataSources'].includes(kind) ? <aside className="eng-editor-picker" style={{ height: 500, width: 300 }}>
        <header><strong>Lista</strong></header><div className="eng-editor-picker-list" data-testid="list-surface">{organizer}</div>
      </aside> : <aside className={kind === 'operationalEvents' ? 'eng-resource-sidebar' : kind === 'gatewayRoutes' ? 'gateway-route-inventory' : 'visual-editor-screens'} style={{ height: 500, width: 300, display: 'flex', flexDirection: 'column' }}>
        <header>Lista</header>{organizer}
      </aside>}
    </div>
  </main>;
}

const tagModel = {
  schema: 'scada.engineering', schemaVersion: 21, tags: [
    { id: '11111111-1111-4111-8111-111111111111', name: 'Motor', path: 'Plant.Motor', dataType: 'boolean', readOnly: false },
    { id: '22222222-2222-4222-8222-222222222222', name: 'Válvula', path: 'Plant.Valve', dataType: 'boolean', readOnly: false }
  ], alarms: [], dataSources: [], screens: [], popups: [], templates: [], equipment: [], dynamos: [], visualAssets: [], securityRoles: [], gateways: []
} as unknown as EngineeringPackageView;
createRoot(document.getElementById('root')!).render(location.search.includes('tag-editor')
  ? <TagEditor model={tagModel} locale="pt-BR" projectKey="tag-organizer-regression" /> : <Harness />);
