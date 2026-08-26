import type { EngineeringPackageView, EngineeringSnapshot, EngineeringWorkspaceDescriptor } from './types';

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    headers: { accept: 'application/json' }
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return await response.json() as T;
}

export async function loadEngineeringSnapshot(): Promise<EngineeringSnapshot> {
  const [workspace, engineeringPackage] = await Promise.all([
    getJson<EngineeringWorkspaceDescriptor>('/api/engineering/workspace'),
    getJson<EngineeringPackageView>('/api/engineering/export/json')
  ]);

  return {
    workspace,
    package: {
      ...engineeringPackage,
      tags: engineeringPackage.tags ?? [],
      alarms: engineeringPackage.alarms ?? [],
      dataSources: engineeringPackage.dataSources ?? [],
      templates: engineeringPackage.templates ?? [],
      equipment: engineeringPackage.equipment ?? [],
      dynamos: engineeringPackage.dynamos ?? [],
      screens: engineeringPackage.screens ?? [],
      popups: engineeringPackage.popups ?? [],
      securityRoles: engineeringPackage.securityRoles ?? []
    }
  };
}
