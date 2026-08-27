import type { EngineeringLifecycleAction } from './engineeringLifecycleTypes';

export function engineeringLifecycleRequestBody(
  action: EngineeringLifecycleAction,
  projectName?: string
): Record<string, unknown> | undefined {
  if (action === 'save') return { projectName: projectName ?? '' };
  if (action === 'publish' || action === 'activate') return {};
  return undefined;
}
