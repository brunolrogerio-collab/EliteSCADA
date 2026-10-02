let historicalPlaybackActive = false;

export class RuntimeHistoricalPlaybackReadOnlyError extends Error {
  constructor(public readonly operation: string) {
    super(`Runtime operation '${operation}' is blocked while Historical Playback is active.`);
    this.name = 'RuntimeHistoricalPlaybackReadOnlyError';
  }
}

/**
 * Session-local Runtime safety boundary. Playback is a client-side temporal
 * projection, so every mutable Runtime adapter must fail closed before it
 * acquires a Runtime lease or issues a process-changing request.
 */
export function setRuntimeHistoricalPlaybackActive(active: boolean): void {
  historicalPlaybackActive = active;
}

export function isRuntimeHistoricalPlaybackActive(): boolean {
  return historicalPlaybackActive;
}

export function assertRuntimeProcessMutationAllowed(operation: string): void {
  if (historicalPlaybackActive) {
    throw new RuntimeHistoricalPlaybackReadOnlyError(operation);
  }
}
