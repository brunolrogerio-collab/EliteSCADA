import type { RuntimeHmiEngineeringPackage } from './runtimeApplicationApi';

export type RuntimeStartupScreenResolution = Readonly<{
  screenKey: string;
  diagnosticCode: 'HMI_RUNTIME_STARTUP_SCREEN_REQUIRED' | 'HMI_RUNTIME_STARTUP_SCREEN_UNRESOLVED' | null;
  detail: string | null;
}>;

export function resolveRuntimeStartupScreen(
  engineeringPackage: Pick<RuntimeHmiEngineeringPackage, 'startupScreenId' | 'screens'>
): RuntimeStartupScreenResolution {
  const startupScreenId = engineeringPackage.startupScreenId?.trim() ?? '';
  if (!startupScreenId) {
    return Object.freeze({
      screenKey: '',
      diagnosticCode: 'HMI_RUNTIME_STARTUP_SCREEN_REQUIRED',
      detail: 'Active project does not define a Startup/Home Screen.'
    });
  }

  const normalizedStartupId = startupScreenId.toLocaleLowerCase('en-US');
  const screen = (engineeringPackage.screens ?? []).find(candidate =>
    candidate.id?.toLocaleLowerCase('en-US') === normalizedStartupId);
  if (!screen?.key?.trim()) {
    return Object.freeze({
      screenKey: '',
      diagnosticCode: 'HMI_RUNTIME_STARTUP_SCREEN_UNRESOLVED',
      detail: `Configured Startup/Home Screen '${startupScreenId}' is not present in the Active project.`
    });
  }

  return Object.freeze({
    screenKey: screen.key,
    diagnosticCode: null,
    detail: null
  });
}
