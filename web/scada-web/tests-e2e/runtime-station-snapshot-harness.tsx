import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { RuntimeStationEngineeringWorkspace } from '../src/engineering/RuntimeStationEngineeringWorkspace';
import type { EngineeringSnapshot } from '../src/engineering/types';
import '../src/app-theme.css';

const initial = {
  workspace: { projectKey: 'snapshot-stations', projectName: 'Estação de teste', changeVersion: 1, isDirty: false },
  package: {
    screens: [
      { id: 'screen-home', key: 'home', name: 'Visão geral', elements: [] },
      { id: 'screen-pumps', key: 'pumps', name: 'Bombas', elements: [] }
    ],
    runtimePresentation: { version: 1, historicalPlaybackEnabled: false, stationProfiles: [] }
  }
} as unknown as EngineeringSnapshot;

function StationSnapshotHarness() {
  const [snapshot, setSnapshot] = useState(initial);
  const refresh = () => {
    setSnapshot(current => ({
      ...current,
      workspace: { ...current.workspace, changeVersion: current.workspace.changeVersion + 1 },
      package: {
        ...current.package,
        runtimePresentation: current.package.runtimePresentation
          ? { ...current.package.runtimePresentation, stationProfiles: [...(current.package.runtimePresentation.stationProfiles ?? [])] }
          : null
      }
    }));
  };
  const remoteStationChange = () => {
    setSnapshot(current => ({
      ...current,
      workspace: { ...current.workspace, changeVersion: current.workspace.changeVersion + 1 },
      package: {
        ...current.package,
        runtimePresentation: {
          version: 1,
          historicalPlaybackEnabled: false,
          stationProfiles: [{ id: 'remote-profile', name: 'Estação alterada por outro editor', windows: [{ screenKey: 'home', mode: 'navigation' }] }]
        }
      }
    }));
  };
  return <main>
    <button data-testid="simulate-station-snapshot-refresh" onClick={refresh}>Simular atualização do snapshot</button>
    <button data-testid="simulate-station-remote-change" onClick={remoteStationChange}>Simular mudança remota</button>
    <RuntimeStationEngineeringWorkspace snapshot={snapshot} onApplied={async () => {}} locale="pt-BR" />
  </main>;
}

const root = document.getElementById('root');
if (!root) throw new Error('Missing Runtime station harness root');
createRoot(root).render(<StationSnapshotHarness />);
